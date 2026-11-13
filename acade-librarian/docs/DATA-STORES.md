# Data stores: LSE Librarian

Maintained by DCW. Last reviewed 2026-11-12.

Every file, database, lock and generated artefact the plugin reads or writes. Paths below use the LSE defaults from `Locations.txt`; all of them are settings.

| Store | Location | Read | Written |
|---|---|---|---|
| `settings.json` | `%APPDATA%\LSE\AutoCADElectricalLibrarian\settings.json` | every command | settings form, after each successful run (recent items) |
| `LseLibraryStandard.json` | next to the DLL, or the shared copy named in settings | wizard, audit, diagnostics | never |
| Symbol library | `C:\_Lake_Shore_E_Vault\CAD Standards\AutoCAD\ECAD Standards\Symbol Library\LSE E-Cad Symbols` | audit, name clash check | one new `.dwg` per filed symbol |
| Stock symbol library | `...\Symbol Library\NFPA` | name clash check, audit | never |
| Footprint library | `...\Footprint Library\LSE E-Cad Footprints\<category>` | audit, clash check | one new `.dwg` per filed footprint or nameplate |
| Stock footprint library | `...\Footprint Library\panel` | audit (block names) | never |
| User circuits | `...\User Circuits\<group>` | audit, clash check | one new `.dwg` per filed circuit |
| Icon menus | `...\dat MENU\ACE_NFPA_MENU.DAT`, `...\dat MENU\ACE_PANEL_MENU_NFPA.DAT` | wizard page list, audit, diagnostics | one appended entry (and optionally one page) per run |
| Icon images | `...\dat MENU\IMAGES\` (the folder on disk is `Images`; Windows is case-insensitive) | wizard "existing image" list, audit | one `<Name>.png` per run unless an existing image is reused |
| Footprint lookup | `...\E-CAD .mbd ACCESS FILES\AeData\en-US\Catalogs\footprint_lookup.mdb` | audit, settings test | one row per registered footprint |
| Catalog | `...\Catalogs\default_cat.mdb` | catalog record dialog (family tables, columns, MANUFACTURER values, issued part numbers), diagnostics, settings test, snapshot family hints | one row per catalog record added (wizard option or `LSECATALOGADD`) |
| Parts catalog workbook | `LSE ELECTRICAL PARTS CATALOG MANAGER.xlsx` (discovered near the catalog, in the ECAD Standards root or `Documents\AutoCAD_Electrical_2026`) | catalog record dialog, wizard validation, diagnostics, settings test, snapshot | never |
| `LseNamingStandard.json` | next to the DLL, or the path named in settings | fallback when the workbook is missing | "Save naming snapshot (JSON)" in the settings form |
| Backups | `%APPDATA%\LSE\AutoCADElectricalLibrarian\Backups\<yyyyMMdd_HHmmss>\` | recovery by hand | every run |
| Catalog backups | `<catalog folder>\LSE_Catalog_Backups\<yyyyMMdd_HHmmss>\` (or `CatalogBackupFolder`) | recovery by hand | every catalog record add |
| Lock | `<dat MENU>\LSE_Library.lock` | | held for the duration of a run |
| Issuance lock | `<catalog folder>\LSE_PartNumber.lock` | | held while a part number is issued |
| Temp exports | `%TEMP%\LSE_Librarian\<block>_<time>.dwg` | inspection, icon preview | wizard; deleted on close |
| Audit report | wherever **Export report...** saves it (defaults to the backup folder) | | audit form |

## 1. settings.json

Serialised `AppSettings`. Blank paths are filled at load time when the LSE standards root `C:\_Lake_Shore_E_Vault\CAD Standards\AutoCAD\ECAD Standards` (or the same path on another fixed or network drive) exists.

| Property | Default | Meaning |
|---|---|---|
| `SymbolLibraryFolder` | `<root>\Symbol Library\LSE E-Cad Symbols` | Root for schematic symbols. |
| `StockSymbolLibraryFolder` | `<root>\Symbol Library\NFPA` | Stock symbols; used for name clash checks and to resolve bare block names in the audit. |
| `FootprintLibraryFolder` | `<root>\Footprint Library\LSE E-Cad Footprints` | Root for footprints and nameplates; category folders live under it. |
| `StockFootprintLibraryFolder` | `<root>\Footprint Library\panel` | Stock footprints. |
| `UserCircuitsFolder` | `<root>\User Circuits` | Root for saved circuits. |
| `IconMenuFolder` | `<root>\dat MENU` | Holds both `.DAT` files, the images subfolder and the lock. |
| `SchematicMenuFile` | `ACE_NFPA_MENU.DAT` | File name inside the icon menu folder. |
| `PanelMenuFile` | `ACE_PANEL_MENU_NFPA.DAT` | File name inside the icon menu folder. |
| `ImagesSubfolder` | `IMAGES` | Subfolder of the icon menu folder; also the prefix written into menu entries. |
| `FootprintLookupPath` | `<root>\E-CAD .mbd ACCESS FILES\AeData\en-US\Catalogs\footprint_lookup.mdb`, else `Documents\Acade 2026\AeData\en-US\Catalogs\footprint_lookup.mdb` | |
| `CatalogPath` | same folder, `default_cat.mdb` | The AutoCAD Electrical catalog that receives records (section 10). Was reference only before the catalog record feature. |
| `NamingWorkbookPath` | `SettingsService.DiscoverWorkbook`: `LSE ELECTRICAL PARTS CATALOG MANAGER.xlsx` in the catalog's folder or up to four parents above it, in `<root>`, in `<root>\E-CAD .mbd ACCESS FILES\`, or in `Documents\AutoCAD_Electrical_2026\`; blank when not found | The naming scheme source (section 9). |
| `NamingStandardPath` | `LseNamingStandard.json` next to the DLL | Fallback snapshot when the workbook file is missing. |
| `CatalogBackupFolder` | blank = `<catalog folder>\LSE_Catalog_Backups` | Root for catalog backups (section 10). |
| `LsePartColumn` | `USER1` | Catalog column read and written for the LSE part number; validated as a column name, blank becomes `USER1`. |
| `OfferCatalogRecord` | `true` | Shows the catalog record group on wizard step 3 (symbols, footprints, nameplates). |
| `PurchaseDescriptionInUser2` | `true` | Pre-ticks "USER2 = purchase description" in the catalog record dialog. |
| `LibraryStandardPath` | `LseLibraryStandard.json` next to the DLL | Point at a shared copy. |
| `BackupFolder` | `%APPDATA%\LSE\AutoCADElectricalLibrarian\Backups` | One subfolder per run. |
| `ReferenceProjectPath` | blank | The `.wdp` last used by "Read library paths from project". |
| `IconPixels` | `40` | Width and height of generated icons (16 to 1024). |
| `DefaultSeries` | `002` | Pre-filled series in the name builder. |
| `RegisterFootprintLookup` | `true` | Pre-ticks the lookup registration box for footprints. |
| `DeepAudit` | `false` | Pre-ticks "Open every drawing" in the audit. |
| `AuditStockEntries` | `false` | Pre-ticks "Include stock AutoCAD Electrical pages". |
| `RecentItems` | `[]` | Paths of the last 25 filed items. |

"Read library paths from project" parses the `.wdp`: `+[1]` (semicolon list of symbol library folders; the one containing "LSE" becomes the symbol root, the one named `NFPA` the stock root), `+[2]` (schematic `.DAT` full path: folder and file name), `+[3]` (footprint folders: "LSE" and the other), `+[4]` (panel `.DAT` file name; a different folder is reported, not applied, because the Librarian uses one icon menu folder). The template `E-CAD CUSTOM PROJECT TEMPLATE 2.wdp` in this repository carries exactly these lines.

## 2. LseLibraryStandard.json

Deserialised into `LibraryStandard`; dictionaries are rebuilt case-insensitive. Loading fails with a plain message when a kind is missing, a `namePattern` is absent or invalid, or an `actionTemplate` lacks `{PATH}`.

| Field | Default in the shipped file | Meaning |
|---|---|---|
| `source` | provenance text | Shown in diagnostics. |
| `imagePrefix` | `IMAGES\` | Prefix written before the image file name in menu entries. |
| `ignoreFolders` | `_V`, `LSE_Library_Backups` | Folder names skipped when scanning libraries (plus any folder starting with `_`). `_V` is Vault's local version store. |
| `series` | `001`..`006` with descriptions | Offered in documentation only; the wizard takes any three digits. |
| `symbolFamilies` | 46 two-letter codes with descriptions | Name builder family list, taken from the stock NFPA menu block names plus `DV`, `PW`, `SU`, `XF` as used by LSE. |
| `kinds.SchematicSymbol`, `kinds.PanelFootprint`, `kinds.Nameplate`, `kinds.UserCircuit` | one `KindRule` each | See below. All four must exist. |

`KindRule` fields:

| Field | Meaning |
|---|---|
| `displayName` | Wizard and audit text. |
| `menu` | `schematic` or `panel`: which `.DAT` the kind is listed in. |
| `prefixes` | Prefix choices for the name builder (`FP`, `FPT` for footprints; `NP` for nameplates; none for symbols and circuits). |
| `namePattern` | .NET regex the file name (without `.dwg`) must match. Named groups `orientation`, `family`, `role`, `variant`, `prefix`, `manufacturer`, `series`, `description` are read back when the wizard is opened on an existing file. |
| `nameExample`, `nameHelp`, `maxNameLength` | Wizard text and length limit (64 in the shipped file; AutoCAD allows 255, AutoCAD Electrical's own limit was not verified). |
| `requiredAttributes` | Attribute tags that must exist in the drawing; `A|B` means either. Error in the rule check. |
| `recommendedAttributes` | Warning when absent. |
| `requiredAttributePatterns`, `recommendedAttributePatterns` | Regexes at least one attribute tag must match (symbols recommend `^X[1-8]TERM[0-9]{2}$`, the wire connection points). |
| `forbiddenBlocks` | Nested block names that make the drawing invalid as a library item (`WD_M`, `WD_PNLM`). |
| `actionTemplate` | Menu entry action with `{PATH}` replaced by the full DWG path. |
| `menuRootPages` | Page ids whose reachable subtree is the LSE part of the menu. |
| `categories` | `name`, `folder` (relative to the kind's root; blank = the root), `menuPage` (default page for that category). |

The shipped categories and pages were read from the LSE menus:

| Kind | Category folder | Page |
|---|---|---|
| Schematic symbol | (root) | M110 LSE COMPONENTS, M113 KIRK SCHEMES, M137 CTs, M138 Isolation transformers |
| Panel footprint | `INSULATORS & WIRE MANAGMENT FOOT PRINTS` | M101 |
| Panel footprint | `LUGS & TERMINAL BLOCKS FOOT PRINTS` | M102 |
| Panel footprint | `LSE PANELS & SUPPORTS FOOT PRINTS` | M103 (subpages M104 to M107, M134) |
| Panel footprint | `LSE BUSS FOOT PRINTS` | M108 (subpages M109 to M115, M119) |
| Panel footprint | `COMPONENT PANEL FOOTPRINTS` | M116 (subpages M120 to M131, M133) |
| Panel footprint | `CONDUIT & HANGER FOOTPRINTS` | M132 |
| Nameplate | `NAME PLATE & LABEL FOOT PRINTS` | M118 |
| User circuit | `UL0891\K-SERIES`, `UL1008\ATS-MTS\...`, `QUICK CONNECT\QCM\CONTROL CIRCUITS`, `AMD CIRCUITS\...` | M112, M114, M115, M119 to M125 |

The wizard's page list is not limited to these: every page reachable from the root pages can be chosen, and a new page can be created under any of them.

## 3. Icon menu .DAT files

Text, UTF-8 with BOM, CRLF, no blank lines. Structure as parsed by `IconMenuService`:

```
; comment lines (header)
**M0
D0                               optional layout line: D<n> or D<n>W
Panel Layout Symbols             title
Push Buttons|s2(s_pb)|$S=M3      entries: label|image|action
...
**M100
LSE FOOTPRINT COLLECTION
INSULATORS & WIRE MANAGMENT|IMAGES\INSULATOR.png|$S=M101
**M101
WIRE MANAGMENT
WIRE TIE DOWN|IMAGES\FP_002HEYCO-TIEDOWN.png|C:\_Lake_Shore_E_Vault\...\FP_002-HEYCO-WIRE_TIEDOWN-PM250.dwg
```

Image field forms: `name(slide)` = slide library (not a file, not checked), `IMAGES\x.png` = relative to the `.DAT` folder, an absolute path, or empty. Action forms: `$S=M<n>`, `$C=<command> [argument]` (argument treated as a path when it contains a backslash or ends with `.dwg`), a bare block name, or a full path. Anything else starting with `$` is kept and not interpreted.

The Librarian appends to the chosen page in the same three-field form. A new page is written at the end of the file as `**M<n>`, title, entries, and linked from the parent with `title|<image of the new item>|$S=M<n>`. The `.DAT` is written through `<file>.tmp` then `File.Move(..., true)`.

Observed in the LSE files on 2026-11-10: schematic menu 86 pages, highest M138; panel menu 53 pages, highest M134; 26 and 36 LSE pages respectively. No page id is duplicated in either file.

## 4. Icon images

PNG (or BMP, JPG, GIF, SLD referenced by stock entries). 235 of the 288 existing LSE images are 40 x 40 pixels, so `IconPixels` defaults to 40. Generated icons are black lines on white, anti-aliased, with an 8 percent margin. The file name is `<Name>.png` and must not already exist unless "Reuse an existing image" is chosen, in which case nothing is written and the entry references the chosen file.

## 5. footprint_lookup.mdb

Access database, one table per `MANUFACTURER` value. Columns used: `CATALOG` (60), `ASSEMBLYCODE` (24), `BLKNAM` (255), `DESCRIPTION` (150), `RECNUM` (counter). The Librarian creates a missing manufacturer table with exactly these columns (same statement as the Inventor add-in), then inserts or updates the row with `ASSEMBLYCODE IS NULL` for the catalog number. `BLKNAM` is the full DWG path, the convention of the existing LSE rows. Opened through ADODB with provider fallback `Microsoft.ACE.OLEDB.16.0`, `12.0`, `Microsoft.Jet.OLEDB.4.0`; needs the 64-bit Access Database Engine. The audit reads every row with a `BLKNAM` and reports those whose path does not exist.

**Symbol link (2026-11-12).** `WDBLKNAM` (the schematic symbol block, 60 characters in the real tables) is updated on an existing family-table row by `CatalogTransactionService.SetSymbols`, which matches `[part column]` + `MANUFACTURER` + `CATALOG` within the family table and refuses unless exactly one row matches. Backup and `SymbolLinkReport.txt` go to `<CatalogBackupFolder or catalog folder\LSE_Catalog_Backups>\<stamp>_symbollink\`; the lock is `LSE_PartNumber.lock`. Tables without a WDBLKNAM column are refused (listing skips them).

**Footprint mapping (`LSEFOOTPRINTMAP`, 2026-11-12).** A "plain" row is one whose `ASSEMBLYCODE` is null or empty (the real stock rows use both); the mapping reads and writes only plain rows, one per catalog number, and never touches assembly rows. The stock tables hold about 519,000 rows (the `AB` table 236,000), so a mapping listing fetches only the manufacturer tables of LSE parts, each once with `Recordset.GetRows` (about 5 s in total), and single-part checks use `CATALOG = ...`. Stock `BLKNAM` values are often bare block names (`AB/LT-PILOT LIGHTS/ABLT3A`); the mapping dialog shows them as `Block name` and leaves them alone. A mapping run uses the same lock file as the library transaction (`LSE_Library.lock` in the icon menu folder) and writes its backup and `FootprintMapReport.txt` to `<BackupFolder>\<stamp>_footprintmap\`. The key of a mapping is the pair MANUFACTURER (table name, case-insensitive) and CATALOG, the same pair a schematic symbol carries in MFG/CAT and `default_cat.mdb` stores in MANUFACTURER/CATALOG.

## 6. Backups and the report

Every run creates `<BackupFolder>\<yyyyMMdd_HHmmss>\` holding the `.DAT` that is about to change, `footprint_lookup.mdb` when registering, and `LibraryReport.txt`, the line-per-step log. On failure the log ends with `ERROR:`, the rollback outcomes and a `Detail:` line with the exception. The library drawing and icon image are not backed up: they are new files, and rollback deletes them.

## 7. Lock file

`LSE_Library.lock` in the icon menu folder, opened with `FileShare.None` and `FileOptions.DeleteOnClose`, holding `user@machine timestamp`. A second workstation retries for 10 seconds, then fails. A crash leaves no file (delete-on-close), but a hung AutoCAD does; diagnostics report a present lock file.

## 8. Temporary exports

When the source is a block in the current drawing, inspection and icon preview need a file: the block is WBLOCKed to `%TEMP%\LSE_Librarian\<block>_<HHmmssfff>.dwg`, reused while the block name is unchanged, and deleted when the wizard closes. The final library file is a fresh WBLOCK straight to the target path.

## 9. The parts catalog workbook and its JSON snapshot

### 9.1 What is read

`LSE ELECTRICAL PARTS CATALOG MANAGER.xlsx` is the LSE parts catalog manager, maintained in Excel. The Librarian never writes it and does not use Excel: an `.xlsx` is a zip of XML parts, so `NamingWorkbookService.ReadSheets` opens the file with `System.IO.Compression` (shared read and write, so the file may stay open in Excel) and reads `xl/workbook.xml` (sheet names), `xl/_rels/workbook.xml.rels` (sheet parts), `xl/sharedStrings.xml` and each sheet part with `XmlReader`. Only sheets whose name starts with `LSE ` are read; numbers are rendered without decimals, booleans as `TRUE`/`FALSE`, and every cell is trimmed with line breaks collapsed.

Each `LSE ` tab starts with a "PROPOSED ... NAMING & DESCRIPTION STANDARD" block. From it the parser takes:

| Block | Rows and columns read | Goes to |
|---|---|---|
| Title | Row 1, column A, when it contains "NAMING" | `NamingTab.Title` |
| "Part number:" | Column B; the widths from `[Category 2][Mfr code 3][Type 2][Sequence 7]` (first tab that states them wins; default 2+3+2+7 = 14) | `PartNumberRule`, `NamingStandard.Format` |
| "Description:", "Purchase Desc.:" | Column B | `DescriptionPattern`, `PurchaseDescriptionPattern` |
| "Part # Segment" table | The `Category` row's rule text (every two-digit number = a category code); the `Type` row's `XX = meaning; YY = meaning` pairs | `CategoryCodes`, `CategoryRule`, `Types` |
| Manufacturer \| Code table (beside the segment table) | Columns found from the "Manufacturer" and "Code" cells of the segment header row (they differ per tab; defaults F and G); rows until the first part table or "APPENDIX"; `ABB / KEY` names with `ABB / KEY` codes are split pairwise | `Manufacturers` (tab) and `NamingStandard.Manufacturers` (union) |
| "Description Field" table | Field name, format, allowed values, example per row until a blank, "Examples", "APPENDIX" or a part table; the numbered rules (`1. `, `2. `...) in the "Rules" column within the next 16 rows | `DescriptionFields`, `Rules` |
| "Examples" rows | Column B (part number), C (description), D (purchase description), while column B is a standard-form number | `Examples` |
| Part tables | Every row block under an "Internal ID" header: every standard-form value of the "LSE Part Number" and "LSE Recommended Part Number" columns; the "Manufacturer" column beside a recommended number counts the spelling used with its manufacturer code | `KnownPartNumbers`, `ManufacturerSpellings`, `ManufacturerCode.AcadeManufacturer` |

A tab whose Category rule names no codes takes them from the numbers listed on the tab (and says so in `CategoryRule`). A tab without a "Part # Segment" table or without type codes is skipped and listed in `SkippedTabs`. On 2026-11-10 the real workbook gave 33 tabs, 314 manufacturer codes, 152 type codes and 9,802 listed numbers in about 0.4 s; the skipped tabs were `LSE LABELS & NAMEPLATES` and `LSE CHEMICALS & CONSUMABLES`.

### 9.2 Where it is found and cached

`SettingsService.DiscoverWorkbook` fills `NamingWorkbookPath` when it is blank (see section 1). `NamingWorkbookService.LoadWorkbook` caches the parsed standard in memory under the upper-cased full path, the last-write time and the file size, so a saved workbook is re-read on the next load and an unchanged one is not; **Reload** in the dialog forces a parse.

### 9.3 The JSON snapshot

`NamingWorkbookService.Load(settings)` prefers the workbook; when `NamingWorkbookPath` is blank or the file is missing it reads `NamingStandardPath` (`LseNamingStandard.json` next to the DLL, shipped from `config\LseNamingStandard.json`). The snapshot is the `NamingStandard` object serialised with indented, case-insensitive JSON and has the same shape as the Inventor add-in's naming file, so that file loads here too:

```
{
  "Source": "LSE ELECTRICAL PARTS CATALOG MANAGER.xlsx (snapshot 2026-11-10 14:02 by dwisniewski)",
  "Loaded": "2026-11-10T14:02:11",
  "FromWorkbook": true,
  "Format": { "CategoryChars": 2, "ManufacturerChars": 3, "TypeChars": 2, "SequenceChars": 7 },
  "Tabs": [
    {
      "Name": "LSE FUSE HOLDERS", "Title": "...", "PartNumberRule": "...",
      "CategoryCodes": ["45"], "CategoryRule": "...",
      "Types": { "FH": "fuse holder", ... },
      "Manufacturers": { "BUS": "Bussmann", ... },
      "DescriptionPattern": "...", "PurchaseDescriptionPattern": "...",
      "DescriptionFields": [ { "Name": "...", "Format": "...", "AllowedValues": "...", "Example": "..." } ],
      "Rules": [ "1. ...", "2. ..." ],
      "Examples": [ { "PartNumber": "45BUSFH0000001", "Description": "...", "PurchaseDescription": "..." } ],
      "KnownPartNumbers": [ "45BUSFH0000001", ... ],
      "ManufacturerSpellings": { "BUS": "BUSSMANN" }
    }
  ],
  "SkippedTabs": [ "LSE LABELS & NAMEPLATES", "LSE CHEMICALS & CONSUMABLES" ],
  "Manufacturers": { "BUS": { "Name": "Bussmann", "AcadeManufacturer": "BUSSMANN" }, ... },
  "FamilyHints": { "45": [ "FU", ... ] }
}
```

**Save naming snapshot (JSON)** in the settings form parses the workbook (bypassing the cache), adds `FamilyHints` from the catalog (category code → family tables already holding numbers with that code), stamps `Source` and writes the file through `.tmp` and move. `FamilyHints` is the only field the workbook does not supply. Diagnostics and the dialog say which source is in use (`Naming standard from workbook` / `from JSON snapshot`).

## 10. default_cat.mdb

The AutoCAD Electrical catalog: one table per family (`FU`, `CB`, `PB`, ...), the `_FAMILY_DESCRIPTION` table (`FAMILY`, `DESC`) that names them, and other `_` tables that the Librarian never touches. Opened through the same ADODB provider fallback as the lookup; needs the 64-bit Access Database Engine.

| Use | Tables and columns |
|---|---|
| Family list in the dialog | Every non-underscore table, with its `DESC` from `_FAMILY_DESCRIPTION`. |
| MANUFACTURER list | `SELECT DISTINCT MANUFACTURER` from the chosen family table. |
| Issued part numbers | The column named by `LsePartColumn` (`USER1`) of every family table, read with `LIKE '<prefix>%'` for the preview and the run, and with `= <number>` for the duplicate check. |
| Family hint | Standard-form numbers in the part column grouped by category code and table. |
| Duplicate check | `MANUFACTURER` + `CATALOG` in the chosen family table. |
| The inserted row | Only columns the table has, only when non-empty: `CATALOG` (the manufacturer part number, 60), `MANUFACTURER` (24), `DESCRIPTION` (150), `TYPE`, `RATING`, `VOLTAGE`, the part column = the LSE part number, `USER2` (the purchase description by default), `USER3`, `TEXTVALUE`, `WEBLINK`, `WDBLKNAM` (60). Sizes are the `DefinedSize` of each column, checked before the insert. A table without the part column is refused. |

The Librarian never updates or deletes a catalog row.

### 10.1 Catalog backups and the report

Every catalog add creates `<CatalogBackupFolder or <catalog folder>\LSE_Catalog_Backups>\<yyyyMMdd_HHmmss>\` (suffixed `_1`, `_2` when the second already exists) holding a copy of `default_cat.mdb` taken before the insert and `CatalogReport.txt`. Stand-alone (`LSECATALOGADD`) the report holds the whole log (`Started`, `Catalog`, `Naming standard`, `Record`, `Part number prefix`, the lock, the backup path, the preview explanation, the insert line, `Status: SUCCESS`). From the wizard the same lines go into `LibraryReport.txt` in the library backup folder, and `CatalogReport.txt` is written only on failure. On failure the backup copy is copied back over the catalog before the error is reported, so a byte-identical catalog is left behind (smoke test).

### 10.2 Issuance lock

`LSE_PartNumber.lock` next to `default_cat.mdb`, opened with `FileShare.None` and `FileOptions.DeleteOnClose`, holding `user@machine timestamp`, held from before the backup copy until the run ends. It is the same file name the Inventor add-in uses, so both tools wait on each other. A second workstation retries 40 times at 250 ms (10 s), then fails with "Another workstation is issuing a part number right now". A crash leaves no file; a hung AutoCAD does, and diagnostics report it.
