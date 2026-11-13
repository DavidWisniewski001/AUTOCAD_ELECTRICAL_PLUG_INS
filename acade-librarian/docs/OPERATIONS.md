# Operations guide: LSE Librarian

Maintained by DCW. Last reviewed 2026-11-12.

How to install, configure and use the Librarian, what a run leaves behind, how to read the log, and how to recover by hand. Read [DATA-STORES.md](DATA-STORES.md) for where each file lives.

## 1. Workstation requirements

| Requirement | Why |
|---|---|
| AutoCAD Electrical 2026 (x64, .NET 8 runtime comes with it) | Host application. |
| Read and write access to the Vault working folder `C:\_Lake_Shore_E_Vault\CAD Standards\AutoCAD\ECAD Standards` (or wherever the libraries and `dat MENU` live) | The Librarian writes DWG, PNG and `.DAT` files there. Check out the `.DAT` files in Vault Explorer first if your Vault keeps them read-only. |
| 64-bit Access Database Engine 2016 (or 2010) | For registering footprints in `footprint_lookup.mdb` and for adding catalog records to `default_cat.mdb`. Without it, untick the registration box and the catalog option. |
| The LSE parts catalog workbook `LSE ELECTRICAL PARTS CATALOG MANAGER.xlsx` reachable on this workstation, or the JSON snapshot `LseNamingStandard.json` next to the DLL | Only for catalog records: the part-number naming scheme is read from the workbook. Excel is not needed; the workbook may be open in Excel elsewhere. |

## 2. Installation

