# Developer guide: LSE Footprint Tools

Maintained by DCW. Last reviewed 2026-11-08.

How to build, debug, extend and regenerate the add-in. Read [ARCHITECTURE.md](ARCHITECTURE.md) for the design and [CODE-REFERENCE.md](CODE-REFERENCE.md) for member-level detail.

## 1. State of the code

The tree was authored on a machine with no .NET SDK, no Inventor and no AutoCAD. **Nothing has been compiled or executed.** Treat the first build as a review pass: expect interop signature mismatches and fix them against the Inventor 2026 interop assembly. The specific calls to check are listed in the README's "Verify on first build" section and repeated in section 6 below.

## 2. Repository layout

```
inventor-addin/
  LSE.Inventor.FootprintTools.csproj   SDK-style project, net8.0-windows, x64, WinForms
  LSE.Inventor.FootprintTools.addin    Inventor manifest (ClassId = the add-in GUID)
  README.md                            overview, format, writes, build, deploy, verify, gaps
  config/LseNamingStandard.json        codes extracted from the LSE workbook; copied to output
  docs/                                this documentation set
  src/*.cs                             eleven source files, one namespace
  tools/extract_naming_standard.py     workbook to JSON extractor
```

## 3. Build

Prerequisites on the build machine: .NET 8 SDK, Inventor 2026 (for `Autodesk.Inventor.Interop.dll`), Windows x64.

```
dotnet build -c Release
```

The project reads the interop assembly from `C:\Program Files\Autodesk\Inventor 2026\Bin\Public Assemblies\Autodesk.Inventor.Interop.dll`. On a machine with Inventor elsewhere:

```
dotnet build -c Release -p:InventorBin="D:\Autodesk\Inventor 2026\Bin"
```

Project settings worth knowing:

| Setting | Value | Reason |
|---|---|---|
| `TargetFramework` | `net8.0-windows` | Inventor 2025+ hosts .NET 8 add-ins. |
| `PlatformTarget` | `x64` | Inventor is 64-bit; the ACE provider must be too. |
| `EnableDynamicLoading` | `true` | The DLL is loaded by Inventor, not run as an app. |
| `AppendTargetFrameworkToOutputPath` | `false` | Output lands in `bin\Release\` directly so the manifest's relative `<Assembly>` works. |
| `Nullable`, `ImplicitUsings` | enabled | `System`, `System.IO`, `System.Linq`, `System.Collections.Generic`, `System.Threading` are implicit. |
| Interop reference | `Private=false`, `EmbedInteropTypes=false` | Inventor supplies the assembly at runtime. |
| Packages | none | ADODB, AutoCAD and Vault are late-bound. |

The build copies the `.addin` and `LseNamingStandard.json` next to the DLL.

## 4. Debugging inside Inventor

1. Build Debug. Point the `.addin` manifest's `<Assembly>` at `bin\Debug\LSE.Inventor.FootprintTools.dll` and copy the manifest into `%APPDATA%\Autodesk\Inventor 2026\Addins\`.
2. In Visual Studio set the project's debug launch to the Inventor executable (`Inventor.exe` in the Inventor `Bin` folder) or attach to a running Inventor.
3. Inventor's **Add-In Manager** shows load errors. `LoadOnStartUp` is 1 in the manifest; set **Load Behavior** to *Loaded/Unloaded* while iterating so Inventor can be left running.
4. Use the wizard's log pane: every guarded action logs `ERROR: <message>`. The full exception text for ribbon-level failures appears in a message box from `StandardAddInServer.Run`.
5. For transaction failures read `TransactionReport.txt` in the backup folder; the exception message thrown from `FootprintTransactionService.Execute` is that same text.

Settings can be edited directly in `%APPDATA%\LSE\InventorFootprintTools\settings.json` between runs; the wizard reloads them each time it opens.

## 5. Regenerating the naming standard

Requires Python 3 with `openpyxl`, and `pyodbc` plus the Access ODBC driver when the optional catalog argument is given. Always run with `-I` so nothing is imported from the data folders.

```
python -I tools\extract_naming_standard.py "C:\Users\david\Documents\AutoCAD_Electrical_2026\LSE ELECTRICAL PARTS CATALOG MANAGER.xlsx" config\LseNamingStandard.json "C:\Users\david\Documents\AutoCAD_Electrical_2026\Catalogs\default_cat.mdb"
```

Omit the third argument to skip the catalog pass; `acadeManufacturer` values and `familyHints` will then be empty and the wizard falls back to the code's name for `MANUFACTURER`. The script prints `tabs=N manufacturers=N familyHints=N`. Commit the regenerated JSON and replace the shared copy that the workstations point at.

What the extractor expects of each `LSE ...` worksheet (first 40 rows): a row whose first cell is `Part # Segment` (optionally with `Manufacturer` and `Code` headers further right), then `Category` and `Type` rows with codes in column C, a `Manufacturer | Code` list to the right, and a `Description Field` row that ends the block. A `Description` row's column B becomes the description pattern. In the shipped JSON every pattern is the word `Format`, which suggests the extractor is reading a header cell; check the workbook layout and adjust the `description` match in the script if a real pattern exists.

## 6. Verify on first build

Items that could not be checked without the tools installed. Work through them in order; each maps to a specific member in [CODE-REFERENCE.md](CODE-REFERENCE.md).

