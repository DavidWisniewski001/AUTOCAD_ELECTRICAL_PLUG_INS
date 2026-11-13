# Architecture: LSE Project Tools (AutoCAD Electrical 2026 plugin)

Maintained by DCW. Last reviewed 2026-11-13.

LSE Project Tools is a .NET plugin that runs inside AutoCAD Electrical and creates a complete Lake Shore Electric job in one guided run: the job folder in the right place under `Custom Sales Order` on the vault working folder, every `<job>-Ennn.dwg` sheet from the LSE template drawings with the `ECAD_LSE_CUSTOM_TB_2.0` title block and the WD_M block filled in, the `<job>.wdp` project file with the eight LSE description lines and the template project's settings, the template's own `.wdt` title block mapping and `_wdtitle.wdl` label file renamed for the job, and a `<job>.aepx`. It exists so that a new job starts from the live LSE standard (the template project "CUSTOM PROJECT TEMPLATE 2.0") every time, instead of being copied by hand from an older job. It is the AutoCAD Electrical counterpart of the Inventor add-in in `../inventor-addin` and shares its shape: a wizard, a JSON standard, a logged transaction with rollback.

Related documents: [CODE-REFERENCE.md](CODE-REFERENCE.md), [DATA-STORES.md](DATA-STORES.md), [OPERATIONS.md](OPERATIONS.md), [DEVELOPER-GUIDE.md](DEVELOPER-GUIDE.md), [GLOSSARY.md](GLOSSARY.md).

## 1. What problem it solves

An AutoCAD Electrical project is a text file (`.wdp`) that lists drawings and carries the description lines (eight at LSE) plus some seventy property and option lines, together with the drawings themselves, each of which must contain the invisible WD_M settings block and a title block whose attributes agree with the project. Creating one by hand means copying the template or an old job, placing it in the right vault folder, renaming files, editing the drawing list in the Project Manager, retyping the description lines and running Title Block Update, and the result differs from engineer to engineer. The plugin asks for the job data once and produces all of it, logging each step, and removes everything it created if any step fails.

## 2. Components

One .NET 8 assembly, `LSE.AutoCADElectrical.ProjectTools.dll`, loaded into AutoCAD Electrical with `NETLOAD` or a `.bundle`. Eleven source files, one namespace `LSE.AutoCADElectrical.ProjectTools`.

| Layer | File | Responsibility |
|---|---|---|
| Host integration | `src/Plugin.cs` | Registers the four `LSE...` commands, opens the forms, runs the post-wizard actions (activate project, open first drawing). |
| User interface | `src/NewProjectForm.cs` | The four-step wizard (job and descriptions, vault location, drawing list, review) and its log pane. |
| User interface | `src/ProjectSettingsForm.cs` | Edits every setting; opens diagnostics. |
| Orchestration | `src/ProjectTransactionService.cs` | Validation against the JSON rules, the single mutating run, its log, its rollback. |
| Drawing output | `src/DrawingFactoryService.cs` | Opens a template drawing as a side database, fills title block and WD_M attributes, inserts WD_M when missing, saves the DWG. Also inspects a template for diagnostics. |
| Project files | `src/WdpFileService.cs` | Parses and renders `.wdp`; copies the template's `.wdt` and `.wdl` or generates them; writes `.aepx`. |
| AutoCAD Electrical bridge | `src/AcadeBridgeService.cs` | Sends the activation LISP to the command line; opens a drawing. |
| Configuration | `src/SettingsService.cs` | `SettingsService` (settings.json, vault discovery, path defaults), `TemplateService` (templates JSON and its validation, token expansion, template resolution, job number rule, unit specs). |
| Diagnostics | `src/DiagnosticsService.cs` | Builds the `LSEPROJECTDIAG` report. |
| Vault awareness | `src/VaultProbe.cs` | Shared helper, identical in every LSE plugin (2026-11-12-r7). Reports the host .NET runtime, the installed Vault client and whether it can load in this process, and whether the Vault add-in is logged in; detects read-only (not checked out) files and paths inside the vault working folder; builds the check-in reminder. It only inspects assemblies already loaded and DLL metadata on disk; it never logs in, checks out or checks in. |
| Data shapes | `src/Models.cs` | Settings, template schema (vault, job rules, lines, title block, WD_M, unit specs, product lines, drawing sets), request, result, `.wdp` document model, drawing job and outcome. |

Supporting files:

