# Architecture: LSE Librarian (AutoCAD Electrical 2026 plugin)

Maintained by DCW. Last reviewed 2026-11-12.

LSE Librarian is a .NET plugin that runs inside AutoCAD Electrical and files a new schematic symbol, panel footprint, nameplate or saved user circuit into the LSE library: it copies (or WBLOCKs) the drawing into the right folder under a standard name, writes an icon image, appends an entry to the right icon menu page and, for footprints, inserts the `footprint_lookup.mdb` row. Optionally, and also on its own with `LSECATALOGADD`, it adds the part to the AutoCAD Electrical catalog `default_cat.mdb` under a new LSE part number whose naming scheme is read straight from the LSE parts catalog workbook. It exists because engineers were saving symbols and footprints wherever they happened to be working, with ad-hoc names and no icon-menu entry, so nobody else could find or insert them. A second command audits the libraries and menus and reports everything that does not fit. It is the AutoCAD Electrical counterpart of the Inventor add-in in `../inventor-addin` and shares its shape: a wizard, a JSON standard, a logged transaction with rollback.

Related documents: [CODE-REFERENCE.md](CODE-REFERENCE.md), [DATA-STORES.md](DATA-STORES.md), [OPERATIONS.md](OPERATIONS.md), [DEVELOPER-GUIDE.md](DEVELOPER-GUIDE.md), [GLOSSARY.md](GLOSSARY.md).

## 1. What problem it solves

AutoCAD Electrical finds a symbol or footprint in three places that must agree: a DWG file in a folder on the project's library search path (`+[1]` and `+[3]` in the `.wdp`), an entry in an icon menu `.DAT` file (`+[2]` and `+[4]`) that names the drawing and an icon image, and, for catalog-driven footprint insertion, a row in `footprint_lookup.mdb`. The LSE standard adds a naming convention (`HDV1_002-...`, `FP_002-...`, `NP_002-...`) and category folders. Doing all of this by hand means editing a 1000-line text file in the Vault working folder, so it was skipped, and `Locations.txt` records the result: "Engineers dump symbols all over no organization". The Librarian asks for the facts once, validates them against the standard, and performs every write in one run that is logged and undone on failure.

## 2. Components

One .NET 8 assembly, `LSE.AutoCADElectrical.Librarian.dll`, loaded into AutoCAD Electrical with `NETLOAD` or a `.bundle`. Twenty-four source files (four of them added on 2026-11-12 when the catalog browser was merged in), one namespace `LSE.AutoCADElectrical.Librarian`.

