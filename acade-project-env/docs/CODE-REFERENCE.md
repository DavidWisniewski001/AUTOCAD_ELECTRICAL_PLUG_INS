# Code reference: LSE_Env

Maintained by DCW. Last reviewed 2026-11-12.

Namespace `LSE.AutoCADElectrical.ProjectEnvironment`, assembly `LSE_Env`. All classes are `sealed` or `static`.

## Contents

- [Commands](#commands)
- [src/Plugin.cs](#srcplugincs)
- [src/Models.cs](#srcmodelscs)
- [src/SettingsService.cs](#srcsettingsservicecs)
- [src/TextFileService.cs](#srctextfileservicecs)
- [src/EnvironmentService.cs](#srcenvironmentservicecs)
- [src/WireNetworkService.cs](#srcwirenetworkservicecs)
- [src/ApplyService.cs](#srcapplyservicecs)
- [src/DrawingSettingsService.cs](#srcdrawingsettingsservicecs)
- [src/WireNumberDrawingService.cs](#srcwirenumberdrawingservicecs)
- [src/AcadeBridgeService.cs](#srcacadebridgeservicecs)
- [src/DiagnosticsService.cs](#srcdiagnosticsservicecs)
- [src/ErrorService.cs](#srcerrorservicecs)
- [src/BuildInfo.cs](#srcbuildinfocs)
- [src/PlanGridService.cs](#srcplangridservicecs)
- [Forms](#forms)
- [Project, rules, profiles, manifest and test files](#project-rules-profiles-manifest-and-test-files)

## Commands

| Command | Method | Opens |
|---|---|---|
| `LSEENV` | `Commands.Env` | `EnvironmentForm(start, active)`; afterwards `After(reload, retag)` |
| `LSEENVWIRES` | `Commands.Wires` | `WireSchemeForm(start, active)`; afterwards `After` |
| `LSEENVPROFILES` | `Commands.Profiles` | `ProfilesForm(projectPath)` |
| `LSEENVSNAPSHOT` | `Commands.Snapshot` | `SnapshotForm(start)` |
| `LSEENVSETTINGS` | `Commands.Settings` | `EnvSettingsForm` |
| `LSEENVDIAG` | `Commands.Diag` | `TextReportForm` with `DiagnosticsService.BuildReport()` |

`start` is the active project (`AcadeBridgeService.TryGetActiveProject`) or the single `.wdp` next to the active
drawing. `After` reloads the project through `ReloadProject` when the form set `ReloadProjectPath`, then queues the
retag LISP when `RetagRequested`.

## src/Plugin.cs

`Plugin : IExtensionApplication` (empty `Initialize`/`Terminate`). `Commands` as above, every body one `try` with
`ErrorService.Show(null, "<command name>", ex)` (full exception in the log file only).

## src/Models.cs

| Type | Members |
|---|---|
| `AppSettings` | `RulesConfigPath`, `ProfilesFolder`, `TemplateProjectPath`, `BackupFolderName` (`_LSE_ENV_BACKUP`), `BackupsToKeep` (10), `ApplyToDrawings` (true), `ComputeFromTo` (true), `RetagAfterApply` (false), `ReactivateAfterApply` (true), `ShowReportAfterApply` (true), `ActiveProjectLisp`, `ActivateProjectLisp`, `RetagLisp`, `LastProfile`, `LastScheme`, `RecentProjects` |
| `EnvRules` | `Source`, `WdmBlockName`, `Settings` (`List<SettingMap>`), `WireSchemes`, `FromTo` (`FromToRules`), `ProtectedOptions` (`1..4`), `ProtectedProperties`, `Labels`; `Map(key)`, `Scheme(name)`, `MapFor(prefix, index)`, `MapForWdm(tag)`, `Label(prefix, index)`, `IsProtected(prefix, index)` |
| `SettingMap` | `Key`, `Label`, `Group`, `Property` (-1), `Option` (-1), `Wdm`, `Kind`, `Choices`, `Confidence`, `Note`, `Protected`; `Mapped`, `Where`, `ChoiceList()`, `Describe(value)` |
| `WireScheme` | `Name`, `Description`, `FromTemplate`, `TemplateKeys`, `Values`, `FromTo`; `IsFromTo` |
| `FromToSettings` | `Format`, `DuplicateSuffix`, `IncludeTerminalPin`, `PinFormat`, `MultiDevice`, `AllSeparator`, `DevicePriority`, `TerminalsLast`, `Uppercase`, `MaxLength`, `Fixed`, `SingleDevice`, `NoDevice`, `OverwriteFixed`; `Clone()` |
| `FromToRules` | `WireNumberBlocks`, `WireNumberAttribute`, `InsertBlockName`, `InsertMissing`, `InsertOffset`, `ConnectionAttributePattern`, `PinAttributePrefix`, `TagAttributes`, `TerminalStripAttribute`, `SignalCodeAttribute`, `IgnoreBlocks`, `JoinTolerance`, `ConnectTolerance`, `GapTolerance`, `MarkTolerance` |
| `EnvironmentProfile` | `Name`, `Customer`, `Description`, `Scheme`, `Values`, `Properties`, `Options`, `Drawing`, `FromTo`, `WdtSource`, `WdlSource`, `Notes`, `CapturedFrom`, `Captured`; `[JsonIgnore] FilePath`, `Display`; `Clone()` |
| `TextDocument`, `WdpDrawingEntry` | as in LSE_Doctor |
| `DrawingState` | `Path`, `Exists`, `OpenInEditor`, `ReadFromEditor`, `HasWdm`, `Error`, `WdmValues`, `WdmTags`; `Wdm(tag)`, `Has(tag)`, `Readable` (exists, no error; an open drawing read fine counts), `Name` |
| `ProjectState` | `WdpPath`, `Folder`, `ProjectName`, `Wdp`, `Properties`, `Options`, `Entries`, `Drawings`, `DrawingsInspected`, `Marker`, `Log`; `Property(i)`, `Option(i)` |
| `ChangeKind` | `Info, SetProperty, SetOption, SetWdm, CopyFile, WireNumbers, UnfixWireNumbers, Marker` |
| `Change` | `Kind`, `Scope`, `Item`, `Current`, `New`, `Note`, `Selected`, `Recommended`, `Target` (`wdp`, `marker` or a drawing/file path), `Prefix`, `Index`, `Name`, `Value`, `Source`, `Wires`, `Handles`; `IsDrawing`, `Applies`, `KindText` ("(note)" for information rows), `Key` |
| `EnvPlan` | `Project`, `Profile`, `Scheme`, `Rules`, `Resolved`, `FromTo`, `Changes`, `Log`, `Planned`; `Selected`, `Applicable`, `ProfileName`, `SchemeName` |
| `EnvMarker` | `Profile`, `Customer`, `Scheme`, `Applied`, `By`, `FromTo`, `Properties`, `Options`, `DrawingAttributes`, `Values` |
| `ApplyResult` | `Plan`, `BackupFolder`, `Log`, `Applied`, `Unresolved`, `ReportPath`, `Report` |
| `Snapshot` | `WdpPath`, `Taken`, `Wdp` (`"?[6]"` -> value), `Drawings` (path -> tag -> value), `Notes` |
| `Pt` (record struct) | `X`, `Y`, `Dist`, `Mid` |
| `WireSegment` | `A`, `B`, `Layer`, `Handle`; `Length`, `Horizontal` |
| `DeviceTerminal` | `Tag`, `Pin`, `Point`, `Block`, `Handle`, `IsTerminalStrip`; `Display(FromToSettings)` |
| `WireNumberMark` | `Handle`, `Point`, `Number`, `Layer`, `Block`, `IsFixed` |
| `SignalArrow` | `Code`, `Point`, `Handle`, `Block` |
| `DrawingWires` | `Path`, `Sheet`, `WireNoLayer`, `FixedLayer`, `WireLayerPatterns`, `Segments`, `Terminals`, `Marks`, `Signals`, `Notes`, `UntaggedConnections` |
| `WireAction` | `Keep, Set, Insert, Skip` |
| `WireNetwork` | `Id`, `Drawing`, `Segments`, `Devices`, `Marks`, `SignalCodes`, `Logical`, `Proposed`, `Existing`, `Action`, `Note`, `LabelPoint`, `LabelRotation`, `DeviceText` |
| `WirePlan` | `Drawing`, `Networks`, `Notes`, `Error`, `FixedLayer`, `WireNoLayer`, `Fixed`, `Sheet`; `Count(action)`, `HasWork`, `Summary` |
| `DrawingApplyRequest` | `DrawingPath`, `WdmBlockName`, `Attributes`, `Wires`, `LibraryPaths`, `UnfixHandles`, `WireNoLayer` |
| `HostBridge` | `Inspect`, `ReadWires`, `ApplyDrawing` delegates; `CanInspect`, `CanReadWires` |

## src/SettingsService.cs

- `SettingsService`: `SettingsPath`, `LogFolder`, `SnapshotFolder`, `AssemblyFolder`, `BundledProfilesFolder`, `Json`
  (shared serializer options), `Load()` (fills defaults), `Save(s)` (temp + move), `Remember(s, wdp)`.
- `RulesService.Load(path)`: deserializes, trims keys, rejects duplicate keys and scheme keys the map lacks, checks the
  connection regex; built-in defaults when the file is missing (`Source` says so).
- `ProfileService`: `Load(s)` (seeds the folder from `profiles\` next to the DLL when empty), `Read(path)`,
  `Save(s, p)` (sanitized file name, refuses to overwrite another profile's file, moves a renamed profile),
  `Export(p, path)`, `Retire(s, p)` (moves to `_retired\<name>_<stamp>.json`), `FileName(name)`.
- `MarkerService`: `PathFor(wdp)` (`<project>.lseenv.json`), `Read`, `Write`.

## src/TextFileService.cs

Identical in behaviour to LSE_Doctor's: `Read`, `Write`, `EncodingName`, `TryIndexed`, `Find`, `Get`, `Group`, `Set`
(index-ordered insertion, group order `*`, `+`, `?`), `Drawings`, `FullPath`, `LabelsPath`, `MappingPath`,
`ExistingCaseInsensitive`, `PathList`.

## src/EnvironmentService.cs

| Member | What it does |
|---|---|
| `ReadProject(wdp, rules, bridge, inspectDrawings, progress)` | `ProjectState` with lines, drawings, marker; inspects drawings (open ones through their Document) through `bridge.Inspect` |
| `ResolveFromTo(profile, scheme)` | profile's `FromTo` when the scheme is From/To (or no scheme), else the scheme's |
| `Resolve(profile, scheme, rules, settings, log)` | key -> value: scheme (template or fixed) then profile |
| `Plan(state, profile, scheme, rules, settings, bridge, applyDrawings, traceWires, progress)` | the `EnvPlan`; resolves the profile's scheme when none is passed |
| `Capture(state, rules, name)` | profile from the project's current mapped, unprotected values |
| `Take(wdp, rules, bridge, drawings)` / `Diff(a, b, rules)` | snapshot and textual diff with map keys |
| `Render(plan)`, `Short(text)` | report text |

Private helpers: `Line` (one `?[n]`/`+[n]` change, replacing an earlier one for the same line), `Wdm` (per-drawing
`WD_M` change), `Note` (deduplicated per-drawing note), `CopyFile`, `WirePlans`, `UnfixPlans`, `Info`, `Current`.

## src/WireNetworkService.cs

Pure logic, no AutoCAD reference. `Wc`, `MatchesAny` (wildcards), `PointToSegment`, `Distance`, `Build(wires, rules)`,
`NaturalCompare`, `Rank`, `Ordered`, `Compose`, `Assign(plans, settings, rules)`, `Verify(plan, after, rules)`,
`Render(plan)`. Section 4 of [ARCHITECTURE.md](ARCHITECTURE.md) describes the algorithm.

## src/ApplyService.cs

`Execute(plan, settings, bridge, progress)` returns `ApplyResult` or throws `InvalidOperationException` whose message
is the whole log after rollback. Private `Prune(root, keep, log)`.

## src/DrawingSettingsService.cs

`FindOpenDocument(path)` -> `Document?` (match on `Name` or `Database.Filename`), `IsOpenInEditor(path)`, `Open(path)` (side database),
`Inspect(path, wdmBlockName)` -> `DrawingState` (open drawing: read from `document.Database`, `ReadFromEditor = true`; shared `ReadWdm`),
`Apply(request, rules)` (open drawing: `ApplyTransaction` under `LockDocument()`, no save; closed: side database, `ApplyTransaction`, `SaveAs`),
`FindBlock(database, transaction, name)`, `EffectiveName(reference, transaction)`.

## src/WireNumberDrawingService.cs

`Read(path, rules)` and `Read(database, transaction, rules, path)` -> `DrawingWires` (lines and polyline segments on
the `WIRELAYS` layers, symbol connection points and tags, wire-number marks, signal arrows, `SHEET`).
`Write(database, transaction, plan, fromToRules, libraryPaths)` sets or inserts wire numbers and moves them to the
fixed layer. `Unfix(database, transaction, handles, wireNoLayer)` moves blocks back. Private: `ReadBlock`,
`InsertMark`, `FindOrImportDefinition` (`Database.Insert` from `<library>\<block>.dwg`), `EnsureLayer`, `TryGet`
(handle lookup), `ReadAttributes`, `Value`, `Split`, `IsWireLayer`.

## src/AcadeBridgeService.cs

`LispFor`, `TryGetActiveProject(s)`, `GuessProjectNearActiveDrawing()`, `ReloadProject(wdp, s)`, `QueueRetag(s)`,
`Bridge(rules)` (the `HostBridge` wired to the two AutoCAD services).

## src/DiagnosticsService.cs

`BuildReport()`: assembly, settings and paths, rules source with the setting map per group (where, confidence,
protected, note), schemes, From/To rules, profiles with their scheme and override counts, the unverified reminder.

## src/ErrorService.cs

Static class, compact. `Log(step, ex)` writes `Error_<yyyyMMdd_HHmmss>.txt` (step, build, time, user, machine, `ex.ToString()`) into
`SettingsService.LogFolder`, creating it, and returns the path ("" if the write failed; failures are swallowed).
`Show(owner, step, ex, advice = null)` calls `Log`, then shows a MessageBox titled `LSE_Env: <step>` with "What happened"
(`ex.Message`), "Where" (the step) and "What to do" (the advice, or a default naming the log file; shorter for
`InvalidOperationException` and `FileNotFoundException`, whose messages are already plain English). Used by every form
`catch` and the six commands (step = command name); `ex.ToString()` goes to the log only. The Apply rollback path
calls `Log` and still shows the full rollback log in `TextReportForm`.

## src/BuildInfo.cs

`Release` (`"2026-11-12-r6"`, bump it with every release zip together with `<Version>` in the `.csproj` and
`AppVersion` in `PackageContents.xml`), `Label` (`LSE_Env <Release>`) and `Title(window)` (`LSE_Env: <window>   [<Release>]`,
used for every form's `Text`). `ErrorService` writes `Build:` into the log and the dialog's "Where" line;
`DiagnosticsService` prints it on line 2. Purpose: a stale DLL in the bundle's `Contents\` is recognised at a glance.

## src/PlanGridService.cs

The `LSEENV` plan grid, kept out of `EnvironmentForm` so the smoke test can run it on a real `DataGridView`.
`AddColumns(grid)` adds the seven columns (check box `Apply?`, Scope, Item, Project has, Becomes, Change, Note).
`Fill(grid, plan, showNotes)` clears and refills: one row per `Change` (notes skipped when `showNotes` is false),
`Tag` = the `Change`; note rows (`!Applies`) keep their check-box cell, are `ReadOnly` at row level, show `"note"` in
the Change column and are greyed; not-recommended rows are tinted. No cell is ever replaced after the row exists,
which is the Windows Forms rule the r2/r3 code broke ("ReadOnly property of a cell cannot be set before it is added
to a row"). `IsNoteRow(grid, i)`; `PaintCell(grid, e)` is wired to `CellPainting` by the form and draws the word
"note" over the check box of a note row.

## src/HelpText.cs

Static class, compact. String constants `Profiles`, `Environment` and `WireScheme`: plain-text help for engineers
(what a profile is, steps, worked example, field meanings, sample profiles, file locations). `Fix(text)` converts
line endings to CRLF for the TextBox. Shown through `TextReportForm` by the Help buttons.

## Forms

| Form | Density | Notes |
|---|---|---|
| `EnvironmentForm(initialProject, activeProject)` | expanded | Help button next to Close, tooltips on all header controls, tick boxes, Plan, tick buttons, Report and Apply (`SetTips`); properties `ReloadProjectPath`, `RetagRequested`; loads rules and profiles in `Shown`, plans automatically when a project is preset; grid columns Apply, Scope, Item, Project has, Becomes, Change, Note; Capture... prompts for a name and saves a profile; Manage... opens `ProfilesForm`; Apply re-plans afterwards |
| `WireSchemeForm(start, activeProject)` | compact | Help button, tooltips; radio button per scheme with description; Preview shows the rendered plan; Switch confirms counts then applies |
| `ProfilesForm(projectPath)` | expanded | Help button (left of Save), `ToolTip` on every editor control (`SetTips`, scheme tooltip built from the loaded rules in `SetSchemeTip`), grid Value cell `ToolTipText` from the map label/note/choices; list, editor (name, customer, description, scheme, settings grid, raw overrides, file sources, From/To block), New, Duplicate, Capture from project..., Import..., Export..., Retire, Save; warns on unsaved changes |
| `SnapshotForm(start)` | compact | Take A, Take B, Diff, B becomes A, Save diff; A and B persist in `Snapshots\last_A.json` / `last_B.json` so AutoCAD can be used between them |
| `EnvSettingsForm` | compact | every `AppSettings` field; Diagnostics button |
| `TextReportForm(title, text)` | compact | read-only monospace text, Save as |

## Project, rules, profiles, manifest and test files

- `LSE_Env.csproj`: `net8.0-windows`, WinForms, x64, nullable, `AssemblyName` `LSE_Env`; AutoCAD DLLs from `AcadDir`
  or the `AutoCAD.NET` 25.1.0 package; copies `config\LseEnvRules.json` and `config\profiles\*.json`; excludes `tests\`.
- `config\LseEnvRules.json`: see [DATA-STORES.md](DATA-STORES.md) section 3.
- `config\profiles\*.json`: the three sample profiles.
- `bundle\LSE_Env.bundle\PackageContents.xml`: autoloader manifest, six commands, `R25.1`.
- `tests\LSE_Env.SmokeTest\`: console project referencing `bin\Release\LSE_Env.dll`; 76 checks.
