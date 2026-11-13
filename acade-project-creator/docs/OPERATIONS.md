# Operations guide: LSE Project Tools

Maintained by DCW. Last reviewed 2026-11-13.

For the engineer who installs the plugin, configures a workstation, creates projects with it and sorts out problems. Build instructions are in [DEVELOPER-GUIDE.md](DEVELOPER-GUIDE.md). Terms are in [GLOSSARY.md](GLOSSARY.md).

**The plugin compiles but has not yet been loaded into AutoCAD Electrical on any machine.** The first installation is also the first test. Work through section 4 with a scratch jobs root before creating a real job on the vault.

## 1. Workstation requirements

| Requirement | Why | Check |
|---|---|---|
| AutoCAD Electrical 2026 (AutoCAD R25.1, .NET 8) | Host application and the managed API the DLL is built against | `(getvar "ACADVER")` at the command line starts with `25.1`. |
| The vault working folder `C:\_Lake_Shore_E_Vault` (or `_Lake_Shore_E_Vault` at the root of another fixed or network drive) with `CAD Standards\AutoCAD\ECAD Standards` and `Engineering\Custom Sales Order` checked out | Every default path (jobs root, template project, drawing template, symbol library) is taken from it | `LSEPROJECTDIAG` shows `Vault working folder: C:\_Lake_Shore_E_Vault`, not `NOT FOUND`. |
| The LSE template folder `...\Project Templates\CUSTOM TEMPLATES\CUSTOM PROJECT TEMPLATE 2.0\` with `E-CAD CUSTOM PROJECT TEMPLATE 2.wdp`, its `.wdt` and `_wdtitle.wdl`, `DRAWING-#.dwg` and `STOCKCODE-E001.dwg` | The `.wdp` supplies the project settings and the sidecars; the two drawings are what every sheet is created from | `LSEPROJECTDIAG` shows `Template folder file DRAWING-#.dwg: PASS`, `... STOCKCODE-E001.dwg: PASS`, `Template .wdt: PASS`, `Template _wdtitle.wdl: PASS`, `Template WD_M block: PASS` and the `ECAD_LSE_CUSTOM_TB_2.0` title block. |
| Write access to the jobs root (`Custom Sales Order`) | The product line folders and the job folder are created there | Create a folder there by hand. |

## 2. Installation

1. Obtain the Release build output: `LSE.AutoCADElectrical.ProjectTools.dll`, `LseProjectTemplates.json`, and the `.deps.json` / `.runtimeconfig.json` the build produces.
2. Either load it by hand each session with `NETLOAD` and pick the DLL, or copy `bundle\LSE.ProjectTools.bundle` to `%APPDATA%\Autodesk\ApplicationPlugins\` (per user) or `C:\ProgramData\Autodesk\ApplicationPlugins\` (all users) and put the build output inside its `Contents\` folder. The bundle loads the DLL the first time one of its commands is typed.
3. If Windows blocked the DLL after a download, right-click it, Properties, Unblock.
4. Type `LSEPROJECTDIAG`. The report should show the runtime, the DLL path, the settings path, the vault working folder, and no `FAIL` or `MISSING`.

## 3. First-time configuration (per user)

On a workstation with the vault working folder in place nothing needs typing: `SettingsService.Load` reads the `vault` section of the templates JSON and fills every blank path from it. Type `LSEPROJECTSETTINGS` anyway and check every row. Defaults are in [DATA-STORES.md](DATA-STORES.md) section 1.

1. **Jobs root (Custom Sales Order)**: where job folders go, default `C:\_Lake_Shore_E_Vault\Engineering\Custom Sales Order`. The product line folders (`UL 891 Switchboards\Eaton\Fixed IC Breakers\2500A`) are added by the wizard underneath. Point it at a scratch folder for the first test.
2. **LSE template project (.wdp)**: default `E-CAD CUSTOM PROJECT TEMPLATE 2.wdp` in the template folder. Its library paths, icon menus and numbering settings are copied into every new job and its `.wdt` / `_wdtitle.wdl` are copied renamed. Without it AutoCAD Electrical applies its `wd.env` defaults and the sidecars are generated from the JSON.
3. **Drawing template (DRAWING-#.dwg)**: default `DRAWING-#.dwg` beside the template project. The fallback when the vault is not found is `ACAD_ELECTRICAL.dwt`, which has no LSE title block, so a `MISSING` vault must be fixed rather than worked around.
4. **Project templates JSON**: point every workstation at one shared copy of `LseProjectTemplates.json` on the vault.
5. **Symbol library folder (wd_m.dwg)**: default the vault NFPA library. Only matters when a template drawing has no WD_M.
6. **Default product line**: the product line pre-selected in the wizard, for example `UL 891 Switchboard`.
7. **Default drawing set**: the drawing set pre-selected when the product line names none.
8. **Date format**: .NET format, default `MM/dd/yyyy`. Save rejects an invalid format.
9. **Drawn by (default)**: value of `{USER}`, default is the Windows user name in capitals.
10. **Activate project LISP ({WDP})**: leave the default unless activation does not work (section 8).
11. Eight check boxes set the wizard defaults: fill the title block, set WD_M values, write `<job>.wdt`, write `<job>_wdtitle.wdl`, copy the template's own `.wdt` / `.wdl` instead of generating them, write `<job>.aepx`, activate the project, open the first drawing. They are only the starting state of the wizard's Options; what each does is in the table in section 4, step 2.

