# LSE Librarian (AutoCAD Electrical 2026 plugin)

Files every new schematic symbol, panel footprint, nameplate and saved user circuit into the right LSE library folder,
under a name that follows the LSE standard, with an icon-menu entry and icon image, and (for footprints) a
`footprint_lookup.mdb` row, in one logged and reversible run inside AutoCAD Electrical. It can also add the part to
the AutoCAD Electrical catalog `default_cat.mdb` under a new LSE part number, with the naming scheme read straight
from the LSE parts catalog workbook `LSE ELECTRICAL PARTS CATALOG MANAGER.xlsx`. An audit command finds everything
that was dumped outside the libraries, named wrongly, left out of the icon menus or left pointing at missing files.

It is the AutoCAD Electrical counterpart of `../inventor-addin` (LSE Footprint Tools, which issues part numbers and
generates footprints from 3D models) and follows the same shape as `../acade-project-creator`: settings in
`%APPDATA%\LSE\...\settings.json`, a shareable JSON standard, a transaction service that logs every step and rolls
back on failure, WinForms GUI in the LSE code style. It ships alone; no assembly is shared with the sibling plugins.

## Documentation

| Document | Read it for |
|---|---|
| this README | overview, commands, what gets written, catalog records and LSE part numbers, configuration, build, deploy, known gaps |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | components, dependency diagram, the transaction step by step, the catalog step, the icon menu model, design decisions |
| [docs/CODE-REFERENCE.md](docs/CODE-REFERENCE.md) | every public member of every source file, the wizard's behaviour, the catalog record dialog, the commands |
| [docs/DATA-STORES.md](docs/DATA-STORES.md) | settings.json, the library standard JSON field by field, the `.DAT` icon menu format, images, lookup MDB, the parts catalog workbook and its JSON snapshot, `default_cat.mdb`, backups, locks, logs |
| [docs/OPERATIONS.md](docs/OPERATIONS.md) | install, configure, file an item step by step, add a catalog record, run the audit, read the logs, recover by hand, every error message |
| [docs/DEVELOPER-GUIDE.md](docs/DEVELOPER-GUIDE.md) | build with or without AutoCAD, smoke test, conventions, recipes, first-build checklist, known gaps |
| [docs/GLOSSARY.md](docs/GLOSSARY.md) | terms |

These documents are maintained with the `lse-documenter` agent definition in `../agents/lse-documenter.md`. Run it after a change lands to refresh them.

**Status (2026-11-12-r7):** compiles in Release with 0 warnings against the AutoCAD.NET 25.1.0 reference package; the
icon-menu parser, naming rules, transaction (copy, icon, menu entry, new page, rollback, in-place registration) and
audit passed a 66-check console smoke test against copies of the real `ACE_NFPA_MENU.DAT` and
`ACE_PANEL_MENU_NFPA.DAT`, and the catalog-record addition (workbook parsing of every tab, JSON round trip, the legacy
rule, preview, insert and read-back, refused duplicate with byte-identical rollback, hand-typed numbers, the issuance
lock) passed a 67-check console smoke test against a copy of the real workbook and a copy of the real
`default_cat.mdb`. The footprint mapping (`LSEFOOTPRINTMAP`, the optional box in `LSECATALOGADD`, the audit note) passed 35 further checks on copies of the real `footprint_lookup.mdb` and `default_cat.mdb` (60 with the 25 catalog checks): create, update, refused duplicate, read-back, rollback, audit. The symbol link (below, Set symbol in `LSEFOOTPRINTMAP`) compiled in Release with 0 warnings and passed 25 further checks on the same copies (85 in total) on 2026-11-12. The plugin has not yet been loaded into AutoCAD Electrical; the parts that use the AutoCAD API
(drawing inspection, WBLOCK export, icon rendering) and the dialogs inside AutoCAD are unverified. Follow the
first-build checklist in the developer guide before using it on the live Vault working folder.
r7 adds the Vault read-only guard (below); it compiles with 0 warnings and the smoke test gained two read-only refusal checks (84 with the lookup argument), but the Vault behaviour has not been seen against a live Vault.

