# Architecture: LSE Footprint Tools (Inventor 2026 add-in)

Maintained by DCW. Last reviewed 2026-11-13.

LSE Footprint Tools is an Autodesk Inventor add-in that turns a 3D model of a purchased electrical component into an AutoCAD Electrical panel footprint and, in the same step, issues the component a new LSE part number and records it in the AutoCAD Electrical catalog database. It exists so that every new LSE part number is created through one guided, logged and reversible flow instead of by hand-editing `default_cat.mdb`. It is the sister project of the AutoCAD Electrical CatalogTools plugin in `../acade-catalog-tools`, which consumes the records this add-in creates.

Related documents: [CODE-REFERENCE.md](CODE-REFERENCE.md), [DATA-STORES.md](DATA-STORES.md), [OPERATIONS.md](OPERATIONS.md), [DEVELOPER-GUIDE.md](DEVELOPER-GUIDE.md), [GLOSSARY.md](GLOSSARY.md).

## 1. What problem it solves

Lake Shore Electric (LSE) assigns every catalog component a 14-character part number stored in the `USER1` column of AutoCAD Electrical's catalog. Before this add-in, three separate manual tasks were needed for a new component: draw a panel footprint in AutoCAD, work out the next free part number by inspecting the catalog, and insert a catalog row plus a footprint-lookup row. Each step could be skipped or done inconsistently. The add-in collapses them into a single wizard inside Inventor, where the engineer already has the vendor's STEP or STL model open.

## 2. Components

The add-in is one .NET 8 assembly, `LSE.Inventor.FootprintTools.dll`, loaded by Inventor. It contains thirteen source files in one namespace, `LSE.InventorFootprintTools`.

| Layer | File | Responsibility |
|---|---|---|
| Host integration | `src/Plugin.cs` | Implements Inventor's add-in interface, adds the ribbon buttons, opens the two windows. |
| User interface | `src/FootprintWizardForm.cs` | The four-step wizard (model, view, part number, create) and its log pane. |
| User interface | `src/SettingsForm.cs` | Edits and tests every setting. |
| Inventor geometry | `src/InventorGeometryService.cs` | Imports the model, previews a view, projects it to 2D curves in footprint units. |
| Drawing output | `src/DxfFootprintWriter.cs` | Serialises the curves and the AutoCAD Electrical attribute set to an R12 DXF file. |
| Conversion | `src/AutoCadConvertService.cs` | Drives AutoCAD over COM to save the DXF as a DWG. |
| Catalog access | `src/AccessCatalogService.cs` | Everything that touches the two Access databases through ADODB. |
| Numbering | `src/PartNumberService.cs` | Validates codes, finds the highest used sequence, computes the next number. |
| Orchestration | `src/FootprintTransactionService.cs` | The single mutating transaction, its lock, its backups and its rollback. It calls the Vault checkout and checkin at the right points. |
| Vault | `src/VaultCatalogService.cs` | Connects to Vault (add-in session, login dialog or Windows authentication) and checks `default_cat.mdb` out, in, or undoes the checkout. Also the Vault part of Settings > Test. |
| Vault awareness | `src/VaultProbe.cs` | Host .NET and installed Vault client report, Vault add-in session detection, the read-only refusal and the check-in reminder. Identical copy in every LSE plugin. |
| Configuration | `src/SettingsService.cs` | Loads and saves `settings.json`; loads the naming standard JSON. |
| Data shapes | `src/Models.cs` | Settings, naming standard, request and result classes. |

Supporting files:

| File | Purpose |
|---|---|
| `LSE.Inventor.FootprintTools.csproj` | Build definition. References only the Inventor interop assembly. No NuGet packages. |
| `LSE.Inventor.FootprintTools.addin` | Inventor's add-in manifest. Tells Inventor which DLL and class to load. |
| `config/LseNamingStandard.json` | The LSE part-number vocabulary (categories, types, manufacturer codes) generated from the LSE parts workbook. Copied to the output folder. |
| `tools/extract_naming_standard.py` | Regenerates that JSON from the workbook and, optionally, the live catalog. |
| `tests/VaultFakeTest/` | Console test of the two Vault files against a stub Vault SDK (27 checks). Excluded from the add-in build by `<Compile Remove="tests\**" />`. |

## 3. Dependency diagram

