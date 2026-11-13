# LSE AutoCAD Electrical Agents: Folder Guide

As of 2026-11-10. A live copy of this guide is the Claude Doc "LSE AutoCAD Electrical Agents: Folder Guide".

## Purpose

This folder is the source workspace for seven Lake Shore Electric (LSE) plugins that automate AutoCAD Electrical 2026 and Inventor 2026 work, plus the Claude Code agent definitions and reference material used to write them. Nothing here is compiled or installed; this PC has no AutoCAD, Inventor or .NET SDK. Code is written here, packaged to Google Drive, and built and tested on the engineering workstation.

It started on 2026-11-08 with the Catalog Tools plugin source and its agents. Over 2026-11-08 to 2026-11-10 four more plugins were added by parallel Claude sessions (Inventor Footprint Tools, Project Tools, LSE_Doctor, Librarian), each with a full documentation set. Copies of the LSE vault standards (custom project template, icon menus, user profile, location list) were pulled in as inputs. On 2026-11-10 everything was packaged into a dated release on Drive and the folder was reorganized into the layout below.

## Folder map

Plugin folders stay at the root because their docs cross-link with `..\` paths.

| Item | What it is | Size |
|---|---|---|
| `README.md` | This guide | 18 KB |
| `README.docx`, `STYLE-GUIDE.docx` | The original 2026-11-08 folder README (Catalog Tools only, now superseded) and the C# style guide every plugin follows | 0.4 MB |
| `agents\` | Twelve Claude Code agent definitions: seven per-plugin guardians (`lse-*-guardian`) with the `guardrails\` hook that fences them in, the `lse-orchestrator`, plus the four helpers `lse-csharp-dev`, `lse-code-reviewer`, `lse-catalog-data`, `lse-documenter` | 105 KB |
| `.claude\agents\` | Installed copies of the seven guardians and the orchestrator, so Claude Code in this folder picks them up | 70 KB |
| `acade-catalog-tools\` | LSE Catalog Tools plugin source (renamed from `src\`) | 76 KB |
| `acade-project-creator\` | LSE Project Tools plugin | 229 KB |
| `acade-project-doctor\` | LSE_Doctor plugin | 290 KB |
| `acade-project-env\` | LSE_Env plugin (added 2026-11-11) | 360 KB |
| `acade-librarian\` | LSE Librarian plugin | 301 KB |
| `acade-wire-tofrom\` | LSE_ToFrom plugin (added 2026-11-12 from the work scope `Agent Brief True To From Wire Numbers in AutoCAD Electrical 2026.docx`) | 224 KB |
| `inventor-addin\` | LSE Footprint Tools add-in for Inventor | 272 KB |
| `reference\` | LSE standards copied from the vault: `CUSTOM PROJECT TEMPLATE 2.0\`, `dat MENU\`, `LSE-ECAD USER PROFILE.arg`, `Locations.txt`, and `vault-zips\` with the two original zips | 125 MB |
| `releases\` | `LSE-Plugins-2026-11-12-r5\` (current: the seven zips and the build guide, same as Drive), `LSE-Plugins-2026-11-12-r4\`, `LSE-Plugins-2026-11-12-r3\`, `LSE-Plugins-2026-11-12-r2\`, `LSE-Plugins-2026-11-12\`, `LSE-Plugins-2026-11-10-r2\` and `LSE-Plugins-2026-11-10\` (superseded cuts) and `archive\` (earlier per-plugin zips) | 4.7 MB |
| `AutoCAD_Electrical_2026\` | A copy of `Documents\AutoCAD_Electrical_2026` made 2026-11-10 07:20: the LSE parts catalog workbook, `Catalogs\default_cat.mdb`, `footprint_lookup.mdb`, import reports, and 21 dated 615 MB backups of `default_cat.mdb`. Left where it was found. | 13 GB |

## The seven plugins

Each plugin folder is self-contained: a `.csproj`, `src\`, `config\` (the shareable JSON standard), `bundle\` or `.addin` (install manifest), `README.md` and `docs\` (ARCHITECTURE, CODE-REFERENCE, DATA-STORES, OPERATIONS, DEVELOPER-GUIDE, GLOSSARY). All target .NET 8, x64, WinForms, no runtime NuGet packages, and store per-user settings in `%APPDATA%\LSE\<plugin>\settings.json`.

### Plugin index

Each plugin's README is the entry point; its `docs\` folder holds the full set (ARCHITECTURE, CODE-REFERENCE, DATA-STORES, OPERATIONS, DEVELOPER-GUIDE, GLOSSARY), last reviewed against the source on 2026-11-13.

| Plugin | One line | Docs |
|---|---|---|
| LSE Catalog Tools (`acade-catalog-tools\`) | Curated browser for `default_cat.mdb` (standards, favorites, recent), assigns a catalog part's values to a component block, and adds catalog rows under an LSE part number in one backed-up, verified transaction with optional Vault check-out and check-in. | [README](acade-catalog-tools/README.md), [docs](acade-catalog-tools/docs/OPERATIONS.md) |
| LSE Librarian (`acade-librarian\`) | Files new symbols, footprints, nameplates and user circuits under standard names with icon-menu entries, adds `default_cat.mdb` records with new LSE part numbers from the parts catalog workbook, maps parts to footprints and symbols, and audits the libraries. | [README](acade-librarian/README.md), [docs](acade-librarian/docs/OPERATIONS.md) |
| LSE Project Tools (`acade-project-creator\`) | Wizard (`LSENEWPROJECT`) that creates a complete LSE job from CUSTOM PROJECT TEMPLATE 2.0: job folder, `<job>-Ennn.dwg` sheets, `.wdp`, `.wdt`, `_wdtitle.wdl`, `.aepx`, then activates the project. | [README](acade-project-creator/README.md), [docs](acade-project-creator/docs/OPERATIONS.md) |
| LSE_Doctor (`acade-project-doctor\`) | Compares an existing project with the live LSE template (`.wdp`, `.wdt`, `.wdl`, `.aepx`, `WD_M`, wire layers) and repairs the ticked findings with backup, verify and rollback; `LSEDOCTORSCAN` does the same across a jobs folder. | [README](acade-project-doctor/README.md), [docs](acade-project-doctor/docs/OPERATIONS.md) |
| LSE_Env (`acade-project-env\`) | Applies a customer profile (wire numbering, tagging, cross-reference, ladder) to a project's `.wdp` and every drawing's `WD_M`, and switches wire-number schemes including traced From/To numbers. | [README](acade-project-env/README.md), [docs](acade-project-env/docs/OPERATIONS.md) |
| LSE_ToFrom (`acade-wire-tofrom\`) | Places a true per-wire To/From identifier (`K1:13/TB2:1`) as a re-runnable label block on layer `LSE_TOFROM` from AutoCAD Electrical's Wire From/To data, and exports label-printer, report and validation files; native wire numbers untouched. | [README](acade-wire-tofrom/README.md), [docs](acade-wire-tofrom/docs/OPERATIONS.md) |
| LSE Footprint Tools (`inventor-addin\`) | Inventor 2026 add-in (Tools tab, LSE panel): import STL/STEP, project a view to a footprint DXF/DWG, issue a 14-character LSE part number, insert the catalog row (optional Vault check-out/check-in) and register the footprint. | [README](inventor-addin/README.md), [docs](inventor-addin/docs/OPERATIONS.md) |

### Status detail

| Folder | Plugin | Host | Commands | What it does | Status 2026-11-10 |
|---|---|---|---|---|---|
| `acade-catalog-tools\` | LSE Catalog Tools | AutoCAD Electrical 2026 | `LSECATALOG`, `LSEASSIGNCOMPONENT`, `LSECATALOGADMIN`, `LSECATALOGSETTINGS`, `LSECATALOGDIAG` | Curated view of `default_cat.mdb`, assign a catalog record to a block, admin add with backup, Vault, verify, rollback | Source only. `.csproj` added 2026-11-10; bundle manifest and full docs set present (docs added 2026-11-13). Never compiled. |
| `acade-project-creator\` | LSE Project Tools | AutoCAD Electrical 2026 | `LSENEWPROJECT`, `LSEPROJECTACTIVATE`, `LSEPROJECTSETTINGS`, `LSEPROJECTDIAG` | Wizard that creates a full LSE job: folder in the vault's Custom Sales Order tree, `<job>-Ennn.dwg` sheets from the template drawings, `.wdp`, `.wdt`, `_wdtitle.wdl`, `.aepx`, then activates it | LSE-specific since 2026-11-10 (template 2.0, Locations.txt). Compiles, 0 warnings. 55-check smoke test. Not loaded in AutoCAD. |
| `acade-project-doctor\` | LSE_Doctor | AutoCAD Electrical 2026 | `LSEDOCTOR`, `LSEDOCTORSCAN`, `LSEDOCTORSETTINGS`, `LSEDOCTORDIAG` | Diffs an existing project against the live LSE template and repairs descriptions, paths, options, properties, `.wdt`, `.wdl`, `.aepx`, `WD_M`; batch scan of the jobs tree | Compiles, 0 warnings. 64-check smoke test on the real template. Not loaded in AutoCAD. |
| `acade-librarian\` | LSE Librarian | AutoCAD Electrical 2026 | `LSELIBRARIAN`, `LSELIBRARYAUDIT`, `LSECATALOGADD`, `LSELIBRARYSETTINGS`, `LSELIBRARYDIAG` | Files a new symbol, footprint, nameplate or user circuit under a standard name, adds the icon-menu entry and image, registers footprints in `footprint_lookup.mdb`; adds catalog records to `default_cat.mdb` under a new LSE part number whose scheme is read straight from the parts catalog workbook (stand-alone or inside the wizard); audit finds misfiled items | Compiles, 0 warnings. 66-check smoke test on the real `.DAT` menus; 67-check smoke test of the workbook reader, part-number issue and catalog insert/rollback on copies of the real workbook and `default_cat.mdb` (2026-11-10). Not loaded in AutoCAD. |
| `acade-project-env\` | LSE_Env | AutoCAD Electrical 2026 | `LSEENV`, `LSEENVWIRES`, `LSEENVPROFILES`, `LSEENVSNAPSHOT`, `LSEENVSETTINGS`, `LSEENVDIAG` | Applies a customer profile (wire numbering, tagging, cross-reference, ladder settings) to a project's `.wdp` and every drawing's `WD_M`; two-click switching between wire schemes (LSE template, sheet-prefixed, line reference/grid, From/To device); From/To numbers traced from the wires and written as fixed wire numbers, un-fixed on the way back; snapshot/diff tool to locate settings | Added 2026-11-11; first run in AutoCAD 2026-11-12 (dialog works; open-drawing support and grid fixes followed). Compiles, 0 warnings. 89-check smoke test through fake drawing bridges. Help buttons and tooltips in every dialog (r3); three-part error dialog with Error_<stamp>.txt log (r4); plan grid rebuilt without cell replacement and build tag [2026-11-12-r6] in every window title, the error dialog, the log and LSEENVDIAG (r6). First run in AutoCAD Electrical 2026-11-12. Setting map inferred, not verified. |
| `acade-wire-tofrom\` | LSE_ToFrom | AutoCAD Electrical 2026 | `LSETOFROM`, `LSETOFROMEXPORT`, `LSETOFROMVALIDATE`, `LSETOFROMDISCOVER`, `LSETOFROMBLOCKS`, `LSETOFROMSETTINGS`, `LSETOFROMDIAG` | True To/From wire identifiers `FROMTAG:PIN/TOTAG:PIN` (`K1:13/TB2:1`) per physical wire, built from AutoCAD Electrical's own Wire From/To data (project scratch database read-only through ADODB, or a saved report); placed as its own label block (`TOFROM_LBL_H/V`, hidden key = wire handle + end) on layer `LSE_TOFROM`, updated in place on re-run, stale labels removed; label-printer CSV (one row per wire end, reversed at the far end), report CSV matching the native columns, validation report; native wire numbers untouched | Added 2026-11-12. Compiles, 0 warnings. 73-check smoke test through fake drawing bridges. Not loaded in AutoCAD. The scratch-database query is empty until `LSETOFROMDISCOVER` runs on a workstation; the report-file source works meanwhile. |
| `inventor-addin\` | LSE Footprint Tools | Inventor 2026 | Ribbon: Tools tab, LSE panel: *LSE Footprint*, *LSE Settings* | Import STL/STEP, pick a view, project to a footprint DXF/DWG, issue a 14-character LSE part number, insert the catalog row, register the footprint | Source only. Never compiled. |

The three AutoCAD plugins that compile were built with a portable .NET 8 SDK against the `AutoCAD.NET` 25.1.0 reference package. Everything that touches the AutoCAD or Inventor API (dialogs, project activation, WD_M insertion, WBLOCK export, icon rendering, interop calls) is untested until the workstation build.

## Reference material (`reference\`)

| Item | Source on the vault | Used by |
|---|---|---|
| `CUSTOM PROJECT TEMPLATE 2.0\` | `...\ECAD Standards\Project Templates\CUSTOM TEMPLATES\CUSTOM PROJECT TEMPLATE 2.0\` (`.wdp`, `.wdt`, `_wdtitle.wdl`, `.aepx`, `DRAWING-#.dwg`, `STOCKCODE-E001.dwg`) | LSE_Doctor: its `config\standard\` copies and its smoke test. Project Tools: the base project every new job copies settings from. |
| `dat MENU\` | `...\ECAD Standards\dat MENU` (`ACE_NFPA_MENU.DAT`, `ACE_PANEL_MENU_NFPA.DAT`, `Images\`, stock `.slb`/`.dll` menus) | Librarian: its icon-menu parser, naming standard JSON and smoke test. |
| `LSE-ECAD USER PROFILE.arg` | Exported AutoCAD profile (2024-12-30) | Not consumed by any plugin yet; records the workstation's support paths. |
| `Locations.txt` | Written by hand 2026-11-10 | The list of vault paths: job folders, symbol and footprint libraries, template, catalog and lookup MDBs, icon menus. Every plugin's defaults come from it. |
| `vault-zips\` | The two zips the template and `dat MENU` were extracted from | Keep or delete; both are also on Drive. |

## Releases (`releases\`)

`LSE-Plugins-2026-11-12-r5\` is the current test package (fifth cut of 2026-11-12: r5 adds Set symbol to the Librarian's LSEFOOTPRINTMAP, 85-check test; the Inventor add-in ships r4 content because a lock/assembly-code fix is pending in inventor-addin/docs/pending; r4 r4 fixes the LSEENV Plan crash of r3 and adds a three-part error dialog with a log file to LSE_Env, and carries the Librarian's catalog-only LSECATALOGADD mode and new LSEFOOTPRINTMAP; r3 added Help buttons and tooltips to every LSE_Env dialog; r2 made LSE_Env read and write drawings open in the editor and its grid explain itself, after the first in-AutoCAD run; the morning cut added LSE_Env and its guardian; the other five plugins unchanged since 2026-11-10-r2): one zip per plugin, an all-in-one zip, and `LSE-Plugins-BUILD-AND-INSTALL-GUIDE.md`. The same set is on Google Drive in folder "LSE AutoCAD Electrical Agents" > Releases > Current (https://drive.google.com/drive/folders/13Zi47pK_tlivdBP6EKWA6ZnxkjE4o2qM), with the guide also as a Google Doc; Current holds exactly one release and older ones are moved to Releases > Archive (policy in `CLAUDE.md`). `LSE-Plugins-2026-11-10-r2\` (now in Releases > Archive on Drive) superseded `LSE-Plugins-2026-11-10\` (morning cut, now in Releases > Archive on Drive): r2 carries the LSE-specific Project Tools, the renamed LSE_Doctor with wire-layer checks, and the Librarian's `LSECATALOGADD`. `archive\` keeps the earlier per-plugin zips (Project Tools and Inventor from 2026-11-08, the Doctor session's own 2026-11-10 zip).

The build guide covers prerequisites, `dotnet build` lines, NETLOAD and bundle install, the Inventor `.addin`, first-run settings and diagnostics per plugin, and the unverified list. On the workstation: unblock the zip, build the three smoke-tested plugins first, run each `...DIAG` command before anything else.

## Agents and conventions

- Each plugin has one guardian agent, the only agent that should change it: `lse-catalog-tools-guardian` (acade-catalog-tools), `lse-project-tools-guardian` (acade-project-creator), `lse-ecad-doctor-guardian` (acade-project-doctor), `lse-librarian-guardian` (acade-librarian), `lse-env-guardian` (acade-project-env), `lse-tofrom-guardian` (acade-wire-tofrom), `lse-footprint-tools-guardian` (inventor-addin). The prompt carries the plugin's invariants (one write path, transaction order, file formats, style, what to ask before doing). A PreToolUse hook, `agents\guardrails\lse-guard.ps1`, blocks any write outside the plugin folder, writes to read-only areas, destructive shell commands, and write commands that name another plugin, `reference\` or `releases\`. See `agents\guardrails\README.md`; tests run with `python -I agents/guardrails/tests/run-tests.py`. The hooks only fire once the workspace trust dialog for this folder is accepted.
- `agents\lse-csharp-dev.md` writes code in the LSE style (sealed classes, file-scoped namespaces, `dynamic` COM, Vault by reflection, no NuGet, compact services and expanded forms). `lse-code-reviewer.md` checks COM, Vault and catalog safety. `lse-catalog-data.md` is the reference for the MDB schema and settings. `lse-documenter.md` writes and refreshes each plugin's `docs\` set, signed "Maintained by DCW". These four are rulebooks the guardians read; they are not installed in `.claude\agents\`.
- `STYLE-GUIDE.docx` is the written form of those rules.
- Claude's memory for this folder lives in `C:\Users\david\.claude\projects\C--Users-david-Documents-LSE-AutoCAD-Electrical-Agents\memory\` (toolchain, naming schema, plugin status, release location).
- The LSE part number format `CCMMMTTNNNNNNN` is defined in the parts catalog manager workbook. The Librarian reads the workbook directly at run time (`acade-librarian\src\NamingWorkbookService.cs`, no Excel needed) and ships a JSON snapshot `acade-librarian\config\LseNamingStandard.json` as its fallback; `inventor-addin\config\LseNamingStandard.json` is the older snapshot generated with `tools\extract_naming_standard.py`. Legacy ERP numbers with a 7-significant-digit tail (`45BUSCF1633301`) stay reserved but do not move the sequence counter; new numbers continue from the highest zero-padded sequence.

## What changed in the 2026-11-10 cleanup

| Before | After |
|---|---|
| `src\` | `acade-catalog-tools\` (four `../src` mentions in the other plugins' docs updated) |
| `CUSTOM PROJECT TEMPLATE 2.0\CUSTOM PROJECT TEMPLATE 2.0\...` (double folder from the zip) | `reference\CUSTOM PROJECT TEMPLATE 2.0\...` |
| `dat MENU\dat MENU\...` | `reference\dat MENU\...` (Librarian developer guide reference updated) |
| `CUSTOM PROJECT TEMPLATE 2.0.zip`, `dat MENU.zip` | `reference\vault-zips\` |
| `LSE-ECAD USER PROFILE.arg`, `Locations.txt` | `reference\` |
| Three per-plugin zips at the root | `releases\archive\` |
| `LSE-Plugins-BUILD-AND-INSTALL-GUIDE.md` at the root (duplicate) | Removed; the copy in `releases\LSE-Plugins-2026-11-10\` stays |
| `src\` had no project file | `acade-catalog-tools\LSE.AutoCADElectrical.CatalogTools.csproj` added |
| no folder README in text form | this `README.md` |
| four helper agents, no enforcement | five guardian agents in `agents\` and `.claude\agents\` with a PreToolUse hook (`agents\guardrails\`) |

Not touched: `AutoCAD_Electrical_2026\` (13 GB, appeared at 07:20 while this cleanup was running), `README.docx`, `STYLE-GUIDE.docx`, `agents\`, and the five plugin folders' contents.

## Open items

- Build and load every plugin on the workstation; work through section 7 of the build guide and each plugin's first-build checklist.
- `AutoCAD_Electrical_2026\` duplicates 13 GB that also lives in `Documents\AutoCAD_Electrical_2026`; 12.9 GB of it is 21 backups of `default_cat.mdb`. Decide whether this copy should stay in the project folder.
- `wd_makeproj_current` and `ace_getactiveproject` come from forum answers; confirm on one workstation and fix the Settings LISP strings if needed.
- Vault 2026 (2026-11-12-r7): Catalog Tools and Footprint Tools check the catalog out and in through the Vault add-in's session (or the Vault login dialog); Librarian, Project Tools, LSE_Doctor and LSE_Env never talk to Vault but refuse before writing when a target is Vault-locked and list what to check in afterwards. Every `...DIAG` shows the host .NET, the installed Vault client and the add-in login. None of this has run against the live Vault yet; AutoCAD 2026.1.2 and Vault 2026.4 (both .NET 10) must be updated together.
- The meaning of the three-digit series in library names is undocumented; the default is `002`.
