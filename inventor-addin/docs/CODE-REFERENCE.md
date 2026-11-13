# Code reference: LSE Footprint Tools

Maintained by DCW. Last reviewed 2026-11-08.

One section per source file, in dependency order (lowest first). Every public member is listed with what it does, what it throws and what it touches on disk. Read [ARCHITECTURE.md](ARCHITECTURE.md) first for how these pieces fit together.

All files share the namespace `LSE.InventorFootprintTools` and build into `LSE.Inventor.FootprintTools.dll`. Members are listed in the order they appear in the file.

## src/Models.cs

Plain data classes. No logic except computed properties.

| Type | Purpose | Notes |
|---|---|---|
| `enum FootprintView` | `Top, Bottom, Front, Back, Left, Right, Current` | `Current` means "whatever the Inventor camera is showing now". |
| `AppSettings` | Everything stored in `settings.json`. | Defaults are in the property initialisers; see [DATA-STORES.md](DATA-STORES.md) section 1 for the full table. |
| `NamingStandard` | Root of `LseNamingStandard.json`. | `Manufacturers` and `FamilyHints` are case-insensitive dictionaries after `NamingStandardService.Load`. |
| `NamingFormat` | Segment lengths of a part number. | `Length` = sum of the four segments (14 with the defaults). |
| `NamingTab` | One LSE workbook tab: name, category codes, type codes, manufacturer codes, description pattern. | `ToString()` returns `Name` so the tab can be placed directly in a combo box. |
| `ManufacturerCode` | `Name` (human) and `AcadeManufacturer` (the `MANUFACTURER` value seen in the live catalog). | `AcadeManufacturer` is empty when the code has never been used in the catalog. |
| `PartNumberRequest` | `Category`, `ManufacturerCode`, `Type`. | `Prefix` = the three joined and upper-cased, for example `45BUSFH`. |
| `CatalogAddRequest` | The row to insert into a family table. | Columns: `TableName, Catalog, Manufacturer, Description, Type, Rating, User1, User2, User3, TextValue, WebLink, WdBlkNam`. The wizard only fills `TableName, Catalog, Manufacturer, Description, WebLink`; the transaction fills `User1`. |
| `FootprintRequest` | How to project: `SourceFile, View, HiddenLines, TangentEdges, OutlineOnly, ScaleCorrection`. | `ScaleCorrection` is 1, 25.4 or 1/25.4. |
| `FootprintCurve` | `Kind` (`LINE`, `CIRCLE`, `ARC`, `POLY`) and `Data`. | `LINE` = x1,y1,x2,y2. `CIRCLE` = cx,cy,r. `ARC` = cx,cy,r,startDeg,endDeg (counter-clockwise). `POLY` = x0,y0,x1,y1,... |
| `FootprintGeometry` | Curve list, `Units`, bounding box (`MinX..MaxY`), `Width`, `Height`. | Bounding box starts at `double.MaxValue`/`MinValue` and is grown by `InventorGeometryService.Extend`. |
| `FootprintResult` | `PartNumber, DxfPath, DwgPath, Report`. | `DwgPath` is empty when DXF-to-DWG conversion is off. |

## src/SettingsService.cs

### `static class SettingsService`

| Member | Behaviour |
|---|---|
| `SettingsPath` | `%APPDATA%\LSE\InventorFootprintTools\settings.json`. |
| `Load()` | Creates the folder, deserialises the file (a corrupt or missing file yields defaults, never an exception), then fills blanks: `CatalogPath` from `DiscoverCatalog`, `FootprintLookupPath` = `footprint_lookup.mdb` beside the catalog, `FootprintLibraryFolder` = `LSE_Footprints` beside the catalog, `NamingStandardPath` = `LseNamingStandard.json` beside the DLL. |
| `Save(AppSettings)` | Writes indented JSON to `settings.json.tmp` and moves it over the real file (atomic replace). |
| `DiscoverCatalog()` (private) | Returns the first existing of `Documents\Acade 2026\AeData\en-US\Catalogs\default_cat.mdb` or `Documents\AutoCAD_Electrical_2026\Catalogs\default_cat.mdb`, else `""`. |

