# LSE Footprint Tools (Autodesk Inventor 2026 add-in)

Maintained by DCW. Last reviewed 2026-11-13.

**Status (2026-11-12-r7):** Vault code rewritten for Vault 2025/2026 shapes; verified against a stub SDK (`tests/VaultFakeTest`, 27 checks); add-in still never compiled against Inventor.

**Who uses it:** the LSE engineer who adds a new purchased component to the AutoCAD Electrical catalog. The engineer works in Inventor because that is where the vendor's STEP or STL model opens; the result is used by everyone who later inserts the part in AutoCAD Electrical (panel footprint and `USER1` part number).

Forces every new LSE part number through one guided flow inside Inventor:

1. **Model** – pick an STL or STEP file; the add-in imports it into Inventor and shows its extents.
2. **View** – choose Top / Bottom / Front / Back / Left / Right / current view (preview it in Inventor), then project it. The projected curves become the AutoCAD Electrical panel footprint. "Outline rectangle only" is available for STL meshes, whose facet edges are not useful footprint geometry.
3. **Part number** – choose the LSE catalog tab, category code, type code and manufacturer code from the naming standard (`config/LseNamingStandard.json`, generated from *LSE ELECTRICAL PARTS CATALOG MANAGER.xlsx*). Enter MANUFACTURER, catalog number (MPN), description and the AutoCAD Electrical family table. "Preview next part number" shows the number that would be issued.
4. **Create** – one transaction: lock, then backups, then read-only check, then issue number, then write footprint DXF, then convert to DWG with AutoCAD, then (Vault checkout), then insert catalog row, then read-back verify, then (Vault checkin), then register the footprint in `footprint_lookup.mdb`. Any failure after the backups rolls both databases back from the backup and removes the generated drawing files. Details and caveats: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) section 5.4.

There are no typed commands. The add-in is loaded by Inventor from its `.addin` manifest (there is no NETLOAD or bundle equivalent) and is driven from two ribbon buttons.

## Documentation

| Document | Read it for |
|---|---|
| this README | overview, part number format, what gets written, build, deploy, verify, known gaps |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | components, dependency diagram, end-to-end data flow, design decisions |
| [docs/CODE-REFERENCE.md](docs/CODE-REFERENCE.md) | every public member of every source file and the extractor script |
| [docs/DATA-STORES.md](docs/DATA-STORES.md) | settings.json, the naming JSON, both MDB files, drawings, backups, lock, Vault |
| [docs/OPERATIONS.md](docs/OPERATIONS.md) | install, configure, create a part step by step, troubleshooting, recovery |
| [docs/DEVELOPER-GUIDE.md](docs/DEVELOPER-GUIDE.md) | build, debug inside Inventor, regenerate the JSON, conventions, extending, gaps |
| [docs/GLOSSARY.md](docs/GLOSSARY.md) | terms |

Refresh these documents whenever a change lands in `src/`. The conventions they follow are in `../agents/lse-documenter.md`.

## Part number format

`[Category 2 digits][Manufacturer code 3 letters][Type code 2 letters][Sequence 7 digits]` = 14 characters, e.g. `45BUSFH0000001`.
The sequence is the next unused number for that Category + Manufacturer + Type prefix, found by scanning `USER1` in **every** family table of `default_cat.mdb` (trimmed, case-insensitive). Numbers are never reused and gaps are never filled. Nothing is ever deleted or updated in `default_cat.mdb`; the only write is one `INSERT` with an explicit column list.

The workbook is the source of the codes. Regenerate the JSON after the workbook changes:

```
python -I tools\extract_naming_standard.py "C:\Users\david\Documents\AutoCAD_Electrical_2026\LSE ELECTRICAL PARTS CATALOG MANAGER.xlsx" config\LseNamingStandard.json "C:\Users\david\Documents\AutoCAD_Electrical_2026\Catalogs\default_cat.mdb"
```

The third argument is optional. When given, the catalog is opened read-only to learn which `MANUFACTURER` value is used with each 3-letter code and which family tables each 2-digit category lands in (used only as defaults in the wizard).

## What gets written

