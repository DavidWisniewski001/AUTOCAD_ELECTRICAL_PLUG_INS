using Autodesk.AutoCAD.ApplicationServices;
using AcColor = Autodesk.AutoCAD.Colors.Color;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace LSE.AutoCADElectrical.ProjectDoctor;

public static class DrawingInspectionService
{
    public static bool IsOpenInEditor(string path)
    {
        try
        {
            foreach (Document document in AcApp.DocumentManager)
            {
                if (document.Name.Equals(path, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
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
                    return true;
                }
            }
        }
        catch
        {
        }

        return false;
    }

    public static DrawingInspection Inspect(string path, string wdmBlockName, IReadOnlyList<string> titleBlockNames)
    {
        var result = new DrawingInspection { Path = path };
        if (IsOpenInEditor(path))
        {
            result.OpenInEditor = true;
            return result;
        }

        try
        {
            using var database = Open(path);
            using Transaction transaction = database.TransactionManager.StartTransaction();
            ReadBlocks(database, transaction, wdmBlockName, titleBlockNames, result);
            ReadLayers(database, transaction, result);
        }
        catch (System.Exception ex)
        {
            result.Error = ex.Message;
        }

        return result;
    }

    public static List<string> Repair(DrawingRepairRequest request)
    {
        var log = new List<string>();
        if (!File.Exists(request.DrawingPath))
        {
            throw new FileNotFoundException("Drawing was not found.", request.DrawingPath);
        }

        if (IsOpenInEditor(request.DrawingPath))
        {
            throw new InvalidOperationException("The drawing is open in the editor. Close it first: " + request.DrawingPath);
        }

        if (request.NeedsTemplate && !File.Exists(request.TemplateDrawingPath))
        {
            throw new FileNotFoundException("Template drawing was not found.", request.TemplateDrawingPath);
        }

        Database? source = null;
        try
        {
            if (request.NeedsTemplate)
            {
                source = Open(request.TemplateDrawingPath);
            }

            Database previous = HostApplicationServices.WorkingDatabase;
            using var database = Open(request.DrawingPath);
            HostApplicationServices.WorkingDatabase = database;
            try
            {
                using (Transaction transaction = database.TransactionManager.StartTransaction())
                {
                    if (request.InsertWdm)
                    {
                        log.Add(InsertWdm(database, source!, transaction, request.WdmBlockName, request.TemplateDrawingPath));
                    }

                    foreach (string name in request.CreateLayers)
                    {
                        log.Add(CreateLayer(database, source!, transaction, name));
                    }

                    foreach (string name in request.RepairLayers)
                    {
                        log.Add(RepairLayer(database, source!, transaction, name));
                    }

                    foreach (var (tag, value) in request.Attributes)
                    {
                        log.Add(SetAttribute(database, transaction, request.WdmBlockName, tag, value));
                    }

                    foreach (string name in request.PurgeLayers)
                    {
                        log.Add(PurgeLayer(database, transaction, name));
                    }

                    transaction.Commit();
                }

                database.SaveAs(request.DrawingPath, DwgVersion.Current);
                log.Add("Saved " + request.DrawingPath);
            }
            finally
            {
                HostApplicationServices.WorkingDatabase = previous;
            }
        }
        finally
        {
            source?.Dispose();
        }

        return log;
    }

    private static Database Open(string path)
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

    private static void ReadBlocks(Database database, Transaction transaction, string wdmBlockName, IReadOnlyList<string> titleBlockNames, DrawingInspection result)
    {
        var blockTable = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
        foreach (ObjectId recordId in blockTable)
        {
            var record = (BlockTableRecord)transaction.GetObject(recordId, OpenMode.ForRead);
            if (!record.IsLayout)
            {
                continue;
            }

            result.Layouts.Add(record.Name);
            foreach (ObjectId entityId in record)
            {
                if (transaction.GetObject(entityId, OpenMode.ForRead) is not BlockReference reference)
                {
                    continue;
                }

                string name = EffectiveName(reference, transaction);
                if (name.Equals(wdmBlockName, StringComparison.OrdinalIgnoreCase))
                {
                    result.HasWdm = true;
                    if (result.WdmValues.Count == 0)
                    {
                        foreach (ObjectId attributeId in reference.AttributeCollection)
                        {
                            if (transaction.GetObject(attributeId, OpenMode.ForRead) is AttributeReference attribute)
                            {
                                result.WdmValues[attribute.Tag] = attribute.TextString;
                            }
                        }
                    }
                }
                else if (WireLayerService.MatchesAny(name, titleBlockNames))
                {
                    result.TitleBlocks.Add(name + " in " + record.Name);
                }
            }
        }
    }

    private static void ReadLayers(Database database, Transaction transaction, DrawingInspection result)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var blockTable = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
        foreach (ObjectId recordId in blockTable)
        {
            var record = (BlockTableRecord)transaction.GetObject(recordId, OpenMode.ForRead);
            foreach (ObjectId entityId in record)
            {
                if (transaction.GetObject(entityId, OpenMode.ForRead) is Entity entity)
                {
                    counts.TryGetValue(entity.Layer, out int count);
                    counts[entity.Layer] = count + 1;
                }
            }
        }

        var layerTable = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
        foreach (ObjectId layerId in layerTable)
        {
            var layer = (LayerTableRecord)transaction.GetObject(layerId, OpenMode.ForRead);
            var info = new LayerInfo
            {
                Name = layer.Name,
                Color = layer.Color.ToString(),
                LineWeight = (int)layer.LineWeight,
                IsPlottable = layer.IsPlottable,
                IsOff = layer.IsOff,
                IsFrozen = layer.IsFrozen,
                IsLocked = layer.IsLocked,
                Description = layer.Description ?? "",
                XdataApps = XdataApps(layer),
            };

            try
            {
                info.Linetype = ((LinetypeTableRecord)transaction.GetObject(layer.LinetypeObjectId, OpenMode.ForRead)).Name;
            }
            catch
            {
                info.Linetype = "?";
            }

            if (counts.TryGetValue(layer.Name, out int used))
            {
                info.EntityCount = used;
            }

            result.Layers.Add(info);
        }
    }

