# Code reference: LSE Project Tools

Maintained by DCW. Last reviewed 2026-11-13.

One section per source file, lowest dependency first. Every public member is listed with what it does, what it throws and what it touches on disk. Read [ARCHITECTURE.md](ARCHITECTURE.md) first for how the pieces fit.

All files share the namespace `LSE.AutoCADElectrical.ProjectTools` and build into `LSE.AutoCADElectrical.ProjectTools.dll`. `ImplicitUsings` is on, so `System`, `System.IO`, `System.Linq` and `System.Collections.Generic` are available everywhere without a `using`.

## src/Models.cs

Plain data classes. Logic is limited to computed properties and `ToString()` overrides for combo boxes.

| Type | Purpose | Notes |
|---|---|---|
| `AppSettings` | Everything stored in `settings.json`. | Defaults are in the property initialisers; full table in [DATA-STORES.md](DATA-STORES.md) section 1. New since V1: `DefaultProductLine` (blank), `WriteAepxFile` (true), `CopyTemplateSidecars` (true). `ActivateProjectLisp` default is a `boundp`-guarded call of `c:wd_makeproj_current` with a `{WDP}` placeholder. |
| `ProjectTemplates` | Root of `LseProjectTemplates.json`: `Vault`, `Job`, `DescriptionLines`, `LabelFile`, `TitleBlock`, `Wdm`, `UnitSpecs`, `Sections`, `ProductLines`, `ProjectTypes`. | `Source` is set by `TemplateService.Load` to the file path that was read. |
| `VaultDefaults` | The `vault` section: `StandardsRoot`, `JobsRoot`, `TemplateProject`, `DrawingTemplate` (default `DRAWING-#.dwg`), `CoverTemplate` (default `STOCKCODE-E001.dwg`), `SymbolLibrary`. | Read by `SettingsService.Load` to fill blank settings and by diagnostics. |
| `JobRules` | The `job` section: `NumberPattern` (default `^[A-Za-z0-9][A-Za-z0-9._\- ]*$`), `NumberExample`, `NumberHint` (default `Job number`), `FolderNamePattern` (`{JOB}`), `ProjectNamePattern` (`{JOB}`), `FileNamePattern` (`{JOB}-{SHEET:3}`). | The code defaults reproduce the V1 behaviour when the section is absent. |
| `DescriptionLineDef` | One `*[n]` line: `Index`, `Label`, `Attribute`, `Role`, `Default`, `Placeholder`, `Required`. | `Required` is a plain settable property (the old "job or title" rule is gone); `Load` forces it on the `title` line. `HasRole(string)` compares `Role` case-insensitively. |
| `LabelFileDef` | `ExtraLabels`: line number (as string) to label, for `LINEn` entries without a description line. | Used only by `WriteLabels`. |
| `TitleBlockMap` | How title blocks are recognised and filled. | Code defaults are the LSE values: `BlockNames` empty, `SheetAttribute` and `SheetMaxAttribute` blank, `FileNameAttribute` `DWG_NUMBER`, `DescriptionAttributes` `[DWG_TYPE]`, `DateAttribute`, `SectionAttribute` and `SubsectionAttribute` blank, `Fixed` empty (case-insensitive). |
| `WdmMap` | WD_M block name and attribute tags. | Code defaults: `WD_M`, `SHEET`, `IEC_PROJ`, `IEC_INST`, `IEC_LOC`, `DrawingNumberAttribute` blank. |
| `UnitSpecsDef` | `Pattern` (default `{TYPE}, {AMPS}A, {VOLTS}, {KAIC}KA@{KV}V, {NEMA}`), `UnitTypes`, `Amperages`, `Voltages`, `Kaic`, `Nema` choice lists. | Feeds the unit-specs builder. |
| `ProductLineDef` | One vault product line: `Name`, `Folder`, `UnitType`, `ProjectType`, `Levels`, `Children`. | `ToString()` returns `Name` for the combo box. |
| `LocationNode` | One node of a product line's folder tree: `Name`, `Folder`, `Children`. | `FolderName` is `Folder` when set, else `Name` (both trimmed). `ToString()` returns `Name`. |
| `ProjectTypeDef` | A drawing set: `Name`, `Description`, `FileNamePattern` (default blank; `Load` fills it), `Drawings`. | `ToString()` returns `Name`. |
| `DrawingDef` | One preset drawing: `Sheet` (the E-number), three descriptions, `Section`, `Subsection`, `Folder`, `Template`. | `Template` is a bare file name resolved in the template folder, or a full path. |
| `DrawingRow` | One wizard grid row: `Sheet`, `FileName` plus the `DrawingDef` fields. | Bound to the `DataGridView` through a `BindingList`. |
| `ProjectRequest` | Everything the transaction needs. | `JobNumber`, `Title`, `Customer`, `Quote`, `ProductLine`, `LocationChoices`, `ProjectFolder`, `ProjectName`, `BaseProjectPath`, `DrawingTemplatePath`, the three IEC codes, `ProjectType`, `DescriptionLines` (`SortedDictionary<int,string>`), `Drawings`. Switches: `FillTitleBlocks`, `SetWdmValues`, `WriteWdtFile`, `WriteWdlFile`, `WriteAepxFile`, `CopyTemplateSidecars`, `ActivateProject`, `OpenFirstDrawing`, `AllowExistingFolder`. `WdpPath` = `ProjectFolder\ProjectName.wdp`. |
| `WdpDrawing` | One drawing entry in a `.wdp`: `Path`, `Section`, `Subsection`, `Folder`, `Descriptions`, `ReferenceOnly`. | `Path` is as written in the file (relative) after a parse, absolute before a render. |
| `WdpDocument` | `Descriptions`, `Properties`, `Options` (sorted int maps), `Drawings`, `Encoding` (default `new UTF8Encoding(true)`, UTF-8 with BOM), `Unknown`. | `Unknown` collects `*`, `?` or `+` lines that are not `x[n]value`; they are not re-rendered. |
| `DrawingJob` | Input to `DrawingFactoryService.Create`: `TemplatePath`, `OutputPath`, `TitleBlockValues`, `WdmValues` (both case-insensitive), `Title`, `Subject`, `FillTitleBlocks`, `SetWdmValues`, `WdmLibraryFile`. | |
| `DrawingOutcome` | Counts returned per drawing: `TitleBlocksFound`, `TitleBlockAttributesSet`, `WdmFound`, `WdmInserted`, `WdmAttributesSet`. | Logged by the transaction. |
| `ProjectResult` | `Request`, `WdpPath`, `DrawingPaths`, `Report`. | Returned by `Execute`; held by the wizard in `Result`; read by `Commands.NewProject`. |