### `static class NamingStandardService`

| Member | Behaviour |
|---|---|
| `Load(string path)` | Deserialises the naming JSON with case-insensitive property names, rebuilds every dictionary as case-insensitive. Throws `FileNotFoundException` ("The LSE naming standard JSON was not found...") when the file is missing, `InvalidOperationException` when the JSON is empty or has no tabs. |

## src/PartNumberService.cs

`static class PartNumberService`. Pure functions; no I/O.

| Member | Behaviour |
|---|---|
| `Validate(NamingFormat, PartNumberRequest)` | Category must be exactly `CategoryChars` digits, manufacturer code exactly `ManufacturerChars` letters, type exactly `TypeChars` letters. Throws `InvalidOperationException` with an example in the message. |
| `Pattern(f, prefix)` | Regex `^<PREFIX>([0-9]{7})$`, prefix escaped and upper-cased. |
| `StandardForm(f)` | Regex for any well-formed part number, `^[0-9]{2}[A-Z]{3}[A-Z]{2}[0-9]{7}$` with defaults. Not used by the add-in itself; available for callers. |
| `Normalize(v)` | Trim and upper-case. |
| `Highest(f, prefix, existing)` | Largest sequence among values matching `Pattern`. Non-matching values (other prefixes, malformed numbers) are ignored. Returns 0 when none match. |
| `Next(f, prefix, existing)` | `Highest + 1`, zero-padded. Throws when the sequence would exceed `10^SequenceChars - 1` ("Sequence exhausted...") or, defensively, when the computed number already exists. |

## src/AccessCatalogService.cs

Three classes. All database access is late-bound ADODB through `dynamic`.

### `static class Ace`

| Member | Behaviour |
|---|---|
| `Open(path)` | Throws `FileNotFoundException` if the file is missing. Creates `ADODB.Connection` from its ProgID and tries providers `Microsoft.ACE.OLEDB.16.0`, `Microsoft.ACE.OLEDB.12.0`, `Microsoft.Jet.OLEDB.4.0` in that order. Throws `InvalidOperationException` listing each provider's error when all fail. |
| `Tables(c)` | `OpenSchema(20)` (adSchemaTables); returns names of type `TABLE` not starting with `MSys`. |
| `Exec(c, sql)` | Executes and releases the returned recordset. |
| `E(v)` | Doubles single quotes for SQL literals. This is the only SQL-injection defence; table names are additionally validated. |
| `ValidateTable(t)` | Table name must match `^[A-Za-z0-9_][A-Za-z0-9_ &/\.\-]*$`; throws otherwise. |
| `Release(o)` | `Marshal.FinalReleaseComObject` when the object is a COM object; swallows errors. |
| `Close(c)` | `Close()` then `Release`. |

### `sealed class AccessCatalogService(string path)`: reads and one insert on `default_cat.mdb`

| Member | Behaviour |
|---|---|
| `GetTables()` | Family tables: every table not starting with `_`, sorted case-insensitively. |
| `GetFamilyDescriptions()` | `FAMILY` to `DESC` from `_FAMILY_DESCRIPTION`. Any error yields the partial or empty dictionary (swallowed). |
| `ReadUser1StartingWith(prefix)` | `SELECT USER1 FROM [t] WHERE USER1 LIKE 'prefix%'` for **every** family table. Tables without a `USER1` column raise inside the loop and are silently skipped. Returns all non-null values. |
| `Exists(table, mfg, cat)` | `SELECT TOP 1 CATALOG ... WHERE MANUFACTURER=... AND CATALOG=...`. Exact match under the Access collation (case-insensitive by default). |
| `AddRecord(CatalogAddRequest)` | Reads the table's column list with `SELECT TOP 1 *`, refuses tables with no `USER1` column, then inserts only the request fields that are both non-empty and present in the table. Columns considered: `CATALOG, MANUFACTURER, DESCRIPTION, TYPE, RATING, USER1, USER2, USER3, TEXTVALUE, WEBLINK, WDBLKNAM`. No `UPDATE`, no `DELETE` anywhere in this class. |

