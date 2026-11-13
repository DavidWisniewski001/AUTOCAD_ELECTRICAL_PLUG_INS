# Data stores: LSE Footprint Tools

Maintained by DCW. Last reviewed 2026-11-08.

Every file the add-in reads or writes, where it lives, what is in it, and who else touches it. Paths marked *default* can be changed in **LSE Settings**. See [ARCHITECTURE.md](ARCHITECTURE.md) section 5 for the order in which these are touched during a create.

| # | Store | Default location | Add-in access |
|---|---|---|---|
| 1 | `settings.json` | `%APPDATA%\LSE\InventorFootprintTools\` | read on every form open; written by Settings > Save |
| 2 | `LseNamingStandard.json` | beside the DLL | read only |
| 3 | `default_cat.mdb` | `Documents\Acade 2026\AeData\en-US\Catalogs\` or `Documents\AutoCAD_Electrical_2026\Catalogs\` | read; one `INSERT` per create |
| 4 | `footprint_lookup.mdb` | beside the catalog | read; `CREATE TABLE`, `INSERT` or `UPDATE` per create |
| 5 | `<PN>.dxf`, `<PN>.dwg` | `<catalog folder>\LSE_Footprints\` | created per create; deleted on rollback |
| 6 | `LSE_Catalog_Backups\<stamp>\` | beside the catalog | created per create |
| 7 | `LSE_PartNumber.lock` | beside the catalog | created and deleted per create |
| 8 | Autodesk Vault | `lse-cep-sql` / `Lake_Shore_E_Vault` | optional checkout and checkin of the catalog |
| 9 | Temporary Inventor drawing | in memory | created and discarded per projection |

## 1. settings.json

Per user. Written as indented JSON by `SettingsService.Save` through a `.tmp` file and an atomic move. Deleting it resets every setting to the defaults below; a corrupt file is also treated as defaults.

| Property | Default | Meaning |
|---|---|---|
| `CatalogPath` | auto-discovered (see table above), else blank | `default_cat.mdb`. |
| `FootprintLookupPath` | `footprint_lookup.mdb` beside the catalog | AutoCAD Electrical footprint lookup database. |
| `FootprintLibraryFolder` | `LSE_Footprints` beside the catalog | Where `<PN>.dxf` and `<PN>.dwg` are written. Must be readable by AutoCAD Electrical at the path stored in `BLKNAM`, so on a multi-user site this should be a share. |
| `NamingStandardPath` | `LseNamingStandard.json` beside the DLL | Point every workstation at one shared copy. |
| `DrawingTemplatePath` | blank = Inventor's default drawing template | Used only for the temporary projection drawing. |
| `FootprintUnits` | `in` | `in` or `mm`. Units of the DXF and of every size shown in the wizard. |
| `GeometryLayer` | `0` | DXF layer for the curves. |
| `AttributeLayer` | `0` | DXF layer for the `ATTDEF`s. |
| `TagTextHeight` | `0.125` | Attribute text height in inches (multiplied by 25.4 when units are mm). |
| `CurveTolerance` | `0.005` | Sampling tolerance, in footprint units, for ellipses and splines. |
| `MaxCurves` | `4000` | Projection aborts above this many curve segments. |
| `ConvertToDwgWithAutoCad` | `true` | Run the AutoCAD DXF to DWG step. |
| `AppendMpnToDescription` | `true` | Catalog `DESCRIPTION` becomes `<description>, MPN <catalog>`. |
| `LsePartAttribute` | `USER1` | Name of the invisible footprint attribute that carries the LSE part number. Stored upper-case. |
| `VaultExplorerPath` | `C:\Program Files\Autodesk\Vault Client 2026\Explorer` | Folder holding the three Vault SDK DLLs. |
| `VaultServer` | `lse-cep-sql` | Vault server host. |
| `VaultName` | `Lake_Shore_E_Vault` | Vault name. |
| `CatalogVaultPath` | blank = Vault disabled | Vault path of `default_cat.mdb`, for example `$/Catalogs/default_cat.mdb`. |

Blank path properties are filled with the defaults at load time but the blanks themselves are what gets saved, so moving the catalog later re-derives the other paths automatically unless they were set explicitly.

## 2. LseNamingStandard.json

Generated from the LSE Electrical Parts Catalog Manager workbook by `tools/extract_naming_standard.py` (see [CODE-REFERENCE.md](CODE-REFERENCE.md)). The copy in `config/` was generated 2026-11-08 and ships beside the DLL. Property names are matched case-insensitively.

```json
{
  "source": "LSE ELECTRICAL PARTS CATALOG MANAGER.xlsx (generated 2026-11-08 by tools/extract_naming_standard.py)",
  "format": { "categoryChars": 2, "manufacturerChars": 3, "typeChars": 2, "sequenceChars": 7 },
  "tabs": [
    {
      "name": "LSE FUSE HOLDERS",
      "categoryCodes": ["25", "45", "76"],
      "types": { "FH": "fuse holder / fuse block", "FU": "fuse-holder part or accessory (base, condenser, cover)" },
      "manufacturers": { "BUS": "Bussmann (Eaton)", "MER": "Mersen / Ferraz Shawmut" },
      "descriptionPattern": "Format"
    }
  ],
  "manufacturers": { "ABB": { "name": "ABB", "acadeManufacturer": "ABB" } },
  "familyHints": { "01": ["PM", "PLCIO", "MISC_CAT"] }
}
```

| Key | Content | Used for |
|---|---|---|
| `source` | Workbook name and generation date | Shown in the wizard log and the Settings test. |
| `format` | Segment lengths | Validation regexes and the sequence cap. Changing these changes the length of every number issued; do not change them after go-live. |
| `tabs[]` | One per `LSE ...` worksheet that defines type codes | Wizard step 3 lists. 33 tabs in the shipped file. |
| `tabs[].categoryCodes` | Two-digit codes allowed on that tab | Category combo. Six shipped tabs have an empty list (`LSE TEST SWITCHES & BLOCKS`, `LSE ELECTRONIC COMPONENTS`, `LSE LUGS & CONNECTORS`, `LSE ENCLOSURES & PANELS`, `LSE HARDWARE & FASTENERS`, `LSE MISCELLANEOUS`); the engineer must type the code. |
| `tabs[].types` | Code to meaning | Type combo (drop-down list only; codes not on the tab cannot be chosen). |
| `tabs[].manufacturers` | Code to name for that tab | Shown first in the manufacturer-code combo; all global codes follow. |
| `tabs[].descriptionPattern` | Column B of the tab's Description row | Placeholder text in the description box. In the shipped file every tab has the literal `Format`, which means the extractor picked up a header cell rather than a pattern; see Known gaps in the [README](../README.md). |
| `manufacturers` | All 242 known codes with `name` and `acadeManufacturer` | Pre-fills `MANUFACTURER` from `acadeManufacturer` (the value most often found in the live catalog for that code), falling back to `name` upper-cased. |
| `familyHints` | Category code to up to three family tables, most used first | Pre-selects the family table when the category changes. 45 categories in the shipped file. |

## 3. default_cat.mdb (AutoCAD Electrical catalog)

Owned by AutoCAD Electrical. One table per component family (`PB`, `CR`, `FU`, `TRMS`, ...), plus system tables starting with `_` (for example `_FAMILY_DESCRIPTION` with `FAMILY` and `DESC` columns) and `MSys*`. About 1.3 million rows in total according to the README.

Columns the add-in cares about:

| Column | Read | Written | Notes |
|---|---|---|---|
| `USER1` | every family table, `LIKE '<prefix>%'` | yes | Holds the LSE part number. Tables without this column cannot receive a record and are silently skipped during the scan. |
| `MANUFACTURER` | duplicate check | yes | Limit 24 characters (enforced by the add-in, not read from the schema). |
| `CATALOG` | duplicate check | yes | Limit 60. |
| `DESCRIPTION` | no | yes | Limit 150. Form: `<DESCRIPTION>, MPN <catalog>` when the append setting is on. |
| `WEBLINK` | no | yes, when the column exists and the box is filled | Datasheet link. |
| `TYPE, RATING, USER2, USER3, TEXTVALUE, WDBLKNAM` | no | supported by `CatalogAddRequest` but left empty by the wizard | |

Rules enforced in code: exactly one `INSERT` with an explicit column list per create; never `UPDATE` or `DELETE`; refuse if `MANUFACTURER` + `CATALOG` already exists in the chosen table; read the row back after insert. Whole-file restore from the backup on any later failure.

Part number format: `[category 2 digits][manufacturer 3 letters][type 2 letters][sequence 7 digits]`, for example `45BUSFH0000001`. The sequence is per prefix, across the whole catalog, highest + 1. Values in `USER1` that do not match the pattern for the requested prefix are ignored.

## 4. footprint_lookup.mdb

Owned by AutoCAD Electrical. One table per manufacturer, named exactly as the catalog `MANUFACTURER` value. AutoCAD Electrical resolves a panel footprint by opening the table named after the component's manufacturer and matching `CATALOG`.

Table shape created by the add-in when a manufacturer table is missing:

```
CATALOG       TEXT(60)
ASSEMBLYCODE  TEXT(24)
BLKNAM        TEXT(255)   full path of the footprint DWG
DESCRIPTION   TEXT(150)   the LSE description without MPN
RECNUM        COUNTER
```

The add-in writes rows with `ASSEMBLYCODE` null and updates an existing row for the same `CATALOG` rather than adding a second. `BLKNAM` is the absolute DWG path, the same convention as the existing `LAKE SHORE` row which points into `C:\_Lake_Shore_E_Vault\...` (README). A path over 255 characters is refused before the write.

## 5. Footprint drawings

`<FootprintLibraryFolder>\<PartNumber>.dxf` and `.dwg`. The create refuses to run if either already exists.

DXF: AutoCAD R12 (`AC1009`), one layer table entry for `0` plus the configured layers, linetype `CONTINUOUS`, style `STANDARD` with font `txt`. Geometry centred on 0,0 in footprint units. Entities: `LINE`, `CIRCLE`, `ARC`, `POLYLINE`/`VERTEX`/`SEQEND`, then `ATTDEF`s:

| Tag | Visible | Pre-filled with |
|---|---|---|
| `P_TAG1` | yes | blank |
| `MFG` | no | `MANUFACTURER` |
| `CAT` | no | catalog number |
| `ASSYCODE`, `DESC1`, `DESC2`, `DESC3`, `INST`, `LOC`, `P_ITEM`, `XREF`, `XREFNO` | no | blank |
| `<LsePartAttribute>` (default `USER1`) | no | the issued part number |

DWG: produced by AutoCAD opening the DXF and saving as 2018 format. Not produced when the conversion setting is off; the log then says what to save by hand.

Not verified on a workstation: that AutoCAD Electrical accepts this attribute set and a plain DWG as a footprint. Adjust `DxfFootprintWriter.StandardAttributes` if the site's footprint library uses a different set.

## 6. LSE_Catalog_Backups

`<catalog folder>\LSE_Catalog_Backups\yyyyMMdd_HHmmss\` is created for every create attempt, successful or not. Contents:

- a copy of `default_cat.mdb` taken before any write
- a copy of `footprint_lookup.mdb` taken before any write
- `TransactionReport.txt`: the full log, one line per step, ending with `Status: SUCCESS` or `ERROR: ...` followed by the rollback results

The sister AutoCAD Electrical plugin writes to the same folder for its admin adds. Nothing prunes this folder. Each backup is the full size of both databases, so on a busy catalog the folder grows quickly; archive or delete old stamps by hand.

## 7. LSE_PartNumber.lock

`<catalog folder>\LSE_PartNumber.lock`. Opened exclusively with delete-on-close and written with `user@machine timestamp`. Exists only while a create is running. If a workstation crashes mid-create the operating system closes the handle and the file disappears; on an SMB share this can take until the server drops the session. A stale lock that persists can be deleted by hand once no create is running anywhere.

## 8. Autodesk Vault

Used only when `CatalogVaultPath` is set. The catalog file is checked out to the local `CatalogPath` before the insert and checked in afterwards with the comment `Add electrical catalog component <MFG> <CAT> (<PN>)`. On failure after checkout the add-in undoes the checkout. Authentication is Windows. The footprint DWG and `footprint_lookup.mdb` are not Vault-controlled by this add-in.

## 9. Temporary Inventor drawing

`InventorGeometryService.Project` adds a drawing document from the configured or default template, places one base view, reads its curves, and closes it with `Close(true)` (skip save). It is never written to disk. The imported model document, however, stays open in Inventor after the wizard finishes; the engineer closes it.