## Commands

| Command | Purpose |
|---|---|
| `LSELIBRARIAN` | Four-step wizard: Item (kind and source) → Name and folder → Icon menu, icon image, lookup and (optionally) a catalog record in `default_cat.mdb` with a new LSE part number → Review and add |
| `LSECATALOGADD` | Stand-alone catalog record dialog: pick the workbook tab and the category, manufacturer and type codes, preview the next unused LSE part number, fill the AutoCAD Electrical fields, add the row to `default_cat.mdb` (backed up first, rolled back on failure). **This is the catalog-only mode**: a plain part number with no symbol, no footprint, no icon menu or library change and no lookup change unless you tick the optional footprint box; WDBLKNAM stays optional |
| `LSEFOOTPRINTMAP` | Footprint mapping, next to the catalog features: lists every LSE part in `default_cat.mdb` (MANUFACTURER, CATALOG, LSE part number, family) with its current `footprint_lookup.mdb` row, filterable by status (Unmapped, Mapped, Missing drawing, Block name), family and text. Select one or many parts, **Browse...** to a footprint DWG in the footprint library, **Map selected parts**. Creates the manufacturer table and row when missing, updates the path when the part is already mapped (only after you confirm the replacement). One backup, one library lock, read-back, all-or-nothing rollback; the report is `FootprintMapReport.txt` in `<BackupFolder>\<stamp>_footprintmap\`. `LSECATALOGADD` has the same mapping as an optional tick box. **Symbol link (2026-11-12, user-approved):** the same dialog (title "LSE Librarian: catalog symbol and footprint links") has a **Symbol (WDBLKNAM)** column, a Symbol filter (All, No symbol, Has symbol), a **Schematic symbol (block)** box with **Browse...** and **Set symbol**. Set symbol writes WDBLKNAM, and nothing else, on the selected catalog rows, matched by family table + part-number column + MANUFACTURER + CATALOG (exactly one row must match; never inserts or deletes). The block must exist as a `.dwg` in the LSE or stock schematic symbol library; the library's own spelling is stored. A non-empty WDBLKNAM is replaced only after you confirm (default No). Written by `CatalogTransactionService.SetSymbols`: one backup (`<CatalogBackupFolder>\<stamp>_symbollink\` with `SymbolLinkReport.txt`), `LSE_PartNumber.lock`, read-back, all-or-nothing rollback |
| `LSELIBRARYAUDIT` | Scans both icon menus, the three LSE library folders, the images folder and `footprint_lookup.mdb`; lists findings with a suggested fix; "Open in Librarian" registers an orphan drawing in place |
| `LSELIBRARYSETTINGS` | Library folders, icon menu folder and file names, lookup MDB, catalog MDB, parts catalog workbook, naming snapshot JSON, catalog backup folder and part-number column, standard JSON, backup folder, icon size, default series; "Read library paths from project" fills the folders from a `.wdp`; "Save naming snapshot (JSON)" writes the workbook's naming scheme to the fallback file |
| `LSELIBRARYDIAG` | Runtime, settings, every path, both menus parsed (page counts, LSE root pages present), JSON loaded, lookup readable, naming standard loaded (from the workbook or the snapshot), catalog readable (family tables, part column, backup root), both lock files if present |

## What the Librarian writes

For one item named `<Name>` of kind K:

```
<library root for K>\<category folder>\<Name>.dwg      copied from the source file, or WBLOCKed from the current drawing
<dat MENU>\IMAGES\<Name>.png                            rendered from the geometry, taken from the DWG preview, or imported (optional)
<dat MENU>\ACE_NFPA_MENU.DAT or ACE_PANEL_MENU_NFPA.DAT one entry  <label>|IMAGES\<Name>.png|<action>  appended to the chosen page,
                                                        optionally a new **M<n> page linked from a parent page with $S=M<n>
