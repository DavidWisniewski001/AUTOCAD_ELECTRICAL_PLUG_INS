# Code reference: LSE Catalog Tools

Maintained by DCW. Last reviewed 2026-11-13.

Every source file in the plugin folder, its types and public members, with what each does, what it reads and writes, what it throws and who calls it. The plugin's namespace is `LSE.AutoCADElectrical.CatalogTools` throughout; files sit flat in the folder (no `src\`).

## Plugin.cs

### `sealed class Plugin : IExtensionApplication`

Empty `Initialize` and `Terminate`. Assembly attributes register it and `Commands`.

### `sealed class Commands`

| Command | Member | Does |
|---|---|---|
| `LSECATALOG` | `Browser()` | Opens `CatalogBrowserForm(null)` with `Application.ShowModalDialog` |
| `LSEASSIGNCOMPONENT` | `Assign()` | `AutoCadComponentService.PromptForBlock()`; if a block was picked opens `CatalogBrowserForm(block)` |
| `LSECATALOGSETTINGS` | `Settings()` | Opens `SettingsForm` |
| `LSECATALOGADMIN` | `Admin()` | Opens `CatalogAdminForm` |
| `LSECATALOGDIAG` | `Diag()` | `MessageBox` with `DiagnosticsService.BuildReport()` |

All `CommandFlags.Modal`. `Browser` and `Assign` catch every exception and show `ex.ToString()` titled `LSE Catalog Tools`; `Settings`, `Admin` and `Diag` have no catch.

## Models.cs

