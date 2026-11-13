# Architecture: LSE_Env (AutoCAD Electrical 2026 plugin)

Maintained by DCW. Last reviewed 2026-11-12.

Assembly `LSE_Env`, namespace `LSE.AutoCADElectrical.ProjectEnvironment`, folder `acade-project-env\`.

## 1. The problem

Every LSE job starts from the same custom project template, but customers mandate their own conventions: wire numbers
as `FROM-TO` device names, wire numbers from an X-Y grid, sheet-prefixed numbers, different tag formats. AutoCAD
Electrical stores each project-wide setting twice (the `.wdp` `?[n]`/`+[n]` line that new drawings inherit, and the
`WD_M` block in every existing drawing), offers no "customer preset", and has no From/To wire numbering at all.
Engineers change settings by hand per project and per drawing, and cannot switch back quickly.

LSE_Env makes the customer requirement a saved **profile**, applies it to both places in one transaction, offers the
wire scheme alone for quick switching, implements From/To numbering as a reversible fixed-wire-number pass, and
records what was applied so the next engineer (and LSE_Doctor) can see it.

## 2. Components

| File | Density | Role |
|---|---|---|
| `Plugin.cs` | compact | `IExtensionApplication` and the six `[CommandMethod]`s; queues reload and retag LISP after a dialog closes |
| `Models.cs` | compact | `AppSettings`, `EnvRules`, `SettingMap`, `WireScheme`, `FromToSettings`, `FromToRules`, `EnvironmentProfile`, `TextDocument`, `ProjectState`, `DrawingState`, `Change`, `EnvPlan`, `EnvMarker`, `ApplyResult`, `Snapshot`, the wire models (`Pt`, `WireSegment`, `DeviceTerminal`, `WireNumberMark`, `SignalArrow`, `DrawingWires`, `WireNetwork`, `WirePlan`), `DrawingApplyRequest`, `HostBridge` |
| `SettingsService.cs` | compact | `SettingsService` (settings.json), `RulesService` (rules JSON load and validation, built-in defaults), `ProfileService` (profile files, seed, retire), `MarkerService` (`<project>.lseenv.json`) |
| `TextFileService.cs` | compact | byte-faithful `.wdp` read/write, indexed lines, drawing list (same code as LSE_Doctor) |
| `EnvironmentService.cs` | compact | `ReadProject`, `Resolve` (scheme + profile values), `Plan` (changes), `Capture`, `Take`/`Diff` (snapshots), `Render` |
| `WireNetworkService.cs` | compact, pure | the From/To engine: union-find over wire segments, device/mark/signal attachment, cross-sheet merge, number composition, dedupe, action decision, verify, render |
| `ApplyService.cs` | compact | the one write transaction: backup, `.wdp`, file copies, per-drawing writes through the bridge, marker, verify, report, prune, rollback |
| `DrawingSettingsService.cs` | expanded, AutoCAD API | open-document lookup, side-database open, `WD_M` inspect, `WD_M` attribute set, per-drawing apply (calls the wire writer), save |
| `WireNumberDrawingService.cs` | expanded, AutoCAD API | reads wires, symbols, wire-number blocks and signal arrows from model space; writes/inserts wire numbers; un-fixes |
| `AcadeBridgeService.cs` | compact | active project via `USERS1`, reload and retag via `SendStringToExecute`, and `Bridge()` that packages the AutoCAD services as `HostBridge` delegates |
| `DiagnosticsService.cs` | compact | the `LSEENVDIAG` report |
| `EnvironmentForm.cs` | expanded | the `LSEENV` dialog: project, profile, scheme, options, plan grid, apply |
| `WireSchemeForm.cs` | compact | the `LSEENVWIRES` dialog: radio list of schemes, preview, switch |
| `ProfilesForm.cs` | expanded | the `LSEENVPROFILES` manager |
| `SnapshotForm.cs` | compact | the `LSEENVSNAPSHOT` tool |
| `EnvSettingsForm.cs`, `TextReportForm.cs` | compact | settings and report dialogs |

## 3. Dependency diagram

```
Plugin.Commands
  |-- EnvironmentForm ----\
  |-- WireSchemeForm ------+--> EnvironmentService --> TextFileService, RulesService, ProfileService, MarkerService
  |-- ProfilesForm --------/          |                    WireNetworkService (pure)
  |-- SnapshotForm                    |                    HostBridge.Inspect / ReadWires  (delegates)
  |-- EnvSettingsForm                 v
  |-- TextReportForm            ApplyService --> TextFileService, MarkerService, WireNetworkService.Verify
  |                                   |           HostBridge.ApplyDrawing / Inspect / ReadWires
  '-- AcadeBridgeService.Bridge() ----+--> DrawingSettingsService --> WireNumberDrawingService   (AutoCAD API)
