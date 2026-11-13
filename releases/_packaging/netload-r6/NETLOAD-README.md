# LSE plugins: NETLOAD-ready binaries (2026-11-12-r6)

This folder holds the five LSE AutoCAD Electrical 2026 plugins already compiled (Release, x64, .NET 8, 0 warnings),
each in its own sub-folder with the JSON and data files it reads at run time. Nothing has to be built.

| Folder | DLL | Commands |
|---|---|---|
| `LSE.AutoCADElectrical.CatalogTools\` | `LSE.AutoCADElectrical.CatalogTools.dll` | `LSECATALOG`, `LSEASSIGNCOMPONENT`, `LSECATALOGADMIN`, `LSECATALOGSETTINGS`, `LSECATALOGDIAG` |
| `LSE.AutoCADElectrical.ProjectTools\` | `LSE.AutoCADElectrical.ProjectTools.dll` + `LseProjectTemplates.json` | `LSENEWPROJECT`, `LSEPROJECTACTIVATE`, `LSEPROJECTSETTINGS`, `LSEPROJECTDIAG` |
| `LSE_Doctor\` | `LSE_Doctor.dll` + `LseDoctorRules.json` + `standard\` | `LSEDOCTOR`, `LSEDOCTORSCAN`, `LSEDOCTORSETTINGS`, `LSEDOCTORDIAG` |
| `LSE.AutoCADElectrical.Librarian\` | `LSE.AutoCADElectrical.Librarian.dll` + `LseLibraryStandard.json` + `LseNamingStandard.json` | `LSELIBRARIAN`, `LSECATALOGADD`, `LSEFOOTPRINTMAP`, `LSELIBRARYAUDIT`, `LSELIBRARYSETTINGS`, `LSELIBRARYDIAG` |
| `LSE_Env\` | `LSE_Env.dll` + `LseEnvRules.json` + `profiles\` | `LSEENV`, `LSEENVWIRES`, `LSEENVPROFILES`, `LSEENVSNAPSHOT`, `LSEENVSETTINGS`, `LSEENVDIAG` |

Keep each sub-folder together: the DLL looks for its JSON and `standard\` / `profiles\` next to itself.
The `.pdb`, `.deps.json` and `.runtimeconfig.json` files are optional (they only improve error reports).

## Load all five in one step

1. Unblock and extract to `C:\` so the folder is `C:\LSE-Plugins-NETLOAD` (any other place works too; see step 4).

   ```powershell
   Unblock-File "$env:USERPROFILE\Downloads\LSE-Plugins-NETLOAD-READY-2026-11-12-r6.zip"
   Expand-Archive "$env:USERPROFILE\Downloads\LSE-Plugins-NETLOAD-READY-2026-11-12-r6.zip" -DestinationPath "C:\"
   Get-ChildItem "C:\LSE-Plugins-NETLOAD" -Recurse | Unblock-File
   ```

   Windows marks downloaded files as "from the internet"; AutoCAD refuses to NETLOAD a blocked DLL.
2. Start AutoCAD Electrical 2026 and drag `LSE-NETLOAD-ALL.lsp` into the drawing area (or `APPLOAD` it).
   It NETLOADs the five DLLs and prints one `NETLOAD` line per plugin, then `5 of 5 NETLOADed`.
3. If the security dialog asks, choose **Always Load** (that adds the folder to `TRUSTEDPATHS`), or add
   `C:\LSE-Plugins-NETLOAD\...` to Options > Files > Trusted Locations beforehand.
4. Extracted somewhere else? Either keep the loader beside the plugin folders and add that folder to the
   support file search path, or edit the one line `(setq *lse-netload-root* "C:\\LSE-Plugins-NETLOAD")` at the top of the `.lsp`.
5. The command `LSELOAD` (defined by the loader) repeats the load in any session. For every session automatically,
   add `LSE-NETLOAD-ALL.lsp` to the APPLOAD **Startup Suite**, or use the bundles below.

`LSE-NETLOAD-ALL.scr` does the same through the `SCRIPT` command (paths fixed to `C:\LSE-Plugins-NETLOAD`).

### One plugin by hand

`NETLOAD`, browse to the DLL in its sub-folder, **Load**. Then type the plugin's `...SETTINGS` command, then `...DIAG`.
NETLOAD lasts for the session only.

## Permanent: ApplicationPlugins bundles

`ApplicationPlugins\` holds the five `*.bundle` folders with `PackageContents.xml` and a filled `Contents\` folder.
Copy the bundles you want into `%APPDATA%\Autodesk\ApplicationPlugins\` (per user) or
`C:\ProgramData\Autodesk\ApplicationPlugins\` (all users) and restart AutoCAD Electrical; the commands load on first use.

## Status

Built on 2026-11-12 on a machine without AutoCAD, against Autodesk's `AutoCAD.NET` 25.1.0 reference package (the AutoCAD 2026 API).
Every DLL references only `acmgd` / `acdbmgd` / `accoremgd` 25.1 and the .NET 8 shared framework that AutoCAD 2026 itself runs on;
database access is late-bound ADODB (needs the 64-bit Access Database Engine). None of the five has yet been loaded in a real
AutoCAD Electrical session: run the `...DIAG` commands first and read section 7 of `LSE-Plugins-BUILD-AND-INSTALL-GUIDE.md`.
Source and build instructions are in the per-plugin zips and the guide.
