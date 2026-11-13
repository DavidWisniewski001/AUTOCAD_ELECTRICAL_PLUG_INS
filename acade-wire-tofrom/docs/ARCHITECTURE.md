# Architecture: LSE_ToFrom (AutoCAD Electrical 2026 plugin)

Maintained by DCW. Last reviewed 2026-11-13.

LSE_ToFrom gives every physical wire a To/From identifier (`K1:13/TB2:1`), shows it on the schematic as a managed label
block, and exports it for the label printer. It builds on AutoCAD Electrical's own from/to data and never changes native
wire numbering. This document explains the pieces, how data flows through them, the one write transaction, and why the
design is what it is.

## 1. The problem

AutoCAD Electrical numbers networks, not wires: a network with three connections is several physical wires sharing one
`WIRENO`. The wire-number format has `%N` and sheet/line tokens but nothing for the connected component tag or pin. A
per-wire to/from identifier therefore cannot live in the wire number. The Wire From/To report, however, already lists
every physical wire with `WIRENO, CMP1, PIN1, CMP2, PIN2` and the entity handles of the wire segment and the components at
each end, in the order given by the network's wire sequence. The plugin turns those rows into labels and a CSV.

## 2. Components

| File | Role | AutoCAD API | Density |
|---|---|---|---|
| `src/Commands.cs` | `Plugin` (extension application) and the seven `[CommandMethod]`s | yes (dialogs, interop) | compact |
| `src/Models.cs` | settings, config, rows, ends, labels, plans, issues, results | no | compact |
| `src/SettingsService.cs` | `SettingsService` (`settings.json`), `ConfigService` (load, validate, save `tofrom.config.json`) | no | compact |
| `src/ProjectService.cs` | reads the `.wdp` drawing list, resolves paths, database path, output file names | no | compact |
| `src/ScratchDb.cs` | every scratch-database query: schema, SELECT, row mapping, discovery text. ADODB via COM `dynamic`, read-only | no (COM) | compact |
| `src/ReportImport.cs` | reads a saved Wire From/To report (CSV, tab or semicolon) into rows | no | compact |
| `src/DataSourceService.cs` | chooses the source, loads rows, resolves drawings, stale-database check | no (COM through ScratchDb) | compact |
| `src/IdentifierService.cs` | the pure engine: compose identifiers, make ends, plan per-drawing changes by key, validation issues, render | no | compact |
| `src/Export.cs` | label CSV, report CSV, validation report, CSV quoting and encodings | no | compact |
| `src/RunService.cs` | the run transaction: backup, write, verify, export, log, prune, rollback | no (through the bridge) | compact |
| `src/LabelWriter.cs` | reads existing labels and `WD_M` layer settings; places, updates and deletes labels; block definitions; DWG export | yes | expanded |
| `src/AcadeInterop.cs` | LISP bridges (active project, scratch database path, refresh) through USERS1 by two evaluation paths (`Editor.Command`, `acedEvaluateLisp`); the live probe; the host bridge | yes (+ P/Invoke into `accore.dll`) | compact |
| `src/BuildInfo.cs` | the release tag shown in every title, the error dialog and log, and `LSETOFROMDIAG` | no | compact |
| `src/GridService.cs` | the plan grid: columns, fill, click/space toggling, `note` rows, painting | WinForms | expanded |
| `src/ToFromForm.cs` | the main dialog | WinForms | expanded |
| `src/ToFromSettingsForm.cs`, `src/TextReportForm.cs` | settings and text report dialogs | WinForms | compact |
| `src/DiagnosticsService.cs`, `src/ErrorService.cs`, `src/HelpText.cs` | diagnostics text, three-part error dialog with log, help text | no | compact |

The `HostBridge` (two delegates: read a drawing's labels, write a drawing's label changes) is the seam between the
AutoCAD-free engine and `LabelWriter`. The smoke test replaces it with fakes.

## 3. Dependency diagram

```
Commands ---- ToFromForm ---- IdentifierService ---- Models
   |              |                 |
   |              +--- DataSourceService --- ScratchDb (ADODB) / ReportImport
   |              |                 |
   |              +--- RunService --+--- Export
   |                      |
   +--- AcadeInterop -----+--- HostBridge ---- LabelWriter (acdbmgd / acmgd)
   |
   +--- DiagnosticsService, ErrorService, SettingsService/ConfigService, ProjectService
```

Nothing below `IdentifierService`, `RunService`, `Export`, `ProjectService`, `ReportImport`, `DataSourceService` or
`ScratchDb` references an AutoCAD assembly. `ScratchDb` touches COM only when a method runs.

## 4. End-to-end data flow

### Plan (`LSETOFROM` > Plan, `LSETOFROMVALIDATE`, `LSETOFROMEXPORT`)

