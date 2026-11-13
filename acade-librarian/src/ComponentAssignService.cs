using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace LSE.AutoCADElectrical.Librarian;

// Assigns a catalog record to a component block in the current drawing: LSEASSIGNCOMPONENT and the Assign button of
// the catalog browser. Merged from the former LSE Catalog Tools plugin on 2026-11-12. The attribute mapping is kept
// as pure functions (PreviewChanges, BuildMap, Warning) so the console smoke test can drive it; only PromptForBlock,
// ReadBlock and Apply touch the AutoCAD API.
public static class ComponentAssignService
{
    private static readonly HashSet<string> DefaultPreserve = new(StringComparer.OrdinalIgnoreCase)
    {
        "TAG", "TAG1", "TAG2", "LOC", "INST", "FAMILY", "XREF", "WDTAGALT"
    };

    public static string PartAttribute(AppSettings settings)
    {
        string attribute = settings.LsePartAttribute.Trim();
        return attribute.Length == 0 ? "USER1" : attribute.ToUpperInvariant();
    }

    public static SelectedBlockInfo? PromptForBlock()
    {
        Document document = AcApp.DocumentManager.MdiActiveDocument
            ?? throw new InvalidOperationException("No active drawing.");

        var options = new PromptEntityOptions("\nSelect an AutoCAD Electrical component block: ");
        options.SetRejectMessage("\nSelect a block reference.");
        options.AddAllowedClass(typeof(BlockReference), true);

        PromptEntityResult selected = document.Editor.GetEntity(options);
        return selected.Status == PromptStatus.OK ? ReadBlock(selected.ObjectId) : null;
    }

    public static SelectedBlockInfo ReadBlock(ObjectId id)
    {
        Document document = AcApp.DocumentManager.MdiActiveDocument;
        using Transaction transaction = document.Database.TransactionManager.StartTransaction();
        var block = (BlockReference)transaction.GetObject(id, OpenMode.ForRead);
        var definition = (BlockTableRecord)transaction.GetObject(block.BlockTableRecord, OpenMode.ForRead);
        var result = new SelectedBlockInfo { Id = id, BlockName = definition.Name };

        foreach (ObjectId attributeId in block.AttributeCollection)
        {
            if (transaction.GetObject(attributeId, OpenMode.ForRead) is AttributeReference attribute)
            {
                result.Attributes[attribute.Tag] = attribute.TextString;
            }
        }

        return result;
    }

    public static List<AttributeChange> Preview(SelectedBlockInfo? block, CatalogRecord record, AppSettings settings)
    {
        return block is null ? new List<AttributeChange>() : PreviewChanges(block.Attributes, record, settings);
    }

    // The proposed value of every attribute the block has, plus the part-number attribute when the block lacks it.
    public static List<AttributeChange> PreviewChanges(
        IReadOnlyDictionary<string, string> attributes,
        CatalogRecord record,
        AppSettings settings)
    {
        var map = BuildMap(record, settings);
        var changes = new List<AttributeChange>();

        foreach (KeyValuePair<string, string> pair in attributes.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            bool preserve = IsPreservedByDefault(pair.Key);
            string proposed = pair.Value;
            if (!preserve && map.TryGetValue(pair.Key, out string? mappedValue))
            {
                proposed = mappedValue;
            }

            changes.Add(new AttributeChange
            {
                Attribute = pair.Key,
                Current = pair.Value,
                Proposed = proposed,
                Preserved = preserve
            });
        }

        string partAttribute = PartAttribute(settings);
        string partNumber = record.Get(CatalogTransactionService.PartColumn(settings));
        if (!attributes.ContainsKey(partAttribute) && !string.IsNullOrWhiteSpace(partNumber))
        {
            changes.Add(new AttributeChange
            {
                Attribute = partAttribute,
                Current = "(missing)",
                Proposed = partNumber,
                Preserved = false
            });
        }

        return changes;
    }

    public static void Apply(
        SelectedBlockInfo blockInfo,
        CatalogRecord record,
        AppSettings settings,
        IEnumerable<AttributeChange> requestedChanges)
    {
        Document document = AcApp.DocumentManager.MdiActiveDocument
            ?? throw new InvalidOperationException("No active drawing.");
        var changes = requestedChanges
            .GroupBy(change => change.Attribute, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

        using (document.LockDocument())
        using (Transaction transaction = document.Database.TransactionManager.StartTransaction())
        {
            var block = (BlockReference)transaction.GetObject(blockInfo.Id, OpenMode.ForWrite);
            var existingTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (ObjectId attributeId in block.AttributeCollection)
            {
                if (transaction.GetObject(attributeId, OpenMode.ForRead) is not AttributeReference attribute)
                {
                    continue;
                }

                existingTags.Add(attribute.Tag);
                if (changes.TryGetValue(attribute.Tag, out AttributeChange? change) && !change.Preserved &&
                    !string.Equals(attribute.TextString, change.Proposed, StringComparison.Ordinal))
                {
                    attribute.UpgradeOpen();
                    attribute.TextString = change.Proposed ?? string.Empty;
                }
            }

            foreach (AttributeChange change in changes.Values)
            {
                if (change.Preserved || existingTags.Contains(change.Attribute) ||
                    string.IsNullOrWhiteSpace(change.Attribute) || change.Current != "(missing)")
                {
                    continue;
                }

                EnsureHiddenAttribute(transaction, block, change.Attribute, change.Proposed ?? string.Empty);
            }

            transaction.Commit();
        }

        document.Editor.Regen();
    }

    // Block attribute -> value from the record. The part number comes from the configured catalog column and lands in
    // USER1, LSE_PART_NUMBER and the configured block attribute; TEXTVALUE KEY=VALUE pairs override everything else.
    public static Dictionary<string, string> BuildMap(CatalogRecord record, AppSettings settings)
    {
        string partNumber = record.Get(CatalogTransactionService.PartColumn(settings));
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["MFG"] = record.Manufacturer,
            ["MANUFACTURER"] = record.Manufacturer,
            ["CAT"] = record.Catalog,
            ["CATALOG"] = record.Catalog,
            ["DESC"] = record.Description,
            ["DESC1"] = record.Description,
            ["RATING"] = record.Rating,
            ["TYPE"] = record.Type,
            ["USER1"] = record.User1,
            ["LSE_PART_NUMBER"] = partNumber,
            [PartAttribute(settings)] = partNumber
        };

        string colorCode = InferColorCode(record);
        if (!string.IsNullOrWhiteSpace(colorCode))
        {
            map["COLOR"] = colorCode;
        }

        foreach (KeyValuePair<string, string> pair in ParseTextValue(record.TextValue))
        {
            map[pair.Key] = pair.Value;
        }

        return map;
    }

