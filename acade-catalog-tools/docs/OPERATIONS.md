# Operations guide: LSE Catalog Tools

Maintained by DCW. Last reviewed 2026-11-13.

How to install and configure Catalog Tools, how to use each dialog field by field, what a catalog add leaves behind, what every error means and how to recover by hand. Where each file lives is in [DATA-STORES.md](DATA-STORES.md).

## 1. Workstation requirements and install

| Need | Why |
|---|---|
| AutoCAD Electrical 2026 (R25.1) | Host. The bundle manifest allows `R25.1` only |
| 64-bit Microsoft Access Database Engine (ACE 16 or 12) | Every catalog read and write. Without it `LSECATALOGDIAG` shows `Catalog read: FAIL` listing each provider |
| Vault Client 2026 with the Vault add-in for AutoCAD (optional) | Only when **Catalog Vault path** is filled in |
| Write access to the catalog folder | The add writes the MDB and a backup folder beside it |

Install one of two ways:

1. **Bundle (every session).** Copy `bundle\LSE.CatalogTools.bundle` to `%APPDATA%\Autodesk\ApplicationPlugins\` or `C:\ProgramData\Autodesk\ApplicationPlugins\`. Put `LSE.AutoCADElectrical.CatalogTools.dll` (and optionally its `.pdb`, `.deps.json`, `.runtimeconfig.json`) in `Contents\`. Restart AutoCAD. The DLL loads the first time any of the five commands is typed (`LoadOnCommandInvocation="True"`, `LoadOnAutoCADStartup="False"`). The NETLOAD-READY release zip carries the bundle with `Contents\` already filled.
2. **NETLOAD (this session).** Type `NETLOAD`, pick the DLL, **Load**. Unblock a downloaded zip first (`Unblock-File`), or AutoCAD refuses the DLL. `LSE-NETLOAD-ALL.lsp` from the NETLOAD-READY zip loads all AutoCAD LSE plugins at once and defines `LSELOAD`.

First run: `LSECATALOGSETTINGS`, check the paths, **Test Vault** if Vault is used, **Save**; then `LSECATALOGDIAG`. Try `LSECATALOGADMIN` on a copy of the catalog before the live one.

## 2. Settings (`LSECATALOGSETTINGS`)

Window title: **LSE Catalog Tools Settings**. Each value is a text box; three have a **Browse** button.

| Field | Saved as | Default | Notes |
|---|---|---|---|
| Catalog MDB | `CatalogPath` | `Documents\Acade 2026\AeData\en-US\Catalogs\default_cat.mdb` if it exists, else blank | Browse opens a `*.mdb` picker. The shared LSE catalog is `C:\_Lake_Shore_E_Vault\CAD Standards\AutoCAD\ECAD Standards\E-CAD .mbd ACCESS FILES\AeData\en-US\Catalogs\default_cat.mdb` |
| Company Standards JSON | `CompanyStandardsPath` | `%APPDATA%\LSE\AutoCADElectricalCatalogTools\CompanyStandards.json` | Browse opens a `*.json` picker. Point several users at one shared file to share the list |
| LSE part attribute | `LsePartAttribute` | `USER1` | Block attribute that receives the catalog `USER1` value on assign; created hidden when missing |
| Vault Explorer folder (auto-discovered when blank or wrong) | `VaultExplorerPath` | `C:\Program Files\Autodesk\Vault Client 2026\Explorer` | Browse opens a folder picker. When the folder does not exist, the newest `Program Files\Autodesk\Vault*\Explorer` holding `Autodesk.Connectivity.WebServices.dll` is used; the saved value is not changed |
| Vault server | `VaultServer` | `lse-cep-sql` | `http://`, `https://` and a trailing `/` are stripped before use. Pre-fills the login dialog; required for Windows authentication |
| Vault name | `VaultName` | `Lake_Shore_E_Vault` | As above |
| Catalog Vault path ($/folder/.../default_cat.mdb; blank = no Vault) | `CatalogVaultPath` | blank | The file's path inside Vault. Backslashes become `/` and a missing `$/` is added. Blank turns every Vault step off |

Buttons:

| Button | Does |
|---|---|
| **Test Vault** | Builds settings from the boxes as they are now (not the saved file), connects to Vault exactly as an add would (may show the login dialog), finds the catalog, reports its version and check-out state and whether the working copy matches. Shown in a **Vault test** window with **Copy**. Changes nothing. With the Vault path blank it only reports the runtime, client and working-copy lines |
| **Save** | Writes all seven values to `settings.json` and closes |