| Layer | File | Responsibility |
|---|---|---|
| Host integration | `src/Plugin.cs` | Registers the eight `LSE...` commands, opens the forms, hands an audit pick to the wizard and a picked block to the catalog browser (`Browse`). |
| User interface | `src/LibrarianForm.cs` | The four-step wizard, drawing inspection, name builder, page picker, icon preview, catalog record group, log pane. |
| User interface | `src/CatalogRecordForm.cs` | The catalog record dialog: workbook tab and codes, part-number preview, family table, catalog fields, description builder. Stand-alone (`LSECATALOGADD`) it writes the record; embedded in the wizard it hands the request back. |
| User interface | `src/CatalogBrowserForm.cs` | The catalog browser (`LSECATALOG`, `LSEASSIGNCOMPONENT`): scope tree, family and filter combos, record grid, attribute change preview, favourites, company standards, New record..., assign. Reads the catalog; writes only block attributes (through `ComponentAssignService`) and settings. |
| User interface | `src/AuditForm.cs` | Runs the audit, shows findings in a filterable grid, exports the report, hands a drawing to the wizard. |
| User interface | `src/LibrarySettingsForm.cs` | Edits and tests every setting; reads library paths from a `.wdp`; writes the naming snapshot JSON. |
| Component assignment | `src/ComponentAssignService.cs` | Prompts for a block, reads its attributes, builds the proposed attribute values from a catalog record, applies them in one locked transaction, warns on FAMILY or WDBLKNAM mismatch. The only code in the plugin that writes attributes of a drawing block. |
| Company standards | `src/CompanyStandardsService.cs` | `CompanyStandardsService` (load, save, find, toggle `CompanyStandards.json`) and `FamilyNames` (friendly family names). |
| Vault | `src/VaultCatalogService.cs` | Optional checkout, checkin and undo of `default_cat.mdb` through the Vault SDK loaded by reflection. Called only from `CatalogTransactionService`. |
| Orchestration | `src/LibraryTransactionService.cs` | Validation, the single mutating run for library items, backups, lock, rollback, the catalog step. `LibraryLock`. |
| Orchestration | `src/CatalogTransactionService.cs` | The only writer of `default_cat.mdb` (`Run` adds a record; since 2026-11-12 `SetSymbols` updates WDBLKNAM on existing rows, same backup, lock, read-back and rollback): validation, preview, family usage, backup, issuance lock, optional Vault checkout and checkin, number computation, insert, read-back, restore on failure, `CatalogReport.txt`. |
| Naming workbook | `src/NamingWorkbookService.cs` | Reads the LSE parts catalog workbook (`.xlsx` as a zip of XML) and parses the naming blocks of every `LSE ` tab; loads and saves the JSON snapshot; caches per file. |
| Part numbers | `src/PartNumberService.cs` | Format, validation, legacy floor, next-sequence computation, preview explanation, description rules, purchase description. |
| Icon menus | `src/IconMenuService.cs` | Parses, renders and edits `.DAT` icon menu files without disturbing anything it did not add. |
| Naming | `src/NamingService.cs` | Validates and builds standard names; decides which library a path belongs to. |
| AutoCAD database | `src/DrawingInspectionService.cs` | Opens a DWG as a side database to list attributes, nested blocks, extents and the preview; lists and picks blocks; WBLOCK export. |
| Icon images | `src/IconRenderService.cs` | Renders the drawing's curves to a PNG, or fits the DWG preview or an imported image. |
| Audit | `src/LibraryAuditService.cs` | `LibraryAuditService` (menus, folders, lookup, images) and `RuleCheckService` (attribute rules against a `DrawingInfo`). |
| Lookup and catalog databases | `src/AccessCatalogService.cs` | `Ace` (ADODB helper), `FootprintLookupService` (table create, find, upsert, read all paths), `CatalogService` (family tables, columns, part-number reads, exists, insert, family hints, and since 2026-11-12 `ReadTable` and `FindByKey` for the browser) and `IssuanceLock`. |
| Configuration | `src/SettingsService.cs` | `SettingsService` (settings.json, path and workbook discovery, `.wdp` import) and `LibraryStandardService` (the JSON standard). |
| Diagnostics | `src/DiagnosticsService.cs` | Builds the `LSELIBRARYDIAG` report, including the naming standard, catalog, company standards, browser and Vault lines. |
| Data shapes | `src/Models.cs` | Settings, standard, request, result, drawing info, icon menu model, audit findings, naming standard, part-number and catalog request/result types, and the browser types (`CatalogRecord`, `StandardEntry`, `SelectedBlockInfo`, `AttributeChange`, `CatalogScope`). |

Supporting files:

