# Developer guide: LSE_ToFrom

Maintained by DCW. Last reviewed 2026-11-13.

## 1. State of the code

Written 2026-11-12 to the work scope `..\..\Agent Brief True To From Wire Numbers in AutoCAD Electrical 2026.docx`.
Compiles in Release with 0 warnings against the `AutoCAD.NET` 25.1.0 reference package (portable .NET 8 SDK, no AutoCAD
on the authoring machine). 73-check smoke test passes. Never loaded in AutoCAD Electrical. The scratch-database query is
empty by design until Discovery (OPERATIONS section 2). Phases of the work plan: 1 Discovery (tooling delivered:
`LSETOFROMDISCOVER`; the findings note itself needs a workstation), 2 Data and export (done, smoke-tested), 3 Drawing
annotation (code done, unverified in AutoCAD), 4 Integration (bundle manifest done; ribbon button and auto-run after
AEWIRENO not done), 5 Documentation (this set; the one-page user guide is README + OPERATIONS section 3).

As of 2026-11-13 the folder is not yet committed to the repository and the plugin is not in any release package (the
2026-11-12-r7 cut has the other six plugins only). It has none of the r7 additions its siblings received: no
`VaultProbe` (Vault client and add-in login in DIAG, refusal of Vault-locked targets), no `BuildInfo` build tag, no
`.csproj` version.

## 2. Repository layout

```
acade-wire-tofrom\
  LSE_ToFrom.csproj
  README.md
  config\tofrom.config.json
  bundle\LSE_ToFrom.bundle\PackageContents.xml
  src\  AcadeInterop  Commands  DataSourceService  DiagnosticsService  ErrorService  Export  HelpText
        IdentifierService  LabelWriter  Models  ProjectService  ReportImport  RunService  ScratchDb
        SettingsService  TextReportForm  ToFromForm  ToFromSettingsForm
  tests\LSE_ToFrom.SmokeTest\  (console, references bin\Release\LSE_ToFrom.dll)
  docs\  ARCHITECTURE  CODE-REFERENCE  DATA-STORES  OPERATIONS  DEVELOPER-GUIDE  GLOSSARY
```

## 3. Build

With AutoCAD Electrical 2026 installed:

```
dotnet build -c Release
```

The project references `accoremgd`, `acdbmgd`, `acmgd` from `C:\Program Files\Autodesk\AutoCAD 2026` (`Copy Local` off);
pass `-p:AcadDir="D:\Autodesk\AutoCAD 2026"` for another location. Without an install the `AutoCAD.NET` 25.1.0 NuGet
reference package is used at compile time only. The config JSON is copied next to the DLL.

Smoke test (no AutoCAD needed):

```
dotnet build -c Release
dotnet run -c Release --project tests\LSE_ToFrom.SmokeTest -- <absolute path to acade-wire-tofrom>
```

