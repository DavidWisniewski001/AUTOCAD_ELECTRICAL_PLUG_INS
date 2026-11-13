# Architecture: LSE Catalog Tools (AutoCAD Electrical 2026 plugin)

Maintained by DCW. Last reviewed 2026-11-13.

Catalog Tools is a .NET 8 WinForms plugin loaded into AutoCAD Electrical 2026. It reads the AutoCAD Electrical parts catalog (`default_cat.mdb`) through late-bound ADODB, writes catalog values into the attributes of a component block in the open drawing, and adds new catalog rows through one backed-up transaction that optionally checks the catalog out of and back into Autodesk Vault. There is no `src\` folder: all source files sit flat in the plugin folder.

## 1. What problem it solves

`default_cat.mdb` holds tens of thousands of rows across one table per component family. The stock AutoCAD Electrical catalog lookup shows all of them. LSE wants engineers to pick from a short company-approved list first, see the LSE part number (stored in `USER1`) next to each part, and have every add to the shared catalog backed up, verified and kept in step with Vault.

## 2. Components

| File | Type(s) | Role |
|---|---|---|
| `Plugin.cs` | `Plugin`, `Commands` | Registers the five `LSE...` commands; catches exceptions at the command boundary |
| `CatalogBrowserForm.cs` | `CatalogBrowserForm` | Browser and assign dialog (`LSECATALOG`, `LSEASSIGNCOMPONENT`) |
| `CatalogAdminForm.cs` | `CatalogAdminForm` | Add-record dialog (`LSECATALOGADMIN`) |
| `SettingsForm.cs` | `SettingsForm` | Settings dialog with Test Vault (`LSECATALOGSETTINGS`) |
| `TextReportForm.cs` | `TextReportForm` | Read-only text window with Copy, used for the Vault test report |
| `DiagnosticsService.cs` | `DiagnosticsService` | Builds the `LSECATALOGDIAG` report |
| `AutoCadComponentService.cs` | `AutoCadComponentService` | Block pick, attribute read, change preview, attribute write, compatibility warning (AutoCAD API) |
| `AccessCatalogService.cs` | `AccessCatalogService` | All `default_cat.mdb` reads and the single INSERT (ADODB) |
| `CatalogTransactionService.cs` | `CatalogTransactionService` | The only write path to the catalog: backup, check, Vault, insert, verify, report, rollback |
| `VaultCatalogService.cs` | `VaultCatalogService`, `VaultCatalogService.VaultSession` | Vault connection, check-out with download, check-in, undo check-out, test report (reflection) |
| `VaultProbe.cs` | `VaultProbe` | Host runtime, Vault client and add-in detection; read-only refusal; check-in reminder. Identical copy in every LSE plugin |
| `SettingsService.cs` | `SettingsService`, `StandardsService`, `FamilyNames` | `settings.json`, `CompanyStandards.json`, family display names |
| `Models.cs` | `CatalogScope`, `CatalogRecord`, `StandardEntry`, `AppSettings`, `SelectedBlockInfo`, `AttributeChange`, `CatalogAddRequest` | Plain data |

## 3. Dependency diagram

```
 Commands (Plugin.cs)
   |-- LSECATALOG / LSEASSIGNCOMPONENT --> CatalogBrowserForm
   |                                         |-- AccessCatalogService ----> default_cat.mdb (read)
   |                                         |-- AutoCadComponentService -> active drawing (block attributes)
   |                                         |-- SettingsService --------> settings.json (Favorites, Recent)
   |                                         '-- StandardsService -------> CompanyStandards.json (read)
   |-- LSECATALOGADMIN -------------------> CatalogAdminForm
   |                                         '-- CatalogTransactionService
   |                                               |-- AccessCatalogService -> default_cat.mdb (read, INSERT)
   |                                               |-- VaultCatalogService --> Vault SDK (reflection) --> Vault server
   |                                               |-- VaultProbe
   |                                               '-- LSE_Catalog_Backups\<stamp>\ (MDB copy, TransactionReport.txt)
   |-- LSECATALOGSETTINGS ----------------> SettingsForm --> SettingsService, VaultCatalogService.TestReport --> TextReportForm
   '-- LSECATALOGDIAG --------------------> DiagnosticsService --> AccessCatalogService, VaultCatalogService.TestReport(connect:false)