| File | Purpose |
|---|---|
| `LSE.AutoCADElectrical.ProjectTools.csproj` | SDK-style project, `net8.0-windows`, x64, WinForms. References AutoCAD's managed assemblies from the install when present, otherwise the `AutoCAD.NET` 25.1.0 reference package at compile time only. |
| `config/LseProjectTemplates.json` | The LSE project standard, mirrored from the live template and the vault folder list on 2026-11-10: vault paths, job number rule and naming patterns, the eight description lines, label file extras, title block mapping, WD_M tags, unit-specs choices, product line folder tree, drawing sets with E-numbers. Copied to the output folder. |
| `bundle/LSE.ProjectTools.bundle/PackageContents.xml` | Autoloader manifest for `%APPDATA%\Autodesk\ApplicationPlugins`. |

## 3. Dependency diagram

```
 AutoCAD Electrical 2026 (acad.exe, x64, .NET 8)
 |
 |  NETLOAD or ApplicationPlugins bundle
 v
 Commands (Plugin.cs)
 |-- LSENEWPROJECT ------> NewProjectForm (modal, session context)
 |                           |-- SettingsService.Load (TemplateService.TryLoadVault, DiscoverVaultRoot)
 |                           |-- TemplateService.Load
 |                           |-- TemplateService.JobNumberOk / Expand / SafeName / ComposeUnitSpecs (per step)
 |                           |-- ProjectTransactionService.Validate(templates, request) (step 3 and review)
 |                           '-- ProjectTransactionService.Execute (Create button)
 |                                 |-- VaultProbe.RequireWritable (existing folder only)
 |                                 |-- WdpFileService.Parse (LSE template project)
 |                                 |-- DrawingFactoryService.Create (per drawing, template resolved per row)
 |                                 |     '-- Autodesk.AutoCAD.DatabaseServices (side Database)
 |                                 |-- WdpFileService.Write
 |                                 |-- WdpFileService.CopySidecar (.wdl, .wdt) or WriteLabels / WriteMapping
 |                                 |-- WdpFileService.WriteConfiguration (.aepx)
 |                                 |-- VaultProbe.CheckinReminder (report lines)
 |                                 '-- SettingsService.Save (recent projects)
 |     after the form closes:   AcadeBridgeService.ActivateProject -> Document.SendStringToExecute (LISP)
 |                               AcadeBridgeService.OpenDrawing    -> DocumentManager.Open
 |-- LSEPROJECTACTIVATE --> OpenFileDialog -> AcadeBridgeService.ActivateProject
 |-- LSEPROJECTSETTINGS --> ProjectSettingsForm -> SettingsService.Save
 '-- LSEPROJECTDIAG -----> DiagnosticsService.BuildReport
                              |-- SettingsService.Load, DiscoverVaultRoot
                              |-- VaultProbe.Report, InVaultWorkingFolder
                              |-- TemplateService.Load, ResolveTemplate
                              |-- DrawingFactoryService.Inspect (drawing template)
                              '-- WdpFileService.Parse, ExistingCaseInsensitive (template project and sidecars)
```

External systems touched: the file system (vault working folder, job folder, settings, logs), AutoCAD's database API (in-process), and the AutoCAD Electrical LISP environment (one expression sent to the command line). No database, no network, no Vault API calls: the vault working folder is used as plain file system and the new job folder is not checked in. `VaultProbe` only reads (the read-only attribute of files, the metadata of the Vault client DLL under `Program Files\Autodesk\Vault*\Explorer`, and the connection object of a Vault add-in already loaded in AutoCAD, by reflection).

## 4. End-to-end data flow

1. `LSENEWPROJECT` opens the wizard. On `Shown` it loads `settings.json` (whose blank paths were filled from the `vault` section of the templates JSON and the discovered vault working folder) and the templates JSON. The job number rule, description lines, product lines, drawing sets, unit-specs choices and defaults come from the JSON; paths and option switches come from settings.
2. Step 1 collects the LSE job number (validated against `job.numberPattern`), the product line, the drawing set, the eight description lines (PROJECT NAME and CUSTOMER 1 required), the composed unit specs for line 7, and the IEC project, installation and location codes. Selecting a product line pre-selects the drawing set and the unit type.
3. Step 2 collects the jobs root, the product line folder choices (cascading from the JSON tree, editable), the job folder name (`{JOB}_{CUSTOMER}`) and project name (`{JOB}`), the template project `.wdp`, the drawing template `DRAWING-#.dwg` and the option switches. The names follow the job number and customer until the engineer edits them. The resulting path is `root\<product line folder>\<choices>\<job folder>\<name>.wdp`.
4. Step 3 shows the drawing list derived from the drawing set: Sheet is the E-number from the JSON, file names come from `{JOB}-E{SHEET:3}` until a file name is edited by hand, and the cover row names `STOCKCODE-E001.dwg` as its template.
5. Step 4 renders a review (with the template file resolved per drawing) and runs validation. **Create project** builds a `ProjectRequest` and calls `ProjectTransactionService.Execute`, echoing every log line into the review pane.
6. When the form closes with a result, `Commands.NewProject` sends the activation LISP and, if asked, opens the first drawing. Checking the job folder into the vault is left to the engineer.