## src/SettingsService.cs

### `static class SettingsService`

| Member | Behaviour |
|---|---|
| `SettingsPath` | `%APPDATA%\LSE\AutoCADElectricalProjectTools\settings.json`. |
| `LogFolder` | `%APPDATA%\LSE\AutoCADElectricalProjectTools\Logs`. Failure reports go here. |
| `VaultFolderName`, `DefaultVaultRoot` | Constants `_Lake_Shore_E_Vault` and `C:\_Lake_Shore_E_Vault`. |
| `Load()` | Creates the folder, deserialises the file (missing or corrupt yields defaults, never an exception), then fills blanks in this order: `TemplatesConfigPath` to `LseProjectTemplates.json` beside the DLL; reads `vault` from it with `TemplateService.TryLoadVault` and finds the vault with `DiscoverVaultRoot`; `BaseProjectPath` from `vault.templateProject` (through `OnVault`) when the file exists; `ProjectsRootFolder` from `vault.jobsRoot` when the folder exists, else `DiscoverProjectsRoot`; `DrawingTemplatePath` from `vault.drawingTemplate` resolved in `TemplateFolder(BaseProjectPath, vault template project)` when the file exists, else `DiscoverTemplate`; `SymbolLibraryFolder` from `vault.symbolLibrary` when the folder exists, else `DiscoverLibrary`. |
| `Save(AppSettings)` | Indented JSON to `settings.json.tmp`, then `File.Move` over the real file. |
| `TemplateFolder(string baseProject, string fallback)` | Directory of `baseProject` when set, else of `fallback`, else blank. The folder in which bare template file names are resolved. |
| `DiscoverVaultRoot()` | `C:\_Lake_Shore_E_Vault` when it exists; else `_Lake_Shore_E_Vault` at the root of the first fixed or network drive that has it; else blank. Never throws. |
| `OnVault(string path, string vaultRoot)` | Blank path gives blank. The path unchanged when `vaultRoot` is blank or the path already exists as a file or folder. Otherwise the part after `_Lake_Shore_E_Vault` is re-rooted on `vaultRoot` (a `C:` vault path remapped to the drive that was found); a path without that folder name is returned unchanged. |
| `AeData()` | `Documents\Acade 2026\AeData` if it exists, else `Documents\Acade 2025\AeData` if it exists, else the 2026 path regardless. |
| `DiscoverProjectsRoot()` (private) | `AeData\Proj` when it exists, else `Documents\AutoCAD Electrical Projects`. |
| `DiscoverTemplate()` (private) | Searches `%LOCALAPPDATA%\Autodesk\AutoCAD Electrical 20*` (newest folder name first) recursively for `ACAD_ELECTRICAL.dwt`; first hit or blank. |
| `DiscoverLibrary()` (private) | Under `AeData\en-US\Libs`: `jic125`, `JIC125`, `iec2`, `IEC2`, else the first sub-folder, else blank. |

