# Data stores: LSE Project Tools

Maintained by DCW. Last reviewed 2026-11-10.

Every file the plugin reads or writes, where it lives, what is in it, and what breaks when it is wrong. Paths marked *default* can be changed in `LSEPROJECTSETTINGS`. The order in which these are touched during a create is in [ARCHITECTURE.md](ARCHITECTURE.md) section 5. `<vault>` below is the vault working folder, `C:\_Lake_Shore_E_Vault` or `_Lake_Shore_E_Vault` at the root of another fixed or network drive (see section 1).

| # | Store | Default location | Plugin access |
|---|---|---|---|
| 1 | `settings.json` | `%APPDATA%\LSE\AutoCADElectricalProjectTools\` | read on every form open, by diagnostics and by activation; written by Settings > Save and after each successful create |
| 2 | `LseProjectTemplates.json` | beside the DLL | read only; its `vault` section is also read by `SettingsService.Load` to fill blank settings |
| 3 | Template drawings `DRAWING-#.dwg` and `STOCKCODE-E001.dwg` | `<vault>\CAD Standards\AutoCAD\ECAD Standards\Project Templates\CUSTOM TEMPLATES\CUSTOM PROJECT TEMPLATE 2.0\` (the template folder) | read only |
| 4 | LSE template project `E-CAD CUSTOM PROJECT TEMPLATE 2.wdp` with its `.wdt` and `_wdtitle.wdl` | the template folder | read only; the two sidecars are copied, renamed, into every new job |
| 5 | WD_M library block `wd_m.dwg` | `<vault>\CAD Standards\AutoCAD\ECAD Standards\Symbol Library\NFPA\`, else under `Documents\Acade 2026\AeData\en-US\Libs\` | read only |
| 6 | Job folder and its contents | `<Jobs root>\<product line folder>\<level choices>\<job>_<customer>\` | created per run; removed on rollback |
| 7 | `Logs\ProjectReport_<stamp>.txt` | `%APPDATA%\LSE\AutoCADElectricalProjectTools\Logs\` | written on failure only |

## 1. settings.json

Per user. Written as indented JSON through a `.tmp` file and an atomic move. Deleting it resets every value to the defaults below; a corrupt file is treated the same way.

`SettingsService.Load` fills only blank values. It first fixes `TemplatesConfigPath`, then reads the `vault` section of that JSON (`TemplateService.TryLoadVault`, which never throws and yields the code defaults when the file or section is missing), then looks for the vault working folder (`DiscoverVaultRoot`: `C:\_Lake_Shore_E_Vault` first, else `_Lake_Shore_E_Vault` at the root of any fixed or network drive). A `vault` path written for `C:` is remapped to the drive that was found (`OnVault`) unless it already exists as written. Each vault default is used only when the file or folder exists; otherwise the old AutoCAD Electrical discovery applies.

| Property | Default | Meaning |
|---|---|---|
| `ProjectsRootFolder` | `vault.jobsRoot` = `<vault>\Engineering\Custom Sales Order` when it exists; else `Documents\Acade 2026\AeData\Proj` when it exists, else `Documents\AutoCAD Electrical Projects` | The jobs root. The product line folders and the job folder are created under it. Labelled "Jobs root (Custom Sales Order)". |
| `BaseProjectPath` | `vault.templateProject` = `<template folder>\E-CAD CUSTOM PROJECT TEMPLATE 2.wdp` when it exists, else blank | The LSE template project whose `+[n]` option lines, `?[n]` property lines and text encoding are copied into every new project, and whose `.wdt` and `_wdtitle.wdl` are copied. Blank means AutoCAD Electrical defaults apply and the sidecars are generated. Its folder is the template folder in which per-drawing template file names are resolved. |
| `DrawingTemplatePath` | `vault.drawingTemplate` (`DRAWING-#.dwg`) resolved in the template folder when it exists; else the first `ACAD_ELECTRICAL.dwt` found under `%LOCALAPPDATA%\Autodesk\AutoCAD Electrical 20*`, else blank | The drawing every sheet is created from unless a row names another template. Labelled "Drawing template (DRAWING-#.dwg)". |
| `TemplatesConfigPath` | `LseProjectTemplates.json` beside the DLL | The LSE standard. Point every workstation at one shared copy on the vault. |
| `SymbolLibraryFolder` | `vault.symbolLibrary` = `<vault>\CAD Standards\AutoCAD\ECAD Standards\Symbol Library\NFPA` when it exists; else `AeData\en-US\Libs\jic125` (or `JIC125`, `iec2`, `IEC2`, or the first library folder) | Where `wd_m.dwg` is looked for first when a template lacks WD_M. |
| `DefaultProductLine` | blank (first product line in the JSON) | Pre-selected product line in the wizard, matched by `name`, case-insensitive. |
| `DefaultProjectType` | blank (first drawing set in the JSON) | Pre-selected drawing set. Labelled "Default drawing set". A product line that names a `projectType` overrides it when selected. |
| `DateFormat` | `MM/dd/yyyy` | .NET format string for `{DATE}` and the title block date attribute. Settings > Save rejects an invalid format. |
| `DrawnBy` | Windows user name, upper case | Value of `{USER}`. |
| `FillTitleBlocks` | `true` | Wizard default for filling the `ECAD_LSE_CUSTOM_TB_2.0` attributes. |
| `SetWdmValues` | `true` | Wizard default for WD_M sheet and IEC values. |
| `WriteWdtFile` | `true` | Wizard default for producing `<job>.wdt`. |
| `WriteWdlFile` | `true` | Wizard default for producing `<job>_wdtitle.wdl`. |
| `CopyTemplateSidecars` | `true` | Wizard default: take `.wdt` and `.wdl` from the template project (renamed) instead of generating them from the JSON. Falls back to generation when the template has none. |
| `WriteAepxFile` | `true` | Wizard default for writing `<job>.aepx`. |
| `ActivateProject` | `true` | Wizard default for sending the activation LISP afterwards. |
| `OpenFirstDrawing` | `false` | Wizard default for opening the first drawing afterwards. |
| `ActivateProjectLisp` | `(if (boundp 'c:wd_makeproj_current) (progn (c:wd_makeproj_current "{WDP}") (princ "...")) (princ "..."))` | Sent to the command line with `{WDP}` replaced by the forward-slash path of the new `.wdp`. |
| `RecentProjects` | empty list | Full paths of the last 25 projects created, newest first. Stored only; no screen reads it yet. |