1. `Commands` runs the LISP bridges before any dialog opens: active project, scratch database path, optional refresh.
2. `ProjectService.Read` parses the `.wdp`: every non-directive line is a drawing, numbered from 1 in file order; `====REF`
   marks reference-only drawings (excluded from annotation).
3. `DataSourceService.Load`:
   - scratch: `ProjectService.DatabasePath` (LISP result if it exists, else `database.pathPattern`), `ScratchDb.ReadRows`
     (`database.wireSql`, mapped through `database.columns`), `ScratchDb.ReadDrawings` (`database.drawingSql`, optional),
     then `Stale`: drawings saved after the database file was written become `StaleDatabase` issues (policy warn/stop/ignore);
   - report: `ReportImport.Read` finds the header row (first line holding `CMP1` and `WIREHDL1` or `CMP2`), maps columns
     by name, reads quoted fields.
   - `IdentifierService.Resolve` turns `DWGIX`/`DWGNAM` into drawing paths (name column first, then the drawing map from
     `drawingSql`, then position `DWGIX - drawingIndexBase + 1` in the `.wdp` list). A row with no second drawing inherits
     the first.
4. `IdentifierService.Plan`:
   - optional `swapEnds`;
   - validation of tags and pins per end;
   - `CsvEnds`: two per row (end 2 uses `farEndFormat` when `csv.reverseAtFarEnd`);
   - `SchematicEnds`: one or two per row by `schematic.labelAt`, always with `format`; ends without a wire handle or
     drawing become `WireNotFound` issues; duplicate `(drawing, key)` pairs get `#n` suffixes and a `DuplicateKey` issue;
   - when annotation is requested and a reader exists: every non-reference project drawing (or the current one) is read
     through the bridge. Unreadable (missing, locked by `.dwl`, exception): skipped with a `DrawingSkipped` issue and its
     ends listed as Skip. Label layer matching `WIRELAYS` or equal to `WIRENO_LAY`: skipped with `LayerConflict`.
     Otherwise existing labels are indexed by `TFKEY` and each wanted end becomes Insert, Update (text or node differ, or
     `repositionOnRerun`) or Keep; extra labels with the same key become Delete; labels with no row become Delete
     (`deleteStale`) or Skip with a `StaleLabel` issue.
5. The dialog shows one grid row per writable change (Insert, Update, Delete), ticked by default. Keep and Skip rows
   appear only with Show unchanged and cannot be ticked; a skipped drawing gets one header row with its reason. The
   plan is a snapshot: the Annotate and Current drawing only tick boxes are inputs to `Plan` and are not looked at
   again until the next Plan. Export CSV is the only header option read at Apply time.

### Apply (`RunService.Execute`)

