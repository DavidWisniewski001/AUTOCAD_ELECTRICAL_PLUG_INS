# Developer guide: LSE_Env

Maintained by DCW. Last reviewed 2026-11-13.

How to build, test, debug and change LSE_Env, the checklist for the first workstation build, and the list of known
gaps and suspected faults. Build tag `2026-11-12-r7`, assembly version `1.7.0`.

## 1. State of the code

- Written 2026-11-11 on a machine with no AutoCAD and no .NET SDK install; compiled with a portable .NET 8 SDK
  against the `AutoCAD.NET` 25.1.0 reference package: Release, 0 warnings.
- `tests\LSE_Env.SmokeTest` (91 checks in r6; r7 adds two for the two-file read-only refusal and moves the build-label
  check to r7) passed against the built DLL: byte-identical round trip of the real template
  `.wdp`; index-ordered insertion; rules load, validation and built-in defaults; profile save/read/retire; wire-network
  building (touching, T-junction, gap join and split), device, mark and signal attachment, natural ordering,
  left-to-right FROM/TO with terminal strips last, priority regexes, pins, `multiDevice: all`, single/no-device
  policies, `maxLength`, cross-sheet merge by signal code, duplicate suffix with existing numbers claiming first,
  insert/keep/set/skip decisions, `overwriteFixed`, `insertMissing`, verify; planning against a fake project (grid
  scheme, profile with scheme, raw overrides replacing value changes, protected `+[1]`, missing `WD_M` attribute
  note, From/To per drawing, un-fix after a recorded From/To run, template-based scheme); apply with backup, marker,
  report, prune; rollback restoring the `.wdp` and marker; read-only refusal before backup (one message naming every
  read-only file, no backup folder made); capture; snapshot diff; the help texts; the error log; the plan grid filled
  on a real `DataGridView`; the build label.
- Loaded in AutoCAD Electrical on 2026-11-12: the `LSEENV` dialog, Plan and the error dialog were seen in the host.
  The writes (`DrawingSettingsService`, `WireNumberDrawingService`), `AcadeBridgeService`'s LISP calls and the other
  forms inside the host are unverified, as is the setting map itself.
- The grid tick box itself is covered only as far as "an applicable row has a ticked, editable check box"; clicking it
  in the host, and the header tick boxes, have no automated test.

## 2. Repository layout

```
acade-project-env\
  LSE_Env.csproj
  README.md
  bundle\LSE_Env.bundle\PackageContents.xml
  config\LseEnvRules.json            the setting map, wire schemes, From/To rules
  config\profiles\*.json             three sample profiles (copied next to the DLL and seeded on first use)
  docs\                              this set
  src\*.cs                           twenty-two files, see CODE-REFERENCE.md
  tests\LSE_Env.SmokeTest\           console smoke test (excluded from the plugin build)
```

## 3. Build

With AutoCAD Electrical 2026 installed:

```
dotnet build acade-project-env\LSE_Env.csproj -c Release
```