```

## 4. End-to-end data flow

### 4.1 Browse

1. The browser loads settings and `CompanyStandards.json`, then lists the user tables of `default_cat.mdb` (system tables `MSys*` and tables starting `_` are skipped), sorted by friendly family name.
2. Choosing a family reads up to 20,000 rows of that table into memory (`SELECT TOP 20000 *`).
3. Every other filter (left tree scope, Manufacturer, Type, Rating, Search) runs in memory on those rows. Search matches any column value, case-insensitive.

### 4.2 Assign

1. A block reference is picked in the active drawing and all its attribute tags and values are read.
2. When a catalog row is selected, a preview is built: for every attribute on the block, the proposed value comes from the attribute map (section 6) unless the tag is preserved. If the block has no attribute named by the LSE part attribute setting and the row has a `USER1`, an extra row marked `(missing)` is added.
3. On **Assign component**, a compatibility warning is shown (block `FAMILY` not matching the table, or `WDBLKNAM` not in the block name) and can be overridden.
4. Under a document lock, one transaction writes `Proposed` into every non-preserved attribute, appends the `(missing)` attribute as an invisible attribute (adding an invisible attribute definition to the block definition when it has none), commits and regenerates.
5. The record key is put at the top of the Recent list (25 entries kept) in `settings.json`.

### 4.3 Add a catalog record

See section 5.

## 5. The mutating transaction (`CatalogTransactionService.Execute`)

```
 1. backup      copy default_cat.mdb to <catalog folder>\LSE_Catalog_Backups\<yyyyMMdd_HHmmss>\default_cat.mdb
 2. duplicate   refuse if TABLE has a row with the same MANUFACTURER and CATALOG (case-insensitive)
 3a. Vault on   VaultCatalogService.Checkout: connect, find file, refuse if checked out by someone else,
                download latest when size/date differ, check out, clear read-only        log "Vault checkout: PASS"
 3b. Vault off  VaultProbe.RequireWritable: refuse if the MDB is read-only
 4. insert      INSERT only the request columns that exist in the table
 5. read-back   the same duplicate query must now find the row                         log "Database insert/read-back: PASS"
 6a. Vault on   VaultCatalogService.Checkin with comment "Add electrical catalog component <MFG> <CAT>"
 6b. Vault off  append the check-in reminder when the MDB is in the Vault working folder
 7. report      "Status: SUCCESS", TransactionReport.txt in the backup folder, report returned to the dialog
 on any error   log "ERROR: <exception>", copy the backup over the MDB ("Local rollback: PASS|FAILED"),
                if step 3a succeeded UndoCheckout ("Vault undo checkout: PASS|FAILED"),
                write TransactionReport.txt, throw with the whole log as the message
 always         VaultCatalogService.EndSession releases any Vault session the plugin opened
