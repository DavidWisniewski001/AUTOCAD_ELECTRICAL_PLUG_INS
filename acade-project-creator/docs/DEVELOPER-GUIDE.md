# Developer guide: LSE Project Tools

Maintained by DCW. Last reviewed 2026-11-10.

How to build, debug, extend and verify the plugin. Read [ARCHITECTURE.md](ARCHITECTURE.md) for the design and [CODE-REFERENCE.md](CODE-REFERENCE.md) for member-level detail.

## 1. State of the code

The tree was authored on a machine with no AutoCAD and no installed .NET SDK. On 2026-11-10 it was made specific to Lake Shore Electric: the templates JSON now mirrors the live template project "CUSTOM PROJECT TEMPLATE 2.0" and the vault folder list, and the code gained vault discovery, the job number rule, product lines, per-row templates, sidecar copying and the `.aepx` writer. It **compiles** (Release, 0 errors, 0 warnings) against the `AutoCAD.NET` 25.1.0 reference package using a portable .NET 8 SDK, and the parts that do not need AutoCAD passed a 55-check console smoke test against the built DLL (the test is not in the tree). It covered the file-format, naming, validation and vault-path logic: parsing the real template `.wdp` and re-rendering it byte for byte (UTF-8 with BOM, CRLF, the `*`, `+`, `?` group order, blank lines kept positional); the generated `_wdtitle.wdl` and the generated `.wdt` mapping lines equal to the template's own files; the `.aepx` shape; the job number rule (`001096-1` accepted, other shapes refused); the folder, project and file name patterns with `{JOB}`, `{CUSTOMER}`, `{SHEET:3}` and the other tokens; unit-specs composition; template file resolution in the template folder; vault path remapping; validation messages (required lines, missing per-row template); and the V1 cases (`====REF`, relative versus absolute paths, duplicate file names, sheet number parsing, LISP quoting). **Nothing has run inside AutoCAD Electrical.** Treat the first `NETLOAD` as a review pass and work through section 7.

## 2. Repository layout

```
acade-project-creator/
  LSE.AutoCADElectrical.ProjectTools.csproj   SDK-style project, net8.0-windows, x64, WinForms
  README.md                                   overview, the LSE standard, commands, output, build, deploy, gaps
  .gitignore                                  bin/, obj/, *.user, .vs/
  config/LseProjectTemplates.json             the LSE project standard (vault, job rule, lines, title block, product lines, drawing sets); copied to output
  bundle/LSE.ProjectTools.bundle/PackageContents.xml   autoloader manifest
  docs/                                       this documentation set
  src/*.cs                                    ten source files, one namespace
```

The reference template the JSON was taken from is kept at `../reference/CUSTOM PROJECT TEMPLATE 2.0/` (the `.wdp`, `.wdt`, `_wdtitle.wdl`, `.aepx` and the two template drawings).

## 3. Build

Prerequisites: .NET 8 SDK, Windows x64. AutoCAD Electrical 2026 is optional for compiling, required for running.

```
dotnet build -c Release
```

Reference resolution in the project file:

| Condition | References |
|---|---|
| `$(AcadDir)\acdbmgd.dll` exists (default `C:\Program Files\Autodesk\AutoCAD 2026`) | `accoremgd.dll`, `acdbmgd.dll`, `acmgd.dll` from that folder, `Private=false` |
| otherwise | NuGet `AutoCAD.NET` 25.1.0 with `ExcludeAssets="runtime"` (compile only, nothing copied) |

Override the install folder with `dotnet build -c Release -p:AcadDir="D:\Autodesk\AutoCAD 2026"`.

Package versions matter: `AutoCAD.NET` 25.0.x is AutoCAD 2025, 25.1.0 is AutoCAD 2026 on .NET 8, and 25.1.1 targets .NET 10 and fails to restore for `net8.0-windows`. Do not bump it without checking the package's target framework.

Project settings worth knowing:

