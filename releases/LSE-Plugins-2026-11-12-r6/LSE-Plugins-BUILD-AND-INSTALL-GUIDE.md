# LSE Plugins: Build and Install Guide

Release date: 2026-11-12, sixth cut (r6). Package: `LSE-Plugins-2026-11-12-r6` on Google Drive, folder "LSE AutoCAD Electrical Agents" > Releases > Current.
This supersedes `LSE-Plugins-2026-11-12-r5` (now in Releases > Archive). r6 is an **LSE_Env** cut: the `LSEENV` plan grid no longer
builds its note rows by replacing a cell (the pattern behind the r3 "ReadOnly property of a cell cannot be set before it is added to a row"
crash); note rows keep their check-box cell, are read-only at row level and are painted with the word "note" (`src\PlanGridService.cs`,
exercised by the smoke test on a real `DataGridView`, now 89 checks). Every LSE_Env window title now ends with the build tag
`[2026-11-12-r6]`, and the error dialog, the error log and `LSEENVDIAG` name the build, so a stale `LSE_Env.dll` left in the bundle's
`Contents\` folder is obvious at a glance. **To update LSE_Env: close AutoCAD Electrical completely, replace the files in
`%APPDATA%\Autodesk\ApplicationPlugins\LSE_Env.bundle\Contents\` with the new `bin\Release\` output, start AutoCAD Electrical, run
`LSEENV` and check the title bar shows `[2026-11-12-r6]`.** **r6 also ships ready-to-load binaries:**
`LSE-Plugins-NETLOAD-READY-2026-11-12-r6.zip` holds all five AutoCAD Electrical plugins compiled Release (0 warnings) with their JSON,
`standard\` and `profiles\` files, a one-step loader (`LSE-NETLOAD-ALL.lsp`, command `LSELOAD`) and ready-made `ApplicationPlugins`
bundles, so nothing has to be built on the workstation (sections 3.0 and 4.1). Catalog Tools compiled for the first time in r6 (four
nullable warnings fixed, `bundle\LSE.CatalogTools.bundle` added); the source of the other four AutoCAD plugins is unchanged since r5. r5 added **Set symbol** to the Librarian's `LSEFOOTPRINTMAP`
(write the schematic symbol block name, `WDBLKNAM`, on existing `default_cat.mdb` rows through the same backed-up transaction), with the
Librarian smoke test at 85 checks. Everything else is as in r4. Work still in progress and deliberately NOT in r5: the Inventor add-in's
pending lock / assembly-code fix (`inventor-addin\docs\pending\`, the r4 Inventor content ships unchanged) and an edit mode for
`LSECATALOGADD`, which has not been started. r4 answered PLUG-IN_TROUBLESHOOTING Issue 3: pressing Plan in
`LSEENV` on r3 failed with "ReadOnly property of a cell cannot be set before it is added to a row" (a Windows Forms rule broken by the r2
note-row change); the cell is now placed in the grid before it is marked read-only, and every LSE_Env error dialog now says what happened,
where, and what to do, with the full detail written to `%APPDATA%\LSE\LSE_Env\Logs\Error_<stamp>.txt`. r4 also carries the Librarian's
catalog-only mode for `LSECATALOGADD` (banner; block name optional; no symbol, footprint or lookup writes), the new
`LSEFOOTPRINTMAP` command (grid of LSE parts in `default_cat.mdb` with their `footprint_lookup.mdb` mapping), an optional "Also map to a footprint" box in `LSECATALOGADD`, and `LSELIBRARYAUDIT` noting
families with unmapped LSE parts (60-check smoke test). r3 added Help buttons and tooltips to every LSE_Env dialog; r2 (same day) answered Issue 1 after the first run of LSE_Env inside AutoCAD Electrical: **LSE_Env** now reads and writes drawings that are open in the editor
(through the open document, under a document lock, not saved by the plugin) instead of skipping them, and the `LSEENV` grid
explains itself (notes carry no check box, a help line, and a reason summary when nothing applies). The other five plugins are
unchanged since 2026-11-10-r2. The morning package added LSE_Env itself and the `lse-env-guardian` agent.

This guide is the single starting point. Every plugin also ships its own `README.md` and a `docs\` set (ARCHITECTURE,
CODE-REFERENCE, DATA-STORES, OPERATIONS, DEVELOPER-GUIDE, GLOSSARY). Read this guide first, then the plugin README,
then `docs\OPERATIONS.md` when you use the tool and `docs\DEVELOPER-GUIDE.md` when you change it.

**Important:** all six plugins were written on a machine with no AutoCAD, no Inventor and no .NET SDK install.
All five AutoCAD Electrical plugins compile cleanly (0 warnings) against the Autodesk reference package and four of them passed
console smoke tests of their file logic; the Inventor add-in has never been compiled; and none has ever been loaded into AutoCAD
Electrical or Inventor. Section 7 lists
exactly what is unverified for each. Try everything on copies before touching a live job, the live catalog or the
Vault working folder.

---

## 1. What is in the package

| Zip | Host | Folder inside the zip | Commands / entry point | Status at packaging |
|---|---|---|---|---|
| `LSE.AutoCADElectrical.CatalogTools-2026-11-12-r6.zip` | AutoCAD Electrical 2026 | `acade-catalog-tools\` | `LSECATALOG`, `LSEASSIGNCOMPONENT`, `LSECATALOGADMIN`, `LSECATALOGSETTINGS`, `LSECATALOGDIAG` | Compiles Release, 0 warnings (first built 2026-11-12 in r6; four nullable warnings fixed, `bundle\LSE.CatalogTools.bundle` added). No console smoke test: its logic runs through AutoCAD and late-bound ADODB. Not loaded in AutoCAD. |
| `LSE.AutoCADElectrical.ProjectTools-2026-11-12-r6.zip` | AutoCAD Electrical 2026 | `acade-project-creator\` | `LSENEWPROJECT`, `LSEPROJECTACTIVATE`, `LSEPROJECTSETTINGS`, `LSEPROJECTDIAG` | LSE-specific since r2. Compiles Release, 0 warnings. 55-check console smoke test (byte-identical round trip of the real template `.wdp`) passed. Not loaded in AutoCAD. |
| `LSE_Doctor-2026-11-12-r6.zip` | AutoCAD Electrical 2026 | `acade-project-doctor\` | `LSEDOCTOR`, `LSEDOCTORSCAN`, `LSEDOCTORSETTINGS`, `LSEDOCTORDIAG` | Renamed from ProjectDoctor in r2; adds WD_M wire-layer checks and repairs. Compiles Release, 0 warnings. 90-check smoke test against a copy of the real template passed. Not loaded in AutoCAD. |
| `LSE.AutoCADElectrical.Librarian-2026-11-12-r6.zip` | AutoCAD Electrical 2026 | `acade-librarian\` | `LSELIBRARIAN`, `LSECATALOGADD`, `LSEFOOTPRINTMAP`, `LSELIBRARYAUDIT`, `LSELIBRARYSETTINGS`, `LSELIBRARYDIAG` | `LSECATALOGADD` and workbook part numbering new in 2026-11-10-r2. r4: catalog-only `LSECATALOGADD` mode (banner, block name optional, no symbol/footprint/lookup writes), new `LSEFOOTPRINTMAP` (symbol-to-footprint mapping grid), optional "Also map to a footprint" in `LSECATALOGADD`, audit notes families with unmapped LSE parts. r5: Set symbol in `LSEFOOTPRINTMAP` (writes `WDBLKNAM` on existing rows through the same backed-up transaction with rollback). Compiles Release, 0 warnings. 85-check catalog, footprint-mapping and symbol-link smoke test (`tests\LSE_Librarian.CatalogSmokeTest`) against copies of the real workbook, catalog and lookup passed; earlier 66-check menu test passed. Not loaded in AutoCAD. |
| `LSE_Env-2026-11-12-r6.zip` | AutoCAD Electrical 2026 | `acade-project-env\` | `LSEENV`, `LSEENVWIRES`, `LSEENVPROFILES`, `LSEENVSNAPSHOT`, `LSEENVSETTINGS`, `LSEENVDIAG` | Compiles Release, 0 warnings. 89-check smoke test (text round trip, rules, profiles, wire-network engine, planner, apply, rollback, open-drawing planning, help texts, error log, plan grid on a real DataGridView, build label) through fake drawing bridges passed. r2: open drawings supported, grid UX fixed. r3: Help buttons and tooltips in every dialog. r4: three-part error dialog with a log file. r6: plan grid rebuilt without cell replacement (`PlanGridService`), build tag `[2026-11-12-r6]` in every window title, the error dialog, the log and `LSEENVDIAG`. First run in AutoCAD Electrical on 2026-11-12 (dialog and Plan seen in the host; the writes not yet). The setting map (which `?[n]` line / `WD_M` attribute holds each setting) is inferred, not verified: run `LSEENVSNAPSHOT` first. |
| `LSE.Inventor.FootprintTools-2026-11-12-r6.zip` | Autodesk Inventor 2026 | `inventor-addin\` | Ribbon: Tools tab, **LSE** panel: *LSE Footprint*, *LSE Settings* | Source only. Never compiled (needs the Inventor interop assembly). Same content as r4; a lock / assembly-code fix is pending in the working tree and is not in this package. |
| `LSE-Plugins-NETLOAD-READY-2026-11-12-r6.zip` | AutoCAD Electrical 2026 | `LSE-Plugins-NETLOAD\` | `LSE-NETLOAD-ALL.lsp` (drag into AutoCAD; defines `LSELOAD`), `LSE-NETLOAD-ALL.scr`, `NETLOAD-README.md` | **Prebuilt binaries, no build needed.** The five AutoCAD plugins above as `bin\Release\` output (DLL, `.pdb`, JSON, `standard\`, `profiles\`), one sub-folder per DLL, plus `ApplicationPlugins\` with the five bundles already filled. Built 2026-11-12 against the AutoCAD.NET 25.1.0 reference package; each DLL references only the AutoCAD 2026 managed assemblies and the .NET 8 shared framework. Not yet loaded in AutoCAD. |
| `LSE-Plugins-ALL-2026-11-12-r6.zip` | all of the above | one folder per plugin plus `agents\`, `README.docx`, `STYLE-GUIDE.docx`, `Locations.txt`, `README.md` and `LSE-Plugins-NETLOAD\` (the prebuilt binaries) | | The same content as the seven zips, in one download. |

What each plugin does, in one line:

- **Catalog Tools**: company-curated view of `default_cat.mdb`, assign a catalog record to a selected block, admin add with backup, Vault, verify and rollback.
- **Project Tools**: wizard that creates a complete AutoCAD Electrical project (folder, drawings from the `.dwt`, `.wdp`, `.wdt`, `_WDTITLE.WDL`) and activates it.
- **LSE_Doctor**: examines an existing project against the live LSE custom template and repairs description lines, library and menu paths, options, properties, `.wdt`, `.wdl`, `.aepx` and optionally, per drawing, the `WD_M` block, its wire-layer settings and the wire layers themselves (missing ones created from the template drawing, off-standard ones repaired, unused non-standard ones purged). Batch scan of a folder tree.
- **Librarian**: files a new symbol, footprint, nameplate or user circuit into the right LSE library folder under a standard name, adds the icon-menu entry and icon image, registers footprints in `footprint_lookup.mdb`. Audit command finds misfiled, misnamed and orphaned items.
- **LSE_Env**: applies a customer profile (wire numbering, tagging, cross-reference and ladder settings) to a project's `.wdp` and every drawing's `WD_M`; two-click switching between wire schemes (LSE template, sheet-prefixed sequential, line reference/grid, From/To device); From/To numbers are traced from the wires and written as fixed wire numbers, un-fixed when switching back; a snapshot/diff tool locates where AutoCAD Electrical stores each setting.
- **Footprint Tools (Inventor)**: import STL/STEP, pick a view, project it to a footprint DXF/DWG, issue a 14-character LSE part number, insert the catalog row and register the footprint.

---

## 2. Workstation prerequisites

| Need | For | Notes |
|---|---|---|
| AutoCAD Electrical 2026 (x64) | the five AutoCAD plugins | The managed assemblies `accoremgd.dll`, `acdbmgd.dll`, `acmgd.dll` are expected in `C:\Program Files\Autodesk\AutoCAD 2026`. If your install is elsewhere pass `-p:AcadDir="<folder>"` to `dotnet build`. |
| Autodesk Inventor 2026 (x64) | the Inventor add-in | `Autodesk.Inventor.Interop.dll` is expected in `C:\Program Files\Autodesk\Inventor 2026\Bin\Public Assemblies`. Override with `-p:InventorBin="<...\Inventor 2026\Bin>"`. |
| .NET 8 SDK (x64) | building everything | Check with `dotnet --list-sdks`. If nothing is listed install the SDK from https://dotnet.microsoft.com/download/dotnet/8.0 (the "SDK", not the runtime). Visual Studio 2022 17.8 or later with the ".NET desktop development" workload also works; open the `.csproj` directly. |
| 64-bit Microsoft Access Database Engine (ACE 16 or 12) | Catalog Tools, Footprint Tools, Librarian footprint lookup | Must match AutoCAD bitness (64-bit). The `...DIAG` commands report "Catalog read: FAIL" for all providers when it is missing. |
| Vault Explorer (optional) | Catalog Tools admin adds, Footprint Tools catalog check-in | Only when the "Catalog Vault path" setting is filled in. The Vault SDK DLLs are loaded by reflection from the Vault Explorer folder; nothing is referenced at compile time. |
| Write access to the Vault working folder | Librarian, Doctor, Project Tools | `C:\_Lake_Shore_E_Vault\CAD Standards\AutoCAD\ECAD Standards\...` as listed in `Locations.txt`. |
| Internet (first build only, and only if AutoCAD is NOT installed on the build machine) | restoring the `AutoCAD.NET` 25.1.0 NuGet reference package | On a workstation with AutoCAD 2026 installed the `.csproj` uses the installed DLLs and needs no package. |

---

## 3. Build

### 3.0 No build needed: the NETLOAD-READY zip

`LSE-Plugins-NETLOAD-READY-2026-11-12-r6.zip` already contains the Release output of all five AutoCAD Electrical plugins. Use it unless
you have changed the source. Unblock it, extract it to `C:\` (giving `C:\LSE-Plugins-NETLOAD`), then go straight to section 4.1:

```powershell
Unblock-File "$env:USERPROFILE\Downloads\LSE-Plugins-NETLOAD-READY-2026-11-12-r6.zip"
Expand-Archive "$env:USERPROFILE\Downloads\LSE-Plugins-NETLOAD-READY-2026-11-12-r6.zip" -DestinationPath "C:\"
Get-ChildItem "C:\LSE-Plugins-NETLOAD" -Recurse | Unblock-File
```

Inside: one folder per DLL (`LSE.AutoCADElectrical.CatalogTools\`, `LSE.AutoCADElectrical.ProjectTools\`, `LSE_Doctor\`,
`LSE.AutoCADElectrical.Librarian\`, `LSE_Env\`), each with its JSON / `standard\` / `profiles\` beside the DLL; the loader
`LSE-NETLOAD-ALL.lsp`; `NETLOAD-README.md`; and `ApplicationPlugins\` with the five bundles ready to copy (section 4.2).
The binaries were built on 2026-11-12 on a machine without AutoCAD, against Autodesk's `AutoCAD.NET` 25.1.0 reference package
(the AutoCAD 2026 API, version 25.1.0.0). Each DLL is x64, .NET 8, and references only `acmgd`, `acdbmgd`, `accoremgd` and the .NET 8
shared framework that AutoCAD 2026 runs on, so nothing else has to be installed beside the 64-bit Access Database Engine (section 2).
The rest of this section is for rebuilding from source.

### 3.1 Unblock and extract

Windows marks downloaded zips as "from the internet". AutoCAD can refuse to NETLOAD a DLL built from blocked files.
Unblock the zip before extracting (right-click, Properties, **Unblock**, or in PowerShell):

```powershell
Unblock-File "C:\Downloads\LSE-Plugins-ALL-2026-11-12-r6.zip"
Expand-Archive "C:\Downloads\LSE-Plugins-ALL-2026-11-12-r6.zip" -DestinationPath "C:\LSE-Plugins"
```

### 3.2 Build the five AutoCAD Electrical plugins

Each plugin is a self-contained SDK-style project: `net8.0-windows`, WinForms, x64, nullable, no runtime NuGet
packages. From a Developer PowerShell or any PowerShell with `dotnet` on the path:

```powershell
cd C:\LSE-Plugins
dotnet build "acade-catalog-tools\LSE.AutoCADElectrical.CatalogTools.csproj"    -c Release
dotnet build "acade-project-creator\LSE.AutoCADElectrical.ProjectTools.csproj"   -c Release
dotnet build "acade-project-doctor\LSE_Doctor.csproj"   -c Release
dotnet build "acade-librarian\LSE.AutoCADElectrical.Librarian.csproj"            -c Release
dotnet build "acade-project-env\LSE_Env.csproj"   -c Release
```

The output lands in `<plugin>\bin\Release\` (the target-framework subfolder is turned off). Expected contents:

| Plugin | Output folder must contain |
|---|---|
| Catalog Tools | `LSE.AutoCADElectrical.CatalogTools.dll` |
| Project Tools | `LSE.AutoCADElectrical.ProjectTools.dll`, `LseProjectTemplates.json` |
| LSE_Doctor | `LSE_Doctor.dll`, `LseDoctorRules.json`, `standard\` (template `.wdp`, `.wdt`, `.wdl` fallback copies) |
| Librarian | `LSE.AutoCADElectrical.Librarian.dll`, `LseLibraryStandard.json`, `LseNamingStandard.json` |
| LSE_Env | `LSE_Env.dll`, `LseEnvRules.json`, `profiles\` (three sample customer profiles) |

If AutoCAD 2026 is installed somewhere other than `C:\Program Files\Autodesk\AutoCAD 2026`:

```powershell
dotnet build "acade-project-doctor\LSE_Doctor.csproj" -c Release -p:AcadDir="D:\Autodesk\AutoCAD 2026"
```

Expect a clean build with 0 warnings for all five (they were built that way against the
reference package; the binaries in the NETLOAD-READY zip are exactly that output). LSE_Env also has a console smoke test: `dotnet build acade-project-env\tests\LSE_Env.SmokeTest -c Release`
then run `acade-project-env\tests\LSE_Env.SmokeTest\bin\Release\LSE_Env.SmokeTest.dll acade-project-env` (expect 89 passed). If a build fails with a missing-namespace error, confirm `AcadDir` points at the folder
that holds `acdbmgd.dll`.

### 3.3 Build the Inventor add-in

```powershell
dotnet build "inventor-addin\LSE.Inventor.FootprintTools.csproj" -c Release
```

Output: `inventor-addin\bin\Release\` with `LSE.Inventor.FootprintTools.dll`, `LSE.Inventor.FootprintTools.addin`,
`LseNamingStandard.json`. **This project has never been compiled.** The interop member names it uses are listed in
the README section "Verify on first build"; optional-parameter arities can differ by release, so expect to fix a
handful of call signatures. The `Inv = Inventor` alias is deliberate (see the developer guide); do not replace it
with `using Inventor;`.

---

## 4. Install the AutoCAD Electrical plugins

Two ways. Use NETLOAD to try a plugin; use a bundle to make it load automatically for everyone.

### 4.1 Quick: NETLOAD

**All five at once** (from the NETLOAD-READY zip extracted to `C:\LSE-Plugins-NETLOAD`, section 3.0):

1. Start AutoCAD Electrical 2026 and drag `C:\LSE-Plugins-NETLOAD\LSE-NETLOAD-ALL.lsp` into the drawing area (or `APPLOAD` it).
   The command line shows one `NETLOAD` line per plugin and then `LSE plugins: 5 of 5 NETLOADed`.
2. If the security dialog appears, choose **Always Load** (it adds the folder to `TRUSTEDPATHS`); or add `C:\LSE-Plugins-NETLOAD\...`
   to Options > Files > Trusted Locations first and no dialog appears.
3. Type `LSELOAD` in any later session to load them again, or add the `.lsp` to the APPLOAD **Startup Suite** so it runs at start-up.
   Extracted somewhere other than `C:\`? Edit the one line `(setq *lse-netload-root* ...)` at the top of the `.lsp`.
   `LSE-NETLOAD-ALL.scr` does the same through the `SCRIPT` command (fixed paths).
4. Type each plugin's `...SETTINGS` command, then its `...DIAG` command (section 5).

**One plugin by hand** (NETLOAD-READY folder or your own `bin\Release\` output):

1. Keep the DLL together with its JSON and `standard\` / `profiles\` files (copy the whole folder to a share or the workstation).
2. In AutoCAD Electrical type `NETLOAD`, pick the DLL. If a security dialog appears choose **Load** (or **Always load**).
3. Type the plugin `...SETTINGS` command, then its `...DIAG` command (section 5).

NETLOAD lasts for the session only. Add the DLL folder to `TRUSTEDPATHS` or use a bundle for a permanent load. A loaded DLL cannot
be replaced while AutoCAD Electrical is running: close it completely before copying a newer build over the old one.

### 4.2 Permanent: Autodesk ApplicationPlugins bundle

Each AutoCAD plugin ships a `bundle\<Name>.bundle\PackageContents.xml` (Catalog Tools since r6). The NETLOAD-READY zip carries
all five bundles with `Contents\` already filled, under `LSE-Plugins-NETLOAD\ApplicationPlugins\`: copy them as they are and skip step 2.

1. Copy the `.bundle` folder to `%APPDATA%\Autodesk\ApplicationPlugins\` (per user) or `C:\ProgramData\Autodesk\ApplicationPlugins\` (all users).
2. Create `Contents\` inside it and copy the whole `bin\Release\` output there.
3. Restart AutoCAD Electrical. The commands load on first use (`LoadOnCommandInvocation="True"`).
4. When updating a plugin: a loaded .NET DLL cannot be replaced or unloaded, so close AutoCAD Electrical completely first, then
   replace the files in `Contents\`, then start it again. LSE_Env shows its build tag at the end of every window title (for example
   `[2026-11-12-r6]`) and in `LSEENVDIAG`; an older tag means the previous DLL is still loaded.

Bundle folder names: `LSE.CatalogTools.bundle`, `LSE.ProjectTools.bundle`, `LSE_Doctor.bundle`, `LSE.Librarian.bundle`, `LSE_Env.bundle`.

### 4.3 Install the Inventor add-in

Copy `LSE.Inventor.FootprintTools.addin` to `%APPDATA%\Autodesk\Inventor 2026\Addins\` (per user) or
`C:\ProgramData\Autodesk\Inventor Addins\` (all users). If the DLL is not in the same folder as the `.addin`, put its
full path in the `<Assembly>` element. Start Inventor; the **LSE** panel appears on the Tools tab.

---

## 5. First run: settings and diagnostics per plugin

Run these once per user, in this order, before using any plugin on real work. Every plugin stores per-user settings
in `%APPDATA%\LSE\<PluginFolder>\settings.json` and reads a shareable JSON standard; point everyone at ONE copy of
each standard JSON on the vault or a share so all workstations behave the same.

| Plugin | 1. Settings command | What to set | 2. Diagnostics command | 3. First real test |
|---|---|---|---|---|
| Catalog Tools | `LSECATALOGSETTINGS` | catalog MDB path (auto-discovers `Documents\Acade 2026\AeData\en-US\Catalogs\default_cat.mdb`; the shared one is under `E-CAD .mbd ACCESS FILES`), `CompanyStandards.json` location, Vault server/vault/path (leave blank to skip Vault), LSE part attribute | `LSECATALOGDIAG` | `LSECATALOG` browse; `LSECATALOGADMIN` on a COPY of the MDB |
| Project Tools | `LSEPROJECTSETTINGS` | projects root, drawing template `.dwt`, the LSE base project `.wdp` (`CUSTOM PROJECT TEMPLATE 2.0\E-CAD CUSTOM PROJECT TEMPLATE 2.wdp`), shared `LseProjectTemplates.json`, activation LISP | `LSEPROJECTDIAG` (must show WD_M present and title blocks matched) | `LSENEWPROJECT` into a scratch folder; open the result in Project Manager |
| LSE_Doctor | `LSEDOCTORSETTINGS` | template `.wdp` (pre-filled to the vault path), rules JSON, scan root (Custom Sales Order root), backups to keep, drawing checks | `LSEDOCTORDIAG` (standard must parse: 8 descriptions, 38 options, 75 properties, 11 mappings, 14 labels) | `LSEDOCTOR` on a COPY of one project; read the findings before ticking repairs |
| Librarian | `LSELIBRARYSETTINGS` | library folders and `dat MENU` folder (pre-filled when the vault working folder is at the standard path, otherwise "Read library paths from project" from any LSE `.wdp`), lookup MDB, catalog MDB and the parts catalog workbook (for `LSECATALOGADD`), standard JSON, icon size, default series | `LSELIBRARYDIAG` (both menus parsed, LSE root pages present) | `LSELIBRARYAUDIT` first (read only); then `LSELIBRARIAN` on a copy of the `dat MENU` folder; `LSECATALOGADD` on a COPY of `default_cat.mdb` |
| LSE_Env | `LSEENVSETTINGS` (every LSE_Env dialog has a **Help** button and tooltips) | rules JSON (`LseEnvRules.json`, share one copy), profiles folder (point it at a vault share so every engineer sees the same customers), template `.wdp`, backups to keep, LISP bridges | `LSEENVDIAG` (19 setting-map rows, 5 wire schemes, 3 sample profiles) | **First confirm the setting map**: `LSEENVSNAPSHOT` A, change the wire number format in Project Properties, B, Diff (`docs\OPERATIONS.md` section 3). Then `LSEENV` on a COPY of a project; `LSEENVWIRES` both ways on the copy |
| Footprint Tools (Inventor) | *LSE Settings* button | catalog MDB, footprint lookup MDB, footprint library folder, naming standard JSON, units, layers, Vault; press **Test** | (the **Test** button) | *LSE Footprint* with a STEP file; "Preview next part number" before Create; use a COPY of both MDBs the first time |

---

## 6. Where the data lives

All paths come from `Locations.txt` (included in the all-in-one zip).

| Thing | Path |
|---|---|
| Catalog database | `C:\_Lake_Shore_E_Vault\CAD Standards\AutoCAD\ECAD Standards\E-CAD .mbd ACCESS FILES\AeData\en-US\Catalogs\default_cat.mdb` |
| Footprint lookup | same folder, `footprint_lookup.mdb` |
| Icon menus (`.DAT` + `IMAGES\`) | `C:\_Lake_Shore_E_Vault\CAD Standards\AutoCAD\ECAD Standards\dat MENU` |
| Symbol library | `...\ECAD Standards\Symbol Library\LSE E-Cad Symbols` (and `\NFPA`) |
| Footprint library | `...\ECAD Standards\Footprint Library\LSE E-Cad Footprints` (and `\panel`) |
| Custom project template | `...\ECAD Standards\Project Templates\CUSTOM TEMPLATES\CUSTOM PROJECT TEMPLATE 2.0\E-CAD CUSTOM PROJECT TEMPLATE 2.wdp` |
| Jobs (Doctor scan root) | `C:\_Lake_Shore_E_Vault\Engineering\Custom Sales Order\UL 891 Switchboards\...` |
| Per-user settings | `%APPDATA%\LSE\AutoCADElectricalCatalogTools\`, `...\AutoCADElectricalProjectTools\`, `...\LSE_Doctor\`, `...\AutoCADElectricalLibrarian\`, `...\LSE_Env\`, `...\InventorFootprintTools\` |
| Backups | Catalog adds: `LSE_Catalog_Backups\<stamp>\` next to the MDB. Doctor: `<project>\_LSE_DOCTOR_BACKUP\<stamp>\`. Librarian: `%APPDATA%\LSE\AutoCADElectricalLibrarian\Backups\<stamp>\`. LSE_Env: `<project>\_LSE_ENV_BACKUP\<stamp>\` plus `<project>\LSE_EnvReport.txt` and the applied-profile record `<project>.lseenv.json`. Each has a `TransactionReport.txt` or `LibraryReport.txt`. |

---

## 7. What has NOT been verified (test these first)

Every item below was written from Autodesk help pages, forum answers and inspection of the real LSE files, not from
running inside the host application. Each plugin README repeats its own list.

**All AutoCAD plugins**
- The whole WinForms GUI inside AutoCAD (modal dialogs via `Application.ShowModalDialog`).
- `(c:wd_makeproj_current "<wdp>")` as the project activate/reload function (forum answer). `LSEPROJECTACTIVATE` tests it on any existing project; change the expression in the Settings dialog if your release names it differently.
- `(ace_getactiveproject)` round trip through `Editor.Command` and `USERS1` (Doctor active-project detection). If it fails the dialog opens without a preset project; browse to it.

**Project Tools**
- `.wdp`, `.wdt` and `.wdl` line formats and ordering. First run `LSEPROJECTDIAG`, then create a test project and confirm Project Manager opens it and the title block names in the JSON match the LSE `.dwt`.

**LSE_Doctor**
- `WD_M` insertion via `WblockCloneObjects` + `SaveAs` on a side database. Test on a copy of one drawing before batch use.
- Whether AutoCAD Electrical accepts either `+[n]` / `?[n]` group order (the doctor preserves whatever order the project already has).

**Librarian**
- `LSECATALOGADD`: the `default_cat.mdb` insert, backup and rollback are smoke-tested on a copy; the live catalog and the Vault check-in of the changed MDB are not.
- `Database.Wblock` export of a block from the current drawing, `ThumbnailBitmap` preview capture, icon rendering by curve sampling, and `footprint_lookup.mdb` writes.
- Whether AutoCAD Electrical shows an appended icon-menu entry without a restart.
- The `.DAT` parsing and rewriting is smoke-tested and byte-identical on round trip; the first-build checklist is in `docs\DEVELOPER-GUIDE.md` section 7.

**LSE_Env**
- The setting map in `LseEnvRules.json`: that `?[6]`/`WIREMODE` is the wire-number mode (`S`/`R`), `?[9]`/`WIREFMT` the format, `?[7]` start, `?[10]` increment, `?[8]` suffixes, `?[2..5]` the component-tag block, `?[14]`/`XREFFMT`, `?[0]`/`RUNGHORV`. Inferred from the LSE template's values. The X-Y grid / Format Referencing location is unknown; `LSEENVSNAPSHOT` finds it.
- Whether `WD_M` edits on a side database are enough for AutoCAD Electrical to use the new numbering on the next retag.
- r2: writes to a drawing that is open in the editor (`Document.LockDocument()` + a transaction on `Document.Database`, no save by the plugin). Standard AutoCAD .NET practice, not yet run in the host. If you apply and then close that sheet without saving, the `.wdp` and the project marker keep the change while the drawing does not; the next Plan offers the drawing change again.
- The wire-number block (`WD_WN*`, attribute `WIRENO`, fixed = on the `WIREFIXED` layer, `WD_WN.dwg` importable from the library path), the symbol connection attributes (`X?TERMnn` position, `TERMnn` pin, `TAG1`/`TAG2`/`TAGSTRIP`, `SIGCODE`) and the tracing tolerances. All are rules-file keys.
- `c:wd_wnum` as the retag entry point. LSE_Doctor does not yet read `<project>.lseenv.json`, so it will report the customer's lines as off-standard; list them in the Doctor's `protectedProperties` meanwhile.

**Catalog Tools**
- First compiled in r6 (0 warnings) but, like the others, never loaded in AutoCAD. Run `LSECATALOGDIAG` first; every catalog call is late-bound ADODB and the Vault calls are reflection, neither run in the host yet.

**Footprint Tools (Inventor)**
- Never compiled. Interop member names and arities, STL import through `Documents.Open`, drawing curves from mesh bodies, the AutoCAD Electrical footprint attribute set, `Arc2d.SweepAngle` sign. Full list in the README "Verify on first build".
- Vault checkout/checkin is copied from the Catalog Tools plugin; the footprint DWG and `footprint_lookup.mdb` are not Vault-controlled.

---

## 8. Troubleshooting

| Symptom | Cause / fix |
|---|---|
| `dotnet` not recognised, or `dotnet --list-sdks` prints nothing | Only the runtime is installed. Install the .NET 8 **SDK** x64. |
| Build error `The type or namespace 'Autodesk' could not be found` | `AcadDir` does not point at the folder with `acdbmgd.dll`. Pass `-p:AcadDir=...`. Without AutoCAD on the machine the build needs internet to restore `AutoCAD.NET` 25.1.0 (do not use 25.1.1, it targets net10). |
| NETLOAD: "could not load assembly" or nothing happens | Zip was not unblocked (section 3.0 / 3.1); or the DLL was built for a different AutoCAD major version; or the JSON / `standard\` files are not next to the DLL. `LSELOAD` printing `MISSING` means the root path at the top of `LSE-NETLOAD-ALL.lsp` is not where you extracted the zip. |
| Security dialog on every NETLOAD | Choose **Always Load** once, or add the plugin folder to `TRUSTEDPATHS` (Options > Files > Trusted Locations). |
| `...DIAG` reports `Catalog read: FAIL` for every provider | 64-bit Access Database Engine missing. Install the 2016 x64 redistributable (`/passive` if a 32-bit Office blocks it). |
| `LSEENV` grid shows only grey "note" rows and nothing can be ticked | Nothing is applicable: the message box after Plan lists why (settings not mapped, project already matches, drawings unreadable). Before r2 this also happened when the sheets were open in the editor; r2 reads open sheets. |
| Plugin command "unknown command" | The DLL is not loaded in this session (NETLOAD again) or the bundle `Contents\` folder is empty. |
| Vault errors on admin add / footprint create | Leave "Catalog Vault path" blank to skip Vault entirely, or confirm the three `Autodesk.DataManagement...` DLLs are in the Vault Explorer folder. |
| Doctor or Project Tools says a template is missing | The vault working folder is not at `C:\_Lake_Shore_E_Vault\...`. Point the Settings dialog at the real path; the Doctor falls back to the bundled `standard\` copies (no drawings, so no `WD_M` insertion). |
| Inventor does not show the LSE panel | `.addin` not in an add-in folder, `<Assembly>` path wrong, or the add-in failed to load: Tools > Add-Ins shows the error. |

---

## 9. Maintaining the code

- `STYLE-GUIDE.docx` and `agents\` (Claude Code agent definitions: `lse-csharp-dev`, `lse-code-reviewer`, `lse-catalog-data`, `lse-documenter`, plus one `*-guardian` agent per plugin and the `guardrails\` PreToolUse hook that fences writes to each plugin folder) are included in the all-in-one zip. Copy `agents\*.md` to `.claude\agents\` in the repo (or `~\.claude\agents\`) to use them.
- Each plugin `docs\DEVELOPER-GUIDE.md` has the first-build checklist, conventions, recipes and known gaps. Re-run the `lse-documenter` agent after a change so the docs stay current.
- Regenerate the Inventor naming JSON after the workbook changes: `python -I tools\extract_naming_standard.py "<LSE ELECTRICAL PARTS CATALOG MANAGER.xlsx>" config\LseNamingStandard.json [default_cat.mdb]`.
