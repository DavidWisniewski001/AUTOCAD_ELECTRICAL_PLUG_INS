# Code reference: LSE Librarian

Maintained by DCW. Last reviewed 2026-11-12.

Every file, type and public member of `LSE.AutoCADElectrical.Librarian`. Private helpers are summarised. Read [ARCHITECTURE.md](ARCHITECTURE.md) first for how the pieces fit.

Density: `Models.cs`, `SettingsService.cs`, `AccessCatalogService.cs`, `NamingService.cs`, `NamingWorkbookService.cs`, `PartNumberService.cs`, `IconMenuService.cs`, `LibraryTransactionService.cs`, `CatalogTransactionService.cs`, `LibraryAuditService.cs`, `DiagnosticsService.cs`, `LibrarySettingsForm.cs` and `Plugin.cs` are compact (one member per line). `DrawingInspectionService.cs`, `IconRenderService.cs`, `LibrarianForm.cs`, `CatalogRecordForm.cs` and `AuditForm.cs` are expanded, and since 2026-11-12 so are `CatalogBrowserForm.cs` and `ComponentAssignService.cs`; `CompanyStandardsService.cs` and `VaultCatalogService.cs` are compact. Twenty-four source files in all (the four new ones are `CatalogBrowserForm.cs`, `ComponentAssignService.cs`, `CompanyStandardsService.cs`, `VaultCatalogService.cs`).

## src/Models.cs