Press **Save**, then **Diagnostics** (or `LSEPROJECTDIAG`) and read the whole report. On a correctly set up workstation it looks like this (the lines from `Template WD_M block` on are what the template inspection is expected to print; the template drawings have not yet been inspected on a workstation):

```
LSE Project Tools V2 (Lake Shore Electric)
.NET 8.0.x
Host: acad on .NET 8.0.x (.NET 8)
Vault client: C:\Program Files\Autodesk\Vault Client 2026\Explorer (file version ..., built for .NETCoreApp,Version=v8.0) - compatible with this host
Vault add-in: logged in as <you> in vault <vault> on <server>
Assembly: ...\LSE.AutoCADElectrical.ProjectTools.dll
Settings: C:\Users\<you>\AppData\Roaming\LSE\AutoCADElectricalProjectTools\settings.json
Vault working folder: C:\_Lake_Shore_E_Vault
Jobs root (Custom Sales Order): C:\_Lake_Shore_E_Vault\Engineering\Custom Sales Order (exists)
LSE template project: C:\_Lake_Shore_E_Vault\CAD Standards\AutoCAD\ECAD Standards\Project Templates\CUSTOM TEMPLATES\CUSTOM PROJECT TEMPLATE 2.0\E-CAD CUSTOM PROJECT TEMPLATE 2.wdp (exists)
Drawing template: C:\_Lake_Shore_E_Vault\CAD Standards\AutoCAD\ECAD Standards\Project Templates\CUSTOM TEMPLATES\CUSTOM PROJECT TEMPLATE 2.0\DRAWING-#.dwg (exists)
Templates JSON: C:\_Lake_Shore_E_Vault\CAD Standards\AutoCAD\ECAD Standards\LSE Project Tools\LseProjectTemplates.json
Symbol library: C:\_Lake_Shore_E_Vault\CAD Standards\AutoCAD\ECAD Standards\Symbol Library\NFPA (exists)
WD_M library file: C:\_Lake_Shore_E_Vault\CAD Standards\AutoCAD\ECAD Standards\Symbol Library\NFPA\wd_m.dwg
Activation LISP: (if (boundp 'c:wd_makeproj_current) ...)
Copy template .wdt/.wdl: True   Write .aepx: True
Templates JSON: PASS (8 description lines, 4 product lines: UL 891 Switchboard, UL 1008 Transfer Switch, UL 508A Control Panel, Other (choose the folder yourself); 4 drawing sets: UL 891 Switchboard, UL 1008 Transfer Switch, UL 508A Control Panel, Template only)
Job number rule: ^\d{6}-\d{1,2}$ (example 001096-1); folder {JOB}_{CUSTOMER}; project {JOB}; files {JOB}-E{SHEET:3}
Title block names: ECAD_LSE_CUSTOM_TB_2.0
Template folder file DRAWING-#.dwg: PASS
Template folder file STOCKCODE-E001.dwg: PASS
Template .wdt: PASS (copied into new projects)
Template _wdtitle.wdl: PASS (copied into new projects)
Template WD_M block: PASS
Template layouts: *Model_Space, *Paper_Space
Template title blocks: ECAD_LSE_CUSTOM_TB_2.0 in *Paper_Space (DWG_NUMBER, DWG_TYPE, PROJECT_NAME, QUOTE_NUMBER, CUSTOMER, CUSTOMER_2, CUSTOMER_3, CUSTOMER_DESIGNATION, SWBD_AMP_VOLT_KAIC_NEMA, SCALE, ...)
Template project read: PASS (8 descriptions, 38 options, 75 properties, 2 drawings, UTF-8 with BOM)
Template schematic icon menu: C:\_Lake_Shore_E_Vault\CAD Standards\AutoCAD\ECAD Standards\dat MENU\ACE_NFPA_MENU.DAT (exists)
Jobs root is inside the Vault working folder: new job folders must be added to Vault after creation
Recent projects: 0
```