```

Order matters and has two consequences that the docs record as known gaps ([DEVELOPER-GUIDE.md](DEVELOPER-GUIDE.md#8-known-gaps)):

- The backup and the duplicate check run on the working copy before step 3a can download a newer Vault version.
- A check-in failure in step 6a is treated like any other error: the local insert is rolled back and the check-out undone, even though the check-in error text says the working copy still holds the record.

### 5.1 Vault connection order (`VaultCatalogService.Connect`)

1. The Vault add-in's session inside AutoCAD (`Connectivity.Application.VaultBase.ConnectionManager.Instance.Connection`). Not owned: never logged out by the plugin.
2. The Vault login dialog from `Autodesk.DataManagement.Client.Framework.Vault.Forms` (`Library.Login(LoginSettings)`), pre-filled with server and vault name. Cancel throws `OperationCanceledException`. Owned.
3. Windows authentication without a dialog (`WinAuthCredentials(ServerIdentities, vault, null, LicensingAgent.Client)` then `WebServiceManager`, then a Framework `Connection`). Owned.

Before loading any SDK DLL from disk the Vault client's target framework is read from its metadata. A .NET Framework client, or one built for a newer .NET than the host, is refused with an explanation instead of failing on load.

### 5.2 Check-out detail (`VaultCatalogService.Checkout`)

| Situation | What happens |
|---|---|
| Checked out by another user | Refused, names the user and machine, nothing changed |
| Already checked out to me | That check-out is used; if the working copy differs, the latest version is downloaded over it |
| Not checked out, working copy current | `FileManager.AcquireFiles` with Checkout; if the Framework call fails, `DocumentService.CheckoutFile` is used instead |
| Not checked out, working copy stale or missing | `AcquireFiles` with Checkout + Download into the catalog folder; if that fails the add is refused (no fallback, so a stale copy can never be checked out) |
| After check-out | The file is re-read from Vault and must show a check-out user; the read-only attribute is cleared |

"Current" means the working copy's size equals the Vault file size and its modified time is within 3 seconds of the Vault `ModDate` (local or UTC).

Check-in uses the `FileManager.CheckinFile` overload whose last parameter is a `FilePathAbsolute`, classification `None`, keep-checked-out false. Undo uses `FileManager.UndoCheckoutFile`, then `DocumentService.UndoCheckoutFile` as the fallback. Overloads are found by name and parameter types at run time so minor API changes between Vault releases do not break the call.

## 6. Attribute map on assign

| Block attribute | Catalog source |
|---|---|
| `MFG`, `MANUFACTURER` | `MANUFACTURER` |
| `CAT`, `CATALOG` | `CATALOG` |
| `DESC`, `DESC1` | `DESCRIPTION` |
| `RATING` | `RATING` |
| `TYPE` | `TYPE` |
| `USER1`, `LSE_PART_NUMBER`, the LSE part attribute setting | `USER1` |
| `COLOR` | First colour word found in DESCRIPTION, TYPE, RATING, CATALOG, MISCELLANEOUS1, MISCELLANEOUS2: GREEN G, RED R, YELLOW Y, BLUE B, WHITE W, CLEAR C, AMBER A, ORANGE O, BLACK K |
| any `KEY` | `TEXTVALUE` parsed as `KEY=VALUE;KEY=VALUE` (overrides the rows above) |

Attributes on the block that are not in the map keep their value. Preserved by default: `TAG`, `TAG1`, `TAG2`, `LOC`, `INST`, `FAMILY`, `XREF`, `WDTAGALT`, any tag starting with `X`, containing `WIRE` or containing `PIN`.

## 7. Process, threading and COM model

- Every command is `CommandFlags.Modal` and shows its dialog with `Application.ShowModalDialog`, on AutoCAD's main thread. No background threads.
- ADODB objects are `dynamic` COM objects opened per call and released with `Marshal.FinalReleaseComObject` in `finally`. Provider order: `Microsoft.ACE.OLEDB.16.0`, `Microsoft.ACE.OLEDB.12.0`, `Microsoft.Jet.OLEDB.4.0`. The 64-bit Access Database Engine must be installed.
- Vault types are never referenced at compile time. Calls go through `dynamic` and `MethodInfo.Invoke` (out parameters only through `Invoke`).
- A Vault session opened by the plugin is cached in a static field between check-out and check-in of one transaction, then released.

## 8. Design decisions

| Decision | Reason |
|---|---|
| Add only, never update or delete catalog rows | The catalog is shared; an add is easy to verify and to roll back by restoring one file |
| Whole-file backup before every add | `default_cat.mdb` is one Access file; restoring the copy is the simplest complete rollback |
| Record identity is `TABLE\|MANUFACTURER\|CATALOG` upper-cased | The catalog has no stable LSE key; favourites, recents and standards must survive re-imports |
| Company standards in a separate JSON file | Curating the list must not require writing to the shared catalog |
| ADODB late-bound, no OleDb or interop reference | No NuGet packages and no compile-time Office dependency |
| Vault by reflection, three connection routes | Builds on machines without Vault; uses the add-in login when there is one, so the engineer is not asked twice |
| Download before check-out when the working copy differs | Prevents checking a stale catalog in over someone else's newer version |

## 9. Relationship to the sibling projects

- `..\acade-librarian\` (`LSECATALOGADD`) also adds rows to `default_cat.mdb`, issues the LSE part number from the parts catalog workbook, and refuses when the catalog is Vault-locked rather than checking it out. Catalog Tools does not issue part numbers; the administrator types `USER1`.
- `..\inventor-addin\` carries a copy of `VaultCatalogService.cs` that differs only in namespace and `AppName` (`LSE Footprint Tools`). `VaultProbe.cs` is identical in every plugin apart from the namespace. Keep the copies in step.
