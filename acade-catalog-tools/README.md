# LSE Catalog Tools (AutoCAD Electrical 2026 plugin)

Maintained by DCW. Last reviewed 2026-11-13.

Status: 2026-11-12-r7. Compiles Release with 0 warnings since r6. The Vault code was rewritten for the Vault 2025/2026 API shapes in r7 and is verified against a stub SDK only, not against a live Vault. The plugin has never been run in AutoCAD.

Catalog Tools gives LSE engineers a company-curated view of the AutoCAD Electrical catalog (`default_cat.mdb`). They browse it by family, manufacturer, type, rating or free text, assign a catalog part to a component block in the open drawing, and (catalog administrators) add new catalog records under an LSE part number. The plugin only adds rows; it never updates or deletes catalog rows.

## Who uses it

| Role | Commands |
|---|---|
| Electrical designer | `LSECATALOG`, `LSEASSIGNCOMPONENT` |
| Catalog administrator | `LSECATALOGADMIN` (adds rows to the shared catalog, with Vault check-out and check-in) |
| Anyone setting up a workstation | `LSECATALOGSETTINGS`, `LSECATALOGDIAG` |

## Commands

| Command | Purpose |
|---|---|
| `LSECATALOG` | Catalog browser. No block is selected; **Assign component** asks for one (see the known gap in [docs/OPERATIONS.md](docs/OPERATIONS.md#4-assign-a-part-to-a-block)) |
| `LSEASSIGNCOMPONENT` | Pick a component block first, then the browser opens with a preview of every attribute change. Preserved attributes (`TAG`, `TAG1`, `TAG2`, `LOC`, `INST`, `FAMILY`, `XREF`, `WDTAGALT`, any tag starting with `X`, containing `WIRE` or containing `PIN`) are ticked **Preserve** and are never written while ticked |
| `LSECATALOGADMIN` | Add a record to `default_cat.mdb` (Table, Manufacturer, Catalog number and USER1, the LSE part number, are required) |
| `LSECATALOGSETTINGS` | Settings, including the **Test Vault** button |
| `LSECATALOGDIAG` | Diagnostics report, including the Vault section (does not connect to Vault) |

**Preserve** in the assign preview: a ticked row is skipped when the block is written. Unticking it does not change the value by itself; type the new value in **Proposed (editable)** after unticking. Ticking it again puts the current value back in Proposed.

## Install

| Way | How | Lasts |
|---|---|---|
| Bundle | Copy `bundle\LSE.CatalogTools.bundle` to `%APPDATA%\Autodesk\ApplicationPlugins\` (per user) or `C:\ProgramData\Autodesk\ApplicationPlugins\` (all users), put the Release build output in its `Contents\` folder, restart AutoCAD. The DLL loads the first time one of the five commands is typed | Every session |
| NETLOAD | `NETLOAD` then browse to `bin\Release\LSE.AutoCADElectrical.CatalogTools.dll` (or the copy in the NETLOAD-READY release zip, which `LSE-NETLOAD-ALL.lsp` loads together with the other AutoCAD plugins) | This session only |

Then run `LSECATALOGSETTINGS`, then `LSECATALOGDIAG`. Detail in [docs/OPERATIONS.md](docs/OPERATIONS.md).

## Settings

`%APPDATA%\LSE\AutoCADElectricalCatalogTools\settings.json`; company standards in `CompanyStandards.json` beside it (path settable).

| Setting | Default | Meaning |
|---|---|---|
| Catalog MDB | `Documents\Acade 2026\AeData\en-US\Catalogs\default_cat.mdb` when it exists, else blank | Local path of `default_cat.mdb` (the Vault working copy when Vault is used) |
| Company Standards JSON | `CompanyStandards.json` next to `settings.json` | The curated list behind the **Company Standards** view |
| LSE part attribute | `USER1` | Block attribute that receives the LSE part number on assign |
| Vault Explorer folder | `C:\Program Files\Autodesk\Vault Client 2026\Explorer` | Folder holding the Vault client DLLs. Auto-discovered when blank or missing |
| Vault server / Vault name | `lse-cep-sql` / `Lake_Shore_E_Vault` | Bare server name (a leading `http://` or `https://` is stripped) and vault name |
| Catalog Vault path | blank | `$/folder/.../default_cat.mdb`. Blank = Vault off |

Full field reference in [docs/OPERATIONS.md](docs/OPERATIONS.md#2-settings-lsecatalogsettings) and the JSON schema in [docs/DATA-STORES.md](docs/DATA-STORES.md).

## Add transaction

Backup to `<catalog folder>\LSE_Catalog_Backups\<yyyyMMdd_HHmmss>\`, duplicate check, Vault check-out (when a Vault path is set) or read-only refusal (when it is not), insert, read-back, Vault check-in, `TransactionReport.txt` in the backup folder. On any failure the backup is copied back over the catalog, the Vault check-out is undone and the report holds the detail. The Vault login dialog can appear at check-out, before anything is written; cancelling it aborts the add. Every attempt, including a refused one, copies the whole MDB into a new backup folder; nothing prunes them.

## Vault

The Vault SDK is loaded at run time (no compile-time reference): from the running process when the Vault add-in already has it loaded, otherwise from the Vault Explorer folder.

- Connection, in order: the logged-in session of the Vault add-in inside AutoCAD; otherwise the standard Vault login dialog (Vault, Windows or Autodesk ID accounts); otherwise Windows authentication with server and vault name from settings. Sessions the plugin opened itself are released after check-in, undo or the end of the add.
- Check-out guarantees: the latest Vault version is downloaded over the working copy when size or date differ, so a stale copy is never checked in over a newer catalog; the add is refused when another user holds the check-out; the read-only flag is cleared.
- Vault off: when the catalog file is read-only (typical of a Vault working folder, file not checked out) the add is refused before the database is touched, with the Vault check-out instruction. After a successful add with Vault off, the report reminds you to check the file in when it sits in the Vault working folder (`C:\_Lake_Shore_E_Vault\`).
- .NET 10: from August 2026 AutoCAD 2026.1.2, Inventor 2026.4 and Vault 2026.4 run on .NET 10. This plugin (net8.0-windows) still loads in a .NET 10 AutoCAD, but Vault client DLLs built for .NET 10 cannot be loaded into an AutoCAD still on .NET 8. Update AutoCAD and Vault together. `LSECATALOGDIAG` and the Test Vault button show the host runtime, the installed Vault client and whether they match.
- Test Vault (Settings) tests the values currently in the boxes (not the saved file) with a full connection; it may show the Vault login dialog. `LSECATALOGDIAG` reports without connecting.

## Regression test

`tests\VaultFakeTest` compiles `VaultCatalogService.cs` and `VaultProbe.cs` against a stub Vault SDK that carries the real type names (27 checks). Build it, then run it (`dotnet run` in that folder, or `dotnet bin\Release\VaultFakeTest.dll`); the last line must be `ALL PASS`. It is excluded from the plugin build (`<Compile Remove="tests\**" />`). It proves the call shapes against the stub only, not against a real Vault. There is no test for the catalog, the browser or the AutoCAD code.

## Build

`dotnet build -c Release` in this folder (add `-p:AcadDir="<AutoCAD 2026 folder>"` if AutoCAD is installed somewhere other than `C:\Program Files\Autodesk\AutoCAD 2026`; without an install the AutoCAD.NET 25.1.0 package is used for compile only). Output: `bin\Release\LSE.AutoCADElectrical.CatalogTools.dll`.

## Versioning

There is no build tag in the code. The release name (for example `2026-11-12-r7`) is on the release zip only; `PackageContents.xml` says `AppVersion="1.0.0"`, the assembly carries the SDK default `1.0.0.0`, and `LSECATALOGDIAG` prints `LSE Catalog Tools V2` on its first line. Identify a deployed build by the zip it came from or the DLL's file date.

## Unverified

All AutoCAD API behaviour (block pick from inside the dialog, attribute writes, appending a hidden attribute to a block definition) and all Vault API behaviour (login dialog, check-out/check-in calls, runtime detection) are unverified on a live system. See the Known Gaps in [docs/DEVELOPER-GUIDE.md](docs/DEVELOPER-GUIDE.md#8-known-gaps).

## Documentation

| Document | Answers |
|---|---|
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | What the parts are, how they depend on each other, the add transaction and Vault sequence, design decisions |
| [docs/CODE-REFERENCE.md](docs/CODE-REFERENCE.md) | Every file, type and public member |
| [docs/DATA-STORES.md](docs/DATA-STORES.md) | `default_cat.mdb`, `settings.json`, `CompanyStandards.json`, backups, the transaction report, Vault |
| [docs/OPERATIONS.md](docs/OPERATIONS.md) | Install, every dialog field and button, what a run leaves behind, error messages, recovery |
| [docs/DEVELOPER-GUIDE.md](docs/DEVELOPER-GUIDE.md) | Build, test, deploy, conventions, recipes, first-build checklist, Known Gaps |
| [docs/GLOSSARY.md](docs/GLOSSARY.md) | Terms, acronyms, products and file types |