## 5. The mutating transaction

`ProjectTransactionService.Execute` is the only code that creates or deletes anything on disk. Order of operations:

| Step | Action | On failure |
|---|---|---|
| 1 | `Validate(templates, request)`: job number against `job.numberPattern`, title and every required line, project name, rooted folder, drawing template exists, template project exists when set, at least one drawing, valid and unique file names, sheet values, per-drawing templates resolved in the template folder exist. | Throws before anything is touched. |
| 2 | Job folder: create it with its missing parent folders (the job folder is remembered for rollback). An existing folder is reused only if it holds no `<name>.wdp`, no `<name>_wdtitle.wdl`, `<name>.wdt` or `<name>.aepx` (matched case-insensitively), its `LSE_ProjectReport.txt` is not read-only (`VaultProbe.RequireWritable`; a Vault file that is not checked out is read-only), and it is either empty or `AllowExistingFolder` is set. | Throws before anything is written. |
| 3 | Template project: parse it and copy its `+[n]` options, `?[n]` properties and text encoding into the new document; raise the description line count to its highest index if that exceeds the JSON's. Without a template project nothing is copied and the log says AutoCAD Electrical defaults will apply. | Throws (file missing or unreadable). |
| 4 | Pre-check that none of the target DWG paths exist. | Throws. |
| 5 | Locate the WD_M library block (`wd_m.dwg`) for templates that lack WD_M. | Logged, not fatal. |
| 6 | For every drawing: resolve its template (row template in the template folder, else the drawing template), build the title block value map and the WD_M value map, call `DrawingFactoryService.Create`, record the output path, log the template file and counts of attributes set, add the drawing to the `.wdp` document. | Throws; every DWG written so far is deleted. |
| 7 | Write `<name>.wdp` with at least `*[1..8]`. | Throws; rollback as above. |
| 8 | `<name>_wdtitle.wdl` when `WriteWdlFile` is set: copy the template project's `.wdl` renamed when `CopyTemplateSidecars` is set and one exists; otherwise generate it from the JSON. | Throws; rollback. |
| 9 | `<name>.wdt` when `WriteWdtFile` is set: copy the template project's `.wdt` renamed when `CopyTemplateSidecars` is set and one exists; otherwise generate it when the JSON names at least one title block, else log that it was skipped. | Throws; rollback. |
| 10 | Write `<name>.aepx` when `WriteAepxFile` is set: the template's shape with one `<Drawing>` per sheet. | Throws; rollback. |
| 11 | Log `Status: SUCCESS`, then the Vault check-in reminder (`VaultProbe.CheckinReminder`: every created file and the job folder that lie inside the vault working folder, with "Add to Vault" advice; nothing when the jobs root is outside the vault), write `LSE_ProjectReport.txt` into the job folder (overwriting one already there in a reused folder), push the `.wdp` path onto `RecentProjects` (25 kept) and save settings. | Still inside the `try`: if the report or `settings.json` cannot be written, the whole project is rolled back even though the log already says `Status: SUCCESS`. A report written before a failed settings save is not in the created list and stays, so the folder is then not removed. |

Rollback deletes every file recorded in the created list in creation order (copied sidecars included), removes the job folder if this run created it and it is now empty (parent product line folders created on the way stay), writes the full log to `%APPDATA%\LSE\AutoCADElectricalProjectTools\Logs\ProjectReport_<yyyyMMdd_HHmmss>.txt`, and rethrows as `InvalidOperationException` whose message is the whole log. Files are written and copied through a `.tmp` sibling and `File.Move(..., true)`, so a half-written `.wdp` is never left behind.

## 6. Drawing creation model

