# Architecture: LSE_Doctor (AutoCAD Electrical 2026 plugin)

Maintained by DCW. Last reviewed 2026-11-10.

LSE_Doctor is a .NET 8 plugin that runs inside AutoCAD Electrical 2026. It compares an existing project with the LSE custom project template, lists every difference as a finding, and repairs the ticked ones inside a transaction that backs up first, verifies afterwards and rolls back on failure. It exists because projects drift from the LSE standard (copied from old jobs, created by hand, edited in the Project Manager) and nobody can see the drift without opening the text files by hand. It is the repair-shop counterpart of `../../acade-project-creator`, which creates new projects.

Related documents: [CODE-REFERENCE.md](CODE-REFERENCE.md), [DATA-STORES.md](DATA-STORES.md), [OPERATIONS.md](OPERATIONS.md), [DEVELOPER-GUIDE.md](DEVELOPER-GUIDE.md), [GLOSSARY.md](GLOSSARY.md).

## 1. The problem

An AutoCAD Electrical project is a set of files that must agree with each other:

- the `.wdp` project file: description lines `*[n]`, library and option lines `+[n]`, property lines `?[n]`, and the drawing list;
- the `<project>.wdt` title block mapping (which title block attribute shows which project value);
- the `<project>_wdtitle.wdl` labels (the prompts in the Project Manager description dialog);
- the `<project>.aepx` project configuration;
- inside every drawing, the `WD_M` drawing-settings block, the LSE title block, and the wire layers: the layer table entries AutoCAD Electrical treats as wires, and the `WD_M` attributes (`WIRELAYS` and the others) that name them.

The LSE standard for all of this lives in one real project, the LSE custom project template (version 2.0). The doctor does not hard-code the standard. It reads the template's `.wdp`, `.wdt` and `_wdtitle.wdl` at run time, and, when drawings are checked, the template drawing `DRAWING-#.dwg` next to them, so changing the template changes the standard. A rules JSON says only how strictly to compare.

## 2. Components

One assembly, `LSE_Doctor.dll`, one namespace `LSE.AutoCADElectrical.ProjectDoctor`, loaded with `NETLOAD` or a `.bundle`. Fifteen source files. The source folder is still named `acade-project-doctor` and the namespace still says `ProjectDoctor`; only the product name, assembly, project file, bundle folder and settings folder say `LSE_Doctor`.

| Layer | File | Responsibility |
|---|---|---|
| Host integration | `src/Plugin.cs` | `Plugin` (empty `IExtensionApplication`) and `Commands` with the four `LSEDOCTOR...` command methods. After a dialog closes, sends the reload LISP. |
| User interface | `src/DoctorForm.cs` | Single-project screen: pick a project, examine, review the findings grid, tick repairs, repair. |
| User interface | `src/ScanForm.cs` | Folder scan: find every `.wdp` under a root, examine each, batch-apply recommended repairs, open one in the doctor form. |
| User interface | `src/DoctorSettingsForm.cs` | Edits `AppSettings`; opens diagnostics. |
| User interface | `src/TextReportForm.cs` | Read-only text viewer with Save as, Copy and Close. |
| Orchestration | `src/DiagnosisService.cs` | `DiagnosisService.Examine`: compares a project with the standard and produces `Finding` objects. Also renders the text report. |
| Orchestration | `src/RepairService.cs` | `RepairService.Execute`: the one mutating run (backup, edit, verify, report, prune, rollback). Also `ProjectConfigurationService`, which writes a minimal `.aepx`. |
| Standard | `src/StandardService.cs` | Loads the template (or the bundled copy) into a `ProjectStandard`. Builds finding labels. |
| File formats | `src/TextFileService.cs` | Encoding-preserving read and write of text files; parsing and editing of `.wdp`, `.wdt`, `.wdl` lines; path-list helpers. |
| Drawings | `src/DrawingInspectionService.cs` | Reads a DWG as a side database: `WD_M` presence and attribute values, title blocks, the layer table with entity counts, linetype names and xdata application names. Applies one `DrawingRepairRequest` to a DWG (insert `WD_M`, create, repair and purge layers, set `WD_M` attributes) and saves it once. The only file besides `Plugin.cs` and the bridge that uses the AutoCAD API. |
| Wire layers | `src/WireLayerService.cs` | Pure logic, no AutoCAD types: wildcard matching, `WD_M` list-attribute comparison, which layers of an inspection are wire layers, whether a layer name is standard, property differences between a drawing layer and the template layer, and whether a drawing finding is resolved after repair. |
| AutoCAD Electrical bridge | `src/AcadeBridgeService.cs` | Asks the host for the active project; sends the reload LISP. |
| Configuration | `src/SettingsService.cs` | `SettingsService` (per-user `settings.json`, bundled standard lookup) and `RulesService` (rules JSON). |
| Diagnostics | `src/DiagnosticsService.cs` | Builds the `LSEDOCTORDIAG` report. |
| Data shapes | `src/Models.cs` | Settings, rules, text document, standard, finding, diagnosis, results, drawing inspection, layer info, drawing repair request. |

