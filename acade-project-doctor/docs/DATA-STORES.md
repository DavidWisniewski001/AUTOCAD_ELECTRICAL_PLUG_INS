# Data stores: LSE_Doctor

Maintained by DCW. Last reviewed 2026-11-13.

Every file, folder, setting and generated artefact the doctor reads or writes: where it is, its format, who owns it, and what breaks if it is wrong. The doctor has no database, no registry keys and no lock files. It uses plain files and one AutoCAD system variable. It does not talk to the Vault server; it only reads the read-only attribute that the Vault client sets on files that are not checked out (section 9).

Related documents: [ARCHITECTURE.md](ARCHITECTURE.md), [CODE-REFERENCE.md](CODE-REFERENCE.md), [OPERATIONS.md](OPERATIONS.md).

## 1. Inventory

| # | Store | Location | Access | Owner |
|---|---|---|---|---|
| 1 | `settings.json` | `%APPDATA%\LSE\LSE_Doctor\` | read on every form open; written by Settings > Save, by Scan, and when a project is remembered | each user |
| 2 | Rules JSON `LseDoctorRules.json` | next to the DLL, or the path in settings | read on every examine session | LSE standards (shareable) |
| 3 | Template `.wdp`, `.wdt`, `_wdtitle.wdl` (the standard) | Vault: `C:\_Lake_Shore_E_Vault\CAD Standards\AutoCAD\ECAD Standards\Project Templates\CUSTOM TEMPLATES\CUSTOM PROJECT TEMPLATE 2.0\` | read only | LSE standards |
| 4 | Bundled standard | `standard\` next to the DLL (source: `config\standard\`) | read only, fallback | shipped with the build |
| 5 | Template drawing `DRAWING-#.dwg` | same Vault folder as the template `.wdp` | read only (source of `WD_M`; the wire-layer standard: its layer table and its `WD_M` wire-layer attributes) | LSE standards |
| 6 | Project `.wdp` | the project folder | read, edited line by line | the project |
| 7 | Project `<name>.wdt` | project folder | read, edited or created | the project |
| 8 | Project `<name>_wdtitle.wdl` | project folder | read, edited or created | the project |
| 9 | Project `<name>.aepx` | project folder | existence check; optionally created | the project (AutoCAD Electrical normally writes it) |
| 10 | Project drawings `*.dwg` | wherever the `.wdp` lists them | read as side databases; `WD_M` inserted, `WD_M` attribute values set, layers created, repaired or purged, then saved in place (section 5) | the project |
| 11 | Backup folder | `<project folder>\_LSE_DOCTOR_BACKUP\<yyyyMMdd_HHmmss>\` | written before any change; pruned | doctor |
| 12 | `LSE_DoctorReport.txt` | project folder | appended on every successful repair | doctor |
| 13 | Failure log `DoctorRepair_<stamp>.txt` | `%APPDATA%\LSE\LSE_Doctor\Logs\` | written on failure only | doctor |
| 14 | `*.tmp` | beside the file being written | transient; moved over the target | doctor |
| 15 | System variable `USERS1` | AutoCAD session | cleared, set by the active-project LISP, read, cleared | doctor |
| 16 | Saved report text | wherever the user picks in "Save as..." | written by the text viewer | user |

## 2. settings.json

`%APPDATA%\LSE\LSE_Doctor\settings.json`. JSON, indented, written through a `.tmp` file. Property names are the C# names with exact case (the settings reader is case-sensitive, unlike the rules reader). If the file is missing or cannot be parsed, defaults are used and the problem is not reported; the next Save replaces the file.

| Property | Type | Default | Meaning |
|---|---|---|---|
| `TemplateProjectPath` | string | `C:\_Lake_Shore_E_Vault\CAD Standards\AutoCAD\ECAD Standards\Project Templates\CUSTOM TEMPLATES\CUSTOM PROJECT TEMPLATE 2.0\E-CAD CUSTOM PROJECT TEMPLATE 2.wdp` | The standard. If blank or missing on disk, the bundled copy is used. |
| `RulesConfigPath` | string | blank, becomes `<DLL folder>\LseDoctorRules.json` on load | Rules JSON. A path that does not exist gives built-in defaults. |
| `ProjectsRootFolder` | string | `C:\_Lake_Shore_E_Vault\Engineering\Custom Sales Order` | Default folder for Browse and for the scan. Saved by every scan. |
| `CheckDrawings` | bool | true | Initial state of "Check every drawing" in `LSEDOCTOR`. |
| `InsertMissingWdm` | bool | true | A missing `WD_M` is a recommended repair (needs a template drawing). |
| `ScanChecksDrawings` | bool | false | Initial state of the scan's drawing check. Saved by every scan, so the last scan's choice overwrites what was set in `LSEDOCTORSETTINGS`. (The `LSEDOCTOR` check box, by contrast, is never written back to `CheckDrawings`.) |
| `ReactivateAfterRepair` | bool | true | Send the reload LISP after the active project is repaired. |
| `ShowReportAfterRepair` | bool | true | Show the repair log window after each repair in `LSEDOCTOR`. |
| `BackupFolderName` | string | `_LSE_DOCTOR_BACKUP` | Folder created inside each project folder. Blank on load becomes the default. |
| `BackupsToKeep` | int | 10 | Newest backup folders kept per project. Below 1 on load becomes 1. |
| `ActiveProjectLisp` | string | `(setvar "USERS1" (if (and (boundp 'ace_getactiveproject) (ace_getactiveproject)) (ace_getactiveproject) ""))` | Run with `Editor.Command` to find the active project. |
| `ActivateProjectLisp` | string | `(if (boundp 'c:wd_makeproj_current) (progn (c:wd_makeproj_current "{WDP}") (princ ...)) (princ ...))` | Reload expression. `{WDP}` is replaced by the project path with `/` separators. Full default text is in `AppSettings`. |
| `RecentProjects` | string list | empty | Most recent first, at most 25, case-insensitive unique. Fills the project drop-down. |

What breaks if wrong: a wrong `TemplateProjectPath` silently switches to the bundled standard (the standard label turns red). A wrong `RulesConfigPath` silently gives built-in defaults, which equal the shipped rules file. A broken `ActiveProjectLisp` means the form opens without a preset project. A broken `ActivateProjectLisp` means the project is not reloaded.

## 3. LseDoctorRules.json

Shipped as `config\LseDoctorRules.json`, copied to the output folder. Parsed by `RulesService.Load` with case-insensitive names, comments and trailing commas allowed. Missing keys take the class defaults shown. The shipped file sets every key to the same value as the class default.

| Key | Type | Default | Meaning |
|---|---|---|---|
| `descriptionLineCount` | int | 0 | How many `*[n]` lines every project must have. 0 means the template's highest index (8). |
| `fillBlankDescriptionsWithPlaceholder` | bool | true | A missing `*[n]` line gets the template placeholder text; if false it gets an empty value. |
| `enforceOptions` | bool | true | A differing `+[n]` is a recommended replacement. If false it is listed, unticked. |
| `enforceProperties` | bool | true | Same for `?[n]`. |
| `pathListMode` | string | `replace` | `replace`: list becomes exactly the template list. `merge`: template paths first, then the project's extra paths. Any other value is rejected on load. |
| `pathListOptions` | int list | `[1, 3]` | `+[n]` that are `;`-separated lists. |
| `pathOptions` | int list | `[2, 4]` | `+[n]` that are single paths. |
| `protectedOptions` | int list | `[]` | `+[n]` reported but not recommended. |
| `protectedProperties` | int list | `[]` | `?[n]` reported but not recommended. |
| `requireLabelFile` | bool | true | Missing `_wdtitle.wdl` is an Error and the copy is recommended. If false, Info and unticked. |
| `enforceLabels` | bool | true | Differing `LINEn` labels are recommended replacements. |
| `requireMappingFile` | bool | true | Same for the `.wdt`. |
| `enforceMappings` | bool | true | Differing mappings and the `BLOCK` name are recommended repairs. |
| `requireProjectConfigurationFile` | bool | false | If true a missing `.aepx` is an Error and creation is recommended. |
| `wdmBlockName` | string | `WD_M` | Block every drawing must carry. Blank becomes `WD_M`. |
| `titleBlockNames` | string list | `[]` | Title block name patterns every drawing should carry. Empty means the names on the template `.wdt` `BLOCK` line. |
| `labels` | object | four entries | Friendly names for grid rows: `"+1"` Schematic symbol library paths, `"+2"` Schematic icon menu (.dat), `"+3"` Panel footprint library paths, `"+4"` Panel icon menu (.dat). Keys are the prefix and the number; `*` names come from the template `.wdl`. |
| `checkWireLayers` | bool | true | Compare each drawing's wire layers and `WD_M` wire-layer attributes with the template drawing. Only acts when "Check every drawing" is on and the template drawing is readable. If false, no wire-layer log line is written at all. |
| `enforceWireLayers` | bool | true | Differing `WD_M` wire-layer values ("Set WD_M value") and off-standard layer properties ("Repair layer") are recommended. If false they are listed, unticked. "Create layer from template" is recommended regardless. |
| `purgeUnusedWireLayers` | bool | true | A wire layer the template does not have, with nothing drawn on it, is a recommended "Purge layer". A non-standard wire layer that carries entities is only reported in either case. |
| `wireLayerAttributes` | string list | `WIRELAYS`, `WIRENO_LAY`, `WIRECOPY_LAY`, `WIREFIXED_LAY`, `WIREREF_LAY`, `FAN_INOUT_LAYS` | `WD_M` attribute tags that name wire layers. A tag is compared only when the template's `WD_M` carries it. Names come from the Autodesk help page "About the WD_M block", where `WIRELAYS` is "valid wire layer names where "" = all valid (comma-delimited)". |
| `wireLayerListAttributes` | string list | `WIRELAYS`, `FAN_INOUT_LAYS` | Which of the attributes hold a comma-delimited list (`,` or `;`; wildcards `*` and `?` allowed) compared as an unordered case-insensitive set. The others hold one layer name, compared trimmed and ignoring case. A blank `WIRELAYS` means every layer to AutoCAD Electrical; the doctor then finds the drawing's wire layers through xdata and the single-name attributes only. |
| `wireLayerXdataApps` | string list | `VIA_WD*` | Registered-application name patterns. Xdata under a matching name on a layer table record marks the layer as an AutoCAD Electrical wire type. Autodesk does not document the application name; this pattern is a guess to be confirmed on a workstation. |
| `neverPurgeLayers` | string list | `0`, `DEFPOINTS` | Layer names never offered for purge, whatever else says so. |

What breaks if wrong: invalid JSON or a bad `pathListMode` stops every examine with the message from the loader. Putting a wrong index in `pathListOptions` makes a plain value be compared as a path list. An empty `wireLayerXdataApps` list disables the xdata test, so a drawing with a blank `WIRELAYS` reports no wire layers of its own and the "no wire-type data" difference is never raised. A wrong pattern in `wireLayerListAttributes` makes a single layer name be compared as a list (harmless) or a list be compared as one string (every drawing with a different list order is reported).

## 4. The LSE template files

The standard is three text files in the template folder. A copy of each is bundled in `config\standard\`. Counts below are for the bundled copy and match the verified `LSEDOCTORDIAG` expectation.

### `E-CAD CUSTOM PROJECT TEMPLATE 2.wdp`

UTF-8 with BOM, CRLF line endings (verified by the byte-identical round trip). Group order: `*`, `+`, `?`, then the drawing list.

| Group | Lines | Content |
|---|---|---|
| `*[1]` to `*[8]` | 8 | Description placeholders: `PROJECT NAME`, `#####`, `CUST 1`, `CUST 2`, `CUST 3`, `UNIT TYPE OR CUSTOMER DESIGNATION`, `SWBD, ####A, ###/###VAC, ##KA@###V, N#?`, `NTS`. |
| `+[1]` to `+[38]` | 38 | `+[1]` schematic library path list (`...\Symbol Library\NFPA;...\NFPA\1-;...`); `+[2]` `...\dat MENU\ACE_NFPA_MENU.DAT`; `+[3]` footprint library list (`...\Footprint Library\panel;...\LSE E-Cad Footprints;`); `+[4]` `...\dat MENU\ACE_PANEL_MENU_NFPA.DAT`; `+[5]` to `+[38]` numbering and format options, compared as opaque values. |
| `?[0]` to `?[76]` | 75 | Project properties (tag format `%F%N`, attribute names such as `WIRENO`, `XREF`, `DESC`, `LOC`). Indexes 13 and 46 do not exist in the file. |
| Drawing list | 2 drawings | `STOCKCODE-E001.dwg`, then `===DRAWING TYPE` and `DRAWING-#.dwg`. |

All vault paths start `C:\_Lake_Shore_E_Vault\CAD Standards\AutoCAD\ECAD Standards\`. Meaning of the individual `+[n]` and `?[n]` indexes beyond `+[1]` to `+[4]` is not documented in the tree.

### `E-CAD CUSTOM PROJECT TEMPLATE 2.wdt`

Title block mapping. Starts with a UTF-8 BOM and a long `;;` comment header (copied from AutoCAD Electrical's default). Eleven mapping lines in the form `ATTRIBUTE = CODE`:

```
BLOCK = ECAD_LSE_CUSTOM_TB_2.0
DWG_NUMBER = FILENAME
DWG_TYPE = DD1
PROJECT_NAME = LINE1
QUOTE_NUMBER = LINE2
CUSTOMER = LINE3
CUSTOMER_2 = LINE4
CUSTOMER_3 = LINE5
CUSTOMER_DESIGNATION = LINE6
SWBD_AMP_VOLT_KAIC_NEMA = LINE7
SCALE = LINE8
```

### `E-CAD CUSTOM PROJECT TEMPLATE 2_wdtitle.wdl`

Fourteen lines `LINE1=PROJECT NAME`, `LINE2=QUOTE#`, `LINE3=CUSTOMER 1`, `LINE4=CUSTOMER 2`, `LINE5=CUSTOMER 3`, `LINE6=UNIT DESIGNATION`, `LINE7=UNIT SPECS`, `LINE8=DWG SCALE`, then `LINE9` to `LINE14` all `FUTURE USE`.

### Real template folder

`reference\CUSTOM PROJECT TEMPLATE 2.0\` at the root of this workspace (moved there from a doubled `CUSTOM PROJECT TEMPLATE 2.0\CUSTOM PROJECT TEMPLATE 2.0\` folder in the 2026-11-10 cleanup) holds the real files: `DRAWING-#.dwg`, `E-CAD CUSTOM PROJECT TEMPLATE 2.aepx`, `.wdp`, `.wdt`, `_wdtitle.wdl`, `STOCKCODE-E001.dwg` and a `_V` folder (Vault version files). The doctor does not compare the bundled copy with the real files at build or run time; refresh `config\standard\` by hand when the template changes (see the [developer guide](DEVELOPER-GUIDE.md)). Drawings are never bundled, so `WD_M` insertion and the whole wire-layer check need the Vault folder. The wire layers of `DRAWING-#.dwg` (names, colours, linetypes, lineweights, xdata) are not listed in the tree; the examine log line `Wire layers: standard is DRAWING-#.dwg with N wire layer(s): ...` shows what the doctor read.

## 5. Project files the doctor reads and writes

### Project `.wdp`

Edited line by line; see section 4 for the line types. Drawing list syntax as parsed by `TextFileService.Drawings`:

| Line starts with | Meaning |
|---|---|
| `*[n]` | description |
| `+[n]` | option |
| `?[n]` | property |
| `=====SUB=` | Project Manager folder for the next drawing |
| `====REF` | next drawing is reference only |
| `===` | drawing description (several allowed) |
| `==` | subsection |
| `=` | section |
| anything else non-blank | a drawing path (relative to the `.wdp` folder or absolute) |

Edits the doctor makes: add a line, replace a line's text after the `]`, delete a drawing entry (marker lines and path). It never reorders, re-encodes or reflows. If the file did not end with a line break, one is added on write.

### Project `.wdt` and `_wdtitle.wdl`

Edited the same way (mapping lines `ATTR = CODE`; label lines `LINEn=text`). A missing file is created by copying the standard's file byte for byte. The doctor finds existing files ignoring case, so a creator-written `_WDTITLE.WDL` is found, but a copy it creates is named `<project>_wdtitle.wdl`.

### Project `.aepx`

Only the existence is checked. Creation is optional and writes the minimal XML described in [CODE-REFERENCE.md](CODE-REFERENCE.md#projectconfigurationservice-static). AutoCAD Electrical 2024 and later is said (in the finding text) to write this file itself when the project is opened.

### Drawings

Inspection opens each DWG read-only as a side database and reads: the top-level block references of every layout (for `WD_M` and title blocks), the attribute values of the first `WD_M`, every layer table record (colour, linetype, lineweight, plot flag, on/off/frozen/locked, description, xdata regapp names) and a count of entities per layer over every block table record. The template drawing is read the same way.

Repair is the only place the doctor changes a binary file. All ticked actions for one DWG are applied in one transaction and the file is rewritten once with `SaveAs` in the current DWG version of the host. What changes inside the DWG, in the order applied:

| Step | Inside the DWG |
|---|---|
| Insert `WD_M` | Block table: the `WD_M` definition is cloned from the template if absent. Model space: one new `WD_M` block reference at the template's insertion point with the template's attribute values. |
| Create layer | Layer table: a new record cloned from the template's record with `WblockCloneObjects`; the linetype table and regapp table receive whatever the clone needs. |
| Repair layer | The existing layer table record: colour, lineweight, plot flag, description (if the template's is not blank), linetype (linetype table record cloned from the template if absent), xdata (regapp table records added as needed). Entities on the layer are untouched. |
| Set `WD_M` value | One attribute reference on the first `WD_M` block reference. |
| Purge layer | `CLAYER` moved to `0` if it pointed at the layer; the layer table record erased after `Database.Purge` agrees it is unreferenced. |

Nothing else in the DWG is touched. Layer on/off/frozen/locked flags are never written. The DWG version may change on save (see Known Gaps in the [developer guide](DEVELOPER-GUIDE.md#9-known-gaps)).

## 6. Backups

`<project folder>\<BackupFolderName>\<yyyyMMdd_HHmmss>\`, one folder per repair run, containing a plain copy of each file the run was going to change, by file name only (no sub-folders). Files the run creates are not in the backup. Read-only attributes on backup copies are cleared. Pruning keeps the newest `BackupsToKeep` folders by name and deletes the rest, including their files. Nothing else reads the backups: recovery is by hand ([OPERATIONS.md](OPERATIONS.md#recovering-by-hand)). The folder lies inside the project folder, so a Vault check-in of the project folder will include it unless it is excluded.

Every DWG that has a ticked drawing action is backed up in full before the run, so the backup folder can hold many megabytes when wire-layer repairs are ticked across a project. A purged layer exists nowhere else after the run: the only way to get it back is the backup copy of that DWG (or Vault history).

Limits: two files with the same name from different folders (for example two drawings in different sub-folders) get a `(2)` suffix on the second copy; match the suffix to its folder by the run log before restoring.

## 7. Reports and logs

| File | Written when | Content |
|---|---|---|
| `<project folder>\LSE_DoctorReport.txt` | after every successful repair (also partial) | The run log appended, followed by a blank line, including the Vault check-in reminder lines. UTF-8 without BOM. Pruning messages come after the file is written and are not in it. |
| `%APPDATA%\LSE\LSE_Doctor\Logs\DoctorRepair_<yyyyMMdd_HHmmss>.txt` | only when a repair fails after the backup stage | The run log followed by `Detail: <exception text>`. Nothing deletes old files here. |
| Text from the "Report" button | when the user saves it | `DiagnosisService.Render` output. |

See [OPERATIONS.md](OPERATIONS.md#reading-the-log) for the line-by-line format.

## 8. The USERS1 system variable

`TryGetActiveProject` sets `USERS1` to an empty string, runs the active-project LISP (which stores the result in `USERS1`), reads the value and clears it again. Any other tool or LISP that uses `USERS1` at the same moment would conflict. Not verified on a workstation.

## 9. Vault working folder

Projects and the template normally live under `C:\_Lake_Shore_E_Vault\`, the Autodesk Vault working folder. The Vault client marks files that are not checked out read-only. The doctor reads only that attribute; it never logs in to Vault, never checks files in or out, and holds no Vault state.

| Check | Where | Effect |
|---|---|---|
| Read-only refusal | `RepairService.Execute` through `VaultProbe.RequireWritable` | Before the backup folder is created, every file the repair would edit (the `.wdp`, an existing `.wdt` or `.wdl` being edited, every DWG with a ticked drawing action) is tested. Any read-only one stops the run; the message lists them all and ends "Nothing was changed." |
| Check-in reminder | end of a successful run, `VaultProbe.CheckinReminder` | Lists the edited and created files inside the working folder that are not checked in yet. The `_LSE_DOCTOR_BACKUP` folder is not listed; leave it out of the check-in. |
| Working-folder test | `VaultProbe.InVaultWorkingFolder` | A path containing `\_Lake_Shore_E_Vault\` (or under `C:\_Lake_Shore_E_Vault\`). Decides the wording of the refusal and the Get Latest Version hints in `LSEDOCTORDIAG`. |
| Diagnostics | `LSEDOCTORDIAG` | Host .NET, installed Vault client (folder, file version, target .NET, whether it can load in this host) and the Vault add-in login. Information only. |

The scan skips folders named `_V` (Vault version folders). Not verified against a live Vault.
