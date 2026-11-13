# Operations guide: LSE_ToFrom

Maintained by DCW. Last reviewed 2026-11-13.

How to install, discover the database once, run the tool, prove it with the ten acceptance tests, read what it leaves
behind, and recover by hand. Section 3 explains every control and tick box of the main window, and the last section every field
of the settings screen (section 9), including when each one takes effect.

## 1. Install

1. Unblock the zip (file Properties > Unblock) before extracting. Put the Release output (`LSE_ToFrom.dll`,
   `tofrom.config.json`) in a folder that is a trusted location (OPTIONS > Files > Trusted Locations) on each seat.
2. `NETLOAD` the DLL for the session, or copy `bundle\LSE_ToFrom.bundle` to `%APPDATA%\Autodesk\ApplicationPlugins\`
   with the output in `Contents\` so the commands load on first use.
3. `LSETOFROMDIAG`: confirm ADODB is available and at least one ACE provider is registered. If none is, install the
   64-bit Microsoft Access Database Engine.
4. `LSETOFROMSETTINGS`: point `Config JSON` at the department's shared copy if there is one.

## 2. Discovery (once per AutoCAD Electrical release)

1. Open a test project (a copy). Run AEWIRE and AEWIRENO if needed, then Reports > Schematic > Wire From/To, so the
   project database is created and current. Save that report to a CSV with the fields WIRENO, CMP1, PIN1, CMP2, PIN2,
   WIREHDL1, WIREHDL2, CMPHDL1, CMPHDL2, DWGIX1, DWGIX2 as ground truth, and save the format as a `.set` file.
2. `LSETOFROMDISCOVER`. Read the list: the table marked CANDIDATE with the highest score is the from/to table. Check its
   first rows look like the report. Answer Yes to write the SELECT into the config. If no candidate appears, compare the
   column names in the list with `database.columns` and `database.candidateColumns` in the config, edit them, and run
   discovery again.
3. Read the "LISP bridge" lines at the top: what the scratch-database function returned, and "Paths looked at", which
   lists `database.pathPattern` and `<project>.mdb` in every `database.searchFolders` entry. If the database is reported
   MISSING, run the native report again with the project active, then add the folder the LISP returned (or the folder
   you find `<project>.mdb` in) to `database.searchFolders` in the config, and run discovery again.
4. The findings note is saved in `%APPDATA%\LSE\LSE_ToFrom\Logs\Discovery_<stamp>.txt`. Keep it with the release notes.
5. `LSETOFROMBLOCKS`: a folder dialog opens on the first block folder (`schematic.blockFolder`, else the DLL folder);
   OK writes the two block DWGs there, Cancel skips that step. A second question offers to add the layer and both block
   definitions to the drawing that is open. Open the department template `.dwt`, run it, cancel the folder dialog if the
   DWGs already exist, answer Yes, then save the template.

If discovery cannot save (the config sits in a write-protected folder such as `C:\ProgramData\Autodesk\ApplicationPlugins`),
copy the config somewhere writable, point `Config JSON` in `LSETOFROMSETTINGS` at it, and run discovery again. Saving
rewrites the file without its comments.

Until discovery is done, pick **Report file** as the data source in `LSETOFROM` and browse to the saved report.

## 3. Run (LSETOFROM)

### 3.1 Walkthrough

1. Open the project in the Project Manager (or any of its drawings). Run `LSETOFROM`. Before the window opens the
   command asks AutoCAD Electrical for the active project and the scratch database path, and runs the refresh LISP if
   one is set in settings.
2. Project: the active one is filled in (or the only `.wdp` next to the current drawing). If it exists, the window plans
   it straight away. Data source: Project database, or Report file with the saved CSV.
3. Tick boxes: Annotate drawings (on), Current drawing only (as in settings), Export CSV (on). See 3.2 for what each does.
4. Plan. Read the status line: wires, labels wanted, changes, issues, and a stale-database warning if any drawing was
   saved after the database. If the warning shows, close the dialog, run a native report, and run `LSETOFROM` again
   (the refresh LISP only runs when the command starts, not on each Plan).
5. Read the grid. Insert, Update and Delete rows are ticked. Keep and Skip rows are hidden until you tick Show
   unchanged; they are grey and cannot be ticked. A skipped drawing has one orange row with the reason. Validation...
   shows the full list of issues; Plan text... shows the plan as text.
6. Apply ticked. Confirm. Closed drawings are backed up and saved; drawings open in AutoCAD are changed in the session
   and must be saved by you. The result window lists everything written and verified (when "Show the run report after
   each run" is on). The window then plans again, so the grid shows what is left.
7. Open a sheet: the labels sit on layer `LSE_TOFROM` beside the From end of each wire.

`LSETOFROMEXPORT` loads the data and writes the CSVs, the validation report and the run log without the planning
dialog and without reading or touching drawings; it always shows the run log and validation text at the end.
`LSETOFROMVALIDATE` reads data and drawings but writes only the validation report. Both use the data source and report
file saved by the last Plan in `LSETOFROM`.

### 3.2 Every control in the window

**When things take effect.** The project box, the data-source choice and the Annotate and Current drawing only tick
boxes are read only when the plan is built (Plan, Browse..., Active project, or after Apply). Changing them afterwards
changes nothing until you press Plan again: the grid, and what Apply ticked writes, still come from the old plan. Export
CSV is read when you press Apply ticked. Show unchanged and the three tick buttons act at once.

| Control | Default | Effect |
|---|---|---|
| Project (.wdp) | active project, else the single `.wdp` next to the current drawing | the project to plan. The list holds the last 25 projects run |
| Browse... | | pick a `.wdp`; plans it at once |
| Active project | | puts the active project in the box and plans; says so when AutoCAD Electrical reported none |
| Project database (radio) | as last used (settings `DataSource`) | read the from/to rows from the scratch database. Needs `database.wireSql`, filled by `LSETOFROMDISCOVER`; until then Plan stops with a message pointing at discovery |
| Report file: (radio), box, Browse... | as last used | read the rows from a saved Wire From/To report. Browse ticks the radio for you; typing a path does not. The radio and the path are saved in settings at every Plan |
| info label (right of Browse...) | | shows the label end and layer; hover for the config path, format and the refresh-LISP result |
| Annotate drawings (place and update labels) | ticked on every opening (not remembered) | Ticked: every non-reference drawing of the project is read, and each wanted label becomes Insert, Update, Keep, Delete or Skip in the grid. Unticked: no drawing is read, the grid is empty, and Apply ticked writes only the files (CSVs if Export CSV is ticked, the validation report, the run log). Untick it for a CSV-only run |
| Current drawing only | settings "Current drawing only by default"; greyed out when no saved drawing was active when the command started | Only that drawing is read and written. The CSVs still cover the whole project, because they come from the data, not the drawings. It only matters while Annotate drawings is ticked |
| Export CSV | ticked | Ticked: Apply ticked writes `<project>_tofrom_labels.csv` and `<project>_tofrom_report.csv`. Unticked: no CSV. The validation report is written either way. With nothing ticked in the grid, Export CSV alone keeps Apply ticked usable for a CSV-only run |
| Plan | | reads the project, the data and (when annotating) the drawings; nothing is written |
| Apply? (grid column) | ticked for Insert, Update, Delete | untick a row to leave that change out. Keep, Skip and skipped-drawing rows cannot be ticked |
| Tick recommended | | ticks inserts, updates and the deletion of duplicate or stale labels. Stale labels are only listed as Delete when `deleteStale` is on, so in practice this equals Tick all |
| Tick all / Untick all | | ticks every writable change / clears every tick |
| Show unchanged | unticked | also lists Keep rows (label already correct) and Skip rows (cannot be written: drawing locked or missing, or a stale label kept because `deleteStale` is off) |
| summary (bold) | | ticked changes of the writable total, drawings with work, validation issues |
| Plan text... | | the plan as text: log, per-drawing changes, validation lines; Save as... writes it to a `.txt` |
| Validation... | | the validation report as it would be written now |
| Apply ticked | greyed out until a plan exists with a tick or with Export CSV ticked | confirms, then runs the transaction (DATA-STORES and ARCHITECTURE section 4). After a run it plans again |
| Help | | the built-in guide to the window and the config |
| Close / Esc | | closes; nothing is written |

Grid columns: Apply?, Drawing, Change (Insert label, Update label, Delete stale label, Keep, Skip, Drawing skipped),
Wire no, Label (the text to write), Drawing has (the text the drawing holds now), Key (`<wire handle>/<end>`), Note
(why: "was ...", stale, duplicate, "open in the editor: written in the session, not saved", skip reason).

Known quirk: the Apply ticked button only re-checks whether it should be enabled when a grid tick changes or a plan is
built. If you untick Export CSV with nothing ticked, the button stays enabled and pressing it says there is nothing to
do; if you tick Export CSV on an empty grid, press Plan to enable the button.

## 4. The ten acceptance tests

Run them on a **copy** of a real department project after discovery. Each line says how to check.

| # | Case | How to check |
|---|---|---|
| 1 | Two-point wire from K1 pin 13 to TB2 terminal 1 | the label reads `K1:13/TB2:1`; the labels CSV has end 1 `K1:13/TB2:1` and end 2 `TB2:1/K1:13` |
| 2 | Network with three connections | two labels, one per physical wire, each naming its own two ends in wire-sequence order; the report CSV has two rows with one WIRENO |
| 3 | Wire sequence edited, then re-run | the same two keys are listed as Update with the new text; no Insert, no Delete |
| 4 | K1 retagged to K5, then re-run | every label that named K1 is an Update; afterwards `_tofrom_labels.csv` contains no `K1:` |
| 5 | Component with a blank pin | label shows `?`; validation has a MissingPin line naming the component |
| 6 | Wire across sheets through source and destination arrows | the label names the real end devices; DWGIX1 and DWGIX2 differ in the report CSV; with `labelAt=both` the second label lands on the other sheet |
| 7 | Multi-level terminal block | the terminal number of the correct level at each end (compare with the native report row) |
| 8 | Command run twice with no edits | second Plan shows 0 changes to write and only Keep rows; drawing file times unchanged |
| 9 | CSV export | `_tofrom_report.csv` equals the native report saved in step 2.1 row for row (sort both by WIRENO, CMP1, PIN1) |
| 10 | Native commands after the tool has run | AEWIRENO, the schematic reports and the terminal strip editor run without errors and without new rows; the label layer is not in WIRELAYS |

Also time the full run on the department's largest project and note it in the release notes.

## 5. What a successful run leaves behind

| Item | Where |
|---|---|
| labels | layer `LSE_TOFROM`, blocks `TOFROM_LBL_H` / `TOFROM_LBL_V`, one per wire end labelled |
| label CSV, report CSV, validation report | `<project>\LSE_ToFrom\` |
| run log | `<project>\LSE_ToFromReport.txt` (appended) |
| backups | `<project>\_LSE_TOFROM_BACKUP\<stamp>\` (closed drawings that changed) |
| settings | recent project remembered |

## 6. Reading the log

```
Started: 2026-11-12T15:02:11 by <user>@<machine>
Project: C:\...\301144-9.wdp
Data source: project database C:\...\301144-9.mdb
Rows: 412  |  drawings to write: 9  |  labels ticked: 418
Backed up 301144-9-E003.dwg to C:\...\_LSE_TOFROM_BACKUP\20261112_150211
Open in the editor: 301144-9-E001.dwg. Labels are written into the open session ...
Writing 301144-9-E001.dwg: 44 insert, 0 update, 0 delete
  Created layer LSE_TOFROM
  Label block TOFROM_LBL_H imported from C:\...\TOFROM_LBL_H.dwg
  Labels: 44 inserted, 0 updated, 0 deleted, 0 skipped (layer LSE_TOFROM)
  Labels written in the open editor session; save the drawing to keep them: ...