| Setting | Value | Reason |
|---|---|---|
| `TargetFramework` | `net8.0-windows` | AutoCAD 2025 and 2026 host .NET 8 plugins. |
| `PlatformTarget` | `x64` | AutoCAD is 64-bit only. |
| `EnableDynamicLoading` | `true` | Produces `.deps.json` / `.runtimeconfig.json` suitable for a plugin loaded by `NETLOAD`. |
| `AppendTargetFrameworkToOutputPath` | `false` | Output lands in `bin\Release\` directly, easier to copy. |
| `Nullable`, `ImplicitUsings` | enabled | House style. |

Building without an installed SDK: download `https://aka.ms/dotnet/8.0/dotnet-sdk-win-x64.zip`, unzip anywhere, set `DOTNET_ROOT` to that folder and run its `dotnet.exe build`. No administrator rights are needed.

## 4. Deploy and debug

Deploy as in [OPERATIONS.md](OPERATIONS.md) section 2. For development:

1. Build Debug. In AutoCAD Electrical type `NETLOAD` and pick `bin\Debug\LSE.AutoCADElectrical.ProjectTools.dll`. AutoCAD locks the DLL until it exits, so each rebuild needs a restart of AutoCAD (or use a copy-and-load script).
2. Attach Visual Studio to `acad.exe` (Debug, Attach to Process, .NET Core code type). Breakpoints in `ProjectTransactionService.Execute` and `DrawingFactoryService.Create` are the useful ones.
3. `LSEPROJECTDIAG` is the quickest way to see what the plugin resolved on this machine: the vault working folder, every default path, the template folder files and sidecars, the template drawing's blocks.
4. Point the jobs root at a scratch folder while testing; rollback deletes only what a run created, but a scratch folder keeps real jobs (and the vault) out of the way. On a machine without the vault, delete `settings.json` after checking the vault out so the blanks are filled from it.

## 5. Conventions in use

The code follows `../STYLE-GUIDE.docx` and the sibling projects:

- File-scoped namespace. Every class `sealed` or `static`. No interfaces, inheritance or dependency injection.
- Two densities. Compact (one member per line, one-space indent, short locals `s`, `q`, `t`, `r`, `p`, `x`): `Models.cs`, `SettingsService.cs`, `WdpFileService.cs`, `AcadeBridgeService.cs`, `ProjectTransactionService.cs`, `DiagnosticsService.cs`, `ProjectSettingsForm.cs`, `Plugin.cs`. Expanded (4-space, descriptive names, `private` written out): `DrawingFactoryService.cs`, `NewProjectForm.cs`. Do not mix within a file.
- Every text comparison is `StringComparison.OrdinalIgnoreCase`; dictionaries keyed by attribute names or tokens use `StringComparer.OrdinalIgnoreCase`.
- Errors are `InvalidOperationException` or the specific BCL exception with a sentence the engineer can act on. Caught only at command methods and button handlers; shown with `MessageBox.Show`.
- Mutating work produces a line-per-step log that ends `Status: SUCCESS` and is written to a report file.
- Settings live in one `AppSettings` POCO, saved through `SettingsService.Save` (temp file plus move). Any new setting: add the property, add a row in `ProjectSettingsForm`, read it from `s.` where needed.
- Everything site-specific (paths, names, patterns, folder trees) lives in the JSON, never in code; code defaults only reproduce the JSON for a missing section.
- Commands are `LSE` + upper-case name, no separators, on the single `Commands` class.
- No XML doc comments, regions or pragmas. No NuGet packages at runtime.

## 6. Recipes

### Add a description line

Add an object to `descriptionLines` in the JSON with a unique `index`, a `label`, the title block `attribute`, optionally `role`, `required`, `placeholder` and `default`. Nothing in code changes. The wizard shows it, the `.wdp` gets `*[n]`, a generated `.wdl` gets `LINEn=label` and a generated `.wdt` gets `attribute = LINEn`, and the drawing's title block attribute is filled. With `CopyTemplateSidecars` on (the default) the copied template files are used instead, so also add the line to the template's `_wdtitle.wdl` and `.wdt` on the vault, and give the title block the attribute; remove the matching `labelFile.extraLabels` entry if the index was one of the FUTURE USE lines.

### Add a product line or a vault folder

Edit `productLines` in the JSON. A new folder under an existing line is a new `{ "name": ... }` node in `children` at the right depth (add `"folder"` when the vault folder name differs from the display name). A new product line is a new object with `name`, `folder` (its folder under the jobs root), `unitType`, `projectType` (an existing drawing set name, or blank), `levels` and `children`. The wizard shows at most three levels. Until the JSON is updated an engineer can type the missing folder into the level box.