The wizard reads the eight switch defaults into its check boxes but does not write them back; only `LSEPROJECTSETTINGS` changes them.

## 2. LseProjectTemplates.json

Read by `TemplateService.Load`. Property names are case-insensitive; `//` comments and trailing commas are allowed. The shipped file mirrors the live LSE template "CUSTOM PROJECT TEMPLATE 2.0" and the vault folder list `Locations.txt` as of 2026-11-10. Validation failures are listed in [OPERATIONS.md](OPERATIONS.md) section 7.

### 2.1 vault

Where the LSE standard lives. Read twice: by `Load` as part of the whole file, and by `SettingsService.Load` through `TryLoadVault` to fill blank settings (section 1).

| Field | Shipped value | Meaning |
|---|---|---|
| `standardsRoot` | `C:\_Lake_Shore_E_Vault\CAD Standards\AutoCAD\ECAD Standards` | Documentation only; no code reads it. |
| `jobsRoot` | `C:\_Lake_Shore_E_Vault\Engineering\Custom Sales Order` | Default `ProjectsRootFolder`. |
| `templateProject` | `...\Project Templates\CUSTOM TEMPLATES\CUSTOM PROJECT TEMPLATE 2.0\E-CAD CUSTOM PROJECT TEMPLATE 2.wdp` | Default `BaseProjectPath`; its folder is the template folder. |
| `drawingTemplate` | `DRAWING-#.dwg` | File name inside the template folder; default `DrawingTemplatePath`. Checked by diagnostics. Code default is the same name. |
| `coverTemplate` | `STOCKCODE-E001.dwg` | File name of the cover drawing inside the template folder. Only diagnostics reads it (reports PASS or MISSING); the drawing sets name it per row in `template`. Code default is the same name. |
| `symbolLibrary` | `C:\_Lake_Shore_E_Vault\CAD Standards\AutoCAD\ECAD Standards\Symbol Library\NFPA` | Default `SymbolLibraryFolder`. |

### 2.2 job

The LSE job number rule and the naming patterns derived from it.

