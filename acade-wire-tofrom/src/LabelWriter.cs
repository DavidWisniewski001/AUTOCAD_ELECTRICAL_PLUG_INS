using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace LSE.AutoCADElectrical.WireToFrom;

// Label placement, update and delete by hidden key. The only code that changes a drawing. It never touches wires,
// wire-number blocks or symbols: it reads them to find a position, and writes only label block references on the label layer.
public static class LabelWriter
{
    public static Document? FindOpenDocument(string path)
    {
        try
        {
            foreach (Document document in AcApp.DocumentManager)
            {
                if (document.Name.Equals(path, StringComparison.OrdinalIgnoreCase))
                {
                    return document;
                }

                string? databaseName = null;
                try
                {
                    databaseName = document.Database.Filename;
                }
                catch
                {
                }

                if (databaseName is not null && databaseName.Equals(path, StringComparison.OrdinalIgnoreCase))
                {
                    return document;
                }
            }
        }
        catch
        {
        }

        return null;
    }

    public static Database Open(string path)
    {
        var database = new Database(false, true);
        try
        {
            database.ReadDwgFile(path, FileOpenMode.OpenForReadAndAllShare, true, null);
            database.CloseInput(true);
            return database;
        }
        catch
        {
            database.Dispose();
            throw;
        }
    }

    public static bool IsLockedElsewhere(string path)
    {
        try
        {
            return File.Exists(Path.ChangeExtension(path, ".dwl")) || File.Exists(Path.ChangeExtension(path, ".dwl2"));
        }
        catch
        {
            return false;
        }
    }

    public static DrawingLabels Read(string path, SchematicConfig schematic)
    {
        var result = new DrawingLabels { Path = path, Exists = File.Exists(path) };
        if (!result.Exists)
        {
            result.Error = "file not found";
            return result;
        }

        Document? open = FindOpenDocument(path);
        result.OpenInEditor = open is not null;
        if (open is null && IsLockedElsewhere(path))
        {
            result.Locked = true;
            return result;
        }

        try
        {
            if (open is not null)
            {
                using Transaction editorTransaction = open.Database.TransactionManager.StartTransaction();
                Read(open.Database, editorTransaction, schematic, result);
            }
            else
            {
                using var database = Open(path);
                using Transaction transaction = database.TransactionManager.StartTransaction();
                Read(database, transaction, schematic, result);
            }
        }
        catch (System.Exception ex)
        {
            result.Error = ex.Message;
        }

        return result;
    }