### `sealed class FootprintLookupService(string path)`: `footprint_lookup.mdb`

| Member | Behaviour |
|---|---|
| `TableExists(t)` | Case-insensitive table lookup. |
| `EnsureTable(t)` | `CREATE TABLE [t] ([CATALOG] TEXT(60), [ASSEMBLYCODE] TEXT(24), [BLKNAM] TEXT(255), [DESCRIPTION] TEXT(150), [RECNUM] COUNTER)` when missing. |
| `Find(t, catalog)` | `BLKNAM` of the row with that `CATALOG` and `ASSEMBLYCODE IS NULL`, or `null` when absent. |
| `Upsert(t, catalog, blkName, description)` | Throws when `blkName` exceeds 255 characters; truncates `description` to 150. `UPDATE` when `Find` hits, else `INSERT`. This is the only `UPDATE` statement in the add-in, and it only touches `footprint_lookup.mdb`. |

## src/AutoCadConvertService.cs

`static class AutoCadConvertService`.

| Member | Behaviour |
|---|---|
| `IsInstalled()` | True when the `AutoCAD.Application` ProgID is registered. |
| `DxfToDwg(dxf, dwg)` | Attaches to a running AutoCAD via `GetActiveObject` (P/Invoke, because .NET 8 has no `Marshal.GetActiveObject`), else starts one with `Activator.CreateInstance` and remembers that it did. Waits up to 60 x 1 s for `Documents.Count` to respond. Opens the DXF read-only, `SaveAs(dwg, 64)` (64 = `ac2018_dwg`), falling back to `SaveAs(dwg)` if that constant is rejected, closes without saving, and verifies the DWG exists. Quits AutoCAD only if this call started it. |

## src/InventorGeometryService.cs

### `sealed class InventorGeometryService(Inventor.Application app)`

| Member | Behaviour |
|---|---|
| `ImportModel(path)` | Accepts `.stl .stp .step .ipt .iam` only. Sets `SilentOperation` so translator dialogs are suppressed, then `Documents.Open(path, true)`. Restores `SilentOperation`. Returns the resulting document. |
| `ModelExtentsCm(doc)` | X, Y, Z size of `ComponentDefinition.RangeBox` in centimetres. |
| `PreviewView(view)` | Sets the active camera's `ViewOrientationType` (unless `Current`), `Fit()`, `Apply()`. |
| `Project(model, request, settings)` | Creates a hidden drawing from `settings.DrawingTemplatePath` or Inventor's default drawing template; adds one base view at scale 1.0 at the sheet centre, hidden-line or hidden-line-removed style; sets `DisplayTangentEdges`. Computes `factor = (mm ? 10 : 1/2.54) x ScaleCorrection / view.Scale`. If `OutlineOnly`, emits four lines from `view.Width`/`view.Height`. Otherwise walks every `DrawingCurve.Segments`, throwing when the count passes `settings.MaxCurves` and when no curves result. Always closes the drawing without saving. |
| `AddSegment` (private) | Line to `LINE`; `Circle2d` to `CIRCLE`; `Arc2d` to `ARC` with angles in degrees, start/end swapped when `SweepAngle` is negative, normalised to 0-360; anything else to `POLY` sampled through `Curve2dEvaluator` with 8-256 steps based on length / (8 x tolerance). |
| `Orientation` (private) | Maps `FootprintView` to `ViewOrientationTypeEnum`. |

### `sealed class InventorWindow(Inventor.Application app) : IWin32Window`

