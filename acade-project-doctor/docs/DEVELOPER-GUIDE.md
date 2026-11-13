# Developer guide: LSE_Doctor

Maintained by DCW. Last reviewed 2026-11-13.

How to build, deploy, debug, extend and verify the doctor, and the list of known gaps. Read [ARCHITECTURE.md](ARCHITECTURE.md) for the design and [CODE-REFERENCE.md](CODE-REFERENCE.md) for member-level detail.

## 1. State of the code

The tree was authored on a machine with no AutoCAD and no installed .NET SDK. Verified on 2026-11-10:

- It compiles in Release with 0 warnings and 0 errors against the `AutoCAD.NET` 25.1.0 reference package, using a portable .NET 8 SDK.
- A console smoke test of 90 checks passed against the built code. It covered: a byte-identical round trip of the real template `.wdp` (UTF-8 with BOM, CRLF); an exact copy of the template diagnosed as healthy; a deliberately broken UTF-16 project (missing `*[n]`, `+[n]` and `?[n]` lines, wrong library paths, no `.wdt`, `.wdl` or `.aepx`, a drawing missing on disk) repaired to healthy with encoding and line-group order preserved and new lines inserted in index order; partial `.wdt` and `.wdl` edits (BLOCK name appended, differing codes replaced, comments and extra lines kept, labels sorted); merge-mode path lists; backup pruning; a read-only `.wdp` refused before any backup with a Vault hint; a mid-run failure rolled back (original `.wdp` restored, created `.wdt` deleted); the pure wire-layer logic in `WireLayerService` (splitting, wildcards, unordered list comparison, wire-layer detection by pattern, single name and xdata, standard-name test, layer differences, `Resolved`); and the wire-layer finding generation, by calling the private `DiagnosisService.WireLayers` through reflection with hand-built `DrawingInspection` objects for the template and a drawing.
- The 2026-11-10 harness is not in the tree.

Verified on 2026-11-12 (build `2026-11-12-r7`): compiles with 0 warnings; `tests\LSE_Doctor.SmokeTest` passes 45 checks on a real `DataGridView` (findings grid and scan grid: columns, fill, tick toggling, note rows kept as read-only check-box cells, painting, hide-Info behaviour; the help texts; the build tag; `Diagnosis.OpenDrawings`).

**In AutoCAD Electrical:** the user opened `LSEDOCTOR` on 2026-11-12 (the window and the findings grid appeared) and reported that the tick boxes did not respond; r7 answers that (Known gaps 40). No repair has been run in the host. Everything that touches the AutoCAD API is unverified on a workstation: `DrawingInspectionService` (inspection of blocks and layers, `WD_M` insertion, attribute setting, layer creation, repair and purge, `SaveAs`), `AcadeBridgeService` (`ace_getactiveproject` through `Editor.Command` and `USERS1`, the `c:wd_makeproj_current` reload), the command registration and the forms inside the host. Treat the first `NETLOAD` as a review pass and work through section 7.

Naming: the product, assembly, project file, bundle folder and settings folder are `LSE_Doctor`; the source folder (`acade-project-doctor`) and the namespace (`LSE.AutoCADElectrical.ProjectDoctor`) keep their original names. Do not rename the namespace without rebuilding every reference in the bundle manifest and the docs.

## 2. Repository layout

```
acade-project-doctor/
  LSE_Doctor.csproj   SDK-style project, net8.0-windows, x64, WinForms
  README.md                                    overview, commands, checks, transaction, build, deploy, gaps
  .gitignore                                   bin/, obj/, *.user, .vs/
  config/LseDoctorRules.json                   the comparison rules; copied to the output root
  config/standard/                             bundled copy of the template .wdp, .wdt, _wdtitle.wdl; copied to standard\
  bundle/LSE_Doctor.bundle/PackageContents.xml   autoloader manifest
  docs/                                        this documentation set
  src/*.cs                                     nineteen source files, one namespace (GridService, HelpText, BuildInfo, VaultProbe added in r7)
  tests/LSE_Doctor.SmokeTest/                  console smoke test: grids, help texts, build tag (45 checks)
```

## 3. Build

Prerequisites: .NET 8 SDK, Windows x64. AutoCAD Electrical 2026 is optional for compiling and required for running.

```
dotnet build -c Release
```

Reference resolution in the project file:

