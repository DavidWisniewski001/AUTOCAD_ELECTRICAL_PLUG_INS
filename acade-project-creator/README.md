# LSE Project Tools (AutoCAD Electrical 2026 plugin, Lake Shore Electric)

Creates a complete Lake Shore Electric job in AutoCAD Electrical from one wizard: the job folder in the right place on
the vault (`Custom Sales Order\UL 891 Switchboards\Eaton\Fixed IC Breakers\2500A\001096-1_AEC Contractors`), every
`001096-1-Ennn.dwg` sheet from the LSE template drawings with the `ECAD_LSE_CUSTOM_TB_2.0` title block and `WD_M`
filled in, the `001096-1.wdp` with the eight LSE description lines and the template's library, icon-menu and property
settings, the template's own `.wdt` and `_wdtitle.wdl` renamed for the job, the `.aepx`, and then activates the project
in the Project Manager.

Everything it writes mirrors the live LSE standard, **CUSTOM PROJECT TEMPLATE 2.0** on the vault
(`C:\_Lake_Shore_E_Vault\CAD Standards\AutoCAD\ECAD Standards\Project Templates\CUSTOM TEMPLATES\CUSTOM PROJECT TEMPLATE 2.0\`):
the `.wdp` written by AutoCAD Electrical 2026 there is the base project, `DRAWING-#.dwg` is the sheet template,
`STOCKCODE-E001.dwg` the cover, and its `.wdt` / `_wdtitle.wdl` / `.aepx` are the sidecar files. The repair-shop
counterpart is `../acade-project-doctor` (LSE E-CAD Doctor), which checks existing jobs against the same template.

It follows the shape of the other LSE plugins: settings in `%APPDATA%\LSE\AutoCADElectricalProjectTools\settings.json`,
a shareable JSON standard, a transaction that logs every step and rolls back on failure, WinForms GUI in the LSE code style.

## Documentation

| Document | Read it for |
|---|---|
| this README | overview, the LSE standard it encodes, commands, what gets produced, configuration, build, deploy, activation, known gaps |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | components, dependency diagram, the transaction step by step, drawing creation model, `.wdp` layout, design decisions |
| [docs/CODE-REFERENCE.md](docs/CODE-REFERENCE.md) | every public member of every source file, the wizard's behaviour, the commands |
| [docs/DATA-STORES.md](docs/DATA-STORES.md) | settings.json, the templates JSON field by field, the template folder, everything written into a job folder, failure logs |
| [docs/OPERATIONS.md](docs/OPERATIONS.md) | install, configure, create a job step by step, reading the log, recovery, every error message, project activation |
| [docs/DEVELOPER-GUIDE.md](docs/DEVELOPER-GUIDE.md) | build with or without AutoCAD, debug, conventions, recipes, first-build checklist, test strategy, known gaps |
| [docs/GLOSSARY.md](docs/GLOSSARY.md) | terms |

These documents are maintained with the `lse-documenter` agent definition in `../agents/lse-documenter.md`; the plugin
itself is owned by `../agents/lse-project-tools-guardian.md`.

**Status (2026-11-10):** made LSE-specific (this README describes the result). Compiles in Release with no warnings
against the AutoCAD.NET 25.1.0 reference package; the file-format, naming, validation and vault-path logic passed a
55-check console smoke test that includes a byte-identical round trip of the real template `.wdp` and generated
`.wdl` / `.wdt` files equal to the template's own. The plugin has not yet been loaded into AutoCAD Electrical. Follow
the first-build checklist in the developer guide before creating a real job.

## The LSE standard the plugin encodes

| Item | Value | Source |
|---|---|---|
| Job number | six-digit sales order, dash, release: `001096-1` | existing job folders (`001096-1_AEC Contractors`, `000924-1_SNWRF-SWBD1`) |
| Job folder | `<job>_<customer>` under the product line's vault folder | same |
| Project file | `<job>.wdp`, with `<job>.wdt`, `<job>_wdtitle.wdl`, `<job>.aepx` beside it | template file set |
| Drawings | `<job>-E<nnn>.dwg` (`001096-1-E001.dwg`); cover from `STOCKCODE-E001.dwg`, sheets from `DRAWING-#.dwg` | template drawings, existing `000924-1-E303.dwg` |
| Description lines | 1 PROJECT NAME, 2 QUOTE#, 3 CUSTOMER 1, 4 CUSTOMER 2, 5 CUSTOMER 3, 6 UNIT DESIGNATION, 7 UNIT SPECS (`SWBD, ####A, ###/###VAC, ##KA@###V, N#?`), 8 DWG SCALE (`NTS`); 9 to 14 FUTURE USE | template `.wdp` and `_wdtitle.wdl` |
| Title block | `ECAD_LSE_CUSTOM_TB_2.0`: `DWG_NUMBER = FILENAME`, `DWG_TYPE = DD1`, `PROJECT_NAME..SCALE = LINE1..LINE8` | template `.wdt` |
| Project settings | `+[1..38]` (NFPA and LSE symbol libraries, `ACE_NFPA_MENU.DAT`, panel and LSE footprint libraries, `ACE_PANEL_MENU_NFPA.DAT`, ...) and `?[0..76]` copied from the template | template `.wdp` |
| `.wdp` text | UTF-8 with BOM, CRLF, groups in the order `*[n]`, `+[n]`, `?[n]`, then drawings | template `.wdp` as written by AutoCAD Electrical 2026 |
| Vault folders | `Custom Sales Order\UL 891 Switchboards\{Eaton,ABB}\{Draw out IC, Fixed IC, Molded Case, Bolted Pressure Switch}\{amps}`, `UL1008\{MC,IC}`, `UL 508A Control Panels\Service Pedestal\Eaton Components` | `../reference/Locations.txt`, workstation history |

The E-number series used by the shipped drawing sets (E001 cover, E1xx one-line, E2xx elevations, E3xx schematics,
E4xx metering, E5xx schedules) is inferred from `000924-1-E303.dwg` and must be confirmed against a finished job; edit
`projectTypes` in the JSON once the real series is known.

## Commands

| Command | Purpose |
|---|---|
| `LSENEWPROJECT` | Four-step wizard: Job and descriptions → Vault location and templates → Drawings → Review and create |
| `LSEPROJECTACTIVATE` | Pick an existing `.wdp` and make it the active project (tests the LISP bridge) |
| `LSEPROJECTSETTINGS` | Vault paths, defaults, option switches, activation LISP |
| `LSEPROJECTDIAG` | Runtime, vault working folder, settings, templates JSON, template folder files, template drawing inspection (WD_M present? LSE title block matched?), template project parse |

## The wizard

1. **Job and project descriptions.** LSE job number (validated as `NNNNNN-R`), product line (UL 891 Switchboard, UL
   1008 Transfer Switch, UL 508A Control Panel, Other), drawing set, the eight description lines with the template's
   placeholders, a unit-specs builder (Type, Amps, Voltage, kAIC, NEMA → `SWBD, 2500A, 277/480VAC, 65KA@480V, N1` into
   line 7), optional IEC codes for `WD_M`.
2. **Vault location and templates.** Jobs root (`Custom Sales Order`), the product line's folder pickers (Manufacturer
   → Breaker type → Amperage, editable), job folder name (`{JOB}_{CUSTOMER}`), project name (`{JOB}`), the resulting
   `.wdp` path, the template project and drawing template, and the option switches.