Verifying
Label CSV: ...\301144-9_tofrom_labels.csv (824 rows)
Report CSV: ...\301144-9_tofrom_report.csv (412 rows)
Validation report: ...\301144-9_tofrom_validation.txt (7 issue(s))
Status: SUCCESS (418 label change(s) written and verified, 7 validation issue(s))
```

`UNRESOLVED` lines name a change that could not be confirmed by re-reading the drawing: open the drawing and look at the
key named. `Status: PARTIAL` means at least one such line.

## 7. Messages and what to do

| Message | Meaning | What to do |
|---|---|---|
| database.wireSql is empty ... Run LSETOFROMDISCOVER | discovery not done | section 2, or use the report-file source |
| The database query is not set yet (status line when `LSETOFROM` opens) | discovery not done; the window does not plan by itself | pick Report file and browse to a saved report, or run `LSETOFROMDISCOVER` |
| Project database was not found | no `.mdb` at the pattern path, in any search folder, and the LISP returned nothing | run a native report once; read "Paths looked at" in `LSETOFROMDISCOVER`; add the folder to `database.searchFolders` |
| A window title shows an older tag than `[2026-11-13-r1]` | the previous DLL is still loaded | close AutoCAD Electrical completely, replace the DLL, start again |
| could not be opened read-only with any configured provider | no 64-bit Access engine | install it; `LSETOFROMDIAG` shows providers |
| The query result has no column for Cmp1 (CMP1) ... | wrong table or column names | fix `database.columns` or `wireSql` from the discovery list |
| No header row with the columns CMP1 and WIREHDL1 | the saved report lacks a header or the fields | re-save the report with the fields in section 2.1 |
| drawings were saved after the project database was last written | stale data | run a native report, run again; or set stalePolicy |
| label layer LSE_TOFROM is this drawing's wire-number layer / matches the wire layer pattern | config conflict | change `schematic.labelLayer` |
| locked by another user or session | `.dwl` lock | run again when the drawing is free; other drawings were done |
| Read-only drawing | Vault or attribute | check out or clear read-only; nothing was changed |
| wire handle ... is not in this drawing | the data names a handle the drawing no longer has | refresh the database and run again |
| Every label is already up to date. Nothing to write. | Apply pressed with nothing ticked and Export CSV off | none, or tick Export CSV |
| The data source returned no from/to rows ... / No wire end could be labelled ... / Every drawing was skipped ... | the other three nothing-to-do cases | draw and number wires and refresh the data; read the validation list |
| Data source is 'report file' but no file is chosen | Report file selected with an empty box | browse to the saved report |
| No active AutoCAD Electrical project was detected and no single .wdp sits next to the current drawing | `EXPORT`, `VALIDATE` or `DISCOVER` without a project | activate the project, or use `LSETOFROM` and browse |
| database.wireSql must be a SELECT statement / schematic.labelAt must be from, to or both / ... is not a valid AutoCAD layer name | the config failed validation when loaded (the window disables Plan) | fix the named key in `tofrom.config.json` |
| Label changes are ticked but no drawing writer is available (outside AutoCAD) | only seen in tests | none |
| Status: PARTIAL | at least one written change could not be confirmed on re-read | open the drawing at the UNRESOLVED key; run Plan again |

Every unexpected exception shows What happened / Where / What to do and writes `Logs\Error_<stamp>.txt`.

## 8. Recovering by hand

- To undo a run on closed drawings: copy the drawings from `_LSE_TOFROM_BACKUP\<stamp>\` over the project's. The
  stamp is in the run log.
- To undo in an open drawing: close it without saving.
- To remove every label: erase all blocks named `TOFROM_LBL_*` (QSELECT by block name) or freeze/purge the layer. The
  next run re-inserts them.
- To start over with a new key format: remove the labels as above first, or every old label becomes stale.
- After a failed run: the drawings were restored from the backup, but the CSV and validation files of that run were
  deleted, and a same-named file from an earlier run is gone too (it was overwritten before the failure). Run again, or
  `LSETOFROMEXPORT`, to regenerate them. The failed run's log is in `%APPDATA%\LSE\LSE_ToFrom\Logs\ToFromRun_<stamp>.txt`,
  not in the project's `LSE_ToFromReport.txt`.

## 9. The settings screen (LSETOFROMSETTINGS)

Per user, saved to `%APPDATA%\LSE\LSE_ToFrom\settings.json` when you press Save. Save checks the numbers and the folder
name, and loads the config file (when it exists) to catch errors before you leave.

| Field | Default | Effect |
|---|---|---|
| Config JSON (format, layer, blocks, queries), Browse | `tofrom.config.json` next to the DLL | the shared config every command loads. A missing file means built-in defaults (`LSETOFROMDIAG` says so) |
| Data source | scratch | the starting radio in `LSETOFROM`, and the source used by `LSETOFROMEXPORT` and `LSETOFROMVALIDATE`. `LSETOFROM` overwrites it at every Plan |
| When drawings are newer than the database | warn | warn: list each newer drawing as `StaleDatabase` and continue. stop: refuse to plan until the database is refreshed. ignore: no check. Applies to the project-database source only |
| Backup folder name (inside the project) | `_LSE_TOFROM_BACKUP` | must be a valid folder name |
| Backups to keep per project | 10 | whole number of 1 or more; older stamp folders are deleted after each successful run |
| Active project LISP (sets USERS1) | `ace_getactiveproject` expression | how the active project is found. Unverified on a 2026 seat |
| Scratch database path LISP (sets USERS1) | `c:wd_mdb_get_proj_scratch_dbnam` expression | how the scratch database path is found; the result is used only when that file exists, else `database.pathPattern`. Unverified |
| Refresh project database LISP (run before each read; blank = none) | blank | runs when a command starts (`LSETOFROM` once, before the window; the other commands before reading), not on every Plan |
| Show the run report after each run (tick box) | ticked | `LSETOFROM` only: after Apply ticked, opens the run log window. Unticked: only the status line reports the result; the log is still written to `LSE_ToFromReport.txt`. `LSETOFROMEXPORT` always shows its report |
| Current drawing only by default (instead of the whole project) (tick box) | unticked | the starting state of the Current drawing only tick box in `LSETOFROM` (still greyed out when no saved drawing is active), and the scope of `LSETOFROMVALIDATE`. No effect on `LSETOFROMEXPORT`, which never reads drawings |
| Diagnostics | | the `LSETOFROMDIAG` report |
| Open config | | opens the config path in Notepad |
| Save | | validates and saves, then closes. Closing the window any other way discards the edits |

The report file last picked and the recent-project list are also in `settings.json` but have no field here.