There is no Cancel button; close the window to discard changes. Favorites and Recent are kept as loaded.

## 3. Browse the catalog (`LSECATALOG`)

Window title: **LSE Electrical Components** (or **Assign Component - <block name>** from `LSEASSIGNCOMPONENT`).

### 3.1 Layout

| Area | Control | Does |
|---|---|---|
| Top row | **Family** | Lists the catalog tables by friendly name (`TRMS` Terminal Blocks, `PB` Pushbuttons, `CR` Control Relays, `CB` Circuit Breakers, `DISC` Disconnects, `FU` Fuses, `LT` Pilot Lights, `MOT` Motors, `PLCIO` PLC I/O, `PS` Power Supplies, `SS` Selector Switches, `VFD` Variable Frequency Drives, `MISC_CAT` Miscellaneous; any other table shows its code). Changing it reloads rows from the MDB (up to 20,000) |
| Top row | **Manufacturer**, **Type**, **Rating** | Distinct non-blank values of the loaded family, plus `(All)`. Exact, case-insensitive match. The previous choice is kept when it still exists |
| Top row | **Search** | Free text matched against every column of the row, case-insensitive, as you type |
| Left | Scope tree | **Company Standards** (default on open): rows listed in `CompanyStandards.json` whose Status is not `Legacy`. **My Favorites**: rows you starred. **Recently Used**: the last 25 rows you assigned. **Full Catalog**: everything |
| Upper grid | Catalog rows | Columns Status, Manufacturer, Catalog, LSE Part Number (`USER1`), Description, Type, Rating. Click a header to sort. Double-click a row = **Assign component** |
| Lower grid | Change preview | Attribute, Current, **Proposed (editable)** (yellow), **Preserve** (check box). Empty unless a block is selected |
| Footer | Status line | `<shown> shown of <loaded> \| <catalog path>`, or the last assign result |
| Footer | **Toggle favorite** | Adds or removes the selected row from My Favorites (saved at once) |
| Footer | **Select block** / **Change block** | Hides the browser, asks you to pick a block, then opens a new browser for that block |
| Footer | **Assign component** | See section 4 |
| Footer | **Refresh** | Re-reads the selected family from the MDB |
| Footer | **Close** | Closes |

Status column values: `Standard` (in the standards file, not Legacy), `Legacy` (in the file with Status `Legacy`), `Favorite`, `Catalog` (none of these).

If the grid is empty on open, the Company Standards list is empty or holds nothing from the first family: click **Full Catalog**.

## 4. Assign a part to a block