### Add a drawing set with E-numbers

Add an object to `projectTypes` with `name`, `description`, `fileNamePattern` (`{JOB}-E{SHEET:3}` or blank to inherit `job.fileNamePattern`) and `drawings`, each with `sheet` (the E-number as digits, `001`, `100`, `300`), `description1` (the drawing type) and, for the cover, `"template": "STOCKCODE-E001.dwg"`. A row without `sheet` continues from the previous row. Point a product line's `projectType` at it, or set `DefaultProjectType` in Settings to pre-select it. Confirm the E-number series against a finished LSE job first (section 9).

### Change the job number rule

Edit `job.numberPattern` (a .NET regular expression; `Load` rejects an invalid one), `job.numberExample` and `job.numberHint`. The hint and example appear beside the job box and in every rejection message. The naming patterns `folderNamePattern`, `projectNamePattern` and `fileNamePattern` take `{JOB}` plus `{CUSTOMER}`, `{QUOTE}`, `{PRODUCTLINE}`, `{PROJECT}`, `{USER}`, `{DATE}`, `{YEAR}`, `{SHEET}`, `{SHEET:n}`. To add a token, extend `ProjectTransactionService.Tokens` and `NewProjectForm.Tokens` together.

### Change which title block is recognised

Edit `titleBlock.blockNames` (wildcards) and the attribute names. Run `LSEPROJECTDIAG` to confirm the template now reports the block with its tags. If a site uses a block that has no `SHEET` attribute, set `sheetAttribute` to the tag it does use. The LSE block maps no sheet or sheet-max attribute, so both are blank.

### Write another AutoCAD Electrical value to the title block

Add the tag under `titleBlock.fixed` with a token value, or, for a value the code computes, add a `Put(...)` call in `ProjectTransactionService.Execute` beside the sheet and file name lines and, if Title Block Update should also know it, a `Map(sb, ..., "CODE")` in `WdpFileService.WriteMapping` and the same line in the template's `.wdt` on the vault.

### Add a WD_M attribute

Add a property to `WdmMap`, a field to the JSON, and a `Put(job.WdmValues, ...)` line in `Execute`. `DrawingFactoryService` writes any tag present in the map.

### Add a setting

Property on `AppSettings` with its default; a row in `ProjectSettingsForm` (text box via `Add`, check box via `Check`) and the matching assignment in the Save handler; read it where needed; mention it in [DATA-STORES.md](DATA-STORES.md). For a path that should default from the vault, add the field to `VaultDefaults` and the JSON `vault` section and fill it in `SettingsService.Load` through `OnVault`.

### Add a post-creation action

`Commands.NewProject` runs after the wizard closes with `Result` set and already handles activation and opening the first drawing. Add the new action there, behind a `ProjectRequest` switch and a wizard check box. A vault check-in would go here.

## 7. First-build verification checklist

Do these on an AutoCAD Electrical 2026 workstation with the vault working folder checked out, in order, with the jobs root pointed at a scratch folder.