The three Vault lines are written by the shared `VaultProbe` helper and have not yet been seen on a workstation; the exact folder and version text will differ. Project Tools has no Vault Explorer setting, so the client folder is always found by searching `Program Files\Autodesk\Vault*\Explorer`.

What to look for:

- `Vault client: ... NOT LOADABLE HERE: ...`: the installed Vault client is built for a newer .NET than AutoCAD (or is the .NET Framework build). Project Tools itself does not need the Vault client; the line matters for Catalog Tools and Footprint Tools on the same machine. Update AutoCAD 2026 and Vault 2026 to matching releases.
- `Vault add-in: not loaded in this session` or `loaded but not logged in`: harmless for Project Tools, which never checks in or out. Add the job to Vault from Vault Explorer afterwards.
- `(MISSING) - not downloaded yet? In Vault Explorer select the folder and Get Latest Version`: the path is inside the vault working folder but the files are not on this disk yet. Get Latest Version on that folder, then rerun.
- `Vault working folder: NOT FOUND (expected C:\_Lake_Shore_E_Vault; ...)`: the vault is not checked out on this machine, and every path has fallen back to AutoCAD Electrical's own folders. Check the working folder out, delete `settings.json` so the blanks are filled again, and rerun.
- `WD_M library file: not found`: no `wd_m.dwg` in the symbol library folder or under `AeData\en-US\Libs`. Harmless while the template drawings carry WD_M (`Template WD_M block: PASS`).
- `Template .wdt: MISSING (generated from the JSON instead)` (or `_wdtitle.wdl`): the template folder lacks the sidecar; new jobs get a generated one with the same mapping or label lines.
- `Drawing set <name> sheet <nnn> template MISSING: <path>`: a per-row template such as `STOCKCODE-E001.dwg` is not in the template folder. Creating that set fails at validation until it is.
- `Template title blocks: NONE matched (expected ECAD_LSE_CUSTOM_TB_2.0)`: the title block in `DRAWING-#.dwg` is not named like that. To see what the drawing really holds, temporarily clear `blockNames` in the JSON and set `sheetAttribute` to a tag the block carries (then any block with that attribute is listed with its tags), read the names and tags, and put the right values back into the JSON.
- `Template schematic icon menu: ... (MISSING on this machine)`: `+[2]` of the template points at a `.DAT` file this workstation cannot see; the job will still be created, but AutoCAD Electrical will complain when the icon menu is opened.

## 4. Creating a project

Type `LSENEWPROJECT`. The example below creates job `001096-1` for AEC Contractors, a UL 891 switchboard built on Eaton fixed IC breakers at 2500 A.

### Step 1: Job and project descriptions

- **LSE job number (sales order)**: `001096-1`. Upper-cased as typed, validated against the job number rule, whose hint and example (`LSE sales order number: six digits, dash, release (001096-1). Example: 001096-1`) show beside the box. Typing it fills the job folder name and the project name on step 2 and every file name on step 3 until you edit those by hand.
- **Product line (vault folder)**: `UL 891 Switchboard`. Selecting one pre-selects its drawing set and the Type box of the unit-specs builder and rebuilds the folder pickers of step 2.
- **Drawing set**: the sheet list to start from, `UL 891 Switchboard`; its description shows below.
- **Description lines**: one box per line of the LSE template, labelled `n. LABEL (ATTRIBUTE)` with `*` for required and the template's own placeholder in grey. Fill `1. PROJECT NAME *`, `3. CUSTOMER 1 *` (`AEC Contractors`; it also feeds the job folder name) and whatever else is known (`2. QUOTE#`, `4.`/`5.` further customer lines, `6. UNIT DESIGNATION`). `8. DWG SCALE` is pre-filled `NTS`. These become `*[1]` to `*[8]` of the `.wdp` and are pushed to the mapped title block attributes.
- **Unit specs builder (line 7)**: pick Type `SWBD`, Amps `2500`, Voltage `277/480VAC`, kAIC `65`, NEMA `N1` (every box is editable) and press **Compose line 7**: `SWBD, 2500A, 277/480VAC, 65KA@480V, N1` lands in line 7. Edit the text afterwards if needed.
- **IEC codes (%P, %I, %L)**: optional. Written to WD_M as the drawing defaults.