| Condition | References |
|---|---|
| `$(AcadDir)\acdbmgd.dll` exists (default `C:\Program Files\Autodesk\AutoCAD 2026`) | `accoremgd.dll`, `acdbmgd.dll`, `acmgd.dll` from that folder, `Private=false` |
| otherwise | NuGet `AutoCAD.NET` 25.1.0 with `ExcludeAssets="runtime"` (compile only, nothing copied to the output) |

Override the install folder with `dotnet build -c Release -p:AcadDir="D:\Autodesk\AutoCAD 2026"`.

The sibling project (`../../acade-project-creator`) records that `AutoCAD.NET` 25.1.1 targets .NET 10 and fails to restore for `net8.0-windows`, so 25.1.0 is pinned. Check the package's target framework before changing it.

Project settings worth knowing:

| Setting | Value | Reason |
|---|---|---|
| `TargetFramework` | `net8.0-windows` | AutoCAD 2025 and 2026 host .NET 8 plugins. |
| `PlatformTarget` | `x64` | AutoCAD is 64-bit only. |
| `EnableDynamicLoading` | `true` | Output suitable for a plugin loaded by `NETLOAD`. |
| `AppendTargetFrameworkToOutputPath` | `false` | Output lands in `bin\Release\` directly. |
| `Nullable`, `ImplicitUsings` | enabled | House style. |
| Content items | `LseDoctorRules.json`, `standard\*` | Copied with `PreserveNewest`. |

### Building without an installed SDK or AutoCAD

Download `https://aka.ms/dotnet/8.0/dotnet-sdk-win-x64.zip`, unzip it to its own folder, set `DOTNET_ROOT` to that folder and run its `dotnet.exe build -c Release` against the project file. No administrator rights are needed. The build uses the NuGet reference package because no `acdbmgd.dll` is found. This is how the 2026-11-10 verification was done.

## 4. Deploy and debug