Wraps `app.MainFrameHWND` so WinForms dialogs can be owned by Inventor's main window.

## src/DxfFootprintWriter.cs

`static class DxfFootprintWriter`.

| Member | Behaviour |
|---|---|
| `StandardAttributes` (private) | Ordered list of `(Tag, Prompt, Visible)`: `P_TAG1` visible; `MFG, CAT, ASSYCODE, DESC1, DESC2, DESC3, INST, LOC, P_ITEM, XREF, XREFNO` invisible. |
| `Write(path, geometry, settings, defaults)` | Throws when there are no curves. Writes an R12 (`AC1009`) DXF: HEADER (`$INSBASE`, `$EXTMIN`, `$EXTMAX`, `$TEXTSIZE`, `$ATTMODE`=1), TABLES (LTYPE `CONTINUOUS`; LAYER `0` plus the geometry and attribute layers; STYLE `STANDARD`), then ENTITIES: every curve on the geometry layer, then one `ATTDEF` per standard attribute pre-filled from `defaults`, then one extra invisible `ATTDEF` for every key in `defaults` not already written (this is how the LSE part attribute, default `USER1`, gets in, with prompt "LSE part number"). Text height = `TagTextHeight` x 25.4 when metric. `P_TAG1` sits at `MaxY + textHeight`; invisible attributes stack downwards from `MinY - 2 x textHeight` at 1.5 x textHeight spacing, all at `x = MinX`. Written as UTF-8 without BOM with CRLF line ends. |
| `WriteCurve` (private) | `LINE`, `CIRCLE`, `ARC` as native entities; `POLY` as `POLYLINE` + `VERTEX` list + `SEQEND`. |

## src/FootprintTransactionService.cs

### `static class FootprintTransactionService`

`Execute(AppSettings s, NamingStandard n, PartNumberRequest pn, CatalogAddRequest q, FootprintGeometry g, string footprintDescription)` returns `FootprintResult`.

The one mutating code path. The ordered steps, the rollback and the report file are tabulated in [ARCHITECTURE.md](ARCHITECTURE.md) section 5.4. Pre-checks before any file is touched: `PartNumberService.Validate`; `TableName`, `Catalog`, `Manufacturer` non-empty; `Manufacturer` at most 24, `Catalog` at most 60, `Description` at most 150 characters; both MDB files exist; `FootprintLibraryFolder` set. The backup folder is created and the lock taken before the backups are copied; the `try` block that triggers rollback begins at the part-number scan. On failure the method throws `InvalidOperationException` whose message is the **entire log** (including rollback outcomes), with the original exception as `InnerException`.

`footprintDescription` is what goes into the `DESCRIPTION` column of `footprint_lookup.mdb`; the wizard passes the description **without** the appended MPN.

### `sealed class IssuanceLock : IDisposable`

| Member | Behaviour |
|---|---|
| `PathFor(catalogPath)` | `<catalog folder>\LSE_PartNumber.lock`. |
| `Acquire(catalogPath, log)` | Opens the lock file with `FileShare.None` and `FileOptions.DeleteOnClose`, writes `user@machine timestamp` into it, logs "Issuance lock: PASS". Retries 40 x 250 ms on `IOException`, then throws "Another workstation is issuing a part number right now...". |
| `Dispose()` | Closes the stream, which deletes the file. |

### `static class VaultCatalogService`

Reflection and `dynamic` over three Vault SDK assemblies loaded from `VaultExplorerPath`. Shared shape with the sister AutoCAD Electrical plugin.