### Step 2: Vault location and templates

- **Jobs root (Custom Sales Order)**: pre-filled from settings; browse to change for this run only.
- **Product line folders**: one editable box per level of the product line (`Manufacturer` > `Breaker type` > `Amperage`): `Eaton` > `Fixed IC Breakers` > `2500A`. Each box lists the vault's folders under the choice before it; a level with a single folder is selected for you; a folder that does not exist yet can be typed and will be created.
- **Job folder name ({JOB}_{CUSTOMER})**: `001096-1_AEC Contractors`, automatic until edited.
- **Project name (<name>.wdp)**: `001096-1`, automatic until edited. It becomes `001096-1.wdp`, `001096-1.wdt`, `001096-1_wdtitle.wdl` and `001096-1.aepx`.
- **Project file** shows the result: `C:\_Lake_Shore_E_Vault\Engineering\Custom Sales Order\UL 891 Switchboards\Eaton\Fixed IC Breakers\2500A\001096-1_AEC Contractors\001096-1.wdp`.
- **LSE template project (.wdp)** and **Drawing template (DRAWING-#.dwg)**: pre-filled from settings; browse to change for this project only. Per-row templates on step 3 are resolved in the template project's folder.
- **Options**: nine check boxes for this run, listed below.

**The Options check boxes.** The first eight start ticked or unticked from the defaults saved in `LSEPROJECTSETTINGS` (same names, slightly shorter labels there). Changing them in the wizard affects this run only; the wizard never saves them. To change a default for every future run, use `LSEPROJECTSETTINGS` and press Save. The ninth has no saved default and always starts unticked. The review on step 4 shows the value of each under `OPTIONS`.

| Check box (wizard label) | Setting | Default | When ticked | When unticked |
|---|---|---|---|---|
| Fill the LSE title block (ECAD_LSE_CUSTOM_TB_2.0): project lines, drawing number, drawing type | `FillTitleBlocks` | on | Every block matching `titleBlock.blockNames` gets the description lines, `DWG_NUMBER` and `DWG_TYPE`. | Title blocks keep the template's values; the log says `title block skipped`. Title Block Update can fill them later from the `.wdt`. |
| Set WD_M sheet number and IEC project / installation / location codes | `SetWdmValues` | on | WD_M `SHEET` gets the Sheet cell, and `IEC_PROJ`/`IEC_INST`/`IEC_LOC` get the step 1 codes. A template without WD_M gets one inserted from `wd_m.dwg`. | WD_M is left as in the template and a missing WD_M is not inserted either. The log line then still reads `WD_M MISSING - the template has no WD_M block and no wd_m.dwg library file was found` for a template without WD_M even if the library exists (the message does not know the box was off). |
| Write `<job>.wdt` so Title Block Update knows the LSE mapping | `WriteWdtFile` | on | `<job>.wdt` is copied or generated (see the next two rows). | No `.wdt`; Title Block Update has no LSE mapping for this project. |
| Write `<job>_wdtitle.wdl` so the Project Manager shows the LSE description labels | `WriteWdlFile` | on | `<job>_wdtitle.wdl` is copied or generated. | No `.wdl`; the Project Manager Descriptions dialog shows generic prompts. |
| Copy the template's own .wdt and .wdl (recommended; otherwise they are generated from the JSON) | `CopyTemplateSidecars` | on | Each of the two files is copied, renamed, from beside the template project; if the template has none (or no template project is set) that file is generated from the JSON instead. | Both are generated from the JSON. Has no effect at all when both Write .wdt and Write .wdl are unticked. |
| Write `<job>.aepx` project configuration (same shape as the template's) | `WriteAepxFile` | on | `<job>.aepx` lists every sheet. | No `.aepx`; AutoCAD Electrical writes its own when the project is opened. Untick this if a pre-written `.aepx` ever causes trouble. |
| Activate the project in the Project Manager when finished | `ActivateProject` | on | After the wizard closes (Finish), the activation LISP is sent (section 8). Needs an open drawing. | Nothing is sent; open the project from the Project Manager yourself. |
| Open the first drawing when finished | `OpenFirstDrawing` | off | After the wizard closes, the first row's drawing (normally the E001 cover) is opened. | Nothing is opened. |
| Allow an existing, non-empty project folder | none (per run only) | off | A job folder that already exists and holds other files is reused. Still refused: an existing `<job>.wdp`, `<job>.wdt`, `<job>_wdtitle.wdl`, `<job>.aepx`, any target `.dwg`, or a read-only `LSE_ProjectReport.txt`. An existing writable `LSE_ProjectReport.txt` is overwritten. | An existing folder must be empty. A folder that does not exist yet is always fine. |

The last two boxes act only after a successful create, when the wizard is closed; closing it with **Finish** or the window's close button both count.

### Step 3: Drawing list

The grid is pre-filled from the drawing set. Caution: while file names are still automatic, choosing another product line or drawing set on step 1 reloads this list without asking, and any drawing types or templates typed into the grid are lost. Settle step 1 first. **Sheet (E-no.)** is the E-number (`001`, `002`, `100`, `200`, `201`, `300`, `301`, `302`, `400`, `500`, `501` for the UL 891 set). File names follow the **File name pattern** (`{JOB}-E{SHEET:3}` gives `001096-1-E001.dwg`) and are regenerated whenever the job number, the project name or a Sheet cell changes, until you edit a file name by hand; after that press **Regenerate file names** to make them automatic again.

| Button | Effect |
|---|---|
| Load drawing set | Replaces the list with the selected set's sheets (asks first when the list is not empty). |
| Add drawing | Inserts a `DRAWING TYPE` row after the current row with the next free E-number (highest + 1) and a file name from the pattern; the cursor lands in the drawing type cell. |
| Remove | Deletes the current row. |
| Move up / Move down | Reorders the current row. Numbers and names are not changed. |
| Regenerate file names | Regenerates every file name from the pattern and the Sheet cells, and makes names automatic again. |
| Renumber 001..n | Sets Sheet to `001`..`n` in row order and regenerates the names (abandons the E-number series). |

Columns: Sheet (E-no.), File name (`.dwg` is added if missing), Drawing type (description 1: the `===` line and `DWG_TYPE` on the title block), Description 2 and 3, Section and Subsection (the `=` and `==` codes; unused by the LSE template), PM folder (Project Manager tree folder), Template (optional: a file name inside the template folder such as `STOCKCODE-E001.dwg`, or a full path).

### Step 4: Review and create

Read the summary: job, project name, customer, product line with the folder choices, drawing set, project file, template project, drawing template, IEC codes, the eight description lines with their attributes, the options, and the drawing table with the template each sheet is created from. The last line is `Ready...` or `NOT READY: <reason>`. Press **Create project**. The pane becomes the log:

```
Started: 2026-11-10T14:02:11 by dwisniewski@LSE-ENG-07
Project: 001096-1 (001096-1) - 2500A MAIN SWITCHBOARD
Customer: AEC Contractors   Product line: UL 891 Switchboard > Eaton > Fixed IC Breakers > 2500A
Folder: C:\_Lake_Shore_E_Vault\Engineering\Custom Sales Order\UL 891 Switchboards\Eaton\Fixed IC Breakers\2500A\001096-1_AEC Contractors
Drawing template: C:\_Lake_Shore_E_Vault\CAD Standards\AutoCAD\ECAD Standards\Project Templates\CUSTOM TEMPLATES\CUSTOM PROJECT TEMPLATE 2.0\DRAWING-#.dwg
Template folder: C:\_Lake_Shore_E_Vault\CAD Standards\AutoCAD\ECAD Standards\Project Templates\CUSTOM TEMPLATES\CUSTOM PROJECT TEMPLATE 2.0
Templates config: C:\_Lake_Shore_E_Vault\CAD Standards\AutoCAD\ECAD Standards\LSE Project Tools\LseProjectTemplates.json
Project folder created: PASS
LSE template project settings copied from E-CAD CUSTOM PROJECT TEMPLATE 2.wdp: 38 option line(s) (+[n]: library paths, icon menus), 75 property line(s) (?[n]), 2 template drawing(s) ignored, text encoding UTF-8 with BOM
WD_M library block: C:\_Lake_Shore_E_Vault\CAD Standards\AutoCAD\ECAD Standards\Symbol Library\NFPA\wd_m.dwg
Drawing 1/11 001096-1-E001.dwg from STOCKCODE-E001.dwg: title block 1 found, 10 attribute(s) set; WD_M found, 1 attribute(s) set
Drawing 2/11 001096-1-E002.dwg from DRAWING-#.dwg: title block 1 found, 10 attribute(s) set; WD_M found, 1 attribute(s) set
...
Drawing 11/11 001096-1-E501.dwg from DRAWING-#.dwg: title block 1 found, 10 attribute(s) set; WD_M found, 1 attribute(s) set
Project file written (UTF-8 with BOM, *[1..8], then +[n], then ?[n]): ...\001096-1_AEC Contractors\001096-1.wdp
Description labels copied from the LSE template: ...\001096-1_AEC Contractors\001096-1_wdtitle.wdl
Title block mapping copied from the LSE template: ...\001096-1_AEC Contractors\001096-1.wdt
Project configuration written: ...\001096-1_AEC Contractors\001096-1.aepx
Status: SUCCESS
Vault: these 16 item(s) are in the Vault working folder and are not checked in yet. Check them in (new files: Add to Vault) from the Vault tab or Vault Explorer so other workstations get them:
  ...\001096-1_AEC Contractors\001096-1-E001.dwg
  ...
  ...\001096-1_AEC Contractors
```

The `Vault:` lines appear only when the jobs root is inside the vault working folder (a scratch root outside it gives none).

The attribute counts are illustrative; see section 5. A message box confirms the path and drawing count and reminds you to add the new job folder to Vault (Vault tab > Add Files, or Vault Explorer); `LSE_ProjectReport.txt` lists every file. **Cancel** becomes **Finish**. When the wizard closes, the plugin sends the activation LISP (and opens the first drawing if ticked). Watch the command line: `LSE: activated project ...` means the Project Manager switched; `LSE: c:wd_makeproj_current is not loaded...` means you must open the project by hand (Project Manager, Open Project, pick the `.wdp`).

### What to check on the first real run

1. The job folder sits in the right place under `Custom Sales Order` and holds `001096-1.wdp`, `001096-1.wdt`, `001096-1_wdtitle.wdl`, `001096-1.aepx`, the `001096-1-Ennn.dwg` sheets and `LSE_ProjectReport.txt`.
2. The Project Manager opens the project without complaint, lists the sheets in order with their drawing types, and finds the NFPA icon menus and symbol libraries copied from the template.
3. Right-click the project, Descriptions: the labels are `PROJECT NAME`, `QUOTE#`, `CUSTOMER 1` ... `DWG SCALE` and the values are what you typed.
4. Open a sheet: the `ECAD_LSE_CUSTOM_TB_2.0` title block shows the project lines, `DWG_NUMBER` = the file name without extension and `DWG_TYPE` = the drawing type; the cover came from `STOCKCODE-E001.dwg`.
5. Run AutoCAD Electrical's Title Block Update once on the project; it should find the copied `.wdt` and change nothing.
6. Insert a component: the WD_M sheet number appears in tags (`%S`). The plugin writes the Sheet cell text (`001`); confirm against a finished LSE job whether `1`, `001` or `E001` is the house convention and adjust the grid values if needed.
7. Open the `.wdp` in a text editor and compare it with the template's: same encoding (UTF-8 with BOM), same `+[n]` and `?[n]` lines, `*[1]` to `*[8]` filled, one block per sheet.
8. Check whether AutoCAD Electrical kept or rewrote the `.aepx`; either is fine as long as the project opens.
9. Add the job folder to Vault (the report lists the files).

## 5. Reading the log

Each drawing line reports which template it came from and two results:

| Fragment | Meaning |
|---|---|
| `from STOCKCODE-E001.dwg` / `from DRAWING-#.dwg` | The template file resolved for that row (the row's Template column inside the template folder, else the drawing template). |
| `title block N found, M attribute(s) set` | N references matched `ECAD_LSE_CUSTOM_TB_2.0`; M attribute values were changed across them. At most 10 for the LSE block (8 description lines, `DWG_NUMBER`, `DWG_TYPE`); fewer means some values already matched the template (for example `SCALE` = `NTS`) or some tags do not exist on the block. |
| `title block NOT FOUND` | No block matched. Check `blockNames` in the JSON against `LSEPROJECTDIAG`. |
| `title block skipped` | Fill title blocks was unticked. |
| `WD_M found, M attribute(s) set` | The template's WD_M was updated (`SHEET`, plus the IEC codes when set). |
| `WD_M inserted, M attribute(s) set` | The template had no WD_M; it was inserted from `wd_m.dwg`. |
| `WD_M MISSING - ...` | No WD_M and no library file. AutoCAD Electrical will add one with defaults when the drawing is first used, but the sheet number and IEC codes will be blank. |

Project file lines:

| Fragment | Meaning |
|---|---|
| `LSE template project settings copied from <file>: 38 option line(s) ..., 75 property line(s) ..., 2 template drawing(s) ignored, text encoding UTF-8 with BOM` | The `+[n]` and `?[n]` lines and the encoding of the template project were taken over; its drawings were not. |
| `No LSE template project selected: ...` | The template project box was blank; AutoCAD Electrical's `wd.env` defaults will apply. |
| `Project file written (UTF-8 with BOM, *[1..8], then +[n], then ?[n])` | Encoding and group order of the `.wdp`. |
| `Description labels copied from the LSE template` / `generated from the templates JSON` | The `.wdl` was copied from beside the template project, or generated because none was there (or copying was unticked). |
| `Title block mapping copied from the LSE template` / `generated from the templates JSON` / `(.wdt) skipped: no title block names are configured` | Same for the `.wdt`. |
| `Project configuration written` | The `.aepx`. |

The same text is saved as `LSE_ProjectReport.txt` in the job folder.

## 6. Recovery by hand

A failed run rolls itself back: every drawing and project file it wrote (copied sidecars included) is deleted and a folder it created is removed if empty. Product line folders created on the way (for example a new `2500A` folder) stay. The log, with the exception detail, is in `%APPDATA%\LSE\AutoCADElectricalProjectTools\Logs\`.

If rollback itself reported `Rollback could not remove ...` (a file was locked, for example by a preview handler), delete the listed files by hand before rerunning; the next run refuses to start while `<job>.wdp`, `<job>.wdt`, `<job>_wdtitle.wdl`, `<job>.aepx` or any target `.dwg` exists.

If the failure came after `Status: SUCCESS` (the report or `settings.json` could not be written), the project files were rolled back too; read the `ERROR:` line, fix the cause (for example a read-only `settings.json`) and rerun.

To redo a project that succeeded, delete its folder (or move it) and run the wizard again. Nothing outside the job folder except `settings.json` (recent list) is changed by a successful run. A job folder that was already checked into the vault must be handled through the vault, not deleted on disk.

## 7. Error messages

| Message | Cause | Action |
|---|---|---|
| `The LSE project templates JSON was not found. ...` | `TemplatesConfigPath` points nowhere. | Fix the path in Settings or restore the file beside the DLL. |
| `The project templates JSON is empty.` / `...defines no description lines.` / `...defines no project types.` | The JSON is damaged. | Restore from the shared copy. |
| `Description line index N is defined more than once...` / `Description line indexes must start at 1.` | Bad edit to `descriptionLines`. | Fix the JSON. |
| `The project templates JSON needs exactly one description line with role "title" (the LSE PROJECT NAME line).` | The `title` role was removed or duplicated. | Put `"role": "title"` on line 1 only. |
| `Only one description line may carry role "customer".` | Two customer roles. | Keep it on `CUSTOMER 1`. |
| `job.numberPattern is not a valid regular expression: ...` | Bad edit to `job.numberPattern`. | Fix the expression (`^\d{6}-\d{1,2}$`). |
| `Product line "X" names a project type that is not defined: Y` | `productLines[].projectType` does not match a `projectTypes[].name`. | Fix the name. |
| `The templates JSON has no description line with role "specs" to receive the unit specs.` | **Compose line 7** with no `specs` role. | Put `"role": "specs"` on the UNIT SPECS line. |
| `LSE sales order number: six digits, dash, release (001096-1). Example: 001096-1.` (step 1) / `... Entered: '<text>'.` (validation) | The job number does not match `job.numberPattern`. | Type it as `NNNNNN-R`. |
| `Enter description line N (LABEL).` (step 1) / `Description line N (LABEL) is required.` (validation) | A required line (PROJECT NAME, CUSTOMER 1) is blank. | Fill it. |
| `Project name (description line 1) is required.` | The `title` line is blank when the transaction validates. | Fill line 1. |
| `The jobs root must be a full path.` / `Project folder must be a full path.` | Relative or blank root. | Enter a full path. |
| `Product line folder names cannot contain \ / : * ? " < > \|: <choice>` | A typed folder level has path characters. | Remove them. |
| `The job folder name must be a valid folder name.` / `The project name must be a valid file name.` / `Project name must be a valid file name (it becomes <name>.wdp).` | Characters such as `\ / : * ? " < > \|`. | Remove them. |
| `The drawing template was not found: ...` (step 2) / `Drawing template (.dwg/.dwt) was not found.` (validation) / `Drawing template was not found.` (drawing level) | Wrong drawing template path. | Browse to `DRAWING-#.dwg`. |
| `The LSE template project was not found: ...` (step 2) / `LSE template project (.wdp) was not found.` (validation) | Wrong template project path. | Browse to it or clear the box. |
| `Add at least one drawing.` | Empty grid. | Add rows or load a drawing set. |
| `Every drawing needs a valid file name (no folders). Offending row: sheet 'n'.` | Blank name or path characters in File name. | Fix the row. |
| `Duplicate drawing file name: X` | Two rows produce the same `.dwg` (case-insensitive). | Rename or renumber. |
| `Every drawing needs a sheet value (X).` | Blank Sheet cell. | Fill it or renumber. |
| `Per-drawing template was not found: <path> (row <file>).` | The Template column names a file that is not in the template folder (or the full path is wrong). | Fix or clear it; check `LSEPROJECTDIAG` for `template MISSING`. |
| `A project file already exists: ...` | A `.wdp` with that name is already in the folder. | Choose another project name or folder. |
| `A project sidecar file already exists: <path>` | The existing job folder already holds `<job>.wdt`, `<job>_wdtitle.wdl` or `<job>.aepx` (any letter case). Nothing was written. | Choose another folder or project name, or remove the file (through Vault if it is checked in). |
| `This file is read-only: ...\LSE_ProjectReport.txt. They are in the Vault working folder: check them out in Vault first ... Nothing was changed.` | The existing job folder has a report file under Vault control that is not checked out. | Check it out in Vault (or clear the read-only attribute outside the vault), then create again. |
| Message box `Project creation failed` | Any failure during the run. Its text is the whole log up to the `ERROR:` line; the review pane ends `FAILED. Everything created by this run was rolled back.` | Read the `ERROR:` line, fix it, press **Create project** again (it is re-enabled). |
| Message box `LSE Project Tools` with a stack trace | An unexpected error in a command itself (for example activation or opening the first drawing after the wizard closed). | Read the first line; for activation see section 8. |
| `The project folder already exists and is not empty: ... Choose another folder name or tick 'Allow an existing, non-empty folder'.` | Reusing a folder with files in it. | Tick the option on step 2 if that is intended. |
| `A drawing with that name already exists: ...` | A target `.dwg` is already in the folder. | Rename the row or clear the folder. |
| `Drawing already exists: ...` | Same, caught at the drawing level. | Same. |
| `Project file was not found.` | `LSEPROJECTACTIVATE` on a moved file. | Pick the right `.wdp`. |
| `The activation LISP expression is blank (LSEPROJECTSETTINGS).` | The LISP setting was cleared. | Restore it (section 8). |
| `No active drawing. Open any drawing, then run LSEPROJECTACTIVATE.` | No document is open. | Open any drawing first. |
| `The project has been created. Close this wizard and run LSENEWPROJECT again for another project.` | Navigating back after success. | Press Finish. |
| `LSE: c:wd_makeproj_current is not loaded. ...` (command line) | AutoCAD Electrical does not define that function. | Open the project in the Project Manager by hand; see section 8. |

## 8. Project activation

The plugin cannot call AutoCAD Electrical's Project Manager directly; it types one LISP expression at the command line after the wizard closes. The default expression is:

```
(if (boundp 'c:wd_makeproj_current)
    (progn (c:wd_makeproj_current "{WDP}") (princ "\nLSE: activated project {WDP}"))
    (princ "\nLSE: c:wd_makeproj_current is not loaded. Open the project from the Project Manager: {WDP}"))
```

`c:wd_makeproj_current` is quoted from an Autodesk community answer, not from the official API reference, so it is not verified. To test on a workstation: open any drawing, type `(c:wd_makeproj_current "C:/path/to/existing.wdp")` and see whether the Project Manager switches. `(ace_getactiveproject)` prints the active project. If the function has another name on your release, put the working expression into **Activate project LISP** in Settings, keeping `{WDP}`; `LSEPROJECTACTIVATE` lets you test it against any existing project without creating one.