    private static void Read(Database database, Transaction transaction, SchematicConfig schematic, DrawingLabels result)
    {
        var blockTable = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
        result.HasBlockH = blockTable.Has(schematic.BlockH);
        result.HasBlockV = blockTable.Has(schematic.BlockV);

        var modelSpace = (BlockTableRecord)transaction.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForRead);
        foreach (ObjectId entityId in modelSpace)
        {
            if (transaction.GetObject(entityId, OpenMode.ForRead) is not BlockReference reference)
            {
                continue;
            }

            string name = EffectiveName(reference, transaction);
            var attributes = ReadAttributes(reference, transaction);
            if (name.Equals(schematic.WdmBlockName, StringComparison.OrdinalIgnoreCase))
            {
                result.WireLayers = Split(Value(attributes, "WIRELAYS"));
                result.WireNoLayer = Value(attributes, "WIRENO_LAY").Trim();
                result.Sheet = Value(attributes, "SHEET").Trim();
                continue;
            }

            bool ours = name.Equals(schematic.BlockH, StringComparison.OrdinalIgnoreCase) || name.Equals(schematic.BlockV, StringComparison.OrdinalIgnoreCase);
            if (!ours && !attributes.ContainsKey(schematic.KeyAttribute))
            {
                continue;
            }

            result.Labels.Add(new ExistingLabel
            {
                Handle = reference.Handle.ToString(),
                Key = Value(attributes, schematic.KeyAttribute).Trim(),
                Text = Value(attributes, schematic.TextAttribute),
                Node = Value(attributes, schematic.NodeAttribute),
                Block = name,
                Layer = reference.Layer,
                Point = new Pt(reference.Position.X, reference.Position.Y),
            });
        }
    }

    public static List<string> Write(DrawingWriteRequest request)
    {
        var log = new List<string>();
        if (!File.Exists(request.DrawingPath))
        {
            throw new FileNotFoundException("Drawing was not found.", request.DrawingPath);
        }

        Document? open = FindOpenDocument(request.DrawingPath);
        if (open is not null)
        {
            using (DocumentLock _ = open.LockDocument())
            {
                WriteTransaction(open.Database, request, log);
            }

            log.Add("Labels written in the open editor session; save the drawing to keep them: " + request.DrawingPath);
            return log;
        }

        if (IsLockedElsewhere(request.DrawingPath))
        {
            throw new InvalidOperationException("Drawing is locked by another user or session (a .dwl lock file exists): " + request.DrawingPath);
        }

        Database previous = HostApplicationServices.WorkingDatabase;
        using var database = Open(request.DrawingPath);
        HostApplicationServices.WorkingDatabase = database;
        try
        {
            WriteTransaction(database, request, log);
            database.SaveAs(request.DrawingPath, DwgVersion.Current);
            log.Add("Saved " + request.DrawingPath);
        }
        finally
        {
            HostApplicationServices.WorkingDatabase = previous;
        }

        return log;
    }

    private static void WriteTransaction(Database database, DrawingWriteRequest request, List<string> log)
    {
        SchematicConfig schematic = request.Schematic;
        using Transaction transaction = database.TransactionManager.StartTransaction();
        EnsureLayer(database, transaction, schematic.LabelLayer, log);
        ObjectId horizontal = EnsureDefinition(database, transaction, schematic.BlockH, false, schematic, request.LibraryPaths, log);
        ObjectId vertical = EnsureDefinition(database, transaction, schematic.BlockV, true, schematic, request.LibraryPaths, log);
        var modelSpace = (BlockTableRecord)transaction.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForWrite);

        var occupied = new List<Point3d>();
        foreach (ObjectId entityId in modelSpace)
        {
            if (transaction.GetObject(entityId, OpenMode.ForRead) is BlockReference reference && reference.Layer.Equals(schematic.LabelLayer, StringComparison.OrdinalIgnoreCase))
            {
                occupied.Add(reference.Position);
            }
        }

        int inserted = 0, updated = 0, deleted = 0, skipped = 0;
        foreach (LabelChange change in request.Changes)
        {
            switch (change.Action)
            {
                case LabelAction.Delete:
                    if (TryGet(database, change.ExistingHandle, out ObjectId deleteId) && transaction.GetObject(deleteId, OpenMode.ForRead) is BlockReference stale)
                    {
                        stale.UpgradeOpen();
                        stale.Erase();
                        deleted++;
                    }
                    else
                    {
                        log.Add("label " + change.ExistingHandle + " (" + change.Key + ") no longer exists; nothing to delete");
                        skipped++;
                    }

                    break;

                case LabelAction.Update:
                    if (!TryGet(database, change.ExistingHandle, out ObjectId updateId) || transaction.GetObject(updateId, OpenMode.ForRead) is not BlockReference existing)
                    {
                        log.Add("label " + change.ExistingHandle + " (" + change.Key + ") no longer exists; inserted instead");
                        goto case LabelAction.Insert;
                    }

                    SetAttributes(existing, transaction, schematic, change, database);
                    if (schematic.RepositionOnRerun)
                    {
                        Point3d current = existing.Position;
                        occupied.RemoveAll(o => o.DistanceTo(current) < 1e-6);
                        Placement? moved = Locate(database, transaction, change, schematic, occupied, log);
                        if (moved is null)
                        {
                            occupied.Add(current);
                        }
                        else
                        {
                            occupied.Add(moved.Position);
                            existing.UpgradeOpen();
                            existing.Position = moved.Position;
                            existing.Layer = schematic.LabelLayer;
                            foreach (ObjectId attributeId in existing.AttributeCollection)
                            {
                                if (transaction.GetObject(attributeId, OpenMode.ForRead) is AttributeReference attribute)
                                {
                                    attribute.UpgradeOpen();
                                    attribute.AdjustAlignment(database);
                                }
                            }
                        }
                    }

                    updated++;
                    break;

                case LabelAction.Insert:
                    Placement? placement = Locate(database, transaction, change, schematic, occupied, log);
                    if (placement is null)
                    {
                        skipped++;
                        break;
                    }

                    ObjectId definitionId = placement.Vertical ? vertical : horizontal;
                    if (definitionId.IsNull)
                    {
                        log.Add("no label block definition available for " + change.Key + "; skipped");
                        skipped++;
                        break;
                    }

                    var reference = new BlockReference(placement.Position, definitionId) { Layer = schematic.LabelLayer };
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
                        attribute.TextString = ValueFor(attributeDefinition.Tag, change, schematic) ?? attributeDefinition.TextString;
                        attribute.Layer = schematic.LabelLayer;
                        reference.AttributeCollection.AppendAttribute(attribute);
                        transaction.AddNewlyCreatedDBObject(attribute, true);
                        attribute.AdjustAlignment(database);
                    }

                    occupied.Add(placement.Position);
                    if (placement.Note.Length > 0)
                    {
                        log.Add(change.Key + ": " + placement.Note);
                    }

                    inserted++;
                    break;
            }
        }

        transaction.Commit();
        log.Add($"Labels: {inserted} inserted, {updated} updated, {deleted} deleted, {skipped} skipped (layer {schematic.LabelLayer})");
    }

    private static string? ValueFor(string tag, LabelChange change, SchematicConfig schematic)
    {
        if (tag.Equals(schematic.TextAttribute, StringComparison.OrdinalIgnoreCase))
        {
            return change.Text;
        }

        if (tag.Equals(schematic.NodeAttribute, StringComparison.OrdinalIgnoreCase))
        {
            return change.Node;
        }

        if (tag.Equals(schematic.KeyAttribute, StringComparison.OrdinalIgnoreCase))
        {
            return change.Key;
        }

        return null;
    }

    private static void SetAttributes(BlockReference reference, Transaction transaction, SchematicConfig schematic, LabelChange change, Database database)
    {
        reference.UpgradeOpen();
        reference.Layer = schematic.LabelLayer;
        foreach (ObjectId attributeId in reference.AttributeCollection)
        {
            if (transaction.GetObject(attributeId, OpenMode.ForRead) is not AttributeReference attribute)
            {
                continue;
            }

            string? value = ValueFor(attribute.Tag, change, schematic);
            if (value is null)
            {
                continue;
            }

            attribute.UpgradeOpen();
            attribute.TextString = value;
            attribute.Layer = schematic.LabelLayer;
            attribute.AdjustAlignment(database);
        }
    }

    private sealed class Placement
    {
        public Point3d Position { get; set; }

        public bool Vertical { get; set; }

        public string Note { get; set; } = "";
    }

    private static Placement? Locate(Database database, Transaction transaction, LabelChange change, SchematicConfig schematic, List<Point3d> occupied, List<string> log)
    {
        if (!TryGet(database, change.WireHandle, out ObjectId wireId))
        {
            log.Add("wire handle " + change.WireHandle + " (" + change.Key + ") is not in this drawing; no label placed. Refresh the project database and run again.");
            return null;
        }

        Point3d start, end;
        switch (transaction.GetObject(wireId, OpenMode.ForRead))
        {
            case Line line:
                start = line.StartPoint;
                end = line.EndPoint;
                break;
            case Polyline polyline when polyline.NumberOfVertices >= 2:
                start = polyline.GetPoint3dAt(0);
                end = polyline.GetPoint3dAt(polyline.NumberOfVertices - 1);
                break;
            default:
                log.Add("wire handle " + change.WireHandle + " (" + change.Key + ") is not a line or polyline; no label placed.");
                return null;
        }

        string note = "";
        Point3d wireEnd, other;
        if (TryGet(database, change.ComponentHandle, out ObjectId componentId) && transaction.GetObject(componentId, OpenMode.ForRead) is BlockReference component)
        {
            bool startIsNearer = component.Position.DistanceTo(start) <= component.Position.DistanceTo(end);
            wireEnd = startIsNearer ? start : end;
            other = startIsNearer ? end : start;
        }
        else
        {
            wireEnd = start;
            other = end;
            note = "component handle " + change.ComponentHandle + " not found; label placed at the wire's start point";
        }

        double dx = other.X - wireEnd.X, dy = other.Y - wireEnd.Y;
        double length = Math.Sqrt(dx * dx + dy * dy);
        if (length < 1e-9)
        {
            dx = 1;
            dy = 0;
            length = 1;
        }

        dx /= length;
        dy /= length;
        double along = length < 2 * schematic.EndOffset ? length / 2 : schematic.EndOffset;
        double px = -dy, py = dx;
        var position = new Point3d(wireEnd.X + dx * along + px * schematic.SideOffset, wireEnd.Y + dy * along + py * schematic.SideOffset, 0);
        int tries = 0;
        while (tries < schematic.CollisionTries && occupied.Any(o => o.DistanceTo(position) < schematic.CollisionDistance))
        {
            position = new Point3d(position.X + dx * schematic.CollisionStep, position.Y + dy * schematic.CollisionStep, 0);
            tries++;
        }

        if (tries > 0)
        {
            note = (note.Length > 0 ? note + "; " : "") + "moved " + tries + " step(s) along the wire to avoid another label";
        }

        return new Placement { Position = position, Vertical = Math.Abs(dy) > Math.Abs(dx), Note = note };
    }

    public static ObjectId EnsureDefinition(Database database, Transaction transaction, string name, bool vertical, SchematicConfig schematic, IReadOnlyList<string> libraryPaths, List<string> log)
    {
        var blockTable = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
        if (blockTable.Has(name))
        {
            return blockTable[name];
        }

        foreach (string folder in libraryPaths)
        {
            string file = Path.Combine(folder, name + ".dwg");
            if (!File.Exists(file))
            {
                continue;
            }

            try
            {
                using var source = new Database(false, true);
                source.ReadDwgFile(file, FileOpenMode.OpenForReadAndAllShare, true, null);
                source.CloseInput(true);
                ObjectId imported = database.Insert(name, source, false);
                log.Add("Label block " + name + " imported from " + file);
                return imported;
            }
            catch (System.Exception ex)
            {
                log.Add("Label block " + name + " could not be imported from " + file + ": " + ex.Message);
            }
        }

        ObjectId created = CreateDefinition(database, transaction, name, vertical, schematic);
        log.Add("Label block " + name + " defined in the drawing (no " + name + ".dwg found in " + string.Join(";", libraryPaths) + ")");
        return created;
    }

    public static ObjectId CreateDefinition(Database database, Transaction transaction, string name, bool vertical, SchematicConfig schematic)
    {
        var blockTable = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForWrite);
        if (blockTable.Has(name))
        {
            return blockTable[name];
        }

        var record = new BlockTableRecord { Name = name, Origin = Point3d.Origin };
        ObjectId recordId = blockTable.Add(record);
        transaction.AddNewlyCreatedDBObject(record, true);
        double rotation = vertical ? Math.PI / 2 : 0;
        double height = schematic.TextHeight;
        double gap = height * 1.6;
        var nodePosition = vertical ? new Point3d(gap, 0, 0) : new Point3d(0, -gap, 0);

        AddAttribute(record, transaction, schematic.TextAttribute, "To/From identifier", Point3d.Origin, height, rotation, false);
        AddAttribute(record, transaction, schematic.NodeAttribute, "Wire number (node)", nodePosition, height * 0.8, rotation, !schematic.ShowNode);
        AddAttribute(record, transaction, schematic.KeyAttribute, "LSE_ToFrom key (wire handle/end)", Point3d.Origin, height * 0.5, rotation, true);
        return recordId;
    }

    private static void AddAttribute(BlockTableRecord record, Transaction transaction, string tag, string prompt, Point3d position, double height, double rotation, bool invisible)
    {
        var definition = new AttributeDefinition
        {
            Tag = tag,
            Prompt = prompt,
            TextString = "",
            Height = height,
            Rotation = rotation,
            Invisible = invisible,
            Justify = AttachmentPoint.MiddleCenter,
            LockPositionInBlock = false,
        };
        definition.AlignmentPoint = position;
        definition.Position = position;
        record.AppendEntity(definition);
        transaction.AddNewlyCreatedDBObject(definition, true);
    }

    public static List<string> AddToDrawing(Document document, SchematicConfig schematic, IReadOnlyList<string> libraryPaths)
    {
        var log = new List<string>();
        using (DocumentLock _ = document.LockDocument())
        using (Transaction transaction = document.Database.TransactionManager.StartTransaction())
        {
            EnsureLayer(document.Database, transaction, schematic.LabelLayer, log);
            EnsureDefinition(document.Database, transaction, schematic.BlockH, false, schematic, libraryPaths, log);
            EnsureDefinition(document.Database, transaction, schematic.BlockV, true, schematic, libraryPaths, log);
            transaction.Commit();
        }

        log.Add("Layer " + schematic.LabelLayer + " and blocks " + schematic.BlockH + " / " + schematic.BlockV + " are present in " + document.Name + ". Save it (for a template .dwt use Save As).");
        return log;
    }

    public static List<string> ExportBlocks(string folder, SchematicConfig schematic)
    {
        var log = new List<string>();
        Directory.CreateDirectory(folder);
        using var database = new Database(true, true);
        var ids = new Dictionary<string, ObjectId>(StringComparer.OrdinalIgnoreCase);
        using (Transaction transaction = database.TransactionManager.StartTransaction())
        {
            ids[schematic.BlockH] = CreateDefinition(database, transaction, schematic.BlockH, false, schematic);
            ids[schematic.BlockV] = CreateDefinition(database, transaction, schematic.BlockV, true, schematic);
            transaction.Commit();
        }

        foreach (var (name, id) in ids)
        {
            string file = Path.Combine(folder, name + ".dwg");
            using Database block = database.Wblock(id);
            block.SaveAs(file, DwgVersion.Current);
            log.Add("Wrote " + file);
        }

        return log;
    }

    private static void EnsureLayer(Database database, Transaction transaction, string name, List<string> log)
    {
        var layerTable = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
        if (layerTable.Has(name))
        {
            return;
        }

        var record = new LayerTableRecord { Name = name };
        layerTable.UpgradeOpen();
        layerTable.Add(record);
        transaction.AddNewlyCreatedDBObject(record, true);
        log.Add("Created layer " + name);
    }

    private static bool TryGet(Database database, string handle, out ObjectId id)
    {
        id = ObjectId.Null;
        try
        {
            if (handle.Trim().Length == 0)
            {
                return false;
            }

            return database.TryGetObjectId(new Handle(Convert.ToInt64(handle.Trim(), 16)), out id) && !id.IsErased;
        }
        catch
        {
            return false;
        }
    }

    public static string EffectiveName(BlockReference reference, Transaction transaction)
    {
        ObjectId definitionId = reference.IsDynamicBlock ? reference.DynamicBlockTableRecord : reference.BlockTableRecord;
        return ((BlockTableRecord)transaction.GetObject(definitionId, OpenMode.ForRead)).Name;
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
}