| Type | Members | Notes |
|---|---|---|
| `enum LibraryKind` | `SchematicSymbol, PanelFootprint, Nameplate, UserCircuit` | Keys of `kinds` in the JSON. |
| `enum IconSource` | `Render, Thumbnail, ExistingImage, ImageFile, None` | Where the icon image comes from. |
| `enum Severity` | `Error, Warning, Info` | Ordered; the audit filter uses `<=`. |
| `sealed class AppSettings` | folder and file properties listed in [DATA-STORES.md](DATA-STORES.md); `IconPixels`, `DefaultSeries`, `RegisterFootprintLookup`, `DeepAudit`, `AuditStockEntries`, `RecentItems`; catalog record properties `NamingWorkbookPath`, `NamingStandardPath`, `CatalogBackupFolder`, `LsePartColumn` (`USER1`), `OfferCatalogRecord` (true), `PurchaseDescriptionInUser2` (true); browser and Vault properties (2026-11-12) `CompanyStandardsPath` (default set in `SettingsService.Load`), `LsePartAttribute` (`USER1`; the block attribute that receives the part number, distinct from `LsePartColumn`), `Favorites` and `Recent` (lists of `CatalogRecord.Key`, `Recent` at most 25, most recent first), `VaultExplorerPath` (`C:\Program Files\Autodesk\Vault Client 2026\Explorer`), `VaultServer` (`lse-cep-sql`), `VaultName` (`Lake_Shore_E_Vault`), `CatalogVaultPath` (blank = no Vault step); computed `SchematicMenuPath`, `PanelMenuPath`, `ImagesFolder`; `RootFor(LibraryKind)`, `MenuPathFor(KindRule)` | The one settings POCO. |
| `enum CatalogScope` | `CompanyStandards, PersonalFavorites, Recent, AllCatalog` | The four tree scopes of the browser. |
| `sealed class CatalogRecord` | `TableName`; `Values` (every column as a trimmed string, case-insensitive keys); shortcuts `Catalog`, `Manufacturer`, `Description`, `Type`, `Rating`, `User1`, `TextValue`, `WdBlkNam`; `Get(column)`; `Key` = `TABLE\|MANUFACTURER\|CATALOG` upper-cased | One catalog row as the browser sees it. |
| `sealed class StandardEntry` | `RecordKey`, `Category` (`Uncategorized`), `Status` (`Company Standard` or `Legacy`), `Notes`; `IsLegacy` | One element of `CompanyStandards.json`. |
| `sealed class SelectedBlockInfo` | `Id` (`ObjectId`), `BlockName`, `Attributes` (tag to value), `Family` (the FAMILY attribute) | The block picked for assignment. |
| `sealed class AttributeChange` | `Attribute`, `Current`, `Proposed`, `Preserved` | One row of the attribute change preview. |
| `sealed class LibraryStandard` | `Source`, `ImagePrefix`, `IgnoreFolders`, `Series`, `SymbolFamilies`, `Kinds`; `Rule(LibraryKind)` throws when the kind is missing | Deserialised JSON. |
| `sealed class KindRule` | `DisplayName`, `Menu`, `Prefixes`, `NamePattern`, `NameExample`, `NameHelp`, `MaxNameLength`, `RequiredAttributes`, `RecommendedAttributes`, `RequiredAttributePatterns`, `RecommendedAttributePatterns`, `ForbiddenBlocks`, `ActionTemplate`, `MenuRootPages`, `Categories`; `IsPanel` | One per kind. |
| `sealed class LibraryCategory` | `Name`, `Folder`, `MenuPage`; `ToString()` = name | Combo item. |
| `sealed class LibraryRequest` | `Kind`, `SourcePath`, `SourceBlockName`, `Name`, `TargetFolder`, `Description`, `MenuPage`, `CreatePage`, `NewPageTitle`, `ParentPage`, `MenuLabel`, `Icon`, `IconPath`, `RegisterLookup`, `LookupManufacturer`, `LookupCatalog`, `AddCatalogRecord`, `PartNumber` (`PartNumberRequest?`), `CatalogRecord` (`CatalogAddRequest?`); computed `TargetPath` (`TargetFolder\Name.dwg`), `FromCurrentDrawing` (block name set), `InPlace` (source equals target) | Built by the wizard, consumed by the transaction. |
| `sealed class LibraryResult` | `Request`, `DrawingPath`, `ImagePath`, `MenuPath`, `MenuPage`, `Report`, `PartNumber` (blank unless a catalog record was added) | |
| `sealed class AttributeInfo` | `Tag`, `Prompt`, `Default`, `Invisible`, `Constant` | One attribute definition. |
| `sealed class DrawingInfo` | `Path`, `Attributes`, `BlockReferences`, `EntityCount`, `MinX/MinY/MaxX/MaxY`, `HasExtents`, `Width`, `Height`, `HasThumbnail`, `Units`, `DwgVersion`; `HasAttribute(tag)` | Output of inspection; input of the rule check. |
| `sealed class IconMenu` | `Path`, `Encoding`, `NewLine`, `TrailingNewLine`, `Header`, `Pages`; `Page(id)` | Parsed `.DAT`. |
| `sealed class IconMenuPage` | `Id` (`M110`), `Number` (110, or -1), `Layout` (`D3W` or null), `Prelude`, `Title`, `Entries` | |
| `sealed class IconMenuEntry` | `Label`, `Image`, `Action`, `Raw`; `IsEntry` (Raw is null), `Submenu` (target of `$S=`), `ToString()` renders the line | |
| `sealed class MenuTarget` | `Kind` (`Submenu`, `Command`, `Path`, `Block`, `Other`), `Path`, `BlockName`, `Command` | Output of `ResolveAction`. |
| `sealed class AuditFinding` | `Severity`, `Area` (`Menu`, `Library`, `Lookup`, `Drawing`), `Kind`, `Path`, `Message`, `Suggestion`, `SeverityText` | Grid row. |
| `sealed class AuditReport` | `Started`, `Summary`, `Findings`, `Errors`, `Warnings`, `Infos` | |
| `sealed class AuditOptions` | `Deep`, `IncludeStockEntries` | |
| `sealed class NamingStandard` | `Source`, `Loaded`, `FromWorkbook`, `Format`, `Tabs`, `SkippedTabs`, `Manufacturers` (code → `ManufacturerCode`, case-insensitive), `FamilyHints` (category → family tables; filled in the snapshot only); `Tab(name)`, `AllKnownPartNumbers()` | The part-number naming standard read from the workbook or its JSON snapshot. |
| `sealed class NamingFormat` | `CategoryChars` 2, `ManufacturerChars` 3, `TypeChars` 2, `SequenceChars` 7; `Length` (14) | Widths parsed from the "Part number:" rule. |
| `sealed class NamingTab` | `Name` (sheet name), `Title`, `PartNumberRule`, `CategoryCodes`, `CategoryRule`, `Types` (code → meaning), `Manufacturers` (code → name), `DescriptionPattern`, `PurchaseDescriptionPattern`, `DescriptionFields`, `Rules`, `Examples`, `KnownPartNumbers` (sorted, standard form only), `ManufacturerSpellings` (code → MANUFACTURER spelling used on the tab); `ToString()` = name | One per workbook tab with a naming block. Combo item. |
| `sealed class DescriptionField` | `Name`, `Format`, `AllowedValues`, `Example` | One row of the "Description Field" table. |
| `sealed class NamingExample` | `PartNumber`, `Description`, `PurchaseDescription` | One "Examples" row. |
| `sealed class ManufacturerCode` | `Name`, `AcadeManufacturer` (spelling most used in the part tables, e.g. `BUSSMANN` for `BUS`) | Union over all tabs. |
| `sealed class PartNumberRequest` | `Tab`, `Category`, `ManufacturerCode`, `Type`, `ManualPartNumber`; computed `Prefix` (upper-cased category + code + type), `IsManual` | What the dialog asks for. |
| `sealed class CatalogAddRequest` | `TableName`, `Catalog`, `Manufacturer`, `Description`, `Type`, `Rating`, `Voltage`, `User1` (the part number, set by `Run`), `User2`, `User3`, `TextValue`, `WebLink`, `WdBlkNam`; computed `Summary` (`table | manufacturer | catalog`) | The row to insert. |
| `sealed class PartNumberPreview` | `PartNumber`, `Prefix`, `HighestInCatalog`, `HighestInCatalogTable`, `HighestInWorkbook`, `CatalogMatches`, `WorkbookMatches`, `CatalogLegacy`, `WorkbookLegacy`, `Explanation` | Output of `PartNumberService.Preview`. |
| `sealed class CatalogAddResult` | `PartNumber`, `TableName`, `BackupFolder`, `Report` | Output of the catalog transaction. |
| `sealed class FootprintMapRow` | `Family`, `Manufacturer`, `Catalog`, `PartNumber`, `Description`, `BlkNam`, `Status` | One LSE part and its lookup state. |
| `sealed class FootprintMapRequest` | `Manufacturer`, `Catalog`, `Footprint`, `Description`, `Overwrite`; computed `Summary` | One mapping to write. `Overwrite` must be set explicitly to replace a different existing path. |
| `sealed class FootprintMapResult` | `Written`, `Unchanged`, `BackupFolder`, `Report` | Output of `FootprintMappingService.Execute`. |
| `sealed class SymbolLinkRequest` | `Table`, `Manufacturer`, `Catalog`, `PartNumber`, `Symbol`, `Overwrite`, `Summary` | One catalog row and the schematic symbol block to store in its WDBLKNAM. |
| `sealed class SymbolLinkResult` | `Written`, `Unchanged`, `BackupFolder`, `Report` | Output of `CatalogTransactionService.SetSymbols`. `FootprintMapRow` also carries `Symbol` (the row's WDBLKNAM; filled by `Load(s, true)`). |

## src/SettingsService.cs

### `static class SettingsService`

| Member | Behaviour |
|---|---|
| `SettingsPath`, `LogFolder` | `%APPDATA%\LSE\AutoCADElectricalLibrarian\settings.json` and `...\Logs`. |
| `const DefaultStandardsRoot` | `C:\_Lake_Shore_E_Vault\CAD Standards\AutoCAD\ECAD Standards`. |
| `AppSettings Load()` | Deserialises (any failure gives defaults), then fills blank paths from the standards root when it exists on `C:` or any fixed or network drive, from `Documents\Acade 2026\AeData\en-US\Catalogs` for the MDBs, `LseLibraryStandard.json` and `LseNamingStandard.json` next to the DLL, `Backups` under the settings folder, the workbook from `DiscoverWorkbook`, `LsePartColumn` to `USER1`; normalises blank file names, `IconPixels < 16`, blank series. |
| `void Save(AppSettings)` | Indented JSON through `.tmp` and `File.Move(..., true)`. |
| `string DiscoverWorkbook(string root, string catalogPath)` | First existing `LSE ELECTRICAL PARTS CATALOG MANAGER.xlsx` among: the catalog's folder and up to four parents, `<root>`, `<root>\E-CAD .mbd ACCESS FILES\`, `Documents\AutoCAD_Electrical_2026\`; blank when none. |
| `List<string> ApplyProject(AppSettings, string wdpPath)` | Reads `+[1]`..`+[4]` with `ReadOptions` and sets the symbol roots, menu folder and file names, footprint roots; records `ReferenceProjectPath`; returns the lines applied (or a note that nothing was found). Throws `FileNotFoundException`. |
| `Dictionary<int,string> ReadOptions(string wdpPath)` | BOM-aware (UTF-16 LE/BE, UTF-8) read of every `+[n]` line. |

### `static class LibraryStandardService`

| Member | Behaviour |
|---|---|
| `LibraryStandard Load(string path)` | Case-insensitive JSON; rebuilds dictionaries case-insensitive; requires all four kinds, a compiling `namePattern`, `{PATH}` in every `actionTemplate`; `MaxNameLength` below 8 becomes 64; blank `ImagePrefix` becomes `IMAGES\`; sets `Source`. Throws `FileNotFoundException` or `InvalidOperationException`. |

## src/AccessCatalogService.cs

### `static class Ace`

Copied from the Inventor add-in. `Open(path)` (provider fallback ACE 16, ACE 12, Jet 4), `Tables(c)`, `Exec(c, sql)`, `E(value)` (quote escaping), `ValidateTable(name)` (`^[A-Za-z0-9_][A-Za-z0-9_ &/\.\-]*$`), `Release(o)`, `Close(c)`.

### `sealed class FootprintLookupService`

| Member | Behaviour |
|---|---|
| `FootprintLookupService(string path)` | |
| `List<string> GetTables()` | Non-underscore tables, sorted. |
| `bool TableExists(string)` | |
| `void EnsureTable(string)` | `CREATE TABLE [t] ([CATALOG] TEXT(60), [ASSEMBLYCODE] TEXT(24), [BLKNAM] TEXT(255), [DESCRIPTION] TEXT(150), [RECNUM] COUNTER)` when missing. |
| `string? Find(string table, string catalog)` | `BLKNAM` of the row with that catalog and `ASSEMBLYCODE IS NULL`, or null. |
| `void Upsert(table, catalog, blkName, description)` | Update or insert; `BLKNAM` over 255 characters throws; description truncated to 150. |
| `List<(Table, Catalog, BlkNam)> ReadAllPaths()` | Every row with a non-empty `BLKNAM` from every table; tables that fail to read are skipped. Used by the audit. |
| `static string Key(mfr, cat)` | `mfr|cat`, trimmed; the dictionary key used by `ReadMappings`. |
| `string? ResolveTable(string manufacturer)` | The existing manufacturer table (case-insensitive) or null; validates the name. |
| `(int Rows, string BlkNam) FindPlain(table, catalog)` | Count of plain rows (`ASSEMBLYCODE` null or empty) for the catalog number and the first BLKNAM. |
| `void SetMapping(table, catalog, blkName, description)` | Updates every plain row's BLKNAM, or inserts one (DESCRIPTION only on insert, cut to 150); BLKNAM over 255 throws. |
| `Dictionary<string,string> ReadMappings(IEnumerable<(Manufacturer, Catalog)>)` | BLKNAM of the plain row for each key that has one, keyed by `Key`; each involved manufacturer table is fetched once with `Recordset.GetRows` (bulk transfer; `IN (...)` lists and per-cell dynamic reads took 30 s on the 519,000-row lookup, this takes about 5 s) and filtered in memory. Manufacturers without a table are skipped. |

### `sealed class CatalogService`

`default_cat.mdb`: one table per AutoCAD Electrical family. Same ADODB discipline as `FootprintLookupService` (open per call, `finally` release and close). Reads scan every family table because a prefix can be issued in any of them.

| Member | Behaviour |
|---|---|
| `CatalogService(string path)`, `Path` | |
| `static void ValidateColumn(string)` | `^[A-Za-z][A-Za-z0-9_]{0,63}$` or throws. |
| `List<string> GetTables()` | Family tables: non-underscore tables, sorted. |
| `Dictionary<string,string> GetFamilyDescriptions()` | `FAMILY` → `DESC` from `_FAMILY_DESCRIPTION`; empty when the table is missing. |
| `Dictionary<string,int> GetColumns(string table)` | Column name → `DefinedSize` (0 = memo or unknown) from `SELECT TOP 1 *`. |
| `List<(Value, Table)> ReadColumnStartingWith(string column, string prefix)` | Every non-blank value of `[column] LIKE 'prefix%'` with its table, across all family tables; unreadable tables skipped. |
| `List<(Table, Manufacturer, Catalog, Part, Description)> ReadParts(string partColumn)` | Every row whose part column is not empty, across the family tables (tables without the column are skipped). Read only; feeds `FootprintMappingService.Load`. |
| `List<CatalogRecord> ReadTable(string table, int limit = 20000)` | `SELECT TOP limit *` from one family table (table name validated); every column of every row becomes a trimmed string in `CatalogRecord.Values`. Feeds the browser. Read only. |
| `CatalogRecord? FindByKey(string key)` | Direct `SELECT TOP 1` by MANUFACTURER and CATALOG from a `TABLE\|MANUFACTURER\|CATALOG` key; null for a malformed key (fewer than three parts) or an unknown record. |
| `List<CatalogRecord> Query(string table, string sql)` (private) | Runs the SELECT and maps each row; shared by the two members above. |
| `List<string> ReadDistinct(string table, string column)` | Sorted distinct non-null values (used for the MANUFACTURER list). |
| `bool Exists(string table, string mfg, string cat)` | Row with that `MANUFACTURER` and `CATALOG`. |
| `(Table, Manufacturer, Catalog)? FindByColumn(string column, string value)` | First table holding `[column] = value`, or null. |
| `void AddRecord(CatalogAddRequest, string partColumn)` | Throws when the table lacks the part column. Candidate columns: `CATALOG`, `MANUFACTURER`, `DESCRIPTION`, `TYPE`, `RATING`, `VOLTAGE`, the part column (= `User1`), `USER2`, `USER3`, `TEXTVALUE`, `WEBLINK`, `WDBLKNAM`; only those the table has and that are non-empty are written; defined sizes enforced; empty part number refused; one `INSERT`. |
| `Dictionary<string,List<string>> FamilyHints(NamingFormat)` | Category code → up to three family tables holding standard-form numbers in `USER1`, most used first. Written into the snapshot. |

### `sealed class IssuanceLock : IDisposable`

`PathFor(catalogPath)` = `<catalog folder>\LSE_PartNumber.lock` (the Inventor add-in's lock file); `Acquire(catalogPath, log)` opens it exclusively with delete-on-close, 40 tries at 250 ms, writes `user@machine time`, logs `Part number issuance lock: PASS`, otherwise throws "Another workstation is issuing a part number right now"; `Dispose` releases (and deletes) it.

## src/NamingWorkbookService.cs

### `static class NamingWorkbookService`

Reads the LSE parts catalog workbook directly: an `.xlsx` is a zip of XML, opened with `System.IO.Compression` and parsed with `XmlReader` (DTDs prohibited). No Excel, no NuGet. The file is opened with `FileShare.ReadWrite | Delete` so it may stay open in Excel.

| Member | Behaviour |
|---|---|
| `const WorkbookFileName` | `LSE ELECTRICAL PARTS CATALOG MANAGER.xlsx`. |
| `NamingStandard Load(AppSettings)` | `LoadWorkbook(NamingWorkbookPath)` when that file exists, else `LoadJson(NamingStandardPath)` when that exists, else `FileNotFoundException` naming the settings row. |
| `string Describe(NamingStandard)` | `N naming tabs, N manufacturer codes, N type codes, N part numbers listed, format 2+3+2+7 = 14 chars; no naming block on: ...`. Used by diagnostics, the settings test and the dialog. |
| `NamingStandard LoadWorkbook(string path, bool force = false)` | Reads the sheets whose name starts with `LSE `, parses, sets `Source` and `FromWorkbook`. Cached in memory under a lock keyed by upper-cased full path + `LastWriteTimeUtc` ticks + length; `force` bypasses the cache. |
| `NamingStandard LoadJson(string path)` | Case-insensitive JSON; rebuilds every dictionary case-insensitive; throws when empty or without tabs; a `Format` shorter than 8 becomes the default; `FromWorkbook` false, `Loaded` now. |
| `void SaveJson(NamingStandard, string path)` | Indented JSON, UTF-8 without BOM, through `.tmp` and move. |
| `NamingStandard Parse(Dictionary<string,List<string[]>> sheets, string sourceName)` | Per sheet: row 1 containing "NAMING" is the title; the first "Part number" row gives `PartNumberRule` and (once) the `Format` from `[Category n][Mfr code n][Type n][Sequence n]`; "Description:" and "Purchase Desc" rows give the patterns; the "Part # Segment" header row locates the Manufacturer and Code columns (defaults 5 and 6); "Description Field", "Examples", "APPENDIX" and every "Internal ID" header row are noted. Sheets without a "Part # Segment" row go to `SkippedTabs`. Then: the "Category" row's rule text gives `CategoryCodes` (every two-digit number) and the "Type" row gives `Types` from `XX = meaning` pairs separated by `;`; the Manufacturer / Code rows between the segment header and the first part table (or APPENDIX) give `Manufacturers`, with `ABB / KEY` style names and codes split pairwise; the "Description Field" rows (until a blank, "Examples", "APPENDIX" or a part table) give `DescriptionFields`, and the "Rules" column (or the manufacturer column) within 16 rows gives the numbered `Rules`; "Examples" rows are read while column B holds a standard-form number; every part table under an "Internal ID" header contributes the standard-form values of the "LSE Part Number" and "LSE Recommended Part Number" columns to `KnownPartNumbers`, and the recommended column together with the "Manufacturer" column counts spellings per code. A tab with no category codes takes them from the recommended (or all listed) numbers and notes it in `CategoryRule`; a tab without type codes is skipped. Finally the most-used spelling per code becomes `ManufacturerCode.AcadeManufacturer`, the union `Manufacturers` is sorted, and the whole thing throws when no tab survived. |
| `Dictionary<string,List<string[]>> ReadSheets(string path, Func<string,bool> sheetFilter)` | Reads `xl/_rels/workbook.xml.rels`, `xl/sharedStrings.xml` (rich-text runs joined, phonetic runs skipped), `xl/workbook.xml` (sheet name → part), then each matching sheet. Returns one `string[]` per row (row 1 = index 0, at least 16 cells, missing rows empty). |

Private: `ReadSheet` (cell references to column indexes, shared strings, inline strings, booleans, integral numbers rendered without decimals), `Clean` (strips `_x000D_`, collapses whitespace, trims), `IndexOf`, `ColumnIndex`.

## src/PartNumberService.cs

### `static class PartNumberService`

Pure logic over a `NamingFormat` and lists of existing numbers. Part number = `[Category][Mfr code][Type][Sequence]`, 2+3+2+7 = 14 characters by default.

| Member | Behaviour |
|---|---|
| `void Validate(NamingFormat, PartNumberRequest)` | Category = digits of the category width, manufacturer code = letters, type = letters; a manual number must match `StandardForm` and start with `Prefix`. Throws `InvalidOperationException` with an example. |
| `Regex Pattern(NamingFormat, prefix)` | `^<prefix>([0-9]{7})$`. |
| `Regex StandardForm(NamingFormat)` | `^[0-9]{2}[A-Z]{3}[A-Z]{2}[0-9]{7}$` (widths from the format). |
| `string Normalize(string)` | Trim and upper-case. |
| `long LegacyFloor(NamingFormat)` | 10^(SequenceChars - 1) = 1,000,000 for seven digits. |
| `bool IsLegacySequence(NamingFormat, long)` | At or above the floor: a legacy ERP number kept under rule 7 (`45BUSCF1633301`), reserved but not counted. |
| `long Highest(NamingFormat, prefix, existing)` | Highest non-legacy sequence with that prefix, 0 when none. |
| `string Format(NamingFormat, prefix, sequence)` | Zero-padded; throws "Sequence exhausted" outside 1 to 10^n - 1. |
| `PartNumberPreview Preview(NamingFormat, PartNumberRequest, catalogValues (value, table), workbookValues)` | Counts matches, legacy numbers and the highest issued per source; manual: refuses a number already in the catalog, notes whether the workbook lists it; automatic: next = max(highest in catalog, highest in workbook) + 1, skipping any used sequence. `Explanation` is the multi-line reasoning. |
| `string Next(NamingFormat, prefix, existing)` | Next unused non-legacy sequence over one list. |
| `(Category, Manufacturer, Type, Sequence)? Split(NamingFormat, partNumber)` | Null unless in standard form. |
| `string PurchaseDescription(description, catalogNumber)` | `DESCRIPTION, MPN <catalog>`; description alone when the catalog number is blank; blank when the description is blank. |
| `List<string> DescriptionProblems(NamingTab?, description)` | Required; over 150 (catalog column) or over 100 (standard, worded "the LSE standard asks for"); lower-case letters; `N/A`; degree, diameter or prime symbols. Empty list = clean. |

## src/CatalogTransactionService.cs

### `static class CatalogTransactionService`

The only writer of `default_cat.mdb` in this plugin. Step by step in [ARCHITECTURE.md](ARCHITECTURE.md) section 5.1.

Symbol link (2026-11-12): `string ResolveSymbol(AppSettings, LibraryStandard, string name)` validates the block name (no folders or .dwg, 60 characters) and finds `<name>.dwg` under `SymbolLibraryFolder` then `StockSymbolLibraryFolder`, returning the library spelling; `SymbolLinkResult SetSymbols(AppSettings, LibraryStandard, IReadOnlyList<SymbolLinkRequest>, Action<string>? progress)` updates only WDBLKNAM (one row per request, all or nothing). Uses `CatalogService.ReadSymbols`, `FindSymbolRows`, `SetSymbol`.

| Member | Behaviour |
|---|---|
| `string BackupRoot(AppSettings)` | `CatalogBackupFolder`, or `<catalog folder>\LSE_Catalog_Backups` when blank. |
| `string PartColumn(AppSettings)` | `LsePartColumn` upper-cased, `USER1` when blank. |
| `void VaultReady(AppSettings)` (private) | When `VaultCatalogService.Enabled` and the `VaultExplorerPath` folder does not exist, throws "A catalog path in Vault is set but the Vault Explorer folder was not found: ... (LSELIBRARYSETTINGS)." Called from `Validate` and at the top of `SetSymbols`, before the backup and the lock. |
| `void VaultUndo(AppSettings, bool vault, Action<string> log)` (private) | Error path only, after the file rollback: when a checkout is held, `VaultCatalogService.UndoCheckout` and log `Vault undo checkout: PASS` or `Vault undo checkout: FAILED <reason>`; never throws. |
| `void Validate(AppSettings, NamingStandard, PartNumberRequest, CatalogAddRequest)` | Catalog path set and exists, part column valid, tab exists, `PartNumberService.Validate`, type code defined on the tab, family table chosen and valid, MANUFACTURER and CATALOG present (24, 60), description problems other than the 100-character warning, TYPE/RATING/VOLTAGE/USER2/USER3 at most 100, TEXTVALUE and WEBLINK 255, WDBLKNAM 60, WEBLINK starting with `http://` or `https://`. |
| `PartNumberPreview Preview(AppSettings, NamingStandard, PartNumberRequest)` | Read-only: existing values of the part column with the prefix from every table, plus the tab's `KnownPartNumbers`, through `PartNumberService.Preview`. |
| `List<(Table, Count)> FamilyUsage(AppSettings, NamingStandard, string category)` | Family tables holding standard-form numbers with that category code, most used first. Drives the family hint in the dialog. |
| `CatalogAddResult Execute(AppSettings, NamingStandard, PartNumberRequest, CatalogAddRequest, Action<string>? progress)` | Stand-alone run: `Validate`, header lines (started, catalog, naming source, record, prefix and tab), `Run`, `Status: SUCCESS`, `CatalogReport.txt` into the backup folder, `Report` = the log. |
| `CatalogAddResult Run(AppSettings, NamingStandard, PartNumberRequest, CatalogAddRequest, Action<string> log)` | The core, also called by `LibraryTransactionService`: `Validate`; create `<BackupRoot>\<yyyyMMdd_HHmmss>\` (suffix `_n` if taken); `IssuanceLock.Acquire`; copy the catalog into the backup; read existing numbers; `PartNumberService.Preview`; refuse an existing part number (`FindByColumn`) or an existing MANUFACTURER + CATALOG (`Exists`); set `User1`, `AddRecord`; read back both ways; log `Catalog insert into <table> (<column>=<number>, <mfg> <catalog>): PASS`. With `CatalogVaultPath` set (2026-11-12): after the backup copy, `VaultCatalogService.Checkout` and log `Vault checkout: PASS`; after the read-back, `Checkin` with the comment `LSE Librarian: add catalog record <MFG> <CAT> (<column>=<part>)` and log `Vault checkin: PASS`. On any exception: log `ERROR:`, copy the backup over the catalog (`Catalog rollback: PASS`, or `PASS (nothing had been written)`, or `FAILED ...`), then `VaultUndo` when a checkout is held, write `CatalogReport.txt` with the error, rethrow. |
| `SetSymbols` Vault steps (2026-11-12) | Same pattern as `Run`: `VaultReady` first; after the backup and lock, `Checkout` (`Vault checkout: PASS`); after the WDBLKNAM read-back, `Checkin` with `LSE Librarian: set WDBLKNAM on <n> catalog row(s)` (`Vault checkin: PASS`); on failure the file rollback, then `VaultUndo`. With `CatalogVaultPath` blank no Vault line appears in any report. |

## src/CatalogRecordForm.cs

### `sealed class CatalogRecordForm : Form`

Constructor `CatalogRecordForm(AppSettings settings, bool embedded, CatalogAddRequest? prefill, PartNumberRequest? prefillPart)`. Title "LSE Librarian: add a catalog record" (stand-alone) or "LSE Librarian: catalog record" (embedded); the main button reads **Add to catalog** or **Use in Librarian**. Outputs: `PartRequest` and `CatalogRequest` (embedded, after OK), `Result` (`CatalogAddResult`, stand-alone, after a successful add). Size 1180 x 940, minimum 960 x 700, scrolling body, footer with status, **Preview next number**, the main button and **Cancel** (becomes **Close** after a successful add).

| Row | Control and behaviour |
|---|---|
| Naming standard source | Label: `Workbook: <path>` or `JSON snapshot (workbook not reachable): <path>` plus `Describe`; **Reload** re-parses the workbook bypassing the cache. |
| Workbook tab (LSE category sheet) | Drop-down of `NamingStandard.Tabs`; pre-selected from `prefillPart.Tab`, else the first. Changing it refills every code list, the pattern, the builder and the help box. |
| Category code (2 digits) | Editable combo of the tab's `CategoryCodes`; the rule text is shown beneath. |
| Manufacturer code (3 letters) | Editable, auto-complete combo of `CODE  name` items: the tab's codes first, then every other code marked `(other tab)`. Changing it fills MANUFACTURER with the tab's spelling (else the code's `AcadeManufacturer`, else the name up to the first `/` or `(`, upper-cased) when MANUFACTURER is still blank. |
| Type code (2 letters) | Drop-down of `CODE  meaning` from the tab. |
| LSE part number | Bold label: `<prefix>???????  (press Preview next number)` until previewed, then the number; the explanation beneath. Any change to the codes or the manual number clears the preview. |
| Keep an existing LSE number instead (rule 7) | Check box enabling "Existing part number" (upper-cased, 20 max); shown as `<number>  (not verified yet)` until previewed. |
| AutoCAD Electrical family table | Drop-down of `CODE  description` (`GetTables` + `GetFamilyDescriptions`). After a preview the hint line names the tables already holding numbers with that category and the first is auto-selected unless the user chose one (or the prefill named one). Changing it reloads the MANUFACTURER list. |
| MANUFACTURER (as spelled in the catalog) | Editable combo of `ReadDistinct(table, "MANUFACTURER")`, 24 max; **Reload list**. |
| CATALOG (manufacturer part number) | 60 max; changing it refreshes USER2. |
| DESCRIPTION (ALL CAPS, 100 or fewer) | Upper-cased, 150 max; the tab's pattern beneath; live `DescriptionProblems` (except "required") in red. |
| Description builder | One upper-cased box per `DescriptionField` (label `Name (Format)`, placeholder `e.g. <Example>`, tooltip `Allowed: <AllowedValues>`); **Build description** joins the non-empty boxes with `, ` into DESCRIPTION. A tab without fields shows a note. |
| USER2 = purchase description | Check box (default `PurchaseDescriptionInUser2`); when ticked USER2 is read-only and follows `PurchaseDescription(DESCRIPTION, CATALOG)` cut to 100; a prefilled `User2` unticks it. |
| TYPE, RATING, VOLTAGE, USER3 (100), TEXTVALUE, WEBLINK (255), WDBLKNAM (60) | Plain text boxes. |
| Also map this part to a footprint drawing (stand-alone only) | Check box enabling a path box and **Browse...** (starts in the footprint library). On **Add to catalog** the footprint is validated and any different existing lookup row is asked about before the catalog is touched; after the catalog insert succeeds `FootprintMappingService.Execute` runs as a second, separate transaction. A mapping failure restores the lookup MDB, keeps the catalog row and says so. Not shown embedded. |
| Tab rules and examples | Read-only box: title, part number rule, purchase description pattern, the numbered rules, the examples (`number  description`), and the counts of listed numbers, type codes and manufacturer codes on the tab. |
| Run log / Notes | Read-only log (stand-alone it receives the transaction log). |

`LoadReferenceData` (on `Shown`) loads the standard and the catalog lists; any failure shows the message and closes the form. `Preview(showHint)` calls `CatalogTransactionService.Preview` and, with the hint, `FamilyUsage`. `Add()`: `CatalogTransactionService.Validate`, then a Yes/No warning for any description problems (including the 100-character one), a preview if none is current; embedded: sets `PartRequest` and `CatalogRequest`, `DialogResult.OK`; stand-alone: OK/Cancel confirmation naming the record and number, then `CatalogTransactionService.Execute` with log lines echoed, a message with the number and backup folder, CATALOG cleared for the next part. Errors are shown with the title "LSE Librarian: the catalog was not changed". Private helpers: `Guard`, `Code(ComboBox)` (first word of the text, or the item's code), `SelectCode`, `AddRow`, `CreateButton`, records `CodeItem` and `FamilyItem`.

## src/FootprintMappingService.cs

### `static class FootprintMappingService`

Maps catalog parts (MANUFACTURER + CATALOG) to footprint DWGs in `footprint_lookup.mdb`. `Load` and `Existing` are read only; `Execute` is the only writer of stand-alone mapping and follows the transaction pattern of the other two services.

| Member | Behaviour |
|---|---|
| `const Unmapped, Mapped, MissingDrawing, BlockName` | Status texts (`Unmapped`, `Mapped`, `Missing drawing`, `Block name`). |
| `string Status(string blkNam)` | Empty = Unmapped; no path separator = Block name; path that exists = Mapped; otherwise Missing drawing. |
| `List<FootprintMapRow> Load(AppSettings)` | Needs the catalog and lookup MDBs. `CatalogService.ReadParts(PartColumn)` joined to `FootprintLookupService.ReadMappings`; sorted by manufacturer then catalog. Used by `FootprintMapForm` and the audit. |
| `string Existing(settings, manufacturer, catalog)` | Current BLKNAM of the plain row, or "". |
| `string Validate(AppSettings, FootprintMapRequest)` | Throws unless: both MDBs exist; icon menu folder exists (lock lives there); backup folder set; MANUFACTURER 1..24 and a valid table name; CATALOG 1..60; the footprint is a full-path-able `.dwg` that exists inside the footprint or stock footprint library and fits 255 characters. Returns the full path to store. |
| `FootprintMapResult Execute(AppSettings, FootprintMapRequest or IReadOnlyList<FootprintMapRequest>, Action<string>? progress)` | Validate all; refuse a part listed twice; read-only plan (same path = no-op, different path without `Overwrite` = refuse); if nothing to write return; create `<BackupFolder><stamp>_footprintmap`; `LibraryLock.Acquire(IconMenuFolder)`; copy the MDB; re-plan under the lock; `EnsureTable`, `SetMapping` per item; read each back with `FindPlain`; write `FootprintMapReport.txt`. Any exception restores the MDB from the backup, logs `ERROR:` and the rollback, writes the report and rethrows. Never writes `default_cat.mdb`, a drawing or a menu. |

## src/FootprintMapForm.cs

### `sealed class FootprintMapForm : Form`

`LSEFOOTPRINTMAP`. Toolbar (Reload, status filter defaulting to Unmapped, family filter, text filter), a read-only multi-select grid of `FootprintMapRow`, the footprint path box with **Browse...** (starts in the footprint library), **Map selected parts** and a run log; since 2026-11-12 also a Symbol (WDBLKNAM) column, a Symbol filter, a schematic symbol box with **Browse...** (starts in the symbol library) and **Set symbol** (asks before replacing a non-empty symbol, default No, then before writing). Asks before replacing existing mappings (default No) and before writing; repeats of one MANUFACTURER and CATALOG in the selection are collapsed. Load and map run on the UI thread with a wait cursor (no background thread). Expanded code style.

## src/CatalogBrowserForm.cs

### `sealed class CatalogBrowserForm : Form`

Expanded style. The browser behind `LSECATALOG` and `LSEASSIGNCOMPONENT` (merged from LSE Catalog Tools, 2026-11-12). Constructor `CatalogBrowserForm(AppSettings settings, SelectedBlockInfo? block)`. Reads the catalog; its only writes are block attributes (through `ComponentAssignService.Apply`), `settings.json` and `CompanyStandards.json`.

| Member (all private except the constructor) | Behaviour |
|---|---|
| `BuildInterface`, `ConfigureCatalogGrid`, `ConfigureChangeGrid` | Left tree, a filter row (Family, Manufacturer, Type, Rating combos and a search box over every column), the record grid (Status, Manufacturer, Catalog, "LSE Part Number (<part column>)", Description, Type, Rating, Symbol (WDBLKNAM)), the change grid (Attribute, Current, Proposed editable, Preserve), the button row. Double-clicking a record row calls `Assign`. Ticking Preserve resets that row's Proposed to Current. |
| `LoadCatalog` | `CatalogService.GetTables` and `GetFamilyDescriptions` (a missing `_FAMILY_DESCRIPTION` is tolerated); fills the Family combo with `FamilyNames.Friendly`; with a block whose FAMILY matches a table, selects that table. |
| `BuildNavigation` | Four nodes tagged with `CatalogScope`; selects Full Catalog when `CompanyStandards.json` is empty, else Company Standards. |
| `LoadTable` | `CatalogService.ReadTable(family code)` (20,000 rows at most), refills the filters, applies the filter. |
| `ApplyFilter`, `StatusOf` | Filters in memory by scope, combos and search text. Status is `Legacy` or `Standard` (company standards entry), `Favorite`, else `Catalog`. |
| `RefreshChangePreview` | `ComponentAssignService.Preview(selectedBlock, record, settings)` into a `BindingList<AttributeChange>`; empty without a block. |
| `SelectBlock` | Hides the form, `PromptForBlock`, and when a block was picked opens a replacement browser on it and closes this one; the button reads "Change block" once a block is held. |
| `Assign` | Ends cell edit, takes the selected record, asks for a block when none is held, shows `CompatibilityWarning` as a Yes/No prompt ("Apply anyway?"), calls `Apply` with the grid's changes, moves the record key to the top of `Recent` (25 kept), saves settings, updates the status line. Errors show a message box titled "LSE Librarian". |
| `ToggleFavorite` | Adds or removes the key in `settings.Favorites`, saves settings. |
| `ToggleStandard` | `CompanyStandardsService.Toggle` (category = the family's friendly name) then `Save(settings.CompanyStandardsPath, ...)`; the status line says what happened. |
| `NewRecord` | Opens `CatalogRecordForm(settings, false, new CatalogAddRequest{TableName = current family}, null)` modally, then `LoadTable`. The write goes through `CatalogTransactionService`. |

## src/ComponentAssignService.cs

### `static class ComponentAssignService`

Expanded style. Everything that touches a block's attributes. Pure parts (`PreviewChanges`, `BuildMap`, `InferColorCode`, `IsPreservedByDefault`, `ParseTextValue`, `Warning`) are covered by the smoke test; the AutoCAD parts (`PromptForBlock`, `ReadBlock`, `Apply`) are not.

| Member | Behaviour |
|---|---|
| `string PartAttribute(AppSettings)` | `LsePartAttribute` upper-cased, `USER1` when blank. |
| `SelectedBlockInfo? PromptForBlock()` | Throws "No active drawing." without a document; `GetEntity` limited to `BlockReference` ("Select an AutoCAD Electrical component block: "); null when cancelled; else `ReadBlock`. |
| `SelectedBlockInfo ReadBlock(ObjectId)` | Read-only transaction: block definition name and every attribute reference tag and text. |
| `List<AttributeChange> Preview(SelectedBlockInfo?, CatalogRecord, AppSettings)` | Empty for a null block, else `PreviewChanges(block.Attributes, ...)`. |
| `List<AttributeChange> PreviewChanges(IReadOnlyDictionary<string,string> attributes, CatalogRecord, AppSettings)` | Pure. One row per attribute the block has (sorted by tag): preserved tags keep the current value; others take the `BuildMap` value when there is one. When the block lacks the part attribute and the record has a part number, adds a row with Current `(missing)`. |
| `void Apply(SelectedBlockInfo, CatalogRecord, AppSettings, IEnumerable<AttributeChange>)` | Throws "No active drawing." without a document. `LockDocument`, one transaction: the block opened for write, each attribute reference opened for read and upgraded only when a non-preserved change differs from the current text; changes marked `(missing)` are appended by `EnsureHiddenAttribute` (invisible attribute definition on the block definition plus an invisible attribute reference); commit, then `Editor.Regen()`. No backup and no log: UNDO reverses it. |
| `Dictionary<string,string> BuildMap(CatalogRecord, AppSettings)` | `MFG`, `MANUFACTURER` <- MANUFACTURER; `CAT`, `CATALOG` <- CATALOG; `DESC`, `DESC1` <- DESCRIPTION; `RATING`; `TYPE`; `USER1` <- USER1; `LSE_PART_NUMBER` and `PartAttribute(settings)` <- the part column (`CatalogTransactionService.PartColumn`); `COLOR` <- `InferColorCode` when not blank; then `ParseTextValue(record.TextValue)` pairs override. Case-insensitive keys. |
| `string InferColorCode(CatalogRecord)` | First of GREEN G, RED R, YELLOW Y, BLUE B, WHITE W, CLEAR C, AMBER A, ORANGE O, BLACK K found (case-insensitive substring) in DESCRIPTION, TYPE, RATING, CATALOG, MISCELLANEOUS1, MISCELLANEOUS2; empty when none. |
| `bool IsPreservedByDefault(string tag)` | In `DefaultPreserve` (`TAG`, `TAG1`, `TAG2`, `LOC`, `INST`, `FAMILY`, `XREF`, `WDTAGALT`), or starts with `X`, or contains `WIRE` or `PIN`. |
| `Dictionary<string,string> ParseTextValue(string)` | `KEY=VALUE;KEY=VALUE` into a case-insensitive dictionary; tokens without `=` or with an empty key are ignored. |
| `string CompatibilityWarning(SelectedBlockInfo?, CatalogRecord)` | Empty for a null block, else `Warning(block.Family, block.BlockName, record)`. |
| `string Warning(string family, string blockName, CatalogRecord)` | Pure. A message when the family is not blank and neither contains the other with the record's table name; else a message when the record's WDBLKNAM is not blank and not contained in the block name; else empty. |

## src/CompanyStandardsService.cs

Compact. Two static classes.

### `static class CompanyStandardsService`

| Member | Behaviour |
|---|---|
| `List<StandardEntry> Load(string path)` | Missing file or blank path gives an empty list; a file that cannot be parsed also gives an empty list; never throws. JSON options: indented, case-insensitive names, comments and trailing commas tolerated. |
| `void Save(string path, List<StandardEntry>)` | Blank path throws "The company standards JSON path is not set (LSELIBRARYSETTINGS)."; creates the folder; writes `<path>.tmp` then `File.Move` with overwrite. |
| `StandardEntry? Find(List<StandardEntry>, string key)` | Case-insensitive on `RecordKey`. |
| `bool Toggle(List<StandardEntry>, string key, string category)` | Removes an existing entry (legacy or not) and returns false, or adds a `Company Standard` entry (category `Uncategorized` when blank) and returns true. |

### `static class FamilyNames`

| Member | Behaviour |
|---|---|
| `string Friendly(string table, IReadOnlyDictionary<string,string>? descriptions = null)` | The catalog's `_FAMILY_DESCRIPTION` text when present and not blank, else the built-in name, else the code. |
| `IReadOnlyDictionary<string,string> BuiltIn` | TRMS Terminal Blocks, PB Pushbuttons, CR Control Relays, CB Circuit Breakers, DISC Disconnects, FU Fuses, LT Pilot Lights, MOT Motors, PLCIO PLC I/O, PS Power Supplies, SS Selector Switches, VFD Variable Frequency Drives, MISC_CAT Miscellaneous. |

## src/VaultCatalogService.cs

### `static class VaultCatalogService`

Compact. Optional Vault step for `default_cat.mdb`, used only by `CatalogTransactionService`. The Vault SDK assemblies (`Autodesk.Connectivity.WebServices.dll`, `Autodesk.DataManagement.Client.Framework.Vault.dll`, `Autodesk.DataManagement.Client.Framework.dll`) are loaded with `Assembly.LoadFrom` from `VaultExplorerPath`; there is no compile-time reference. Windows authentication. The Inventor add-in carries the same code. Unverified against a live Vault.

| Member | Behaviour |
|---|---|
| `bool Enabled(AppSettings)` | `CatalogVaultPath` is not blank. |
| `Connect` (private) | Throws "Vault Explorer SDK folder not found: ..." when the folder is missing; loads the assemblies; builds the web service manager from the server identity (`http://<VaultServer>`, `VaultName`); throws "Could not obtain Vault server identity." and "Catalog was not found in Vault: <path>" as the case may be; returns the manager, document service and the latest file version. |
| `void Checkout(AppSettings)` | Throws "Catalog is already checked out." when the file has a checkout user; otherwise `CheckoutFile` with the comment "LSE Librarian" to `CatalogPath`, then clears read-only on the local file. |
| `void Checkin(AppSettings, string comment)` | Builds a `Connection` and `FileIteration`, `FileManager.CheckinFile` from `CatalogPath` with the comment. |
| `void UndoCheckout(AppSettings)` | Finds a document service method with "UndoCheckout" in its name taking one or two parameters; throws "No compatible UndoCheckout method was found." otherwise. |

## src/NamingService.cs

### `static class NamingService`

| Member | Behaviour |
|---|---|
| `List<string> Validate(KindRule, string name)` | Empty, invalid file name characters, over `MaxNameLength`, `.dwg` suffix, regex mismatch (message includes the example and help). Empty list = valid. |
| `bool Matches(KindRule, name)`, `Match Parse(KindRule, name)` | Regex only. |
| `string Sanitize(string text, bool allowSpaces = false)` | Upper-case, whitespace runs to `_` (or a single space), strip everything but `A-Z 0-9 _ - & . ( )`, collapse repeats, trim separators. |
| `string SymbolName(orientation, family, role, variant, series, description)` | `HDV1_002-DESCRIPTION`. |
| `string FootprintName(prefix, series, manufacturer, description)` | `FP_002-EATON_DESCRIPTION` (manufacturer loses `_` and `-`). |
| `string Series(string)` | Returns the value or throws unless exactly three digits. |
| `LibraryKind? KindFor(AppSettings, path)` | Circuits root, then symbol root, then footprint root (`NP_` prefix = nameplate); null outside all. |
| `bool Inside(path, folder)`, `string Relative(path, folder)` | Full-path, case-insensitive containment; `Relative` returns the path unchanged when outside. |
| `bool IsIgnoredFolder(name, LibraryStandard)` | Starts with `_` or listed in `ignoreFolders`. |

## src/IconMenuService.cs

### `static class IconMenuService`

| Member | Behaviour |
|---|---|
| `IconMenu Load(string path)` | Detects UTF-8 BOM, UTF-16 LE/BE BOM, else strict UTF-8 falling back to Latin-1; detects CRLF and trailing newline; parses. Throws `FileNotFoundException`. |
| `IconMenu Parse(string text)` | Same parser on in-memory text (used to verify a rendered menu). |
| `IconMenuEntry ParseEntry(string line)` | `;` lines and lines with fewer than three fields become `Raw`; extra fields are kept in `Action`. |
| `string Render(IconMenu)` | Header, then per page `**Id`, prelude, layout, title (when present or entries exist), entries; joined with the menu's newline; trailing newline reproduced. |
| `byte[] Bytes(IconMenu)` | Preamble (BOM) plus encoded `Render`. |
| `void Save(IconMenu, string? path = null)` | `Bytes` through `.tmp` and move. |
| `int NextPageNumber(IconMenu, int min = 100)` | `max(Number) + 1`, at least `min`. |
| `IconMenuPage AddPage(menu, title, parent, linkLabel, linkImage)` | Appends `M<next>` and a `$S=` entry on the parent. Throws on blank title or foreign parent. |
| `IconMenuEntry AddEntry(menu, page, label, image, action)` | Validates label (not blank, no `|`), page membership and label uniqueness on the page. |
| `bool HasLabel(page, label)` | Case-insensitive, trimmed. |
| `HashSet<string> Reachable(menu, roots)` | Pages reachable through `$S=` links. |
| `Dictionary<string,string> Breadcrumbs(menu, root = "M0")` | Title trail per page; unreachable pages get `(unreachable) <title>`. |
| `List<(IconMenuPage, string Trail)> Tree(menu, roots)` | Reachable pages in file order with their breadcrumb. |
| `string? ResolveImage(menu, image)` | Null for slide references, blanks and unknown forms; absolute path for image files (relative to the `.DAT` folder). |
| `MenuTarget ResolveAction(action)` | See [DATA-STORES.md](DATA-STORES.md) section 3. |

## src/DrawingInspectionService.cs

### `static class DrawingInspectionService` (AutoCAD API)

| Member | Behaviour |
|---|---|
| `DrawingInfo Inspect(string path)` | Side database (`ReadDwgFile`, `OpenForReadAndAllShare`, `CloseInput`); walks model space: attribute definitions (invisible ones excluded from extents), block references (effective name for dynamic blocks), extents from `GeometricExtents` per entity; units from `Insunits`, version from `OriginalFileVersion`, `HasThumbnail` from `ThumbnailBitmap`. |
| `Bitmap? Thumbnail(string path)` | Copy of the embedded preview, or null. |
| `List<string> ListBlockNames()` | Non-layout, non-anonymous, non-xref, non-dependent block names of the active document, sorted. Throws when no document. |
| `string? PromptForBlock()` | `GetEntity` limited to block references; returns the effective block name or null when cancelled. |
| `void ExportBlock(string blockName, string targetPath)` | Locks the document, finds the block, `Database.Wblock(blockId)`, `SaveAs(target, DwgVersion.AC1032)`. Throws when the target exists, the block is missing or the file was not written. |
| `string ExportBlockToTemp(string blockName)` | `ExportBlock` to `%TEMP%\LSE_Librarian\<sanitised block>_<HHmmssfff>.dwg`. |

Not verified on a workstation: that `Wblock(ObjectId)` yields the block contents in model space at the base point (documented AutoCAD behaviour), and `ThumbnailBitmap` availability for drawings saved by AutoCAD Electrical.

## src/IconRenderService.cs

### `static class IconRenderService` (AutoCAD API + GDI+)

| Member | Behaviour |
|---|---|
| `void WriteIcon(IconSource, drawingPath, imagePath, outputPath, pixels)` | `Build` then `SavePng`. |
| `Bitmap Build(IconSource, drawingPath, imagePath, pixels)` | `Render` renders curves; `Thumbnail` fits the DWG preview; `ImageFile` and `ExistingImage` fit a file; `None` throws. |
| `void SavePng(Bitmap, path)` | Through `.tmp` and move. |
| `Bitmap FromImageFile(path, pixels)` | Loads from a stream (no file lock) and fits. |
| `Bitmap ThumbnailIcon(drawingPath, pixels)` | Throws when the drawing has no preview. |
| `Bitmap Fit(Image, pixels)` | Square white canvas, aspect preserved, centred, high-quality bicubic. |
| `Bitmap RenderDrawing(drawingPath, pixels)` | Collects polylines from every `Curve` in model space and nested block references (depth 4, 20000 polylines max) by sampling `GetPointAtParameter` (1 step for lines, 32 for arcs, 64 for circles, ellipses and others, 8 per vertex for polylines), transformed by the accumulated `BlockTransform`; scales to fit with an 8 percent margin, Y flipped, black pen (2 px at 128 px and above, else 1 px), anti-aliased. Falls back to the preview when there are no curves, then throws. |

Not verified: parameter sampling on every curve type AutoCAD Electrical symbols use, and the composition order `parent * BlockTransform` for nested blocks (irrelevant for symbols without nested blocks).

## src/LibraryTransactionService.cs

### `static class LibraryTransactionService`

| Member | Behaviour |
|---|---|
| `LibraryResult Execute(AppSettings, LibraryStandard, LibraryRequest, Action<string>? progress = null)` | The transaction in [ARCHITECTURE.md](ARCHITECTURE.md) section 5. Every log line goes to `progress`. When `AddCatalogRecord` is set, after the footprint lookup step it loads the naming standard, logs `Catalog record: <summary> (naming standard from workbook|JSON snapshot <source>)`, calls `CatalogTransactionService.Run` with the same log, sets `LibraryResult.PartNumber` and logs `Catalog record added with LSE part number <n> (catalog backup in <folder>)`. On failure rolls back and throws `InvalidOperationException` whose message is the whole log. |
| `void Validate(AppSettings, LibraryStandard, LibraryRequest)` | All pre-checks; see section 5 step 1. With `AddCatalogRecord`: refuses user circuits, requires both `PartNumber` and `CatalogRecord`, and runs `CatalogTransactionService.Validate` against `NamingWorkbookService.Load(settings)`. Public so the wizard can run it per step. |
| `bool SameFolder(a, b)` | Full-path, case-insensitive equality. |
| `string? FindByName(root, fileName, LibraryStandard)` | First drawing with that file name under the root (ignored folders skipped), or null. |
| `IEnumerable<string> EnumerateDrawings(root, LibraryStandard)` | Iterative walk of `*.dwg`, skipping ignored folders and unreadable directories. |

### `sealed class LibraryLock : IDisposable`

`PathFor(folder)` = `<folder>\LSE_Library.lock`; `Acquire(folder, log)` opens it exclusively with delete-on-close, 40 tries at 250 ms, writes `user@machine time`, logs `Library lock: PASS`; `Dispose` releases (and deletes) it.

## src/LibraryAuditService.cs

### `static class RuleCheckService`

`List<(Severity, string)> Check(KindRule, DrawingInfo)`: empty model space (error), required attributes with `A|B` alternatives (error), recommended (warning), required and recommended patterns, forbidden nested blocks (error), duplicate tags (warning).

### `static class LibraryAuditService`

| Member | Behaviour |
|---|---|
| `AuditReport Run(AppSettings, LibraryStandard, AuditOptions, Action<string>? progress)` | Audits the schematic menu against the non-panel kinds and the panel menu against the panel kinds (LSE pages = reachable from `menuRootPages`; stock pages with `IncludeStockEntries`); reads the lookup; audits the symbol, footprint and circuit roots (naming, category folder, stock clash, unreferenced, missing lookup row, deep rule check, stray files); audits the images folder for unused images. Summary lines go to `progress` and `Summary`. |
| `string WriteReport(AuditReport, path)` | Tab-separated text grouped by severity. |

Finding texts are listed in [OPERATIONS.md](OPERATIONS.md) section 5.

## src/DiagnosticsService.cs

`string BuildReport()`: runtime, assembly, settings path, backup folder, reference project, every folder and file with `(exists)`/`(MISSING)` (now including "Parts catalog workbook" and "Naming standard JSON snapshot"), JSON load summary, both menus parsed (pages, entries, encoding, line ending, next page number, LSE root pages present per kind), image counts, lookup table count, `Naming standard from workbook: PASS (...)` or `Naming standard from JSON snapshot: PASS (...)` with `NamingWorkbookService.Describe` (or `Naming standard: FAIL`), `Catalog read: PASS (n family tables, part number column X, backups in ...)` when the catalog exists, `Part number issuance lock file present: ...` when `LSE_PartNumber.lock` exists next to the catalog, library lock file presence, recent item count. Added 2026-11-12: `Company standards JSON (catalog browser)` with `(exists)` and the record count from `CompanyStandardsService.Load` (or MISSING), `Catalog browser: block attribute for the part number <attribute>, <n> favourites, <n> recent assignments`, and either `Catalog Vault checkout: ON (<CatalogVaultPath> on <server>/<vault name>); Vault Explorer folder ... PASS/MISSING` or `Catalog Vault checkout: off (Catalog path in Vault is blank)`.

## src/LibrarySettingsForm.cs

`sealed class LibrarySettingsForm : Form`: one row per setting (text box with Browse for folders and files), five check boxes, buttons **Read library paths from project...** (`SettingsService.ApplyProject`, fills the boxes, asks you to Save), **Test**, **Save naming snapshot (JSON)**, **Diagnostics**, **Save** (validates icon size 16 to 1024, three-digit series, non-blank menu file names, a valid part column name; a blank part column becomes `USER1`).

Catalog record rows: "Catalog MDB (default_cat.mdb)", "LSE parts catalog workbook (.xlsx)" (filter `*.xlsx;*.xlsm`), "Naming standard JSON (fallback snapshot)", "Catalog backup folder (blank = next to the catalog)", "Catalog column holding the LSE part number"; Browser and Vault rows (2026-11-12): "Company standards JSON (catalog browser)" (filter `*.json`), "Block attribute that receives the LSE part number" (upper-cased; blank becomes `USER1`; otherwise letters, digits and underscores, else "The block attribute name may only contain letters, digits and underscores."), "Vault Explorer folder (optional, catalog checkout)", "Vault server", "Vault name", "Catalog path in Vault (blank = no Vault checkout)" (when set, server and name are required: "Vault server and Vault name are required when a catalog path in Vault is set."). **Test** adds `Company standards JSON: PASS (n records)` or `not created yet (written on the first Toggle company standard)` and `Catalog Vault checkout: ON; Vault Explorer folder PASS/MISSING ...` or `Catalog Vault checkout: off`. Catalog record check boxes: "Offer a catalog record (default_cat.mdb) in the Librarian wizard" and "Fill USER2 with the purchase description (Description + MPN) by default". **Test** also reports `Parts catalog workbook: PASS (...)` or `Naming standard JSON snapshot (workbook not found): PASS (...)` and `Catalog: PASS (n family tables, part number column ..., backups in ...)`. **Save naming snapshot (JSON)** (`SaveSnapshot`): collects the boxes, requires the workbook file, `LoadWorkbook(..., force: true)`, adds `FamilyHints` from `CatalogService.FamilyHints` when the catalog opens, sets `Source` to `<workbook file name> (snapshot <date time> by <user>)`, `SaveJson` to the naming standard path, shows the path and `Describe`.

## src/LibrarianForm.cs

### `sealed class LibrarianForm : Form`

Constructor `LibrarianForm(string? sourcePath)`; `Result` is set after a successful run. Loads settings and the JSON on `Shown`; a JSON failure shows the message and closes the form.

| Step | Controls and behaviour |
|---|---|
| 1 Item | Kind radio buttons (from the JSON); source radio (file with Browse, or block with list, Pick in drawing and Refresh); description; **Inspect drawing** (`DrawingInspectionService.Inspect` on the file or a temp export, then `RuleCheckService.Check`). Browsing a file or picking a block pre-fills the name parts from the file or block name when it matches the pattern. |
| 2 Name & folder | Rows shown per kind: orientation, family (editable, auto-complete), parent/child, variant; prefix, manufacturer; series; description part; custom-name tick and box; resulting name, help, problems; category (JSON plus existing folders, plus "(library root)" for footprints); subfolder; target path. Name and problems update on every change. |
| 3 Menu & icon | Menu file label; page list (`IconMenuService.Tree` for the kind's root pages, falling back to all pages) pre-selected from the category; new-page tick, title, parent; label (defaults to the description); icon radios; existing-image list (images folder); import image with Browse; **Preview icon**; footprint lookup group (visible for footprints and nameplates); catalog record group "Catalog record (default_cat.mdb, LSE part number from the parts catalog workbook)" (visible for every kind but user circuits when `OfferCatalogRecord` is on) with the check box "Also add a catalog record to default_cat.mdb with a new LSE part number", a summary label (`(none yet)`) and the **Catalog record...** button. |
| 4 Review & add | Review text (`Review()`; the catalog line reads `Catalog record  <summary>  part number <prefix>+ next sequence, issued at run time  (catalog backed up next to it)`, or `= <number>` for a manual number, or `none`), **Add to library** runs `LibraryTransactionService.Execute` with progress echoed into the pane; success shows a message (with `Catalog record added with LSE part number <n>.` when one was written) and turns Cancel into Close. |

`EditCatalogRecord()` (the button, and ticking the box when no record exists yet): seeds a `CatalogAddRequest` from the lookup MANUFACTURER and catalog boxes, the upper-cased plain description and, for schematic symbols, `WdBlkNam` = `ComputeName()`; opens `CatalogRecordForm(settings, embedded: true, seed, partRequest)`; on OK stores `CatalogRequest` and `PartRequest`, ticks the box, writes the summary line (`<summary>   part number <prefix>+ next sequence (issued when the item is added)`) and copies MANUFACTURER and catalog number into the lookup boxes when those are empty; on Cancel with no earlier record the box is unticked. `BuildRequest` sets `AddCatalogRecord`, `PartNumber` and `CatalogRecord`.

Navigation: the step list and Back / Next; moving forward validates each intermediate step (`ValidateStep`): source exists or block chosen; name valid, root exists, target free unless in place, no clash elsewhere; page or new page chosen, label present, icon choice complete, lookup fields when ticked; with the catalog box ticked, a record must exist ("Press 'Catalog record...' to fill in the catalog record, or untick the catalog option.") and `CatalogTransactionService.Validate` must pass (reported as `Catalog record: <message>`). Temp exports are deleted on close.

## src/AuditForm.cs

### `sealed class AuditForm : Form`

Runs the audit on `Shown`. Toolbar: **Run audit**, deep and stock check boxes (defaults from settings), severity filter, search box, **Open in Librarian** (enabled for findings whose path is an existing `.dwg`; sets `OpenInLibrarian` and closes), **Open containing folder** (Explorer), **Export report...** (`LibraryAuditService.WriteReport`). Grid rows are coloured by severity; the lower pane shows the summary lines. Double-click equals **Open in Librarian**.

## src/Plugin.cs

### `sealed class Plugin : IExtensionApplication`

Empty `Initialize` and `Terminate`.

### `sealed class Commands`

| Command | Flags | Body |
|---|---|---|
| `LSELIBRARIAN` | `Modal` | `LibrarianForm(null)` shown with `ShowModalDialog`. This is the catalog-only path: `CatalogTransactionService.Execute` touches only `default_cat.mdb`, its backup folder and `CatalogReport.txt`. The dialog shows a "Catalog record only" banner when not embedded. |
| `LSELIBRARYAUDIT` | `Modal` | `AuditForm`; afterwards opens `LibrarianForm(path)` when a drawing was picked. |
| `LSECATALOG` | `Modal` | `Browse(null)`: the private helper builds `CatalogBrowserForm(SettingsService.Load(), block)` and shows it with `ShowModalDialog`, in its own `try` with the same message box. |
| `LSEASSIGNCOMPONENT` | `Modal` | `ComponentAssignService.PromptForBlock()`, then `Browse(block)` when a block was picked. |
| `LSECATALOGADD` | `Modal` | `CatalogRecordForm(SettingsService.Load(), embedded: false, null, null)` shown with `ShowModalDialog`. |
| `LSEFOOTPRINTMAP` | `Modal` | `FootprintMapForm(SettingsService.Load())` shown with `ShowModalDialog`. |
| `LSELIBRARYSETTINGS` | `Modal` | `LibrarySettingsForm`. Also the home of the retired `LSECATALOGSETTINGS`. |
| `LSELIBRARYDIAG` | `Modal` | `MessageBox` with `DiagnosticsService.BuildReport()`. Also the home of the retired `LSECATALOGDIAG`. |

Eight commands. Every body is one `try` whose catch shows `ex.ToString()` in a message box titled "LSE Librarian" (the diagnostics box is titled "LSE Librarian Diagnostics"). The retired `LSECATALOGADMIN` is replaced by `LSECATALOGADD`.

## config/LseLibraryStandard.json

Field by field in [DATA-STORES.md](DATA-STORES.md) section 2.

## config/LseNamingStandard.json

A serialised `NamingStandard` (section 9 of [DATA-STORES.md](DATA-STORES.md)); the shipped file is the snapshot the smoke test wrote from the real workbook on 2026-11-10.

## LSE.AutoCADElectrical.Librarian.csproj

Unchanged target and reference arrangement (`net8.0-windows`, x64, WinForms, AutoCAD assemblies from the install or the `AutoCAD.NET` 25.1.0 package at compile time). Two `None` items copy `config\LseLibraryStandard.json` and `config\LseNamingStandard.json` to the output folder (`Link` to the bare file name, `PreserveNewest`). Still no runtime package: the workbook reader uses `System.IO.Compression` and `System.Xml` from the framework.

## bundle/LSE.Librarian.bundle/PackageContents.xml

Autoloader manifest: `LoadOnCommandInvocation="True"`, all eight commands (`LSELIBRARIAN`, `LSELIBRARYAUDIT`, `LSECATALOG`, `LSEASSIGNCOMPONENT`, `LSECATALOGADD`, `LSEFOOTPRINTMAP`, `LSELIBRARYSETTINGS`, `LSELIBRARYDIAG`), `SeriesMin="R25.1" SeriesMax="R25.1"` (AutoCAD 2026). The package description (2026-11-12) mentions browsing `default_cat.mdb` and assigning catalog records to blocks. Product and upgrade GUIDs are unique to this plugin.