| Field | Shipped value | Meaning |
|---|---|---|
| `numberPattern` | `^\d{6}-\d{1,2}$` | .NET regular expression the job number must match (six-digit sales order, dash, release). `Load` rejects an invalid expression; blank falls back to the old free-form rule `^[A-Za-z0-9][A-Za-z0-9._\- ]*$`. |
| `numberExample` | `001096-1` | Shown beside the job box and in the validation message. |
| `numberHint` | `LSE sales order number: six digits, dash, release (001096-1)` | Shown beside the job box; first part of the validation message. Code default `Job number`. |
| `folderNamePattern` | `{JOB}_{CUSTOMER}` | Job folder name until the engineer edits it. Made file-name safe; trailing `_`, `-` and spaces are trimmed, so a blank customer gives `001096-1`. |
| `projectNamePattern` | `{JOB}` | Project name (`<name>.wdp`) until edited. |
| `fileNamePattern` | `{JOB}-E{SHEET:3}` | Default drawing file name pattern; fills a drawing set whose `fileNamePattern` is blank. `001096-1-E001.dwg`. |

Tokens in these patterns: `{JOB}`, `{CUSTOMER}`, `{QUOTE}`, `{PRODUCTLINE}`, `{PROJECT}`, `{USER}`, `{DATE}`, `{YEAR}`, `{SHEET}`, `{SHEET:n}`. `{CUSTOMER}`, `{QUOTE}` and `{PRODUCTLINE}` are the customer line, the quote line and the product line name with invalid file name characters replaced by `-`.

### 2.3 descriptionLines

One object per `*[n]` project description line. The shipped eight are exactly the LSE template's lines (labels from its `_wdtitle.wdl`, attributes from its `.wdt`).

