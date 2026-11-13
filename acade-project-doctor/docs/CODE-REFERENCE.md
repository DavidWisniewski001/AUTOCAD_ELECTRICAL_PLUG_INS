# Code reference: LSE_Doctor

Maintained by DCW. Last reviewed 2026-11-10.

Every source file, every type and every public member of the doctor, with inputs, outputs, exceptions, side effects and callers. Private helpers are summarised. All code is in namespace `LSE.AutoCADElectrical.ProjectDoctor`. See [ARCHITECTURE.md](ARCHITECTURE.md) for how the pieces fit.

## Contents

- [Commands](#commands)
- [src/Plugin.cs](#srcplugincs)
- [src/Models.cs](#srcmodelscs)
- [src/SettingsService.cs](#srcsettingsservicecs)
- [src/StandardService.cs](#srcstandardservicecs)
- [src/TextFileService.cs](#srctextfileservicecs)
- [src/DiagnosisService.cs](#srcdiagnosisservicecs)
- [src/RepairService.cs](#srcrepairservicecs)
- [src/DrawingInspectionService.cs](#srcdrawinginspectionservicecs)
- [src/WireLayerService.cs](#srcwirelayerservicecs)
- [src/AcadeBridgeService.cs](#srcacadebridgeservicecs)
- [src/DiagnosticsService.cs](#srcdiagnosticsservicecs)
- [Forms](#forms)
- [Project, rules and manifest files](#project-rules-and-manifest-files)

## Commands

Defined in `Commands` (`src/Plugin.cs`). All use `CommandFlags.Modal`. Each wraps its body in try/catch and shows `ex.ToString()` in a message box titled "LSE_Doctor".

| Command | Method | What it does |
|---|---|---|
| `LSEDOCTOR` | `Commands.Doctor` | Loads settings. Gets the active project with `AcadeBridgeService.TryGetActiveProject`; if empty, guesses with `GuessProjectNearActiveDrawing`. Shows `DoctorForm(start, active)`. After the dialog, if `ReloadProjectPath` is set, calls `AcadeBridgeService.ReloadProject` with freshly loaded settings. |
| `LSEDOCTORSCAN` | `Commands.Scan` | Same, with `ScanForm(active)`. Does not use the near-drawing guess. |
| `LSEDOCTORSETTINGS` | `Commands.Settings` | Shows `DoctorSettingsForm`. |
| `LSEDOCTORDIAG` | `Commands.Diag` | Shows `TextReportForm("LSE_Doctor Diagnostics", DiagnosticsService.BuildReport())`. |

## src/Plugin.cs

Assembly attributes: `ExtensionApplication(typeof(Plugin))` and `CommandClass(typeof(Commands))`.

| Type | Members |
|---|---|
| `Plugin : IExtensionApplication` | `Initialize()` and `Terminate()`, both empty. |
| `Commands` | `Doctor()`, `Scan()`, `Settings()`, `Diag()` as in the table above. |

## src/Models.cs

Plain data classes. No behaviour except computed properties.

### AppSettings

Serialized to `settings.json` with `System.Text.Json` default options (property names exactly as below). Field-by-field description with defaults is in [DATA-STORES.md](DATA-STORES.md#settingsjson).

| Property | Type | Default |
|---|---|---|
| `TemplateProjectPath` | string | Vault path to `E-CAD CUSTOM PROJECT TEMPLATE 2.wdp` |
| `RulesConfigPath` | string | `""` (replaced on load by `<DLL folder>\LseDoctorRules.json`) |
| `ProjectsRootFolder` | string | `C:\_Lake_Shore_E_Vault\Engineering\Custom Sales Order` |
| `CheckDrawings` | bool | true |
| `InsertMissingWdm` | bool | true |
| `ScanChecksDrawings` | bool | false |
| `ReactivateAfterRepair` | bool | true |
| `ShowReportAfterRepair` | bool | true |
| `BackupFolderName` | string | `_LSE_DOCTOR_BACKUP` |
| `BackupsToKeep` | int | 10 |
| `ActiveProjectLisp` | string | sets `USERS1` to `(ace_getactiveproject)` or `""` |
| `ActivateProjectLisp` | string | calls `c:wd_makeproj_current "{WDP}"` when bound, else prints a message |
| `RecentProjects` | List<string> | empty |

### DoctorRules

Loaded from the rules JSON by `RulesService.Load`. Defaults below are the class defaults. See [DATA-STORES.md](DATA-STORES.md#lsedoctorrulesjson).

| Member | Type | Default | Notes |
|---|---|---|---|
| `Source` | string | `""` | Set by the loader: the file path, or `(built-in defaults; <path> not found)`. |
| `DescriptionLineCount` | int | 0 | 0 means as many as the template has (highest `*[n]` index). |
| `FillBlankDescriptionsWithPlaceholder` | bool | true | |
| `EnforceOptions`, `EnforceProperties` | bool | true | |
| `PathListMode` | string | `replace` | `replace` or `merge`. |
| `PathListOptions` | List<int> | 1, 3 | `+[n]` that are `;` lists. |
| `PathOptions` | List<int> | 2, 4 | `+[n]` that are single paths. |
| `ProtectedOptions`, `ProtectedProperties` | List<int> | empty | |
| `RequireLabelFile`, `EnforceLabels` | bool | true | |
| `RequireMappingFile`, `EnforceMappings` | bool | true | |
| `RequireProjectConfigurationFile` | bool | false | |
| `WdmBlockName` | string | `WD_M` | |
| `TitleBlockNames` | List<string> | empty | Empty means take them from the template `.wdt` BLOCK line. |
| `Labels` | Dictionary<string,string> | empty, case-insensitive | Keys like `+1`, `?5`. |
| `CheckWireLayers` | bool | true | Compare wire layers and `WD_M` wire-layer attributes with the template drawing (only when drawings are checked). |
| `EnforceWireLayers` | bool | true | `SetAttribute` and `RepairLayer` findings are recommended. |
| `PurgeUnusedWireLayers` | bool | true | `PurgeLayer` findings are recommended. |
| `WireLayerAttributes` | List<string> | `WIRELAYS`, `WIRENO_LAY`, `WIRECOPY_LAY`, `WIREFIXED_LAY`, `WIREREF_LAY`, `FAN_INOUT_LAYS` | `WD_M` attribute tags that name wire layers. Compared only when the template `WD_M` carries the tag. |
| `WireLayerListAttributes` | List<string> | `WIRELAYS`, `FAN_INOUT_LAYS` | Which of those hold a comma-delimited list of names or wildcard patterns; the rest hold one layer name. |
| `WireLayerXdataApps` | List<string> | `VIA_WD*` | Registered-application name patterns; xdata under a matching name on a layer marks it as an AutoCAD Electrical wire type. The name is a guess (not documented by Autodesk). |
| `NeverPurgeLayers` | List<string> | `0`, `DEFPOINTS` | Layer names never offered for purge. |
| `Merge` (computed) | bool | | `PathListMode` equals `merge`, ignoring case. |

### TextDocument

One text file held in memory: `Path`, `Exists`, `Encoding` (default UTF-8 with BOM), `NewLine` (default `\r\n`), `Lines` (without line endings), `Changed` (set by every edit helper, cleared by `Write`).

### WdpDrawingEntry

One drawing in a `.wdp`: `Path` (text as written), `FirstLine` (index of the first marker line for this drawing, or the path line), `PathLine`, `Section`, `Subsection`, `Folder`, `Descriptions` (the `===` lines), `ReferenceOnly`.

### WdtMapping

`Attribute`, `Code`, `Line` (index in the file, -1 if none), `IsBlock` (attribute is `BLOCK`, ignoring case).

### ProjectStandard

The loaded template. `WdpPath`, `Folder` (computed), `Bundled` (true when the fallback copy is used), `Wdp` (the text document), `Descriptions`, `Options`, `Properties` (index to value, sorted), `Wdt` and `Wdl` (null if the file is absent), `Mappings`, `TitleBlockNames`, `Labels`, `TemplateDrawingPath` (empty if none), `DrawingPaths` (full paths of every drawing the template lists).

### Enums

| Enum | Values |
|---|---|
| `Severity` | `Error`, `Warning`, `Info` |
| `RepairAction` | `None`, `Add`, `Replace`, `CreateFile`, `InsertWdm`, `RemoveDrawing`, `SetAttribute`, `CreateLayer`, `RepairLayer`, `PurgeLayer` |

### Finding

One difference. Created by `DiagnosisService`; changed only through `Selected` by the grids.

| Member | Meaning |
|---|---|
| `Category`, `Item` | Group name and row label shown in the grid. |
| `Current`, `Standard`, `Note` | Value in the project, value in the standard, tooltip text. |
| `Severity`, `Action` | See enums. |
| `Recommended` | Ticked by default. |
| `Selected` | Currently ticked. |
| `Target` | `wdp`, `wdt`, `wdl`, `aepx`, or the full path of a drawing. |
| `Prefix`, `Index`, `Value` | For line edits: the group character (`*`, `+`, `?`), the `n`, and the text to write. For `wdt` edits `Item` is the attribute and `Value` the code. For `wdl` edits `Index` is the line number. For `RemoveDrawing`, `Item` is the path text and `Value` the full path. For `InsertWdm`, `Value` is the template drawing path. For `SetAttribute`, `Value` is the template's attribute text. For `CreateLayer` and `RepairLayer`, `Value` is the template drawing path (`RepairService` uses `ProjectStandard.TemplateDrawingPath` instead; the two are the same path). |
| `Name` | For drawing findings: the `WD_M` attribute tag (`SetAttribute`) or the layer name (`CreateLayer`, `RepairLayer`, `PurgeLayer`). `Item` for these rows is `<drawing path as listed in the .wdp> : <tag or layer name>`. Empty for text-file findings. |
| `Key` (computed) | `Target|Category|Item`. Used by verification to match text-file findings before and after a repair. |
| `ActionText` (computed) | "Add line", "Replace value", "Create file", "Insert WD_M", "Remove from project", "Set WD_M value", "Create layer from template", "Repair layer", "Purge layer", "(report only)". Shown in the grid's Repair column. |
| `IsDrawingAction` (computed) | True for `InsertWdm`, `SetAttribute`, `CreateLayer`, `RepairLayer` and `PurgeLayer`: the finding changes a DWG, `Target` is its full path, and `RepairService` groups it into a `DrawingRepairRequest`. |

### Diagnosis

Result of `Examine`. Members: `WdpPath`, `Folder`, `ProjectName` (computed), `Standard`, `Rules`, `Findings`, `Drawings` (parsed entries), `Log`, `DrawingsChecked`, `Examined`, `TemplateDrawing` (the inspection of the template drawing when the wire-layer check ran, else null; `RepairService` passes it to `WireLayerService.Resolved`). Computed: `Errors`, `Warnings`, `Infos`, `Healthy` (no errors and no warnings), `Recommended` (count), `Selected` (count of selected findings whose action is not `None`), `Status` ("Healthy" or "N error(s), M warning(s)").

### RepairResult

`Before` (the diagnosis), `After` (the verification diagnosis), `BackupFolder`, `Log`, `Applied` (count), `Unresolved` (findings), `ReportPath`, `Report` (computed: the log joined with newlines).

### DrawingInspection

What `DrawingInspectionService.Inspect` found in one DWG (or in the template drawing).

| Member | Meaning |
|---|---|
| `Path`, `OpenInEditor`, `Error` | File path; true when the DWG is open in the editor (nothing else is filled); exception message if reading failed. |
| `HasWdm` | A top-level `WD_M` reference exists in some layout. |
| `WdmValues` | Tag to text of every attribute on the first `WD_M` reference found. Case-insensitive keys. |
| `TitleBlocks`, `Layouts` | Strings like `NAME in LayoutName`; layout block names. |
| `Layers` | One `LayerInfo` per layer table record. |
| `Layer(name)` | The `LayerInfo` with that name, ignoring case, or null. |
| `Wdm(tag)` | The attribute text, or `""` when the tag is absent. |

### LayerInfo

One layer table record as read by `Inspect`.

| Member | Meaning |
|---|---|
| `Name` | Layer name. |
| `Color` | `LayerTableRecord.Color.ToString()`; used as the comparison key. |
| `Linetype` | Name of the linetype record, or `?` if it could not be opened. |
| `LineWeight` | `(int)LayerTableRecord.LineWeight`: hundredths of a millimetre; -3 default, -2 ByBlock, -1 ByLayer. |
| `IsPlottable`, `IsOff`, `IsFrozen`, `IsLocked` | Flags as read. Only `IsPlottable` is written back by a repair; none of the four is compared. |
| `Description` | Layer description or `""`. |
| `XdataApps` | Registered-application names found in the record's xdata. |
| `EntityCount` | Entities on the layer across every block table record (layouts and block definitions). |
| `Summary` (computed) | `color <c>, linetype <l>, lineweight <default|ByBlock|ByLayer|0.00 mm>[, no plot]`; shown in the grid. |

### DrawingRepairRequest

Everything `DrawingInspectionService.Repair` needs for one DWG. Built by `RepairService.Execute` from the ticked drawing findings of that drawing.

| Member | Meaning |
|---|---|
| `DrawingPath`, `TemplateDrawingPath`, `WdmBlockName` | The DWG, the template DWG, the block name from the rules (default `WD_M`). |
| `InsertWdm` | Insert the block from the template. |
| `Attributes` | Tag to text to set on the drawing's `WD_M` (case-insensitive keys). |
| `CreateLayers`, `RepairLayers`, `PurgeLayers` | Layer names for each step. |
| `NeedsTemplate` (computed) | True when `InsertWdm`, `CreateLayers` or `RepairLayers` is set; the template DWG is opened only then. |

## src/SettingsService.cs

### SettingsService (static)

| Member | Behaviour |
|---|---|
| `SettingsPath` | `%APPDATA%\LSE\LSE_Doctor\settings.json`. |
| `LogFolder` | `%APPDATA%\LSE\LSE_Doctor\Logs`. |
| `AssemblyFolder` | Folder of the running DLL. |
| `BundledStandardFolder` | `<AssemblyFolder>\standard`. |
| `Load()` | Creates the settings folder. Reads `settings.json`; on any exception or a missing file it uses defaults without reporting. Then fills a blank `RulesConfigPath` with `<AssemblyFolder>\LseDoctorRules.json`, a blank `BackupFolderName` with `_LSE_DOCTOR_BACKUP`, and raises `BackupsToKeep` below 1 to 1. Callers: every form, `Commands`, `DiagnosticsService`. |
| `Save(s)` | Writes indented JSON to `settings.json.tmp` then moves it over `settings.json`. Throws on I/O failure. |
| `Remember(s, wdp)` | Moves the path to the front of `RecentProjects` (case-insensitive de-duplication), caps the list at 25, saves, ignores save errors. Called after Examine in `DoctorForm` and after a successful repair. |
| `BundledStandardWdp()` | First `*.wdp` (sorted by name) in `BundledStandardFolder`, or null. Never throws. |

### RulesService (static)

`Load(path)`: if the file does not exist, returns a default `DoctorRules` whose `Source` says so. Otherwise parses with case-insensitive names, `//` comments and trailing commas allowed. Throws `InvalidOperationException` with "The doctor rules JSON is empty: <path>" for a null document and "pathListMode must be "replace" or "merge" in <path>" for a bad mode; `JsonException` for malformed JSON. A blank `WdmBlockName` becomes `WD_M`; a negative `DescriptionLineCount` becomes 0; `Labels` is rebuilt case-insensitive. No other field is range-checked.

## src/StandardService.cs

### StandardService (static)

| Member | Behaviour |
|---|---|
| `Load(settings, rules)` | Uses `TemplateProjectPath` if it exists, otherwise the bundled copy (`Bundled` true). Throws `FileNotFoundException` ("The LSE template project (.wdp) was not found and no bundled copy exists...") if neither exists, and `InvalidOperationException` if the file has no `*[n]`, `+[n]` or `?[n]` line. Reads the three line groups. Finds the `.wdt` as `<template>.wdt` or `default.wdt` in the template folder, the `.wdl` as `<template>_wdtitle.wdl`, both case-insensitively. Title block names come from `rules.TitleBlockNames` or else the template's `BLOCK` line. Lists the template's drawings (full paths); `TemplateDrawingPath` is the first existing drawing whose file name starts with `DRAWING`, else the first existing drawing, else empty. |
| `Describe(std)` | One-line summary for the standard label: path, `[bundled copy ...]` marker, counts of descriptions, options, properties, mappings, labels, and a note when there is no template drawing. |
| `Label(rules, std, prefix, index)` | Grid label such as `*[2] QUOTE#` or `+[1] Schematic symbol library paths`. `*` names come from the template `.wdl`; `+` and `?` names from `rules.Labels` (key `+1`, `?5`). Falls back to `*[2]`. |

## src/TextFileService.cs

### TextFileService (static)

File and line helpers. None throw except the file I/O ones.

| Member | Behaviour |
|---|---|
| `Read(path)` | Returns a `TextDocument` (`Exists` false if the file is missing). Detects UTF-8 BOM, UTF-16 LE BOM, UTF-16 BE BOM, else strict UTF-8 without BOM, else Latin-1. Sets `NewLine` to `\r\n` if present. Splits on `\n`, trims `\r`, drops one trailing empty line. |
| `Write(doc)` | Joins lines with `NewLine` plus a final `NewLine` (empty document writes an empty file). Creates the folder. Writes preamble then body to `<path>.tmp` then moves over the target. Sets `Exists`, clears `Changed`. |
| `EncodingName(e)` | "UTF-16 LE", "UTF-16 BE", "UTF-8 with BOM", "UTF-8", "ANSI (Latin-1)" or the web name. |
| `TryIndexed(line, prefix, out index, out value)` | True for `x[n]value` lines with the given prefix and integer `n`. Requires at least 4 characters. |
| `Find`, `Get`, `Group` | Index of the first line `prefix[index]`, its value (null if absent), and a sorted index-to-value map of a group (first occurrence wins). |
| `Set(doc, prefix, index, value)` | Replaces the line if present (sets `Changed` only if text differs). Otherwise inserts in index order inside the group; if the group is empty, after the last line of the earlier groups in order `*`, `+`, `?`. |
| `Drawings(doc)` | Parses the drawing list: every non-blank line not starting with `*`, `?` or `+` is either a marker (`=====SUB=` folder, `====REF` reference only, `===` description, `==` subsection, `=` section) attached to the next path line, or a path line. Returns `WdpDrawingEntry` list. |
| `FullPath(wdpPath, entry)` | Absolute path as is, relative path combined with the `.wdp` folder; returns the raw text if normalisation throws. |
| `RemoveDrawing(doc, entry)` | Deletes lines from `FirstLine` (or `PathLine`) to `PathLine`. Includes the marker lines before the path. |
| `Mappings(doc)` / `Mapping(doc, attr)` | Parses `.wdt` lines `attribute = code`. Lines whose first non-blank character is `;` are comments. Attribute names may start with a grave accent. Lookup ignores case, first match wins. |
| `SetMapping(doc, attr, code)` | Writes `attr = code`. Replaces the existing mapping line or inserts a new `BLOCK` line before the first mapping (end of file if none), or appends any other attribute at the end. |
| `BlockNames(value)` | Splits a `BLOCK` value on commas, trimmed. |
| `Labels(doc)` | Sorted map from `LINEn = text` lines (the word LINE is matched ignoring case; first occurrence of a number wins). |
| `SetLabel(doc, n, label)` | Writes `LINEn=label` (no spaces) in numeric order. |
| `LabelsPath`, `MappingPath`, `ConfigurationPath` | `<project>_wdtitle.wdl` in the project folder, `<project>.wdt`, `<project>.aepx`. |
| `ExistingCaseInsensitive(path)` | The path if it exists, else the first file in the same folder with the same name ignoring case, else null. |
| `PathList`, `JoinPathList`, `SamePathList`, `StartsWithPathList`, `SamePath`, `MergePathList` | `;` list helpers. `SamePath` trims and ignores trailing slashes and case. `JoinPathList` ends the list with `;`. `MergePathList` returns the template paths first, then project paths not already present. |

## src/DiagnosisService.cs

### DiagnosisService (static)

| Member | Behaviour |
|---|---|
| `Examine(wdpPath, std, rules, checkDrawings, insertWdm, progress?)` | Builds a `Diagnosis`. Throws `FileNotFoundException` ("Project file was not found.") or `InvalidOperationException` ("That is the LSE template itself...") when the path is the standard. Runs the private checks listed below, then sets `Selected = Recommended` on all findings. `progress` receives every log line. Callers: `DoctorForm.Examine`, `ScanForm.AddRow` and `RefreshRow`, `RepairService.Execute` (verification). |
| `Render(dx)` | Plain-text report: header (project, examined time and user, standard, rules, status), findings grouped by category, then the check log. |
| `Short(v, max = 160)` | Replaces newlines with spaces and truncates with `...`. |

Private checks, in the order run. The finding catalogue (severities, recommended rules) is in [OPERATIONS.md](OPERATIONS.md#findings-catalogue).

| Private member | Compares |
|---|---|
| `Descriptions` | `*[1]` to `*[count]`. |
| `Lines(prefix ...)` | One group of `+[n]` or `?[n]` against the template, plus extra project lines. |
| `Mapping` | `.wdt` file presence, `BLOCK`, each attribute mapping. |
| `Labels` | `.wdl` file presence and each `LINEn`. |
| `Configuration` | `.aepx` presence. |
| `Drawings` | Each listed drawing exists. With drawing checks on: if `CheckWireLayers` is set and `TemplateDrawingPath` is not empty, inspects the template drawing once; on success stores it in `Diagnosis.TemplateDrawing` and logs `Wire layers: standard is <file> with N wire layer(s): ...; WIRELAYS = "..."`, otherwise logs that the wire check is skipped (no template drawing, open in the editor, or the read error). Then inspects each drawing for `WD_M` and the title block and, when the template inspection succeeded, calls `WireLayers`. The pass count treats a drawing with any wire-layer finding as failed. |
| `WireLayers` | For one drawing against the template inspection. (a) If the drawing has a `WD_M`: for each tag in `WireLayerAttributes` that the template `WD_M` carries, a missing tag gives a report-only Warning (note: run Update to New WD_M Block), a value that fails `WireLayerService.SameValue` gives a `SetAttribute` Warning with `Value` = template text (a blank list is shown as `(blank = every layer)`). (b) For each template wire layer: absent in the drawing gives a `CreateLayer` Error (always recommended); present with `WireLayerService.Differences` gives a `RepairLayer` Warning. (c) For each wire layer of the drawing that is neither standard nor in `NeverPurgeLayers`: no entities gives a `PurgeLayer` Warning, entities give a report-only Warning. (d) For each attribute whose value matches the template, every plain name in it that exists in neither the drawing nor the template wire layers gives an Info row whose `Item` ends with ` -> <name>`. |

## src/RepairService.cs

### RepairService (static)

| Member | Behaviour |
|---|---|
| `Execute(dx, settings, progress?)` | The transaction in [ARCHITECTURE.md](ARCHITECTURE.md#42-repair-transaction). Returns a `RepairResult`. Throws `InvalidOperationException("Nothing is ticked...")`, `FileNotFoundException`, `InvalidOperationException("Read-only file: ...")` before any change; throws `InvalidOperationException` with the full log as its message after a rollback. The touched-file list (read-only check and backup) includes the `Target` of every ticked finding with `IsDrawingAction`. After the text files, the drawing findings are grouped by `Target` (case-insensitive) into one `DrawingRepairRequest` each (`InsertWdm` sets the flag, `SetAttribute` adds `Name` to `Value` in `Attributes`, the layer actions add `Name` to their list); the log gets `Repairing drawing <file> (n repair(s))` and the lines `DrawingInspectionService.Repair` returns, indented; `Applied` grows by the group size. Verification examines the text files again, inspects each repaired drawing once and asks `WireLayerService.Resolved` for every drawing finding. Side effects: backup folder, edited and created files, rewritten DWGs, `LSE_DoctorReport.txt`, pruning, `RecentProjects`. Callers: `DoctorForm.Repair`, `ScanForm.RepairTicked`. |
| `ApplyFile` (private) | For one target file (`wdt` or `wdl`): create from the standard if ticked and absent, then apply edits and write once. Throws "The standard has no <file> to copy from." if the standard lacks the file. |
| `Prune` (private) | Deletes the oldest backup folders beyond `keep`, clearing read-only attributes first. Failures are logged, not thrown. |

### ProjectConfigurationService (static)

`Write(aepxPath, wdpPath, drawings)` writes a minimal XML file (UTF-8, no BOM, written through a `.tmp` file): `ProjectConfiguration Version="1.0"` containing `Project` (attributes `ProjectPath` = project folder, `LocalGUID` = new GUID, empty `EmxFilePath` and `EmxGUID`) and `Drawings NextID="<count+1>"` with one `Drawing FilePath="<path text upper-cased>" FileID="<n>"` per drawing. The structure is the author's reading of the format; it has not been compared with a file written by AutoCAD Electrical.

## src/DrawingInspectionService.cs

### DrawingInspectionService (static)

| Member | Behaviour |
|---|---|
| `IsOpenInEditor(path)` | True if any open document's `Name` or `Database.Filename` equals the path (case-insensitive). Swallows all exceptions and returns false on error. |
| `Inspect(path, wdmBlockName, titleBlockNames)` | Returns a `DrawingInspection`. If open in the editor, sets `OpenInEditor` and stops. Otherwise reads the DWG into a side `Database` (`ReadDwgFile`, read-only share, `CloseInput`) inside one read transaction. `ReadBlocks` walks every layout block record (model space and paper space); for each top-level `BlockReference` it takes the effective name (the dynamic block's table record if dynamic). A name equal to `wdmBlockName` sets `HasWdm` and, for the first such reference, fills `WdmValues` from its attribute references; a name matching any title block pattern (`WireLayerService.MatchesAny`) is added to `TitleBlocks`. `ReadLayers` counts entities per layer over every block table record (including block definitions), then fills one `LayerInfo` per layer table record with colour text, lineweight, flags, description, linetype name (`?` if unreadable) and the regapp names in its xdata. Exceptions are caught into `Error`. Does not look inside nested blocks or xrefs. Callers: `DiagnosisService.Drawings` (template drawing and each project drawing), `RepairService.Execute` (verification). |
| `Repair(request)` | Applies one `DrawingRepairRequest` and returns the log lines. Throws `FileNotFoundException` ("Drawing was not found." / "Template drawing was not found.") and `InvalidOperationException` ("The drawing is open in the editor. Close it first: <path>") before opening anything. Opens the template as a side database only when `NeedsTemplate`, opens the drawing, makes it `HostApplicationServices.WorkingDatabase`, and inside one transaction runs, in this order: `InsertWdm` if set; `CreateLayer` for each name; `RepairLayer` for each name; `SetAttribute` for each tag; `PurgeLayer` for each name. Commits, then `SaveAs(DrawingPath, DwgVersion.Current)` and logs `Saved <path>`. The working database is restored in `finally` and the template database disposed. Any exception from a step leaves the transaction uncommitted, so the DWG on disk is not changed by that call. Caller: `RepairService.Execute`. |
| `Open` (private) | `new Database(false, true)`, `ReadDwgFile(path, OpenForReadAndAllShare, true, null)`, `CloseInput(true)`; disposes and rethrows on failure. |
| `ReadBlocks`, `ReadLayers`, `XdataApps` (private) | The inspection steps above. `XdataApps` collects the `ExtendedDataRegAppName` entries of an object's `XData`. |
| `InsertWdm` (private) | Reads the template block definition and, from the first layout reference to it, the position and every attribute text; throws if the template has no such definition. Copies the definition with `WblockCloneObjects` (`DuplicateRecordCloning.Ignore`) if the drawing lacks it (throws if it is still missing), appends a new `BlockReference` at the template position to model space, adds an `AttributeReference` per non-constant attribute definition with the template's text when the tag matches, else the definition's default. Returns `<block> inserted into model space with N attribute value(s) from <template file>`. Layer, linetype and other properties of the template's reference are not copied. |
| `SetAttribute` (private) | Finds the first top-level `WD_M` reference in any layout and sets the attribute with the given tag; returns `<block> <tag>: "<before>" -> "<after>"`. Throws if the block has no such attribute ("...Use Project > Other Tools > Update to New WD_M Block in AutoCAD Electrical first.") or if no block was found. |
| `TemplateLayer` (private) | The template's layer table record id for a name; throws "The template drawing has no layer <name>." |
| `CreateLayer` (private) | Returns "Layer <name> already exists; left as it is" if present. Otherwise clones the template layer table record into the drawing's layer table with `WblockCloneObjects` (`DuplicateRecordCloning.Ignore`), which is expected to bring the linetype, registered applications and xdata along; throws if the layer is still missing. Returns "Created layer <name> from the template (properties, linetype and wire-type data)". |
| `RepairLayer` (private) | Falls back to `CreateLayer` when the layer is missing. Otherwise reads colour, lineweight, plot flag, description, linetype id and name, and xdata from the template record; sets colour, lineweight and plot flag on the drawing's record, the description when the template's is not empty, clones the linetype table record if the drawing lacks it and assigns it (a note is added if the clone did not land), registers every regapp named in the xdata that the drawing lacks, then assigns a copy of the template xdata. Returns "Repaired layer <name>: color, linetype, lineweight, plot flag and wire-type data set from the template" with any notes. |
| `PurgeLayer` (private) | Returns "Layer <name> is already gone" if absent. Moves `CLAYER` to `0` if it is the layer, then calls `Database.Purge` on the layer id; if the id was removed from the collection (still referenced) throws "Layer <name> is still referenced (entities, block definitions or viewport overrides) and cannot be purged." Otherwise erases the record and returns "Purged layer <name>". |
| `EffectiveName` (private) | Block name resolution for dynamic blocks. |

## src/WireLayerService.cs

### WireLayerService (static)

Pure helpers; no AutoCAD types, so they run in a console test. `Ci` is `StringComparer.OrdinalIgnoreCase` and every comparison below ignores case.

| Member | Behaviour |
|---|---|
| `Split(v)` | Splits on `,` and `;`, trims, drops empties, de-duplicates. Null gives an empty list. |
| `HasWildcard(p)` | True if the text contains `*` or `?`. |
| `Wc(name, pattern)` | Wildcard match of the whole name: `*` any text, `?` one character, via an escaped regular expression. A blank pattern never matches. |
| `MatchesAny(name, patterns)` | `Wc` against each pattern. Also used for title block names. |
| `IsList(rules, tag)` | The tag is in `WireLayerListAttributes`. |
| `SameList(a, b)` | Same count after `Split` and every item of one is in the other (unordered set equality). |
| `SameValue(rules, tag, a, b)` | `SameList` for list tags, otherwise trimmed case-insensitive equality. |
| `HasWireXdata(layer, rules)` | Any of the layer's `XdataApps` matches a `WireLayerXdataApps` pattern. |
| `ListPatterns(inspection, rules)` | Every entry of every list attribute in the inspection's `WD_M`. |
| `SingleNames(inspection, rules)` | The trimmed values of the single-name attributes, excluding blanks and values with wildcards. |
| `WireLayers(inspection, rules)` | Names of the inspection's layers that match a list pattern, are a single name, or have wire xdata. |
| `IsStandard(name, template, rules, templateWireLayers)` | The name is a template wire layer, or matches the template's list patterns, or is one of the template's single names. |
| `Differences(drawing, template, rules)` | Text lines for colour, linetype and lineweight differences, plus "no AutoCAD Electrical wire-type data on the layer" when the template layer has wire xdata and the drawing's has none. Plot flag, description and the on/off/frozen/locked flags are not compared. |
| `SameLayer(drawing, template, rules)` | `Differences` is empty. |
| `Resolved(finding, after, template?, rules)` | False if the re-inspection has an error or the drawing is open. `InsertWdm`: `HasWdm`. `SetAttribute`: `SameValue` of the new attribute text and `finding.Value`. `CreateLayer`, `RepairLayer`: the layer exists and (if the template has it) `SameLayer`. `PurgeLayer`: the layer is gone. Any other action: true. Caller: `RepairService.Execute`. |

## src/AcadeBridgeService.cs

### AcadeBridgeService (static)

| Member | Behaviour |
|---|---|
| `LispFor(wdpPath, template)` | Replaces `{WDP}` (case-insensitive) with the path using `/` for `\` and `\"` for quotes. |
| `TryGetActiveProject(settings)` | Clears `USERS1`, runs `ActiveProjectLisp` with `Editor.Command`, reads `USERS1`, clears it again. Returns the full path if the value ends with `.wdp` and exists, else `""`. Any exception returns `""`. Needs an active document. |
| `GuessProjectNearActiveDrawing()` | The only `*.wdp` in the folder of the active, saved drawing; else `""`. Swallows exceptions. |
| `ReloadProject(wdpPath, settings)` | Returns silently if the file is missing, the LISP is blank or there is no active document. Otherwise queues `LispFor(...) + "\n"` with `SendStringToExecute(command, true, false, true)`. The LISP runs after the current command ends. |

## src/DiagnosticsService.cs

### DiagnosticsService (static)

`BuildReport()` returns the text of the `LSEDOCTORDIAG` report: product line "LSE_Doctor V1", runtime, assembly path, settings path, template path (exists or MISSING), bundled standard, rules path, projects root, backup policy, check settings, both LISP strings; then `Rules: PASS/FAIL`, and if the rules loaded, `Standard: PASS/FAIL` with the `.wdp` counts and encoding, `.wdt` path with mapping count and title block names, `.wdl` path with label count, and the template drawing; finally the recent project count. Exceptions from rules or standard loading are shown as FAIL lines, not thrown.

## Forms

All forms are `sealed` WinForms classes. They read `AppSettings` when constructed.

### DoctorForm (`src/DoctorForm.cs`)

`DoctorForm(string initialProject, string activeProject)`. Public: `ReloadProjectPath` (set after a successful repair of the active project when `ReactivateAfterRepair` is on). On `Shown` it loads the standard and, if the start project file exists, examines it at once. The drawing check box reads "Check every drawing: WD_M block, LSE title block and wire layers against the template drawing (opens each DWG; slower)". The grid's Repair column shows `Finding.ActionText`. Controls and behaviour are described step by step in [OPERATIONS.md](OPERATIONS.md). Private members: `BuildLayout`, `LoadStandard`, `Browse`, `UseActiveProject`, `Examine`, `FillGrid`, `UpdateSummary`, `Tick`, `ShowReport`, `Repair`, `Status`.

### ScanForm (`src/ScanForm.cs`)

`ScanForm(string activeProject)`. Public: `ReloadProjectPath`. Private members: `Browse`, `Scan` (saves the root and the drawing-check choice to settings first), `Walk` (recursive; skips the template itself, folders named like the backup folder and folders named `_V`; reports unreadable folders on the status line), `AddRow`, `RefreshRow`, `Tick`, `UpdateSummary`, `OpenInDoctor`, `RepairTicked`, `Status`. `RepairTicked` selects exactly the recommended findings of each ticked project and calls `RepairService.Execute` per project, so one failure does not stop the batch.

### DoctorSettingsForm (`src/DoctorSettingsForm.cs`)

Edits all `AppSettings` except `RecentProjects`. Save validates `BackupsToKeep` (whole number of 1 or more) and the backup folder name (not empty, no invalid file name characters), writes with `SettingsService.Save`, closes with OK. The Diagnostics button opens the same report as `LSEDOCTORDIAG`.

### TextReportForm (`src/TextReportForm.cs`)

`TextReportForm(string title, string text)`: monospaced read-only box with Save as (default file name `LSE_DoctorReport.txt`), Copy, Close.

## Project, rules and manifest files

| File | Notes |
|---|---|
| `LSE_Doctor.csproj` | `net8.0-windows`, WinForms, nullable and implicit usings on, `LangVersion latest`, x64, `EnableDynamicLoading`, no framework folder in the output path. `AcadDir` defaults to `C:\Program Files\Autodesk\AutoCAD 2026`; if `acdbmgd.dll` exists there, references `accoremgd`, `acdbmgd`, `acmgd` with `Private=false`; otherwise `PackageReference AutoCAD.NET 25.1.0` with `ExcludeAssets="runtime"`. Copies `config\LseDoctorRules.json` to the output root and `config\standard\*` to `standard\`. No assembly version property is set. |
| `config/LseDoctorRules.json` | See [DATA-STORES.md](DATA-STORES.md#lsedoctorrulesjson). |
| `config/standard/*` | See [DATA-STORES.md](DATA-STORES.md#the-lse-template-files). |
| `bundle/LSE_Doctor.bundle/PackageContents.xml` | `Name="LSE_Doctor"`, `AppVersion="1.0.0"`, `RuntimeRequirements Platform="AutoCAD*" SeriesMin="R25.1" SeriesMax="R25.1"`, module `./Contents/LSE_Doctor.dll`, `LoadOnCommandInvocation="True"`, `LoadOnAutoCADStartup="False"`, four commands in group `LSEDOCTOR`. |
| `.gitignore` | `bin/`, `obj/`, `*.user`, `.vs/`. |
