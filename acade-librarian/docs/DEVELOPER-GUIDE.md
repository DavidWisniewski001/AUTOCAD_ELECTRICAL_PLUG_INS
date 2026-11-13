# Developer guide: LSE Librarian

Maintained by DCW. Last reviewed 2026-11-12.

How to build, test, debug, extend and verify the plugin. Read [ARCHITECTURE.md](ARCHITECTURE.md) for the design and [CODE-REFERENCE.md](CODE-REFERENCE.md) for member-level detail.

## 1. State of the code

The tree was authored on a machine with no AutoCAD and no installed .NET SDK. It **compiles** (Release, 0 errors, 0 warnings) against the `AutoCAD.NET` 25.1.0 reference package using a portable .NET 8 SDK, and the parts that do not need AutoCAD passed a 66-check console smoke test against the built DLL and copies of the real LSE icon menus from `..\dat MENU`. The catalog-record addition (2026-11-10: `NamingWorkbookService`, `PartNumberService`, `CatalogTransactionService`, `CatalogRecordForm`, the wizard group, settings and diagnostics) passed a separate 67-check console smoke test against a copy of the real `LSE ELECTRICAL PARTS CATALOG MANAGER.xlsx` and a copy of the real `default_cat.mdb` (section 4.1). **Nothing has run inside AutoCAD Electrical**; the dialogs are unverified there. Treat the first `NETLOAD` as a review pass and work through section 7.

## 2. Repository layout

```
acade-librarian/
  LSE.AutoCADElectrical.Librarian.csproj   SDK-style project, net8.0-windows, x64, WinForms
  README.md                                overview, commands, output, build, deploy, gaps
  .gitignore                               bin/, obj/, *.user, .vs/
  config/LseLibraryStandard.json           the LSE library standard; copied to output
  config/LseNamingStandard.json            snapshot of the part-number naming standard parsed from the workbook; copied to output
  bundle/LSE.Librarian.bundle/PackageContents.xml   autoloader manifest
  docs/                                    this documentation set
  src/*.cs                                 eighteen source files, one namespace
```

New in the catalog-record change: `src/NamingWorkbookService.cs` (workbook reader and parser, JSON snapshot), `src/PartNumberService.cs` (format, legacy rule, next sequence, description rules), `src/CatalogTransactionService.cs` (the `default_cat.mdb` writer), `src/CatalogRecordForm.cs` (the dialog); `CatalogService` and `IssuanceLock` were added to `src/AccessCatalogService.cs`.

## 3. Build

Prerequisites: .NET 8 SDK, Windows x64. AutoCAD Electrical 2026 is optional for compiling, required for running.

```
dotnet build -c Release
```

Reference resolution in the project file:

| Condition | References |
|---|---|
| `$(AcadDir)\acdbmgd.dll` exists (default `C:\Program Files\Autodesk\AutoCAD 2026`) | `accoremgd.dll`, `acdbmgd.dll`, `acmgd.dll` from that folder, `Private=false` |
| otherwise | NuGet `AutoCAD.NET` 25.1.0 with `ExcludeAssets="runtime"` (compile only; it pulls `AutoCAD.NET.Core` and `AutoCAD.NET.Model` for `AcCoreMgd` and `AcDbMgd`) |

Override the install folder with `dotnet build -c Release -p:AcadDir="D:\Autodesk\AutoCAD 2026"`.

Package versions matter: `AutoCAD.NET` 25.0.x is AutoCAD 2025, 25.1.0 is AutoCAD 2026 on .NET 8, and 25.1.1 targets .NET 10 and fails to restore for `net8.0-windows`. Do not bump it without checking the package's target framework.