1. `NETLOAD` the DLL. `LSEPROJECTDIAG` shows `.NET 8`, the DLL path, `Vault working folder: C:\_Lake_Shore_E_Vault` and no `FAIL` or `MISSING`.
2. `LSEPROJECTSETTINGS`: confirm the LSE template project, `DRAWING-#.dwg` and the shared JSON were filled in; set the jobs root to the scratch folder. Diagnostics shows `Template folder file DRAWING-#.dwg: PASS`, `... STOCKCODE-E001.dwg: PASS`, `Template .wdt: PASS`, `Template _wdtitle.wdl: PASS`, `Template WD_M block: PASS` and `ECAD_LSE_CUSTOM_TB_2.0` with its tags. If `NONE matched`, fix `blockNames`.
3. `LSEPROJECTACTIVATE` on an existing job. Confirm the Project Manager switches. If the command line says the function is not loaded, find the right function (`(c:wd_makeproj_current ...)` was quoted from a forum; try it by hand) and put it into the LISP setting.
4. `LSENEWPROJECT` with product line `Other` and drawing set `Template only` (two sheets). Confirm: folder created, `<job>.wdp`, `<job>.wdt`, `<job>_wdtitle.wdl`, `<job>.aepx`, `<job>-E001.dwg` from `STOCKCODE-E001.dwg`, `<job>-E002.dwg` from `DRAWING-#.dwg`, `LSE_ProjectReport.txt`; the Project Manager opens the project, lists both sheets with their drawing types and shows the LSE labels under Descriptions; each drawing opens without a recovery prompt; the title block shows the values.
5. Repeat with `UL 891 Switchboard` > Eaton > Fixed IC Breakers > 2500A (11 sheets). Confirm the folder path under the scratch root mirrors the vault tree, the sheets are `E001`, `E002`, `E100` ... `E501`, and the Project Manager lists them in that order.
6. Confirm the E-number series against a finished LSE job (for example the one `000924-1-E303.dwg` belongs to) and correct `projectTypes` in the JSON if the real series differs.
7. Open a sheet and insert a component: note the sheet value in the tag (`%S`). The plugin writes the Sheet cell text (`001`) into WD_M `SHEET`; confirm against a finished job whether LSE uses `1`, `001` or `E001` and adjust the grid values or the convention.
8. Run AutoCAD Electrical's Title Block Update on the project: it should find the copied `.wdt` and change nothing. If it changes a value, the plugin's direct fill and the mapping disagree; compare the attribute values in the drawing with the `*[n]` lines.
9. Open the `.aepx` after AutoCAD Electrical has had the project open: confirm it either accepted the pre-written file or rewrote it without complaint.
10. Force a failure (make one row's Template name a missing file after passing step 3 by editing the cell, or make the root read-only) and confirm the rollback log and that no files remain (copied sidecars included).
11. Test a template without WD_M and confirm `WD_M inserted` in the log and a working WD_M in the drawing. If `Database.Insert` misbehaves inside the transaction, move the insert before `StartTransaction`.
12. Check the `.wdp` in a text editor against the template's: UTF-8 BOM, `*[1]` to `*[8]`, the copied `+[n]` then `?[n]` lines, drawing blocks as in [ARCHITECTURE.md](ARCHITECTURE.md) section 7, relative paths.
13. Only then create a real job on the vault and check it in.

## 8. Test strategy

- The `.wdp`, `.wdt`, `.wdl`, `.aepx`, token, naming, vault-path and validation logic is pure .NET and can be exercised from a console project that references the built DLL (types in `WdpFileService`, `TemplateService`, `SettingsService.OnVault`/`TemplateFolder`, `ProjectTransactionService.Validate`, `AcadeBridgeService.LispFor` load without AutoCAD assemblies). The 55-check smoke test used on 2026-11-10 covered the cases listed in section 1, with `../reference/CUSTOM PROJECT TEMPLATE 2.0/` as the fixture; a byte-identical round trip of the template `.wdp` is the single most useful check and should stay in any test project.
- `DrawingFactoryService`, `AcadeBridgeService.ActivateProject`, `SettingsService.DiscoverVaultRoot` on a real vault and the forms can only be tested inside AutoCAD or on a workstation. Use the checklist above.
- There is no automated test project in the tree. Adding one is straightforward: a `tests/` console or xUnit project referencing the DLL with the cases listed above and the reference template as fixture.

## 9. Known gaps

| # | Gap | Where | Impact |
|---|---|---|---|
| 1 | Never executed inside AutoCAD Electrical. | whole tree | First build is the first test. |
| 2 | `c:wd_makeproj_current` is from a forum answer, not the API reference. | `AppSettings.ActivateProjectLisp` | Activation may print the fallback message; the project still exists and opens by hand. |
| 3 | The `.wdp` group order and the marker order before a drawing path. The group order (`*`, `+`, `?`) is now taken from the template as written by AutoCAD Electrical 2026 and verified by a byte-identical round trip; the marker order (descriptions before section codes) remains a choice because the template uses neither. | `WdpFileService.Render` | If the Project Manager wants another marker order, change `Render`; the parser already accepts any order. |
| 4 | Without a template project no `+[n]`/`?[n]` lines are written. | `ProjectTransactionService.Execute` step 3 | Assumed that AutoCAD Electrical applies `wd.env` defaults to a project lacking them. Verify, or run the E-CAD Doctor on the job. |
| 5 | `Database.Insert` is called inside an open transaction when WD_M must be inserted. | `DrawingFactoryService.InsertWdm` | Standard but unverified in 2026; see checklist item 11. |
| 6 | Title blocks nested inside other blocks or in external references are not found. Attributes on blocks named `WD_*` are never written. | `DrawingFactoryService.IsTitleBlock` | The LSE template drawings are expected to carry a plain `ECAD_LSE_CUSTOM_TB_2.0` reference; unverified. |
| 7 | `SHEETMAX` is only written when `sheetMaxAttribute` is set; the LSE block maps none. | `Execute` | No sheet count on the LSE title block; nothing to update after adding drawings. |
| 8 | Lines in a template project that start with `*`, `?` or `+` but are not `x[n]value` are dropped. | `WdpFileService.Parse` | Only `?[n]`/`+[n]` are copied anyway; `Unknown` is kept for diagnostics only. The LSE template has none. |
| 9 | A template project without a BOM is read and the new project written as UTF-8 without BOM. The LSE template carries a UTF-8 BOM, so this only affects another base project; a pre-2016 ANSI project with accented characters would be mis-decoded. | `WdpFileService.Parse` | Re-save such a project from AutoCAD Electrical 2016 or later first. |
| 10 | `RecentProjects` is stored but no screen shows it. | `AppSettings` | Cosmetic. |
| 11 | `sections` in the JSON only supplies the default section for **Add drawing**; the grid has no drop-down. The LSE template uses no sections. | `NewProjectForm.AddDrawing` | Type the code if ever needed. |
| 12 | All drawings are written flat into the job folder; the PM folder column is a Project Manager tree folder only. | `Execute` | Matches the LSE template and AutoCAD Electrical's default layout. |
| 13 | The transaction runs on the UI thread; AutoCAD is blocked while drawings are written. | `NewProjectForm.CreateProject` | Seconds per drawing; acceptable for tens of drawings. |
| 14 | Without the vault working folder the fallback drawing template is `ACAD_ELECTRICAL.dwt`, which has no LSE title block. | `SettingsService.Load` | Diagnostics says `Vault working folder: NOT FOUND`; check the vault out rather than work around it. |
| 15 | Both a description line with role `date` and `titleBlock.dateAttribute` could target the same tag; the shipped JSON has neither. | `config/LseProjectTemplates.json` | Keep one or the other if a date line is ever added. |
| 16 | No Vault check-in. The job folder is created in the working folder only. | whole tree | The engineer checks it in by hand; the success message says so. Follow `../inventor-addin/src/FootprintTransactionService.cs` (`VaultCatalogService`) if automation is wanted. |
| 17 | The E-number series of the shipped drawing sets (E001 cover, E1xx one-line, E2xx elevations, E3xx schematics, E4xx metering, E5xx schedules) is inferred from one existing file name (`000924-1-E303.dwg`), not confirmed. | `config/LseProjectTemplates.json` `projectTypes` | Confirm against a finished job (checklist item 6) and edit the JSON. |
| 18 | The value written to WD_M `SHEET` for an E-numbered sheet is the Sheet cell text (`001`); whether LSE expects `1`, `001` or `E001` is unverified. | `Execute` (`Put(job.WdmValues, t.Wdm.SheetAttribute, r.Sheet)`) | Checklist item 7. |
| 19 | AutoCAD Electrical's acceptance of a pre-written `.aepx` is unverified; it normally writes the file itself (the template's carries `FileID` 3 and 4 and `NextID` 6, the plugin writes 1..n and n+1). | `WdpFileService.WriteConfiguration` | Checklist item 9; untick **Write .aepx** if it causes trouble. |
| 20 | The product line folder tree is a snapshot of `Locations.txt` dated 2026-11-10. | `config/LseProjectTemplates.json` `productLines` | New folders can be typed in the wizard; update the JSON when the vault tree changes. |
| 21 | The two LSE template drawings have not been inspected: that `DRAWING-#.dwg` and `STOCKCODE-E001.dwg` carry WD_M and the `ECAD_LSE_CUSTOM_TB_2.0` block with the tags the `.wdt` maps is inferred from the template `.wdt`. | `config/LseProjectTemplates.json` `titleBlock` | `LSEPROJECTDIAG` on a workstation settles it. |
