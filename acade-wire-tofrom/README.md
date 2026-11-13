# LSE_ToFrom (AutoCAD Electrical 2026 plugin)

Maintained by DCW. Last reviewed 2026-11-13.

Gives every physical wire in an AutoCAD Electrical project a **true To/From identifier** of the form
`FROMTAG:PIN/TOTAG:PIN`, for example `K1:13/TB2:1`, shows it on the schematic next to the wire, and exports the same data
to a CSV for the wire label printer, one row per wire end. It is re-runnable: after edits it updates existing identifiers
in place, with no duplicates and no stale text. Native wire numbering is left alone: the node number stays in `WIRENO`
and the to/from text is its own label layer.

AutoCAD Electrical cannot do this natively (one wire number per network, no tag or pin token in the wire-number format),
but it already computes the answer: the Wire From/To report exposes `WIRENO, CMP1, PIN1, CMP2, PIN2` plus the entity
handles of the wire and both components. LSE_ToFrom reuses that data instead of tracing wires itself. It reads it either
from the project's scratch database (read-only, through ADODB) or from a Wire From/To report saved to a file.

Built to the work scope `..\Agent Brief True To From Wire Numbers in AutoCAD Electrical 2026.docx` (2026-11-12), on the
scope's defaults: keep the node number and show to/from as its own label; one string per wire on the schematic, reversed
at the far end on printed labels; `:` and `/` delimiters; no location prefix; `?` for a missing pin plus a line in the
validation report; terminal blocks are endpoints; internal wires only; C# DLL loaded with NETLOAD.

**Status (2026-11-13, build `2026-11-13-r1`):** compiles in Release with no warnings against the AutoCAD.NET 25.1.0
reference package; the smoke test in `tests\LSE_ToFrom.SmokeTest` (109 checks: config, project file, identifier
composition, report import, planning against fake drawings, insert/update/keep/delete by key, run twice unchanged, retag,
stale and duplicate labels, locked and open drawings, current-drawing scope, layer conflicts, CSV layout and quoting,
rollback, read-only refusal, stale-database detection, the plan grid on a real `DataGridView`, build tag, database search
folders, settings upgrade) passes; the plugin has **not** been loaded into AutoCAD Electrical. The scratch-database table
that holds the from/to rows is undocumented, so `database.wireSql` in the config is **empty** until `LSETOFROMDISCOVER`
has been run once on a workstation (Discovery phase of the work plan). Until then use the report-file data source.

r1 (2026-11-13) prepares the first run in AutoCAD: every window title ends with the build tag `[2026-11-13-r1]` (the error
dialog, the error log and `LSETOFROMDIAG` name it too, so a stale DLL is obvious); the Apply? boxes in the plan grid are
toggled by the form itself on a click or the space bar (`src\GridService.cs`, the same cure that fixed the dead boxes in
LSE_Doctor and LSE_Env) and rows that cannot be written say **note**; the LISP bridge tries the command line and then
`acedEvaluateLisp`, and the default expressions try several candidate functions each (`ace_getactiveproject`,
`wd_proj_wdp_fnam`, `GBL_wd_prj`; `wd_mdb_get_proj_scratch_dbnam` with and without `c:`); `LSETOFROMDIAG` and
`LSETOFROMDISCOVER` run a live probe of that bridge and print what each function returned; the scratch database is also
looked for in `database.searchFolders` (`{FOLDER}`, `{TEMP}`, `{LOCALAPPDATA}\Temp`); and `LSETOFROM` no longer plans on
opening when the data source is not ready (no query yet, or no report file picked), it tells you what to do instead.
r1 also closes the suspected bugs found in the 2026-11-13 code read (`..\KNOWN-ISSUES.md`, Wire To/From section): the
plan is dropped when Annotate, Current drawing only or the data source changes after Plan; Export CSV re-evaluates the
Apply button; Tick recommended leaves stale-label deletions to you; the sheet number reaches the label CSV; a failed run
puts back the previous output files and is appended to the project report; a re-placed label no longer creeps along the
wire; discovery writes the query without stripping the config's comments; `reportFile.delimiter` accepts `tab`; the
provider fallback keeps Jet 4; the assembly carries version 1.1.0.