Project settings: `TargetFramework net8.0-windows`, `PlatformTarget x64`, `EnableDynamicLoading true` (plugin-style `.deps.json`), `AppendTargetFrameworkToOutputPath false` (output in `bin\Release\`), `Nullable` and `ImplicitUsings` enabled.

Building without an installed SDK: download `https://aka.ms/dotnet/8.0/dotnet-sdk-win-x64.zip`, unzip anywhere, set `DOTNET_ROOT` to that folder and run its `dotnet.exe build`. No administrator rights are needed.

## 4. Smoke test

The smoke test is a throw-away console project (not in the tree) that references the built DLL and exercises everything that does not need AutoCAD. It is worth recreating after any change to the parser, naming or transaction. What it checks, so it can be rebuilt:

| Area | Checks |
|---|---|
| Standard | `LibraryStandardService.Load` on `config\LseLibraryStandard.json`: four kinds, 46 families. |
| Icon menu | `Load` then `Bytes` of both real `.DAT` files is byte-identical to the file (UTF-8 BOM, CRLF, 86 and 53 pages); M110 title; M1 layout `D3W`; next page numbers 139 and 135; header comments kept; reachability from the LSE roots (26 pages) and breadcrumbs; `ResolveAction` for `$S=`, `$C=WD_INS_CIRC_MAIN <path>`, `$C=wd_infpx "CR"`, full path, bare block; `ResolveImage` for slide references and `IMAGES\` paths. |
| Naming | Every LSE symbol path in the schematic menu (34) matches the symbol pattern; 255 of the 264 footprint paths in the panel menu match, and the 9 that fail are genuinely irregular names (spaces, `FP-002`, `NP_002NP_002`, lowercase `mm`, `PC&S`); builders, sanitiser, parse groups, spaces rejected for symbols and allowed for circuits. |
| Project import | `ReadOptions` and `ApplyProject` on `E-CAD CUSTOM PROJECT TEMPLATE 2.wdp` set the four roots and both menu names. |
| Rule check | `WD_M` is an error, missing `MFG` a warning, `TAG1` + `DESC1` satisfy the required list. |
| Transaction | In a sandbox with copies of both menus and fake roots: file a symbol (copy, PNG from an imported image, entry appended to M110, menu grows by exactly one line, BOM kept, backup folder with menu copy and report, lock released); duplicate name, stock clash and target outside the root refused by `Validate`; footprint filed into a new category folder with a new page M135 linked from M116 reusing an existing image; a forced runtime failure (menu held open read-only during `Execute`) reports `ERROR:` and rolls back the copied drawing, the icon and the created folder, leaving the menu byte-identical and no lock; in-place registration of an orphan succeeds without copying. |
| Audit | On the sandbox: missing stock drawings reported as errors, a badly named footprint in the root flagged three ways, the registered symbol not flagged as unreferenced, missing lookup reported as a warning, report file written. |

The test program is about 220 lines; the shape is `Check(name, condition, detail)` lines in a top-level `Program.cs` with `<Reference HintPath>` to the DLL and `UseWindowsForms` so `System.Drawing` resolves. Methods that touch AutoCAD types are never called, and the DLL loads without `AcDbMgd` present because the runtime binds those assemblies only when such a method is JIT-compiled.

### 4.1 Catalog record smoke test

A second throw-away console program of the same shape (67 checks, not shipped) ran on 2026-11-10 against a copy of the real workbook and a copy of the real `default_cat.mdb` (the 64-bit Access engine is present on the authoring machine). It covered:

| Area | Checks |
|---|---|
| Workbook | `ReadSheets` and `Parse` on every `LSE ` tab: 33 tabs, 314 manufacturer codes, 152 type codes, 9,802 listed numbers, format 2+3+2+7, about 0.4 s; the two skipped tabs named; per-tab category codes, type codes, manufacturer codes, description fields, rules and examples agree with the output of the Python extractor used to study the workbook; `ManufacturerSpellings` (`BUS` → `BUSSMANN`); reading succeeds while another process holds the file open for writing (the "open in Excel" case); the cache returns the same instance until the file changes and `force` re-parses. |
| JSON | `SaveJson` then `LoadJson` round-trips every tab and code; `Load(settings)` picks the workbook when it exists and the snapshot when the workbook path is missing; the shipped `config\LseNamingStandard.json` loads. |
| Part numbers | `StandardForm`, `Split`, `Format` padding and exhaustion; `LegacyFloor` = 1,000,000; `45BUSFH1691250` is legacy, `45BUSFH0000011` is not; `Highest` ignores legacy; `Preview` on the FUSE HOLDERS tab (`0000001`..`0000011` listed) with a catalog holding the legacy `45BUSFH1691250` gives `45BUSFH0000012` and the expected counts in `Explanation`; a used sequence is skipped; a manual number must start with the prefix, be in standard form and be unused in the catalog; `DescriptionProblems` for each rule; `PurchaseDescription`. |
| Catalog | `GetTables`, `GetFamilyDescriptions`, `GetColumns` sizes, `ReadColumnStartingWith` across tables, `ReadDistinct`, `Exists`, `FindByColumn`, `FamilyHints` on the copy. |
| Transaction | `Validate` refusals (missing tab, undefined type, bad WEBLINK, over-long fields, description rules); `Execute` on the copy: lock taken and released, backup folder with the MDB copy and `CatalogReport.txt`, the row read back by MANUFACTURER + CATALOG and by part number, `Status: SUCCESS`; a second `Execute` of the same MANUFACTURER + CATALOG is refused and the catalog file is byte-identical to the backup afterwards; a hand-typed (rule 7) number is inserted and a second attempt with it refused; a held `LSE_PartNumber.lock` makes `Acquire` fail after its retries. |

Rebuild it after any change to the parser, the sequence rule or the insert. Always run it against copies: the test inserts rows.

Since 2026-11-12 a catalog-only part of this test lives in the tree at `tests\LSE_Librarian.CatalogSmokeTest\` (excluded from the plugin build by `<Compile Remove="tests\**" />`; 25 catalog checks, plus 35 footprint-mapping and audit checks when the optional third argument, a copy of `footprint_lookup.mdb`, is passed: 60 in total; the symbol-link section added the same day contributes further checks, written but not run, see below). Build the plugin in Release, then `dotnet run -c Release --project tests\LSE_Librarian.CatalogSmokeTest -- "<workbook.xlsx>" "<default_cat.mdb>"`. It copies the files to a temp folder, issues two numbers (`45BUSFH0000012`, `45BUSFH0000013` on the 2026-11-12 copies, legacy `...1691250` not counted), checks `Validate` needs no symbol or footprint, read-back by MANUFACTURER+CATALOG and by number, byte-identical backup, report, lock release, that no library or lookup file appears, refusal of a duplicate, and rollback (catalog byte-identical, report with ERROR, lock released) when the run fails after backup and lock (unknown part-number column). The symbol-link checks (set, set again as a no-op, change refused without confirmation and accepted with it, missing block refused, no matching row refused, duplicate row in a batch refused, batch rollback at the read-back line, only WDBLKNAM changed and row count unchanged, byte-identical backups, report, lock) run on the copy of `default_cat.mdb` against throw-away symbol folders. They were written without a .NET SDK on the PC and have not been compiled or run; build and run them before relying on the symbol link.

The footprint-mapping part (2026-11-12) runs on copies of the real `footprint_lookup.mdb` (519,000 rows) and the catalog: `Load` and its statuses, `Validate` refusals (missing drawing, outside the library, not a `.dwg`, blank key, missing MDB), create (new manufacturer table), byte-identical backup, report, lock release, read-back, refusal of a different path without `Overwrite` (MDB byte-identical, no backup folder), no-op on the same path, update with confirmation (still one row), reuse of an existing table whatever the spelling, a two-row batch rolled back byte-identical when a failure is raised after the writes (from the progress callback at the read-back line), the same batch succeeding, repeat refusal, `default_cat.mdb` untouched, and the full `LibraryAuditService.Run` producing one Info note per family with unmapped parts while leaving both MDBs unchanged. Run it with the three arguments: workbook, `default_cat.mdb`, `footprint_lookup.mdb`; the audit step reads every lookup row and takes several minutes. `Load` takes about six seconds on the real data.

## 5. Deploy and debug

Deploy as in [OPERATIONS.md](OPERATIONS.md) section 2. For development:

1. Build Debug. In AutoCAD Electrical type `NETLOAD` and pick `bin\Debug\LSE.AutoCADElectrical.Librarian.dll`. AutoCAD locks the DLL until it exits, so each rebuild needs a restart of AutoCAD (or a copy-and-load script).
2. Attach Visual Studio to `acad.exe` (Debug, Attach to Process, .NET Core code type). Breakpoints in `LibraryTransactionService.Execute`, `DrawingInspectionService.ExportBlock` and `IconRenderService.RenderDrawing` are the useful ones.
3. `LSELIBRARYDIAG` is the quickest way to see what the plugin resolved on this machine.
4. Point every folder setting at a scratch copy of `ECAD Standards` while testing. Rollback restores only what a run touched, but a scratch copy keeps the Vault working folder clean until the first-build checklist is done.

## 6. Conventions in use

The code follows `..\STYLE-GUIDE.docx` and the sibling projects:

- File-scoped namespace. Every class `sealed` or `static`. No interfaces, inheritance or dependency injection.
- Two densities. Compact (one member per line, one-space indent, short locals `s`, `q`, `r`, `p`, `x`, `t`): `Models.cs`, `SettingsService.cs`, `AccessCatalogService.cs`, `NamingService.cs`, `NamingWorkbookService.cs`, `PartNumberService.cs`, `IconMenuService.cs`, `LibraryTransactionService.cs`, `CatalogTransactionService.cs`, `LibraryAuditService.cs`, `DiagnosticsService.cs`, `LibrarySettingsForm.cs`, `Plugin.cs`. Expanded (4-space, descriptive names, `private` written out): `DrawingInspectionService.cs`, `IconRenderService.cs`, `LibrarianForm.cs`, `CatalogRecordForm.cs`, `AuditForm.cs`. Do not mix within a file.
- One writer per store: `LibraryTransactionService.Execute` for libraries, menus and the lookup; `CatalogTransactionService.Run` for `default_cat.mdb`. Anything that needs a catalog row goes through `Run`, never through `CatalogService.AddRecord` directly.
- The workbook is read with the framework only (`System.IO.Compression`, `System.Xml`); do not add an Excel or OpenXML package.
- Every text comparison is `StringComparison.OrdinalIgnoreCase`; dictionaries keyed by names use `StringComparer.OrdinalIgnoreCase`.
- Errors are `InvalidOperationException`, `FileNotFoundException` or `DirectoryNotFoundException` with a sentence the engineer can act on. Caught only at command methods and button handlers; shown with `MessageBox.Show`.
- Mutating work produces a line-per-step log that ends `Status: SUCCESS` and is written to a report file.
- Settings live in one `AppSettings` POCO, saved through `SettingsService.Save` (temp file plus move). Any new setting: add the property, add a row in `LibrarySettingsForm` (`Add` or `Check`) and its line in `Collect`, read it from `s.` where needed.
- COM through `dynamic` with `Marshal.FinalReleaseComObject` in `finally`; no `System.Data.OleDb`.
- Commands are `LSE` + upper-case name, no separators, on the single `Commands` class.
- No XML doc comments, regions or pragmas. No NuGet packages at runtime.

## 7. First-build verification checklist

Do these on a workstation with AutoCAD Electrical 2026, against a scratch copy of the `ECAD Standards` folder first.

1. `NETLOAD` the DLL; run `LSELIBRARYDIAG`. Every path `(exists)`, both menus `parse: PASS`, root pages `PASS`, `Footprint lookup read: PASS`.
2. `LSELIBRARIAN`, kind schematic symbol, source an existing LSE symbol file (for example `HDV1_003-SYM_ROGOW_COIL.dwg`). **Inspect drawing**: attributes listed, extents sensible, `Standard check: PASS` or only warnings. This proves `Inspect` (side database, `GeometricExtents`, `OriginalFileVersion`, `ThumbnailBitmap`).
3. Step 3, **Preview icon** with Render: the symbol outline appears. Try "embedded preview image". This proves `RenderDrawing` sampling and `Thumbnail`.
4. File it under a new test name on page M110 with a rendered icon. Open Insert Component in AutoCAD Electrical, go to LSE COMPONENTS: the entry and icon appear; inserting places the symbol and prompts for its attributes. This proves the menu edit is accepted without restarting AutoCAD (if the entry does not appear until AutoCAD is restarted, note it in OPERATIONS.md).
5. Open a drawing containing a block, run `LSELIBRARIAN` with "block definition in the current drawing", **Pick in drawing**, **Inspect drawing** (temp WBLOCK), file it. Open the filed DWG: entities in model space, base point at the origin, attributes present. This proves `Wblock(ObjectId)` and `SaveAs(AC1032)`.
6. File a panel footprint with lookup registration (a test MANUFACTURER). Check the table and row in `footprint_lookup.mdb`; assign the catalog number to a schematic component and insert its footprint from the panel. This proves `EnsureTable` and `Upsert` against the real database.
7. File a user circuit; insert it from the LSE CIRCUITS page. This proves the `$C=WD_INS_CIRC_MAIN <path>` entry.
8. Create a new page under M116 for a footprint; check the parent link and the page title render as expected (AutoCAD Electrical shows the page title at the top).
9. Run `LSELIBRARYAUDIT` on the real folders (not deep first). Read the warnings; confirm each "outside every configured library folder" and "not referenced" finding is real. Then run deep on the symbol library only (set the other roots to an empty folder temporarily) and time it.
10. Force a failure: make a `.DAT` read-only, file an item; confirm the error, the rollback lines, that the copied drawing is gone and the menu unchanged. Make it writable again.
11. Try the lock: start a run on two workstations within a few seconds; the second should wait and then succeed, or report the lock after 10 seconds.
12. Check the changed files in to Vault and confirm the `_V` folders did not confuse the audit (they are ignored by name).
13. Catalog records, against a COPY of `default_cat.mdb` first: point "Catalog MDB" at the copy in `LSELIBRARYSETTINGS`, press **Test** (workbook parses with 33 tabs or so, catalog opens), **Save**. Run `LSECATALOGADD`: the dialog opens with the workbook as source, the tab list fills, the family list shows `CODE  description`. Pick `LSE FUSE HOLDERS`, `45`, `BUS`, `FH`, **Preview next number**: the number and explanation match a manual count of `USER1` values in the catalog copy and the tab. Add a test record; check the row in the family table, the backup folder, the report and that the lock file is gone. Try a duplicate MANUFACTURER + CATALOG: refused, catalog unchanged. This proves the WinForms dialog inside AutoCAD, ADODB against the real catalog layout and the issuance lock on a workstation.
14. In the wizard, file a test symbol with "Also add a catalog record" ticked, still against the catalog copy: the embedded dialog is pre-filled (description, `WDBLKNAM` = the symbol name), **Use in Librarian** fills the summary line, the review shows the catalog line, the run logs the catalog step last and the success message shows the number. Force a failure in the catalog step (make the catalog copy read-only): the log shows `Catalog rollback`, then the library rollback, and neither the menu nor the drawing was left changed.
15. Open AutoCAD Electrical's Catalog Browser on the catalog copy and confirm the new row is listed and inserts. Only then point "Catalog MDB" back at the live catalog, and press **Save naming snapshot (JSON)** so the shipped snapshot matches the current workbook.

## 8. Recipes

### Add a category folder or default page
Edit `kinds.<Kind>.categories` in the JSON: `name`, `folder` (relative to the root), `menuPage`. No code change.

### Change a naming rule
Edit `namePattern` (keep the named groups the wizard reads: `orientation`, `family`, `role`, `variant`, `prefix`, `manufacturer`, `series`, `description`), `nameExample` and `nameHelp`. The name builder in `LibrarianForm.ComputeName` composes the parts with `NamingService.SymbolName` or `FootprintName`; change those if the composition changes, not just the regex.

### Require another attribute
Add the tag to `requiredAttributes` (`A|B` for alternatives) or `recommendedAttributes`. `RuleCheckService.Check` reads them; the inspection pane and the deep audit follow.

### Add a kind
Add a `LibraryKind` value, a `kinds.<Kind>` section, a `RootFor` case in `AppSettings`, a `KindFor` rule in `NamingService`, and the wizard rows in `LibrarianForm.KindChanged`. The audit's `AuditFolder` takes the root and kind.

### Add a setting
Property on `AppSettings`, row in `LibrarySettingsForm` constructor and `Collect`, read it from `s.`.

### Draw text in icons
`IconRenderService.Collect` skips everything that is not a `Curve`. Add a branch for `DBText` / `MText` that draws with `Graphics.DrawString` after the polylines, scaling the height by the same factor.

### Vault check-in after a run
Follow `..\inventor-addin\src\FootprintTransactionService.cs` (`VaultCatalogService`): load the three Vault SDK assemblies by reflection, check out the `.DAT` before step 8 and add or check in the DWG, PNG and `.DAT` after step 10, undoing the checkout on rollback. Keep it optional behind a blank setting. The same applies to `default_cat.mdb` after the catalog step.

### Change the legacy floor
`PartNumberService.LegacyFloor` returns 10^(SequenceChars - 1); `IsLegacySequence` is the only place that uses it, and `Highest` and `Preview` call that. To treat, say, only sequences at or above 1,500,000 as legacy, change `LegacyFloor` (or make it a setting read there). The explanation text in `Preview` ("legacy ERP number(s) with 7 significant digits kept under rule 7") should be reworded to match. Re-run the catalog smoke test: the FUSE HOLDERS case must still give `45BUSFH0000012`.

### Add a catalog column
`CatalogAddRequest` gets the property; `CatalogService.AddRecord` adds a `(column, value)` pair to its `values` list (it is written only when the table has the column and the value is non-empty, with the defined size enforced); `CatalogTransactionService.Validate` adds the length check; `CatalogRecordForm` adds a text box row, the prefill line in `ApplyPrefill` and the line in `CatalogRequestFromForm`. Nothing else changes; the insert statement is built from the list.

### Refresh the naming snapshot
Settings form, **Save naming snapshot (JSON)** on a workstation that can reach the workbook; copy the resulting `LseNamingStandard.json` into `config\` so the next build ships it. The smoke test can do the same with `NamingWorkbookService.LoadWorkbook` + `SaveJson`.

### Teach the parser a new workbook layout
`NamingWorkbookService.Parse` finds everything by the cell texts `Part number`, `Description:`, `Purchase Desc`, `Part # Segment` (with `Manufacturer` and `Code` on the same row), `Category`, `Type`, `Description Field`, `Rules`, `Examples`, `APPENDIX`, `Internal ID`, `LSE Part Number`, `LSE Recommended Part Number`, `Manufacturer`. If the catalog manager renames a header, change the matching text there; the row and column positions are not hard-coded except the default manufacturer columns (F and G) used when the segment header row lacks the two headers.

## 9. Test strategy

- Pure logic (`IconMenuService`, `NamingService`, `SettingsService.ReadOptions`, `RuleCheckService`, `LibraryTransactionService` with file sources, `LibraryAuditService` without deep) is covered by the console smoke test in section 4 and can be re-run on any machine with the SDK.
- The workbook reader, the part-number rules and the catalog transaction (`NamingWorkbookService`, `PartNumberService`, `CatalogService`, `CatalogTransactionService`) are covered by the catalog smoke test in section 4.1, which needs a copy of the workbook, a copy of `default_cat.mdb` and the 64-bit Access engine.
- AutoCAD-dependent code (`DrawingInspectionService`, `IconRenderService.RenderDrawing`, `ThumbnailIcon`) and every dialog inside AutoCAD (`LibrarianForm`, `CatalogRecordForm`, `AuditForm`, `LibrarySettingsForm`) are covered only by the first-build checklist.
- The lookup database code is a copy of the Inventor add-in's; it has not been run against a real `footprint_lookup.mdb` from either project. The catalog code has been run against a copy of the real `default_cat.mdb`.

## 10. Known gaps

- Unverified AutoCAD API behaviour: `Database.Wblock(ObjectId)` output, `ThumbnailBitmap` for AutoCAD Electrical-saved files, `Curve.GetPointAtParameter` sampling on every entity type, nested-block transform order, and whether the icon menu picks up an appended entry without restarting AutoCAD Electrical.
- No Vault integration; no rename or move of existing items; the audit only reports. A changed `default_cat.mdb` is checked in by hand.
- The legacy-floor rule (`PartNumberService.LegacyFloor`, sequence at or above 1,000,000 = legacy ERP number, reserved but not counted) is inferred from the numbers found in the workbook and the catalog, not stated in the workbook. If a legacy number with a small tail ever turns up it would move the counter.
- Workbook tabs without a "Part # Segment" table or without type codes (`LSE LABELS & NAMEPLATES`, `LSE CHEMICALS & CONSUMABLES` on 2026-11-10) are skipped; the parser does not invent a naming block for them.
- The parser relies on the header texts listed in the recipe above; a reorganised workbook silently drops what it cannot find (a tab then appears in `SkippedTabs` or with fewer codes). Diagnostics show the counts so a drop is visible.
- The catalog dialog, the wizard's catalog group and `LSECATALOGADD` have not been shown inside AutoCAD; the smoke test drives the services, not the forms. The catalog-only banner on the stand-alone dialog is likewise unseen in AutoCAD.
- `FootprintMapForm`, the optional footprint box in `LSECATALOGADD` and `LSEFOOTPRINTMAP` itself have not been shown inside AutoCAD; the smoke test drives `FootprintMappingService`, not the forms. Whether AutoCAD Electrical resolves a footprint from the `footprint_lookup.mdb` row the Librarian writes (plain row, `BLKNAM` = full DWG path) when a part is inserted by manufacturer and catalog number is unverified; the existing LSE rows use full paths, which is why that form was chosen. Stock rows use bare block names; whether a bare name for a library footprint would resolve through the project footprint search path was not checked.
- `FootprintMappingService.Execute` is a second writer of `footprint_lookup.mdb` next to `LibraryTransactionService` (same `LSE_Library.lock`, own backup and rollback). The Inventor add-in also writes this MDB but takes only `LSE_PartNumber.lock` (`..inventor-addinsrcFootprintTransactionService.cs`), never `LSE_Library.lock`, so a simultaneous Inventor run and a mapping run are not excluded from each other; this applies equally to the library transaction. Mapping rows are never deleted by the Librarian.
- `LSEFOOTPRINTMAP` loads every LSE part (about 6,650 on 2026-11-12, about six seconds) on the UI thread and must be listed in `bundle\LSE.Librarian.bundle\PackageContents.xml` for auto-load (the bundle is outside this plugin's write territory). The symbol side of the pairing (`WDBLKNAM`) can be set or changed on existing rows since 2026-11-12 (user-approved second entry in `CatalogTransactionService`); the symbol column and Set symbol are not shown inside AutoCAD, and whether AutoCAD Electrical picks up a changed WDBLKNAM for a part already placed in a project is unverified. The symbol-link code has not been compiled (no SDK on this PC when it was written).
- The Librarian does not write the issued number back into the workbook; the catalog manager adds it to the tab by hand (otherwise the workbook's "numbers listed" lags the catalog, which is harmless for issuance because the catalog is read every time).
- The meaning of the three-digit series and the `FPT` prefix is inferred from existing names.
- `maxNameLength` 64 is a guess; AutoCAD allows 255-character block names and AutoCAD Electrical's practical limit was not checked.
- The icon menu layout line (`D3W`) is preserved but not interpreted; new pages are written without one, like the existing LSE pages.
- The audit does not check that menu entries use the images folder name consistently (`IMAGES` versus `Images`); Windows is case-insensitive so it does not matter on NTFS.
- Nameplate attribute requirements are empty (only recommendations) because the LSE nameplate convention was not available in the inputs.
