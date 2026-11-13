# Developer guide: LSE_Env

Maintained by DCW. Last reviewed 2026-11-12.

## 1. State of the code

- Written 2026-11-11 on a machine with no AutoCAD and no .NET SDK install; compiled with a portable .NET 8 SDK
  against the `AutoCAD.NET` 25.1.0 reference package: Release, 0 warnings.
- `tests\LSE_Env.SmokeTest` (89 checks) passed against the built DLL: byte-identical round trip of the real template
  `.wdp`; index-ordered insertion; rules load, validation and built-in defaults; profile save/read/retire; wire-network
  building (touching, T-junction, gap join and split), device, mark and signal attachment, natural ordering,
  left-to-right FROM/TO with terminal strips last, priority regexes, pins, `multiDevice: all`, single/no-device
  policies, `maxLength`, cross-sheet merge by signal code, duplicate suffix with existing numbers claiming first,
  insert/keep/set/skip decisions, `overwriteFixed`, `insertMissing`, verify; planning against a fake project (grid
  scheme, profile with scheme, raw overrides replacing value changes, protected `+[1]`, missing `WD_M` attribute
  note, From/To per drawing, un-fix after a recorded From/To run, template-based scheme); apply with backup, marker,
  report, prune; rollback restoring the `.wdp` and marker; read-only refusal before backup; capture; snapshot diff.
- Never loaded in AutoCAD Electrical. Everything in `DrawingSettingsService`, `WireNumberDrawingService`,
  `AcadeBridgeService` and the forms inside the host is unverified, as is the setting map itself.

## 2. Repository layout

```
acade-project-env\
  LSE_Env.csproj
  README.md
  bundle\LSE_Env.bundle\PackageContents.xml
  config\LseEnvRules.json            the setting map, wire schemes, From/To rules
  config\profiles\*.json             three sample profiles (copied next to the DLL and seeded on first use)
  docs\                              this set
  src\*.cs                           eighteen files, see CODE-REFERENCE.md
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

1. Build Release with the AutoCAD install; expect 0 warnings. Run the smoke test; expect 89 passed.
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
| 13 | WinForms grid fix (2026-11-12, field Issue 3): `EnvironmentForm.FillGrid` set `ReadOnly` on a new cell before adding it to the row, which throws. Now the cell is assigned first. The same pattern was searched for in `ProfilesForm` and `WireSchemeForm` (none). The three-part error dialog and `Error_<stamp>.txt` log (`ErrorService`) are compiled and the log writer is smoke-tested, but the dialog and the grid fix have not been run in AutoCAD. | `EnvironmentForm.FillGrid`, `ErrorService.Show` | first-build checklist: press Plan on a project with note rows |