1. Interop signatures and optional-argument arity: `Documents.Open(path, true)`, `Documents.Add(type, template, false)`, `DrawingViews.AddBaseView(...)` (eight arguments as written), `DrawingView.get_DrawingCurves(null)`, `DrawingCurveSegment.Geometry` / `GeometryType`, `Curve2dEvaluator.GetParamExtents` / `GetLengthAtParam` / `GetPointAtParam` (array `ref` parameters), `Camera.ViewOrientationType`, `FileManager.GetTemplateFile(...)`, `ControlDefinitions.AddButtonDefinition(...)` (nine arguments), `RibbonPanels.Add(...)`, `CommandControls.AddButton(...)`.
2. That `Documents.Open` imports `.stl` on this Inventor configuration. If not, open the STL in Inventor by hand and import the saved `.ipt`, or extend `InventorGeometryService.ImportModel`.
3. Whether drawing views of mesh bodies expose `DrawingCurves` at all. The wizard defaults STL files to the outline rectangle, which uses `view.Width`/`view.Height` instead.
4. `Arc2d.SweepAngle` sign convention. The writer handles both signs; confirm one arc visually in AutoCAD.
5. The footprint `ATTDEF` set against AutoCAD Electrical 2026's footprint attribute help, and that AcadE inserts a plain DWG with those attributes as a footprint.
6. `SaveAs(dwg, 64)`: confirm 64 is `ac2018_dwg` in the installed AutoCAD's `AcSaveAsType`; the code falls back to the default format if the call is rejected.
7. Vault: the reflection calls into `WebServiceManager`, `WinAuthCredentials`, `CheckoutFile`, `CheckinFile` and the `UndoCheckout*` search were copied from the AutoCAD Electrical plugin and have not been exercised from Inventor.
8. ADODB `OpenSchema(20)` field positions (table name at index 2, type at index 3) with the installed ACE provider.

## 7. Conventions

The code follows the house style used by the sister AutoCAD Electrical plugin (see `STYLE-GUIDE` at the repository root). Points that matter when adding code here:

- File-scoped namespace `LSE.InventorFootprintTools;` on one line after the `using`s. The namespace differs from the assembly name on purpose.
- Two densities, never mixed in one file: services, models, the settings form and the plugin class are compact (one member per line); the wizard, geometry service and DXF writer are expanded.
- All COM is `dynamic`, released through `Ace.Release` in `finally`; connections closed through `Ace.Close`. Never hold a COM object in a field beyond the method that created it, except the Inventor `Application` and the two button definitions.
- Anything that writes `default_cat.mdb` or `footprint_lookup.mdb` goes through `FootprintTransactionService.Execute`. UI code never calls `AccessCatalogService.AddRecord` or `FootprintLookupService.Upsert` directly.
- SQL literals pass through `Ace.E`; table names through `Ace.ValidateTable`.
- Every user-facing failure is an `InvalidOperationException` (or `FileNotFoundException`) with a sentence that tells the engineer what to do; the wizard shows `ex.Message`, not the stack.
- No background threads. Long operations run on Inventor's UI thread under the wizard's wait cursor.
- No new NuGet packages or compile-time references to AutoCAD or Vault.

## 8. Extending

Typical changes and where they go:

| Change | Files |
|---|---|
| Add a setting | `Models.cs` (`AppSettings` property with default), `SettingsForm.cs` (one `Add`/`AddCheck` row and a line in `Save`), consumer. |
| Add a catalog column to the insert | `Models.cs` (`CatalogAddRequest`), `AccessCatalogService.AddRecord` (`values` dictionary), `FootprintWizardForm.Create` to fill it. |
| Change the footprint attribute set | `DxfFootprintWriter.StandardAttributes`. |
| Support another model format | `InventorGeometryService.SupportedExtensions` and the `Browse` filter in the wizard. |
| Add an edit or delete of existing parts | Not supported today by design; the catalog rules (never update or delete `default_cat.mdb`) would need an explicit decision first. |
| Description generator | `FootprintWizardForm` step 3; the tab's `descriptionPattern` is already shown as placeholder text. |
| Change part-number segment lengths | `format` in the JSON only. Do not change after numbers have been issued. |

## 9. Known gaps (as of 2026-11-08)

- Never compiled or run (section 1).
- The LSE workbook's *LSE Recommended Part Number* columns may hold numbers never imported into the catalog; those can collide with issued numbers. Import or reconcile first.
- Each issue and each preview scans every family table (about 1.3 million rows). A few seconds on a LAN share.
- `MANUFACTURER` tables in `footprint_lookup.mdb` must match the catalog `MANUFACTURER` value exactly.
- No edit or delete of existing parts and no description generator.
- Whole-file rollback restores the entire catalog, so rows another user added during the few seconds between backup and rollback would be lost. The lock serialises LSE issuance only, not AutoCAD Electrical's own writes.
- The wizard limits the description to 100 characters and then appends `, MPN <catalog>`; with a 60-character catalog number the combined `DESCRIPTION` can exceed the 150-character catalog limit and the transaction refuses it before writing. Shorten the description.
- Six tabs in the shipped JSON have no category codes and every tab's `descriptionPattern` is the placeholder word `Format` (section 5).
- `ReadUser1StartingWith` swallows per-table errors, so a table that fails to read (permissions, corruption) is silently excluded from the scan and could make the add-in issue a number already used there.
- The imported model stays open in Inventor after a create.
- `LSE_Catalog_Backups` is never pruned.
- The footprint DWG and `footprint_lookup.mdb` are not Vault-controlled.
