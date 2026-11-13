using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace LSE.AutoCADElectrical.Librarian;

public static class DrawingInspectionService
{
    public static DrawingInfo Inspect(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Drawing was not found.", path);
        }

        var info = new DrawingInfo { Path = path };
        using var database = new Database(false, true);
        database.ReadDwgFile(path, FileOpenMode.OpenForReadAndAllShare, true, null);
        database.CloseInput(true);
        info.Units = database.Insunits.ToString();
        info.DwgVersion = database.OriginalFileVersion.ToString();
        try
        {
            using System.Drawing.Bitmap? thumbnail = database.ThumbnailBitmap;
            info.HasThumbnail = thumbnail is not null;
        }
        catch
        {
            info.HasThumbnail = false;
        }

        using (Transaction transaction = database.TransactionManager.StartTransaction())
        {
            var blockTable = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
            var modelSpace = (BlockTableRecord)transaction.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            foreach (ObjectId entityId in modelSpace)
            {
                if (transaction.GetObject(entityId, OpenMode.ForRead) is not Entity entity)
                {
                    continue;
                }

                info.EntityCount++;
                if (entity is AttributeDefinition definition)
                {
                    info.Attributes.Add(new AttributeInfo
                    {
                        Tag = definition.Tag,
                        Prompt = definition.Prompt,
                        Default = definition.TextString,
                        Invisible = definition.Invisible,
                        Constant = definition.Constant
                    });
                    if (definition.Invisible)
                    {
                        continue;
                    }
                }
                else if (entity is BlockReference reference)
                {
                    info.BlockReferences.Add(EffectiveName(reference, transaction));
                }

                try
                {
                    Extents3d extents = entity.GeometricExtents;
                    info.MinX = Math.Min(info.MinX, extents.MinPoint.X);
                    info.MinY = Math.Min(info.MinY, extents.MinPoint.Y);
                    info.MaxX = Math.Max(info.MaxX, extents.MaxPoint.X);
                    info.MaxY = Math.Max(info.MaxY, extents.MaxPoint.Y);
                }
                catch
                {
                    // Entities without extents (empty text, degenerate curves) are skipped.
                }
            }

            transaction.Commit();
        }

        return info;
    }

    public static System.Drawing.Bitmap? Thumbnail(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Drawing was not found.", path);
        }

        using var database = new Database(false, true);
        database.ReadDwgFile(path, FileOpenMode.OpenForReadAndAllShare, true, null);
        database.CloseInput(true);
        using System.Drawing.Bitmap? thumbnail = database.ThumbnailBitmap;
        return thumbnail is null ? null : new System.Drawing.Bitmap(thumbnail);
    }

    public static List<string> ListBlockNames()
    {
        Document document = AcApp.DocumentManager.MdiActiveDocument
            ?? throw new InvalidOperationException("No active drawing.");
        var names = new List<string>();

        using Transaction transaction = document.Database.TransactionManager.StartTransaction();
        var blockTable = (BlockTable)transaction.GetObject(document.Database.BlockTableId, OpenMode.ForRead);
        foreach (ObjectId recordId in blockTable)
        {
            var record = (BlockTableRecord)transaction.GetObject(recordId, OpenMode.ForRead);
            if (record.IsLayout || record.IsAnonymous || record.IsFromExternalReference || record.IsDependent)
            {
                continue;
            }

            if (record.Name.StartsWith("*", StringComparison.Ordinal))
            {
                continue;
            }

            names.Add(record.Name);
        }

        names.Sort(StringComparer.OrdinalIgnoreCase);
        return names;
    }

    public static string? PromptForBlock()
    {
        Document document = AcApp.DocumentManager.MdiActiveDocument
            ?? throw new InvalidOperationException("No active drawing.");

        var options = new PromptEntityOptions("\nSelect the block to file in the library: ");
        options.SetRejectMessage("\nSelect a block reference.");
        options.AddAllowedClass(typeof(BlockReference), true);

        PromptEntityResult selected = document.Editor.GetEntity(options);
        if (selected.Status != PromptStatus.OK)
        {
            return null;
        }

        using Transaction transaction = document.Database.TransactionManager.StartTransaction();
        var reference = (BlockReference)transaction.GetObject(selected.ObjectId, OpenMode.ForRead);
        return EffectiveName(reference, transaction);
    }

    public static void ExportBlock(string blockName, string targetPath)
    {
        Document document = AcApp.DocumentManager.MdiActiveDocument
            ?? throw new InvalidOperationException("No active drawing to export a block from.");

        if (File.Exists(targetPath))
        {
            throw new InvalidOperationException("Drawing already exists: " + targetPath);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        ObjectId blockId;
        using (document.LockDocument())
        using (Transaction transaction = document.Database.TransactionManager.StartTransaction())
        {
            var blockTable = (BlockTable)transaction.GetObject(document.Database.BlockTableId, OpenMode.ForRead);
            if (!blockTable.Has(blockName))
            {
                throw new InvalidOperationException("Block " + blockName + " was not found in the current drawing.");
            }

            blockId = blockTable[blockName];
            transaction.Commit();
        }

        using (document.LockDocument())
        {
            // WBLOCK of a block definition: the block's entities land in model space of the new
            // drawing with the block base point at the origin, which is how AutoCAD Electrical
            // expects a library symbol or footprint to be built.
            using Database exported = document.Database.Wblock(blockId);
            exported.SaveAs(targetPath, DwgVersion.AC1032);
        }

        if (!File.Exists(targetPath))
        {
            throw new InvalidOperationException("AutoCAD reported success but the drawing was not written: " + targetPath);
        }
    }

    public static string ExportBlockToTemp(string blockName)
    {
        string folder = Path.Combine(Path.GetTempPath(), "LSE_Librarian");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, NamingService.Sanitize(blockName) + "_" + DateTime.Now.ToString("HHmmssfff") + ".dwg");
        ExportBlock(blockName, path);
        return path;
    }

    private static string EffectiveName(BlockReference reference, Transaction transaction)
    {
        ObjectId recordId = reference.IsDynamicBlock ? reference.DynamicBlockTableRecord : reference.BlockTableRecord;
        var record = (BlockTableRecord)transaction.GetObject(recordId, OpenMode.ForRead);
        return record.Name;
    }
}