Run both from `acade-wire-tofrom\`. The test references `bin\Release\LSE_ToFrom.dll`, so the plugin must be built first.
The `.csproj` comment spells the flags `- -project` and `- -` because XML comments cannot hold a double hyphen; type
them as `--project` and `--`. Expected end: `73 passed, 0 failed`, exit code 0.

It works in `%TEMP%\LSE_ToFrom_smoke_<stamp>` and writes `Error_*.txt`/`ToFromRun_*.txt` into the real
`%APPDATA%\LSE\LSE_ToFrom\Logs` (the error-log checks).

## 4. Deploy and debug

NETLOAD or the bundle (README). Debug by attaching to `acad.exe` after NETLOAD. `LSETOFROMDIAG` prints paths and
providers; `Logs\Error_<stamp>.txt` carries every caught exception; the run log in the project folder shows each write.
When a label lands in the wrong place, check the `Locate` rules in `LabelWriter` against the actual wire geometry: the
component handle decides which end is the wire end.

## 5. Conventions in use

- File-scoped namespaces, `sealed`/`static` classes, nullable on, `OrdinalIgnoreCase` for every name comparison.
- Compact density (one member per line) everywhere except `LabelWriter` and `ToFromForm` (expanded, 4-space).
- Errors for the engineer are `InvalidOperationException` with plain English; caught at the command or button boundary
  by `ErrorService.Show`.
- Settings only through `SettingsService.Save`; config through `ConfigService.Save` (temp + move).
- No COM outside `ScratchDb` (and the provider check in `DiagnosticsService`); no AutoCAD types outside `LabelWriter`,
  `AcadeInterop`, `Commands`, `ToFromForm`.
- Docs signed `Maintained by DCW.`; no tool or assistant named as author.

## 6. Recipes

- **Add a config key:** property with default on the right class in `Models.cs`; a line in `ConfigService.Validate` if it
  can be wrong; use it; document it in `config\tofrom.config.json`, DATA-STORES section 3 and the README.
- **Add an identifier token:** one more `.Replace` in `IdentifierService.Compose`; a smoke check; the help text and docs.
- **Add a validation kind:** `IssueKind` value, an `Explain` line in `Export`, a README table row, raise it in `Plan` or
  `DataSourceService`.
- **Change the CSV layout:** `Export.LabelColumns` and `LabelLines`; the smoke test's header and row checks; DATA-STORES
  section 6; agree it with the label printer import template.
- **Read another database table (for example the drawing list):** only in `ScratchDb`; the SELECT text in the config;
  never a write.
- **Add a setting:** property on `AppSettings`, a normalising line in `SettingsService.Load` if needed, an `Add` or
  `Check` row in `ToFromSettingsForm` and its line in the Save handler, the DIAG line, DATA-STORES section 2 and
  OPERATIONS section 9.
- **Give the plugin a version:** add `Version`, `AssemblyVersion`, `FileVersion` and `InformationalVersion` to the
  `.csproj` and `AppVersion` in `PackageContents.xml`, as LSE_Env does; copy LSE_Env's `BuildInfo` pattern for a build
  tag in the window titles.
- **Change the key format:** do not. Every placed label would become stale. If it must change, add a migration that maps
  old keys first and ask the user.

## 7. First-build verification checklist (workstation)

1. `dotnet build -c Release` against the real AutoCAD assemblies: 0 warnings.
2. NETLOAD; `LSETOFROMDIAG`: ADODB available, a provider registered, config loaded.
3. `LSETOFROMDISCOVER` on a test project after a native Wire From/To report: a CANDIDATE table appears; its first rows
   match the report; accept the SELECT. Check the LISP line reports the scratch database path.
4. `LSETOFROMBLOCKS`: two DWGs written; open one and confirm the three attributes, their visibility and the vertical rotation.
5. `LSETOFROMVALIDATE`: issues make sense; `DrawingUnresolved` absent (else fix `drawingIndexBase`/`drawingSql`).
6. `LSETOFROM` on a copy: Plan, Apply; open a sheet and judge the offsets (`endOffset`, `sideOffset`, `textHeight`), then
   the ten acceptance tests in OPERATIONS section 4.
7. Compare `_tofrom_report.csv` with the saved native report. Time the run on the largest project.
8. Open drawing case: with a sheet open, run; confirm the labels appear in the session and the drawing is marked modified,
   not saved.
9. Locked drawing case: open a sheet on another seat (or create a `.dwl` by opening it in a second AutoCAD); run; the
   drawing is skipped and reported.

## 8. Test strategy

The smoke test covers everything that does not need AutoCAD: config validation and round trip, `.wdp` parsing, identifier
composition (tokens, missing values, location, upper case, length), handle and key normalisation, report import
(title lines, quoting, tab/BOM, refusal), planning (counts, validation kinds, unresolved drawings, three-connection
network, run twice unchanged, retag update, stale and duplicate deletion, `deleteStale` off, `labelAt=both`,
`repositionOnRerun`, locked and open drawings, current-drawing scope, layer conflicts, export-only), the run transaction
(backup of changed closed drawings only, CSV content and encoding, verify, rollback, read-only refusal, pruning), stale
database policies, CSV quoting, error log and help text. `LabelWriter`, `AcadeInterop` and the forms are covered only by
the workstation checklist.

## 9. Known gaps

- `database.wireSql` is empty: the from/to table of the 2026 scratch database is undocumented. `LSETOFROMDISCOVER` is the
  tool to fill it; until then the report-file source is the only working route.
- Column semantics are assumed from the Autodesk report documentation: `WIREHDL1/2` = handle of the wire entity touching
  CMP1/CMP2, `CMPHDL1/2` = handle of the component block, `DWGIX` = 1-based position in the project list. Any of these may
  differ; `Resolve` and `Locate` are the places to adjust.
- Refreshing the project database from code: no function configured. The scope names `AEONLISPDEBUG` as the way to find
  what the native report calls; put the result in `RefreshDbLisp`.
- `ace_getactiveproject` and `c:wd_mdb_get_proj_scratch_dbnam` are forum-sourced names.
- `.dwl`/`.dwl2` as the lock signal is a convention of AutoCAD's own locking; a network share may behave differently.
- Placement rules (nearest endpoint to the component insertion point, left-side offset, step along the wire) are untested
  against real symbol geometry; a component whose insertion point is far from its connection may pick the wrong end.
  A better rule would use the connection attribute position; LSE_Env's `WireNumberDrawingService` shows how to read it.
- Polylines use their first and last vertex only; a wire drawn as a multi-vertex polyline gets its label at the extreme end.
- No ribbon button, no automatic prompt after AEWIRENO (work plan phase 4, optional).
- `WIREFRM2.lsp/.dcl` User Post files are not provided (the scope marks them optional; the DLL writes the CSV itself).
- Terminal blocks are endpoints and cables are not considered, as the scope's defaults say; neither is configurable yet.
- Characters `:` and `/` in attributes, CSV and the label printer import are untested end to end.
- No Vault awareness: a closed drawing that Vault keeps read-only is refused by the read-only check (with a check-out
  hint) before any backup; nothing else is Vault-specific, and DIAG does not show the Vault client.

### Suspected bugs (found while documenting 2026-11-13; not fixed)

| Where | Problem |
|---|---|
| `src/IdentifierService.cs:40` | The drawing's `WD_M` SHEET is copied onto the schematic ends only; the CSV ends are separate objects, so the label CSV's `Sheet` column holds the `DWGIX` value or the `.wdp` position, never the sheet number. |
| `src/RunService.cs:25-26, 32` | Rollback deletes every output path the run wrote; a CSV or validation file of the same name from an earlier run has already been overwritten, so a failed run destroys the previous outputs. The smoke test deletes the CSV before its rollback check (`tests/LSE_ToFrom.SmokeTest/Program.cs:244`), which hides this. |
| `src/RunService.cs:30-34` | A failed run is logged only under `%APPDATA%`, not appended to the project's `LSE_ToFromReport.txt`, so the project record has no trace of it. |
| `src/ToFromForm.cs:25-26` | Annotate drawings and Current drawing only have no change handler and are read only by `Plan()`; changing them after planning and pressing Apply ticked still writes the old plan (for example labels after Annotate was unticked). Re-plan on change, or disable Apply until Plan. |
| `src/ToFromForm.cs:451` | `applyButton.Enabled` depends on Export CSV but is recomputed only on grid changes and plans, so toggling Export CSV leaves the button state stale. |
| `src/ToFromForm.cs:227` | Tick recommended ticks exactly what Tick all ticks (a stale label is a Delete only when `deleteStale` is on, and the rule ticks it then), so the two buttons are indistinguishable. |
| `src/LabelWriter.cs:206-213, 245` | With `repositionOnRerun`, the label being moved is still in the `occupied` list at its old position, so it can collide with itself and step along the wire on every run. |
| `src/SettingsService.cs:17` | `ConfigService.Save` (used only by discovery) drops all comments and rewrites keys in PascalCase; it also fails when the config sits in the write-protected bundle folder. |
| `src/ReportImport.cs:8` | `reportFile.delimiter` accepts `\t` but not the word `tab` (taken as the letter `t`), unlike `csv.delimiter`. |
| `src/SettingsService.cs:26` | An empty `database.providers` falls back to ACE 16 and 12 only, not the documented three (Jet 4 dropped). Harmless on 64-bit. |