    private static List<string> XdataApps(DBObject obj)
    {
        var apps = new List<string>();
        using ResultBuffer? xdata = obj.XData;
        if (xdata is null)
        {
            return apps;
        }

        foreach (TypedValue value in xdata)
        {
            if (value.TypeCode == (int)DxfCode.ExtendedDataRegAppName && value.Value is string name)
            {
                apps.Add(name);
            }
        }

        return apps;
    }

    private static string InsertWdm(Database database, Database source, Transaction transaction, string wdmBlockName, string templateDrawingPath)
    {
        var templateValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Point3d position = Point3d.Origin;
        ObjectId sourceDefinitionId;
        using (Transaction sourceTransaction = source.TransactionManager.StartTransaction())
        {
            var sourceTable = (BlockTable)sourceTransaction.GetObject(source.BlockTableId, OpenMode.ForRead);
            if (!sourceTable.Has(wdmBlockName))
            {
                throw new InvalidOperationException("The template drawing has no " + wdmBlockName + " block definition: " + templateDrawingPath);
            }

            sourceDefinitionId = sourceTable[wdmBlockName];
            foreach (ObjectId recordId in sourceTable)
            {
                var record = (BlockTableRecord)sourceTransaction.GetObject(recordId, OpenMode.ForRead);
                if (!record.IsLayout)
                {
                    continue;
                }

                foreach (ObjectId entityId in record)
                {
                    if (sourceTransaction.GetObject(entityId, OpenMode.ForRead) is not BlockReference reference)
                    {
                        continue;
                    }

                    if (!EffectiveName(reference, sourceTransaction).Equals(wdmBlockName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    position = reference.Position;
                    foreach (ObjectId attributeId in reference.AttributeCollection)
                    {
                        if (sourceTransaction.GetObject(attributeId, OpenMode.ForRead) is AttributeReference attribute)
                        {
                            templateValues[attribute.Tag] = attribute.TextString;
                        }
                    }

                    break;
                }

                if (templateValues.Count > 0)
                {
                    break;
                }
            }

            sourceTransaction.Commit();
        }

        var blockTable = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
        if (!blockTable.Has(wdmBlockName))
        {
            var ids = new ObjectIdCollection();
            ids.Add(sourceDefinitionId);
            var mapping = new IdMapping();
            source.WblockCloneObjects(ids, database.BlockTableId, mapping, DuplicateRecordCloning.Ignore, false);
            blockTable = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
        }

        if (!blockTable.Has(wdmBlockName))
        {
            throw new InvalidOperationException("The " + wdmBlockName + " block definition could not be copied into the drawing.");
        }

        ObjectId definitionId = blockTable[wdmBlockName];
        var definition = (BlockTableRecord)transaction.GetObject(definitionId, OpenMode.ForRead);
        var modelSpace = (BlockTableRecord)transaction.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForWrite);
        var newReference = new BlockReference(position, definitionId);
        modelSpace.AppendEntity(newReference);
        transaction.AddNewlyCreatedDBObject(newReference, true);

        int count = 0;
        foreach (ObjectId id in definition)
        {
            if (transaction.GetObject(id, OpenMode.ForRead) is not AttributeDefinition attributeDefinition || attributeDefinition.Constant)
            {
                continue;
            }

            var attribute = new AttributeReference();
            attribute.SetAttributeFromBlock(attributeDefinition, newReference.BlockTransform);
            if (templateValues.TryGetValue(attributeDefinition.Tag, out string? value))
            {
                attribute.TextString = value;
                count++;
            }
            else
            {
                attribute.TextString = attributeDefinition.TextString;
            }

            newReference.AttributeCollection.AppendAttribute(attribute);
            transaction.AddNewlyCreatedDBObject(attribute, true);
        }

        return wdmBlockName + " inserted into model space with " + count + " attribute value(s) from " + Path.GetFileName(templateDrawingPath);
    }

    private static string SetAttribute(Database database, Transaction transaction, string wdmBlockName, string tag, string value)
    {
        var blockTable = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
        foreach (ObjectId recordId in blockTable)
        {
            var record = (BlockTableRecord)transaction.GetObject(recordId, OpenMode.ForRead);
            if (!record.IsLayout)
            {
                continue;
            }

            foreach (ObjectId entityId in record)
            {
                if (transaction.GetObject(entityId, OpenMode.ForRead) is not BlockReference reference)
                {
                    continue;
                }

                if (!EffectiveName(reference, transaction).Equals(wdmBlockName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (ObjectId attributeId in reference.AttributeCollection)
                {
                    if (transaction.GetObject(attributeId, OpenMode.ForRead) is not AttributeReference attribute || !attribute.Tag.Equals(tag, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string before = attribute.TextString;
                    attribute.UpgradeOpen();
                    attribute.TextString = value;
                    return wdmBlockName + " " + tag + ": \"" + before + "\" -> \"" + value + "\"";
                }

                throw new InvalidOperationException("The " + wdmBlockName + " block in this drawing has no attribute " + tag + ". Use Project > Other Tools > Update to New WD_M Block in AutoCAD Electrical first.");
            }
        }

        throw new InvalidOperationException("No " + wdmBlockName + " block was found in the drawing, so " + tag + " could not be set.");
    }

    private static ObjectId TemplateLayer(Database source, string name)
    {
        using Transaction sourceTransaction = source.TransactionManager.StartTransaction();
        var sourceTable = (LayerTable)sourceTransaction.GetObject(source.LayerTableId, OpenMode.ForRead);
        if (!sourceTable.Has(name))
        {
            throw new InvalidOperationException("The template drawing has no layer " + name + ".");
        }

        return sourceTable[name];
    }

    private static string CreateLayer(Database database, Database source, Transaction transaction, string name)
    {
        var layerTable = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
        if (layerTable.Has(name))
        {
            return "Layer " + name + " already exists; left as it is";
        }

        var ids = new ObjectIdCollection();
        ids.Add(TemplateLayer(source, name));
        var mapping = new IdMapping();
        source.WblockCloneObjects(ids, database.LayerTableId, mapping, DuplicateRecordCloning.Ignore, false);
        layerTable = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
        if (!layerTable.Has(name))
        {
            throw new InvalidOperationException("Layer " + name + " could not be copied from the template drawing.");
        }

        return "Created layer " + name + " from the template (properties, linetype and wire-type data)";
    }

    private static string RepairLayer(Database database, Database source, Transaction transaction, string name)
    {
        var layerTable = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
        if (!layerTable.Has(name))
        {
            return CreateLayer(database, source, transaction, name);
        }

        AcColor color;
        LineWeight lineWeight;
        bool plottable;
        string description;
        string linetypeName;
        ObjectId linetypeId;
        TypedValue[]? xdata;
        using (Transaction sourceTransaction = source.TransactionManager.StartTransaction())
        {
            var sourceLayer = (LayerTableRecord)sourceTransaction.GetObject(TemplateLayer(source, name), OpenMode.ForRead);
            color = sourceLayer.Color;
            lineWeight = sourceLayer.LineWeight;
            plottable = sourceLayer.IsPlottable;
            description = sourceLayer.Description ?? "";
            linetypeId = sourceLayer.LinetypeObjectId;
            linetypeName = ((LinetypeTableRecord)sourceTransaction.GetObject(linetypeId, OpenMode.ForRead)).Name;
            using ResultBuffer? buffer = sourceLayer.XData;
            xdata = buffer?.AsArray();
            sourceTransaction.Commit();
        }

        var notes = new List<string>();
        var target = (LayerTableRecord)transaction.GetObject(layerTable[name], OpenMode.ForWrite);
        target.Color = color;
        target.LineWeight = lineWeight;
        target.IsPlottable = plottable;
        if (description.Length > 0)
        {
            target.Description = description;
        }

        var linetypeTable = (LinetypeTable)transaction.GetObject(database.LinetypeTableId, OpenMode.ForRead);
        if (!linetypeTable.Has(linetypeName))
        {
            var ids = new ObjectIdCollection();
            ids.Add(linetypeId);
            source.WblockCloneObjects(ids, database.LinetypeTableId, new IdMapping(), DuplicateRecordCloning.Ignore, false);
            linetypeTable = (LinetypeTable)transaction.GetObject(database.LinetypeTableId, OpenMode.ForRead);
        }

        if (linetypeTable.Has(linetypeName))
        {
            target.LinetypeObjectId = linetypeTable[linetypeName];
        }
        else
        {
            notes.Add("linetype " + linetypeName + " could not be copied");
        }

        if (xdata is not null && xdata.Length > 0)
        {
            var registered = (RegAppTable)transaction.GetObject(database.RegAppTableId, OpenMode.ForRead);
            foreach (TypedValue value in xdata)
            {
                if (value.TypeCode != (int)DxfCode.ExtendedDataRegAppName || value.Value is not string application || registered.Has(application))
                {
                    continue;
                }

                if (!registered.IsWriteEnabled)
                {
                    registered.UpgradeOpen();
                }

                var record = new RegAppTableRecord { Name = application };
                registered.Add(record);
                transaction.AddNewlyCreatedDBObject(record, true);
            }

            using var copy = new ResultBuffer(xdata);
            target.XData = copy;
        }

        return "Repaired layer " + name + ": color, linetype, lineweight, plot flag and wire-type data set from the template" + (notes.Count == 0 ? "" : " (" + string.Join("; ", notes) + ")");
    }

    private static string PurgeLayer(Database database, Transaction transaction, string name)
    {
        var layerTable = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
        if (!layerTable.Has(name))
        {
            return "Layer " + name + " is already gone";
        }

        ObjectId layerId = layerTable[name];
        if (database.Clayer == layerId)
        {
            database.Clayer = layerTable["0"];
        }

        var ids = new ObjectIdCollection();
        ids.Add(layerId);
        database.Purge(ids);
        if (ids.Count == 0)
        {
            throw new InvalidOperationException("Layer " + name + " is still referenced (entities, block definitions or viewport overrides) and cannot be purged.");
        }

        var record = (LayerTableRecord)transaction.GetObject(layerId, OpenMode.ForWrite);
        record.Erase(true);
        return "Purged layer " + name;
    }

    private static string EffectiveName(BlockReference reference, Transaction transaction)
    {
        ObjectId definitionId = reference.IsDynamicBlock ? reference.DynamicBlockTableRecord : reference.BlockTableRecord;
        return ((BlockTableRecord)transaction.GetObject(definitionId, OpenMode.ForRead)).Name;
    }
}
