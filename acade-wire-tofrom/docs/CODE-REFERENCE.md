# Code reference: LSE_ToFrom

Maintained by DCW. Last reviewed 2026-11-13.

Every file, type and public member of `acade-wire-tofrom\src\`, in dependency order. Namespace
`LSE.AutoCADElectrical.WireToFrom`, assembly `LSE_ToFrom`. Private helpers are summarised at the end of each section.

## Commands

| Command | Method | What it does |
|---|---|---|
| `LSETOFROM` | `Commands.Run` | runs the LISP bridges (active project, scratch path, then refresh when configured; `Prepare` runs refresh before the scratch path), opens `ToFromForm` with the active project or the single `.wdp` next to the current drawing |
| `LSETOFROMEXPORT` | `Commands.ExportOnly` | `Prepare`, load rows (settings `DataSource`, `LastReportFile`), plan without annotation, `RunService.Execute(exportCsv: true)`, always show the run log and validation (ignores `ShowReportAfterRun`) |
| `LSETOFROMVALIDATE` | `Commands.ValidateOnly` | `Prepare`, load rows, plan with annotation (drawings read only; current drawing only when `CurrentDrawingOnly`), write the validation file, show it with the plan text |
| `LSETOFROMDISCOVER` | `Commands.Discover` | `Prepare`, `ScratchDb.Schema`, describe, pick the best candidate (3+ candidate columns), show its first rows, offer to save the SELECT into the config, save `Logs\Discovery_<stamp>.txt` |
| `LSETOFROMBLOCKS` | `Commands.Blocks` | folder dialog, `LabelWriter.ExportBlocks`, optional `LabelWriter.AddToDrawing` on the active document |
| `LSETOFROMSETTINGS` | `Commands.Settings` | `ToFromSettingsForm` |
| `LSETOFROMDIAG` | `Commands.Diag` | `DiagnosticsService.BuildReport` in a `TextReportForm` |

`Commands.Prepare()` (private): settings, config, active project or the single `.wdp` next to the current drawing (else
an `InvalidOperationException` telling the user to activate a project), refresh LISP, scratch path, `ProjectService.Read`.
Every command body is one `try` whose catch is `ErrorService.Show`.

## src/Models.cs

| Type | Members | Notes |
|---|---|---|
| `AppSettings` | `ConfigPath`, `BackupFolderName` (`_LSE_TOFROM_BACKUP`), `BackupsToKeep` (10), `DataSource` (`scratch`/`report`), `LastReportFile`, `StalePolicy` (`warn`/`stop`/`ignore`), `ShowReportAfterRun`, `CurrentDrawingOnly`, `ActiveProjectLisp`, `ScratchDbLisp`, `RefreshDbLisp` (blank), `RecentProjects` | persisted by `SettingsService` |
| `ToFromConfig` | `Source`, `Format`, `FarEndFormat`, `TagPinFormat`, `LocationFormat`, `MissingPinText`, `MissingTagText`, `IncludeLocation`, `Uppercase`, `MaxLength`, `SwapEnds`, `Schematic`, `Csv`, `Database`, `ReportFile`, `Both` | `tofrom.config.json` |
| `SchematicConfig` | `LabelAt`, `LabelLayer`, `BlockH`, `BlockV`, `BlockFolder`, `TextAttribute`, `NodeAttribute`, `KeyAttribute`, `ShowNode`, `TextHeight`, `EndOffset`, `SideOffset`, `CollisionDistance`, `CollisionStep`, `CollisionTries`, `RepositionOnRerun`, `DeleteStale`, `WdmBlockName` | |
| `CsvConfig` | `Delimiter`, `Encoding`, `ReverseAtFarEnd`, `OutputFolder`, `LabelFile`, `ReportFile`, `ValidationFile` | `{PROJECT}`, `{DATE}` tokens in names |
| `DatabaseConfig` | `PathPattern`, `Providers`, `WireSql`, `DrawingSql`, `DrawingIndexBase`, `Confidence`, `Note`, `Columns`, `CandidateColumns` | |
| `ReportFileConfig` | `HasHeader`, `Delimiter`, `Columns` | |
| `ColumnMap` | one string per role (`WireNo`, `Cmp1`, `Pin1`, `Cmp2`, `Pin2`, `WireHdl1/2`, `CmpHdl1/2`, `DwgIx1/2`, `Loc1/2`, `Inst1/2`, `DwgName1/2`); `All()` yields `(Role, Column, Required)` | required: `Cmp1`, `Cmp2`, `WireHdl1` |
| `WireRow` | the mapped row plus `Drawing1/2`, `Sheet1/2` after resolution; `Describe`; `Swapped()` | one physical wire |
| `LabelEnd` | `Row`, `End` (1/2), `Drawing`, `Sheet`, `WireHandle`, `ComponentHandle`, `Text`, `Key`, `Node`, `Tag`, `Pin`, `OtherTag`, `OtherPin` | one wire end, for schematic or CSV |
| `ExistingLabel` | `Handle`, `Key`, `Text`, `Node`, `Block`, `Layer`, `Point` | a label block found in a drawing |
| `DrawingLabels` | `Path`, `Exists`, `OpenInEditor`, `Locked`, `Error`, `Sheet`, `WireLayers`, `WireNoLayer`, `Labels`, `HasBlockH/V`, `Readable` | what `LabelWriter.Read` returns |
| `LabelAction` | `Insert`, `Update`, `Keep`, `Delete`, `Skip` | |
| `LabelChange` | `Drawing`, `Key`, `Action`, `Text`, `Node`, `Existing`, `ExistingHandle`, `WireHandle`, `ComponentHandle`, `Note`, `Selected`, `End`, `Writes`, `ActionText` | one grid row |
| `DrawingPlan` | `Path`, `State`, `Changes`, `SkipReason`, `Name`, `Count(action)`, `HasWork`, `Summary` | |
| `IssueKind`, `Issue` | `MissingPin`, `MissingTag`, `WireNotFound`, `DrawingSkipped`, `DrawingUnresolved`, `StaleLabel`, `DuplicateKey`, `StaleDatabase`, `LayerConflict`, `DataSource`; `Drawing`, `WireNo`, `Detail`, `Text` | validation |
| `ProjectInfo`, `ProjectDrawing` | `WdpPath`, `Folder`, `Name`, `Drawings`, `DatabasePath`, `DatabaseWritten`, `ByIndex(oneBased)`, `ByName(name)`; `Index`, `Path`, `Section`, `Description`, `ReferenceOnly` | |
| `RunPlan` | `Project`, `Config`, `DataSource`, `Rows`, `SchematicEnds`, `CsvEnds`, `Drawings`, `Issues`, `Log`, `Planned`, `AnnotationRequested`, `AllChanges`, `Selected`, `Writable`, `Count(kind)` | |
| `DrawingWriteRequest` | `DrawingPath`, `Changes`, `Schematic`, `LibraryPaths` | one drawing's write |
| `HostBridge` | `ReadLabels`, `WriteLabels`, `CanRead`, `CanWrite` | the AutoCAD seam |
| `RunResult` | `Plan`, `BackupFolder`, `Log`, `Applied`, `Unresolved`, `ReportPath`, `LabelCsv`, `ReportCsv`, `ValidationPath`, `Report` | |
| `SchemaTable` | `Name`, `Columns`, `Score` | discovery |
| `Pt` | `X`, `Y`, `Dist` | 2D point |

## src/SettingsService.cs

- `SettingsService.Load()` reads `%APPDATA%\LSE\LSE_ToFrom\settings.json`, fills defaults (config path next to the DLL,
  backup name, keep >= 1, data source and stale policy normalised). `Save(s)` writes temp + move. `Remember(s, wdp)` keeps
  25 recent projects. `SettingsPath`, `LogFolder`, `AssemblyFolder`, `Json` options (indented, case-insensitive, comments
  and trailing commas allowed).
- `ConfigService.Load(path)` deserialises or returns defaults when missing, then `Validate`; `Source` records the path.
  `Save(c, path)` validates and writes temp + move with the shared `Json` options, which have no naming policy: keys
  are written in PascalCase, comments are not preserved, and the computed `Both` and the blanked `Source` are written too. `Validate(c, path)` throws `InvalidOperationException` for an empty
  format, an empty or invalid layer name, empty block or attribute names, a `labelAt` other than from/to/both, a
  non-positive text height, a non-SELECT `wireSql`/`drawingSql`, or an empty required column; normalises `tab` and `\t`
  delimiters and unknown encodings.

## src/ProjectService.cs

- `Read(wdp)`: `ProjectInfo` with every drawing in file order (`=`, `==`, `===`, `=====SUB=`, `====REF` directives
  understood; `*`, `+`, `?` lines skipped). Throws `FileNotFoundException`.
- `FullPath(wdp, entry)`, `DatabasePath(p, d, fromLisp)` (LISP result if it exists, else the pattern with `{FOLDER}` and
  `{PROJECT}`), `OutputFolder(p, csv)`, `OutputFile(p, csv, pattern)`.

## src/ScratchDb.cs

- `DiscoverHint` constant used in messages.
- `Schema(path, d)`: `OpenSchema(4)` (columns), grouped per table, `MSys*` skipped, `Score` = number of candidate columns present.
- `Query(path, sql, providers, limit)`: SELECT only; returns rows as case-insensitive dictionaries; an empty result returns
  one row of `\0` markers so the column names are still known.
- `ReadRows(path, d, notes)`: throws with the discovery hint when `WireSql` is empty; checks required columns against the
  result; notes missing optional columns; maps through `IdentifierService.MapRow`.
- `ReadDrawings(path, d, notes)`: optional `DrawingSql`, first column index, second file name.
- `SuggestSql(table)`, `Describe(schema, d)`.
- Private `Open` tries each provider with `Mode=1`; `Release` frees COM objects.

## src/ReportImport.cs

- `Read(path, c, notes)`: encoding by BOM or UTF-8 validity, delimiter from config (first character, or `\t`) or
  guessed from the first non-blank line (tab, semicolon, comma), header = first of the first 30 lines holding `Cmp1` and (`WireHdl1` or `Cmp2`), required
  columns checked, rows mapped; the note records the row count, delimiter and header line.
- `Split(line, delim)`: RFC-style quoted field splitter.

## src/DataSourceService.cs

- `Load(s, c, p, scratchFromLisp, reportFile, issues, notes, out sourceText)`: report route or scratch route (sets
  `p.DatabasePath` and `p.DatabaseWritten`, reads rows and the drawing map, runs `Stale`), then `IdentifierService.Resolve`.
- `Stale(s, p, issues, notes)`: drawings saved more than 2 s after the database file: one `StaleDatabase` issue each; a
  WARNING note, or an exception when the policy is `stop`; nothing when `ignore`.

## src/IdentifierService.cs

- `MapRow(get, map, line)`, `Handle(text)` (upper-case, `0x` stripped), `TagPin(tag, pin, loc, c)`,
  `Compose(row, farEnd, c)` (tokens `{FROM} {TO} {FROMTAG} {FROMPIN} {TOTAG} {TOPIN} {FROMLOC} {TOLOC} {FROMINST} {TOINST} {WIRENO}`,
  upper-case, max length), `Key(handle, end)` = `HANDLE/end`, `MakeEnd(row, end, c, forCsv)`.
- `Resolve(rows, p, d, dbDrawings, issues)`: drawing paths per end; `DrawingUnresolved` once per unknown index or name.
- `Plan(p, rows, c, s, bridge, annotate, onlyDrawing, progress)`: described in ARCHITECTURE section 4.
- `LayerConflict(state, sc)`, `Wc(name, pattern)` (wildcard match), `Render(plan)`, `NothingToDo(plan)`.

## src/Export.cs

- `LabelColumns` (`Drawing, Sheet, WireNo, End, Label, Tag, Pin, OtherTag, OtherPin, WireHandle, ComponentHandle, Key`),
  `ReportColumns` (`WIRENO, CMP1, PIN1, CMP2, PIN2, WIREHDL1, WIREHDL2, CMPHDL1, CMPHDL2, DWGIX1, DWGIX2`).
- `EncodingFor(csv)`, `Field(value, delim)` (quotes only when needed), `Line(fields, delim)`, `LabelLines(plan)`,
  `ReportLines(plan)`, `WriteLabelCsv`, `WriteReportCsv`, `Validation(plan, result)`, `WriteValidation`.

## src/RunService.cs

- `Execute(plan, s, bridge, exportCsv, progress)`: the transaction (ARCHITECTURE section 4). Throws
  `InvalidOperationException` carrying the whole log on failure after rollback.
- `LibraryPaths(c)`: `schematic.blockFolder` entries (`;` separated) or the DLL folder.

## src/LabelWriter.cs

- `FindOpenDocument(path)`, `Open(path)` (side database), `IsLockedElsewhere(path)` (`.dwl`/`.dwl2`).
- `Read(path, sc)`: `DrawingLabels` (open-document or side-database read; `WD_M` `WIRELAYS`, `WIRENO_LAY`, `SHEET`; label
  blocks by effective name or by the key attribute).
- `Write(request)`: open document under `LockDocument` (not saved) or side database (`SaveAs`); throws when locked.
- `EnsureDefinition(db, tr, name, vertical, sc, libraryPaths, log)`, `CreateDefinition(db, tr, name, vertical, sc)`,
  `AddToDrawing(document, sc, libraryPaths)`, `ExportBlocks(folder, sc)` (`Database.Wblock` per block, `SaveAs`),
  `EffectiveName(reference, tr)`.
- Private: `WriteTransaction` (layer, definitions, occupied positions, per-change switch, summary line), `Locate`
  (placement rules), `SetAttributes`, `ValueFor`, `AddAttribute`, `EnsureLayer`, `TryGet`, `ReadAttributes`, `Value`, `Split`.

## src/AcadeInterop.cs

- `TryGetActiveProject(s)`, `TryGetScratchDbPath(s)`, `RefreshDatabase(s)` (returns a note), `GuessProjectNearActiveDrawing()`,
  `CurrentDrawingPath()`, `Bridge(c)`. Private `Lisp(expr)` runs an expression through `Editor.Command` and reads USERS1.

## Forms

- `ToFromForm(initialProject, activeProject, scratchPath, currentDrawing, refreshNote)`: header (project, data source
  radios and report file, three tick boxes, Plan), grid (Apply?, Drawing, Change, Wire no, Label, Drawing has, Key, Note),
  tick buttons and "Show unchanged", footer (status, Plan text, Validation, Apply ticked, Help, Close). On `Shown` it
  loads the config (`LoadConfig`; a failure disables Plan) and plans when the project file exists. `Plan()` saves
  `DataSource` and `LastReportFile`, then loads project, rows and plan with `annotateBox.Checked` and `OnlyDrawing`
  (current drawing when `currentOnlyBox` is ticked). `Apply()` reads `exportBox.Checked`, confirms, runs
  `RunService.Execute`, shows the report when `ShowReportAfterRun`, re-plans. `FillGrid()` hides non-writing rows unless
  `showKeepBox` is ticked; `Tick(rule)` sets `Selected` on writable changes of non-skipped drawings; `UpdateSummary()`
  sets the summary and `applyButton.Enabled = Selected > 0 || exportBox.Checked` (not re-run when Export CSV changes).
  The header tick boxes have no change handlers. Tooltips on every control.
- `ToFromSettingsForm`: Config JSON (with Browse), Data source and stale-policy drop-downs, backup name and count, the
  three LISP strings, two tick boxes (`ShowReportAfterRun`, `CurrentDrawingOnly`), Diagnostics, "Open config" (Notepad),
  Save (validates the count and folder name, loads the config file if it exists, saves, closes). No Help button or
  tooltips. `LastReportFile` and `RecentProjects` are not shown.
- `TextReportForm(title, text)`: read-only Consolas text with Save as.

## src/DiagnosticsService.cs, src/ErrorService.cs, src/HelpText.cs

- `DiagnosticsService.BuildReport()`: assembly, settings, config summary, block DWG presence, ADODB availability and
  per-provider registration, the unverified list.
- `ErrorService.Log(step, ex)` writes `Logs\Error_<stamp>.txt`; `Show(owner, step, ex, advice)` shows the three-part dialog.
- `HelpText.Main`, `HelpText.Fix(text)` (CRLF).

## Project, config, manifest and test files

- `LSE_ToFrom.csproj`: `net8.0-windows`, WinForms, x64, nullable, `AcadDir` detection, `AutoCAD.NET` 25.1.0 reference
  package fallback, copies `config\tofrom.config.json` next to the DLL, excludes `tests\**`.
- `config\tofrom.config.json`: see DATA-STORES.
- `bundle\LSE_ToFrom.bundle\PackageContents.xml`: `AppVersion` 1.0.0, series `R25.1` only, `LoadOnCommandInvocation`
  for the seven commands, not loaded at start-up. The `.csproj` sets no version, so the assembly is `1.0.0.0`.
- `tests\LSE_ToFrom.SmokeTest\`: console program, 73 checks, fakes `HostBridge`, works in `%TEMP%`.
