# Glossary: LSE Librarian

Maintained by DCW. Last reviewed 2026-11-12.

Terms used across the code and documentation, in alphabetical order.

| Term | Meaning |
|---|---|
| `.bundle`, `PackageContents.xml` | AutoCAD's autoloader format: a folder named `<name>.bundle` with a manifest, placed in `ApplicationPlugins`. The plugin ships one in `bundle\`. |
| `.DAT` (icon menu) | AutoCAD Electrical icon menu definition: pages `**M<n>`, titles and `label|image|action` entries. Two LSE files: `ACE_NFPA_MENU.DAT` (schematic) and `ACE_PANEL_MENU_NFPA.DAT` (panel). |
| `.wdp` | AutoCAD Electrical project file. Its `+[1]`..`+[4]` lines name the symbol library folders, schematic icon menu, footprint library folders and panel icon menu; the settings form can read them. |
| `$C=` | Icon menu action that runs a command, for example `$C=WD_INS_CIRC_MAIN <path>` to insert a saved circuit. |
| `$S=M<n>` | Icon menu action that opens submenu page `M<n>`. |
| `_V` folder | Vault's local version store inside a working folder. Ignored by the audit. |
| AcadE | AutoCAD Electrical. |
| `AcadDir` | MSBuild property naming the AutoCAD install folder used for references. |
| Action template | `actionTemplate` in the JSON: the menu action with `{PATH}` replaced by the drawing path. |
| ADODB, ACE | COM data access and the Microsoft Access OLEDB provider used to read and write `footprint_lookup.mdb` and `default_cat.mdb`. |
| Audit | `LSELIBRARYAUDIT`: a read-only scan of menus, libraries, images and lookup that lists findings. |
| Symbol link | `WDBLKNAM` of a catalog row: the schematic symbol block AutoCAD Electrical inserts for the part. Set or changed from `LSEFOOTPRINTMAP` (Set symbol); the block must exist in the LSE or stock symbol library. |
| Footprint mapping | `LSEFOOTPRINTMAP`: the pairing of a catalog part (MANUFACTURER + CATALOG) with a footprint DWG, stored as a plain row (no ASSEMBLYCODE) in the manufacturer table of `footprint_lookup.mdb` with `BLKNAM` = the DWG path. |
| `AutoCAD.NET` | NuGet reference package holding AutoCAD's managed API assemblies for compiling without an install. 25.1.0 corresponds to AutoCAD 2026 on .NET 8. |
| Backup folder | `%APPDATA%\LSE\AutoCADElectricalLibrarian\Backups\<stamp>\`: the `.DAT` and MDB before a run, plus the report. |
| `BLKNAM` | `footprint_lookup.mdb` column holding the footprint block name or, in LSE rows, the full DWG path. |
| Breadcrumb | The page trail from M0 shown in the wizard's page list and the audit (`Panel Layout Symbols > LSE FOOTPRINT COLLECTION > BUSS`). |
| `CATALOG` (column) | In `default_cat.mdb` and `footprint_lookup.mdb`: the manufacturer part number (MPN), 60 characters. |
| Catalog backup | `<catalog folder>\LSE_Catalog_Backups\<stamp>\` (or the configured catalog backup folder): the pre-change `default_cat.mdb` plus `CatalogReport.txt`, one folder per catalog record added. |
| Catalog record | One row in a family table of `default_cat.mdb`; the Librarian adds one with a new LSE part number from the wizard or with `LSECATALOGADD`. |
| `CatalogReport.txt` | The line-per-step log of a catalog record add, in the catalog backup folder (always for `LSECATALOGADD`; on failure only when run from the wizard, whose own report carries the lines otherwise). |
| Category | A folder under a kind's library root with a default menu page; listed in the JSON `categories`. |
| Category code | The first two digits of an LSE part number (`45` for fuse holders); defined on the workbook tab's "Category" row or taken from the numbers listed on the tab. |
| Child symbol | A symbol whose fourth character is `2` (a contact or auxiliary of a parent); carries `TAG2`. |
| Deep audit | Audit option that opens every library drawing to check attributes and nested blocks. |
| `default_cat.mdb` | The AutoCAD Electrical catalog database: one table per family plus `_FAMILY_DESCRIPTION`; the Librarian can add a record to it under a new LSE part number. |
| `DESC1`, `MFG`, `CAT`, `INST`, `LOC`, `XREF` | AutoCAD Electrical attribute tags on schematic symbols. |
| Description builder | The part of the catalog record dialog with one box per description field of the workbook tab; "Build description" joins the filled boxes with `, `. |
| Family code | Characters 2 and 3 of a symbol name (`CR` relay, `PB` push button, `DV` generic device, ...). |
| Family table | A table of `default_cat.mdb` named after an AutoCAD Electrical family (`FU`, `CB`, `PB`, ...), described in `_FAMILY_DESCRIPTION`. A catalog record goes into one of them; the family hint in the dialog names the tables already holding numbers with the chosen category code. |
| Footprint | A panel-layout drawing of a component, inserted on the panel drawing; attributes `P_TAG1`, `MFG`, `CAT`, `DESC1`-`3`, `INST`, `LOC`, `P_ITEM`, `XREF`. |
| `footprint_lookup.mdb` | Access database that maps MANUFACTURER + CATALOG to a footprint drawing for catalog-driven insertion. |
| `FP`, `FPT`, `NP` | LSE footprint name prefixes: footprint, terminal footprint (inferred), nameplate. |
| Icon image | PNG shown in the icon menu; LSE images live in `dat MENU\IMAGES` and are mostly 40 x 40 px. |
| In place | A source drawing that already sits at its target path; the Librarian registers it in the menu without copying. |
| Issuance lock, `IssuanceLock` | `LSE_PartNumber.lock` next to `default_cat.mdb`, held while a part number is issued; the same file name the Inventor add-in uses, so both tools serialise on it. |
| Kind | One of `SchematicSymbol`, `PanelFootprint`, `Nameplate`, `UserCircuit`. |
| Legacy number | An ERP part number kept under workbook rule 7 (`45BUSCF1633301`, `45BUSFH1691250`): standard form, but a seven-significant-digit tail at or above the legacy floor 1,000,000. Reserved (never reissued) but not counted when the next sequence is computed. |
| Library root | The top folder for a kind (`SymbolLibraryFolder`, `FootprintLibraryFolder`, `UserCircuitsFolder`). |
| `LibraryLock` | The same lock-file pattern as the issuance lock, for the icon menus: `LSE_Library.lock` in the icon menu folder. |
| LSE | Lake Shore Electric. |
| LSE part number | `[Category 2 digits][Manufacturer code 3 letters][Type 2 letters][Sequence 7 digits]`, 14 characters, e.g. `45BUSFH0000012`; stored as text in the catalog's `USER1` column (setting `LsePartColumn`). |
| `LSE_Catalog_Backups` | Default catalog backup root, next to `default_cat.mdb`. |
| Manufacturer code | Three letters in positions 3 to 5 of an LSE part number (`BUS` Bussmann, `FSC` ...); the Manufacturer \| Code table of each workbook tab defines them, and the union is offered across tabs. |
| Menu root pages | `menuRootPages` in the JSON: the pages whose subtree is the LSE part of a menu (M110, M113, M136; M111; M100, M132; M118). |
| MPN | Manufacturer part number: the `CATALOG` value, and the tail of the purchase description. |
| Naming tab | One `LSE ...` sheet of the workbook that carries a naming block: its codes, patterns, fields, rules, examples and listed numbers (`NamingTab`). |
| Naming workbook | `LSE ELECTRICAL PARTS CATALOG MANAGER.xlsx`, the LSE parts catalog manager; the source of the part-number naming scheme, read directly as a zip of XML. |
| `NETLOAD` | AutoCAD command that loads a managed DLL for the current session. |
| Orientation | First character of a symbol name: `H` for a horizontal rung, `V` for a vertical wire. |
| Orphan | A library drawing not referenced by any icon menu entry. |
| `P_TAG1` | The visible tag attribute of a panel footprint. |
| Parent symbol | A symbol whose fourth character is `1`; carries `TAG1` and the catalog data. |
| Part column | The `default_cat.mdb` column that holds the LSE part number; `USER1` by default (`LsePartColumn`). |
| Prefix | Category + manufacturer + type codes of a part number (`45BUSFH`); the sequence is counted per prefix. |
| Preview (part number) | Read-only computation of the next unused sequence for a prefix, with an explanation of the counts; `CatalogTransactionService.Preview`. |
| Purchase description | `DESCRIPTION, MPN <catalog number>`, written to `USER2` by default (`PurchaseDescriptionInUser2`). |
| Rollback | Restoring the `.DAT` and MDB from the backup and deleting the drawing, icon and folder a failed run created; for the catalog, copying the backup `default_cat.mdb` back. |
| Rule 7 | The workbook rule that existing LSE numbers are kept; in the dialog, "Keep an existing LSE number instead (rule 7)". |
| Rule check | `RuleCheckService.Check`: required, recommended and forbidden attributes and blocks for a kind. |
| Sequence | The last seven digits of an LSE part number, zero-padded, counted per prefix across every family table of the catalog and the numbers listed on the workbook tab; next = highest non-legacy + 1, skipping used values. |
| Series | The three-digit number after the underscore in LSE names (`_002`). Meaning not documented; default `002`. |
| Side database | An AutoCAD `Database` loaded from a file without opening it as a document. Used for inspection and rendering. |
| Slide library reference | Image field of the form `nfpa(s_pb)`: a slide in a `.slb`, used by stock entries; not a file the audit can check. |
| Smoke test | The console checks run during authoring against the built DLL for the non-AutoCAD logic (66 checks for the library and audit code; 67 checks for the workbook, part number and catalog code against copies of the real workbook and catalog). Not shipped. |
| Snapshot (naming) | `LseNamingStandard.json`: the parsed naming standard serialised, used when the workbook cannot be reached; written by "Save naming snapshot (JSON)". Same shape as the Inventor add-in's naming file. |
| Stock libraries | Autodesk's `NFPA` symbol folder and `panel` footprint folder; never written. |
| Type code | Two letters in positions 6 and 7 of an LSE part number (`FH` fuse holder); defined on the workbook tab's "Type" row as `XX = meaning` pairs. |
| `USER1`, `USER2`, `USER3`, `TEXTVALUE`, `WEBLINK`, `WDBLKNAM` | `default_cat.mdb` columns: the LSE part number, the purchase description, free text, `KEY=VALUE;` pairs, a URL, the schematic symbol block name. |
| User circuit | A saved circuit DWG inserted with `WD_INS_CIRC_MAIN`; lives under `User Circuits`. |
| Variant | Optional characters after the parent/child digit in a symbol name (`HDV11_001`, `VSU21_001`). |
| WBLOCK | AutoCAD's write-block: the Librarian uses `Database.Wblock(blockId)` to turn a block definition into a stand-alone drawing. |
| `WD_M`, `WD_PNLM` | AutoCAD Electrical drawing-settings blocks that must not be inside a library drawing. |
| `X?TERM??` | Wire connection attributes on schematic symbols (`X1TERM01`, `X4TERM02`...). Recommended by the rule check. |
