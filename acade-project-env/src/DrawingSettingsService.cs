using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace LSE.AutoCADElectrical.ProjectEnvironment;

public static class DrawingSettingsService
{
    public static bool IsOpenInEditor(string path) => FindOpenDocument(path) is not null;

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

    public static DrawingState Inspect(string path, string wdmBlockName)
    {
        var result = new DrawingState { Path = path, Exists = File.Exists(path) };
        if (!result.Exists)
        {
            result.Error = "file not found";
            return result;
        }

        Document? open = FindOpenDocument(path);
        result.OpenInEditor = open is not null;
        try
        {
            if (open is not null)
            {
                ReadWdm(open.Database, wdmBlockName, result);
                result.ReadFromEditor = true;
            }
            else
            {
                using var database = Open(path);
                ReadWdm(database, wdmBlockName, result);
            }
        }
        catch (System.Exception ex)
        {
            result.Error = ex.Message;
        }

        return result;
    }

    private static void ReadWdm(Database database, string wdmBlockName, DrawingState result)
    {
        using Transaction transaction = database.TransactionManager.StartTransaction();
        BlockReference? settings = FindBlock(database, transaction, wdmBlockName);
        if (settings is null)
        {
            return;
        }

        result.HasWdm = true;
        foreach (ObjectId attributeId in settings.AttributeCollection)
        {
            if (transaction.GetObject(attributeId, OpenMode.ForRead) is AttributeReference attribute)
            {
                result.WdmValues[attribute.Tag] = attribute.TextString;
                result.WdmTags.Add(attribute.Tag);
            }
        }
    }

    public static List<string> Apply(DrawingApplyRequest request, EnvRules rules)
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
                ApplyTransaction(open.Database, request, rules, log);
            }

            log.Add("Changes applied in the open editor session; save the drawing to keep them: " + request.DrawingPath);
            return log;
        }

        Database previous = HostApplicationServices.WorkingDatabase;
        using var database = Open(request.DrawingPath);
        HostApplicationServices.WorkingDatabase = database;
        try
        {
            ApplyTransaction(database, request, rules, log);
            database.SaveAs(request.DrawingPath, DwgVersion.Current);
            log.Add("Saved " + request.DrawingPath);
        }
        finally
        {
            HostApplicationServices.WorkingDatabase = previous;
        }

        return log;
    }

    private static void ApplyTransaction(Database database, DrawingApplyRequest request, EnvRules rules, List<string> log)
    {
        using Transaction transaction = database.TransactionManager.StartTransaction();
        if (request.Attributes.Count > 0)
        {
            BlockReference? settings = FindBlock(database, transaction, request.WdmBlockName);
            if (settings is null)
            {
                throw new InvalidOperationException("No " + request.WdmBlockName + " block was found in the drawing, so its settings could not be changed.");
            }

            foreach (var (tag, value) in request.Attributes)
            {
                log.Add(SetAttribute(settings, transaction, request.WdmBlockName, tag, value));
            }
        }

        if (request.Wires is not null)
        {
            log.AddRange(WireNumberDrawingService.Write(database, transaction, request.Wires, rules.FromTo, request.LibraryPaths));
        }

        if (request.UnfixHandles.Count > 0)
        {
            log.Add(WireNumberDrawingService.Unfix(database, transaction, request.UnfixHandles, request.WireNoLayer));
        }

        transaction.Commit();
    }

    public static BlockReference? FindBlock(Database database, Transaction transaction, string blockName)
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
                if (transaction.GetObject(entityId, OpenMode.ForRead) is BlockReference reference
                    && EffectiveName(reference, transaction).Equals(blockName, StringComparison.OrdinalIgnoreCase))
                {
                    return reference;
                }
            }
        }

        return null;
    }

    private static string SetAttribute(BlockReference settings, Transaction transaction, string wdmBlockName, string tag, string value)
    {
        foreach (ObjectId attributeId in settings.AttributeCollection)
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

    public static string EffectiveName(BlockReference reference, Transaction transaction)
    {
        ObjectId definitionId = reference.IsDynamicBlock ? reference.DynamicBlockTableRecord : reference.BlockTableRecord;
        return ((BlockTableRecord)transaction.GetObject(definitionId, OpenMode.ForRead)).Name;
    }
}