### `static class TemplateService`

| Member | Behaviour |
|---|---|
| `Load(string path)` | Reads the JSON (case-insensitive names, comments and trailing commas allowed). Throws `FileNotFoundException` when missing, `InvalidOperationException` when empty, when there are no description lines, no project types, a duplicate line index, an index below 1, not exactly one line with role `title`, more than one line with role `customer`, an invalid `job.numberPattern`, or a product line whose `projectType` names no project type (case-insensitive). Sets `Required` on the `title` line, fills a blank `ProjectTypeDef.FileNamePattern` from `job.fileNamePattern` (else `{JOB}-{SHEET:3}`), sorts lines by index, makes `TitleBlock.Fixed` case-insensitive, sets `Source`. |
| `TryLoadVault(string path)` | The `vault` section of the JSON, or a default `VaultDefaults` when the file is missing, unreadable or has no such section. Never throws. |
| `ResolveTemplate(string template, string templateFolder)` | Blank gives blank; a rooted path, or any value when the folder is blank, is returned as is; otherwise `templateFolder\template`. |
| `Expand(string pattern, string job, int sheet, string projectName, DateTime now, string user, string dateFormat, IDictionary<string,string>? extra = null)` | Replaces `{JOB}`, `{PROJECT}`, `{USER}`, `{YEAR}`, `{DATE}` (formatted with `dateFormat`, falling back to `yyyy-MM-dd` when the format is invalid), then every `{KEY}` of `extra`, then `{SHEET}` and `{SHEET:n}` (zero-padded to n digits). Case-insensitive. |
| `SafeName(string value)` | Trims and replaces every invalid file name character with `-`. Used for `{CUSTOMER}`, `{QUOTE}`, `{PRODUCTLINE}` and the generated folder and project names. |
| `JobNumberOk(JobRules j, string job)` | `Regex.IsMatch` of the trimmed job against `NumberPattern` (the V1 free-form rule when blank). An invalid pattern degrades to "non-blank". |
| `ComposeUnitSpecs(UnitSpecsDef u, string type, string amps, string volts, string kaic, string nema)` | Builds the line-7 text from `Pattern`: `{KV}` is the largest integer in `volts` (`###` when none); a trailing `A` is stripped from `amps`; blank inputs become the template placeholders `####`, `####`, `###/###VAC`, `##`, `N#?`. |

Private: `Blank(value, placeholder)`.

Callers: the wizard (defaults, names, job check, unit specs), the transaction (`Fixed` values, job check, template resolution), `SettingsService.Load` (vault defaults, template resolution), diagnostics.

## src/WdpFileService.cs

### `static class WdpFileService`