```
 Inventor 2026 (host process, x64, .NET 8)
 |
 |  loads via .addin manifest
 v
 StandardAddInServer (Plugin.cs)
 |-- ribbon button "LSE Footprint"  -> FootprintWizardForm (modeless, one instance)
 |-- ribbon button "LSE Settings"   -> SettingsForm (modal)
 |
 FootprintWizardForm
 |-- SettingsService.Load ............... %APPDATA%\LSE\InventorFootprintTools\settings.json
 |-- NamingStandardService.Load ......... LseNamingStandard.json
 |-- InventorGeometryService ............ Inventor API (Documents, DrawingViews, Camera)
 |-- AccessCatalogService (read) ........ default_cat.mdb via ADODB/ACE
 |-- PartNumberService (preview)
 |-- FootprintTransactionService.Execute
      |-- IssuanceLock ................... LSE_PartNumber.lock (next to the catalog)
      |-- File.Copy backups .............. LSE_Catalog_Backups\<stamp>\
      |-- AccessCatalogService ........... default_cat.mdb (scan USER1, INSERT, verify)
      |-- PartNumberService.Next
      |-- DxfFootprintWriter ............. <FootprintLibraryFolder>\<PN>.dxf
      |-- AutoCadConvertService .......... AutoCAD.Application COM -> <PN>.dwg
      |-- VaultCatalogService (optional) . Vault SDK by reflection -> checkout / checkin
      |-- FootprintLookupService ......... footprint_lookup.mdb (CREATE TABLE, INSERT/UPDATE)
      +-- TransactionReport.txt .......... written into the backup folder
```

Nothing above the transaction service writes to disk except the settings form (settings file) and the geometry service (a temporary Inventor drawing that is closed without saving).

## 4. External systems and how each is reached