| Field | Required | Meaning |
|---|---|---|
| `index` | yes, unique, 1 or higher | The `n` in `*[n]` and in `LINEn`. |
| `label` | no | Shown in the wizard; written to a generated `_wdtitle.wdl` as `LINEn=label`. Blank labels are not written. |
| `attribute` | no | Title block attribute tag that receives the value; written to a generated `.wdt` as `attribute = LINEn`. Blank means the line is not pushed to the title block. |
| `role` | no | `title` (exactly one, always required), `customer` (at most one; feeds `{CUSTOMER}`), `quote` (feeds `{QUOTE}`), `specs` (target of the unit-specs builder), `job` (box upper-cased; no longer required), or a descriptive value with no code behaviour (`designation`, `scale`, `date`, `user`). |
| `required` | no | `true` makes the wizard and the transaction refuse a blank value. `Load` forces it on the `title` line. |
| `placeholder` | no | Grey placeholder text in the wizard box (the template's own placeholder). Not written anywhere. |
| `default` | no | Initial text, expanded with `{JOB}`, `{PROJECT}`, `{USER}`, `{DATE}`, `{YEAR}` (the job is blank at that point). |

| `*[n]` | label | attribute | role | required | placeholder or default |
|---|---|---|---|---|---|
| 1 | `PROJECT NAME` | `PROJECT_NAME` | `title` | yes | placeholder `PROJECT NAME` |
| 2 | `QUOTE#` | `QUOTE_NUMBER` | `quote` | no | placeholder `#####` |
| 3 | `CUSTOMER 1` | `CUSTOMER` | `customer` | yes | placeholder `CUST 1` |
| 4 | `CUSTOMER 2` | `CUSTOMER_2` | | no | placeholder `CUST 2` |
| 5 | `CUSTOMER 3` | `CUSTOMER_3` | | no | placeholder `CUST 3` |
| 6 | `UNIT DESIGNATION` | `CUSTOMER_DESIGNATION` | `designation` | no | placeholder `UNIT TYPE OR CUSTOMER DESIGNATION` |
| 7 | `UNIT SPECS` | `SWBD_AMP_VOLT_KAIC_NEMA` | `specs` | no | placeholder `SWBD, ####A, ###/###VAC, ##KA@###V, N#?` |
| 8 | `DWG SCALE` | `SCALE` | `scale` | no | default `NTS` |

### 2.4 labelFile

| Field | Shipped value | Meaning |
|---|---|---|
| `extraLabels` | `{ "9": "FUTURE USE", ... "14": "FUTURE USE" }` | `LINEn=label` entries added to a generated `_wdtitle.wdl` for indexes that have no description line (the template's `.wdl` carries LINE9 to LINE14). Keys are the line numbers as strings; a key that is not an integer, that clashes with a description line, or whose value is blank is ignored. Not used when the template's `.wdl` is copied. |

### 2.5 titleBlock

| Field | Shipped value | Meaning |
|---|---|---|
| `blockNames` | `["ECAD_LSE_CUSTOM_TB_2.0"]` | Wildcard patterns (`*`, `?`, case-insensitive) for title block names. Empty list means "any block that has the sheet attribute", and then no `.wdt` is generated. Code default is an empty list. |
| `sheetAttribute` | blank | When set, receives the row's sheet value; `.wdt` code `SHEET`. Blank because the LSE block has no sheet mapping. Code default blank. |
| `sheetMaxAttribute` | blank | When set, receives the drawing count at creation; `.wdt` code `SHEETMAX`. Code default blank. |
| `fileNameAttribute` | `DWG_NUMBER` | Receives the file name without extension (`001096-1-E001`); `.wdt` code `FILENAME`. Code default `DWG_NUMBER`. |
| `descriptionAttributes` | `["DWG_TYPE"]` | Receive drawing descriptions 1 to 3; `.wdt` codes `DD1`..`DD3`. Code default `["DWG_TYPE"]`. |
| `dateAttribute` | blank | When set, receives today's date in `DateFormat`. Not written to the `.wdt`. |
| `sectionAttribute` | blank | When set, receives the section; `.wdt` code `DWGSEC`. |
| `subsectionAttribute` | blank | When set, receives the subsection; `.wdt` code `DWGSUB`. |
| `fixed` | `{}` | `"ATTR": "text or tokens"` pairs written to every title block. All tokens of 2.2, with `{SHEET}` from the row's sheet digits. Not written to the `.wdt`. |

### 2.6 wdm

| Field | Shipped value | Meaning |
|---|---|---|
| `blockName` | `WD_M` | The drawing settings block. |
| `sheetAttribute` | `SHEET` | Receives the row's sheet text as typed in the grid, for example `001` (the `%S` of AutoCAD Electrical). Whether AutoCAD Electrical expects `1` or `001` here for E-numbered sheets is unverified. |
| `projectAttribute` | `IEC_PROJ` | Receives the wizard's IEC project code (`%P`). |
| `installationAttribute` | `IEC_INST` | Installation code (`%I`). |
| `locationAttribute` | `IEC_LOC` | Location code (`%L`). |
| `drawingNumberAttribute` | blank | When set (for example `SHEETDWGNAME`), receives the file name without extension (`%D`). Blank leaves the template value. |

### 2.7 unitSpecs

Choices for the builder that composes the `specs` line (line 7).

| Field | Shipped value | Meaning |
|---|---|---|
| `pattern` | `{TYPE}, {AMPS}A, {VOLTS}, {KAIC}KA@{KV}V, {NEMA}` | Output pattern. `{KV}` is the largest number found in the voltage text (`277/480VAC` gives `480`), `###` when none. A blank box yields the template's placeholder piece (`####`, `####`, `###/###VAC`, `##`, `N#?`); a trailing `A` typed in the amps box is removed. |
| `unitTypes` | `SWBD, ATS, CP, MCC, PNL` | Type choices. A product line's `unitType` pre-selects one. |
| `amperages` | `225` .. `6000` | Amps choices. |
| `voltages` | `208/120VAC, 240/120VAC, 277/480VAC, 347/600VAC` | Voltage choices. |
| `kaic` | `10` .. `200` | kAIC choices. |
| `nema` | `N1, N1 GASKETED, N3R, N12, N4, N4X` | Enclosure choices. |

Every box is editable, so values outside the lists can be typed. Example: `SWBD`, `2500`, `277/480VAC`, `65`, `N1` compose `SWBD, 2500A, 277/480VAC, 65KA@480V, N1`.

### 2.8 sections

A list of section codes. Only the first entry is used, as the section of a drawing added with **Add drawing**. Shipped empty: the LSE template uses no section codes.

### 2.9 productLines

The `Custom Sales Order` folder tree on the vault (from `Locations.txt`), one entry per product line.

| Field | Meaning |
|---|---|
| `name` | Shown in the product line combo; matched against `DefaultProductLine`. |
| `folder` | Folder under the jobs root (`UL 891 Switchboards`, `UL1008`, `UL 508A Control Panels`). Blank means the job folder goes directly under the jobs root. |
| `unitType` | Pre-selects the Type box of the unit-specs builder. |
| `projectType` | Name of the drawing set to pre-select; must match a `projectTypes` entry (case-insensitive) or `Load` fails. Blank leaves the drawing set alone. |
| `levels` | Labels of the cascading folder boxes (`Manufacturer`, `Breaker type`, `Amperage`). The wizard shows `max(levels, tree depth)` boxes, at most 3; missing labels read `Level n`. |
| `children[]` | Tree of `{ name, folder, children }`. `folder` is an optional folder name that differs from `name`. Each box lists the children of the choice above it; a level with exactly one child is selected automatically. Boxes are editable, so a folder that is not in the tree can be typed. |

Shipped lines: UL 891 Switchboard (`Eaton` > Draw out IC Breakers / Fixed IC Breakers / Bolted Pressure Switch / Molded Case Breakers > amperage; `ABB` > Fixed IC Breakers > amperage), UL 1008 Transfer Switch (`MC`, `IC`), UL 508A Control Panel (`Service Pedestal` > `Eaton Components`), and `Other (choose the folder yourself)` with no folder and drawing set `Template only`.

### 2.10 projectTypes

The drawing sets (the wizard calls them "Drawing set").

| Field | Meaning |
|---|---|
| `name` | Shown in the drawing set combo; matched against `DefaultProjectType` and `productLines[].projectType`. |
| `description` | Shown beside the combo. |
| `fileNamePattern` | Blank is filled by `Load` from `job.fileNamePattern`, else `{JOB}-{SHEET:3}`. Shipped `{JOB}-E{SHEET:3}` on every set. |
| `drawings[]` | `sheet` (the E-number, `001`, `100`, `300`; a row without one takes the previous row's number plus 1), `description1` (the drawing type, `DWG_TYPE` on the title block and `===` in the `.wdp`), `description2`, `description3`, `section`, `subsection`, `folder` (Project Manager tree folder), `template` (bare file name resolved inside the template folder, for example `STOCKCODE-E001.dwg`, or a full path; blank uses the drawing template). |

Shipped sets: UL 891 Switchboard (11 sheets: 001 cover from `STOCKCODE-E001.dwg`, 002, 100, 200, 201, 300, 301, 302, 400, 500, 501), UL 1008 Transfer Switch (6: 001, 100, 200, 300, 301, 500), UL 508A Control Panel (7: 001, 100, 101, 200, 300, 301, 500), Template only (2: 001 cover, 002 `DRAWING TYPE`, which is what the LSE template project itself lists). The E-number series is inferred from an existing job (`000924-1-E303.dwg`) and is not confirmed.

## 3. Template drawings (DRAWING-#.dwg, STOCKCODE-E001.dwg)

Read-only input. Every sheet is created from the drawing template (`DrawingTemplatePath`, normally `DRAWING-#.dwg`) unless its row names another file; the shipped sets name `STOCKCODE-E001.dwg` for the cover. A bare file name in the `Template` column or in the JSON is resolved inside the template folder: the folder of the template project `.wdp`, or of the drawing template when no template project is set. A `.dwt` works the same way. Requirements the plugin checks or relies on:

| Requirement | What happens otherwise |
|---|---|
| File exists | Validation fails before anything is created (`Drawing template (.dwg/.dwt) was not found.` or `Per-drawing template was not found: ...`). Diagnostics reports each template folder file and each drawing set's missing per-row templates. |
| Contains a WD_M block reference in a layout | The plugin inserts WD_M from `wd_m.dwg`; if that file is not found the log says `WD_M MISSING` and the drawing is still created. AutoCAD Electrical adds WD_M itself when the drawing is first used, with its own defaults. |
| Contains a title block named `ECAD_LSE_CUSTOM_TB_2.0` (per `blockNames`) whose attribute tags match the JSON | Attributes that do not exist are silently skipped; the log reports `title block NOT FOUND` when no block matched. Run `LSEPROJECTDIAG` to see the names and tags the template really has. |

The two template drawings have not been inspected on a workstation; the block and attribute names come from the template's `.wdt`.

## 4. LSE template project (.wdp and sidecars)

Read-only input, `BaseProjectPath`. From the `.wdp` only the `+[n]` options (38 lines: symbol library paths, `ACE_NFPA_MENU.DAT`, footprint libraries, `ACE_PANEL_MENU_NFPA.DAT`, numbering options), the `?[n]` properties (75 lines, indexes 0 to 76 without 13 and 46) and the detected encoding (UTF-8 with BOM) are copied. Its 8 description lines (the placeholders) and its 2 drawings are ignored; its highest description index raises the number of `*[n]` lines written when it exceeds the JSON's. Lines beginning with `*`, `?` or `+` that are not of the form `x[n]value` are collected but never written. A project without a byte order mark is read as UTF-8 and the new project is then written as UTF-8 without BOM.

The two sidecars beside it, `E-CAD CUSTOM PROJECT TEMPLATE 2.wdt` and `E-CAD CUSTOM PROJECT TEMPLATE 2_wdtitle.wdl`, are found case-insensitively and copied byte for byte into the new job as `<job>.wdt` and `<job>_wdtitle.wdl` when `CopyTemplateSidecars` is on. When either is missing, that file is generated from the JSON instead. The template `.aepx` is not read; the plugin writes its own in the same shape.

## 5. wd_m.dwg

Read-only input, used only when a template lacks WD_M. Looked up in `SymbolLibraryFolder` (the vault NFPA library by default) first, then anywhere under `AeData\en-US\Libs`. Not verified: that `wd_m.dwg` is present in the vault NFPA folder or in the 2026 library folders. `LSEPROJECTDIAG` shows the resolved path or `not found`.

## 6. The job folder

Created at `<ProjectsRootFolder>\<product line folder>\<level choices>\<job folder name>\`, for example `C:\_Lake_Shore_E_Vault\Engineering\Custom Sales Order\UL 891 Switchboards\Eaton\Fixed IC Breakers\2500A\001096-1_AEC Contractors\`. `Directory.CreateDirectory` creates the intermediate product line folders when they do not exist yet. Contents after a successful run, with `<job>` the project name (`001096-1`):

| File | Writer | Format |
|---|---|---|
| `<job>.wdp` | `WdpFileService.Write` | Text, UTF-8 with BOM (the template project's encoding; UTF-8 with BOM is also the default without one), CRLF. Groups in the order `*[1..8]` (more when the JSON or the template has a higher index), `+[n]`, `?[n]`, then the drawings; layout in [ARCHITECTURE.md](ARCHITECTURE.md) section 7. Drawing paths relative to this folder. |
| `<job>.wdt` | `WdpFileService.CopySidecar`, else `WriteMapping` | Copied: byte for byte the template's file. Generated: a `;;` comment line, `BLOCK = ECAD_LSE_CUSTOM_TB_2.0`, `DWG_NUMBER = FILENAME`, `DWG_TYPE = DD1`, `PROJECT_NAME = LINE1` .. `SCALE = LINE8` (the same mapping lines as the template's), in the `.wdp` encoding. Read by Title Block Update when no `WD_TB` attribute is on the title block. |
| `<job>_wdtitle.wdl` | `WdpFileService.CopySidecar`, else `WriteLabels` | Copied: the template's file. Generated: `LINE1=PROJECT NAME` .. `LINE8=DWG SCALE`, `LINE9=FUTURE USE` .. `LINE14=FUTURE USE`, no spaces, UTF-8 without BOM, CRLF (the same lines as the template's). Read by the Project Manager for the Descriptions dialog labels. |
| `<job>.aepx` | `WdpFileService.WriteConfiguration` | XML, UTF-8 without BOM: `<ProjectConfiguration Version="1.0">`, `<Project ProjectPath="<job folder>" LocalGUID="<new GUID>" EmxFilePath="" EmxGUID=""/>`, `<Drawings NextID="n+1">` with one `<Drawing FilePath="001096-1-E001.DWG" FileID="i"/>` per sheet (relative path, upper-cased, IDs 1..n). Same shape as the template's `.aepx`. Whether AutoCAD Electrical accepts a pre-written one is unverified; it rewrites the file itself. |
| `<job>-Ennn.dwg` per row | `DrawingFactoryService.Create` | DWG in the current version, from `DRAWING-#.dwg` (or the row's template, `STOCKCODE-E001.dwg` for the cover), with title block and WD_M attributes set and summary info filled. All drawings are flat in this folder; the PM folder column is a Project Manager tree folder, not a disk folder. |
| `LSE_ProjectReport.txt` | `ProjectTransactionService.Execute` | The line-per-step log of the successful run. |

Write order: drawings, `.wdp`, `.wdl`, `.wdt`, `.aepx`, report. Every `.wdt`, `.wdl` and `.aepx` step is skipped when its switch is off.

On rollback every file above that was created (copied sidecars included) is deleted, and the folder is removed if the run created it and it is empty. Product line folders created on the way are left in place. `LSE_ProjectReport.txt` is written only on success, so it never needs removal. The new job folder is created in the vault working folder only; checking it into the vault is a manual step.

## 7. Failure logs

`%APPDATA%\LSE\AutoCADElectricalProjectTools\Logs\ProjectReport_<yyyyMMdd_HHmmss>.txt` holds the same log the wizard showed, followed by `Detail:` and the exception text. Written only when a run fails. Nothing deletes old files here.