Deploy as in [OPERATIONS.md](OPERATIONS.md#1-install). For development:

1. Build Debug. In AutoCAD Electrical type `NETLOAD` and pick `bin\Debug\LSE_Doctor.dll`. AutoCAD locks the DLL until it exits, so each rebuild needs a restart.
2. Attach a debugger to `acad.exe` (.NET code). Useful breakpoints: `RepairService.Execute`, `DiagnosisService.Examine` and its private `WireLayers`, `DrawingInspectionService.Inspect` and `Repair`, `AcadeBridgeService.TryGetActiveProject`.
3. `LSEDOCTORDIAG` shows what the plugin resolved.
4. Test repairs on a copy of a project. Rollback restores backed-up files and deletes created ones, but a copy keeps real jobs out of the way.

## 5. Conventions in use

The code follows the same conventions as the sibling project:

- File-scoped namespace. Types are `sealed` or `static`. No interfaces, inheritance or dependency injection.
- Two densities. Compact (one-space indent, many statements per line, short locals such as `s`, `r`, `d`, `p`, `x`): `Models.cs`, `SettingsService.cs`, `TextFileService.cs`, `StandardService.cs`, `DiagnosisService.cs`, `RepairService.cs`, `WireLayerService.cs`, `AcadeBridgeService.cs`, `DiagnosticsService.cs`, `DoctorSettingsForm.cs`, `Plugin.cs`, `TextReportForm.cs`, `HelpText.cs`, `VaultProbe.cs`. Expanded (4-space indent, descriptive names, `private` written out): `DoctorForm.cs`, `ScanForm.cs`, `DrawingInspectionService.cs`, `GridService.cs`, `BuildInfo.cs`. `VaultProbe.cs` is a shared file: change it in every plugin folder at once. Do not mix within a file.
- Logic that can run without AutoCAD is kept out of the AutoCAD-referencing files so a console test can reach it: `WireLayerService` holds every wire-layer decision and `DrawingInspectionService` only reads and writes the DWG.
- Text comparisons use `StringComparison.OrdinalIgnoreCase` except the `+[n]`/`?[n]` option values that are not paths, which are compared with `Ordinal` after trimming.
- Errors are `InvalidOperationException` or the specific BCL exception with a sentence an engineer can act on. They are caught only at command methods, button handlers and the repair try block.
- A mutating run produces a line-per-step log that ends `Status: SUCCESS` or `Status: PARTIAL`.
- Settings live in one `AppSettings` class, saved through `SettingsService.Save` (temp file plus move). Rules live in `DoctorRules`, read-only at run time.
- Commands are `LSEDOCTOR` plus an upper-case suffix on the single `Commands` class.
- No XML doc comments, regions or pragmas. No NuGet packages at runtime.

## 6. Recipes

### Update the standard

Replace the files in the Vault template folder. The doctor reads them on every form open. Then refresh the fallback: copy the three text files into `config\standard\` and rebuild. Expected `LSEDOCTORDIAG` counts change with the template (README lists 8, 38, 75, 11, 14); update the README and [DATA-STORES.md](DATA-STORES.md#4-the-lse-template-files) if they do.

### Change how strict a comparison is

Edit `LseDoctorRules.json`. No code change. Add `+[n]` or `?[n]` indexes to `protectedOptions` or `protectedProperties` to stop them being recommended. Add friendly names under `labels`.

### Add a rule switch

1. Property with a default on `DoctorRules` (`Models.cs`) and the key in `LseDoctorRules.json` with a header comment.
2. Use it in the matching private check in `DiagnosisService` to set `Severity`, `Recommended` or `Note`.
3. If it needs validation, add it to `RulesService.Load`.
4. Document it in [DATA-STORES.md](DATA-STORES.md#3-lsedoctorrulesjson).

### Add a setting

Property with a default on `AppSettings`; a row in `DoctorSettingsForm` (`Add` for text, `Check` for a check box) and the matching assignment in the Save handler; read it where needed; mention it in [DATA-STORES.md](DATA-STORES.md#2-settingsjson) and [OPERATIONS.md](OPERATIONS.md#6-settings-lsedoctorsettings).

### Add a new kind of check

1. Add a private method in `DiagnosisService` that adds `Finding` objects. Choose a `Target`, `Category` and `Item` that give a stable `Key`. Call it from `Examine`.
2. If it has a repair, add a member to `RepairAction` if needed, set `ActionText`, and handle it in `RepairService.Execute` (back up the file in the touched-file list first, then edit, then let the verification pass prove it).
3. If the repair changes a DWG: add the member to `Finding.IsDrawingAction`, put `Target` = full drawing path and `Name` = the tag or layer name on the finding, add a field to `DrawingRepairRequest`, fill it in the `switch` in `RepairService.Execute`, add a private step to `DrawingInspectionService.Repair` at the right place in the fixed order (insert `WD_M`, create layers, repair layers, attributes, purge layers), and teach `WireLayerService.Resolved` how to prove it from a fresh `DrawingInspection`.
4. Add the category to the catalogue in [OPERATIONS.md](OPERATIONS.md#findings-catalogue).

### Add or remove a wire-layer attribute

Edit `wireLayerAttributes` in the rules JSON (and `wireLayerListAttributes` if the new tag holds a comma-delimited list). No code change. The tag is compared only when the template drawing's `WD_M` carries it, so an attribute the template lacks is ignored. The default list is the set of layer-naming tags on the Autodesk help page "About the WD_M block"; check that page before adding one.

### Change what makes a layer a wire layer

`WireLayerService.WireLayers` decides: a list-pattern match, a single-name match, or xdata under an application matching `wireLayerXdataApps`. If a workstation shows AutoCAD Electrical uses a different registered-application name than `VIA_WD*`, change the rules JSON default and the class default in `DoctorRules` together, and record the real name in [DATA-STORES.md](DATA-STORES.md#3-lsedoctorrulesjson).

### Add a text-file edit helper

Put it in `TextFileService`, operate on `TextDocument.Lines`, set `Changed` only when the text really changed, and keep the line order and encoding untouched.

### Cut a release

Bump `BuildInfo.Release` (`src\BuildInfo.cs`), `Version`, `AssemblyVersion`, `FileVersion` and `InformationalVersion` in `LSE_Doctor.csproj`, and `AppVersion` in `bundle\LSE_Doctor.bundle\PackageContents.xml`; build; run the smoke test; update the README status line. The tag appears in every window title so the workstation can confirm which DLL AutoCAD loaded.

### Add or change a help text

Edit the verbatim string in `src\HelpText.cs` (`Doctor`, `Scan`, `Settings` or `Findings`); write a quote inside it as two quotes. Keep the section headings the smoke test looks for ("Why can I not tick this row?", "Repair ticked", "LSEDOCTORSCAN", "review", "Template project", "settings.json", "Wire layers", "DEFPOINTS") or update the test. Tooltips live at the end of each form's `BuildLayout`.

### Check a different block name

Set `wdmBlockName` or `titleBlockNames` in the rules JSON. Patterns accept `*` and `?`.

## 7. First-build verification checklist

Do these on an AutoCAD Electrical 2026 workstation, in order, on copies of projects. Tick each item as it is confirmed and record any difference in the Known Gaps table.

1. `NETLOAD` the DLL (or install the bundle). `LSEDOCTORDIAG` shows `.NET 8`, the DLL path, `Rules: PASS`, `Standard: PASS` with the Vault template and the counts 8, 38, 75, 11, 14 and a template drawing.
2. Temporarily rename the Vault template path in settings and confirm the standard label turns red with "bundled copy" and diagnostics reports the bundled `.wdp`. Restore the setting.
3. Open a project in AutoCAD Electrical, run `LSEDOCTOR`. Confirm the dialog presets the active project (tests `ace_getactiveproject` through `Editor.Command` and `USERS1`). If not, record what `ace_getactiveproject` returns by hand at the command line.
4. Examine an exact copy of the template project folder: expect Healthy (the smoke test did this on the text files).
5. Examine a project missing description lines, library paths, `.wdt` and `.wdl`. Check every finding makes sense. Repair with the recommended ticks. Open the project in the Project Manager and check the descriptions dialog shows the LSE labels and the Properties and Library settings show the LSE values.
6. Open the repaired `.wdp` in a text editor: same encoding as before, same line endings, new lines in index order.
7. Run the reload. After repairing the active project, confirm the command line shows "LSE_Doctor: reloaded project" and the Project Manager shows the repaired settings. If it says `c:wd_makeproj_current` is not loaded, find the correct function and set it in settings.
8. Run Title Block Update on a drawing of the repaired project. Confirm the LSE title block attributes (`PROJECT_NAME`, `QUOTE_NUMBER`, `CUSTOMER` ...) fill from the description lines.
9. Turn on "Check every drawing" on a project whose drawings include one without `WD_M`. Confirm the finding. Repair on a copy. Open the drawing: check the `WD_M` block exists, its layer, its attribute values (note the `SHEET` value), that AutoCAD Electrical treats the drawing as configured, and the DWG version before and after (`SaveAs` uses `DwgVersion.Current`).
10. Open a drawing in the editor and examine with drawing checks on: expect the "open in the editor" Info row and no change to the drawing.
11. Force a failure (for example make a `.wdl` read-only: expect the Vault hint and no backup; or lock a DWG to force a mid-run exception) and confirm the rollback log and that the project is as before.
12. Run `LSEDOCTORSCAN` over a scratch folder with several projects (one healthy, one broken, one creator-made). Confirm statuses, Open in Doctor... and batch repair.
13. Create an `.aepx` with the doctor on a copy and open the project in AutoCAD Electrical; compare with one AutoCAD Electrical writes itself.
14. Examine a project with drawing checks on while the Vault template is reachable. In the report's Checks section read `Wire layers: standard is DRAWING-#.dwg with N wire layer(s): ...`. Open `DRAWING-#.dwg` in AutoCAD Electrical and confirm the listed names are exactly the wire types it shows (Create/Edit Wire Type). If a wire layer is missing from the list, open its layer table record (for example with the `(entget (tblobjname "LAYER" "name") '("*"))` LISP expression) and note the registered-application name of its xdata; if it is not `VIA_WD*`, update `wireLayerXdataApps` and record the name in Known Gaps.
15. On a copy of one drawing: delete a template wire layer, change the colour of another, add an empty layer named to match the drawing's `WIRELAYS` pattern, and change `WIRELAYS` in `WD_M`. Examine: expect one Create layer from template, one Repair layer, one Purge layer and one Set WD_M value row. Repair. Open the drawing in AutoCAD Electrical: confirm the layers and values, that Insert Wire on the created and repaired layers is treated as a wire, that the purged layer is gone, that nothing drawn moved, and compare the DWG version with the backup.
16. Repeat the purge on a drawing whose non-standard wire layer has a viewport layer override or is used inside a block definition: expect `Layer <name> is still referenced ... cannot be purged.`, a rollback, and the DWG byte-identical to the backup.

## 8. Test strategy

- The text logic and the wire-layer logic are plain .NET and can be exercised from a console project that references the built DLL. The 90-check smoke test of 2026-11-10 did this ([section 1](#1-state-of-the-code)). It is not kept in the tree.
- `WireLayerService` takes `DrawingInspection`, `LayerInfo` and `DoctorRules` objects, all plain classes, so a test builds a fake template and a fake drawing by hand (names, colours, linetypes, lineweights, `XdataApps`, `EntityCount`, `WdmValues`) and never needs a DWG. The finding generation (`DiagnosisService.WireLayers`) is private and static; the smoke test reached it with reflection (`BindingFlags.NonPublic | BindingFlags.Static`) and a `Diagnosis` whose `Rules` were loaded from the shipped JSON.
- `DrawingInspectionService`, `AcadeBridgeService`, the commands and the forms can only be tested inside AutoCAD Electrical. Use the checklist above.
- `tests\LSE_Doctor.SmokeTest` (r7) is a console project that references `bin\Release\LSE_Doctor.dll` and runs 45 checks without AutoCAD: `GridService` on a real `DataGridView` (both grids, tick toggling through the same methods the forms call, note rows, painting through a `Bitmap`), `HelpText`, `BuildInfo`, `Diagnosis.OpenDrawings`. Build the plugin in Release first, then `dotnet build tests\LSE_Doctor.SmokeTest\LSE_Doctor.SmokeTest.csproj -c Release` and run `tests\LSE_Doctor.SmokeTest\bin\Release\LSE_Doctor.SmokeTest.dll` (or `dotnet run -c Release --project tests\LSE_Doctor.SmokeTest\LSE_Doctor.SmokeTest.csproj`; the test csproj comment writes `- -project` because `--` is not allowed inside an XML comment); expect `45 passed, 0 failed`. The text-file and wire-layer cases of the 2026-11-10 harness are still to be added to it. The areas worth keeping as regression tests: byte-identical round trip of a template copy; index-ordered insertion into each group; encoding and line ending preservation for UTF-8 with BOM, UTF-16 and ANSI; merge and replace path lists; backup pruning order; read-only refusal before backup; rollback after a mid-run failure; `SameList` with order, case, `;` and duplicate differences; wildcard matching; wire-layer detection with a blank `WIRELAYS`; `IsStandard` for a name that matches only a template pattern; `Differences` for each property; `Resolved` for every `RepairAction`; and one finding of each wire-layer category from a fake template and drawing.

## 9. Known gaps

Each row names the file and member so it can be found. Items marked decision need a human to choose a behaviour.

| # | Gap | Where | Impact / action |
|---|---|---|---|
| 1 | Never executed inside AutoCAD Electrical. | whole tree | First build is the first test. |
| 2 | `(ace_getactiveproject)` returning the active `.wdp`, and `Editor.Command` running the LISP synchronously so the `USERS1` round trip works, are assumptions. All errors are swallowed and give `""`. | `AcadeBridgeService.TryGetActiveProject` | If wrong, `LSEDOCTOR` opens with no preset project (Browse), `Active project` button says none was detected, and the scan cannot tell which project to reload. |
| 3 | `c:wd_makeproj_current` as the reload function comes from a forum answer, not the official API. `SendStringToExecute` queues it asynchronously after the dialog closes; `ReloadProject` returns silently when the file or document is missing. | `AppSettings.ActivateProjectLisp`, `AcadeBridgeService.ReloadProject` | If it fails, AutoCAD Electrical may overwrite the repaired `.wdp` from memory. Reopen the project by hand. |
| 4 | It is not verified that AutoCAD Electrical accepts either `+` before `?` or `?` before `+` in a `.wdp`. The LSE template writes `*`, `+`, `?`; the creator writes `*`, `?`, `+`. When a whole group is missing, `Set` places it in template order, not in the project's own order. | `TextFileService.Set`, `GroupOrderBefore` | Decision: confirm both orders load, or make the insertion follow the project's order. |
| 5 | `WD_M` insertion is untested in the host: `WblockCloneObjects` of the block definition into a side database, the `HostApplicationServices.WorkingDatabase` swap, `SaveAs`, and whether AutoCAD Electrical then treats the drawing as configured. | `DrawingInspectionService.Repair`, private `InsertWdm` | Test on a copy of one drawing before batch use. |
| 6 | Decision: the `WD_M` insert copies every attribute value from the template's `WD_M`, including per-drawing values such as `SHEET` and any IEC codes. A repaired drawing therefore gets the template's values, not its own sheet number. The log counts them ("N attribute value(s) from <template file>"). | `DrawingInspectionService`, private `InsertWdm` | Decide whether `SHEET` and similar tags should be set per drawing (for example from the drawing's position in the `.wdp`). |
| 7 | Decision: every drawing repair saves with `SaveAs(drawingPath, DwgVersion.Current)`. If the host's current format is newer than the drawing's, the repair upgrades the DWG version. `SaveAs` on a database read with `OpenForReadAndAllShare` and `CloseInput` has not been exercised. The new `WD_M` reference gets only the template's position; layer, linetype and other properties of the template's reference are not copied. | `DrawingInspectionService.Repair` | Decide whether to preserve the original DWG version and copy the reference's layer. Verify the save on a workstation. |
| 8 | Inspection finds `WD_M` and title blocks only as top-level references in layouts. Nested blocks and xrefs are not searched. `WD_M` is an exact name match. | `DrawingInspectionService.Inspect` | A drawing with the title block inside another block reports "no LSE title block". |
| 9 | Decision: the Healthy status counts only Errors and Warnings. Drawings open in the editor or unreadable, and a missing `.aepx`, are Info and do not stop a project reading Healthy. | `Diagnosis.Healthy`, `DiagnosisService.Drawings`, `Configuration` | Decide whether "not inspected" should count as a Warning. |
| 10 | Backup files are stored flat by file name. When two touched files share a name (two drawings with the same name in different folders) the second copy gets a "(2)" suffix, so nothing is lost, but the suffixed name must be matched to its folder by hand during a manual restore. The timestamp has one-second resolution, so two runs in the same second on one project share a folder. | `RepairService.Execute` (backup loop) | Rare. Decision: store the relative path in the backup. |
| 11 | In `LSEDOCTOR` a failure before any change ("Nothing is ticked", a read-only file) is reported as "Repair refused before any change was made"; only failures after the backup say "rolled back". The form tells them apart by searching the message for "Nothing was changed" or "Nothing is ticked", so rewording either message in `VaultProbe` or `RepairService` silently turns a refusal into "FAILED (rolled back)". The `LSEDOCTORSCAN` batch report does not make the distinction at all: a refused project is headed `<wdp> FAILED (rolled back)`. | `DoctorForm.Repair`, `ScanForm.RepairTicked` | Decision: use a dedicated exception type for refusals, and use it in the batch report too. |
| 12 | Decision: the rules header says protected lines are "never changed", but protected findings keep their `Add` or `Replace` action and can be ticked by hand or with Tick all. | `DiagnosisService.Lines`, `DoctorForm.Tick` | Decide whether protected lines should have action `None`. |
| 13 | The `.aepx` structure written by `ProjectConfigurationService.Write` is the author's reading of the format and has not been compared with a file from AutoCAD Electrical. Drawing paths are upper-cased as listed. Creation is off by default. | `ProjectConfigurationService.Write` | Compare with a real file before enabling `requireProjectConfigurationFile`. |
| 14 | Removing a drawing deletes its marker lines (`=`, `==`, `===`, `====REF`, `=====SUB=`) along with its path. The parser assumes markers belong to the next path only; whether AutoCAD Electrical lets a marker apply to several following drawings is not verified. | `TextFileService.Drawings`, `RemoveDrawing` | Removal is never recommended; check on a copy. |
| 15 | The bundled fallback standard has no drawings, so `Insert WD_M` is not offered unless the Vault template folder is reachable. The README's `LSEDOCTORDIAG` expectation "and a template drawing" holds only with the Vault template. | `StandardService.Load` | Documented in the README deploy step. |
| 16 | Only the highest template description index (8) is checked. Extra description lines in a project (for example the creator's 9 to 12) are neither reported nor changed. | `DiagnosisService.Descriptions` | Intended; note for creator-made projects. |
| 17 | Non-path `+[n]` and `?[n]` values are compared case-sensitively and exactly after trimming. Mappings and labels are compared ignoring case. | `DiagnosisService.Lines`, `Mapping`, `Labels` | Intended; a case-only difference in an option value is reported. |
| 18 | `.wdt` mapping lines are parsed by a regular expression: only lines starting with `;` are comments; a line with `;` after the code keeps it as part of the code; duplicate attributes use the first match. New mapping lines are appended at the end of the file; `SetLabel` writes `LINEn=text` without spaces while `SetMapping` writes `ATTR = CODE`. | `TextFileService.Mappings`, `SetMapping`, `SetLabel` | Cosmetic and edge cases. |
| 19 | A text file that is neither BOM-marked nor valid UTF-8 is read as Latin-1 and written back as Latin-1. Bytes round-trip, but characters from a different ANSI code page may display wrongly in the grid. A file without a trailing line break gets one on write. | `TextFileService.Read`, `Write` | Re-save such a project from AutoCAD Electrical. |
| 20 | `SettingsService.Load` swallows any error reading `settings.json` and uses defaults; the next Save overwrites the file. Property names are case-sensitive. | `SettingsService.Load` | Keep a copy before editing by hand. |
| 21 | All work runs on the UI thread; Examine, Scan (with drawing checks) and Repair block AutoCAD and cannot be cancelled. | `DoctorForm`, `ScanForm` | Seconds per project; minutes for large scans with drawing checks. |
| 22 | The run log is appended to `LSE_DoctorReport.txt` before pruning, so prune messages are missing from it; a failed run is not appended to it (only to the `Logs` folder). Nothing deletes old failure logs. | `RepairService.Execute` | Cosmetic. |
| 23 | The manifest allows only `R25.1` (AutoCAD 2026). Since r7 the manifest `AppVersion` (1.1.0), the csproj `Version` / `InformationalVersion` and `BuildInfo.Release` carry the release and must be bumped together; diagnostics and every title bar print the tag. | `PackageContents.xml`, csproj, `BuildInfo`, `DiagnosticsService` | Update when supporting another AutoCAD release; bump the three on every cut. |
| 24 | The meaning of `+[n]` and `?[n]` indexes beyond `+[1]` to `+[4]` is not documented in the tree; they are compared as opaque values. A change deliberately made in a project (a different numbering option) is reported as a difference and, with enforcement on, overwritten. | `DiagnosisService.Lines` | Use `protectedOptions` and `protectedProperties` for settings projects may legitimately change. |
| 25 | The backup folder sits inside the project folder, so a Vault check-in of the project folder includes it. | `RepairService.Execute` | Decision: exclude from check-in, or move the backups. |
| 26 | `GuessProjectNearActiveDrawing` is used by `LSEDOCTOR` only, not by `LSEDOCTORSCAN`. | `Commands.Scan` | Cosmetic. |
| 27 | Vault support is passive: a read-only refusal before the backup (`VaultProbe.RequireWritable`), a check-in reminder after success, and diagnostics lines. The doctor does not check files out or in, and the backup folder it creates inside the project is not mentioned in the reminder. `VaultProbe` has not run against a real Vault 2026.4 install or inside AutoCAD. | `RepairService.Execute`, `VaultProbe` | Check projects out before repairing; check in what the reminder lists. |
| 28 | Not verified: `WblockCloneObjects` of a `LayerTableRecord` from the template database into the drawing's layer table (`CreateLayer`), and that the clone brings the linetype table record, the regapp table records and the xdata with it. If it lands without xdata the layer exists but AutoCAD Electrical may not treat it as a wire type. | `DrawingInspectionService`, private `CreateLayer` | Checklist item 15. If the clone drops xdata, follow the create with the `RepairLayer` steps. |
| 29 | Not verified: `Database.Purge` on a layer id followed by `LayerTableRecord.Erase(true)` inside the same transaction, and that `Purge` returns the id in the collection only when the layer is unreferenced. The doctor's own `EntityCount` is only a hint; `Purge` is the arbiter, and a disagreement throws and rolls the run back. | `DrawingInspectionService`, private `PurgeLayer` | Checklist items 15 and 16. |
| 30 | Not verified: assigning `LayerTableRecord.XData` from a `ResultBuffer` built from another database's xdata, after adding the regapp records by hand. Registered-application names are the only cross-database reference in xdata, but the call has not run. | `DrawingInspectionService`, private `RepairLayer` | Checklist item 15. |
| 31 | Not verified: `Color.ToString()` as the colour comparison key. Two colours that display the same but serialise differently (an ACI index versus the same colour as true colour or a colour book entry) are reported as different and "repaired"; the text itself has not been seen for each colour method. | `DrawingInspectionService.ReadLayers`, `WireLayerService.Differences` | If false positives appear, compare `ColorMethod` and `ColorIndex`/`ColorValue` instead. |
| 32 | Not verified: that AutoCAD Electrical recognises a created or repaired layer as a wire type. It may rely on wire-type xdata on the layer record, and the registered-application name pattern `VIA_WD*` in `wireLayerXdataApps` is a guess; Autodesk does not document it. A wrong pattern means the xdata test never fires and the "no wire-type data" difference is never reported or repaired. | `DoctorRules.WireLayerXdataApps`, `WireLayerService.HasWireXdata` | Checklist item 14: read the real application name from a template wire layer and fix the rules default. |
| 33 | `WireLayerService.Differences` compares colour, linetype, lineweight, plot flag and xdata presence, while `RepairLayer` also writes the description. A layer that differs only in description is reported as matching; the description is cosmetic and is only filled when the template has one. | `WireLayerService.Differences`, `DrawingInspectionService`, private `RepairLayer` | Decision: compare the description too, or stop writing it. |
| 34 | Decision: a blank `WIRELAYS` in a project drawing means "every layer" to AutoCAD Electrical, but to the doctor it means "no list patterns", so the drawing's wire layers are only those named by single-name attributes or carrying wire-type xdata. A drawing with a blank `WIRELAYS` and no wire-type xdata reports no wire layers of its own; the `WIRELAYS` difference itself is still reported and repaired. | `WireLayerService.WireLayers`, `DiagnosisService.WireLayers` | Intended; depends on gap 32 being right. |
| 35 | `Finding.Value` for `CreateLayer` and `RepairLayer` holds the template drawing path, but `RepairService` builds the `DrawingRepairRequest` from `ProjectStandard.TemplateDrawingPath`, not from the finding. They are the same path today; the "findings carry everything" design decision is bypassed for these two actions. | `RepairService.Execute` (drawing grouping) | Cosmetic. |
| 36 | The Info row for an attribute that names a non-existent layer has `Item` = `<drawing> : <tag> -> <name>`, a different shape from the other wire-layer rows. `Finding.Key` still works because `Item` is unique per name. | `DiagnosisService.WireLayers` (last loop) | Cosmetic. |
| 37 | With `checkWireLayers` false the Checks section logs "checkWireLayers is false in the rules JSON; check skipped"; with it true but the template drawing unreadable, one `check skipped` line is logged. In both cases the project can still read Healthy. | `DiagnosisService.Drawings` | Decision: consider a Warning finding when the template drawing cannot be read. |
| 38 | The scan's batch repair applies every recommended finding, which with the default rules includes `Purge layer` and `Repair layer` on every drawing when "Check drawing contents too" is on. There is no separate switch to keep batch repairs to text files. | `ScanForm.RepairTicked`, rules `purgeUnusedWireLayers`, `enforceWireLayers` | Set those two rules to false for a first batch pass, or run the batch with the drawing check off. |
| 39 | `EntityCount` counts entities inside block definitions too, so a non-standard wire layer used only by a block definition is reported as "N entities on a non-standard wire layer" and never offered for purge, which is the safe outcome. Attribute references and nested blocks are not counted (they are children, not record entries). A layer used only by a viewport override or an xref shows 0 entities, is offered for purge, and then fails at `Database.Purge` (gap 29). | `DrawingInspectionService.ReadLayers` | Checklist item 16. |
| 40 | Tick boxes in the host. On 2026-11-12 the user reported that the check boxes in `LSEDOCTOR` did not work (release r5 code). Two things were wrong in that code: every report-only row carried a normal-looking but read-only box that ignored clicks, and repairable rows depended on the `DataGridView` edit cycle (`CurrentCellDirtyStateChanged` + `CommitEdit`). r7 makes the Fix column read-only, toggles from `CellClick` and the space bar through `GridService`, paints `note` on report-only rows and explains clicks on the status line. Whether the r5 report also covered repairable rows is not known. | `GridService`, `DoctorForm`, `ScanForm` | If a box still does not respond in r7: first check the title bar shows `[2026-11-12-r7]` (a loaded DLL cannot be replaced while AutoCAD runs), then report which row. |
| 41 | Decision: drawings open in the editor are skipped (guard rail G5: side databases only), so examining the active project with "Check every drawing" on yields mostly `note` rows for its open drawings. LSE_Env r2 reads open drawings through the `Document` instead. | `DrawingInspectionService.IsOpenInEditor`, `DiagnosisService.Drawings` | Decide whether the Doctor should inspect (and, under `LockDocument`, repair) open drawings; r7 only counts and reports them on the status line. |
| 42 | `HelpText.Doctor` ends "LSEDOCTORHELP shows all of this help at the command line", but `Commands.Help` opens a text window, not command-line output. | `src/HelpText.cs` (`Doctor`, last paragraph) | Cosmetic wording fix. |
| 43 | The two "check drawings" boxes behave differently: the `LSEDOCTOR` box is never saved, the `LSEDOCTORSCAN` box is written to `ScanChecksDrawings` on every Scan, overwriting what was set in `LSEDOCTORSETTINGS`. Neither the settings form nor its help says so. | `ScanForm.Scan`, `DoctorForm` | Decision: make both behave the same, or say so in the settings tooltip. |
| 44 | The settings dialog has no Cancel button and Esc is not bound (`CancelButton = null`); the only way to discard edits is the title-bar X. | `DoctorSettingsForm` | Cosmetic; add a Cancel button. |
| 45 | `LSE_Doctor.csproj` has `Compile Remove="tests\**"` because the nested test project's `obj` files were compiled into the plugin. Keep it when restructuring the project. | `LSE_Doctor.csproj` | None. |
