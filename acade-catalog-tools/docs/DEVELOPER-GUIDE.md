# Developer guide: LSE Catalog Tools

Maintained by DCW. Last reviewed 2026-11-13.

How to build, test, deploy and change Catalog Tools, the conventions the code follows, a first-build checklist for the workstation, and the Known Gaps. Read [ARCHITECTURE.md](ARCHITECTURE.md) first.

## 1. State of the code

| Item | State on 2026-11-13 |
|---|---|
| Compile | Release, 0 warnings, first in release r6 (2026-11-12) against the AutoCAD.NET 25.1.0 reference package |
| Vault code | Rewritten in r7 for Vault 2025/2026; 27-check stub test |
| Loaded in AutoCAD | Never |
| Run against live Vault | Never |
| Tests for catalog, browser, AutoCAD code | None |

## 2. Repository layout

```
acade-catalog-tools\
  LSE.AutoCADElectrical.CatalogTools.csproj
  Plugin.cs Models.cs SettingsService.cs SettingsForm.cs TextReportForm.cs DiagnosticsService.cs
  AccessCatalogService.cs CatalogTransactionService.cs VaultCatalogService.cs VaultProbe.cs
  AutoCadComponentService.cs CatalogBrowserForm.cs CatalogAdminForm.cs
  bundle\LSE.CatalogTools.bundle\PackageContents.xml
  tests\VaultFakeTest\ (Program.cs, VaultFakeTest.csproj, FakeSdk\Ws, Fw, Vdf, VaultBase)
  README.md  docs\
```

There is no `config\` folder: the plugin ships no JSON beside the DLL.

## 3. Build

Prerequisites: .NET 8 SDK (x64). AutoCAD Electrical 2026 is optional at build time.

```
cd acade-catalog-tools
dotnet build -c Release
dotnet build -c Release -p:AcadDir="D:\Autodesk\AutoCAD 2026"
```

| Machine | References used |
|---|---|
| AutoCAD at `$(AcadDir)` (default `C:\Program Files\Autodesk\AutoCAD 2026`) | `accoremgd.dll`, `acdbmgd.dll`, `acmgd.dll` from the install, not copied |
| No AutoCAD | `AutoCAD.NET` 25.1.0 NuGet package, compile only (needs internet once) |

Output: `bin\Release\LSE.AutoCADElectrical.CatalogTools.dll` (plus `.pdb`, `.deps.json`, `.runtimeconfig.json`).

## 4. Test

```
cd acade-catalog-tools\tests\VaultFakeTest
dotnet build -c Release
dotnet run -c Release
```

The stub DLLs must sit next to the test DLL (the project references copy them there); the test passes `AppContext.BaseDirectory` as the Vault Explorer folder unless a folder is given as the first argument. The last line must be `ALL PASS`; the exit code is the number of failures. It writes scratch files under `%TEMP%\lse-vault-faketest\`.

The stub proves argument shapes, overload selection and the order of calls. It does not prove that the real Vault SDK accepts them.

## 5. Deploy and debug

- Bundle or NETLOAD as in [OPERATIONS.md](OPERATIONS.md#1-workstation-requirements-and-install). A NETLOADed DLL cannot be unloaded; restart AutoCAD to load a new build.
- Debug: attach Visual Studio to `acad.exe` (managed .NET Core), load the DLL with NETLOAD, set breakpoints in the form handlers.
- Use a copy of `default_cat.mdb` (point **Catalog MDB** at it and leave **Catalog Vault path** blank) for every first test.

## 6. Conventions in use

From `..\STYLE-GUIDE.docx` and `..\agents\lse-csharp-dev.md`:

- File-scoped namespace, `public sealed` or `public static` classes, nullable on, `OrdinalIgnoreCase` everywhere.
- Two densities: compact (one-line members) in models, services and the small forms; expanded only in `AutoCadComponentService.cs` and `CatalogBrowserForm.cs`. Vault code is compact.
- ADODB as `dynamic`, released in `finally`; provider order ACE 16, ACE 12, Jet 4; table names checked with `^[A-Za-z0-9_]+$`; values escaped `'` to `''`; `SELECT TOP` on reads.
- Vault only through reflection; overloads found by name and parameter types.
- Exceptions caught only at command and button boundaries. Messages say what to do.
- One write path to the catalog: `CatalogTransactionService.Execute`. Never update or delete rows.
- Settings: one `AppSettings` POCO; add a property, a row in `SettingsForm`, read it from `s.` Keep JSON property names stable.

## 7. Recipes

### Add a setting

1. Add the property with a non-null default to `AppSettings` in `Models.cs`.
2. Add a row in `SettingsForm` (`Add(p, "<label>", nameof(s.X), s.X, r++)`) and copy the box into both the Save and Test Vault handlers.
3. Document it in README, [OPERATIONS.md](OPERATIONS.md) and [DATA-STORES.md](DATA-STORES.md).

### Map another attribute on assign