| Member | Behaviour |
|---|---|
| `Connect` (private) | Loads `Autodesk.Connectivity.WebServices.dll`, `Autodesk.DataManagement.Client.Framework.Vault.dll`, `Autodesk.DataManagement.Client.Framework.dll`; `WebServiceManager.Initialize`; `GetServerIdentities("http://<VaultServer>")`; `WinAuthCredentials(identity, VaultName, false, null)`; finds the catalog by `FindLatestFilesByPaths([CatalogVaultPath])`. Throws when the folder, identity or file is missing. |
| `Checkout(s)` | Refuses if `CkOutUserId > 0` ("Catalog is already checked out."). `DocumentService.CheckoutFile(file.Id, <first CheckoutFileOptions>, machineName, CatalogPath, "LSE Footprint Tools", null)`, then clears the read-only attribute on the local file. |
| `Checkin(s, comment)` | Builds a VDF `Connection` with `WindowsAuthentication`, a `FileIteration` and a `FilePathAbsolute`, then `FileManager.CheckinFile(it, comment, false, null, null, false, null, FileClassification.None, false, path)`. |
| `UndoCheckout(s)` | Finds any `DocumentService` method whose name contains `UndoCheckout` with 1 or 2 parameters and calls it with `MasterId` (and machine name). Throws `MissingMethodException` if none works. |

## src/SettingsForm.cs

`sealed class SettingsForm : Form`. Modal, 980 x 720, three-column table: label, value, Browse/Test/Save button. One row per `AppSettings` property, in this order: Catalog MDB, Footprint lookup MDB, Footprint library folder, Naming standard JSON, Inventor drawing template, Footprint units, Geometry layer, Attribute layer, Tag text height, Curve tolerance, Max curve segments, LSE part attribute, two check boxes (Convert DXF to DWG, Append MPN), Vault Explorer folder, Vault server, Vault name, Catalog Vault path.

| Member | Behaviour |
|---|---|
| `Save()` | Validates: units must be `in` or `mm`; LSE part attribute non-blank (stored upper-case); tag text height and curve tolerance positive numbers (invariant culture); max curves a positive integer. Calls `SettingsService.Save`, closes with `DialogResult.OK`. Validation errors show a message box and keep the form open. |
| `Test()` | Four checks shown in one message box: catalog read (counts family tables), footprint lookup read (calls `TableExists("AB")`), naming standard load (tab and manufacturer counts plus the `source` string), and whether `AutoCAD.Application` is registered. |

## src/FootprintWizardForm.cs

`sealed class FootprintWizardForm : Form`. Modeless, owned by Inventor, 1000 x 940, minimum 820 x 700. Four group boxes and a log pane. Every handler runs inside `Guard`, which sets the wait cursor, catches any exception, logs `ERROR: <message>` and shows it in a message box.

| Member | Behaviour |
|---|---|
| ctor | Loads `settings` through `SettingsService.Load()`; wires `Shown` to `LoadReferenceData`. |
| `LoadReferenceData()` | Loads the naming standard into the tab list; lists family tables with friendly names into the family list; logs the catalog, lookup and library paths; warns when DXF-to-DWG is on but AutoCAD is not registered. Each part is guarded separately so a missing JSON does not hide a catalog problem. |
| `TabChanged()` | Fills category codes, type codes and manufacturer codes for the selected tab; appends every global manufacturer code the tab does not list; sets the description box placeholder to the tab's `descriptionPattern`; resets the part-number label. |
| `ManufacturerChanged()` | Pre-fills MANUFACTURER with `AcadeManufacturer` or the upper-cased name, cut to 24 characters. |
| `SelectFamilyHint()` | Selects the first family table from `familyHints[category]` that exists in the catalog. |
| `Browse()` | File dialog for `*.stl;*.stp;*.step;*.ipt;*.iam`; ticks **Outline rectangle only** when an STL is chosen. |
| `Import()` | `ImportModel`, clears any previous geometry, shows extents (after unit correction) with the hint "If this is 25.4 times off, change the unit correction." |
| `Preview()` | `PreviewView(SelectedView())`. |
| `Project()` | `Project(model, Request(), settings)`; shows curve count, width x height, units and view. |
| `PreviewPartNumber()` | Validates the codes, scans `USER1`, shows the next number with "(preview; the number is assigned when the record is created)". |
| `Create()` | Requires a loaded standard, imported model, projected geometry and a selected family. Normalises MANUFACTURER and description to upper-case. Rejects an empty description, any non-ASCII character in it, and a description that already contains the catalog number. Builds the `CatalogAddRequest` (description gets `, MPN <catalog>` appended when the setting is on). Shows a Yes/No summary. Disables the Create button during `FootprintTransactionService.Execute`. On success shows the part number and footprint path, logs the full report, and clears `geometry` so the same curves cannot be reused by accident. |
| `Code(ComboBox)` (private) | Returns the code from a `CodeItem`, or for free text the part before the first space or hyphen, upper-cased. This lets the engineer type `XYZ` or `XYZ - Some Name` into the manufacturer-code box. |
| `CodeItem`, `FamilyItem` (private records) | Display as `CODE - Name` or just `CODE`. |