`DrawingFactoryService.Create` never touches the active document. It opens the template drawing (`DRAWING-#.dwg`, `STOCKCODE-E001.dwg`, or any `.dwg`/`.dwt`) into a side `Database` (`ReadDwgFile` with `OpenForReadAndAllShare`, then `CloseInput`), makes it the working database for the duration of the call, and restores the previous working database in `finally`.

Inside one transaction it walks every block table record whose `IsLayout` is true (model space and every paper space layout) and every `BlockReference` in them, resolving dynamic blocks to their defining block name:

- A reference whose name equals the WD_M block name gets the WD_M value map applied. Only attributes that exist on the block are written, and only when the value differs.
- A reference that matches the title block rule gets the title block value map applied the same way. The rule is: name does not start with `WD_`, and either it matches one of the configured wildcard names (`*` and `?`, case-insensitive; `ECAD_LSE_CUSTOM_TB_2.0` in the shipped JSON) or, when no names are configured, the block carries the sheet attribute.
- If no WD_M was found, WD_M values are wanted and a `wd_m.dwg` library file exists, the block definition is imported with `Database.Insert` (or reused if the definition is already present), a reference is placed at the origin of model space, and attribute references are built from the definition with the WD_M values substituted.

After the transaction commits, drawing summary information is set (title = first drawing description, subject = project title, author = Windows user, comment with a timestamp) and the database is saved with `SaveAs` to the output path in the current DWG version.

Not verified on a workstation: that `Database.Insert` inside an open transaction behaves identically in AutoCAD Electrical 2026, that `SaveAs` on a side database yields a `.dwg` AutoCAD Electrical opens without a repair prompt, and that the two LSE template drawings carry WD_M and the `ECAD_LSE_CUSTOM_TB_2.0` block as the template `.wdt` implies. See [DEVELOPER-GUIDE.md](DEVELOPER-GUIDE.md) Known Gaps.

## 7. The .wdp model

`WdpDocument` holds three sorted integer maps (descriptions `*[n]`, options `+[n]`, properties `?[n]`), a drawing list and the text encoding. `WdpFileService.Render` writes, in order:

```
*[1]..*[max(8, highest index)]         description lines, blanks included (8 = the JSON's highest index, or the template's if higher)
+[n]...                                 project options, ascending (library paths, icon menus, numbering options)
?[n]...                                 project properties, ascending
per drawing:
  =====SUB=<folder>                     only when a folder is set
  ====REF                               only when reference-only
  ===<description 1..k>                 up to the last non-blank of the three
  =<section>                            only when set
  ==<subsection>                        only when set
  <path relative to the .wdp folder>    absolute when outside that folder
```

The parser is prefix-based, so any ordering of the groups and of the marker lines before a drawing path is accepted on read. The group order `*`, `+`, `?` and the minimum of eight description lines are what AutoCAD Electrical 2026 wrote in the LSE template project `E-CAD CUSTOM PROJECT TEMPLATE 2.wdp` on the vault (`*[1..8]`, `+[1..38]`, `?[0..76]`, then the two drawings); the smoke test of 2026-11-10 parsed that file and re-rendered it byte for byte, which fixes the order, the CRLF line ends and the encoding. The E-CAD Doctor plugin (`../acade-project-doctor`) treats the same order as the standard. V1 wrote `*`, `?`, `+` with a minimum of twelve lines. The order of the marker lines before a drawing path (descriptions before section codes) is still a choice, since the template uses neither sections nor folders.

Encoding: new projects are written as UTF-8 with BOM (`new UTF8Encoding(true)`), which is what the template carries. When a template project is given, its detected encoding is reused: a UTF-16 project keeps UTF-16, a project without a BOM is read and written as UTF-8 without BOM.

## 8. Process, threading and context

- All commands run on AutoCAD's main thread. `LSENEWPROJECT` and `LSEPROJECTACTIVATE` are `CommandFlags.Modal | CommandFlags.Session` so they can open documents and send command-line input after the modal form closes. The settings and diagnostics commands are `Modal` only.
- The wizard runs the transaction synchronously on the UI thread and refreshes the review pane after each log line. AutoCAD is unresponsive for the duration (a few seconds per drawing).
- Project activation is asynchronous by nature: the LISP expression is queued with `SendStringToExecute` and runs after the command ends. The plugin cannot read its result; it prints to the command line instead.
- No COM, no ADODB, no Vault API calls. The only unmanaged dependency is AutoCAD's own managed API. `VaultProbe` uses reflection on assemblies the Vault add-in has already loaded and reads DLL metadata with `System.Reflection.Metadata`; nothing from the Vault SDK is referenced at compile time.
- .NET: the DLL targets `net8.0-windows`. AutoCAD 2026.1.2 and Vault 2026.4 moved to .NET 10 together (August 2026); a .NET 8 plugin still loads in a .NET 10 host. `LSEPROJECTDIAG` shows the host runtime and whether the installed Vault client matches it.