| File | Purpose |
|---|---|
| `LSE.AutoCADElectrical.Librarian.csproj` | SDK-style project, `net8.0-windows`, x64, WinForms. AutoCAD assemblies from the install when present, otherwise the `AutoCAD.NET` 25.1.0 reference package at compile time only. |
| `config/LseLibraryStandard.json` | The LSE library standard: naming rules, attribute rules, categories, menu root pages, family codes. Copied to the output folder. |
| `config/LseNamingStandard.json` | Snapshot of the part-number naming standard parsed from the workbook (same shape as the Inventor add-in's file). Copied to the output folder; used only when the workbook cannot be reached. |
| `bundle/LSE.Librarian.bundle/PackageContents.xml` | Autoloader manifest for `%APPDATA%\Autodesk\ApplicationPlugins`; lists all eight commands. |

## 3. Dependency diagram

```
 AutoCAD Electrical 2026 (acad.exe, x64, .NET 8)
 |
 |  NETLOAD or ApplicationPlugins bundle
 v
 Commands (Plugin.cs)
 |-- LSELIBRARIAN -------> LibrarianForm (modal)
 |                           |-- SettingsService.Load, LibraryStandardService.Load
 |                           |-- DrawingInspectionService.Inspect / ListBlockNames / PromptForBlock / ExportBlockToTemp
 |                           |-- RuleCheckService.Check (inspection pane)
 |                           |-- NamingService (name builder, validation)
 |                           |-- IconMenuService.Load / Tree (page picker)
 |                           |-- IconRenderService.Build (icon preview)
 |                           |-- CatalogRecordForm (embedded, "Catalog record...") -> PartNumberRequest + CatalogAddRequest
 |                           |     |-- NamingWorkbookService.Load (workbook or JSON snapshot)
 |                           |     |-- CatalogService.GetTables / GetFamilyDescriptions / ReadDistinct
 |                           |     '-- CatalogTransactionService.Preview / FamilyUsage (read-only)
 |                           '-- LibraryTransactionService.Execute ("Add to library")
 |                                 |-- Validate (includes CatalogTransactionService.Validate when a record is requested)
 |                                 |-- LibraryLock.Acquire ............ <dat MENU>\LSE_Library.lock
 |                                 |-- File.Copy backups .............. <BackupFolder>\<stamp>\
 |                                 |-- File.Copy | DrawingInspectionService.ExportBlock   -> <root>\<category>\<Name>.dwg
 |                                 |-- IconRenderService.WriteIcon ..... <dat MENU>\IMAGES\<Name>.png
 |                                 |-- IconMenuService AddPage/AddEntry/Save -> ACE_NFPA_MENU.DAT | ACE_PANEL_MENU_NFPA.DAT
 |                                 |-- FootprintLookupService.EnsureTable/Upsert -> footprint_lookup.mdb (optional)
 |                                 |-- CatalogTransactionService.Run -> default_cat.mdb (optional, last)
 |                                 |     |-- IssuanceLock.Acquire ......... <catalog folder>\LSE_PartNumber.lock
 |                                 |     |-- File.Copy backup ............. <LSE_Catalog_Backups>\<stamp>\default_cat.mdb
 |                                 |     |-- VaultCatalogService.Checkout / Checkin / UndoCheckout (only when CatalogVaultPath is set)
 |                                 |     |-- CatalogService.ReadColumnStartingWith + PartNumberService.Preview
 |                                 |     '-- CatalogService.AddRecord, Exists, FindByColumn (read-back)
 |                                 '-- LibraryReport.txt (backup folder), settings.RecentItems
 |-- LSECATALOGADD ------> CatalogRecordForm (modal, stand-alone)
 |                           '-- CatalogTransactionService.Execute -> Run (as above) + CatalogReport.txt
 |-- LSEFOOTPRINTMAP ----> FootprintMapForm (modal)
 |                           '-- FootprintMappingService.Load (read only: CatalogService.ReadParts + FootprintLookupService.ReadMappings)
 |                           '-- FootprintMappingService.Execute -> LibraryLock, backup, EnsureTable/SetMapping, FindPlain read-back, FootprintMapReport.txt
 |                         (LSECATALOGADD can call the same Execute after its catalog insert; LibraryAuditService calls Load)
 |-- LSECATALOG ---------> CatalogBrowserForm(settings, null) (modal, via Plugin.Browse)
 |-- LSEASSIGNCOMPONENT -> ComponentAssignService.PromptForBlock -> CatalogBrowserForm(settings, block)
 |                           |-- CatalogService.GetTables / GetFamilyDescriptions / ReadTable (read only, 20,000 rows per table)
 |                           |-- CompanyStandardsService.Load / Save / Toggle -> CompanyStandards.json
 |                           |-- ComponentAssignService.Preview / Apply -> attributes of the picked block (UNDO reverses it)
 |                           |-- SettingsService.Save (Favorites, Recent)
 |                           '-- "New record..." -> CatalogRecordForm -> CatalogTransactionService (the only catalog writer)
 |-- LSELIBRARYAUDIT ----> AuditForm (modal) -> LibraryAuditService.Run
 |                           |-- IconMenuService (both menus), NamingService, FootprintLookupService.ReadAllPaths
 |                           '-- DrawingInspectionService.Inspect + RuleCheckService (deep option only)
 |                         "Open in Librarian" -> LibrarianForm(path)
 |-- LSELIBRARYSETTINGS -> LibrarySettingsForm -> SettingsService.Save / ApplyProject
 |                           '-- "Save naming snapshot (JSON)" -> NamingWorkbookService.LoadWorkbook + CatalogService.FamilyHints + SaveJson
 '-- LSELIBRARYDIAG -----> DiagnosticsService.BuildReport (also NamingWorkbookService.Load, CatalogService.GetTables)
```

External systems touched: the file system (library folders, icon menu folder, backups, settings, temp exports, the parts catalog workbook read as a zip), AutoCAD's database API in-process (side databases, WBLOCK, editor prompt), ADODB over COM for `footprint_lookup.mdb` and `default_cat.mdb`. No Excel automation, no network protocol beyond SMB file access, and Vault only when `CatalogVaultPath` is set (the Vault SDK web services, called through `VaultCatalogService`).

## 4. End-to-end data flow

1. `LSELIBRARIAN` opens the wizard. On `Shown` it loads `settings.json` and the library standard JSON, builds the kind buttons from the JSON, fills the family list, and, when the audit handed over a path, pre-selects the kind from the folder the path lives in and fills the name builder from the file name.
2. Step 1 collects the kind and the source: a `.dwg` file anywhere, or a block definition in the current drawing. **Inspect drawing** opens the source as a side database (a block is first WBLOCKed to `%TEMP%\LSE_Librarian\`) and lists attributes, nested blocks, extents and the rule check.
3. Step 2 builds the name from parts (orientation, family, parent/child, variant, series, description for symbols; prefix, series, manufacturer, description for footprints and nameplates; a free name for circuits) or accepts a typed name, validates it against the JSON pattern and the folder, and shows the target path. The category list is the JSON categories plus any existing folder under the root.
4. Step 3 loads the icon menu that matches the kind, lists the pages reachable from the kind's root pages with breadcrumbs, offers to create a new page under a parent, takes the label, the icon source (render, DWG preview, existing image, imported image, none) with a preview, the lookup fields for footprints, and, for symbols, footprints and nameplates when `OfferCatalogRecord` is on, the group "Catalog record (default_cat.mdb, LSE part number from the parts catalog workbook)". Ticking "Also add a catalog record to default_cat.mdb with a new LSE part number" (or pressing **Catalog record...**) opens `CatalogRecordForm` embedded, pre-filled with MANUFACTURER and catalog number from the lookup boxes, the plain description, and `WDBLKNAM` = the symbol name for schematic symbols. The dialog loads the naming standard (`NamingWorkbookService.Load`: workbook first, JSON snapshot second), reads the family tables of the catalog, and lets the user preview the next number (`CatalogTransactionService.Preview`, read-only). **Use in Librarian** returns a `PartNumberRequest` and a `CatalogAddRequest`; the wizard copies MANUFACTURER and catalog number into the lookup boxes when they are still empty and shows a summary line. Moving on validates the record with `CatalogTransactionService.Validate`.
5. Step 4 renders a review (the catalog line reads `Catalog record <table> | <MANUFACTURER> | <catalog>  part number <prefix>+ next sequence, issued at run time`). **Add to library** builds a `LibraryRequest` and calls `LibraryTransactionService.Execute`, echoing every log line into the review pane. The success message adds the issued number (`LibraryResult.PartNumber`) when a record was written.
6. `LSECATALOGADD` opens `CatalogRecordForm` stand-alone. **Add to catalog** validates, warns about description problems, previews if not yet done, confirms, then runs `CatalogTransactionService.Execute` with the log echoed into the dialog's run log; the message box shows the issued number and the backup folder.
7. `LSELIBRARYAUDIT` runs `LibraryAuditService.Run` on open, lists findings, and when the user picks a drawing finding and presses **Open in Librarian** the audit form closes and the command opens the wizard on that drawing; a drawing already inside its library is registered in place (not copied).
8. `LSEFOOTPRINTMAP` opens `FootprintMapForm`, which loads every LSE part of `default_cat.mdb` with its lookup state (read only). Mapping validates the chosen DWG, asks before replacing, then runs `FootprintMappingService.Execute`: lock, backup of `footprint_lookup.mdb`, `EnsureTable` and `SetMapping` per part, read-back, report; any failure restores the MDB. It is the second writer of the lookup MDB after the library transaction's step 14, never concurrent with it (both hold `LSE_Library.lock`). `LSECATALOGADD`'s optional footprint box calls the same `Execute` after the catalog insert, as a separate transaction.

9. `LSECATALOG` opens `CatalogBrowserForm` with no block; `LSEASSIGNCOMPONENT` first asks for a block (`ComponentAssignService.PromptForBlock`) and opens the browser with it. The browser loads the family list and the company standards, reads the selected family table (`CatalogService.ReadTable`), and filters in memory. Selecting a record fills the attribute change preview; **Assign component** applies it (section 5.2).

## 5. The mutating transaction

`LibraryTransactionService.Execute` is the only code that writes to the libraries and the menus. It and `FootprintMappingService.Execute` (2026-11-12; same lock, backup and rollback discipline, a smaller transaction with no drawing or menu step) are the only code that writes `footprint_lookup.mdb`; it delegates the optional catalog record to `CatalogTransactionService.Run`, the only code that writes `default_cat.mdb`. `CatalogTransactionService.SetSymbols` (2026-11-12, user-approved) is the second entry into that same class: `ResolveSymbol` (the block must be a .dwg in the LSE or stock symbol library), one-row match by family table + part column + MANUFACTURER + CATALOG, refuse when the row is missing or ambiguous or when a non-empty WDBLKNAM would be replaced without Overwrite, backup to `<CatalogBackupRoot>\<stamp>_symbollink\`, `IssuanceLock`, `UPDATE ... SET [WDBLKNAM]` only, read-back, `SymbolLinkReport.txt`, restore on failure. Order of operations of the library transaction:

| Step | Action | On failure |
|---|---|---|
| 1 | `Validate(request)`: name pattern and length, target folder inside the kind's root, source exists (or block chosen), target does not exist unless in place, name unique across the whole root (and not a stock NFPA symbol name), menu file exists, page (or parent page and new title) exists, label present and unique on the page, icon choice consistent, image name free, lookup fields when registering, backup folder set; when a catalog record is requested: not a user circuit, both requests present, and `CatalogTransactionService.Validate` (section 5.1). | Throws before anything is touched. |
| 2 | Create `<BackupFolder>\<yyyyMMdd_HHmmss>\`. | Throws. |
| 3 | `LibraryLock.Acquire`: exclusive, delete-on-close `LSE_Library.lock` in the icon menu folder, 40 tries at 250 ms. | Throws ("Another workstation is changing the icon menus right now"). |
| 4 | Copy the `.DAT` (and `footprint_lookup.mdb` when registering) into the backup folder. | Throws. |
| 5 | Create the target folder if needed (remembered). | Rollback. |
| 6 | In place: log only. File source: `File.Copy` (no overwrite), clear read-only. Block source: `DrawingInspectionService.ExportBlock` (WBLOCK, saved as AC1032). Verify the file exists. | Rollback. |
| 7 | Icon: none (log), existing image (reference `IMAGES\<file>`), or render / preview / import to `IMAGES\<Name>.png`. | Rollback. |
| 8 | Load the menu, optionally `AddPage` (next free `M<n>` plus a `$S=M<n>` link entry on the parent), `AddEntry` with the kind's action template, re-parse the rendered text to prove the entry survives, `Save` (tmp file then move). | Rollback. |
| 9 | Footprints with registration: `EnsureTable(MANUFACTURER)`, `Upsert(CATALOG, BLKNAM = full path, DESCRIPTION)`, read back. | Rollback. |
| 10 | Catalog record requested: `NamingWorkbookService.Load`, then `CatalogTransactionService.Run` (section 5.1), which backs up and, on its own failure, restores `default_cat.mdb` before rethrowing; `LibraryResult.PartNumber` is set. | `Run` has already restored the catalog; the library rollback then undoes steps 5 to 9. |
| 11 | Log `Status: SUCCESS`, write `LibraryReport.txt` into the backup folder, push the path onto `RecentItems` (25 kept), save settings. | Not rolled back. |

Rollback, in order: restore the `.DAT` from its backup copy if it was written, restore the lookup MDB if it was written, delete the written PNG, delete the copied or exported DWG (never an in-place drawing), remove the created folder if it is empty, write `LibraryReport.txt` with the error, then rethrow `InvalidOperationException` whose message is the whole log so the wizard shows it. The lock is released when `Execute` returns or throws. The catalog step is deliberately last: it is the only step with its own backup and restore, so a failure there is undone by `Run` itself and the ordinary library rollback covers everything before it, and no catalog row can exist for an item that was not filed.

### 5.1 The catalog transaction

`CatalogTransactionService.Run` (called from step 10 above, and from `Execute` for `LSECATALOGADD`):

| Step | Action | On failure |
|---|---|---|
| 1 | `Validate`: catalog path set and exists, part column name valid, workbook tab chosen, category (digits), manufacturer code (letters) and type code (letters) of the right widths, type code defined on the tab, manual number in standard form and starting with the prefix, family table chosen and valid, MANUFACTURER and CATALOG present (24 and 60 at most), description rules (required, ALL CAPS, no N/A, no degree/diameter/prime symbols, 150 at most; the 100-character rule is only a warning), TYPE/RATING/VOLTAGE/USER2/USER3 100, TEXTVALUE and WEBLINK 255, WDBLKNAM 60, WEBLINK starts with `http://` or `https://`. | Throws before anything is touched. |
| 2 | Create `<CatalogBackupFolder or <catalog folder>\LSE_Catalog_Backups>\<yyyyMMdd_HHmmss>\` (a `_n` suffix if it exists). | Throws. |
| 3 | `IssuanceLock.Acquire`: exclusive, delete-on-close `LSE_PartNumber.lock` next to the catalog, 40 tries at 250 ms. The same file name as the Inventor add-in, so both tools serialise on it. | Throws ("Another workstation is issuing a part number right now"). |
| 4 | Copy `default_cat.mdb` into the backup folder. | Throws. |
| 5 | Read every value of the part column that starts with the prefix from every family table; `PartNumberService.Preview` against those and the tab's listed numbers; log the explanation. | Restore, report, rethrow. |
| 6 | Refuse if the number already exists in any table (`FindByColumn`) or if MANUFACTURER + CATALOG already exists in the family table (`Exists`). | Restore, report, rethrow. |
| 7 | `CatalogService.AddRecord`: insert only the columns the table has, enforcing defined sizes, part number into the part column. | Restore, report, rethrow. |
| 8 | Read back by MANUFACTURER + CATALOG and by part number (must be in the chosen table). | Restore, report, rethrow. |

On failure the backup copy is copied back over the catalog (`Catalog rollback: PASS`, or `PASS (nothing had been written)`), `CatalogReport.txt` with the error is written into the backup folder, and the exception is rethrown. `Execute` (stand-alone) adds the header lines, logs `Status: SUCCESS` and writes `CatalogReport.txt` on success too. The lock is released when `Run` returns or throws.

### 5.2 Catalog browser and assignment

What is read: `default_cat.mdb` through `CatalogService` (`GetTables`, `GetFamilyDescriptions`, `ReadTable`, `FindByKey`), `CompanyStandards.json`, and the attributes of the block the engineer picked (`ComponentAssignService.ReadBlock`). What is written: the attributes of that one block, in the open drawing, and `settings.json` (`Favorites`, `Recent`), plus `CompanyStandards.json` when a standard is toggled. The browser never writes the catalog; its **New record...** button opens `CatalogRecordForm`, which goes through `CatalogTransactionService.Run` exactly as `LSECATALOGADD` does, so the write path of section 5.1 is unchanged and remains the only one.

`ComponentAssignService.Apply` runs under a `LockDocument`, in one transaction: each attribute reference is opened for read and upgraded to write only when its value changes, a missing part attribute (`LsePartAttribute`) is appended as an invisible attribute definition plus reference, then commit and `Regen`. There is no log file or backup for it: AutoCAD UNDO is the way back. Attributes on the preserved list (`IsPreservedByDefault`) are left alone unless the engineer unticks Preserve in the preview.

**Vault inside the catalog transaction.** With `CatalogVaultPath` set, `Run` and `SetSymbols` add two steps and one undo to section 5.1. `VaultReady` is checked first (from `Validate`, and at the top of `SetSymbols`): a Vault path with a missing `VaultExplorerPath` folder is refused before anything is touched. The order is then: validate, create the backup folder, take `LSE_PartNumber.lock`, copy the catalog into the backup folder, `VaultCatalogService.Checkout` (log `Vault checkout: PASS`), write, read back, `VaultCatalogService.Checkin` with the comment `LSE Librarian: add catalog record <MFG> <CAT> (<column>=<part>)` or `LSE Librarian: set WDBLKNAM on <n> catalog row(s)` (log `Vault checkin: PASS`). The backup is therefore taken before the checkout. On any failure the backup is copied back over the catalog first and then `VaultUndo` runs `UndoCheckout` (log `Vault undo checkout: PASS`, or `FAILED <reason>`; a failed undo does not mask the original error). With `CatalogVaultPath` blank none of this runs and no Vault line appears in any report.

## 6. The icon menu model

An AutoCAD Electrical icon menu `.DAT` is a text file: comment lines (`;`) at the top, then pages. A page starts with `**M<n>`, may have a layout line (`D3W`, `D0`), then a title line, then entries `label|image|action`. `image` is a slide library reference such as `nfpa(s_pb)`, or a file path relative to the `.DAT` folder such as `IMAGES\BUSS.png`. `action` is `$S=M<n>` (submenu), `$C=<command and arguments>` (for example `$C=WD_INS_CIRC_MAIN <path>` for saved circuits, `$C=wd_infpx "CR"` for stock footprints), a block name resolved through the library search path, or a full DWG path.

`IconMenuService` keeps the file as `IconMenu` (header lines, pages) where each page holds `Id`, `Number`, `Layout`, `Prelude` (comment lines between header and title), `Title` and `Entries`; an entry with fewer than three fields or starting with `;` is kept verbatim in `Raw`. Encoding (UTF-8 with BOM in the LSE files), line ending (CRLF) and the trailing newline are detected on load and reproduced on save, so a load-render round trip of both LSE menus is byte-identical (smoke test). New pages take `max(existing number) + 1`, at least 100, which is where the LSE pages already live (M100 to M138).

The audit treats every page reachable through `$S=` links from a kind's `menuRootPages` as an LSE page; stock pages are checked only when "Include stock AutoCAD Electrical pages" is on.

## 6.1 The naming standard model

`NamingWorkbookService` turns the workbook into a `NamingStandard`: `Format` (the four widths, 2+3+2+7 by default), one `NamingTab` per `LSE ` sheet that has a "Part # Segment" table and type codes (`CategoryCodes`, `CategoryRule`, `Types`, `Manufacturers`, `DescriptionPattern`, `PurchaseDescriptionPattern`, `DescriptionFields`, `Rules`, `Examples`, `KnownPartNumbers`, `ManufacturerSpellings`), `SkippedTabs`, the union `Manufacturers` (code → name and the spelling AutoCAD Electrical uses, derived from the "Manufacturer" column of the part tables) and `FamilyHints` (category code → family tables, filled only in the snapshot). The workbook is parsed as a zip of XML (`xl/workbook.xml`, `xl/_rels/workbook.xml.rels`, `xl/sharedStrings.xml`, the sheet parts) with `System.IO.Compression` and `XmlReader`, opened with `FileShare.ReadWrite` so it may stay open in Excel; the result is cached per full path + last-write time + size. The JSON snapshot is the same object serialised; `Load(settings)` takes the workbook when the file exists and the snapshot otherwise.

`PartNumberService` is pure logic over that model and a list of existing numbers: `Preview` returns the next number and an explanation; `Highest` ignores legacy sequences; `Format` zero-pads; `Split` takes a number apart.

## 7. Process, threading and context

- All commands run on AutoCAD's main thread with `CommandFlags.Modal`. The wizard, the catalog record dialog and the audit are modal dialogs shown with `Application.ShowModalDialog`.
- The transaction and the audit run synchronously on the UI thread; the review pane and status label are updated after each log line with `Control.Update`. AutoCAD is unresponsive meanwhile (a few seconds per run; the deep audit opens every drawing and can take minutes on a share). Parsing the workbook takes about 0.4 s and happens once per dialog (cached afterwards); every part-number preview scans all family tables of the catalog over ADODB.
- Side databases (`new Database(false, true)` + `ReadDwgFile(..., OpenForReadAndAllShare)`) never touch the active document. WBLOCK export locks the active document for the duration.
- Temporary exports for inspection and icon preview live in `%TEMP%\LSE_Librarian\` and are deleted when the wizard closes.
- ADODB objects are `dynamic`, released with `Marshal.FinalReleaseComObject` in `finally` blocks, exactly as in the sibling plugins. `CatalogService` opens and closes a connection per call, like `FootprintLookupService`.

## 8. Design decisions

| Decision | Reason |
|---|---|
| Edit the `.DAT` files directly and prove the round trip | The icon menu format is simple and already contains hand-edited LSE pages; preserving every byte the Librarian did not add keeps the file diff-able in Vault and avoids surprising AutoCAD Electrical's parser. |
| Append entries; never reorder, dedupe or reformat | Engineers know the current order of the menus. Repair is reported by the audit, not performed. |
| Full DWG paths in menu entries | That is the convention every LSE page already uses, so the Librarian matches it instead of relying on the library search path. |
| One lock file in the icon menu folder | The menus are shared text files on a Vault working folder; an exclusive delete-on-close file is the cheapest cross-workstation mutual exclusion without a server. |
| Whole-file backup and restore of the `.DAT` and the MDB | Same approach as the catalog tools; simple, inspectable, and the files are small. The restore puts back the whole file, so a change another user made between backup and rollback (a few seconds) would be lost. |
| Names unique across the whole root | AutoCAD Electrical resolves a block by name through every library path; two files with the same name in different folders are ambiguous. |
| In-place registration | The most common audit finding is a correctly placed drawing with no menu entry; copying it onto itself makes no sense, so the request detects source == target and skips the copy. |
| Icon rendered from curves with the DWG preview as fallback | AutoCAD Electrical shows PNG icons as-is; a clean black-on-white rendering at 40 px matches the 235 existing LSE icons. The DWG preview is a coloured screenshot and only used when asked or when there are no curves. |
| JSON standard beside the DLL with a settings override | Same pattern as the catalog, footprint and project tools: one shared copy on the network keeps every workstation identical. |
| Read the `.xlsx` directly, no Excel, no NuGet | The workbook is the document the catalog manager maintains, so the naming scheme has one source of truth; reading it as a zip of XML with the framework's own classes needs no Excel installation, no interop, no package at runtime, and works while the file is open in Excel. The JSON snapshot exists only for workstations that cannot reach the workbook. |
| Legacy floor at 10^(SequenceChars-1) | Legacy ERP numbers kept under rule 7 fill all seven digits (`1633301`, `3238731`, `1691250`); numbers issued under the standard start at `0000001`. Treating a sequence at or above 1,000,000 as legacy keeps those numbers reserved without pushing the counter into the millions. It is an inference from the data (see README, known gaps), kept in one place (`PartNumberService.LegacyFloor`) so it can be changed. |
| Count the sequence across every family table and the workbook tab | A prefix such as `45BUSFH` could have been issued in any family table, and the workbook already reserves numbers the catalog does not hold yet; checking both is the only way never to issue a number twice. |
| Catalog browser merged into the Librarian instead of kept as a separate plugin | The separate Catalog Tools plugin duplicated the Librarian's catalog add (and its admin form required a hand-typed USER1). Merging leaves one catalog reader and one writer, one `settings.json`, one set of catalog paths and one Vault setting; the browser's **New record...** calls the existing transaction instead of a second writer. Only the unique features (browser, assignment, company standards, favourites, optional Vault checkout) moved. |
| Browser and assignment write no catalog and keep no log | The assignment touches one block in the open drawing and AutoCAD UNDO reverses it; a backup, lock and report would add ceremony without protecting anything the undo stack does not. |
| Vault checkout optional, reflection-loaded, catalog only | Not every workstation has the Vault client; a blank `CatalogVaultPath` leaves behaviour unchanged. Loading the SDK by reflection from `VaultExplorerPath` avoids a compile-time dependency on a version-specific installation. |
| One writer per store | `LibraryTransactionService` writes libraries, menus and the lookup; `CatalogTransactionService` writes `default_cat.mdb`; each has its own backup, lock and restore. The library run calls the catalog run last so the two rollbacks compose without either knowing the other's files. |
| Same lock file name as the Inventor add-in | `LSE_PartNumber.lock` next to the catalog means both tools serialise part-number issuance on the same file without a server. |
| Two code densities | Services, models, settings form, diagnostics, plugin and the transactions (`NamingWorkbookService`, `PartNumberService`, `CatalogTransactionService` included) are compact; the wizard, the catalog record dialog, the audit form and the two AutoCAD-API services are expanded. This matches `../STYLE-GUIDE.docx` and the sibling projects. |

## 9. Relationship to the sibling projects

`../inventor-addin` (Footprint Tools) and `../acade-project-creator` (Project Tools) share the settings pattern (`SettingsService.Load`/`Save` with a `.tmp` move), the transaction pattern (line-per-step log, `Status: SUCCESS`, report file, rollback, lock file) and the form conventions. `Ace` and `FootprintLookupService` are copied from the Inventor add-in with the same shapes; if you fix a defect in one copy, check the other. No assembly is shared; each project ships alone. The Inventor add-in creates footprints and catalog rows for new part numbers; the Librarian files hand-drawn symbols, footprints and circuits, keeps the icon menus honest, and can now add the catalog row too. The former `acade-catalog-tools` plugin was merged into this one on 2026-11-12 and is no longer a sibling; the Inventor add-in carries the twin of `VaultCatalogService` and `Ace`, so a defect fixed in one of those should be checked in the other. The two add-ins both write `footprint_lookup.mdb` rows in the same form (table named after MANUFACTURER, `BLKNAM` = full DWG path), both take `LSE_PartNumber.lock` next to `default_cat.mdb` while issuing a number, and the Librarian's `LseNamingStandard.json` has the same shape as the Inventor add-in's naming file so that one loads here as well. The Librarian reads the naming scheme from the workbook itself; the Inventor add-in reads its JSON.