footprint_lookup.mdb                                    (footprints, optional) [MANUFACTURER] CATALOG -> BLKNAM = full DWG path
default_cat.mdb                                         (optional, not for circuits) one row in the chosen family table with USER1 = the new LSE part number
<catalog folder>\LSE_Catalog_Backups\<stamp>\           (only when a catalog record is added) pre-change copy of default_cat.mdb + CatalogReport.txt
%APPDATA%\LSE\AutoCADElectricalLibrarian\Backups\<stamp>\   copy of the .DAT (and the MDB) taken before the change + LibraryReport.txt
```

The `<action>` is the full DWG path for symbols, footprints and nameplates and `$C=WD_INS_CIRC_MAIN <path>` for user
circuits, exactly the forms already used by the LSE pages of both menus. The `.DAT` is rewritten byte for byte except
for the appended lines (UTF-8 BOM and CRLF are preserved; the smoke test proves a load-render round trip is identical).

`LSEFOOTPRINTMAP` writes only `footprint_lookup.mdb` (rows `[MANUFACTURER table] CATALOG -> BLKNAM = full DWG path`, no `ASSEMBLYCODE`), a pre-change copy of it and `FootprintMapReport.txt` under `<BackupFolder>\<stamp>_footprintmap\`, and holds `LSE_Library.lock` in the icon menu folder while it does. It never writes `default_cat.mdb`, a drawing or an icon menu. The footprint must already exist as a `.dwg` inside the footprint library or the stock footprint library. If `LSECATALOGADD` has its optional footprint box ticked, the catalog row is written first (own backup and rollback) and the mapping second (own backup and rollback); a failed mapping leaves the catalog row in place and says so.

`LSELIBRARYAUDIT` adds one Info note per family table that holds LSE parts with no `footprint_lookup.mdb` row ("N of M LSE parts in family X have no footprint_lookup.mdb row"), pointing at `LSEFOOTPRINTMAP`. Parts that never sit on a panel can be left unmapped. Rows already mapped to a missing drawing are the existing Error finding.

`LSECATALOGADD` on its own writes only the `default_cat.mdb` row, its backup folder and `CatalogReport.txt` (plus the mapping above when ticked). While a
part number is being issued (either way) the lock file `LSE_PartNumber.lock` exists next to the catalog; it is deleted
when the run ends.

## Library roots and kinds

| Kind | Root (default, from `Locations.txt`) | Menu | Name standard (regex in the JSON) | Example |
|---|---|---|---|---|
| Schematic symbol | `Symbol Library\LSE E-Cad Symbols` | `ACE_NFPA_MENU.DAT`, pages under M110, M113, M136 | `[HV]<family 2>[12]<variant 0-2>_<series 3 digits>-<DESCRIPTION>` | `HDV1_002-SYM_ROGOW_COIL` |
| Panel footprint | `Footprint Library\LSE E-Cad Footprints\<category folder>` | `ACE_PANEL_MENU_NFPA.DAT`, pages under M100, M132 | `FP|FPT_<series>-<MFG>_<DESCRIPTION>` | `FP_002-EATON_CAM-LOK_MALE` |
| Nameplate | `Footprint Library\LSE E-Cad Footprints\NAME PLATE & LABEL FOOT PRINTS` | panel menu, page M118 | `NP_<series>-<MFG>_<DESCRIPTION>` | `NP_002-LSE_1x2-INCH_A-SIZE_NAMEPLATE` |
| User circuit | `User Circuits\<group folders>` | schematic menu, pages under M111 | capitals, digits, `_ - & .` and spaces | `K891-MAIN-ECAM_CIRC` |

Descriptions are capitals with `_` and `-` as separators and no spaces; the wizard builds the name from the parts and
shows the pattern violations before you can go on. Names must be unique across the whole library (and must not clash
with a stock NFPA symbol) because AutoCAD Electrical resolves blocks by name.

The three-digit series (`001`...`006`) is used consistently in the existing LSE names but its meaning is not written
down anywhere in the inputs this tool was built from; the default is `002` (LSELIBRARYSETTINGS) and the wizard lets you
change it per item.

## Catalog records and LSE part numbers

Symbols, footprints and nameplates (not user circuits) can get a record in the AutoCAD Electrical catalog
`default_cat.mdb` at the same time as they are filed, and `LSECATALOGADD` adds a record on its own, with no drawing at all (a "catalog-only" part: only MANUFACTURER, CATALOG, a family table, a description that passes the tab rules and a part-number prefix are required; the dialog says so at the top). The record
carries a new LSE part number in the column named by the setting `LsePartColumn` (`USER1` by default).

**Format.** `[Category 2 digits][Manufacturer code 3 letters][Type 2 letters][Sequence 7 digits]`, 14 characters,
for example `45BUSFH0000012` (category 45, BUSSMANN, fuse holder, sequence 12). The widths are parsed from the
workbook's "Part number:" rule; 2+3+2+7 is the default when the rule does not state them.

**Where the scheme comes from.** The Librarian reads the workbook `LSE ELECTRICAL PARTS CATALOG MANAGER.xlsx`
directly (it is a zip of XML; no Excel is needed and the file may stay open in Excel). Every sheet whose name starts
with `LSE ` and carries a "PROPOSED ... NAMING & DESCRIPTION STANDARD" block contributes a naming tab: its category
codes, the type codes (`FH = fuse holder, ...`), the Manufacturer | Code table, the description pattern and fields,
the numbered rules, the examples, and every 14-character number already listed in its "LSE Part Number" and "LSE
Recommended Part Number" columns. On 2026-11-10 the workbook gave 33 tabs, 314 manufacturer codes, 152 type codes and
9,802 listed numbers in about 0.4 s. Tabs without a naming block or without type codes are skipped and named in
diagnostics (`LSE LABELS & NAMEPLATES` and `LSE CHEMICALS & CONSUMABLES` on that date); they cannot issue numbers.

**Counting the sequence.** The sequence counts per category + manufacturer + type across every family table of
`default_cat.mdb` and the numbers listed on the chosen workbook tab, so a number is never issued twice. The next
number is max(highest issued in the catalog, highest issued on the tab) + 1, skipping anything already used. The
dialog's **Preview next number** shows the number and why (how many matches, highest in which table, legacy counts).

**The legacy rule.** Legacy ERP numbers kept under workbook rule 7 (`45BUSCF1633301`, `45FSCFH3238731`,
`45BUSFH1691250`, ...) fit the 14-character form but carry a 7-significant-digit tail, whereas numbers issued under
the standard are zero-padded (`0000001`, `0000002`, ...). A sequence at or above the legacy floor 10^(7-1) =
1,000,000 is treated as legacy: it stays reserved (never reissued) but does not move the counter. Worked example from
the smoke test: the `LSE FUSE HOLDERS` tab lists `45BUSFH0000001` to `45BUSFH0000011` and the catalog holds the
legacy `45BUSFH1691250`, so the next number is `45BUSFH0000012`, not `45BUSFH1691251`. To keep an existing LSE number
instead, tick **Keep an existing LSE number instead (rule 7)** and type the full number; it must have the standard
form, start with the chosen codes and be unused in the catalog (it may be listed on the workbook tab).

**Description.** Upper case, no `N/A`, no degree, diameter or prime symbols, 100 characters or fewer by the standard
(warning) and 150 at most (the catalog column). The dialog offers a builder with one box per description field of the
tab. `USER2` is filled with the purchase description (`DESCRIPTION, MPN <catalog number>`) unless you untick it.

**Fallback snapshot.** When the workbook cannot be reached, the naming scheme is read from the JSON snapshot
`LseNamingStandard.json` (shipped from `config\`, same shape as the Inventor add-in's file). **Save naming snapshot
(JSON)** in the settings form parses the workbook and rewrites the snapshot; diagnostics say which source is in use.

**Safety.** The catalog is copied into `<catalog folder>\LSE_Catalog_Backups\<stamp>\` first, the number is issued
under the lock file `LSE_PartNumber.lock` (shared with the Inventor add-in), the row is read back after the insert, and
on any failure the backup is copied back over the catalog. Before the backup and lock the catalog is checked for the read-only attribute (Vault keeps files that are not checked out read-only); a read-only catalog is refused with "Nothing was changed". Afterwards the log lists the catalog as needing check-in. The Librarian never checks files in or out itself.

## Configuration: `config\LseLibraryStandard.json`

Point `LSELIBRARYSETTINGS > Library standard JSON` at a shared copy so everyone files items the same way.

- `kinds.<Kind>.namePattern`, `nameExample`, `nameHelp`, `maxNameLength`, `prefixes` – the naming rule and the wizard text.
- `kinds.<Kind>.requiredAttributes` (`A|B` = either), `recommendedAttributes`, `requiredAttributePatterns`, `recommendedAttributePatterns`, `forbiddenBlocks` – what "Inspect drawing" and the deep audit check (`TAG1|TAG2` and `DESC1` for symbols, `P_TAG1` for footprints, `WD_M` forbidden everywhere).
- `kinds.<Kind>.menu` (`schematic` or `panel`), `menuRootPages` (the LSE pages; every page reachable from them is an LSE page for the audit and the wizard's page list), `actionTemplate` (`{PATH}` or `$C=WD_INS_CIRC_MAIN {PATH}`), `categories` (name, folder under the root, default menu page).
- `symbolFamilies` – the two-letter AutoCAD Electrical family codes offered by the name builder, taken from the stock NFPA menu.
- `series`, `ignoreFolders` (`_V`, backups), `imagePrefix` (`IMAGES\`).

## Configuration: catalog records (`settings.json`)

All of these are optional and default sensibly; older settings files keep working.

| Setting (`LSELIBRARYSETTINGS` row) | Default | Meaning |
|---|---|---|
| `CatalogPath` ("Catalog MDB (default_cat.mdb)") | `<root>\E-CAD .mbd ACCESS FILES\AeData\en-US\Catalogs\default_cat.mdb` | The catalog that receives records. Was reference only before; now used. |
| `NamingWorkbookPath` ("LSE parts catalog workbook (.xlsx)") | discovered: `LSE ELECTRICAL PARTS CATALOG MANAGER.xlsx` next to the catalog MDB or up to four parent folders above it, in the ECAD Standards root, in `<root>\E-CAD .mbd ACCESS FILES\`, or in `Documents\AutoCAD_Electrical_2026\` | The naming scheme source. |
| `NamingStandardPath` ("Naming standard JSON (fallback snapshot)") | `LseNamingStandard.json` next to the DLL | Used when the workbook file is missing. |
| `CatalogBackupFolder` ("Catalog backup folder (blank = next to the catalog)") | blank = `<catalog folder>\LSE_Catalog_Backups` | One subfolder per catalog add. |
| `LsePartColumn` ("Catalog column holding the LSE part number") | `USER1` | Read in every family table; written on insert. |
| `OfferCatalogRecord` | `true` | Shows the catalog group on wizard step 3. |
| `PurchaseDescriptionInUser2` | `true` | Pre-ticks "USER2 = purchase description" in the dialog. |

## Build

- Target `net8.0-windows`, WinForms, x64, nullable. No NuGet packages at runtime.
- On a workstation with AutoCAD Electrical 2026: `dotnet build -c Release` (references `accoremgd/acdbmgd/acmgd` from `C:\Program Files\Autodesk\AutoCAD 2026`, or pass `-p:AcadDir=...`).
- Without an install the project compiles against the `AutoCAD.NET` 25.1.0 reference package (compile-time only).

## Deploy / load

1. Build Release; copy the output folder (DLL + `LseLibraryStandard.json` + `LseNamingStandard.json`) to a share or each workstation.
2. `NETLOAD` → `LSE.AutoCADElectrical.Librarian.dll`, or copy `bundle\LSE.Librarian.bundle` to `%APPDATA%\Autodesk\ApplicationPlugins\` with the build output in `Contents\` for automatic loading.
3. Run `LSELIBRARYSETTINGS` once. On a workstation with the Vault working folder at `C:\_Lake_Shore_E_Vault\CAD Standards\AutoCAD\ECAD Standards` every path is pre-filled; otherwise press **Read library paths from project...** and pick any LSE `.wdp` (its `+[1]`..`+[4]` lines name the symbol and footprint folders and both icon menus). Press **Test**, then **Save**.
4. Run `LSELIBRARYDIAG` and fix anything marked MISSING or FAIL.
5. Run `LSELIBRARYAUDIT` once to see the current state of the libraries before filing anything new.

## Requirements on the workstation

- AutoCAD Electrical 2026 (the host).
- Write access to the Vault working folder that holds the libraries and `dat MENU`. The Librarian does not talk to Vault. Before the first write every file the request will change (icon menu `.DAT`, `footprint_lookup.mdb`, `default_cat.mdb`, an in-place drawing) is checked for the read-only attribute; if any is read-only the run is refused, naming each file, with the instruction to check them out in Vault (Vault tab in AutoCAD or Vault Explorer), and nothing is backed up, locked or written. After a successful run the log (and `LibraryReport.txt`) lists the files in the Vault working folder that now need to be checked in (new files: Add to Vault). The same guard covers `LSEFOOTPRINTMAP` and the symbol link. `LSELIBRARYDIAG` shows the host .NET runtime, the installed Vault client and whether it is compatible, whether the Vault add-in in AutoCAD is logged in, and marks the menus, lookup and catalog "read-only: not checked out in Vault" or "writable". AutoCAD 2026.1.2 and Vault 2026.4 belong together and run on .NET 10 (no API change); this net8.0-windows plugin still loads.
- 64-bit Access Database Engine (ACE 16 or 12), if footprints are registered in `footprint_lookup.mdb` or catalog records are added to `default_cat.mdb`.
- For catalog records: the workbook `LSE ELECTRICAL PARTS CATALOG MANAGER.xlsx` reachable at the configured path, or the JSON snapshot `LseNamingStandard.json` next to the DLL (or at the configured path). No Excel is needed.

## Known gaps / next refinements

- No Vault integration beyond the guard: the Librarian refuses read-only (not checked out) targets before writing and lists what to check in afterwards, but does not check files in or out. New files (DWG, PNG) are not checked for being new-to-Vault. `../inventor-addin/src/FootprintTransactionService.cs` (`VaultCatalogService`) shows the reflection approach if it is wanted. Unverified against a live Vault: the read-only attribute is assumed to reflect check-out state, and the Vault client / add-in session detection in `VaultProbe`.
- No rename or move of an existing library item: the audit tells you what is wrong, the wizard files a correctly named copy, and the old file and its menu entry are retired by hand.
- The audit does not edit the menus (no automatic repair). Everything it reports has a suggested manual or Librarian action.
- Workbook tabs without a "Part # Segment" table or without type codes (`LSE LABELS & NAMEPLATES`, `LSE CHEMICALS & CONSUMABLES` on 2026-11-10) are skipped, so no part number can be issued for them until the tab gets a naming block.
- The legacy-floor rule (sequence at or above 1,000,000 = legacy ERP number, reserved but not counted) is an inference from the numbers in the workbook and the catalog, documented above; the workbook itself only says that existing numbers are kept under rule 7.
- Footprint mapping never deletes a lookup row. The symbol link edits only WDBLKNAM of existing catalog rows (no other field, no new or deleted rows). `LSEFOOTPRINTMAP` is listed in the bundle manifest by the hub; neither form has been shown inside AutoCAD. Whether AutoCAD Electrical uses a changed WDBLKNAM for a part already in a project (it reads the catalog when a part is inserted or updated) is unverified.
- The catalog add never updates or deletes a row; a wrong record is corrected by hand (or restored from `LSE_Catalog_Backups`).
- The meaning of the three-digit series and of the `FPT` prefix is inferred from existing names, not documented.
- Icon rendering draws lines, arcs, circles, polylines, ellipses and splines in black on white at the configured size (40 px by default, matching the existing LSE icons); text and attribute definitions are not drawn. Use "Import an image file" for a hand-made icon.

Maintained by DCW. Last reviewed 2026-11-12.