Add the tag to `BuildMap` in `AutoCadComponentService.cs`. List it in [ARCHITECTURE.md](ARCHITECTURE.md#6-attribute-map-on-assign). Do not map a preserved tag.

### Write another catalog column on add

Add the property to `CatalogAddRequest`, the field to `CatalogAdminForm`, and the column to the `values` dictionary in `AccessCatalogService.AddRecord`. The column is skipped automatically in tables that lack it.

### Add a family display name

Add the code and name to `FamilyNames.Names` in `SettingsService.cs`.

## 8. Known gaps

Bugs and risks found by reading the code (none of them fixed in code):

| # | Where | Gap |
|---|---|---|
| 1 | `CatalogBrowserForm.cs` `Assign` (lines 451-470) | From `LSECATALOG` with no block, Assign picks a block but applies the preview built for no block (empty), so nothing is written while the status line says `Assigned` and Recent is updated |
| 2 | `CatalogBrowserForm.cs:470` | `AutoCadComponentService.Apply` is not in a try/catch; an AutoCAD error in a button handler escapes the dialog as an unhandled WinForms exception |
| 3 | `CatalogTransactionService.cs:3-4` | The backup and the duplicate check run before Vault check-out downloads a newer catalog, so the duplicate check can miss a row added by another user, and the backup is the stale copy |
| 4 | `CatalogTransactionService.cs:5` | On failure after a download, rollback copies the stale backup over the freshly downloaded copy; if the undo check-out then fails, the file stays checked out with old content and a manual check-in would overwrite the newer catalog |
| 5 | `CatalogTransactionService.cs:4` vs `VaultCatalogService.cs:55,67` | A check-in failure triggers rollback and undo check-out, but the check-in message says the working copy still holds the record and is checked out |
| 6 | `CatalogTransactionService.cs:3` | Backup is taken before validation; every refused add (duplicate, read-only, checked out elsewhere) copies the whole MDB (about 615 MB); same-second attempts share a folder; nothing prunes |
| 7 | `CatalogTransactionService.cs:5` | With Vault off and a read-only MDB, the refusal is followed by `Local rollback: FAILED` (cannot copy over a read-only file); harmless but alarming |
| 8 | `AccessCatalogService.cs` `Exists` / `ReadTable` | Duplicate check and read-back read only the first 20,000 rows of the table |
| 9 | `AccessCatalogService.cs` `Open` | The `writable` parameter is unused |
| 10 | `AutoCadComponentService.cs` `Apply` (line 109) | Opens every attribute `ForWrite` and rewrites unchanged values, contrary to the guardian's rule of opening only changed attributes |
| 11 | `AutoCadComponentService.cs` `EnsureHiddenAttribute` | Adds an attribute definition to the block definition, which changes every future insert of that block in the drawing |
| 12 | `AutoCadComponentService.cs:61-65` | The `COLOR` branch only runs for preserved tags, and `COLOR` is never preserved, so the branch is dead code |
| 13 | `CatalogBrowserForm.cs` `SelectBlock` / `Assign` | `Hide()` on a modal dialog and `Editor.GetEntity` while it is open are unverified; a hidden modal WinForms dialog can end its modal loop. AutoCAD normally needs `Editor.StartUserInteraction` for this |
| 14 | `CatalogAdminForm.cs` | No `USER1` format or uniqueness check; **Manage standards** has no editor; the result label sits in a 170-pixel column |
| 15 | `Plugin.cs` | `Settings`, `Admin`, `Diag` commands have no try/catch |
| 16 | Versioning | No build tag; `AppVersion` 1.0.0 and the `V2` diagnostics label do not track releases |
| 17 | `..\agents\lse-catalog-data.md` | Still says Vault uses Windows authentication only; the code now tries the add-in session and the login dialog first |

## 9. First-build verification checklist

1. `LSECATALOGDIAG`: `Catalog read: PASS`, table count matches Access, Vault client line says compatible.
2. `LSECATALOG`: families listed with friendly names; Full Catalog shows rows; Search and the three filters narrow them.
3. Toggle favorite, close, reopen: My Favorites shows the row.
4. `LSEASSIGNCOMPONENT` on a test drawing: pick a block, check Preserve ticks, Assign, check attributes in the drawing and in AutoCAD Electrical's Edit Component, check a missing `USER1` becomes a hidden attribute.
5. **Select block** and **Assign component** from `LSECATALOG`: confirm whether picking from the hidden dialog works (gaps 1 and 13).
6. `LSECATALOGADMIN` on a copy of the MDB with Vault off: add, open the copy in Access, read `TransactionReport.txt`; add the same part again and expect the duplicate refusal.
7. Make the copy read-only and add: expect the read-only refusal.
8. Settings **Test Vault** with the real Vault path: connection route, version, check-out state.
9. Vault on, against a test file in Vault first: add, confirm the new version and comment in Vault Explorer; repeat while another user has it checked out.