1. Validate: project exists; a writer exists when any drawing has ticked writes.
2. Refuse read-only closed drawings (before any backup, with the Vault check-out hint).
3. Back up every closed drawing that will change to `<project>\_LSE_TOFROM_BACKUP\<yyyyMMdd_HHmmss>\`. Open drawings are
   noted: written in the session, not saved by the plugin, rollback cannot undo them.
4. For each drawing with work: one `DrawingWriteRequest` (its ticked Insert/Update/Delete changes, the schematic config,
   the block library paths) through `HostBridge.WriteLabels`.
5. Verify: each written drawing is read again; an Insert/Update must be found by key exactly once with the planned text;
   a Delete must be gone. Anything else is UNRESOLVED.
6. Export (when requested): label CSV, report CSV (temp file then move). Always: the validation report (written
   directly). Each path is tracked for rollback.
7. Append the run log to `<project>\LSE_ToFromReport.txt`; prune backups beyond `BackupsToKeep` (stamp folders sorted
   by name, newest kept; runs even when this run made no backup); remember the project.
8. On any exception: restore every backed-up drawing, delete every tracked output path (a previous run's file of the
   same name was already overwritten, so it is lost, not restored), write the log to
   `%APPDATA%\LSE\LSE_ToFrom\Logs\ToFromRun_<stamp>.txt`, rethrow with the whole log. Nothing is appended to
   `LSE_ToFromReport.txt` for a failed run. Changes written into drawings open in the editor are not rolled back.

### Label placement (`LabelWriter.Write`)

1. Open drawing: `LockDocument` on its Document, transaction on `Document.Database`, not saved. Closed drawing: side
   database (`ReadDwgFile` ... `SaveAs`), `WorkingDatabase` swapped for the duration. A `.dwl`/`.dwl2` next to a closed
   drawing means locked: exception, and the planner had already skipped it.
2. Ensure the label layer and both block definitions (import `<name>.dwg` from `blockFolder` or the DLL folder; else
   create: `TOFROM` middle-centre text at the origin, `WIRENO` below or beside it (invisible unless `showNode`), `TFKEY`
   invisible; the vertical block's attributes are rotated 90 degrees).
3. Per change: Delete erases by handle; Update sets the three attributes (and re-places when `repositionOnRerun`);
   Insert locates the wire by `WIREHDL` (Line start/end or Polyline first/last vertex), takes the endpoint nearest the
   component block (`CMPHDL`; falls back to the start point with a note), moves `endOffset` along the wire and
   `sideOffset` to its left, steps `collisionStep` along the wire up to `collisionTries` times while another label on the
   label layer is within `collisionDistance`, and inserts the horizontal or vertical block by the wire's direction
   (vertical when the wire runs more up-down than left-right). An Update whose label no longer exists falls through to
   Insert. A wire handle not found, or an entity that is not a Line or Polyline, skips that label with a log line; the
   verify step then reports it UNRESOLVED.

### Command order of the other commands

| Command | Steps |
|---|---|
| `LSETOFROMEXPORT` | prepare (settings, config, project, refresh LISP, scratch path) then load rows then `Plan` with annotation off then `RunService.Execute` with export on (no drawing work, so no backup) then show log and validation |
| `LSETOFROMVALIDATE` | prepare then load rows then `Plan` with annotation on (drawings read, never written; scope from `CurrentDrawingOnly`) then write and show the validation report and the plan text |
| `LSETOFROMDISCOVER` | prepare then `ScratchDb.Schema` then score tables then, for the best table with 3 or more candidate columns, sample 5 rows and offer to save `SELECT * FROM [table]` into the config then save the findings note |
| `LSETOFROMBLOCKS` | folder dialog then `LabelWriter.ExportBlocks` then optional `LabelWriter.AddToDrawing` on the active drawing (layer and both definitions, not saved) |

## 5. Process, threading and COM model

Everything runs on AutoCAD's UI thread inside a modal command. The LISP bridges are synchronous calls made before the
dialog opens (they fail inside a modal dialog): first `Editor.Command` with the expression, and when USERS1 stays blank,
`acedEvaluateLisp` from `accore.dll` by P/Invoke (the x64 decorated export; the result buffer is released with
`acutRelRb`). Whichever fills USERS1 wins; `LSETOFROMDIAG` shows which. ADODB is created from its ProgID, opened with
`Mode=1` (read), and released with `Marshal.FinalReleaseComObject` in `finally`. No background threads, no timers.

## 6. Units, coordinates and encodings

Offsets and text height are drawing units (the LSE schematics are inches; defaults 0.5, 0.0625, 0.08). Positions are
model-space 2D. The `.wdp` is read as UTF-8 with or without BOM, UTF-16 with BOM, or Latin-1. CSV files are written UTF-8
with BOM by default (`csv.encoding` also offers plain UTF-8 and ANSI/Latin-1 for label printer software). Handles are
upper-case hexadecimal without a `0x` prefix.

## 7. Design decisions

| Decision | Choice | Why |
|---|---|---|
| Replace or keep the node number | keep; to/from is its own label | the work scope's default; overwriting `WIRENO` is wrong on multi-connection networks and goes stale on retag |
| Data source | AutoCAD Electrical's from/to data, two routes | the scope forbids re-tracing; the scratch table is undocumented, so a saved report is the confirmed fallback and the ground truth |
| Scratch access | ADODB through COM `dynamic` | house rule: no NuGet runtime packages (`System.Data.OleDb` is one); the Catalog Tools plugin already reads MDBs this way |
| Database writes | none, enforced twice | `Mode=1` and a SELECT-only check in both the config validator and the query method |
| Re-run identity | hidden `TFKEY` = `<wire handle>/<end>` | the handle is stable for the life of the entity; the end disambiguates single-segment wires that touch both components |
| Schematic text | one string per wire, at the From end by default | scope default; `labelAt` offers to/both |
| Printed text | reversed at the far end | scope default; `csv.reverseAtFarEnd` |
| Missing pin | `?` plus a validation line | scope default |
| Stale labels | deleted by default | the scope's main risk is stale text; `deleteStale` can turn it off |
| Block DWGs | generated on the workstation by `LSETOFROMBLOCKS`, or created in-drawing | no AutoCAD here to author a DWG; the generated definition is the single source of truth |
| Open drawings | written in the session, not saved | field finding from LSE_Env: engineers keep sheets open; silently saving them is not acceptable |
| Locked drawings | skip and report | scope guardrail for shared projects |
| Queries in one file | `ScratchDb.cs` | scope guardrail: an ACADE upgrade touches one file |

## 8. Relationship to the other LSE plugins

LSE_Env's "From/To device" wire scheme writes `FROM-TO` **as the wire number** (fixed), traced from geometry, one per
network. LSE_ToFrom writes a **separate** per-wire label from AutoCAD Electrical's data and leaves wire numbers alone. The
two can coexist; LSE_ToFrom's node attribute simply shows whatever `WIRENO` holds. LSE_Doctor does not know the label
layer; it is not a wire layer and not a `WD_M` setting, so the Doctor ignores it. The settings, error dialog, backup and
rollback shape, side-database handling and smoke-test style are copied from LSE_Env.