**Status (2026-11-13):** source unchanged since 2026-11-12. LSE_ToFrom is **not in any release package yet**: the
2026-11-12-r7 cut in `..\releases\` carries the other six plugins only, so it has to be built from this folder. Unlike
LSE_Env and LSE_Doctor it has no `VaultProbe`, no build tag in its window titles and no explicit version number (see
Versioning below); a Vault-locked closed drawing is caught only by the read-only check before the backup.

**Who uses it:** LSE electrical designers finishing a schematic set, and whoever prints the wire labels for the shop.

## Commands

| Command | Purpose |
|---|---|
| `LSETOFROM` | The dialog: pick the project and data source, Plan, review the grid of label changes (insert, update, keep, delete stale, skip with reason), tick, Apply. Writes the labels and the CSVs. Help button and tooltips |
| `LSETOFROMEXPORT` | No planning dialog: read the data for the active project (data source and report file as last saved in the `LSETOFROM` window), write the label CSV, the report CSV, the validation report and the run log, then show the run log and the validation text. Drawings are not read or written |
| `LSETOFROMVALIDATE` | No planning dialog, nothing written to drawings: read data and drawings (whole project, or the current drawing when "Current drawing only by default" is on in settings), write `<project>_tofrom_validation.txt` and show it with the plan text |
| `LSETOFROMDISCOVER` | Discovery: probe the LISP bridge, list every path looked at for the project database, list every table and column of the database, mark the table that carries the from/to columns, show its first rows, offer to write the `SELECT` into the config, save a findings note |
| `LSETOFROMBLOCKS` | Write `TOFROM_LBL_H.dwg` and `TOFROM_LBL_V.dwg` to a folder, and add the label layer and both block definitions to the current drawing (run once inside the department template `.dwt`) |
| `LSETOFROMSETTINGS` | Config path, data source, stale-database policy, backups, the three LISP bridges, defaults |
| `LSETOFROMDIAG` | Build tag, runtime, settings, a live probe of the LISP bridge (which evaluation path works, what the active-project and scratch-database functions returned), config summary, ADODB and provider availability, the unverified list |

The commands that work on a project (`EXPORT`, `VALIDATE`, `DISCOVER`) take the active project, or the only `.wdp` in
the current drawing's folder; with neither they stop and say so. `LSETOFROM` lets you browse instead.

## The LSETOFROM window in one table

Every option is read at a fixed moment. The tick boxes in the header are read when **Plan** runs, except Export CSV,
which is read when **Apply ticked** runs. Change a header tick box after planning and press Plan again, or the change
has no effect. Full walkthrough: [docs/OPERATIONS.md](docs/OPERATIONS.md) section 3.

| Control | Default | What it does | Read when |
|---|---|---|---|
| Project (.wdp), Browse..., Active project | the active project | the project to work on; Browse and Active project plan at once | Plan |
| Project database / Report file: | from settings | where the from/to rows come from; the choice and file are saved to settings and reused by `LSETOFROMEXPORT` and `LSETOFROMVALIDATE` | Plan |
| Annotate drawings (place and update labels) | ticked every time | ticked: every drawing is read and label changes are listed. Unticked: no drawing is read, the grid stays empty and Apply only writes files | Plan |
| Current drawing only | the settings default, and only when a saved drawing was active when the command started (greyed out otherwise) | limits the drawings read and written to that one drawing; the CSVs still cover the whole project | Plan |
| Export CSV | ticked | writes the label CSV and the report CSV; the validation report is written either way | Apply |
| Apply? (grid column) | ticked for every writable change | only ticked rows are written | Apply |
| Tick recommended / Tick all / Untick all | | set the Apply? ticks; recommended and all give the same result today | at once |
| Show unchanged | unticked | also lists Keep rows and Skip rows (they cannot be ticked) | at once |
| Plan text..., Validation... | | the plan or validation report as text, with Save as | at once |

## What a run does

1. **Data.** Rows `WIRENO, CMP1, PIN1, CMP2, PIN2, WIREHDL1, WIREHDL2, CMPHDL1, CMPHDL2, DWGIX1, DWGIX2` come from the
   project database (`database.wireSql`) or a saved report. `CMP1:PIN1` is the From end, `CMP2:PIN2` the To end, as the
   report's wire sequence decides. Each row's drawings are resolved from `DWGIX` (position in the `.wdp` list, or
   `database.drawingSql`) or a drawing-name column. The database file time is compared with every drawing's save time;
   drawings saved later are listed as `StaleDatabase` (policy warn, stop or ignore).
2. **Identifier.** `format` (`{FROM}/{TO}` with `tagPinFormat` `{TAG}:{PIN}`) builds the string; a blank pin or tag
   shows `?` and is listed. The label CSV uses `farEndFormat` (`{TO}/{FROM}`) for end 2. `swapEnds` flips a project
   that reads backwards.
3. **Plan.** For every drawing in the project (or the current one) the existing labels are read by their hidden key
   `TFKEY` = `<wire handle>/<end>`. Same text: keep. Different: update in place. Missing: insert. Key with no row: delete
   (stale). Two labels with one key: the extra is deleted. Drawings that are missing, locked by another user (`.dwl`) or
   whose `WIRELAYS`/`WIRENO_LAY` would swallow the label layer are skipped and reported, never failed on.
4. **Apply** (`RunService.Execute`): refuse read-only closed drawings; back up every closed drawing that will change to
   `<project>\_LSE_TOFROM_BACKUP\<stamp>\`; write each drawing (closed: side database, saved in place; open in the
   editor: through its Document under `LockDocument`, not saved, the engineer saves); re-read every written drawing and
   verify each change; write `<project>\LSE_ToFrom\<project>_tofrom_labels.csv` (one row per wire end), `..._report.csv`
   (native columns, one row per wire) and `..._validation.txt`; append `<project>\LSE_ToFromReport.txt`; prune backups.
   Any exception restores the backups, deletes the CSV and validation files this run wrote (including a same-named
   file left by an earlier run, which is not restored), writes the log to
   `%APPDATA%\LSE\LSE_ToFrom\Logs\ToFromRun_<stamp>.txt` and shows it. A failed run is not appended to
   `LSE_ToFromReport.txt`.
5. **Label placement** (`LabelWriter`): the wire entity is found by handle, the end nearest the component block (by
   `CMPHDL`) is the wire end, the label goes `endOffset` along the wire and `sideOffset` beside it, horizontal or
   vertical block by wire direction, stepped along the wire when another label is within `collisionDistance`. The two
   block definitions are imported from `blockFolder`/the DLL folder when the DWGs exist there, otherwise created in the
   drawing (attributes `TOFROM` visible, `WIRENO` hidden unless `showNode`, `TFKEY` hidden).

Any other error in a form or command shows a three-part dialog (What happened / Where / What to do) and writes the full
exception to `%APPDATA%\LSE\LSE_ToFrom\Logs\Error_<stamp>.txt`.

## Validation report

| Kind | Meaning |
|---|---|
| MissingPin | a connection without a terminal number; the label shows `?` |
| MissingTag | a component without a tag; the label shows `?` |
| WireNotFound | no wire handle or no resolvable drawing for an end, or the handle is not in the drawing; no label |
| DrawingSkipped | drawing missing, locked, unreadable; its labels were not written |
| DrawingUnresolved | a `DWGIX` or drawing name that is not in the project's list |
| StaleLabel | a label whose wire is not in the data (deleted when `deleteStale`) |
| DuplicateKey | two rows name the same wire end; both are labelled, check the wire sequence |
| StaleDatabase | a drawing saved after the database was written |
| LayerConflict | the label layer would be a wire or wire-number layer in that drawing; the drawing was skipped |

## Configuration

- `LSETOFROMSETTINGS` (per user, `%APPDATA%\LSE\LSE_ToFrom\settings.json`): config path, data source (`scratch` or
  `report`), stale policy, backup folder and count, `ActiveProjectLisp`, `ScratchDbLisp`, `RefreshDbLisp` (blank by
  default: refresh the database by running a native report, or set the function once Discovery names it), show report,
  current drawing only.
- `config\tofrom.config.json` (shared, copied next to the DLL): identifier format and delimiters, missing-pin and
  missing-tag text, location prefix, schematic placement (label at from/to/both, layer, block names, attributes, text
  height, offsets, collision, reposition on re-run, delete stale), CSV (delimiter, encoding, reverse at far end, output
  folder and names), database (path pattern, search folders, providers, `wireSql`, `drawingSql`, index base, column map,
  candidate columns), report-file column map. Every key is explained in the file. When `LSETOFROMDISCOVER` saves the query it
  rewrites the whole file: the comments are lost and the keys come back in PascalCase (still read correctly). Keep a
  commented copy.

## Build

- Target `net8.0-windows`, WinForms, x64, nullable. No NuGet packages at runtime (ADODB through COM `dynamic`).
- With AutoCAD Electrical 2026 installed: `dotnet build -c Release` (references `accoremgd/acdbmgd/acmgd` from
  `C:\Program Files\Autodesk\AutoCAD 2026`, or pass `-p:AcadDir=...`).
- Without an install the project compiles against the `AutoCAD.NET` 25.1.0 reference package (compile-time only).
- Smoke test: build the plugin in Release first (the test references `bin\Release\LSE_ToFrom.dll`), then
  `dotnet run -c Release --project tests\LSE_ToFrom.SmokeTest -- <absolute path to acade-wire-tofrom>`. It works in a
  temp folder, needs no AutoCAD, and prints `PASS`/`FAIL` per check and `73 passed, 0 failed` at the end (exit code 1
  on any failure). It also writes `Error_*.txt` and `ToFromRun_*.txt` into the real `%APPDATA%\LSE\LSE_ToFrom\Logs\`.

## Versioning

| Where | Value |
|---|---|
| `LSE_ToFrom.csproj` | no `Version` property, so the assembly version is the SDK default `1.0.0.0` |
| `bundle\LSE_ToFrom.bundle\PackageContents.xml` | `AppVersion="1.0.0"`, `SeriesMin`/`SeriesMax` `R25.1` (AutoCAD 2026) |
| `LSETOFROMDIAG` | prints the assembly version (`1.0.0.0`) and the .NET runtime |

There is no `BuildInfo` build tag as in LSE_Env and LSE_Doctor, so two builds cannot be told apart from inside AutoCAD.
When the plugin joins a release, add a version to the `.csproj` and a build tag before shipping.

## Deploy / load

1. Build Release; copy the output folder (DLL, `tofrom.config.json`) to a trusted location on each seat (unblock a
   downloaded zip first). `NETLOAD` the DLL, or copy `bundle\LSE_ToFrom.bundle` to `%APPDATA%\Autodesk\ApplicationPlugins\`
   (one user) or `C:\ProgramData\Autodesk\ApplicationPlugins\` (all users) with the build output in `Contents\`; the
   manifest loads the DLL on the first `LSETOFROM...` command, not at start-up. Keep the config somewhere writable if
   `LSETOFROMDISCOVER` is to save into it (point `Config JSON` in settings at it).
2. Run `LSETOFROMDIAG`: the first line must say `Build: 2026-11-13-r1` (an older tag means an old DLL is still loaded;
   close AutoCAD Electrical completely before replacing a DLL). ADODB must be available and at least one ACE provider
   registered. The "LISP bridge" block says whether the command-line or the `acedEvaluateLisp` path works and what the
   active-project and scratch-database functions returned; both blank means you browse to the `.wdp` in `LSETOFROM` and the
   database is found through `database.pathPattern` and `database.searchFolders`.
3. **Discovery (once per release of AutoCAD Electrical):** open a test project, run a native Wire From/To report once so the
   scratch database exists and is current, then `LSETOFROMDISCOVER`. Accept the suggested `SELECT` if the candidate
   table shows the right columns; otherwise edit `database.columns` to the names listed and run it again. Save the native
   report to a CSV as ground truth. If the report says the database is MISSING, it lists every path it looked at: add the
   folder the scratch-database LISP returned to `database.searchFolders`, or set `database.pathPattern`.
4. `LSETOFROMBLOCKS`: write the two block DWGs next to the DLL, and add layer and blocks to the department template.
5. On a **copy** of a real project: `LSETOFROMVALIDATE`, read the report, then `LSETOFROM`, Plan, Apply. Compare
   `<project>_tofrom_report.csv` with the native report row for row, then work through the ten acceptance tests in
   `docs\OPERATIONS.md`.

## Not verified on a workstation

- The scratch database: its file name (`{FOLDER}\{PROJECT}.mdb` by default, or whatever `c:wd_mdb_get_proj_scratch_dbnam`
  returns), the table that holds the from/to rows, its column names, that `WIREHDL1/2` are entity handles of the wire
  segment touching each component and `CMPHDL1/2` the component blocks, and that `DWGIX` counts from 1 in `.wdp` order.
- The LISP entry points (`ace_getactiveproject`, `wd_proj_wdp_fnam`, `GBL_wd_prj`, `wd_mdb_get_proj_scratch_dbnam`) and
  which of the two evaluation paths (`Editor.Command`, `acedEvaluateLisp` through `accore.dll`) works in 2026; and how
  the database is refreshed from code (no refresh function is configured; running a native report refreshes it).
- That the plan grid's Apply? boxes respond to the first click inside AutoCAD's modal dialog host. r1 handles the click
  itself (`CellClick`, space bar) and paints `note` on rows that cannot be written; proven on a real `DataGridView`
  outside AutoCAD, not yet inside it.
- Everything written through the AutoCAD API: side databases, `LockDocument` on open drawings, block definition
  creation, `Database.Wblock` export, attribute alignment, that `.dwl` files mark a drawing as locked.
- The default offsets and text height (inch-based drawings assumed).
- The native Wire From/To report's saved-file layout (header row and field names) that `ReportImport` expects.

## Documentation

| Document | Read it for |
|---|---|
| this README | overview, commands, the main window's options, what a run does, validation kinds, configuration, build, versioning, deploy, what is not verified |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | components, dependency diagram, data flow, the run transaction, placement rules, design decisions, the work scope's decisions |
| [docs/CODE-REFERENCE.md](docs/CODE-REFERENCE.md) | every file, type and public member, the seven commands |
| [docs/DATA-STORES.md](docs/DATA-STORES.md) | settings.json, tofrom.config.json, the label block, the CSV layouts, the validation report, backups and logs, the scratch database |
| [docs/OPERATIONS.md](docs/OPERATIONS.md) | install, discovery, first run, every control and tick box, the settings screen field by field, the ten acceptance tests, reading the log, messages, recovery by hand |
| [docs/DEVELOPER-GUIDE.md](docs/DEVELOPER-GUIDE.md) | build with or without AutoCAD, the smoke test, conventions, recipes, first-build checklist, known gaps and suspected bugs |
| [docs/GLOSSARY.md](docs/GLOSSARY.md) | terms and file extensions |
