using System.Text.RegularExpressions;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace LSE.AutoCADElectrical.ProjectEnvironment;

public static class WireNumberDrawingService
{
    public static DrawingWires Read(string path, EnvRules rules)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Drawing was not found.", path);
        }

        Document? open = DrawingSettingsService.FindOpenDocument(path);
        if (open is not null)
        {
            using Transaction editorTransaction = open.Database.TransactionManager.StartTransaction();
            return Read(open.Database, editorTransaction, rules, path);
        }

        using var database = DrawingSettingsService.Open(path);
        using Transaction transaction = database.TransactionManager.StartTransaction();
        return Read(database, transaction, rules, path);
    }

    public static DrawingWires Read(Database database, Transaction transaction, EnvRules rules, string path)
    {
        FromToRules fromTo = rules.FromTo;
        var result = new DrawingWires { Path = path };
        var connection = new Regex(fromTo.ConnectionAttributePattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);

        BlockReference? settings = DrawingSettingsService.FindBlock(database, transaction, rules.WdmBlockName);
        if (settings is not null)
        {
            var values = ReadAttributes(settings, transaction);
            result.WireLayerPatterns = Split(Value(values, "WIRELAYS"));
            result.WireNoLayer = Value(values, "WIRENO_LAY").Trim().Length > 0 ? Value(values, "WIRENO_LAY").Trim() : result.WireNoLayer;
            result.FixedLayer = Value(values, "WIREFIXED_LAY").Trim().Length > 0 ? Value(values, "WIREFIXED_LAY").Trim() : result.FixedLayer;
            result.Sheet = Value(values, "SHEET").Trim();
        }
        else
        {
            result.Notes.Add("no " + rules.WdmBlockName + " block; every LINE on every layer is treated as a wire");
        }

        if (result.WireLayerPatterns.Count == 0)
        {
            result.Notes.Add("WIRELAYS is blank, so every layer counts as a wire layer");
        }

        var modelSpace = (BlockTableRecord)transaction.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForRead);
        foreach (ObjectId entityId in modelSpace)
        {
            DBObject entity = transaction.GetObject(entityId, OpenMode.ForRead);
            switch (entity)
            {
                case Line line:
                    if (IsWireLayer(line.Layer, result.WireLayerPatterns))
                    {
                        result.Segments.Add(new WireSegment { A = new Pt(line.StartPoint.X, line.StartPoint.Y), B = new Pt(line.EndPoint.X, line.EndPoint.Y), Layer = line.Layer, Handle = line.Handle.ToString() });
                    }

                    break;
                case Polyline polyline:
                    if (IsWireLayer(polyline.Layer, result.WireLayerPatterns))
                    {
                        int count = polyline.Closed ? polyline.NumberOfVertices : polyline.NumberOfVertices - 1;
                        for (int i = 0; i < count; i++)
                        {
                            if (polyline.GetSegmentType(i) != SegmentType.Line)
                            {
                                continue;
                            }

                            LineSegment2d segment = polyline.GetLineSegment2dAt(i);
                            result.Segments.Add(new WireSegment { A = new Pt(segment.StartPoint.X, segment.StartPoint.Y), B = new Pt(segment.EndPoint.X, segment.EndPoint.Y), Layer = polyline.Layer, Handle = polyline.Handle.ToString() });
                        }
                    }

                    break;
                case BlockReference reference:
                    ReadBlock(reference, transaction, fromTo, connection, result);
                    break;
            }
        }

        return result;
    }

    private static void ReadBlock(BlockReference reference, Transaction transaction, FromToRules fromTo, Regex connection, DrawingWires result)
    {
        string name = DrawingSettingsService.EffectiveName(reference, transaction);
        var attributes = new Dictionary<string, (string Text, Pt Point, string Layer)>(StringComparer.OrdinalIgnoreCase);
        foreach (ObjectId attributeId in reference.AttributeCollection)
        {
            if (transaction.GetObject(attributeId, OpenMode.ForRead) is AttributeReference attribute)
            {
                Point3d position = attribute.Justify == AttachmentPoint.BaseLeft ? attribute.Position : attribute.AlignmentPoint;
                attributes[attribute.Tag] = (attribute.TextString, new Pt(position.X, position.Y), attribute.Layer);
            }
        }

        bool hasNumber = attributes.TryGetValue(fromTo.WireNumberAttribute, out var number);
        bool numberLayer = reference.Layer.Equals(result.WireNoLayer, StringComparison.OrdinalIgnoreCase) || reference.Layer.Equals(result.FixedLayer, StringComparison.OrdinalIgnoreCase);
        if (WireNetworkService.MatchesAny(name, fromTo.WireNumberBlocks) || (hasNumber && numberLayer))
        {
            string layer = hasNumber && number.Layer.Length > 0 ? number.Layer : reference.Layer;
            result.Marks.Add(new WireNumberMark
            {
                Handle = reference.Handle.ToString(),
                Point = new Pt(reference.Position.X, reference.Position.Y),
                Number = hasNumber ? number.Text : "",
                Layer = reference.Layer,
                Block = name,
                IsFixed = layer.Equals(result.FixedLayer, StringComparison.OrdinalIgnoreCase) || reference.Layer.Equals(result.FixedLayer, StringComparison.OrdinalIgnoreCase),
            });
            return;
        }

        if (WireNetworkService.MatchesAny(name, fromTo.IgnoreBlocks))
        {
            return;
        }

        var connections = new List<(string Digits, Pt Point)>();
        foreach (var (tag, value) in attributes)
        {
            Match match = connection.Match(tag);
            if (match.Success)
            {
                connections.Add((match.Groups.Count > 1 ? match.Groups[1].Value : "", value.Point));
            }
        }

        if (connections.Count == 0)
        {
            return;
        }

        if (attributes.TryGetValue(fromTo.SignalCodeAttribute, out var signal) && signal.Text.Trim().Length > 0)
        {
            foreach (var (_, point) in connections)
            {
                result.Signals.Add(new SignalArrow { Code = signal.Text.Trim(), Point = point, Handle = reference.Handle.ToString(), Block = name });
            }

            return;
        }

        string deviceTag = "";
        foreach (string tagAttribute in fromTo.TagAttributes)
        {
            if (attributes.TryGetValue(tagAttribute, out var candidate) && candidate.Text.Trim().Length > 0)
            {
                deviceTag = candidate.Text.Trim();
                break;
            }
        }

        if (deviceTag.Length == 0)
        {
            result.UntaggedConnections += connections.Count;
            return;
        }

        bool terminalStrip = attributes.TryGetValue(fromTo.TerminalStripAttribute, out var strip) && strip.Text.Trim().Length > 0;
        foreach (var (digits, point) in connections)
        {
            string pin = attributes.TryGetValue(fromTo.PinAttributePrefix + digits, out var pinValue) ? pinValue.Text.Trim() : "";
            result.Terminals.Add(new DeviceTerminal { Tag = deviceTag, Pin = pin, Point = point, Block = name, Handle = reference.Handle.ToString(), IsTerminalStrip = terminalStrip });
        }
    }

    public static List<string> Write(Database database, Transaction transaction, WirePlan plan, FromToRules fromTo, IReadOnlyList<string> libraryPaths)
    {
        var log = new List<string>();
        string targetLayer = plan.Fixed ? plan.FixedLayer : plan.WireNoLayer;
        EnsureLayer(database, transaction, targetLayer, plan.WireNoLayer, log);

        var modelSpace = (BlockTableRecord)transaction.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForWrite);
        ObjectId definitionId = ObjectId.Null;
        bool definitionLookedUp = false;
        int set = 0, inserted = 0, skipped = 0;

        foreach (WireNetwork network in plan.Networks)
        {
            if (network.Action == WireAction.Set)
            {
                foreach (WireNumberMark mark in network.Marks)
                {
                    if (!TryGet(database, mark.Handle, out ObjectId markId) || transaction.GetObject(markId, OpenMode.ForRead) is not BlockReference reference)
                    {
                        log.Add("wire number block " + mark.Handle + " no longer exists; network " + network.Id + " skipped");
                        skipped++;
                        continue;
                    }

                    bool found = false;
                    foreach (ObjectId attributeId in reference.AttributeCollection)
                    {
                        if (transaction.GetObject(attributeId, OpenMode.ForRead) is not AttributeReference attribute || !attribute.Tag.Equals(fromTo.WireNumberAttribute, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        attribute.UpgradeOpen();
                        attribute.TextString = network.Proposed;
                        attribute.Layer = targetLayer;
                        found = true;
                    }

                    if (!found)
                    {
                        log.Add("wire number block " + mark.Handle + " has no " + fromTo.WireNumberAttribute + " attribute; network " + network.Id + " skipped");
                        skipped++;
                        continue;
                    }

                    reference.UpgradeOpen();
                    reference.Layer = targetLayer;
                    set++;
                }
            }
            else if (network.Action == WireAction.Insert)
            {
                if (!definitionLookedUp)
                {
                    definitionLookedUp = true;
                    definitionId = FindOrImportDefinition(database, transaction, fromTo.InsertBlockName, libraryPaths, log);
                }

                if (definitionId.IsNull)
                {
                    skipped++;
                    continue;
                }

                InsertMark(database, transaction, modelSpace, definitionId, network, fromTo, targetLayer);
                inserted++;
            }
        }

        log.Add($"Wire numbers: {set} renumbered, {inserted} inserted, {skipped} skipped" + (plan.Fixed ? " (on layer " + targetLayer + ", fixed)" : ""));
        return log;
    }

    public static string Unfix(Database database, Transaction transaction, IReadOnlyList<string> handles, string wireNoLayer)
    {
        var log = new List<string>();
        EnsureLayer(database, transaction, wireNoLayer, wireNoLayer, log);
        int moved = 0, missing = 0;
        foreach (string handle in handles)
        {
            if (!TryGet(database, handle, out ObjectId id) || transaction.GetObject(id, OpenMode.ForRead) is not BlockReference reference)
            {
                missing++;
                continue;
            }

            reference.UpgradeOpen();
            reference.Layer = wireNoLayer;
            foreach (ObjectId attributeId in reference.AttributeCollection)
            {
                if (transaction.GetObject(attributeId, OpenMode.ForRead) is AttributeReference attribute)
                {
                    attribute.UpgradeOpen();
                    attribute.Layer = wireNoLayer;
                }
            }

            moved++;
        }

        return $"Un-fixed {moved} wire number(s) onto layer {wireNoLayer}" + (missing > 0 ? $" ({missing} no longer exist)" : "") + (log.Count > 0 ? "; " + string.Join("; ", log) : "");
    }

    private static void InsertMark(Database database, Transaction transaction, BlockTableRecord modelSpace, ObjectId definitionId, WireNetwork network, FromToRules fromTo, string layer)
    {
        bool vertical = Math.Abs(network.LabelRotation - 90) < 0.5;
        var position = vertical
            ? new Point3d(network.LabelPoint.X - fromTo.InsertOffset, network.LabelPoint.Y, 0)
            : new Point3d(network.LabelPoint.X, network.LabelPoint.Y + fromTo.InsertOffset, 0);
        var reference = new BlockReference(position, definitionId) { Layer = layer, Rotation = vertical ? Math.PI / 2 : 0 };
        modelSpace.AppendEntity(reference);
        transaction.AddNewlyCreatedDBObject(reference, true);

        var definition = (BlockTableRecord)transaction.GetObject(definitionId, OpenMode.ForRead);
        foreach (ObjectId id in definition)
        {
            if (transaction.GetObject(id, OpenMode.ForRead) is not AttributeDefinition attributeDefinition || attributeDefinition.Constant)
            {
                continue;
            }

            var attribute = new AttributeReference();
            attribute.SetAttributeFromBlock(attributeDefinition, reference.BlockTransform);
            attribute.TextString = attributeDefinition.Tag.Equals(fromTo.WireNumberAttribute, StringComparison.OrdinalIgnoreCase) ? network.Proposed : attributeDefinition.TextString;
            attribute.Layer = layer;
            reference.AttributeCollection.AppendAttribute(attribute);
            transaction.AddNewlyCreatedDBObject(attribute, true);
        }
    }

    private static ObjectId FindOrImportDefinition(Database database, Transaction transaction, string blockName, IReadOnlyList<string> libraryPaths, List<string> log)
    {
        var blockTable = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
        if (blockTable.Has(blockName))
        {
            return blockTable[blockName];
        }

        foreach (string folder in libraryPaths)
        {
            string file = Path.Combine(folder, blockName + ".dwg");
            if (!File.Exists(file))
            {
                continue;
            }

            try
            {
                using var source = new Database(false, true);
                source.ReadDwgFile(file, FileOpenMode.OpenForReadAndAllShare, true, null);
                source.CloseInput(true);
                ObjectId id = database.Insert(blockName, source, false);
                log.Add("Wire number block " + blockName + " imported from " + file);
                return id;
            }
            catch (System.Exception ex)
            {
                log.Add("Wire number block " + blockName + " could not be imported from " + file + ": " + ex.Message);
            }
        }

        log.Add("No " + blockName + " block definition in the drawing or the schematic library paths; networks without a wire number were skipped. Run Insert Wire Numbers once in AutoCAD Electrical so the blocks exist, then apply From/To again.");
        return ObjectId.Null;
    }

    private static void EnsureLayer(Database database, Transaction transaction, string name, string modelLayer, List<string> log)
    {
        var layerTable = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
        if (layerTable.Has(name))
        {
            return;
        }

        var record = new LayerTableRecord { Name = name };
        if (layerTable.Has(modelLayer))
        {
            var model = (LayerTableRecord)transaction.GetObject(layerTable[modelLayer], OpenMode.ForRead);
            record.Color = model.Color;
            record.LineWeight = model.LineWeight;
            record.LinetypeObjectId = model.LinetypeObjectId;
            record.IsPlottable = model.IsPlottable;
        }

        layerTable.UpgradeOpen();
        layerTable.Add(record);
        transaction.AddNewlyCreatedDBObject(record, true);
        log.Add("Created layer " + name + (layerTable.Has(modelLayer) ? " with the properties of " + modelLayer : ""));
    }

    private static bool TryGet(Database database, string handle, out ObjectId id)
    {
        id = ObjectId.Null;
        try
        {
            return database.TryGetObjectId(new Handle(Convert.ToInt64(handle, 16)), out id) && !id.IsErased;
        }
        catch
        {
            return false;
        }
    }

    private static Dictionary<string, string> ReadAttributes(BlockReference reference, Transaction transaction)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (ObjectId attributeId in reference.AttributeCollection)
        {
            if (transaction.GetObject(attributeId, OpenMode.ForRead) is AttributeReference attribute)
            {
                values[attribute.Tag] = attribute.TextString;
            }
        }

        return values;
    }

    private static string Value(Dictionary<string, string> values, string tag) => values.TryGetValue(tag, out string? value) ? value : "";

    private static List<string> Split(string value) => value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0).ToList();

    private static bool IsWireLayer(string layer, List<string> patterns) => patterns.Count == 0 || WireNetworkService.MatchesAny(layer, patterns);
}
