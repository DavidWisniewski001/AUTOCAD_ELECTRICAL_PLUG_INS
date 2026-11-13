# Data stores: LSE Catalog Tools

Maintained by DCW. Last reviewed 2026-11-13.

Catalog Tools reads and appends to one Access database (`default_cat.mdb`), keeps two small JSON files per user, writes a backup folder with a report for every catalog add, and optionally checks the database out of and into Autodesk Vault. It writes attribute values into the open drawing. It uses no registry keys, no lock file and no SQL Server.

## Summary

| Store | Location | Format | Read | Written |
|---|---|---|---|---|
| Catalog | `AppSettings.CatalogPath` | Access `.mdb` | Browser, admin form, diagnostics | `LSECATALOGADMIN` only (INSERT) |
| Settings | `%APPDATA%\LSE\AutoCADElectricalCatalogTools\settings.json` | JSON | Every command | Settings Save, Toggle favorite, Assign (Recent) |
| Company standards | `AppSettings.CompanyStandardsPath` (default beside `settings.json`) | JSON | Browser | Never by the plugin (edit by hand) |
| Backups and report | `<catalog folder>\LSE_Catalog_Backups\<yyyyMMdd_HHmmss>\` | MDB copy + text | By hand | Every add attempt |
| Vault | `AppSettings.CatalogVaultPath` on `VaultServer` / `VaultName` | Vault file | Check-out, test | Check-in |
| Drawing | Active document | DWG block attributes | Assign | Assign |

## 1. default_cat.mdb

AutoCAD Electrical's catalog. One table per component family; the table name is the family code (`TRMS`, `PB`, `CR`, `CB`, `DISC`, `FU`, `LT`, `MOT`, `PLCIO`, `PS`, `SS`, `VFD`, `MISC_CAT`, and others). Tables named `MSys*` or starting with `_` are ignored.

| Location | Path |
|---|---|
| Default (discovered when the setting is blank) | `%USERPROFILE%\Documents\Acade 2026\AeData\en-US\Catalogs\default_cat.mdb` |
| LSE shared copy (Vault working folder) | `C:\_Lake_Shore_E_Vault\CAD Standards\AutoCAD\ECAD Standards\E-CAD .mbd ACCESS FILES\AeData\en-US\Catalogs\default_cat.mdb` |

Columns the plugin uses:

| Column | Read for | Written by admin add |
|---|---|---|
| `CATALOG` | Grid, key, map to `CAT`/`CATALOG` | Yes |
| `MANUFACTURER` | Grid, key, map to `MFG`/`MANUFACTURER` | Yes |
| `DESCRIPTION` | Grid, map to `DESC`/`DESC1`, colour | Yes |
| `TYPE` | Grid, filter, map, colour | Yes |
| `RATING` | Grid, filter, map, colour | Yes |
| `USER1` | Grid "LSE Part Number", map to `USER1`/`LSE_PART_NUMBER`/LSE part attribute | Yes (required) |
| `USER2`, `USER3` | Search only | Yes |
| `TEXTVALUE` | `KEY=VALUE;...` attribute overrides | Yes |
| `WDBLKNAM` | Compatibility warning | Yes |
| `MISCELLANEOUS1`, `MISCELLANEOUS2` | Colour inference | No |
| any other | Search only | No |

Rules:

- Reads are `SELECT TOP 20000 * FROM [<table>]`; rows beyond 20,000 are invisible to the browser and to the duplicate check.
- The insert writes only the columns above that exist in the table, as single-quote-escaped strings (blanks become `''`).
- Record identity: `CatalogRecord.Key` = `TABLE|MANUFACTURER|CATALOG`, upper-case. Favorites, Recent and the standards file store this key. Renaming a manufacturer or catalog number in the MDB breaks the link.
- No UPDATE or DELETE is ever issued.
- Opened through ADODB with `Microsoft.ACE.OLEDB.16.0`, then `12.0`, then `Microsoft.Jet.OLEDB.4.0`.

What breaks if it is wrong: the browser shows an error and an empty family list; the admin add refuses.

## 2. settings.json

`%APPDATA%\LSE\AutoCADElectricalCatalogTools\settings.json`, one `AppSettings` object. Written through `settings.json.tmp` then a replace. If the file cannot be parsed, defaults are used silently and the next save overwrites it.

| Property | Type | Default | Meaning |
|---|---|---|---|
| `CatalogPath` | string | discovered path above, else `""` | Catalog MDB |
| `CompanyStandardsPath` | string | `<settings folder>\CompanyStandards.json` | Standards file |
| `VaultExplorerPath` | string | `C:\Program Files\Autodesk\Vault Client 2026\Explorer` | Vault client folder |
| `VaultServer` | string | `lse-cep-sql` | Vault server (scheme stripped at use) |
| `VaultName` | string | `Lake_Shore_E_Vault` | Vault name |
| `CatalogVaultPath` | string | `""` | Path in Vault; blank = Vault off |
| `LsePartAttribute` | string | `USER1` | Block attribute for the LSE part number |
| `Favorites` | string[] | `[]` | Record keys starred with Toggle favorite |
| `Recent` | string[] | `[]` | Record keys assigned, most recent first, at most 25 |

Example:

```json
{
  "CatalogPath": "C:\\_Lake_Shore_E_Vault\\CAD Standards\\AutoCAD\\ECAD Standards\\E-CAD .mbd ACCESS FILES\\AeData\\en-US\\Catalogs\\default_cat.mdb",
  "CompanyStandardsPath": "C:\\Users\\<user>\\AppData\\Roaming\\LSE\\AutoCADElectricalCatalogTools\\CompanyStandards.json",
  "VaultExplorerPath": "C:\\Program Files\\Autodesk\\Vault Client 2026\\Explorer",
  "VaultServer": "lse-cep-sql",
  "VaultName": "Lake_Shore_E_Vault",
  "CatalogVaultPath": "$/CAD Standards/AutoCAD/ECAD Standards/E-CAD .mbd ACCESS FILES/AeData/en-US/Catalogs/default_cat.mdb",
  "LsePartAttribute": "USER1",
  "Favorites": [ "PB|ALLEN-BRADLEY|800T-A1A" ],
  "Recent": []
}
```

## 3. CompanyStandards.json

A JSON array of `StandardEntry`. Missing or unreadable means an empty list (the Company Standards view is then empty). The plugin never writes it; **Manage standards** only shows its path.

| Field | Default | Meaning |
|---|---|---|
| `RecordKey` | `""` | `TABLE\|MANUFACTURER\|CATALOG`, matched case-insensitively |
| `Category` | `Uncategorized` | Not used by the UI yet |
| `Status` | `Company Standard` | `Legacy` hides the row from Company Standards and shows `Legacy` in the Status column; any other value shows `Standard` |
| `Notes` | `""` | Not used by the UI yet |

```json
[
  { "RecordKey": "PB|ALLEN-BRADLEY|800T-A1A", "Category": "Pushbuttons", "Status": "Company Standard", "Notes": "" },
  { "RecordKey": "LT|ALLEN-BRADLEY|800T-P16R", "Category": "Pilot lights", "Status": "Legacy", "Notes": "Use 800FP" }
]
```

## 4. Backups and TransactionReport.txt

| Item | Path |
|---|---|
| Backup root | `<folder of CatalogPath>\LSE_Catalog_Backups\` (falls back to `Documents` when the path has no folder) |
| One attempt | `...\<yyyyMMdd_HHmmss>\<catalog file name>` and `...\<yyyyMMdd_HHmmss>\TransactionReport.txt` |

- Created at the start of every add, before any check, so refused adds also leave a full copy.
- Two adds within the same second share a folder; the second overwrites the first's backup.
- Never pruned.
- The report is one line per step (`Started`, `Catalog`, `Backup`, `Record`, Vault lines, `Database insert/read-back: PASS`, `Status: SUCCESS`, or `ERROR`, `Local rollback`, `Vault undo checkout`). See [OPERATIONS.md](OPERATIONS.md#6-what-an-add-does-and-leaves-behind).

## 5. Vault

Used only when `CatalogVaultPath` is set.

| What | Detail |
|---|---|
| File | `FindLatestFilesByPaths(CatalogVaultPath)` |
| Check-out | Framework `AcquireFiles` (Checkout, plus Download into the catalog folder when the working copy differs), fallback `DocumentService.CheckoutFile` only when the copy is current |
| Check-in | Framework `CheckinFile` with comment `Add electrical catalog component <MFG> <CAT>` |
| Undo | Framework `UndoCheckoutFile`, fallback `DocumentService.UndoCheckoutFile` |
| Read-only flag | Cleared on the working copy after check-out |
| Client DLLs | `Autodesk.Connectivity.WebServices`, `Autodesk.DataManagement.Client.Framework`, `...Framework.Vault`, optional `...Framework.Vault.Forms`, taken from the process or the Vault Explorer folder |
| Add-in | `Connectivity.Application.VaultBase` (loaded by the AutoCAD Vault add-in) for the logged-in session |

`VaultProbe` treats any path containing `\_Lake_Shore_E_Vault\` (or under `C:\_Lake_Shore_E_Vault\`) as the Vault working folder for the read-only message and the check-in reminder.

## 6. The drawing

Assign writes block attribute values in the active drawing and may add an invisible attribute reference and attribute definition (tag = LSE part attribute, height 0.1, at the block origin). The drawing is not saved by the plugin.