| Member | Behaviour |
|---|---|
| `Parse(string path)` | Reads the whole file with a `StreamReader` that starts from UTF-8 without BOM and detects a BOM (UTF-8, UTF-16 LE or BE), records the resulting `Encoding`, then classifies each non-blank line: `*[n]`, `?[n]`, `+[n]` into the maps; `=====SUB=`, `====REF`, `===`, `==`, `=` onto a pending drawing; other `*`/`?`/`+` lines into `Unknown`; anything else is a drawing path that closes the pending drawing. Throws `FileNotFoundException`. |
| `Render(string wdpPath, WdpDocument d, int minDescriptionLines = 8)` | Returns the file text in the order described in [ARCHITECTURE.md](ARCHITECTURE.md) section 7: `*[1]` to `*[max(minDescriptionLines, highest index)]` (blanks included), then `+[n]` ascending, then `?[n]` ascending, then the drawings. Drawing paths are made relative to the `.wdp` folder with `Relative`. CRLF line ends. |
| `Write(string wdpPath, WdpDocument d, int minDescriptionLines = 8)` | `Render` then `WriteText` in `d.Encoding`. |
| `Relative(string folder, string file)` | `Path.GetRelativePath`; returns the original absolute path when the result starts with `..` or is rooted, or when `file` is not rooted. |
| `WriteText(string path, string text, Encoding enc)` | Writes `path.tmp` then moves over `path`. |
| `LabelsPath(string wdpPath)` | `<folder>\<name>_wdtitle.wdl` (the template's casing). |
| `MappingPath(string wdpPath)` | `<folder>\<name>.wdt`. |
| `ConfigurationPath(string wdpPath)` | `<folder>\<name>.aepx`. |
| `ExistingCaseInsensitive(string path)` | The path when it exists; else the first file in its folder whose name matches case-insensitively; else `null`. |
| `CopySidecar(string wdpPath, string baseProjectPath, Func<string,string> pathOf)` | Copies `pathOf(baseProjectPath)` (found case-insensitively) to `pathOf(wdpPath)` through a `.tmp` sibling and returns the destination; `null` without writing when the base project is blank or has no such file. Used with `LabelsPath` and `MappingPath`. |
| `WriteLabels(string wdpPath, IEnumerable<DescriptionLineDef> lines, LabelFileDef extra)` | Writes `LINEn=label` (no spaces) for every line with a non-blank label, plus `extra.ExtraLabels` entries whose key is an integer not already used and whose value is non-blank, ascending, UTF-8 without BOM, CRLF. Returns the path. |
| `WriteMapping(string wdpPath, ProjectTemplates t, Encoding enc)` | Returns `null` without writing when no title block names are configured. Otherwise writes a `;;` comment line (date and JSON file name), `BLOCK = name1,name2`, then in the template's order: `fileNameAttribute = FILENAME`, `DD1`..`DD3` for the description attributes, `attribute = LINEn` for every description line with an attribute, then `SHEET`, `SHEETMAX`, `DWGSEC`, `DWGSUB` for the configured attributes (blank ones omitted). Returns the path. |
| `WriteConfiguration(string wdpPath, IEnumerable<string> drawingPaths)` | Writes `<name>.aepx`: XML declaration, `<ProjectConfiguration Version="1.0">`, `<Project ProjectPath="<folder>" LocalGUID="<new GUID>" EmxFilePath="" EmxGUID=""/>`, `<Drawings NextID="n+1">` with one `<Drawing FilePath="<relative path, upper case>" FileID="i"/>` per drawing. XML-escaped, UTF-8 without BOM. Returns the path. |
| `EncodingName(Encoding e)` | `UTF-16 LE`, `UTF-16 BE`, `UTF-8 with BOM`, `UTF-8` or the web name. For logs. |

Private: `TryIndexed`, `Map` (writes `attr = CODE` only for a non-blank attribute), `Esc` (`SecurityElement.Escape`).

Side effects: file writes only through `WriteText` and `CopySidecar`.

## src/DrawingFactoryService.cs

### `static class DrawingFactoryService`

| Member | Behaviour |
|---|---|
| `Create(DrawingJob job, TitleBlockMap titleBlock, WdmMap wdm)` | Throws `FileNotFoundException` when the template is missing and `InvalidOperationException` when the output exists. Creates the output folder. Opens the template (`.dwg` or `.dwt`) as a side `Database`, swaps `HostApplicationServices.WorkingDatabase` for the call, walks every layout block, applies WD_M values to the WD_M reference and title block values to matching references (`SetAttributes`), inserts WD_M from `job.WdmLibraryFile` when absent (`InsertWdm`), commits, sets summary info, `SaveAs(job.OutputPath, DwgVersion.Current)`. Returns a `DrawingOutcome`. Restores the working database in `finally`. |
| `Inspect(string templatePath, TitleBlockMap titleBlock, WdmMap wdm)` | Read-only. Returns `(HasWdm, TitleBlocks, Layouts)`: whether a WD_M reference exists in any layout, a description of each matched title block (`<name> in <layout> (<tags>)`), and the layout names. Used by diagnostics. |

Private helpers: `EffectiveName` (dynamic block aware), `Tags` (attribute tags of a reference), `IsTitleBlock` (see [ARCHITECTURE.md](ARCHITECTURE.md) section 6), `Wildcard` (`*`/`?` to regex, case-insensitive), `SetAttributes` (writes only existing, changed tags; returns count), `InsertWdm` (imports or reuses the definition, places a reference at the origin of model space, builds attribute references from non-constant definitions; returns count of WD_M values applied), `SetSummary` (`DatabaseSummaryInfoBuilder`: title, subject, author, comments). Unchanged since V1.

## src/AcadeBridgeService.cs

### `static class AcadeBridgeService`

| Member | Behaviour |
|---|---|
| `LispFor(string wdpPath, string template)` | Replaces `{WDP}` (case-insensitive) with the path using forward slashes and `"` escaped as `\"`. |
| `ActivateProject(string wdpPath)` | Throws `FileNotFoundException` when the `.wdp` is missing, `InvalidOperationException` when the configured LISP is blank or there is no active document. Sends `LispFor(...)` plus a newline with `Document.SendStringToExecute(text, true, false, true)`. Returns before the LISP runs. |
| `OpenDrawing(string path)` | Throws `FileNotFoundException`; otherwise `DocumentManager.Open(path, false)`. |

Callers: `Commands.NewProject`, `Commands.Activate`. Unchanged since V1.

## src/ProjectTransactionService.cs

### `static class ProjectTransactionService`

| Member | Behaviour |
|---|---|
| `Execute(AppSettings s, ProjectTemplates t, ProjectRequest q, Action<string>? progress)` | The transaction in [ARCHITECTURE.md](ARCHITECTURE.md) section 5. Starts with `Validate(t, q)`. For an existing folder it refuses (before writing) an existing `.wdp`, any existing `_wdtitle.wdl`, `.wdt` or `.aepx` of the same name (`A project sidecar file already exists: <path>`), and a read-only `LSE_ProjectReport.txt` (`VaultProbe.RequireWritable`). After `Status: SUCCESS` it appends `VaultProbe.CheckinReminder` lines for the created files and the job folder. Every log line is also passed to `progress`. Returns `ProjectResult`. Throws `InvalidOperationException` whose message is the complete log after rolling back. Saves `s` (recent list) on success, so it writes back the settings object the wizard loaded when it opened. |
| `Validate(ProjectTemplates t, ProjectRequest q)` | Job number must satisfy `TemplateService.JobNumberOk(t.Job, ...)` (message: the hint, the example and the entered text); `Title` non-blank; every description line with `Required` non-blank (`Description line n (LABEL) is required.`); project name a valid file name; project folder rooted; drawing template exists; template project exists when set; at least one drawing; each file name valid, without folder separators, unique after `EnsureDwg` (case-insensitive), with a non-blank sheet; a row's template, resolved with `RowTemplate`, exists when set. Throws `InvalidOperationException` or `FileNotFoundException`. |
| `Validate(ProjectRequest q)` | `Validate(new ProjectTemplates(), q)`: the V1 free-form job rule, no required lines beyond the title. |
| `TemplateFolder(ProjectRequest q)` | `SettingsService.TemplateFolder(q.BaseProjectPath, q.DrawingTemplatePath)`. |
| `RowTemplate(DrawingRow r, ProjectRequest q, string templateFolder)` | `TemplateService.ResolveTemplate(r.Template, templateFolder)`, else `q.DrawingTemplatePath`. |
| `Tokens(ProjectRequest q)` | `{CUSTOMER}`, `{QUOTE}`, `{PRODUCTLINE}` as `SafeName` values, for `Expand`. |
| `EnsureDwg(string name)` | Trims and appends `.dwg` unless already present (case-insensitive). |
| `SheetNumber(string sheet, int fallback)` | First run of digits in the sheet text, else `fallback`. Used for `{SHEET}` in names and `Fixed` values and for the wizard's E-number arithmetic. |
| `FindWdmLibrary(AppSettings s)` | `<SymbolLibraryFolder>\wd_m.dwg` when it exists, else the first `wd_m.dwg` found under `AeData\en-US\Libs`, else blank. |

Private: `Put` (adds a value to a map only when the attribute name is non-blank).

Log header: started by, project (`<name> (<job>) - <title>`), customer and product line with the level choices, folder, drawing template, template folder, templates config. The number of `*[n]` lines written is the larger of the JSON's highest index and the template project's highest index (8 for the shipped standard). Each drawing line names the template file it was created from.

Title block values built per drawing: every description line with an attribute, sheet, total count, file name without extension, date (`s.DateFormat`), section, subsection, the three drawing descriptions, then `Fixed` entries expanded with `TemplateService.Expand` and `Tokens(q)`; blank attribute names are skipped, so for the LSE block the result is the eight `LINEn` attributes plus `DWG_NUMBER` and `DWG_TYPE`. WD_M values: sheet (the row's Sheet text, `001`), IEC project, installation, location, and the drawing number attribute (file name without extension) when configured.

## src/DiagnosticsService.cs

### `static class DiagnosticsService`

| Member | Behaviour |
|---|---|
| `BuildReport()` | Returns the multi-line report shown by `LSEPROJECTDIAG`, headed `LSE Project Tools V2 (Lake Shore Electric)`: runtime, the three `VaultProbe.Report("")` lines (host process and .NET, Vault client found by discovery only since this plugin has no Vault Explorer setting, Vault add-in login), assembly path, settings path, vault working folder (or `NOT FOUND (expected C:\_Lake_Shore_E_Vault; ...)`), jobs root, LSE template project, drawing template, templates JSON, symbol library, each with `(exists)` or `(MISSING)`, the WD_M library file, the activation LISP, the copy-sidecars and write-aepx switches; templates JSON result (line count, product lines, drawing sets), the job number rule with its naming patterns, title block names; `Template folder file <name>: PASS/MISSING` for `vault.drawingTemplate` and `vault.coverTemplate`; `Template .wdt` and `Template _wdtitle.wdl` PASS (copied) or MISSING (generated); `Drawing set <set> sheet <n> template MISSING: <path>` per unresolved per-row template; template drawing inspection (WD_M present, layouts, matched title blocks); template project parse counts and encoding, and its `+[2]` schematic icon menu with `(exists)` or `(MISSING on this machine)`; `Jobs root is inside the Vault working folder: new job folders must be added to Vault after creation` when it is; recent project count. Every MISSING path, file or per-row template that lies inside the vault working folder gets the hint `- not downloaded yet? In Vault Explorer select the folder and Get Latest Version`. Each probe is wrapped so one failure is reported as `FAIL <message>` without stopping the others. |

## src/VaultProbe.cs

### `static class VaultProbe`

Shared helper, an identical copy in every LSE plugin folder (keep them in step). Nothing from the Vault SDK is referenced at compile time. Constants: `VaultBaseAssembly` = `Connectivity.Application.VaultBase`, `WebServicesAssembly` = `Autodesk.Connectivity.WebServices`, `DefaultWorkingFolder` = `C:\_Lake_Shore_E_Vault`.

| Member | Behaviour | Used here by |
|---|---|---|
| `RuntimeMajor`, `RuntimeLine()` | Host process name and .NET version (`Host: acad on .NET 8.0.x (.NET 8)`). | `Report` |
| `Loaded(string name)` | An assembly with that simple name already loaded in the process, or null. | `AddinLine` |
| `AddinConnection()` | By reflection: `ConnectionManager.Instance.Connection` of the Vault add-in (`Connectivity.Application.VaultBase`), or null. Never throws. | `AddinLine` |
| `Describe(object)`, `Str(object, string)` | `<user> in vault <vault> on <server>` from the connection's properties. | `AddinLine` |
| `AddinLine()` | `Vault add-in: logged in as ...`, `... loaded but not logged in (log in on the Vault tab)`, or `... not loaded in this session (...)`. | `Report` |
| `DiscoverExplorerFolder(string configured)` | The configured folder when it exists, else the newest `Program Files\Autodesk\Vault*\Explorer` (or x86) holding `Autodesk.Connectivity.WebServices.dll`, by the year in the folder name. | `ClientLine` |
| `TargetFrameworkOf(string path)`, `TargetMajor(string?)`, `FileVersion(string)` | Reads the `TargetFrameworkAttribute` from a DLL's metadata without loading it; major .NET version (-1 = .NET Framework, 0 = unknown); file version. | `RuntimeMismatch`, `ClientLine` |
| `RuntimeMismatch(string explorerFolder)` | Null when the Vault client could load in this process; else the reason (.NET Framework build, or built for a newer .NET than the host, with the advice to update AutoCAD and Vault 2026 together). | `ClientLine` |
| `ClientLine(string configured)` | `Vault client: <folder> (file version ..., built for ...) - compatible with this host` or `- NOT LOADABLE HERE: ...`, or `Vault client: not found ...`. | `Report` |
| `Report(string configured)` | `RuntimeLine`, `ClientLine`, `AddinLine` joined by new lines. Called with `""`. | `DiagnosticsService.BuildReport` |
| `IsReadOnly(string path)` | True when the file exists and has the read-only attribute. | `RequireWritable` |
| `InVaultWorkingFolder(string path)` | True when the full path contains `\_Lake_Shore_E_Vault\` or starts with `C:\_Lake_Shore_E_Vault\`. | diagnostics hints, `CheckinReminder` |
| `RequireWritable(IEnumerable<string> files, string what = "Nothing was changed.")` | Throws `InvalidOperationException` listing the read-only files, with check-out advice when they are in the vault working folder, otherwise "clear the read-only attribute". | `ProjectTransactionService.Execute` (existing folder, `LSE_ProjectReport.txt` only) |
| `CheckinReminder(IEnumerable<string> touched)` | Yields `Vault: these n item(s) are in the Vault working folder and are not checked in yet. ...` and one indented line per item that exists and lies in the working folder; nothing otherwise. | `ProjectTransactionService.Execute` |

## src/ProjectSettingsForm.cs

### `sealed class ProjectSettingsForm : Form`

Compact form, titled `LSE Project Tools Settings (Lake Shore Electric)`. Rows: Jobs root (Custom Sales Order) (folder browse), LSE template project (.wdp) (browse), Drawing template (DRAWING-#.dwg) (`.dwg`/`.dwt` browse), Project templates JSON (browse), Symbol library folder (wd_m.dwg) (folder browse), Default product line, Default drawing set, Date format, Drawn by (default), Activate project LISP ({WDP}); then eight check boxes: Fill title block attributes (ECAD_LSE_CUSTOM_TB_2.0), Set WD_M sheet / IEC values, Write `<job>.wdt`, Write `<job>_wdtitle.wdl`, Copy the template's own .wdt / .wdl instead of generating them, Write `<job>.aepx`, Activate project after creation, Open first drawing after creation. **Save** copies every box into `AppSettings`, formats the current date with the entered date format (an invalid format throws and is shown in a message box), calls `SettingsService.Save`, closes with `DialogResult.OK`. **Diagnostics** shows `DiagnosticsService.BuildReport()`.

## src/NewProjectForm.cs

### `sealed class NewProjectForm : Form`

Expanded form, 1440 x 920, minimum 1150 x 720, titled `LSE New AutoCAD Electrical Project (Lake Shore Electric)`. Public surface: the constructor and `Result` (`ProjectResult?`, set after a successful create).

Structure: a step list on the left (`1 Job`, `2 Vault location`, `3 Drawings`, `4 Review & create`; owner-drawn, current step highlighted, completed steps green), a header `Step n of 4: <title>`, a host panel with four step panels of which one is visible, and a footer with status, **Back**, **Next**, **Create project** and **Cancel** (renamed **Finish** after success).

| Step | Panel contents | Validation on leaving forward |
|---|---|---|
| 1 Job and project descriptions | LSE job number box (upper-cased, bold) with the hint and example beside it; Product line combo; Drawing set combo with its description; one text box per description line labelled `n. Label *   (ATTRIBUTE)` with the template placeholder as `PlaceholderText` and the default expanded from the JSON; the unit-specs builder (editable combos Type, Amps, Voltage, kAIC, NEMA and **Compose line 7**); the IEC `%P`, `%I`, `%L` boxes. | Job number matches `job.numberPattern` (message: hint and example); every `Required` line non-blank (`Enter description line n (LABEL).`). |
| 2 Vault location and templates | Jobs root (browse); Product line folders: up to three cascading editable combos labelled from `levels` (or a note when the line has no levels); Job folder name; Project name; the resulting `Project file` path; LSE template project (browse); Drawing template (browse); nine option check boxes. | Rooted jobs root; level choices without invalid file name characters; valid folder and project names; drawing template exists; template project exists when set. |
| 3 Drawing list | Toolbar: File name pattern, **Load drawing set** (asks before replacing a non-empty list), **Add drawing**, **Remove**, **Move up**, **Move down**, **Regenerate file names**, **Renumber 001..n**; a hint line; the editable grid: Sheet (E-no.), File name, Drawing type (description 1), Description 2, Description 3, Section, Subsection, PM folder, Template (optional). | At least one row, then `ProjectTransactionService.Validate(templates, request)` on the built request. |
| 4 Review and create | Read-only monospace pane: `JOB` (job number, project name, customer, product line and choices, drawing set, project file, template project, drawing template, IEC codes), `DESCRIPTION LINES` (`*[n] label attribute value`), `OPTIONS`, `DRAWINGS (n)` (sheet, file name, drawing type, resolved template file, PM folder) and `Ready...` or `NOT READY: <reason>`; becomes the live log during creation. | None. |

Behaviour worth knowing:

- Data loads in `Shown`, not the constructor: templates, the hint text, the settings paths and switches, the drawing sets (pre-selected by `DefaultProjectType`), the product lines (pre-selected by `DefaultProductLine`; a placeholder entry when the JSON has none). A templates JSON error closes the form after a message box.
- Selecting a product line sets the builder's Type to its `unitType`, selects the drawing set named by its `projectType`, rebuilds the level combos (`max(levels, tree depth)` boxes, at most 3) and fills the first level with its children. Choosing a node fills the next level with that node's children and clears the deeper ones; a level with exactly one node is selected automatically. Typed text that matches no node is used as the folder name. `LocationChoices()` stops at the first empty level.
- The project folder is `root \ product line folder \ choices... \ job folder name`; the `Project file` label shows it with `<project name>.wdp`, or a prompt while any part is blank.
- The job folder name is `SafeName(Expand(job.folderNamePattern))` with trailing `_`, `-` and spaces trimmed, and the project name `SafeName(Expand(job.projectNamePattern))`, each refreshed on every job number or customer line change until the engineer types into that box (`folderNameEdited`, `projectNameEdited`, set only when the box has focus).
- Changing the drawing set shows its description, puts its `fileNamePattern` into the pattern box and reloads the drawing list when the list is empty or still auto-named; otherwise the list is left alone.
- **Load drawing set** numbers rows from each `DrawingDef.Sheet` (digits, `000` format); a row without a sheet takes the previous number plus 1. **Add drawing** inserts after the current row with the next free number (highest + 1), a `DRAWING TYPE` description, the first configured section and a file name from the pattern, and puts the cursor in the drawing type cell. **Move up/down** only reorders.
- File names are regenerated from the pattern and the Sheet cells (`autoNamed`) whenever the job number, a customer line, the project name or a Sheet cell changes, and when entering step 3 or later from step 1 or 2. Editing any file name cell sets `autoNamed` false; **Regenerate file names** sets it true again. **Renumber 001..n** rewrites the Sheet cells sequentially and regenerates.
- **Compose line 7** writes `TemplateService.ComposeUnitSpecs(...)` into the box of the line with role `specs`, or shows a message when there is none. The caption and button are built from the JSON: `Unit specs builder (line 7, UNIT SPECS):` and `Compose line 7` for the shipped file; without a `specs` line the caption says so and the button is disabled.
- The nine option check boxes start from the saved settings (except **Allow an existing, non-empty project folder**, which always starts unticked) and are copied into the `ProjectRequest`; nothing in the wizard writes them back to `settings.json`.
- Changing the product line can select another drawing set; while file names are still automatic (`autoNamed`) that reloads the drawing list without asking, so grid edits made before (drawing types, templates) are lost. Only **Load drawing set** asks first.
- The description labels read `n. LABEL *   (ATTRIBUTE)`; a line without an attribute shows empty brackets `()`.
- Clicking a step in the list validates every intervening step in order and stops at the first failure.
- After a successful create, **Back** and **Create project** are disabled and navigating away shows a message to close and start again. The success message box reminds the engineer to check the job folder into the vault.
- On failure the log pane keeps the lines received so far, appends a rollback note, and shows the full message; **Create project** is re-enabled.

## src/Plugin.cs

### `sealed class Plugin : IExtensionApplication`

Empty `Initialize` and `Terminate`.

### `sealed class Commands`

| Command | Flags | Behaviour |
|---|---|---|
| `LSENEWPROJECT` | `Modal, Session` | Shows `NewProjectForm` modally (`Application.ShowModalDialog`). If `Result` is set: `AcadeBridgeService.ActivateProject` when `ActivateProject` was ticked, then `OpenDrawing` of the first drawing when `OpenFirstDrawing` was ticked. Exceptions go to a message box titled `LSE Project Tools` with the full `ex.ToString()` (stack trace included), as in every command. |
| `LSEPROJECTACTIVATE` | `Modal, Session` | `OpenFileDialog` filtered to `*.wdp`, then `ActivateProject`. |
| `LSEPROJECTSETTINGS` | `Modal` | Shows `ProjectSettingsForm`. |
| `LSEPROJECTDIAG` | `Modal` | Message box with `DiagnosticsService.BuildReport()`. |

Unchanged since V1.

## config/LseProjectTemplates.json

Not code, but read by `TemplateService.Load` (whole file) and `TemplateService.TryLoadVault` (the `vault` section, from `SettingsService.Load`). Field-by-field schema in [DATA-STORES.md](DATA-STORES.md) section 2.

## bundle/LSE.ProjectTools.bundle/PackageContents.xml

Autoloader manifest: `AutodeskProduct="AutoCAD"`, `Platform="AutoCAD*"`, `SeriesMin="R25.1"`, `SeriesMax="R25.1"`, module `./Contents/LSE.AutoCADElectrical.ProjectTools.dll`, `LoadOnCommandInvocation="True"`, the four commands listed. The `ProductCode` and `UpgradeCode` GUIDs are fixed in the file; change `AppVersion` on each release. Unchanged since V1.