Input limits enforced by the controls: MANUFACTURER 24, catalog number 60, description 100, web link 255 characters.

## src/Plugin.cs

`[ComVisible, Guid("55D0C0CC-1B4A-49A9-8008-A4FBFF38A7AC")] sealed class StandardAddInServer : Inventor.ApplicationAddInServer`. The GUID equals the `ClassId`/`ClientId` in the `.addin` manifest and the `ClientId` constant.

| Member | Behaviour |
|---|---|
| `Activate(site, firstTime)` | Creates button definitions `LSE_FootprintTools_Wizard` ("LSE Footprint") and `LSE_FootprintTools_Settings` ("LSE Settings"). On first activation adds an **LSE** panel (`LSE_FootprintTools_Panel_<ribbon>`) to the `id_TabTools` tab of the `ZeroDoc`, `Part`, `Assembly` and `Drawing` ribbons; failures per ribbon are swallowed. |
| `Deactivate()` | Closes the wizard, releases both button definitions, forces GC. |
| `ExecuteCommand(int)` | Empty. |
| `Automation` | Returns `null!`; no automation interface is exposed. |
| `OpenWizard()` (private) | Singleton: activates the existing wizard if open, else creates one owned by `InventorWindow`. |
| `OpenSettings()` (private) | Shows `SettingsForm` modally, owned by Inventor. |

## tools/extract_naming_standard.py

Standalone Python 3 script. Dependencies: `openpyxl`; `pyodbc` only when a third argument is given. Run with `python -I`.

```
python -I tools\extract_naming_standard.py "<workbook.xlsx>" "<output.json>" ["<default_cat.mdb>"]
```

Behaviour:

1. For every worksheet whose title starts with `LSE ` reads the first 40 rows. Inside the block between `Part # Segment` and `Description Field` it collects: category codes (two-digit numbers in column C of the `Category` row), type codes (`XX = text` pairs in column C of the `Type` row), and the `Manufacturer | Code` table (column positions taken from the `Part # Segment` header row, defaulting to columns F and G). Slash-separated names and codes (`A / B` with `AAA / BBB`) are split into pairs. The `Description` row's column B becomes `descriptionPattern`. Tabs with no type codes are dropped.
2. If a catalog is given, opens it read-only through the Access ODBC driver, reads `USER1, MANUFACTURER` from every table, and for every value matching `\d{2}[A-Z]{3}[A-Z]{2}\d{7}` records the most common `MANUFACTURER` per 3-letter code (`acadeManufacturer`) and the three most common tables per 2-digit category (`familyHints`). Codes seen only in the catalog are added to `manufacturers` with a title-cased name.
3. Writes the JSON described in [DATA-STORES.md](DATA-STORES.md) section 2 and prints the counts.

The workbook and the catalog are never written. Not verified here: the exact layout of the workbook's standard block, since the workbook is not in this tree; the generated `config/LseNamingStandard.json` shows what the script produced on 2026-11-08.