Fastest route: `LSEASSIGNCOMPONENT`, pick the component block in the drawing (only block references are accepted), and the browser opens with the matching family selected (first table equal to or containing the block's `FAMILY`).

1. Select a catalog row. The preview lists every attribute on the block.
2. Read **Proposed (editable)**. You may type over any value.
3. **Preserve** is pre-ticked for `TAG`, `TAG1`, `TAG2`, `LOC`, `INST`, `FAMILY`, `XREF`, `WDTAGALT` and any tag starting with `X` or containing `WIRE` or `PIN`. While ticked, the row is not written, whatever Proposed says.
4. **Assign component** (or double-click the row). A compatibility warning appears when the block `FAMILY` does not match the table, or the row's `WDBLKNAM` is not part of the block name; **Yes** applies anyway.
5. The status line shows `Assigned <MFG> <CAT>. USER1/LSE part number: <USER1>`, the drawing regenerates and the row goes to Recently Used.

### 4.1 How the Preserve check box works

| You do | Effect |
|---|---|
| Leave a preserved row ticked | Attribute untouched |
| Untick a preserved row | Nothing changes yet: Proposed still shows the current value, because the preview never mapped a preserved tag. Type the new value in Proposed, then Assign |
| Tick a row that was not preserved | Proposed is reset to Current and the row is skipped on Assign |
| Select another catalog row, or Assign | The preview is rebuilt: your ticks and typed values are lost |

The extra row whose Current is `(missing)` is the LSE part attribute when the block does not have it. On Assign, an invisible attribute with that tag is added to this block reference, and an invisible attribute definition (at the block origin, height 0.1) is added to the block definition if it has none. That changes the block definition in this drawing, so later inserts of the same block also carry the tag. Tick Preserve on that row to avoid it.

### 4.2 Assign from `LSECATALOG` (no block yet)

When the browser was opened by `LSECATALOG`, the preview is empty. **Assign component** asks you to pick a block, but then writes the empty preview, so nothing is written to the block although the status line says `Assigned ...` and Recent is updated. Use **Select block** first (which opens a browser bound to that block), or `LSEASSIGNCOMPONENT`. This is recorded as a bug in [DEVELOPER-GUIDE.md](DEVELOPER-GUIDE.md#8-known-gaps).

## 5. Add a catalog record (`LSECATALOGADMIN`)

Window title: **LSE Electrical Catalog Administrator**.

| Field | Column written | Required |
|---|---|---|
| Catalog family/table | which table | Yes (drop-down of the MDB's tables, by code) |
| Manufacturer | `MANUFACTURER` | Yes |
| Catalog number | `CATALOG` | Yes |
| Description | `DESCRIPTION` | No |
| Type | `TYPE` | No |
| Rating | `RATING` | No |
| LSE Part Number (USER1) | `USER1` | Yes. Not checked for format or uniqueness; issue numbers in the Librarian (`LSECATALOGADD`) or the Inventor add-in |
| USER2, USER3 | `USER2`, `USER3` | No |
| TEXTVALUE | `TEXTVALUE` | No. `KEY=VALUE;KEY=VALUE` pairs become attribute values on assign |
| WDBLKNAM | `WDBLKNAM` | No. Block-name filter used by the compatibility warning |

All values are trimmed. A field whose column does not exist in the chosen table is silently left out. Blank fields are written as empty strings, not NULL.

| Button | Does |
|---|---|
| **Manage standards** | Shows where `CompanyStandards.json` is and says to add record keys to it by hand. No editor exists yet |
| **Add to catalog** | Runs the transaction (section 6). The report appears in a message box and in the label at the bottom left |

## 6. What an add does and leaves behind

Sequence (detail in [ARCHITECTURE.md](ARCHITECTURE.md#5-the-mutating-transaction-catalogtransactionserviceexecute)):

1. Copy the whole MDB to `<catalog folder>\LSE_Catalog_Backups\<yyyyMMdd_HHmmss>\default_cat.mdb`. The LSE catalog is about 615 MB, so each attempt costs that much disk, including refused ones.
2. Refuse if the family already has that Manufacturer + Catalog number.
3. Vault path set: check out (login dialog possible). Blank: refuse if the MDB is read-only.
4. Insert and read back.
5. Vault path set: check in with comment `Add electrical catalog component <MFG> <CAT>`.
6. Write `TransactionReport.txt` into the backup folder.

A successful report looks like this:

```
Started: 2026-11-13T09:12:04
Catalog: C:\_Lake_Shore_E_Vault\...\Catalogs\default_cat.mdb
Backup: C:\_Lake_Shore_E_Vault\...\Catalogs\LSE_Catalog_Backups\20261113_091204\default_cat.mdb
Record: PB | ALLEN-BRADLEY | 800T-A1A | USER1=...
Vault connection: using the Vault add-in's session (<user> in vault Lake_Shore_E_Vault on lse-cep-sql)
Vault file: $/.../default_cat.mdb version 41 (...)
Vault check-out: PASS (working copy already current)
Vault checkout: PASS
Database insert/read-back: PASS
Vault check-in: PASS (now version 42)
Vault checkin: PASS
Status: SUCCESS
```

A failed one ends with `ERROR: <exception and stack>`, `Local rollback: PASS` (or `FAILED <reason>`), and when a check-out was made `Vault undo checkout: PASS` (or `FAILED`). The failure message box (**Catalog transaction failed**) shows the same lines.

## 7. Error messages

| Message (start) | Meaning | Do |
|---|---|---|
| `Catalog database was not found.` | Catalog MDB path wrong or blank | Fix it in Settings |
| `ADODB unavailable.` | ADODB COM not registered | Repair Windows / install the Access Database Engine |
| `Microsoft.ACE.OLEDB.16.0: ...` (three lines) | No provider could open the MDB | Install the 64-bit Access Database Engine; check the file is not locked exclusively |
| `Invalid table name.` | Table name outside `A-Z a-z 0-9 _` | Pick a table from the list |
| `Table, Manufacturer, Catalog number, and LSE Part Number (USER1) are required.` | Admin form validation | Fill them in |
| `That manufacturer/catalog number already exists in the selected family.` | Duplicate | Nothing to add; a backup folder was still created |
| `This file is read-only: ... check them out in Vault first ...` | Vault path blank and the MDB is read-only | Check it out in Vault (or fill in the Catalog Vault path), then retry. The log will also show `Local rollback: FAILED Access ... denied` because the restore cannot write a read-only file; that is harmless, nothing was written |
| `Read-back verification failed after insert.` | Insert ran but the row cannot be found | Rolled back; investigate the table (more than 20,000 rows hides later rows from the check) |
| `The catalog is checked out in Vault by <user> (machine <PC>)...` | Someone else holds it | Wait for their check-in |
| `Vault login was cancelled. Nothing was changed.` | You cancelled the login dialog | Retry and log in, or log in on the AutoCAD Vault tab first |
| `The Vault login dialog failed: ...` | The Vault Forms login threw | See the inner message; log in on the Vault tab instead |
| `Vault server and vault name are required (Settings).` | Windows-auth route with blanks | Fill both in |
| `Windows authentication to Vault failed: ...` / `... could not be created` | Windows-auth route failed | Log in on the Vault tab (the add-in session is tried first) |
| `Vault SDK assembly <name>.dll was not found...` | No Vault client found | Install Vault Client 2026 or fix the Vault Explorer folder; or blank the Vault path |
| `The Vault client in ... is the .NET Framework build ...` / `... is built for .NET 10 but ... is running .NET 8 ...` | Client and host runtimes differ | Install the matching AutoCAD 2026.1.2 and Vault 2026.4 updates together |
| `The catalog was not found in Vault at <path>...` | Catalog Vault path wrong | Copy the path from the file's Properties in Vault Explorer |
| `The working copy is older than the Vault version and the download failed ...` / `Vault check-out through the Vault Framework failed (...) and the working copy is older ...` | Could not refresh a stale copy | In Vault Explorer, Get Latest Version (or Check Out), then retry |
| `Vault returned without an error but the catalog is still not checked out.` | Check-out silently did nothing | Check it out in Vault Explorer and retry |
| `Vault check-in failed: ... check it in from Vault Explorer.` | Check-in failed | The transaction then restores the backup and undoes the check-out, so the record is NOT in the catalog despite the wording. Read the report, re-add after fixing Vault |
| `Vault undo check-out failed: ...` | Rollback could not release the check-out | Undo Check Out in Vault Explorer by hand. Do not check the file in: the working copy may be the pre-download backup |
| `No active drawing.` | Assign with no drawing open | Open a drawing |
| `Selected block FAMILY '<f>' does not match catalog family ...` / `Catalog filter WDBLKNAM '<w>' does not match block ...` | Compatibility warning | **Yes** to apply anyway, **No** to stop |

## 8. Diagnostics (`LSECATALOGDIAG`)

A message box titled **Diagnostics** with:

| Line | Shows |
|---|---|
| `LSE Catalog Tools V2` | Fixed label (not a build number) |
| .NET description | Runtime AutoCAD is running |
| `Assembly:` | Where the DLL was loaded from |
| `Catalog:`, `Catalog exists:` | Path and whether the file is there |
| `Standards:`, `Vault path:` | The two settings |
| `Tables: <n>` and `Catalog read: PASS` | Or `Catalog read: FAIL <reason>` |
| `Host: ...`, `Vault client: ...`, `Vault add-in: ...` | Runtime, installed Vault client and whether it can load here, add-in login state |
| `Vault server: ... vault: ...`, `Catalog Vault path: ...`, `Working copy: ... (writable / read-only / MISSING)` | Without connecting |

## 9. Recovery by hand

| Situation | Steps |
|---|---|
| Undo a successful add, Vault off | Close AutoCAD Electrical catalog lookups, copy `LSE_Catalog_Backups\<stamp>\default_cat.mdb` over the catalog |
| Undo a successful add, Vault on | Check the catalog out in Vault Explorer (Get Latest), copy the backup over it, check it in with a comment |
| Rollback reported `FAILED` | Copy the backup file from the folder named in the report over the catalog by hand |
| Catalog left checked out to you | Vault Explorer: Undo Check Out. Only check in if you have confirmed the working copy is the version you want |
| Disk filling with backups | Delete old `LSE_Catalog_Backups\<stamp>\` folders by hand; the plugin never prunes |
| Favorites or Recent lost | `settings.json` was unreadable and was replaced by defaults on the next save; re-star the rows |