    public static string InferColorCode(CatalogRecord record)
    {
        string source = string.Join(" ", new[]
        {
            record.Description,
            record.Type,
            record.Rating,
            record.Catalog,
            record.Get("MISCELLANEOUS1"),
            record.Get("MISCELLANEOUS2")
        });

        var codes = new (string Word, string Code)[]
        {
            ("GREEN", "G"), ("RED", "R"), ("YELLOW", "Y"), ("BLUE", "B"),
            ("WHITE", "W"), ("CLEAR", "C"), ("AMBER", "A"), ("ORANGE", "O"),
            ("BLACK", "K")
        };

        foreach ((string word, string code) in codes)
        {
            if (source.Contains(word, StringComparison.OrdinalIgnoreCase)) return code;
        }

        return string.Empty;
    }

    public static bool IsPreservedByDefault(string tag)
    {
        return DefaultPreserve.Contains(tag) ||
               tag.StartsWith("X", StringComparison.OrdinalIgnoreCase) ||
               tag.Contains("WIRE", StringComparison.OrdinalIgnoreCase) ||
               tag.Contains("PIN", StringComparison.OrdinalIgnoreCase);
    }

    public static Dictionary<string, string> ParseTextValue(string text)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string token in (text ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            int separator = token.IndexOf('=');
            if (separator > 0)
            {
                result[token[..separator].Trim()] = token[(separator + 1)..].Trim();
            }
        }
        return result;
    }

    private static void EnsureHiddenAttribute(
        Transaction transaction,
        BlockReference block,
        string tag,
        string value)
    {
        var definition = (BlockTableRecord)transaction.GetObject(block.BlockTableRecord, OpenMode.ForWrite);
        AttributeDefinition? attributeDefinition = null;

        foreach (ObjectId id in definition)
        {
            if (transaction.GetObject(id, OpenMode.ForRead) is AttributeDefinition candidate &&
                candidate.Tag.Equals(tag, StringComparison.OrdinalIgnoreCase))
            {
                attributeDefinition = candidate;
                break;
            }
        }

        if (attributeDefinition is null)
        {
            attributeDefinition = new AttributeDefinition
            {
                Tag = tag,
                Prompt = tag,
                TextString = string.Empty,
                Invisible = true,
                Position = Autodesk.AutoCAD.Geometry.Point3d.Origin,
                Height = 0.1
            };
            definition.AppendEntity(attributeDefinition);
            transaction.AddNewlyCreatedDBObject(attributeDefinition, true);
        }

        var attribute = new AttributeReference();
        attribute.SetAttributeFromBlock(attributeDefinition, block.BlockTransform);
        attribute.TextString = value;
        attribute.Invisible = true;
        block.AttributeCollection.AppendAttribute(attribute);
        transaction.AddNewlyCreatedDBObject(attribute, true);
    }

    public static string CompatibilityWarning(SelectedBlockInfo? block, CatalogRecord record)
    {
        return block is null ? string.Empty : Warning(block.Family, block.BlockName, record);
    }

    // A soft warning, never a hard stop: the block's FAMILY against the family table, and the record's WDBLKNAM filter
    // against the block name.
    public static string Warning(string family, string blockName, CatalogRecord record)
    {
        if (!string.IsNullOrWhiteSpace(family) &&
            !record.TableName.Contains(family, StringComparison.OrdinalIgnoreCase) &&
            !family.Contains(record.TableName, StringComparison.OrdinalIgnoreCase))
        {
            return $"Selected block FAMILY '{family}' does not match catalog family " +
                   $"'{record.TableName}' ({FamilyNames.Friendly(record.TableName)}).";
        }

        if (!string.IsNullOrWhiteSpace(record.WdBlkNam) &&
            !blockName.Contains(record.WdBlkNam, StringComparison.OrdinalIgnoreCase))
        {
            return $"Catalog filter WDBLKNAM '{record.WdBlkNam}' does not match block '{blockName}'.";
        }

        return string.Empty;
    }
}