| Target | Write | Notes |
|---|---|---|
| `default_cat.mdb` → chosen family table | one `INSERT` (CATALOG, MANUFACTURER, DESCRIPTION, USER1, WEBLINK when the column exists) | `USER1` = LSE part number. DESCRIPTION follows the LSE import convention `<description>, MPN <catalog>` (setting). Backup in `LSE_Catalog_Backups\<stamp>\` with `TransactionReport.txt`. |
| `footprint_lookup.mdb` → table named after MANUFACTURER | `INSERT` or `UPDATE` of CATALOG / BLKNAM / DESCRIPTION | The table is created (`CATALOG, ASSEMBLYCODE, BLKNAM, DESCRIPTION, RECNUM`) when it does not exist. `BLKNAM` is the full path of the DWG, the same convention as the existing `LAKE SHORE` row. The DWG path is written even when the DWG step was skipped, so the DWG must then be saved by hand at that path. |
| `<FootprintLibraryFolder>\<PN>.dxf` and `.dwg` | footprint drawing | Geometry centred on 0,0 in footprint units (inches by default). Attributes: `P_TAG1` (visible), `MFG`, `CAT`, `ASSYCODE`, `DESC1-3`, `INST`, `LOC`, `P_ITEM`, `XREF`, `XREFNO` (invisible) plus `USER1` holding the LSE part number. MFG/CAT/USER1 are pre-filled. |
| `LSE_PartNumber.lock` next to the catalog | temporary lock file | Serialises issuance across workstations on the share; deleted on close. |

## Build

- Target: `net8.0-windows`, WinForms, x64. Inventor 2025+ add-ins run on .NET 8.
- Reference: `Autodesk.Inventor.Interop.dll` from `C:\Program Files\Autodesk\Inventor 2026\Bin\Public Assemblies` (Copy Local = false). Override with `dotnet build -p:InventorBin="D:\Autodesk\Inventor 2026\Bin"`.
- No NuGet packages. ADODB and AutoCAD are late-bound COM (`dynamic`); the Vault SDK is used by reflection: assemblies already loaded by Inventor's Vault add-in, else those in the Vault Explorer folder (auto-discovered when the setting is blank).
- **This folder was authored on a machine without the .NET SDK, Inventor or AutoCAD, so the code has not been compiled or run yet.** Build it on an engineering workstation first; expect to fix small interop signature differences (see "Verify" below).

## Deploy

1. Build Release. Copy the output folder (DLL, `LseNamingStandard.json`, `.addin`) to a share or each workstation.
2. Copy `LSE.Inventor.FootprintTools.addin` into `%APPDATA%\Autodesk\Inventor 2026\Addins\` (per user) or `C:\ProgramData\Autodesk\Inventor Addins\` (all users). If the DLL is not next to the `.addin`, put its full path in `<Assembly>`.
3. Start Inventor. The **LSE** panel appears on the Tools tab (zero-document, part, assembly and drawing ribbons) with *LSE Footprint* and *LSE Settings*.
4. Run *LSE Settings* once per user: catalog MDB (auto-discovered under `Documents\Acade 2026\...` or `Documents\AutoCAD_Electrical_2026\Catalogs`), footprint lookup MDB, footprint library folder (should be a folder AutoCAD Electrical can read; the `LAKE SHORE` footprint row points into `C:\_Lake_Shore_E_Vault\...`), naming standard JSON (point everyone at one shared copy), units, layers, Vault. Press **Test**.

Settings live in `%APPDATA%\LSE\InventorFootprintTools\settings.json`.

## Requirements on the workstation

- Inventor 2026 or later (the add-in host). The manifest says `<SupportedSoftwareVersionGreaterThan>29..</SupportedSoftwareVersionGreaterThan>`; Inventor 2025 is internal version 29, so the manifest loads the add-in in 2026 (version 30) and later only, not in 2025.
- 64-bit Access Database Engine (ACE 16 or 12) – the same requirement as the AutoCAD Electrical plugin.
- AutoCAD Electrical, if *Convert DXF to DWG with AutoCAD* is on (default). The add-in attaches to a running AutoCAD or starts one invisibly, opens the DXF and saves it as a 2018 DWG. Without AutoCAD the DXF is still produced; turn the setting off and save it as DWG by hand.
- Vault client (Vault 2026.4, same generation as Inventor 2026.4), only if *Catalog Vault path* is set. Both moved to .NET 10 in August 2026 and must be updated together: the Vault client DLLs built for .NET 10 cannot load into an Inventor still on .NET 8. Settings > Test reports the host .NET, the installed Vault client and whether they match.

## Verify on first build (things that could not be checked here)

- Inventor interop names used: `Documents.Open`, `Documents.Add`, `DrawingViews.AddBaseView`, `DrawingView.get_DrawingCurves(null)`, `DrawingCurveSegment.Geometry/GeometryType`, `Curve2dEvaluator.GetParamExtents/GetLengthAtParam/GetPointAtParam`, `Camera.ViewOrientationType`, `FileManager.GetTemplateFile`, `ControlDefinitions.AddButtonDefinition`, `RibbonPanels.Add`, `CommandControls.AddButton`. Optional-parameter arities may differ by release.
- That `Documents.Open` imports `.stl` on your Inventor configuration (it does for STEP). If not, use *File → Open* on the STL yourself, then press *Import* with the `.ipt` Inventor saved, or extend `InventorGeometryService.ImportModel`.
- Whether drawing views of mesh (STL) bodies expose usable `DrawingCurves`. The wizard defaults STL files to *Outline rectangle only*, which uses the view's width/height instead.
- The AutoCAD Electrical footprint attribute set in `DxfFootprintWriter.StandardAttributes` against the AcadE 2026 help, and that AcadE inserts a plain DWG with those `ATTDEF`s as a footprint. Adjust the list if your footprint library uses a different set.
- `Arc2d.SweepAngle` sign convention (handled for both signs, but check one arc visually).
- Vault (`src/VaultCatalogService.cs`, `src/VaultProbe.cs`): rewritten 2026-11-12 for Vault 2025/2026 and checked only against a stub SDK that carries the real type names (`tests/VaultFakeTest`: build it and run `bin\Release\VaultFakeTest.dll`, expect `ALL PASS`). Never run against a real Vault server. Connection order: the logged-in session of Inventor's Vault add-in, else the standard Vault login dialog (Vault, Windows or Autodesk ID accounts; Checkout may show it, cancelling aborts and rolls back), else Windows authentication. Checkout downloads the latest Vault version over the working copy when size or date differ, refuses when another user holds the check-out and clears the read-only flag, so a stale copy is never checked in. Checkin and undo release any session the class opened. *Catalog Vault path* looks like `$/folder/.../default_cat.mdb`; blank = no Vault.
- The footprint DWG, DXF and `footprint_lookup.mdb` are **not** Vault-controlled. They must be writable: when they sit in the Vault working folder, check them out first (read-only files stop the run before anything is changed). After success the report lists the files in the Vault working folder that still need to be checked in or added.

## Test

`tests/VaultFakeTest` is the only automated test. It compiles `src/VaultCatalogService.cs` and `src/VaultProbe.cs` against four stub assemblies (`tests/VaultFakeTest/FakeSdk/`) that carry the real Vault type names and constructor shapes. It needs no Vault, Inventor or AutoCAD:

```
dotnet build tests\VaultFakeTest\VaultFakeTest.csproj -c Release
dotnet tests\VaultFakeTest\bin\Release\VaultFakeTest.dll
```

Expect 27 `PASS` lines and `ALL PASS`; the exit code is the number of failures. The test works in `%TEMP%\lse-vault-faketest`. Nothing tests the wizard, the geometry, the DXF writer or the Access code.

## Versioning

The project has no `<Version>`, no `BuildInfo` and no build tag in any window title, so the DLL reports the SDK default 1.0.0.0. A build is identified only by the release package it came in (`releases\LSE-Plugins-<date>-r<n>\LSE.Inventor.FootprintTools-<date>-r<n>.zip`) and by the "Status" line at the top of this README. The `.addin` manifest's `DataVersion` and `UserInterfaceVersion` are both 1; raising `UserInterfaceVersion` is the usual way to make Inventor treat the next load as a first load and rebuild the ribbon panel (not verified on a workstation).

## Known gaps

- Vault mode: the part number and the duplicate check are computed from the local catalog **before** the Vault check-out downloads the latest version over it, so a stale working copy can still yield a number or MPN that already exists in Vault. See [docs/DEVELOPER-GUIDE.md](docs/DEVELOPER-GUIDE.md) section 9 for this and the other suspected defects.
- The LSE workbook's *LSE Recommended Part Number* columns may contain numbers that were never imported into `default_cat.mdb`. The add-in only knows numbers that are in the catalog, so a recommended-but-unimported number can collide. Import the workbook first, or run the reconciliation before go-live.
- Issuance scans every family table on each create (about 1.3 M rows in `default_cat.mdb`). On a LAN share expect a few seconds; `Preview next part number` performs the same scan.
- `MANUFACTURER` tables in `footprint_lookup.mdb` must match the catalog `MANUFACTURER` value exactly (AcadE looks the footprint up by manufacturer then catalog number). The wizard pre-fills MANUFACTURER from the code→value mapping learned from the live catalog.
- No edit or delete of existing parts, no description generator yet (type the LSE standard description; the tab's pattern is shown as placeholder text).

## Pending work

- Paused 2026-11-12: shared `LSE_Library.lock` + blank ASSEMBLYCODE fix. See [docs/pending/PENDING-LOCK-AND-ASSEMBLYCODE.md](docs/pending/PENDING-LOCK-AND-ASSEMBLYCODE.md). Not in the code yet (checked 2026-11-13: `FootprintLookupService.Find` and `Upsert` still filter on `ASSEMBLYCODE IS NULL`, and only `LSE_PartNumber.lock` is taken); ask the user before starting. That note says `src\` matches r4 exactly; this is no longer true, because the r7 Vault rewrite changed `src/FootprintTransactionService.cs` and `src/SettingsForm.cs` and added `src/VaultCatalogService.cs` and `src/VaultProbe.cs`. Re-check its ready-made patch against the current file before using it.