| Type | Members |
|---|---|
| `enum CatalogScope` | `CompanyStandards`, `PersonalFavorites`, `Recent`, `AllCatalog` |
| `sealed class CatalogRecord` | `TableName`; `Values` (column to trimmed string, case-insensitive); computed `Catalog`, `Manufacturer`, `Description`, `Type`, `Rating`, `User1`, `TextValue`, `WdBlkNam`; `Get(name)` returns `""` when absent; `Key` = `TABLE\|MANUFACTURER\|CATALOG` upper-case |
| `sealed class StandardEntry` | `RecordKey`, `Category` (`Uncategorized`), `Status` (`Company Standard`), `Notes` |
| `sealed class AppSettings` | `CatalogPath`, `CompanyStandardsPath`, `VaultExplorerPath`, `VaultServer`, `VaultName`, `CatalogVaultPath`, `LsePartAttribute`, `Favorites`, `Recent` (defaults in [DATA-STORES.md](DATA-STORES.md#2-settingsjson)) |
| `sealed class SelectedBlockInfo` | `Id` (ObjectId), `BlockName`, `Attributes` (tag to value), `Family` (the `FAMILY` attribute or `""`) |
| `sealed class AttributeChange` | `Attribute`, `Current`, `Proposed`, `Preserved` (bound to the preview grid) |
| `sealed class CatalogAddRequest` | `TableName`, `Catalog`, `Manufacturer`, `Description`, `Type`, `Rating`, `User1`, `User2`, `User3`, `TextValue`, `WdBlkNam` |

## SettingsService.cs

### `static class SettingsService`

| Member | Does |
|---|---|
| `SettingsPath` | `%APPDATA%\LSE\AutoCADElectricalCatalogTools\settings.json` |
| `Load()` | Creates the folder, deserialises (defaults on any error), fills a blank `CatalogPath` by discovery and a blank `CompanyStandardsPath` with the default |
| `Save(AppSettings)` | Writes indented JSON to `.tmp`, then `File.Move(tmp, path, true)` |
| `DiscoverCatalog()` (private) | `Documents\Acade 2026\AeData\en-US\Catalogs\default_cat.mdb` if it exists, else `""` |

### `static class StandardsService`

`Load(path)` returns the list or an empty list on any error. `Save(path, list)` writes through a temp file; not called anywhere in the plugin.

### `static class FamilyNames`

`Friendly(table)` maps 13 family codes to display names (unknown codes return the code). `All` exposes the map.

## AccessCatalogService.cs

### `sealed class AccessCatalogService(string path)`

| Member | Does | Throws |
|---|---|---|
| `GetTables()` | `OpenSchema(20)`; returns user tables (type `TABLE`, not `MSys*`, not `_*`) ordered by friendly name | Open errors |
| `ReadTable(table, limit = 20000)` | `SELECT TOP limit *`; one `CatalogRecord` per row, nulls as `""`, values trimmed | `Invalid table name.` |
| `FindByKey(key)` | Splits the key, reads the table, returns the matching record or null. Not called | |
| `AddRecord(CatalogAddRequest)` | Reads the column names with `SELECT TOP 1 *`, then one `INSERT` of the request fields whose column exists. Called only by `CatalogTransactionService` | `Invalid table name.`, provider errors |
| `Exists(table, mfg, cat)` | `ReadTable` then case-insensitive match on Manufacturer and Catalog | |
| `Open(bool writable)` (private) | `FileNotFoundException("Catalog database was not found.")`; `ADODB unavailable.`; tries ACE 16, ACE 12, Jet 4 and throws all three messages joined. The `writable` flag is unused | |
| `Release(object)` (private) | `Marshal.FinalReleaseComObject` when COM | |

## CatalogTransactionService.cs

### `static class CatalogTransactionService`

`Execute(AppSettings s, CatalogAddRequest q)` returns the report text on success. Steps, log lines and rollback are in [ARCHITECTURE.md](ARCHITECTURE.md#5-the-mutating-transaction-catalogtransactionserviceexecute). On failure it throws `InvalidOperationException` whose message is the whole log (inner exception = cause). Side effects: backup folder, `TransactionReport.txt`, MDB insert, Vault check-out and check-in. Called by `CatalogAdminForm.AddRecord`.

## VaultCatalogService.cs

### `static class VaultCatalogService`

| Member | Does | Throws |
|---|---|---|
| `AppName` | `LSE Catalog Administration`, used as the DocumentService check-out comment | |
| `Enabled(s)` | `CatalogVaultPath` not blank | |
| `NormalizeVaultPath(p)` | Trims, `\` to `/`, ensures a `$/` prefix | |
| `Checkout(s, log)` | Connects (or reuses the cached session), finds the file, refuses other users' check-outs, downloads when stale, checks out, verifies, clears read-only, caches the session | `InvalidOperationException`, `FileNotFoundException`, `OperationCanceledException` |
| `Checkin(s, comment, log)` | Requires a check-out, calls the `CheckinFile(..., FilePathAbsolute)` overload with the fewest parameters that works, releases the session | `InvalidOperationException` |
| `UndoCheckout(s, log)` | No-op when not checked out; `FileManager.UndoCheckoutFile`, then `DocumentService.UndoCheckoutFile`; always releases the session | `InvalidOperationException` |
| `EndSession()` | Releases the cached session, if any | |
| `TestReport(s, connect = true)` | Never throws. Runtime, client, add-in lines, settings, working-copy state; with `connect` also connection, Vault version, check-out state and working-copy comparison | |
| `Connect(s, log)` | Add-in session, else login dialog, else Windows authentication (see [ARCHITECTURE.md](ARCHITECTURE.md#51-vault-connection-order-vaultcatalogserviceconnect)) | as above |

Private helpers: `Begin`/`End` (cached session), `WinAuth`, `ServerIdentities`, `Need`/`TryLoad` (load an SDK assembly from the process or the Explorer folder after `VaultProbe.RuntimeMismatch`), `FindFile`, `NewFileIteration`, `MyId`, `UserName`, `NeedsDownload` (size equal and time within 3 s), `Acquire` (AcquireFiles with relationships off; copies the downloaded file over the working copy if Vault put it elsewhere), `RawCheckout`, `Fits`, `Set`, `Str`, `Root` (unwraps TargetInvocation and Aggregate exceptions).

### `sealed class VaultCatalogService.VaultSession`

`Connection` (dynamic), `Source` (text for the log), `Owned`, assemblies `Ws`, `Fw`, `Vdf`, `Wsm` (WebServiceManager), `Describe()`, `Release()` (only when `Owned`: `Library.ConnectionManager.LogOut`, else dispose the WebServiceManager).

## VaultProbe.cs

### `static class VaultProbe`

Shared by every LSE plugin; keep the copies identical apart from the namespace.

| Member | Does |
|---|---|
| `VaultBaseAssembly`, `WebServicesAssembly`, `DefaultWorkingFolder` | `Connectivity.Application.VaultBase`, `Autodesk.Connectivity.WebServices`, `C:\_Lake_Shore_E_Vault` |
| `RuntimeMajor`, `RuntimeLine()` | Host .NET major version and a `Host: <process> on <framework>` line |
| `Loaded(name)` | An assembly already in the AppDomain, or null |
| `AddinConnection()` | The Vault add-in's `ConnectionManager.Instance.Connection`, or null |
| `Describe(conn)`, `Str(obj, member)` | `<user> in vault <v> on <server>`; safe reflection read |
| `AddinLine()` | Logged in / loaded but not logged in / not loaded |
| `DiscoverExplorerFolder(configured)` | Configured folder if it exists, else the newest `Program Files\Autodesk\Vault*\Explorer` containing the WebServices DLL |
| `TargetFrameworkOf(path)`, `TargetMajor(tfm)`, `FileVersion(path)` | Read an assembly's `TargetFrameworkAttribute` from metadata without loading it |
| `RuntimeMismatch(folder)` | Null when loadable, else the explanation (.NET Framework client, or client newer than host) |
| `ClientLine(configured)`, `Report(configured)` | Diagnostic lines |
| `IsReadOnly(path)`, `InVaultWorkingFolder(path)` | File checks |
| `RequireWritable(files, what)` | Throws `InvalidOperationException` naming read-only files and how to check them out |
| `CheckinReminder(touched)` | Lines reminding to check in files that are in the Vault working folder |

## AutoCadComponentService.cs

### `static class AutoCadComponentService` (AutoCAD API)

| Member | Does | Throws |
|---|---|---|
| `PromptForBlock()` | `Editor.GetEntity` limited to `BlockReference`; returns `ReadBlock` or null on cancel | `No active drawing.` |
| `ReadBlock(id)` | Block name and every attribute tag/value, read-only transaction | |
| `Preview(block, record, settings)` | One `AttributeChange` per block attribute (sorted by tag), plus a `(missing)` row for the LSE part attribute when absent and `USER1` is set. Empty when `block` is null | |
| `Apply(blockInfo, record, settings, changes)` | Locks the document, writes `Proposed` to each non-preserved attribute, adds the `(missing)` attribute through `EnsureHiddenAttribute`, commits, `Regen` | AutoCAD errors (not caught by the caller) |
| `CompatibilityWarning(block, record)` | Text when `FAMILY` and table do not contain each other, or `WDBLKNAM` is not in the block name; `""` otherwise | |

Private: `BuildMap` (section 6 of [ARCHITECTURE.md](ARCHITECTURE.md#6-attribute-map-on-assign)), `InferColorCode`, `IsPreservedByDefault`, `ParseTextValue`, `EnsureHiddenAttribute` (adds an invisible `AttributeDefinition` to the block definition when missing, then an invisible `AttributeReference`).

## CatalogBrowserForm.cs

### `sealed class CatalogBrowserForm : Form`

Constructor `CatalogBrowserForm(SelectedBlockInfo? block)`; 1750 x 950, minimum 1200 x 700. Controls and behaviour are listed in [OPERATIONS.md](OPERATIONS.md#3-browse-the-catalog-lsecatalog). Private members: `BuildInterface`, `ConfigureCatalogGrid`, `ConfigureChangeGrid` (Preserve tick resets Proposed; check-box edits commit immediately), `LoadCatalog` (tables, pre-selects the block's family), `BuildNavigation`, `LoadTable`, `FillFilters`, `ApplyFilter`, `StatusOf`, `RefreshChangePreview`, `SelectBlock` (hides, picks, opens a new browser with `ShowDialog`, closes itself), `Assign`, `ToggleFavorite`, splitter helpers, and `record FamilyItem(Code, Name)`. Writes `settings.json` on Assign and Toggle favorite.

## CatalogAdminForm.cs

### `sealed class CatalogAdminForm : Form`

850 x 650. Table drop-down filled from `GetTables()` on Shown (error in a message box). Eleven inputs, **Manage standards**, **Add to catalog**. `AddRecord` trims, validates the four required fields, calls `CatalogTransactionService.Execute`, shows the report or `Catalog transaction failed`.

## SettingsForm.cs

### `sealed class SettingsForm : Form`

1100 x 540. Seven text boxes (three with Browse), **Test Vault** (runs `VaultCatalogService.TestReport` on the unsaved values with a wait cursor, shows `TextReportForm`), **Save**.

## TextReportForm.cs

### `sealed class TextReportForm(string title, string text) : Form`

900 x 600 read-only Consolas text box with a **Copy** button.

## DiagnosticsService.cs

### `static class DiagnosticsService`

`BuildReport()`: label, runtime, assembly location, catalog path and existence, standards path, Vault path, table count (`Catalog read: PASS|FAIL`), then `VaultCatalogService.TestReport(s, connect:false)`.

## LSE.AutoCADElectrical.CatalogTools.csproj

`net8.0-windows`, WinForms, nullable, x64, `EnableDynamicLoading`, no framework folder in the output path. References `accoremgd`, `acdbmgd`, `acmgd` from `$(AcadDir)` (default `C:\Program Files\Autodesk\AutoCAD 2026`) when `acdbmgd.dll` is there, else the `AutoCAD.NET` 25.1.0 package with runtime assets excluded. `tests\**` is excluded from compilation.

## bundle/LSE.CatalogTools.bundle/PackageContents.xml

`AppVersion="1.0.0"`, `SeriesMin`/`SeriesMax` `R25.1`, module `./Contents/LSE.AutoCADElectrical.CatalogTools.dll`, load on command invocation, the five commands in group `LSECATALOG`.

## tests/VaultFakeTest

Console program that compiles the real `VaultCatalogService.cs` and `VaultProbe.cs` against four stub assemblies under `FakeSdk\` (`Ws`, `Fw`, `Vdf`, `VaultBase`) carrying the real Vault type names. It declares its own minimal `AppSettings`. 27 checks in scenarios A (Windows auth, stale read-only copy, check-out with download, check-in, logout), B (current copy, no download, undo), C (checked out by another user), D (wrong path), E (TestReport), G (stale copy and Framework failure refused), H (current copy, DocumentService fallback), F (add-in session used, no login or logout). Exit code = number of failures.