| System | Mechanism | Compile-time reference | Why this way |
|---|---|---|---|
| Inventor | `Autodesk.Inventor.Interop.dll`, early-bound | Yes (Copy Local = false) | The add-in runs inside Inventor, so the interop assembly is always present at runtime. |
| Access databases (`.mdb`) | `ADODB.Connection` created from its ProgID, driven through `dynamic` | No | Avoids a dependency on `System.Data.OleDb` and works with whichever 64-bit ACE provider is installed. Tries `Microsoft.ACE.OLEDB.16.0`, then `12.0`, then `Microsoft.Jet.OLEDB.4.0`. |
| AutoCAD (Electrical) | `AutoCAD.Application` COM: attach to a running instance with `GetActiveObject`, else start one | No | Only used to re-save a DXF as DWG. Late binding means the add-in builds and loads on machines without AutoCAD. |
| Autodesk Vault | SDK assemblies taken from the process (Inventor's Vault add-in) or loaded with `Assembly.LoadFrom` from the Vault Explorer folder, invoked by reflection and `dynamic` (`VaultCatalogService`, `VaultProbe`) | No | Identical approach to the AutoCAD Electrical plugin. Vault is optional; the add-in must build and run without the SDK installed. |

All COM objects are `dynamic`, released with `Marshal.FinalReleaseComObject` in `finally` blocks through `Ace.Release`, and connections are closed through `Ace.Close`.

## 5. End-to-end data flow

### 5.1 Reference data (on wizard open)

1. `SettingsService.Load` reads `settings.json` and fills blank paths with defaults: catalog auto-discovery, `footprint_lookup.mdb` beside the catalog, `LSE_Footprints` beside the catalog, the naming JSON beside the DLL.
2. `NamingStandardService.Load` reads the naming JSON and rebuilds its dictionaries as case-insensitive. The wizard fills the tab, category, type and manufacturer-code lists from it.
3. `AccessCatalogService.GetTables` lists the catalog's family tables (every table not starting with `_` or `MSys`), and `GetFamilyDescriptions` reads friendly names from `_FAMILY_DESCRIPTION`. The wizard fills the family list from both.
4. If DXF-to-DWG conversion is on and no `AutoCAD.Application` ProgID is registered, the wizard logs a warning.

### 5.2 Geometry (wizard steps 1 and 2)

1. The engineer picks a file. Supported: `.stl`, `.stp`, `.step`, `.ipt`, `.iam`. Choosing an STL ticks "Outline rectangle only" because mesh facet edges are useless as footprint geometry.
2. `InventorGeometryService.ImportModel` opens the file with Inventor's `Documents.Open(path, true)` under `SilentOperation` so translator dialogs do not appear. Inventor's translators turn STEP and STL into a part or assembly document.
3. `ModelExtentsCm` reads the component's range box. Inventor's internal length unit is centimetres everywhere in its API.
4. Optionally `PreviewView` orients Inventor's active camera to the chosen standard view so the engineer can confirm it is the mounting face.
5. `Project` creates a hidden drawing document from the configured (or default) template, adds one base view of the model at scale 1.0 in the chosen orientation and line style, then reads every `DrawingCurveSegment` from the view. Each segment is classified as line, circle, arc or "other" (sampled into a polyline). Coordinates are shifted so the view centre is the origin and scaled by

   ```
   factor = (mm ? 10 : 1/2.54) * ScaleCorrection / view.Scale
   ```

   which converts sheet centimetres to footprint units (inches by default), undoes the drawing view scale, and applies the user's STL unit correction (x25.4 or /25.4). The drawing is closed without saving. The result is a `FootprintGeometry`: a list of curves and a bounding box in footprint units, centred on 0,0.

### 5.3 Part number and catalog record (wizard step 3)

1. The engineer chooses a naming tab, which populates the two-digit category codes, two-letter type codes and three-letter manufacturer codes for that family of parts. Manufacturer codes not listed on the tab are appended from the global list.
2. Choosing a manufacturer code pre-fills the AutoCAD Electrical `MANUFACTURER` text with the value most often seen in the live catalog for that code (`acadeManufacturer` in the JSON), falling back to the code's name in upper case, cut to 24 characters.
3. Typing or choosing a category code selects a suggested family table from the JSON `familyHints`.
4. "Preview next part number" runs the same scan as the real transaction: read `USER1` from every family table where it starts with the prefix, take the highest seven-digit sequence, add one.

### 5.4 The transaction (wizard step 4)

`FootprintTransactionService.Execute` is the only code path that writes to the databases. Its order is fixed and every step appends a line to a log that becomes `TransactionReport.txt`.

| # | Step | On failure |
|---|---|---|
| 1 | Validate the part-number codes, required fields, and length limits (MANUFACTURER 24, CATALOG 60, DESCRIPTION 150). Check both MDB files exist and the library folder is set. | Throws before anything is touched. |
| 2 | Create the backup folder `<catalog folder>\LSE_Catalog_Backups\yyyyMMdd_HHmmss\`. | Throws. |
| 3 | Acquire `IssuanceLock`: open `LSE_PartNumber.lock` beside the catalog with exclusive sharing and delete-on-close. Retries 40 times at 250 ms (10 s total), then fails with "Another workstation is issuing a part number right now". | Throws. The empty backup folder from step 2 stays behind. |
| 4 | Copy `default_cat.mdb` and `footprint_lookup.mdb` into the backup folder. | Throws. |
| 4a | `VaultProbe.RequireWritable`: refuse when `footprint_lookup.mdb` is read-only, and also the catalog when the Catalog Vault path is blank. The message names the files and says to check them out in Vault. | Throws "Nothing was changed." No rollback is needed and no `TransactionReport.txt` is written; the backup copies stay. |
| 5 | Scan `USER1` across all family tables for the prefix; compute the next number; store it in the request as `USER1`. | Rollback. |
| 6 | Refuse if the same MANUFACTURER + CATALOG already exists in the chosen family table. | Rollback. |
| 7 | Create the library folder if needed; refuse if `<PN>.dxf` or `<PN>.dwg` already exists. | Rollback. |
| 8 | Write `<PN>.dxf` with the curves and the attribute set, pre-filling `MFG`, `CAT` and the LSE part attribute (default `USER1`). | Rollback. |
| 9 | If "Convert DXF to DWG with AutoCAD" is on, open the DXF in AutoCAD and save as a 2018-format DWG; otherwise log SKIPPED. | Rollback. |
| 10 | If a catalog Vault path is set, check the catalog out of Vault (latest version downloaded over the working copy when size or date differ; refused if held by another user; read-only flag cleared). This may show the Vault login dialog; cancelling it fails the run. | Rollback. |
| 11 | `INSERT` one row into the family table with only the non-empty columns that exist in that table. Read it back; refuse if not found. | Rollback. |
| 12 | If checked out, check the catalog back in with comment `Add electrical catalog component <MFG> <CAT> (<PN>)`. | Rollback. |
| 13 | Ensure a table named after `MANUFACTURER` exists in `footprint_lookup.mdb`, then insert or update the row for CATALOG with `BLKNAM` = full DWG path and the footprint description. | Rollback. |
| 14 | Log `Status: SUCCESS`, append the Vault check-in reminder lines, write `TransactionReport.txt`, return the part number and paths. | n/a |

`VaultCatalogService.EndSession()` runs in a `finally` after success or rollback. Rollback, in order: restore the catalog from its backup copy, restore the footprint lookup from its backup copy, delete the generated DXF and DWG, undo the Vault checkout if one was made, write `TransactionReport.txt` with the error and every rollback outcome, then rethrow with the whole log as the message so the wizard shows it. The lock is released when `Execute` returns or throws.

Ordering caveats in Vault mode (read from the code, not yet seen on a workstation):

- Steps 5 and 6 read the local working copy. The download of the latest Vault version happens later, in step 10. When the working copy is stale, the number issued and the duplicate check are based on the old copy, and the inserted row then lands in the newer downloaded copy. A number or MPN added on another workstation since the last download can therefore be duplicated.
- The catalog backup (step 4) is the pre-download copy. A rollback after step 10 copies that older file back over the working copy and then undoes the checkout, so the working copy ends up stale again (harmless, since it is not checked in, but the next run downloads again).
- If step 13 fails after step 12 checked the catalog in, the rollback restores the local catalog, but the new row is already in Vault. `UndoCheckout` then finds nothing checked out, logs "nothing to undo", and the log still shows "Vault undo checkout: PASS". The part number exists in Vault with no footprint lookup row. Re-run the lookup registration by hand (DATA-STORES section 4) or check the Vault history.
- When step 12 fails, `VaultCatalogService.Checkin` says the working copy "holds the new record and is still checked out", but the rollback that follows restores the backup and undoes the checkout, so neither is true by the time the engineer reads it. Trust the rollback lines at the end of the report.

### 5.5 Consumers

The AutoCAD Electrical CatalogTools plugin (`../acade-catalog-tools`) reads the inserted row and pushes `USER1` onto schematic blocks. AutoCAD Electrical itself resolves the panel footprint by looking up `footprint_lookup.mdb` by manufacturer table then catalog number and inserting the DWG named in `BLKNAM`.

## 6. Design decisions and their reasons

- **Whole-file backup and restore rather than SQL transactions.** ADODB on Access gives weak transactional guarantees across two databases plus a Vault round-trip. Copying both `.mdb` files first and copying them back on failure is simple and inspectable. Caveat: the restore puts back the entire file, so any row another user added to that catalog between the backup copy and the rollback (normally a few seconds) would be lost. The lock only serialises LSE issuance, not AutoCAD Electrical's own writes.
- **A file lock for cross-workstation serialisation.** The catalog usually lives on a share. An exclusive, delete-on-close file next to it is the cheapest mutual exclusion that works over SMB without a server component.
- **Scan every family table for the prefix.** LSE part numbers are unique across the whole catalog, not per family, and the same prefix can appear in several families. The cost is a full scan on every issue and every preview (around 1.3 million rows according to the README). Numbers are never reused and gaps are never filled.
- **DXF written by hand, DWG produced by AutoCAD.** There is no free, supported library for writing DWG. Writing an R12 (`AC1009`) DXF needs no dependency and is universally readable; AutoCAD, when present, performs the DWG save. When it is not, the DXF is still produced and the engineer saves it manually.
- **Inventor's drawing view does the projection.** Rather than reimplementing hidden-line removal and silhouette computation, the add-in creates a temporary drawing view and reads the 2D curves Inventor already computed, then discards the drawing.
- **Late binding for everything except Inventor.** The add-in must build and load on machines that lack AutoCAD, Vault or a particular ACE version. Only the host's interop assembly is referenced.
- **Two code densities.** Services, models and the settings form are written compactly (one member per line, short local names). The wizard, geometry service and DXF writer are written in standard expanded C#. This follows the house style guide for the sister plugin; do not mix the two within a file.

## 7. Process, threading and lifetime

- Everything runs on Inventor's UI thread. No background threads, no async. Long operations (catalog scans, AutoCAD start-up) block the wizard; the wizard shows a wait cursor through `Guard`.
- The wizard is modeless and a singleton: a second click on the ribbon button activates the existing window. Its owner is Inventor's main window via `InventorWindow`, so it stays above Inventor.
- The settings form is modal.
- `Deactivate` closes the wizard, releases the two button definitions and forces a garbage collection so Inventor can unload cleanly.
- AutoCAD, if the add-in started it, is quit at the end of the conversion. An already-running AutoCAD is left alone.

## 8. Units and coordinates

| Context | Unit | Notes |
|---|---|---|
| Inventor API (range boxes, sheet, view centre, curve points) | centimetres | Always, regardless of the document's display units. |
| Footprint geometry and DXF | inches (`FootprintUnits` = `in`) or millimetres (`mm`) | Set in Settings. Text height is stored in inches and multiplied by 25.4 for mm. |
| STL unit correction | factor 1, 25.4 or 1/25.4 | STL files carry no units; the engineer chooses the correction in step 1 and checks the displayed extents. |
| Angles in DXF arcs | degrees, counter-clockwise, 0 to 360 | Inventor's `Arc2d` gives radians and a signed sweep; the writer normalises both signs. |

The footprint origin (0,0) is the centre of the projected view. The visible `P_TAG1` attribute sits one text height above the geometry; invisible attributes are stacked below it.

## 9. Relationship to the AutoCAD Electrical plugin

`AccessCatalogService.cs` (the `Ace` helper, provider fallback, release pattern) was carried over from `../acade-catalog-tools` with the same shapes. `VaultCatalogService.cs` and `VaultProbe.cs` were rewritten 2026-11-12 and are now the same text as the Catalog Tools copies except for the namespace and `VaultCatalogService.AppName` (`LSE Footprint Tools` here, used as the check-out comment in the DocumentService fallback). The Vault test folder is duplicated the same way. The two projects do not share a compiled assembly; the code is duplicated deliberately so each can ship alone. If you fix a defect in one copy, check the other.