3. **Drawing list.** Sheets pre-filled from the drawing set with their E-numbers; file names follow `{JOB}-E{SHEET:3}`
   until edited; add, remove, reorder, regenerate names, renumber `001..n`; per-row template (`STOCKCODE-E001.dwg` for
   the cover).
4. **Review and create.** Summary, validation result, then the live log.

## What the wizard produces

```
<Jobs root>\<Product line>\<Manufacturer>\<Breaker type>\<Amps>\<Job>_<Customer>\
    <Job>.wdp                 *[1..8] description lines; +[1..38] options and ?[0..76] properties copied from the template; drawings with ===<drawing type>
    <Job>.wdt                 copy of the template's .wdt (BLOCK = ECAD_LSE_CUSTOM_TB_2.0, DWG_NUMBER = FILENAME, DWG_TYPE = DD1, ... = LINE1..8)
    <Job>_wdtitle.wdl         copy of the template's labels (LINE1=PROJECT NAME ... LINE14=FUTURE USE)
    <Job>.aepx                project configuration in the template's shape (one <Drawing> per sheet)
    <Job>-E001.dwg            from STOCKCODE-E001.dwg; <Job>-E002.dwg ... from DRAWING-#.dwg; title block and WD_M SHEET / IEC set
    LSE_ProjectReport.txt     line-per-step log of the run
```

When the template folder has no `.wdt` or `.wdl` (or "Copy the template's own .wdt / .wdl" is off) both are generated
from the JSON in the same format, and the smoke test shows the generated files equal the template's.

