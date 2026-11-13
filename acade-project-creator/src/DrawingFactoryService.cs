using System.Text.RegularExpressions;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace LSE.AutoCADElectrical.ProjectTools;

public static class DrawingFactoryService
{
    public static DrawingOutcome Create(DrawingJob job, TitleBlockMap titleBlock, WdmMap wdm)
    {
        if (!File.Exists(job.TemplatePath))
        {
            throw new FileNotFoundException("Drawing template was not found.", job.TemplatePath);
        }

        if (File.Exists(job.OutputPath))
        {
            throw new InvalidOperationException("Drawing already exists: " + job.OutputPath);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(job.OutputPath)!);
        var outcome = new DrawingOutcome();
        Database previous = HostApplicationServices.WorkingDatabase;

        using var database = new Database(false, true);
        database.ReadDwgFile(job.TemplatePath, FileOpenMode.OpenForReadAndAllShare, true, null);
        database.CloseInput(true);
        HostApplicationServices.WorkingDatabase = database;

        try
        {
            using (Transaction transaction = database.TransactionManager.StartTransaction())
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

                        string name = EffectiveName(reference, transaction);
                        if (name.Equals(wdm.BlockName, StringComparison.OrdinalIgnoreCase))
                        {
                            outcome.WdmFound = true;
                            if (job.SetWdmValues)
                            {
                                outcome.WdmAttributesSet += SetAttributes(reference, job.WdmValues, transaction);
                            }
                            continue;
                        }

                        if (job.FillTitleBlocks && IsTitleBlock(name, reference, titleBlock, transaction))
                        {
                            outcome.TitleBlocksFound++;
                            outcome.TitleBlockAttributesSet += SetAttributes(reference, job.TitleBlockValues, transaction);
                        }
                    }
                }

                if (!outcome.WdmFound && job.SetWdmValues && job.WdmLibraryFile.Length > 0 && File.Exists(job.WdmLibraryFile))
                {
                    outcome.WdmAttributesSet += InsertWdm(database, blockTable, transaction, job, wdm);
                    outcome.WdmFound = true;
                    outcome.WdmInserted = true;
                }

