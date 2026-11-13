# LSE Plugins: Build and Install Guide

Release date: 2026-11-10. Package: `LSE-Plugins-2026-11-10` on Google Drive, folder "LSE AutoCAD Electrical Agents".

This guide is the single starting point. Every plugin also ships its own `README.md` and a `docs\` set (ARCHITECTURE,
CODE-REFERENCE, DATA-STORES, OPERATIONS, DEVELOPER-GUIDE, GLOSSARY). Read this guide first, then the plugin README,
then `docs\OPERATIONS.md` when you use the tool and `docs\DEVELOPER-GUIDE.md` when you change it.

**Important:** all five plugins were written on a machine with no AutoCAD, no Inventor and no .NET SDK install.
Three compile cleanly against the Autodesk reference package and passed console smoke tests of their file logic,
two have never been compiled, and none has ever been loaded into AutoCAD Electrical or Inventor. Section 7 lists
exactly what is unverified for each. Try everything on copies before touching a live job, the live catalog or the
Vault working folder.

---

## 1. What is in the package

| Zip | Host | Folder inside the zip | Commands / entry point | Status at packaging |
|---|---|---|---|---|
| `LSE.AutoCADElectrical.CatalogTools-2026-11-10.zip` | AutoCAD Electrical 2026 | `acade-catalog-tools\` | `LSECATALOG`, `LSEASSIGNCOMPONENT`, `LSECATALOGADMIN`, `LSECATALOGSETTINGS`, `LSECATALOGDIAG` | Source only (10 files). The `.csproj` was added at packaging time; it has not been compiled. |
| `LSE.AutoCADElectrical.ProjectTools-2026-11-10.zip` | AutoCAD Electrical 2026 | `acade-project-creator\` | `LSENEWPROJECT`, `LSEPROJECTACTIVATE`, `LSEPROJECTSETTINGS`, `LSEPROJECTDIAG` | Compiles Release, 0 warnings. 40-check console smoke test passed. Not loaded in AutoCAD. |
| `LSE_Doctor-2026-11-10.zip` | AutoCAD Electrical 2026 | `acade-project-doctor\` | `LSEDOCTOR`, `LSEDOCTORSCAN`, `LSEDOCTORSETTINGS`, `LSEDOCTORDIAG` | Compiles Release, 0 warnings. 64-check smoke test against the real template passed. Not loaded in AutoCAD. |
| `LSE.AutoCADElectrical.Librarian-2026-11-10.zip` | AutoCAD Electrical 2026 | `acade-librarian\` | `LSELIBRARIAN`, `LSELIBRARYAUDIT`, `LSELIBRARYSETTINGS`, `LSELIBRARYDIAG` | Compiles Release, 0 warnings. 66-check smoke test against copies of the real `.DAT` menus passed. Not loaded in AutoCAD. |
| `LSE.Inventor.FootprintTools-2026-11-10.zip` | Autodesk Inventor 2026 | `inventor-addin\` | Ribbon: Tools tab, **LSE** panel: *LSE Footprint*, *LSE Settings* | Source only. Never compiled (needs the Inventor interop assembly). |
| `LSE-Plugins-ALL-2026-11-10.zip` | all of the above | one folder per plugin plus `agents\`, `README.docx`, `STYLE-GUIDE.docx`, `Locations.txt` | | The same content as the five zips, in one download. |

What each plugin does, in one line:

- **Catalog Tools**: company-curated view of `default_cat.mdb`, assign a catalog record to a selected block, admin add with backup, Vault, verify and rollback.
- **Project Tools**: wizard that creates a complete AutoCAD Electrical project (folder, drawings from the `.dwt`, `.wdp`, `.wdt`, `_WDTITLE.WDL`) and activates it.
- **LSE_Doctor**: examines an existing project against the live LSE custom template and repairs description lines, library and menu paths, options, properties, `.wdt`, `.wdl`, `.aepx` and optionally, per drawing, the `WD_M` block, its wire-layer settings and the wire layers themselves (missing ones created from the template drawing, off-standard ones repaired, unused non-standard ones purged). Batch scan of a folder tree.
- **Librarian**: files a new symbol, footprint, nameplate or user circuit into the right LSE library folder under a standard name, adds the icon-menu entry and icon image, registers footprints in `footprint_lookup.mdb`. Audit command finds misfiled, misnamed and orphaned items.
- **Footprint Tools (Inventor)**: import STL/STEP, pick a view, project it to a footprint DXF/DWG, issue a 14-character LSE part number, insert the catalog row and register the footprint.

---

## 2. Workstation prerequisites

| Need | For | Notes |
|---|---|---|
| AutoCAD Electrical 2026 (x64) | the four AutoCAD plugins | The managed assemblies `accoremgd.dll`, `acdbmgd.dll`, `acmgd.dll` are expected in `C:\Program Files\Autodesk\AutoCAD 2026`. If your install is elsewhere pass `-p:AcadDir="<folder>"` to `dotnet build`. |
| Autodesk Inventor 2026 (x64) | the Inventor add-in | `Autodesk.Inventor.Interop.dll` is expected in `C:\Program Files\Autodesk\Inventor 2026\Bin\Public Assemblies`. Override with `-p:InventorBin="<...\Inventor 2026\Bin>"`. |
| .NET 8 SDK (x64) | building everything | Check with `dotnet --list-sdks`. If nothing is listed install the SDK from https://dotnet.microsoft.com/download/dotnet/8.0 (the "SDK", not the runtime). Visual Studio 2022 17.8 or later with the ".NET desktop development" workload also works; open the `.csproj` directly. |
| 64-bit Microsoft Access Database Engine (ACE 16 or 12) | Catalog Tools, Footprint Tools, Librarian footprint lookup | Must match AutoCAD bitness (64-bit). The `...DIAG` commands report "Catalog read: FAIL" for all providers when it is missing. |
| Vault Explorer (optional) | Catalog Tools admin adds, Footprint Tools catalog check-in | Only when the "Catalog Vault path" setting is filled in. The Vault SDK DLLs are loaded by reflection from the Vault Explorer folder; nothing is referenced at compile time. |
| Write access to the Vault working folder | Librarian, Doctor, Project Tools | `C:\_Lake_Shore_E_Vault\CAD Standards\AutoCAD\ECAD Standards\...` as listed in `Locations.txt`. |
| Internet (first build only, and only if AutoCAD is NOT installed on the build machine) | restoring the `AutoCAD.NET` 25.1.0 NuGet reference package | On a workstation with AutoCAD 2026 installed the `.csproj` uses the installed DLLs and needs no package. |

---

## 3. Build

### 3.1 Unblock and extract

Windows marks downloaded zips as "from the internet". AutoCAD can refuse to NETLOAD a DLL built from blocked files.
Unblock the zip before extracting (right-click, Properties, **Unblock**, or in PowerShell):

```powershell
Unblock-File "C:\Downloads\LSE-Plugins-ALL-2026-11-10.zip"
Expand-Archive "C:\Downloads\LSE-Plugins-ALL-2026-11-10.zip" -DestinationPath "C:\LSE-Plugins"
```

### 3.2 Build the four AutoCAD Electrical plugins

Each plugin is a self-contained SDK-style project: `net8.0-windows`, WinForms, x64, nullable, no runtime NuGet
packages. From a Developer PowerShell or any PowerShell with `dotnet` on the path:

```powershell
cd C:\LSE-Plugins
dotnet build "acade-catalog-tools\LSE.AutoCADElectrical.CatalogTools.csproj"    -c Release
dotnet build "acade-project-creator\LSE.AutoCADElectrical.ProjectTools.csproj"   -c Release
dotnet build "acade-project-doctor\LSE_Doctor.csproj"   -c Release
dotnet build "acade-librarian\LSE.AutoCADElectrical.Librarian.csproj"            -c Release
```

The output lands in `<plugin>\bin\Release\` (the target-framework subfolder is turned off). Expected contents:

| Plugin | Output folder must contain |
|---|---|
| Catalog Tools | `LSE.AutoCADElectrical.CatalogTools.dll` |
| Project Tools | `LSE.AutoCADElectrical.ProjectTools.dll`, `LseProjectTemplates.json` |
| LSE_Doctor | `LSE_Doctor.dll`, `LseDoctorRules.json`, `standard\` (template `.wdp`, `.wdt`, `.wdl` fallback copies) |
| Librarian | `LSE.AutoCADElectrical.Librarian.dll`, `LseLibraryStandard.json` |

If AutoCAD 2026 is installed somewhere other than `C:\Program Files\Autodesk\AutoCAD 2026`:

```powershell
dotnet build "acade-project-doctor\LSE_Doctor.csproj" -c Release -p:AcadDir="D:\Autodesk\AutoCAD 2026"
```

Expect a clean build with 0 warnings for Project Tools, Doctor and Librarian (they were built that way against the
reference package). Catalog Tools has never been built; it follows the same conventions and the same `.csproj` shape,
so expect at most minor fixes. If a build fails with a missing-namespace error, confirm `AcadDir` points at the folder
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

1. Copy the `bin\Release\` folder of the plugin to a share or the workstation (keep the JSON and `standard\` next to the DLL).
2. In AutoCAD Electrical type `NETLOAD`, pick the DLL. If a security dialog appears choose **Load** (or **Always load**).
3. Type the plugin `...SETTINGS` command, then its `...DIAG` command (section 5).

NETLOAD lasts for the session only. Add the DLL folder to `TRUSTEDPATHS` or use a bundle for a permanent load.

### 4.2 Permanent: Autodesk ApplicationPlugins bundle

Each AutoCAD plugin ships a `bundle\<Name>.bundle\PackageContents.xml` (Catalog Tools does not; copy one of the
others and change the names and commands, or use an `acad.lsp` entry).

1. Copy the `.bundle` folder to `%APPDATA%\Autodesk\ApplicationPlugins\` (per user) or `C:\ProgramData\Autodesk\ApplicationPlugins\` (all users).
2. Create `Contents\` inside it and copy the whole `bin\Release\` output there.
3. Restart AutoCAD Electrical. The commands load on first use (`LoadOnCommandInvocation="True"`).

Bundle folder names: `LSE.ProjectTools.bundle`, `LSE_Doctor.bundle`, `LSE.Librarian.bundle`.

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
| Librarian | `LSELIBRARYSETTINGS` | library folders and `dat MENU` folder (pre-filled when the vault working folder is at the standard path, otherwise "Read library paths from project" from any LSE `.wdp`), lookup MDB, standard JSON, icon size, default series | `LSELIBRARYDIAG` (both menus parsed, LSE root pages present) | `LSELIBRARYAUDIT` first (read only); then `LSELIBRARIAN` on a copy of the `dat MENU` folder |
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
| Per-user settings | `%APPDATA%\LSE\AutoCADElectricalCatalogTools\`, `...\AutoCADElectricalProjectTools\`, `...\LSE_Doctor\`, `...\AutoCADElectricalLibrarian\`, `...\InventorFootprintTools\` |
| Backups | Catalog adds: `LSE_Catalog_Backups\<stamp>\` next to the MDB. Doctor: `<project>\_LSE_DOCTOR_BACKUP\<stamp>\`. Librarian: `%APPDATA%\LSE\AutoCADElectricalLibrarian\Backups\<stamp>\`. Each has a `TransactionReport.txt` or `LibraryReport.txt`. |

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
- `Database.Wblock` export of a block from the current drawing, `ThumbnailBitmap` preview capture, icon rendering by curve sampling, and `footprint_lookup.mdb` writes.
- Whether AutoCAD Electrical shows an appended icon-menu entry without a restart.
- The `.DAT` parsing and rewriting is smoke-tested and byte-identical on round trip; the first-build checklist is in `docs\DEVELOPER-GUIDE.md` section 7.

**Catalog Tools**
- Never compiled. Build first, then `LSECATALOGDIAG`.

**Footprint Tools (Inventor)**
- Never compiled. Interop member names and arities, STL import through `Documents.Open`, drawing curves from mesh bodies, the AutoCAD Electrical footprint attribute set, `Arc2d.SweepAngle` sign. Full list in the README "Verify on first build".
- Vault checkout/checkin is copied from the Catalog Tools plugin; the footprint DWG and `footprint_lookup.mdb` are not Vault-controlled.

---

## 8. Troubleshooting

| Symptom | Cause / fix |
|---|---|
| `dotnet` not recognised, or `dotnet --list-sdks` prints nothing | Only the runtime is installed. Install the .NET 8 **SDK** x64. |
| Build error `The type or namespace 'Autodesk' could not be found` | `AcadDir` does not point at the folder with `acdbmgd.dll`. Pass `-p:AcadDir=...`. Without AutoCAD on the machine the build needs internet to restore `AutoCAD.NET` 25.1.0 (do not use 25.1.1, it targets net10). |
| NETLOAD: "could not load assembly" or nothing happens | Zip was not unblocked (section 3.1); or the DLL was built for a different AutoCAD major version; or the JSON / `standard\` files are not next to the DLL. |
| `...DIAG` reports `Catalog read: FAIL` for every provider | 64-bit Access Database Engine missing. Install the 2016 x64 redistributable (`/passive` if a 32-bit Office blocks it). |
| Plugin command "unknown command" | The DLL is not loaded in this session (NETLOAD again) or the bundle `Contents\` folder is empty. |
| Vault errors on admin add / footprint create | Leave "Catalog Vault path" blank to skip Vault entirely, or confirm the three `Autodesk.DataManagement...` DLLs are in the Vault Explorer folder. |
| Doctor or Project Tools says a template is missing | The vault working folder is not at `C:\_Lake_Shore_E_Vault\...`. Point the Settings dialog at the real path; the Doctor falls back to the bundled `standard\` copies (no drawings, so no `WD_M` insertion). |
| Inventor does not show the LSE panel | `.addin` not in an add-in folder, `<Assembly>` path wrong, or the add-in failed to load: Tools > Add-Ins shows the error. |

---

## 9. Maintaining the code

- `STYLE-GUIDE.docx` and `agents\` (Claude Code agent definitions: `lse-csharp-dev`, `lse-code-reviewer`, `lse-catalog-data`, `lse-documenter`) are included in the all-in-one zip. Copy `agents\*.md` to `.claude\agents\` in the repo (or `~\.claude\agents\`) to use them.
- Each plugin `docs\DEVELOPER-GUIDE.md` has the first-build checklist, conventions, recipes and known gaps. Re-run the `lse-documenter` agent after a change so the docs stay current.
- Regenerate the Inventor naming JSON after the workbook changes: `python -I tools\extract_naming_standard.py "<LSE ELECTRICAL PARTS CATALOG MANAGER.xlsx>" config\LseNamingStandard.json [default_cat.mdb]`.