Supporting files:

| File | Purpose |
|---|---|
| `LSE_Doctor.csproj` | SDK-style, `net8.0-windows`, x64, WinForms. Uses AutoCAD's own DLLs when found, else the `AutoCAD.NET` 25.1.0 reference package (compile only). Copies the rules JSON and `standard\` next to the DLL. |
| `config/LseDoctorRules.json` | How strictly to compare. Copied to the output folder. |
| `config/standard/*` | Bundled copy of the template `.wdp`, `.wdt` and `_wdtitle.wdl`, the fallback standard. Copied to `standard\` next to the DLL. |
| `bundle/LSE_Doctor.bundle/PackageContents.xml` | Autoloader manifest. |

## 3. Dependency diagram

```
 AutoCAD Electrical 2026 (acad.exe, .NET 8)
        |  NETLOAD / bundle, command line
        v
 Plugin.cs  Commands
   LSEDOCTOR  LSEDOCTORSCAN  LSEDOCTORSETTINGS  LSEDOCTORDIAG
        |            |               |                |
        |            |               |                +--> DiagnosticsService --> TextReportForm
        |            |               +--> DoctorSettingsForm --> SettingsService
        v            v
   DoctorForm <--- ScanForm  ("Open in Doctor")
        \            /
         \          /       RulesService.Load, StandardService.Load
          v        v        (once per form, on Shown)
        DiagnosisService.Examine ----------+
              |                            |
              |  text files                |  DWG contents (only if "check drawings")
              v                            v
        TextFileService            DrawingInspectionService.Inspect
        (.wdp .wdt .wdl lines)       (side Database, read only;
              ^                       template DRAWING-#.dwg once, then each drawing)
              |                            |
              |                            v
              |                    WireLayerService (pure comparison of
              |                      WD_M values and layer tables)
              |
        RepairService.Execute -- backup --> <project>\_LSE_DOCTOR_BACKUP\<stamp>\
              |  edits .wdp .wdt .wdl, creates .wdt/.wdl/.aepx
              |  DrawingInspectionService.Repair, one DrawingRepairRequest per DWG
              |    (side Database, one transaction, one SaveAs)
              |  DiagnosisService.Examine + Inspect + WireLayerService.Resolved (verify)
              +--> <project>\LSE_DoctorReport.txt   (success)
              +--> %APPDATA%\LSE\LSE_Doctor\Logs\   (failure)

 Commands --> AcadeBridgeService  (active project in, reload LISP out)
 Everything --> SettingsService   (settings.json, bundled standard folder)
```

Only `Plugin.cs`, `AcadeBridgeService.cs` and `DrawingInspectionService.cs` reference the AutoCAD API. The rest, including `WireLayerService.cs`, is plain .NET and WinForms.

## 4. End-to-end data flow

### 4.1 Examine

1. A form (or `ScanForm` for each project found) loads `AppSettings`, then the rules (`RulesService.Load`) and the standard (`StandardService.Load`).
2. `DiagnosisService.Examine` reads the project `.wdp` with `TextFileService.Read`, which keeps the encoding and line ending.
3. It compares in this order: description lines `*[n]`, `+[n]` options (library and menu paths are a sub-category), `?[n]` properties, the `.wdt`, the `.wdl`, the `.aepx`, then the drawing list.
4. With "Check every drawing" on and `checkWireLayers` true, the template drawing (`ProjectStandard.TemplateDrawingPath`) is inspected once before the project drawings and kept in `Diagnosis.TemplateDrawing`. Its own wire layers and `WD_M` wire-layer attributes are the standard. If there is no template drawing, or it is open in the editor or unreadable, one log line says the wire check is skipped and the rest of the examine continues.
5. Each listed drawing is then inspected once (`WD_M`, title block, layer table). For each drawing with a `WD_M` the wire-layer attributes are compared with the template's; each template wire layer is looked for in the drawing and its colour, linetype, lineweight and wire-type xdata compared; each wire layer of the drawing that is not standard is reported for purge (no entities) or for moving by hand (entities).
6. Every difference becomes a `Finding` with a severity, a repair action, a recommended flag, and the data needed to repair it.
7. `Selected` is set equal to `Recommended` for every finding. The grid starts with the recommended repairs ticked.
8. The caller shows the findings. A project is Healthy when it has no Error and no Warning findings. Info findings do not count.

### 4.2 Repair transaction

`RepairService.Execute` runs on the UI thread. Steps 1 to 4 run before the try block; steps 5 to 12 run inside it.

| # | Step | Detail |
|---|---|---|
| 1 | Select | Findings with `Selected` true and an action other than `None`. If none, throw "Nothing is ticked." |
| 2 | Check project file | `FileNotFoundException` if the `.wdp` is gone. |
| 3 | Work out touched files | The `.wdp` if any `wdp` finding is ticked; the `.wdt` or `.wdl` if edits (not file creation) are ticked; every drawing that has a ticked drawing action (`Finding.IsDrawingAction`: `InsertWdm`, `SetAttribute`, `CreateLayer`, `RepairLayer`, `PurgeLayer`). |
| 4 | Read-only check | Any touched file that exists and is read-only stops the run with a Vault hint. Nothing has been changed or backed up yet. |
| 5 | Back up | Create `<project folder>\<BackupFolderName>\<yyyyMMdd_HHmmss>\` and copy each touched file into it by file name. Backup copies get the Normal attribute. |
| 6 | Edit `.wdp` | Re-read the file, apply `Add`, `Replace` and `RemoveDrawing` findings in the order they appear in the findings list, write once if anything changed. |
| 7 | Edit or create `.wdt` and `.wdl` | `CreateFile` copies the standard's file. Edits go through `SetMapping` and `SetLabel`. Written once per file. |
| 8 | Create `.aepx` | Only if ticked and the file does not already exist. |
| 9 | Repair drawings | Ticked drawing actions are grouped by drawing path into one `DrawingRepairRequest` per DWG. `DrawingInspectionService.Repair` opens the DWG as a side database once, applies the steps in the fixed order insert `WD_M`, create layers, repair layers, set attributes, purge layers inside one transaction, commits, then `SaveAs(path, DwgVersion.Current)`. The log gets `Repairing drawing <file> (n repair(s))` and one indented line per step. A step that throws (for example a layer that `Database.Purge` says is still referenced) aborts that drawing's transaction before the save and fails the whole run (step 4.3). |
| 10 | Verify | `DiagnosisService.Examine` again with drawing checks off. A text-file finding is Unresolved if the same Key and action still appears. Each repaired drawing is inspected once more and every drawing finding is checked with `WireLayerService.Resolved` (the `WD_M` exists, the attribute has the template value, the layer exists and matches the template layer, or the purged layer is gone). |
| 11 | Report | Log ends `Status: SUCCESS` or `Status: PARTIAL`. The log is appended to `<project folder>\LSE_DoctorReport.txt`. |
| 12 | Prune | Keep the newest `BackupsToKeep` backup folders (sorted by folder name, descending), delete the rest. Remember the project in `RecentProjects`. |

### 4.3 Rollback

Any exception inside the try block does this, in order:

1. Log `ERROR: <message>`.
2. For every backed-up file, copy the backup over the original. This includes every DWG that had a drawing action, so a drawing already saved earlier in the same run goes back to its backed-up state.
3. For every file the run created (`.wdt`, `.wdl`, `.aepx` from `CreateFile`), delete it.
4. Write the whole log plus `Detail: <exception>` to `%APPDATA%\LSE\LSE_Doctor\Logs\DoctorRepair_<yyyyMMdd_HHmmss>.txt`.
5. Throw `InvalidOperationException` whose message is the log. The form shows it in a text window.

The backup folder itself is left in place after a rollback. The "Nothing is ticked" and read-only checks (steps 1 to 4) throw before the try block, so they do not roll back and do not write a log file; nothing has changed at that point. The screens still word this as a rollback; see Known Gaps in the [developer guide](DEVELOPER-GUIDE.md).

After a successful repair of the active project, the form records the project path. When the dialog closes, `Commands` calls `AcadeBridgeService.ReloadProject`, which queues the reload LISP with `SendStringToExecute`.

## 5. Process, threading and COM model

- One process: `acad.exe`. The plugin is a managed DLL in the .NET 8 runtime that AutoCAD 2026 hosts.
- All commands are `CommandFlags.Modal`. Forms are shown with `Application.ShowModalDialog`, so AutoCAD is blocked while a form is open.
- Everything runs on the UI thread. Examine, scan and repair have no background worker and no cancel. A scan with "check drawing contents" opens every DWG in turn on the UI thread.
- No COM, no ActiveX, no external processes. The only host round trip is `Editor.Command` with a LISP expression and the `USERS1` system variable (`AcadeBridgeService.TryGetActiveProject`).
- DWG files are never opened as documents. They are read into a `Database` object with `ReadDwgFile`. A drawing that is open in the editor is not inspected or modified. The same applies to the template drawing: if it is open, the wire-layer check is skipped for the whole examine.
- During a drawing repair the template drawing is open as a second side database for the whole of that drawing's transaction, and `HostApplicationServices.WorkingDatabase` is switched to the drawing being repaired and restored in `finally`.
- Settings are loaded when a form is constructed. A change saved in the settings dialog takes effect the next time a form opens.

## 6. Units, coordinates and encodings

- The doctor measures nothing. The only coordinate it handles is the insertion point of the `WD_M` reference in the template drawing, which it reuses unchanged for the new reference in model space.
- Layer lineweights are the `LineWeight` enum cast to an integer: hundredths of a millimetre, with -3 default, -2 ByBlock and -1 ByLayer. Layer colours are compared as the text of `Color.ToString()`. Layer names, attribute tags, list entries and xdata application names are compared ignoring case. List attributes (`WIRELAYS`, `FAN_INOUT_LAYS`) are split on `,` and `;`, trimmed, de-duplicated and compared as unordered sets.
- Text encodings are preserved. `TextFileService.Read` detects UTF-8 with BOM, UTF-16 LE and BE (by BOM), UTF-8 without BOM (strict decode), and falls back to Latin-1. The line ending is `\r\n` if the text contains one, else `\n`. `Write` re-emits the same preamble and line ending.
- Path comparison ignores case and trailing `\` or `/`. Path lists are `;`-separated.
- Index order: new `*[n]`, `+[n]` and `?[n]` lines are inserted in numeric order inside their own group. A group the project does not have yet is placed in template order (`*`, then `+`, then `?`).

## 7. Design decisions

| Decision | Reason |
|---|---|
| The standard is read from the live template project. | One place to update. The rules JSON never repeats a value. |
| A bundled copy of the three template text files ships next to the DLL. | The doctor still works when the Vault path is not reachable. The standard label turns red and says "bundled copy". |
| The `.wdp` is edited line by line, not parsed and rewritten. | Existing order, encoding, line ending and unknown lines survive untouched. A round trip of the real template is byte-identical (smoke test). |
| Findings carry everything needed to repair them (`Target`, `Prefix`, `Index`, `Value`). | Repair does not re-derive anything from the standard, so what the user ticked is exactly what is applied. |
| Recommended versus ticked. | Safe repairs (add a missing line, copy a missing file) are recommended. Judgement calls (blank descriptions, drawing removal, `.aepx` creation, protected lines, enforcement switched off) are listed but unticked. |
| Verify by examining again. | Proves the files now match instead of trusting the edit code. |
| Backup before any write; rollback restores and deletes. | A failed run leaves the project as it was. |
| Drawings are processed as side databases and only when closed. | Avoids changing a document the user is editing. |
| The template drawing is the wire-layer standard. | The rules JSON names only which `WD_M` attributes to compare and which xdata applications mark a wire type. Which layers exist, their colour, linetype, lineweight and wire-type data, and the `WIRELAYS` list itself all come from `DRAWING-#.dwg`, so the drafting group changes the standard by editing the template drawing, not a config file. |
| A layer is a wire layer when it matches a `WD_M` list pattern, is named by a single-name attribute, or carries wire-type xdata. | Mirrors how AutoCAD Electrical itself decides (the `WIRELAYS` list from the Autodesk help page "About the WD_M block"), with the xdata test as a safety net when `WIRELAYS` is blank, which means every layer to AutoCAD Electrical. The xdata application name pattern `VIA_WD*` is a guess; Autodesk does not document it. |
| Missing template layers are cloned, differing ones are repaired in place, non-standard unused ones are purged, and entities are never moved. | `WblockCloneObjects` of the layer table record brings linetype, regapps and xdata along in one call. Repairing in place keeps whatever is drawn on the layer. A non-standard layer that still has entities is only reported, because choosing the target layer is an engineering decision. |
| On, off, frozen, locked are never compared or changed. | They are per-drawing working state, not standard. |
| One open and one save per drawing per run. | All ticked actions for a DWG go through one `DrawingRepairRequest` and one transaction, so a drawing is rewritten once however many findings it had. |
| Reload is queued with `SendStringToExecute` after the dialog closes. | The Project Manager may overwrite the repaired `.wdp` with settings still held in memory unless it rereads the file (stated in the README; not verified). |
| Mirrors LSE Project Tools. | Same settings location pattern (`%APPDATA%\LSE\<Product>\settings.json`), shareable JSON, logged transaction, WinForms, two code densities, `Commands` class. |

## 8. Relationship to LSE Project Tools

| Topic | Project Tools (creator) | LSE_Doctor |
|---|---|---|
| Purpose | Create a new project | Examine and repair an existing project |
| Settings folder | `%APPDATA%\LSE\AutoCADElectricalProjectTools\` | `%APPDATA%\LSE\LSE_Doctor\` |
| Shared standard | `config\LseProjectTemplates.json` | The template project itself plus `config\LseDoctorRules.json` |
| `.wdp` encoding | Writes UTF-16, or the base project's encoding | Keeps whatever the project has |
| `.wdp` group order | `*`, `?`, `+` (per the Doctor README) | Keeps whatever the project has; the LSE template uses `*`, `+`, `?` |
| Failure log | `Logs\ProjectReport_<stamp>.txt` | `Logs\DoctorRepair_<stamp>.txt` |
| Reload / activation LISP | `c:wd_makeproj_current` | Same function, same caveat |

A project made by Project Tools can be examined by the doctor. The two tools do not share code, so differences between what the creator writes and what the template contains (description line count, mapping attribute names, group order) will show up as findings. Compare the creator's templates JSON with the template files before running a batch repair over creator-made projects.