Elsewhere, pass `-p:AcadDir="D:\Autodesk\AutoCAD 2026"`; with no install at all the reference package is used
automatically (restore needs nuget.org once). Output in `bin\Release\`: `LSE_Env.dll`, `LseEnvRules.json`, `profiles\`.

Smoke test (after the Release build):

```
dotnet build acade-project-env\tests\LSE_Env.SmokeTest\LSE_Env.SmokeTest.csproj -c Release
dotnet acade-project-env\tests\LSE_Env.SmokeTest\bin\Release\LSE_Env.SmokeTest.dll acade-project-env
```

The argument is the plugin folder (used to find `config\LseEnvRules.json` and `..\reference\CUSTOM PROJECT TEMPLATE
2.0\*.wdp`). It works in `%TEMP%\LSE_Env_smoke_<stamp>` and prints PASS/FAIL per check; exit code 1 on any failure.

## 4. Deploy and debug

NETLOAD the DLL for a session, or use the bundle. Attach the debugger to `acad.exe` with the Release build loaded;
the compact services are easiest to step through from the form's call (`EnvironmentForm.Plan`, `.Apply`).
`LSEENVDIAG` reports which rules file and profiles folder are in use.

## 5. Conventions in use

- Two densities (see `..\agents\lse-csharp-dev.md`): compact services, models, settings/scheme/snapshot/report forms,
  `Plugin.cs`; expanded for the AutoCAD-API services and the two big forms.
- AutoCAD types are confined to `DrawingSettingsService`, `WireNumberDrawingService`, `AcadeBridgeService`,
  `Plugin` and the forms. Services reach drawings only through `HostBridge` delegates so they can be tested.
- `WireNetworkService` is pure: it takes `DrawingWires` and returns decisions on `WireNetwork` objects.
- Errors are `InvalidOperationException` with a plain-English message; forms catch at the button boundary.
- The rules JSON is the only place that knows a `?[n]` index or a `WD_M` tag for a numbering/tag setting.

## 6. Recipes

- **Add a setting the map should cover**: add a row to `settings[]` in `LseEnvRules.json` (key, label, group,
  property/option/wdm, kind, choices, confidence). No code change; the dialogs, capture, diff and diagnostics pick it
  up. Document the row in DATA-STORES section 3.
- **Add a wire scheme**: add to `wireSchemes[]`; reference only keys the map defines (the loader refuses others).
- **Change the From/To composition** (`WireNetworkService.Compose` / `Assign`): add a smoke-test case first.
- **Teach the reader a new symbol convention** (`WireNumberDrawingService.ReadBlock`): prefer a rules key
  (`fromTo.*`) over a constant.
- **New change kind**: add to `ChangeKind`, produce it in `EnvironmentService.Plan`, apply it in `ApplyService`,
  verify it in the verify switch, document it in README "What a plan contains". A kind the verify step cannot check
  is not allowed (guardian rule G1).

## 7. First-build verification checklist

1. Build Release with the AutoCAD install; expect 0 warnings. Run the smoke test; expect every line PASS, 0 failed
   (93 checks in r7).
2. NETLOAD; `LSEENVDIAG`: rules 19 rows / 5 schemes, profiles 3, template found.
3. `LSEENV` with a project open: the project is preset (tests `ace_getactiveproject` via `USERS1`).
4. Confirm the setting map with `LSEENVSNAPSHOT` ([OPERATIONS.md](OPERATIONS.md) section 3). Record which rows moved.
   This decides whether anything below can be trusted.
5b. With two sheets of the copy open in the editor: `LSEENV` > Plan lists them with "(open in the editor: written in the session, not saved)"; Apply; the sheets show as modified; check `WD_M` and wire numbers, then save or close without saving.
5. On a copy of a job with closed drawings: `LSEENVWIRES` > "Sequential with sheet prefix" > Switch. Open a drawing:
   Drawing Properties should show the new format; run Insert Wire Numbers project-wide and check the numbers.
6. `LSEENVWIRES` > "From/To device" on the copy. Open a drawing: wire numbers read `PB1-CR1` style and sit on the
   `WIREFIXED` layer; AutoCAD Electrical's retag leaves them alone. Check a T-junction network, a network crossing a
   wire gap, a source/destination pair across two sheets, and a network with a terminal strip.
7. `LSEENVWIRES` > "LSE standard (template)" on the copy: un-fix count matches; after the retag the numbers are
   sequential again.
8. If a drawing had no wire-number block on a network: confirm `WD_WN.dwg` was imported from the library path and the
   inserted block looks like a native one (position above the wire, rotation on vertical wires).
9. `LSEENVPROFILES`: capture the copy, edit, save, export, import, retire.
9b. Tick boxes in `LSEENV` (the owner's 2026-11 report "The check box does not work"): Plan with Read every drawing on
    and Trace wires on, then each alone, and confirm the grid matches [OPERATIONS.md](OPERATIONS.md) section 4.3.
    Click an Apply? box: the summary count must change at once and Apply ticked must grey out when the last tick goes.
    Change the profile after a plan without pressing Plan and note that Apply ticked is still enabled (gap 15).
10. Open the project in LSE_Doctor and note which lines it now reports; until the Doctor reads the marker, add those
    indexes to its `protectedProperties`.

## 8. Test strategy

- Everything above `HostBridge` is covered by the smoke test; keep adding cases there (it is a plain console project,
  one `Check` per assertion). Fake drawings are `DrawingState` / `DrawingWires` objects built by hand.
- The AutoCAD side can only be tested in the host; use the checklist.

## 9. Known gaps

| # | Gap | Where | Mitigation |
|---|---|---|---|
| 1 | Never executed inside AutoCAD Electrical. | whole tree | the checklist |
| 2 | The setting map is inferred from the template's values; `referenceStyle` (X-Y grid) is unmapped. | `config\LseEnvRules.json` | `LSEENVSNAPSHOT`, section 3 of OPERATIONS |
| 3 | Whether `WD_M` edits on a side database are enough, or the Project Properties dialog must run once to push settings. | `DrawingSettingsService.Apply` | checklist 5 |
| 4 | Wire-number block name `WD_WN*`, attribute `WIRENO`, fixed = on `WIREFIXED_LAY`, `WD_WN.dwg` importable from the library path, inserted block equivalent to a native one. | `WireNumberDrawingService` | checklist 6 and 8; all names are rules keys |
| 5 | Symbol conventions: `X?TERMnn` position is the wire end, `TERMnn` the pin, `TAG1`/`TAG2`/`TAGSTRIP` the tag, `SIGCODE` the signal; attribute position vs alignment point for justified attributes. | `WireNumberDrawingService.ReadBlock` | rules keys; checklist 6 |
| 6 | Tolerances (join 0.001, connect 0.02, gap 0.25, mark 0.35) are guesses for inch-based NFPA sheets; metric or scaled sheets need different values. | `fromTo.*Tolerance` | rules keys |
| 7 | Wire gaps and crossings drawn as separate collinear segments are joined by `gapTolerance`; a deliberate break of the same size would be joined too. | `WireNetworkService.Touch` | lower `gapTolerance` |
| 8 | Networks reached only through PLC I/O modules, multi-wire fan-in/out or terminal jumpers are not followed beyond what the geometry and signal codes show. | engine | reported as single-device networks |
| 9 | `c:wd_wnum`, `c:wd_makeproj_current`, `ace_getactiveproject` unverified. | `AppSettings` | settings |
| 10 | LSE_Doctor ignores `<project>.lseenv.json`. | the Doctor | its guardian; meanwhile `protectedProperties` |
| 11 | `multiDevice: all` and `{SHEET}` in cross-sheet networks use the first member's sheet for every member. | `WireNetworkService.Assign` | documented |
| 12 | Writes to a drawing open in the editor (`DocumentLock` on `Document.Database`, no save, `FindOpenDocument` match by `Name`/`Database.Filename`) are unverified in the host; standard AutoCAD .NET practice but untested here. Undo, dirty flag and rollback of a session are not handled (rollback restores disk files only). | `DrawingSettingsService.Apply`, `Read` | checklist 5b |
| 13 | WinForms grid crash (2026-11-12, field Issue 3): r3 set `ReadOnly` on a new cell before adding it to the row, which throws. Since r6 `PlanGridService.Fill` never replaces a cell: note rows keep their check-box cell, are `ReadOnly` at row level and get the word "note" painted over the box. Smoke-tested on a real `DataGridView`; the error dialog (`ErrorService`) was seen in the host, the r6 grid has not been confirmed there. | `PlanGridService`, `ErrorService.Show` | checklist 9b: press Plan on a project with note rows |
| 14 | **Suspected fault, probable cause of "the check box does not work".** In `LSEENV`, Trace wires has no effect unless Read every drawing is also ticked: `Plan` passes `drawingsBox.Checked` as the inspect flag (`src/EnvironmentForm.cs` line 418) and `EnvironmentService.Plan` skips tracing when drawings were not inspected (`src/EnvironmentService.cs` line 33). The only sign is a log line in Report...; no grid row. `LSEENVWIRES` inspects when either box is ticked (`src/WireSchemeForm.cs` line 22). | `EnvironmentForm.Plan` | documented in OPERATIONS 4.3; decide whether to inspect on `drawingsBox || wiresBox` as `LSEENVWIRES` does, or add a grid note |
| 15 | **Suspected fault.** Changing the header after a plan (project, profile, scheme, Read every drawing, Trace wires) neither re-plans nor disables Apply ticked; there are no change handlers for the two boxes, and `ProfileChanged` only resets the scheme box (`src/EnvironmentForm.cs` lines 189 and 291-299). Apply ticked then writes the previous plan. The confirmation names the plan's profile and scheme, but not the tick-box state. | `EnvironmentForm` | OPERATIONS 4.1 warns; consider clearing the plan or disabling Apply on any header change |
| 16 | **Suspected fault.** On first use (no `LastProfile`, no `LastScheme`) with an active project, `LSEENV` plans as soon as it opens (`Shown`, `src/EnvironmentForm.cs` lines 76-81) with "(none: scheme only)" and "(from the profile)", so the first thing the engineer sees is the error dialog "Pick a customer profile or a wire scheme first." (line 410). | `EnvironmentForm` constructor | documented in OPERATIONS section 10; consider planning on open only when a profile or scheme is selected |
| 17 | The labels "Read every drawing (WD_M values; closed drawings only; slower)" (`src/EnvironmentForm.cs` line 29), "Include every drawing's WD_M (closed drawings only)" (`src/SnapshotForm.cs` line 4) and the Capture question "(closed drawings only; slower)" (`src/ProfilesForm.cs` line 564) are out of date: since r2 open drawings are read from the session too. The tooltips are correct. | three forms | documented |
| 18 | The read-only (Vault) refusal is thrown before the transaction's `try`, so nothing is written, but `EnvironmentForm.Apply` reports it as "Apply failed and was rolled back" in a window titled "LSE_Env apply failed (rolled back)" (`src/EnvironmentForm.cs` lines 553-558). | `EnvironmentForm.Apply` | documented in OPERATIONS 4.6 |
| 19 | `LSEENVWIRES` has no grid, so it applies the recommended changes only. Un-fixing fixed numbers that LSE_Env did not record (no `.lseenv.json` with `fromTo: true`) is never recommended and cannot be ticked there. | `WireSchemeForm.Run` | use `LSEENV` and tick the un-fix rows |
| 20 | `rb.CheckedChanged += wiresBox.Enabled = true` in `WireSchemeForm` (`src/WireSchemeForm.cs` line 17) is dead code: the box is never disabled. Harmless. | `WireSchemeForm` | none |
| 21 | `bundle\LSE_Env.bundle\PackageContents.xml` says `AppVersion="1.6.0"`; `LSE_Env.csproj` is `1.7.0` (`2026-11-12-r7`). | manifest | bump with the next release |
| 22 | `LSEENVDIAG` prints four of the five default switches; "Show the report after each apply" is not listed. | `DiagnosticsService.BuildReport` | cosmetic |