```

The AutoCAD-dependent services are reached only through `HostBridge` delegates. Everything above the bridge compiles
and runs without AutoCAD, which is how the smoke test drives the planner and the transaction with fake drawings.

## 4. End-to-end data flow

### Plan (`LSEENV` > Plan, `LSEENVWIRES` > Preview/Switch)

1. `EnvironmentService.ReadProject`: read the `.wdp` (`TextFileService`), group `?[n]` and `+[n]`, list drawings, read
   the marker; with "Read every drawing" on, `HostBridge.Inspect` each drawing for its `WD_M` values (an open drawing through its Document, a closed one as a side database).
2. `EnvironmentService.Resolve`: start from the scheme (fixed `values`, or `templateKeys` read from the LSE template
   `.wdp` when `fromTemplate`), then the profile's `values` on top. `ResolveFromTo`: the profile's `fromTo` block if it
   has one and the scheme is From/To, else the scheme's, else none.
3. For each resolved key, the setting map says where it lives: a `?[n]` / `+[n]` change when the project's line
   differs (protected indexes become notes), a `WD_M` change per drawing that has the attribute and differs (missing
   attribute, no block, open or unreadable drawing become notes). Raw profile overrides follow the same path and
   replace a value-derived change for the same line.
4. `.wdt` / `.wdl` sources become unticked copy changes when the bytes differ.
5. With wire tracing on: a From/To scheme reads every drawing's wires (`HostBridge.ReadWires`), builds networks
   per drawing, merges across sheets by signal code, composes and dedupes numbers, and decides per network (keep, set,
   insert, skip); one `WireNumbers` change per drawing with work carries the `WirePlan`. A non-From/To scheme instead
   looks for wire numbers on the fixed layer and plans an `UnfixWireNumbers` change per drawing (recommended only when
   the marker records a From/To run).
6. A `Marker` change closes the plan whenever anything applies.

### Apply (`ApplyService.Execute`)

Backup every touched file; edit the `.wdp` once; copy files; one `DrawingApplyRequest` per drawing (attributes, wire
plan, un-fix handles) through `HostBridge.ApplyDrawing`, which, for a closed drawing, opens it once as a side database and saves it
in place, and for a drawing open in the editor runs the same transaction on `Document.Database` under
`LockDocument()` without saving (the log says to save the drawing; backup is the file on disk, rollback restores only that); write the marker (merging the previously recorded indexes); verify every ticked change by reading back;
append the report; prune; remember the project. Any exception restores the backups, deletes created files, writes the
failure log and rethrows with the whole log.

### The From/To engine (`WireNetworkService`)

- **Build**: segments touch when an end is within `joinTolerance` of another segment's end or body (T-junctions), or
  when two collinear segments face each other across less than `gapTolerance` (wire gaps). Union-find groups them.
  Device terminals and signal arrows attach to the network whose nearest segment is within `connectTolerance`;
  wire-number marks within `markTolerance`. The label point is the midpoint of the longest horizontal segment.
- **Assign**: networks sharing a signal code (case-insensitive) form one logical network. Devices are ordered by the
  priority regex list, then terminal strips last, then left to right (X), top to bottom (Y), then natural tag order;
  FROM is the first, TO the last (`multiDevice: all` joins every device). A logical network whose existing mark already
  equals its composed text claims that text first; later networks with the same text get the duplicate suffix. Per
  network: no mark means insert (or skip when `insertMissing` is off); all marks equal means keep; a fixed mark with
  `overwriteFixed` off means skip; otherwise set. Fewer than two tagged devices follow `singleDevice` / `noDevice`.
- **Verify**: rebuild networks from the re-read drawing and check that the network nearest each planned label point
  carries the proposed number.

## 5. Process, threading and COM model

Everything runs on the AutoCAD UI thread inside a modal dialog; progress is shown by refreshing a status label. No
background threads, no COM, no Vault. Closed drawings are side databases (`ReadDwgFile`, `SaveAs`); drawings already open in the editor are reached through
their `Document` (read transaction, or write under `document.LockDocument()`, never saved by LSE_Env). No closed drawing is
opened as a document.

## 6. Units, coordinates and encodings

Wire geometry is model-space WCS in drawing units; tolerances in the rules JSON are in the same units (defaults sized
for inch-based NFPA schematics). `.wdp` encoding, BOM and line endings are preserved by `TextFileService`. JSON files
are UTF-8 without BOM, indented, case-insensitive on read.

## 7. Design decisions

- **A separate plugin, not a Doctor feature.** The Doctor's contract is "match the template"; LSE_Env's is "match the
  customer". One command set with both would argue with itself. The marker file is the hand-off.
- **The setting map is data.** Autodesk documents the `WD_M` tags but not the `.wdp` indexes. Putting the map in the
  rules JSON with a confidence per row, and shipping `LSEENVSNAPSHOT` to confirm it, keeps a wrong guess a one-line
  fix on the workstation instead of a rebuild.
- **From/To as fixed wire numbers.** AutoCAD Electrical cannot generate them, but it respects fixed numbers during a
  retag. Writing them fixed and un-fixing on the way back makes the scheme switch reversible.
- **Geometry, not the project database.** Tracing lines and connection attributes needs only the DWG and the documented
  symbol conventions; the scratch database format is undocumented and version-bound.
- **Delegated host access.** `HostBridge` keeps the planner and transaction testable outside AutoCAD and keeps the
  AutoCAD-referencing code to two files.
- **Open drawings through their Document.** Field finding 2026-11-12: engineers keep the project's sheets open, and refusing
  them left the plan empty. `DrawingSettingsService.FindOpenDocument` matches by `Name` or `Database.Filename`; open
  drawings are read and written in the session and never saved by the plugin. The host-specific split stays inside
  `DrawingSettingsService` and `WireNumberDrawingService.Read`; `HostBridge` and the planner are unchanged.
- **Protected lines.** Library and menu paths belong to LSE, not the customer; profiles cannot move them.

## 8. Relationship to LSE_Doctor and LSE Project Tools

Project Tools creates a job from the template; LSE_Env then applies the customer's environment; the Doctor later checks
the job against the template and will, once its guardian adds marker support, treat the recorded indexes as
intentional. `TextFileService` is shared by copy so the three plugins edit `.wdp` files identically.