                transaction.Commit();
            }

            SetSummary(database, job);
            database.SaveAs(job.OutputPath, DwgVersion.Current);
        }
        finally
        {
            HostApplicationServices.WorkingDatabase = previous;
        }

        return outcome;
    }

    public static (bool HasWdm, List<string> TitleBlocks, List<string> Layouts) Inspect(string templatePath, TitleBlockMap titleBlock, WdmMap wdm)
    {
        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException("Drawing template was not found.", templatePath);
        }

        bool hasWdm = false;
        var titleBlocks = new List<string>();
        var layouts = new List<string>();

        using var database = new Database(false, true);
        database.ReadDwgFile(templatePath, FileOpenMode.OpenForReadAndAllShare, true, null);
        database.CloseInput(true);

        using Transaction transaction = database.TransactionManager.StartTransaction();
        var blockTable = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
        foreach (ObjectId recordId in blockTable)
        {
            var record = (BlockTableRecord)transaction.GetObject(recordId, OpenMode.ForRead);
            if (!record.IsLayout)
            {
                continue;
            }

            layouts.Add(record.Name);
            foreach (ObjectId entityId in record)
            {
                if (transaction.GetObject(entityId, OpenMode.ForRead) is not BlockReference reference)
                {
                    continue;
                }

                string name = EffectiveName(reference, transaction);
                if (name.Equals(wdm.BlockName, StringComparison.OrdinalIgnoreCase))
                {
                    hasWdm = true;
                }
                else if (IsTitleBlock(name, reference, titleBlock, transaction))
                {
                    titleBlocks.Add(name + " in " + record.Name + " (" + string.Join(", ", Tags(reference, transaction)) + ")");
                }
            }
        }

        return (hasWdm, titleBlocks, layouts);
    }

    private static string EffectiveName(BlockReference reference, Transaction transaction)
    {
        ObjectId definitionId = reference.IsDynamicBlock ? reference.DynamicBlockTableRecord : reference.BlockTableRecord;
        return ((BlockTableRecord)transaction.GetObject(definitionId, OpenMode.ForRead)).Name;
    }

    private static List<string> Tags(BlockReference reference, Transaction transaction)
    {
        var tags = new List<string>();
        foreach (ObjectId attributeId in reference.AttributeCollection)
        {
            if (transaction.GetObject(attributeId, OpenMode.ForRead) is AttributeReference attribute)
            {
                tags.Add(attribute.Tag);
            }
        }
        return tags;
    }

    private static bool IsTitleBlock(string name, BlockReference reference, TitleBlockMap titleBlock, Transaction transaction)
    {
        if (name.StartsWith("WD_", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var patterns = titleBlock.BlockNames.Where(pattern => pattern.Trim().Length > 0).ToList();
        if (patterns.Count > 0)
        {
            return patterns.Any(pattern => Wildcard(pattern.Trim()).IsMatch(name));
        }

        return Tags(reference, transaction).Any(tag => tag.Equals(titleBlock.SheetAttribute, StringComparison.OrdinalIgnoreCase));
    }

    private static Regex Wildcard(string pattern)
    {
        string expression = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
        return new Regex(expression, RegexOptions.IgnoreCase);
    }

    private static int SetAttributes(BlockReference reference, Dictionary<string, string> values, Transaction transaction)
    {
        int count = 0;
        foreach (ObjectId attributeId in reference.AttributeCollection)
        {
            if (transaction.GetObject(attributeId, OpenMode.ForRead) is not AttributeReference attribute)
            {
                continue;
            }

            if (!values.TryGetValue(attribute.Tag, out string? value) || attribute.TextString == value)
            {
                continue;
            }

            attribute.UpgradeOpen();
            attribute.TextString = value;
            count++;
        }
        return count;
    }

    private static int InsertWdm(Database database, BlockTable blockTable, Transaction transaction, DrawingJob job, WdmMap wdm)
    {
        ObjectId definitionId;
        if (blockTable.Has(wdm.BlockName))
        {
            definitionId = blockTable[wdm.BlockName];
        }
        else
        {
            using var source = new Database(false, true);
            source.ReadDwgFile(job.WdmLibraryFile, FileOpenMode.OpenForReadAndAllShare, true, null);
            source.CloseInput(true);
            definitionId = database.Insert(wdm.BlockName, source, true);
        }

        var modelSpace = (BlockTableRecord)transaction.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForWrite);
        var definition = (BlockTableRecord)transaction.GetObject(definitionId, OpenMode.ForRead);
        var reference = new BlockReference(Point3d.Origin, definitionId);
        modelSpace.AppendEntity(reference);
        transaction.AddNewlyCreatedDBObject(reference, true);

        int count = 0;
        foreach (ObjectId id in definition)
        {
            if (transaction.GetObject(id, OpenMode.ForRead) is not AttributeDefinition attributeDefinition || attributeDefinition.Constant)
            {
                continue;
            }

            var attribute = new AttributeReference();
            attribute.SetAttributeFromBlock(attributeDefinition, reference.BlockTransform);
            if (job.WdmValues.TryGetValue(attributeDefinition.Tag, out string? value))
            {
                attribute.TextString = value;
                count++;
            }
            else
            {
                attribute.TextString = attributeDefinition.TextString;
            }

            reference.AttributeCollection.AppendAttribute(attribute);
            transaction.AddNewlyCreatedDBObject(attribute, true);
        }
        return count;
    }

    private static void SetSummary(Database database, DrawingJob job)
    {
        var builder = new DatabaseSummaryInfoBuilder(database.SummaryInfo)
        {
            Title = job.Title,
            Subject = job.Subject,
            Author = Environment.UserName,
            Comments = "Created by LSE Project Tools " + DateTime.Now.ToString("s")
        };
        database.SummaryInfo = builder.ToDatabaseSummaryInfo();
    }
}