## Configuration: `config\LseProjectTemplates.json`

Point `LSEPROJECTSETTINGS > Project templates JSON` at a shared copy on the vault so everyone creates identical jobs.
Every section is explained in the file header; in short:

- `vault` – standards root, jobs root, the template project `.wdp`, the drawing and cover template file names, the symbol library. Settings default from here (remapped to whichever drive holds `_Lake_Shore_E_Vault`).
- `job` – the job number pattern, example and hint; the folder, project and file name patterns (`{JOB}`, `{CUSTOMER}`, `{QUOTE}`, `{SHEET:3}`, `{DATE}`, `{USER}`, `{YEAR}`).
- `descriptionLines` – the eight template lines: label, title block attribute, role (`title`, `quote`, `customer`, `designation`, `specs`, `scale`, `date`, `user`), placeholder, default, required.
- `labelFile.extraLabels` – LINE9..14 for a generated `.wdl`.
- `titleBlock` – `ECAD_LSE_CUSTOM_TB_2.0`, `DWG_NUMBER` for the file name, `DWG_TYPE` for drawing description 1.
- `wdm` – WD_M attribute tags.
- `unitSpecs` – the choice lists and pattern of the line-7 builder.
- `productLines` – the vault folder tree (levels and children), the unit type and the drawing set each product line pre-selects.
- `projectTypes` – the drawing sets with E-numbers, drawing types and per-row templates.

If the title block is not found, run `LSEPROJECTDIAG`: it lists every block in the template's layouts that matched the
configured name, with its attribute tags, so the JSON can be corrected.

## Build

- Target `net8.0-windows`, WinForms, x64, nullable. No NuGet packages at runtime.
- On a workstation with AutoCAD Electrical 2026: `dotnet build -c Release` (references `accoremgd/acdbmgd/acmgd` from `C:\Program Files\Autodesk\AutoCAD 2026`, or pass `-p:AcadDir=...`).
- Without an install the project compiles against the `AutoCAD.NET` 25.1.0 reference package (compile-time only).

## Deploy / load

1. Build Release; copy the output folder (DLL + `LseProjectTemplates.json`) to the vault or each workstation.
2. `NETLOAD` → `LSE.AutoCADElectrical.ProjectTools.dll`, or copy `bundle\LSE.ProjectTools.bundle` to `%APPDATA%\Autodesk\ApplicationPlugins\` with the build output in `Contents\` for automatic loading.
3. Run `LSEPROJECTDIAG`. On a workstation with the vault working folder at `C:\_Lake_Shore_E_Vault` every path is pre-filled: jobs root `Custom Sales Order`, template project `E-CAD CUSTOM PROJECT TEMPLATE 2.wdp`, drawing template `DRAWING-#.dwg`, the NFPA symbol library. Fix anything marked MISSING or FAIL in `LSEPROJECTSETTINGS`.
4. Create a test job into a scratch jobs root first (change **Jobs root** for that run) and open it in the Project Manager.

## Project activation

After the wizard closes the plugin sends the LISP in `Activate project LISP` to the command line, default
`(c:wd_makeproj_current "<wdp>")`, guarded by `boundp` so a missing function only prints a message. If your release
names the function differently, change the expression in `LSEPROJECTSETTINGS`; `LSEPROJECTACTIVATE` lets you test it
on any existing project. `(ace_getactiveproject)` at the command line shows which project is active.

## Known gaps / next refinements

- Nothing has run inside AutoCAD Electrical: drawing creation from `DRAWING-#.dwg` / `STOCKCODE-E001.dwg`, title block and `WD_M` filling, and activation are unverified.
- The E-number series of the drawing sets is inferred, not confirmed. The value written to `WD_M` `SHEET` for an E-numbered sheet (`1` or `001` or `E001`) needs confirming against an existing job.
- `wd_makeproj_current` comes from an Autodesk forum answer, not the official API reference; confirm on one workstation.
- The product-line folder tree is a snapshot of `Locations.txt` from 2026-11-10; new amperages or manufacturers can be typed in the wizard, but the JSON should be updated when the vault tree changes.
- The `.aepx` is written in the template's shape but AutoCAD Electrical's acceptance of a pre-written one is unverified (it rewrites the file itself on open).
- No Vault check-in: the new job folder is created in the working folder and must be checked in by hand.
- Drawing properties beyond `WD_M` attributes are not touched; the template drawings define them.