## 9. Design decisions

| Decision | Reason |
|---|---|
| Write the `.wdp` ourselves instead of calling AutoCAD Electrical's LISP writer | The documented file format is stable and testable without AutoCAD; the LISP functions seen in forums (`c:wd_proj_wdp_write`) are not in the official reference. The template written by AutoCAD Electrical 2026 gives a byte-exact reference to test against. |
| Copy `+[n]`/`?[n]` from the LSE template project rather than invent them | Those lines encode site-specific settings (vault symbol libraries, `ACE_NFPA_MENU.DAT`, footprint libraries, numbering rules) whose indexes are only partly documented. Copying the live template is safer than defaults, and keeps every job on the same libraries. |
| Copy the template's own `.wdt` and `_wdtitle.wdl` instead of generating them | The template files are the standard; a renamed copy cannot drift from it. Generation from the JSON remains as the fallback (and produces the same mapping and label lines) for a template folder without sidecars. |
| Mirror the vault folder tree (`productLines`) in the JSON | The job must land where the vault expects it (`Custom Sales Order\UL 891 Switchboards\Eaton\Fixed IC Breakers\2500A`). Cascading, editable pickers keep the choice consistent without blocking a new amperage or manufacturer. |
| Job number as a first-class field rather than a description line | The LSE template has no job line among its eight; the job number drives the folder, project and file names and has its own rule (`NNNNNN-R`), so it lives in `ProjectRequest.JobNumber` and `job.numberPattern`, not in `*[n]`. |
| `.aepx` written in the template's shape | The template ships one, and the E-CAD Doctor writes the same shape, so a new job looks like a template copy from the first open. Acceptance by AutoCAD Electrical is unverified; it rewrites the file itself. |
| Default every path from the `vault` section and the discovered working folder | A workstation with the vault checked out needs no configuration; a path written for `C:` is remapped to the drive that holds `_Lake_Shore_E_Vault`. The AutoCAD-folder discovery remains only as a fallback. |
| Fill title blocks directly and also provide a `.wdt` | Direct fill gives a correct drawing immediately; the `.wdt` lets the engineer rerun Title Block Update later after adding drawings. |
| Activation through one configurable LISP expression | `c:wd_makeproj_current` comes from an Autodesk forum answer, not the API reference. Keeping it in settings lets a site fix the name without a rebuild, and `boundp` keeps a missing function harmless. |
| Side database instead of opening each drawing | Faster, no document switching, no prompts, and the active document is never modified. |
| JSON standard beside the DLL with a settings override | Same pattern as the catalog and footprint tools: one shared copy on the vault keeps every workstation identical. |
| Refuse a Vault-locked target instead of checking it out | The other AutoCAD plugins except Catalog Tools never talk to Vault; a read-only file in the working folder is the visible sign of "not checked out", and refusing before the first write keeps rollback simple. The engineer adds the new job to Vault afterwards, guided by the reminder in the report. |
| Rollback by deleting what was created, not by snapshotting | A new project has no prior state to restore; deleting the recorded files and the new folder is complete. |
| Two code densities | Services and small forms are compact, one member per line; the wizard and the AutoCAD database code are expanded. This matches `../STYLE-GUIDE.docx` and the sibling projects. |

## 10. Relationship to the sibling projects

`../acade-catalog-tools` (Catalog Tools) and `../inventor-addin` (Footprint Tools) share the settings pattern (`SettingsService.Load`/`Save` with a `.tmp` move), the transaction pattern (line-per-step log, `Status: SUCCESS`, report file, rollback) and the form conventions. `../acade-project-doctor` (LSE E-CAD Doctor, `LSEDOCTOR`) is the repair counterpart: it checks existing jobs against the same LSE template, treats the same `.wdp` group order as the standard and writes the same `.aepx` shape, and the wizard's log points at it when a job is created without the template project. No assembly is shared; each project ships alone. The project tools do not read the catalog database.