1. Copy the Release output folder (`LSE.AutoCADElectrical.Librarian.dll`, `.deps.json`, `.runtimeconfig.json`, `LseLibraryStandard.json`, `LseNamingStandard.json`) to a share or to each workstation.
2. Either type `NETLOAD` in AutoCAD Electrical and pick the DLL (per session), or copy `bundle\LSE.Librarian.bundle` to `%APPDATA%\Autodesk\ApplicationPlugins\` and put the output files in its `Contents\` folder (loads on first use of a command).
3. Type `LSELIBRARYDIAG`. The first lines show the runtime and the settings file; the rest should read `(exists)` or `PASS`. For catalog records look for `Parts catalog workbook: ... (exists)`, `Naming standard from workbook: PASS (33 naming tabs, ...)` (or `from JSON snapshot`) and `Catalog read: PASS (n family tables, part number column USER1, backups in ...)`.

## 3. First-time configuration (per user)

Type `LSELIBRARYSETTINGS`.

| Field | Set it to |
|---|---|
| LSE symbol library folder | `...\Symbol Library\LSE E-Cad Symbols` |
| Stock (NFPA) symbol library folder | `...\Symbol Library\NFPA` |
| LSE footprint library folder | `...\Footprint Library\LSE E-Cad Footprints` |
| Stock (panel) footprint library folder | `...\Footprint Library\panel` |
| User circuits folder | `...\User Circuits` |
| Icon menu folder (dat MENU) | `...\dat MENU` |
| Schematic / panel icon menu file name | `ACE_NFPA_MENU.DAT` / `ACE_PANEL_MENU_NFPA.DAT` |
| Images subfolder name | `IMAGES` |
| Footprint lookup MDB | `...\E-CAD .mbd ACCESS FILES\AeData\en-US\Catalogs\footprint_lookup.mdb` |
| Catalog MDB (default_cat.mdb) | `...\E-CAD .mbd ACCESS FILES\AeData\en-US\Catalogs\default_cat.mdb` (the catalog that receives records) |
| LSE parts catalog workbook (.xlsx) | the shared `LSE ELECTRICAL PARTS CATALOG MANAGER.xlsx`; pre-filled when it sits next to the catalog MDB, up to four folders above it, in the ECAD Standards root, in `E-CAD .mbd ACCESS FILES`, or in `Documents\AutoCAD_Electrical_2026` |
| Naming standard JSON (fallback snapshot) | leave `LseNamingStandard.json` next to the DLL, or point at a shared snapshot |
| Catalog backup folder (blank = next to the catalog) | leave blank for `<catalog folder>\LSE_Catalog_Backups`, or a folder on the share |
| Catalog column holding the LSE part number | `USER1` unless the catalog keeps LSE numbers elsewhere |
| Library standard JSON | the shared copy of `LseLibraryStandard.json` |
| Backup folder | leave the default, or a per-user folder on the share |
| Icon size | 40 (matches the existing icons) |
| Default series | 002 |
| Offer a catalog record (default_cat.mdb) in the Librarian wizard | ticked, unless this workstation should never write the catalog |
| Fill USER2 with the purchase description (Description + MPN) by default | ticked (the LSE purchase description rule) |

On a workstation where the Vault working folder is at the standard path all of these are pre-filled. Otherwise press **Read library paths from project...**, choose any LSE `.wdp` (for example `E-CAD CUSTOM PROJECT TEMPLATE 2.wdp`), and the folders and menu names are taken from its `+[1]`..`+[4]` lines. Press **Test** (folders exist, both menus parse, JSON loads, lookup opens, the workbook or the snapshot parses with its tab and code counts, the catalog opens with its family table count), then **Save**.

If the workbook is reachable from this workstation, press **Save naming snapshot (JSON)** once: it parses the workbook, adds the family hints from the catalog and rewrites the snapshot file, so a workstation (or a day) without access to the workbook still issues numbers from the same scheme. Repeat it after the catalog manager adds a tab, a code or a batch of numbers; the snapshot does not update itself.

## 4. Filing an item

Type `LSELIBRARIAN`.

### Step 1: Item

1. Pick the kind: schematic symbol, panel footprint, nameplate / label footprint, or saved user circuit.
2. Pick the source: **A drawing file** and browse to the `.dwg` (anywhere, including a project folder where someone saved it), or **A block definition in the current drawing** and choose it from the list or press **Pick in drawing** and click one. Blocks are exported with WBLOCK, so the block's base point becomes the drawing origin.
3. Type a plain description. It becomes the default menu label and the lookup description.
4. Press **Inspect drawing**. The pane shows the format, units, entity count, extents, every attribute (tag, visibility, default, prompt), nested blocks and the standard check. Fix ERROR lines in the drawing before filing (a symbol without `TAG1`/`TAG2` or `DESC1`, a footprint without `P_TAG1`, or any drawing containing `WD_M`).

### Step 2: Name and folder

Build the name from its parts:

| Kind | Parts |
|---|---|
| Schematic symbol | Orientation H or V; family code (two letters, list from the JSON, `DV` for a generic device); parent (1) or child (2); optional variant (0 to 2 characters); series; description. |
| Panel footprint | Prefix `FP` (or `FPT` for terminal footprints); series; manufacturer (`EATON`, `WEID`, `LSE`...); description. |
| Nameplate | Prefix `NP`; series; manufacturer; description. |
| User circuit | The circuit name. |

The resulting name is shown in bold with the pattern problems, if any, in red; the description is upper-cased and spaces become `_` automatically. Tick **Type the full name myself** to enter a name directly; it still has to pass the pattern. Choose the library category (the standard folders, plus any existing folder under the root) and an optional subfolder. The full target path is shown. Names must be unique across the whole library.

### Step 3: Icon menu, icon image and footprint lookup

1. The menu file for the kind is shown. Choose the page: the list shows every LSE page with its breadcrumb (`Panel Layout Symbols > LSE FOOTPRINT COLLECTION > LUGS & TERMINAL BLOCKS`), pre-selected from the category. Or tick **Create a new page**, give it a title and choose the parent page; the new page gets the next free `M` number and a link on the parent.
2. Menu label: what engineers click. Must be unique on the page.
3. Icon image: **Render from the drawing geometry** (default), **Use the drawing's embedded preview image**, **Reuse an existing image** (list of the images folder), **Import an image file**, or **No icon image**. Press **Preview icon** to see it at the configured size.
4. Footprints and nameplates only: tick **Register in footprint_lookup.mdb** and enter MANUFACTURER (becomes the table name, max 24) and the catalog number (max 60) if AutoCAD Electrical should insert this footprint when that catalog number is assigned on the schematic.
5. Symbols, footprints and nameplates (not circuits), when the setting "Offer a catalog record" is on: the group **Catalog record (default_cat.mdb, LSE part number from the parts catalog workbook)**. Tick **Also add a catalog record to default_cat.mdb with a new LSE part number** (or press **Catalog record...**) to open the catalog record dialog described in section 4.1, pre-filled with the MANUFACTURER and catalog number from the lookup boxes, the plain description and, for a schematic symbol, `WDBLKNAM` = the symbol name. Press **Use in Librarian** when the record is complete: the summary line shows the family table, MANUFACTURER, catalog number and the part-number prefix (`45BUSFH+ next sequence (issued when the item is added)`), and empty lookup boxes are filled from the record. Cancelling the dialog without a record unticks the box. Next validates the record (codes, family table, field limits, description rules) before the review.

### Step 4: Review and add

Read the summary; with a catalog record it includes `Catalog record  <table> | <MANUFACTURER> | <catalog>  part number <prefix>+ next sequence, issued at run time  (catalog backed up next to it)`. Press **Add to library**. Every step is echoed as it happens; the catalog step runs last, after the footprint lookup, and logs the lock, the catalog backup, the preview reasoning and `Catalog insert into <table> (USER1=<number>, ...): PASS`; the last line is `Status: SUCCESS`, and a message box shows the library path, the menu page and, when a record was added, `Catalog record added with LSE part number <number>.` Open the icon menu in AutoCAD Electrical (Insert Component or Insert Footprint) to see the new entry.

### What to check on the first real run

- The new entry appears on the expected page with its icon, and inserting it places the drawing correctly (base point, attributes prompting as expected).
- The `.DAT` opens in a text editor and differs from the backup copy only by the appended line(s).
- For a footprint registered in the lookup, assign the catalog number to a schematic component and insert the footprint from the panel; AutoCAD Electrical should pick the library drawing.
- For a catalog record, open Catalog Browser in AutoCAD Electrical on the family table and find the new row by MANUFACTURER and catalog number; `USER1` holds the LSE part number and `USER2` the purchase description.
- Check the changed files in to Vault, including `default_cat.mdb` when a record was added.

## 4.1 Adding a catalog record

The same dialog serves the wizard (step 3, embedded, **Use in Librarian**) and the stand-alone command `LSECATALOGADD` (**Add to catalog**). Stand-alone it is the catalog-only path: it writes the row itself and nothing else (no symbol, footprint, icon menu, library folder or `footprint_lookup.mdb`), and WDBLKNAM is optional; embedded it hands the record to the wizard, which writes it inside the library run.

1. **Naming standard source.** The first line says `Workbook: <path>` (33 naming tabs, 314 manufacturer codes, ... on 2026-11-10) or `JSON snapshot (workbook not reachable): <path>`. **Reload** re-reads the workbook after the catalog manager saved it (the dialog otherwise re-reads it only when the file's date or size changed).
2. **Workbook tab.** Pick the LSE category sheet the part belongs to (`LSE FUSE HOLDERS`, `LSE WIRE`, ...). Tabs without a naming block (on 2026-11-10 `LSE LABELS & NAMEPLATES`, `LSE CHEMICALS & CONSUMABLES`) are not listed and cannot issue numbers until they get one.
3. **Category code, Manufacturer code, Type code.** The tab's own codes are offered first; the category rule text and the type meanings are shown. Manufacturer codes from other tabs are listed as `(other tab)`; a new code can be typed (three letters) when the workbook does not have it yet, but it should be added to the workbook as well. Picking a manufacturer code fills MANUFACTURER with the spelling the workbook uses for it (`BUS` → `BUSSMANN`) when MANUFACTURER is empty.
4. **Preview next number.** The bold number is the next unused sequence for `<category><code><type>`, counted across every family table of `default_cat.mdb` and the numbers listed on the tab; the lines beneath explain it (`default_cat.mdb: 3 45BUSFH number(s), highest issued 45BUSFH0000011 in table FU (1 legacy ERP number(s) ... not counted)`, `Workbook: 11 45BUSFH number(s), highest issued 45BUSFH0000011`, `Next unused sequence: 45BUSFH0000012`). Legacy ERP numbers with a 7-significant-digit tail (`45BUSFH1691250`) stay reserved and are not counted, so the next number after `0000011` is `0000012`. The family-table hint beneath names the tables already holding numbers with that category and selects the first one unless you chose one.
5. **Keep an existing LSE number instead (rule 7).** Tick it and type the full 14-character number to keep a number the item already has (from the ERP or the workbook). It must start with the chosen codes and must not already be in the catalog; the preview says whether the workbook lists it.
6. **AutoCAD Electrical family table.** `CODE  description` from the catalog's `_FAMILY_DESCRIPTION` table. Changing it reloads the MANUFACTURER list.
7. **MANUFACTURER, CATALOG.** MANUFACTURER as spelled in that family table (the list shows the existing values; max 24); CATALOG is the manufacturer part number (max 60). The pair must not already exist in the table.
8. **DESCRIPTION.** Upper case, 100 characters or fewer by the LSE standard (a warning), 150 at most (the column), no `N/A`, no degree, diameter or prime symbols. The tab's pattern is shown; problems appear in red as you type. Use the **Description builder** (one box per field of the tab, example as placeholder, allowed values as tooltip) and **Build description** to compose it in the standard order.
9. **USER2 = purchase description.** Ticked by default: USER2 follows `DESCRIPTION, MPN <catalog number>` (cut to 100). Untick to type USER2 yourself.
10. **TYPE, RATING, VOLTAGE, USER3, TEXTVALUE, WEBLINK, WDBLKNAM.** Optional; only columns the family table has are written. WEBLINK must start with `http://` or `https://`. WDBLKNAM is the schematic symbol block name (pre-filled by the wizard).
11. **Tab rules and examples** repeats the tab's numbered rules and example rows for reference.
12. **Add to catalog** (stand-alone): the record is validated, description warnings ask for confirmation, a preview is taken if none is current, and a confirmation names the record and the number. The catalog is copied to `LSE_Catalog_Backups\<stamp>\`, the issuance lock is taken, the number is recomputed against the live catalog, the row is inserted and read back, and `CatalogReport.txt` is written. A message shows the issued number and the backup folder; CATALOG is cleared so the next part of the same family can be entered.

### What to check on the first catalog run

Do the first run against a copy of `default_cat.mdb` (point "Catalog MDB" at the copy, see the developer guide checklist). Then:

- The preview's "highest issued" and legacy counts agree with what you see in the catalog and on the workbook tab.
- The row is in the expected family table with the right columns filled; nothing else in the catalog changed (compare with the backup copy).
- AutoCAD Electrical's Catalog Browser lists the row and inserting a component with that catalog number works.
- The backup folder holds the pre-change `default_cat.mdb` and `CatalogReport.txt`, and `LSE_PartNumber.lock` is gone from the catalog folder.
- Tell the catalog manager the issued number so the workbook tab lists it (the Librarian does not write the workbook).

13. **Footprint drawing (optional, stand-alone only).** Tick "Also map this part to a footprint drawing", **Browse...** to a `.dwg` in the footprint library and add as usual. The drawing, the lookup MDB path and the lock folder are validated before anything is written, and if `footprint_lookup.mdb` already maps this MANUFACTURER and CATALOG to another drawing you are asked whether to replace it. The catalog row is written first (own backup, own rollback), then the mapping (own backup, own rollback). If only the mapping fails, the message says the lookup MDB was restored and the catalog row stays; map it later with `LSEFOOTPRINTMAP`. The wizard does not show this box: it already registers the lookup row of the footprint it files.

## 4.2 Mapping catalog parts to footprints (`LSEFOOTPRINTMAP`)

AutoCAD Electrical places a panel footprint from the catalog number through `footprint_lookup.mdb`: one table per MANUFACTURER, a row per CATALOG with `BLKNAM` holding the footprint drawing (the Librarian writes the full DWG path), looked up by manufacturer and then catalog. The dialog is the view and fix tool for that mapping.

1. The grid lists every LSE part of `default_cat.mdb` (rows whose part-number column, `USER1` by default, is not empty) with its MANUFACTURER, CATALOG, LSE part number, family table, DESCRIPTION and current BLKNAM. Loading reads the catalog and then the lookup rows of those manufacturers only; on the 2026-11-12 copies (6,650 LSE parts, a 519,000-row lookup) it takes about six seconds, with a wait cursor.
2. **Status**: `Unmapped` (no lookup row, or one with an empty BLKNAM; grey), `Mapped` (a path that exists), `Missing drawing` (a path that does not exist; red; the audit reports the same as an Error), `Block name` (BLKNAM is a bare block name as in the stock rows; not checked). The default filter is Unmapped; filter on family or type text as needed.
3. Select one or more rows (Ctrl or Shift), press **Browse...** (opens in the footprint library) or paste a path, then **Map selected parts**. The drawing must exist, end in `.dwg` and sit inside the footprint library or the stock footprint library; the full path must fit the 255-character BLKNAM column.
4. If any selected part is already mapped to a different drawing the dialog lists them and asks before replacing (default answer No). Without that confirmation nothing is written. Mapping a part to the drawing it already has is a no-op: no backup, no write.
5. The run: validate, take `LSE_Library.lock` in the icon menu folder, copy `footprint_lookup.mdb` to `<BackupFolder>\<stamp>_footprintmap\`, create any missing manufacturer table (same columns as the Librarian's footprint registration), insert or update the plain row (no ASSEMBLYCODE; DESCRIPTION is copied from the catalog only on insert), read every row back, write `FootprintMapReport.txt`. A failure restores the MDB from the backup, records `ERROR:` and the rollback in the report and shows the message. The batch is all or nothing.
6. The list reloads afterwards. Existing rows are never deleted; a wrong mapping is replaced, and a row to remove is removed by hand (or restored from the backup).

What to check on the first real run: do it against a copy of `footprint_lookup.mdb` (point the lookup path at the copy). Confirm that AutoCAD Electrical resolves the footprint from the manufacturer and catalog number (panel layout, "Insert footprint" by catalog) and that a stock row you did not touch still resolves.

## 4.3 Setting the schematic symbol of catalog rows (`LSEFOOTPRINTMAP`, Set symbol)

The same dialog (title "LSE Librarian: catalog symbol and footprint links") shows each LSE part's WDBLKNAM in the **Symbol (WDBLKNAM)** column; the **Symbol** filter narrows the list to rows with or without a symbol.

1. Select one or more rows, type the block name in **Schematic symbol (block)** or press **Browse...** (opens in the LSE symbol library; only the file name without `.dwg` is used).
2. Press **Set symbol**. The block must exist as a `.dwg` in the LSE or stock symbol library (subfolders are searched); otherwise nothing is written. The library's own spelling is stored.
3. If any selected row already has a different symbol the dialog lists them and asks before replacing (default No). Setting the symbol a row already has is a no-op: no backup, no write.
4. The run backs up `default_cat.mdb` to `<CatalogBackupFolder>\<stamp>_symbollink\`, takes `LSE_PartNumber.lock`, updates only the WDBLKNAM column, reads each row back and writes `SymbolLinkReport.txt`. A failure restores the catalog. Rows are never inserted or deleted; a row that does not match exactly once is refused.
5. To undo, copy the catalog back from that backup folder (or set the previous symbol; the report lists it as "was ...").

## 5. Running the audit

Type `LSELIBRARYAUDIT`. The audit runs on open and lists findings with a severity colour, where, what and what to do. Filter with **Show** (errors; errors and warnings; everything) and the search box. Options: **Open every drawing (slow)** adds attribute and `WD_M` checks per drawing; **Include stock AutoCAD Electrical pages** checks the Autodesk pages too (bare block names are then verified against the stock folders).

| Finding | Severity | Meaning |
|---|---|---|
| Entry points to a drawing that does not exist | Error | The menu names a file that was moved or deleted. |
| Icon image is missing | Error | The `IMAGES\...` file is not there. |
| Submenu link points to a page that does not exist | Error | `$S=M<n>` with no `**M<n>`. |
| Page is defined more than once | Error | Two `**M<n>` headers with the same number. |
| Duplicate drawing name | Error | Two files with the same name under one library root. |
| Lookup row points to a missing footprint drawing | Error | `BLKNAM` path does not exist. |
| Entry points to a drawing outside every configured library folder | Warning | The "dumped somewhere" case. |
| File name does not follow the LSE standard | Warning | Pattern violation. |
| Footprint sits in the library root / unknown category folder | Warning | Needs a category folder. |
| Drawing is not referenced by any icon menu entry | Warning | Orphan: select it and press **Open in Librarian** to register it in place. |
| A symbol is listed in the panel menu (or the reverse) | Warning | Wrong menu. |
| Missing required or recommended attribute, contains WD_M | Error / Warning | Deep audit only. |
| N of M LSE part(s) in family table X have no footprint_lookup.mdb row | Info | One note per family, listing three examples. Run `LSEFOOTPRINTMAP`, filter on Unmapped and map them; parts that never go on a panel can stay unmapped. Read only (`FootprintMappingService.Load`). |
| Page not reachable from M0, empty page, no icon, unused image, stray non-DWG file, no lookup row | Info | Housekeeping. |

**Export report...** writes a tab-separated text file grouped by severity. **Open containing folder** shows the file in Explorer.

The audit never changes anything. On this authoring machine (no libraries present) it reports every menu entry as missing; that is expected.

## 6. Reading the log

`LibraryReport.txt` in the run's backup folder, also shown in the wizard:

```
Started: 2026-11-10T14:02:11 by dwisniewski@LSE-ENG07
Kind: Panel footprint
Source: C:\Users\...\Desktop\relay base.dwg
Target: C:\_Lake_Shore_E_Vault\...\COMPONENT PANEL FOOTPRINTS\FP_002-PHO_RLY-27E891_BSE.dwg
Icon menu: C:\_Lake_Shore_E_Vault\...\dat MENU\ACE_PANEL_MENU_NFPA.DAT
Backup folder: C:\Users\...\AppData\Roaming\LSE\AutoCADElectricalLibrarian\Backups\20261110_140211
Library lock: PASS
Backups: PASS
Drawing copied into the library: PASS
Icon (Render) written: ...\IMAGES\FP_002-PHO_RLY-27E891_BSE.png (40 px)
Icon menu entry added to ACE_PANEL_MENU_NFPA.DAT page M128 (RELAYS PLUS): RELAY BASE 27E891|IMAGES\FP_002-PHO_RLY-27E891_BSE.png|C:\...\FP_002-PHO_RLY-27E891_BSE.dwg
Footprint lookup [PHOENIX CONTACT] 2967015 -> C:\...\FP_002-PHO_RLY-27E891_BSE.dwg: PASS
Status: SUCCESS
```

With a catalog record the log continues after the lookup line:

```
Catalog record: FU | BUSSMANN | CH14J1I (naming standard from workbook C:\...\LSE ELECTRICAL PARTS CATALOG MANAGER.xlsx)
Part number issuance lock: PASS
Catalog backup: C:\_Lake_Shore_E_Vault\...\Catalogs\LSE_Catalog_Backups\20261110_140214\default_cat.mdb
  default_cat.mdb: 1 45BUSFH number(s), none issued under the standard yet (1 legacy ERP number(s) with 7 significant digits kept under rule 7, not counted).
  Workbook: 11 45BUSFH number(s), highest issued 45BUSFH0000011.
  Next unused sequence: 45BUSFH0000012 (the workbook already reserved numbers above the catalog's highest, so the count continues after them).
Catalog insert into FU (USER1=45BUSFH0000012, BUSSMANN CH14J1I): PASS
Catalog record added with LSE part number 45BUSFH0000012 (catalog backup in C:\...\LSE_Catalog_Backups\20261110_140214)
Status: SUCCESS
```

A failed run ends with `ERROR: <message>`, then `Icon menu rollback: PASS`, `Rollback removed ...` lines, and `Detail:` with the exception. If any rollback line says `FAILED`, recover by hand. When the catalog step fails, `Catalog rollback: PASS` (or `PASS (nothing had been written)`) appears before the library rollback lines, and the catalog backup folder gets a `CatalogReport.txt` with the error.

`CatalogReport.txt` in the catalog backup folder (`LSECATALOGADD`) has the same shape with its own header:

```
Started: 2026-11-10T15:31:04 by dwisniewski@LSE-ENG07
Catalog: C:\_Lake_Shore_E_Vault\...\Catalogs\default_cat.mdb
Naming standard: C:\...\LSE ELECTRICAL PARTS CATALOG MANAGER.xlsx (workbook)
Record: FU | BUSSMANN | CH14J1I
Part number prefix: 45BUSFH (LSE FUSE HOLDERS)
Part number issuance lock: PASS
Catalog backup: ...\LSE_Catalog_Backups\20261110_153104\default_cat.mdb
  ...preview lines...
Catalog insert into FU (USER1=45BUSFH0000012, BUSSMANN CH14J1I): PASS
Status: SUCCESS
```

## 7. Recovery by hand

1. Open the run's backup folder (the path is in the log and in the error message).
2. Copy `ACE_NFPA_MENU.DAT` or `ACE_PANEL_MENU_NFPA.DAT` from it back over the file in `dat MENU`.
3. If `footprint_lookup.mdb` is in the backup folder, copy it back as well.
4. Delete `<Name>.dwg` from the library folder and `<Name>.png` from the images folder if they exist and the log shows they were created by that run.
5. If `LSE_Library.lock` remains in `dat MENU` and no AutoCAD is running on any workstation, delete it.

For the catalog (`Catalog rollback: FAILED`, or a record that should not have been added):

1. Open the catalog backup folder named in the log (`<catalog folder>\LSE_Catalog_Backups\<stamp>\`, or the configured catalog backup folder). `CatalogReport.txt` says which record and number were involved.
2. Make sure nobody has AutoCAD Electrical's Catalog Browser open on the catalog, then copy `default_cat.mdb` from the backup folder back over the live catalog. Any record another user added to the catalog after that backup was taken is lost; the backups are a few seconds apart, so check the newer folders first.
3. Alternatively delete just the row (MANUFACTURER + CATALOG, or `USER1` = the part number) in Access or the Catalog Browser; the number then becomes free again unless the workbook already lists it.
4. If `LSE_PartNumber.lock` remains next to the catalog and neither AutoCAD Electrical nor Inventor is issuing a number on any workstation, delete it.
5. Check the restored or corrected `default_cat.mdb` in to Vault.

## 8. Error messages

| Message | Cause | Action |
|---|---|---|
| X is already mapped to <path>. Nothing was changed | `LSEFOOTPRINTMAP` or the optional footprint box found a different drawing for that MANUFACTURER and CATALOG | Confirm the replacement in the dialog, or leave it. |
| The footprint must live in the footprint library ... | The chosen `.dwg` is outside both footprint library folders | File it with `LSELIBRARIAN` first, or choose a library drawing. |
| The same part is listed twice | A batch named one MANUFACTURER and CATALOG twice | The dialog removes repeats; report it if you see this. |
| The LSE library standard JSON was not found | `LibraryStandardPath` wrong | Settings. |
| The library standard JSON has no "kinds.X" section / invalid namePattern | Edited JSON is incomplete | Fix the JSON; compare with `config\LseLibraryStandard.json`. |
| Name does not follow the ... pattern | Name builder input | Follow the example and help text under the name. |
| The name is already used elsewhere in the library | Same file name in another folder | Choose another description or variant. |
| The name clashes with a stock AutoCAD Electrical symbol | Same name in the NFPA folder | Choose another name. |
| The target folder must be the ... library or a folder inside it | Category or subfolder resolves outside the root | Choose a category. |
| Page M<n> already has an entry labelled "..." | Duplicate label | Change the label. |
| An icon image with that name already exists | `IMAGES\<Name>.png` exists | Choose "Reuse an existing image" or rename. |
| Another workstation is changing the icon menus right now | Lock held | Wait and retry; see section 7 if it persists. |
| Access to the path is denied | `.DAT`, folder or image read-only (Vault) | Check out or make writable, retry. |
| No active drawing to export a block from | Block source with no document | Open the drawing that holds the block. |
| Block ... was not found in the current drawing | Typed name is wrong | Press Refresh list or Pick in drawing. |
| The drawing has no embedded preview image | DWG saved with previews off | Choose Render or import an image. |
| The drawing has no lines, arcs, ... to render | Only text or blocks on unsupported entities | Import an image. |
| No 64-bit Access OLEDB provider could open ... | Access engine missing or 32-bit | Install the 64-bit Access Database Engine, or untick lookup registration. |
| MANUFACTURER is limited to 24 characters / CATALOG to 60 | Lookup or catalog field length | Shorten. |
| The new entry did not survive a re-parse of the rendered icon menu | Label or action contains something the parser treats specially | Remove `|` and leading `;` from the label; report the case. |
| Neither the LSE parts catalog workbook nor the naming standard JSON snapshot was found | Both paths blank or missing | Set "LSE parts catalog workbook (.xlsx)" in settings, or restore `LseNamingStandard.json` next to the DLL. |
| The LSE parts catalog workbook was not found | `NamingWorkbookPath` points at a missing file (Reload or Save snapshot) | Fix the path; the dialog itself falls back to the snapshot. |
| Not an Excel workbook (xl/workbook.xml missing) | The path is not an `.xlsx`/`.xlsm` (an `.xls` or a stray file) | Point at the real workbook. |
| No "LSE ..." tab with a Part # Segment table and type codes was found | Workbook renamed its tabs or lost the naming blocks | Check the workbook with the catalog manager; use the snapshot meanwhile. |
| The catalog MDB path is not set / Catalog database was not found | `CatalogPath` blank or wrong | Settings, "Catalog MDB (default_cat.mdb)". |
| Choose the workbook naming tab / Choose the type code / Choose the AutoCAD Electrical family table | Dialog field empty | Fill it. |
| Category code must be 2 digits / Manufacturer code must be 3 letters / Type code must be 2 letters | Code typed in the wrong form | Pick from the list or type the right form. |
| Type code XX is not defined on the "..." tab. Defined: ... | Type code from another tab | Pick a defined type, or ask the catalog manager to add it to the tab. |
| The part number must be 14 characters: 2 digits, 3 letters, 2 letters, 7 digits | Manual number in the wrong form | Type the full number, for example `45BUSFH0000001`. |
| The part number ... does not start with the chosen category, manufacturer and type codes | Manual number disagrees with the codes | Change the codes or the number. |
| Part number ... is already issued in default_cat.mdb (table X) | Manual number already in the catalog | Choose another, or find the existing record. |
| Part number ... already exists in table X / ... already exists in family table X | Issued or inserted between preview and run, or the MANUFACTURER + CATALOG pair exists | The catalog was restored; press Preview again, or find the existing record. |
| Sequence exhausted for <prefix> | All 999,999 non-legacy sequences used | Not expected; talk to the catalog manager. |
| Table X has no USER1 column, so it cannot hold an LSE part number | Family table without the part column | Choose another family table or change "Catalog column holding the LSE part number". |
| Description is required / Description must be ALL CAPS / Leave out fields that do not apply; never write N/A / Description must not contain degree, diameter or prime symbols / Description is n characters; the catalog column holds 150 | Description rules | Fix the description (the builder helps). |
| Description is n characters; the LSE standard asks for 100 or fewer | Rule 4 | Warning only; confirm or shorten. |
| TYPE/RATING/VOLTAGE/USER2/USER3 is limited to 100 characters, TEXTVALUE/WEBLINK to 255, WDBLKNAM to 60 / X is n characters; the catalog column holds m | Field length | Shorten. |
| WEBLINK must start with http:// or https:// | Link without a scheme | Add `https://`. |
| Another workstation is issuing a part number right now (lock file ...) | `LSE_PartNumber.lock` held by another Librarian or Inventor session | Wait and retry; see section 7 if it persists. |
| Read-back verification failed after the insert / Read-back of the part number failed | The row did not come back from the catalog | The catalog was restored from its backup; check the Access engine and the file, then retry. |
| User circuits do not get catalog records | Catalog option on a circuit (not reachable from the wizard) | Untick. |
| Press 'Catalog record...' to fill in the catalog record, or untick the catalog option | Box ticked, dialog cancelled | Do one or the other. |
