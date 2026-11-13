# Operations guide: LSE_Doctor

Maintained by DCW. Last reviewed 2026-11-10.

How to install the doctor, examine a project, repair it, read the log, understand every message and recover by hand. Written for the engineer who uses the tool. Nothing in this guide has been run inside AutoCAD Electrical yet; see the Verify on a workstation notes and the [developer guide](DEVELOPER-GUIDE.md#7-first-build-verification-checklist) before trusting it on a live job.

Related documents: [ARCHITECTURE.md](ARCHITECTURE.md), [DATA-STORES.md](DATA-STORES.md), [CODE-REFERENCE.md](CODE-REFERENCE.md), [GLOSSARY.md](GLOSSARY.md).

## 1. Install

Prerequisite: AutoCAD Electrical 2026 on Windows x64. The autoloader manifest allows exactly series R25.1 (AutoCAD 2026).

1. Get the Release build output folder: the DLL, `LseDoctorRules.json`, and the `standard\` folder.
2. Either:
   - `NETLOAD` the DLL in each session; or
   - copy `bundle\LSE_Doctor.bundle` to `%APPDATA%\Autodesk\ApplicationPlugins\` (per user) or `C:\ProgramData\Autodesk\ApplicationPlugins\` (all users) and put the build output in the bundle's `Contents\` folder. The commands load on first use.
3. Run `LSEDOCTORDIAG`. With the Vault reachable it should report `Rules: PASS`, `Standard: PASS` and a standard with 8 description, 38 option and 75 property lines, 11 mappings, 14 labels and a template drawing. If the Vault is not reachable the standard is the bundled copy (same counts, no template drawing). Anything marked MISSING or FAIL is fixed in `LSEDOCTORSETTINGS`.
4. Optional: put `LseDoctorRules.json` on a share and point `Doctor rules JSON` at it so every engineer uses the same rules.

Verify on a workstation: bundle autoload and `LoadOnCommandInvocation` behaviour.

## 2. Commands

| Command | Use it to |
|---|---|
| `LSEDOCTOR` | Examine and repair one project. |
| `LSEDOCTORSCAN` | Triage every project under a folder and batch-apply recommended repairs. |
| `LSEDOCTORSETTINGS` | Change settings. |
| `LSEDOCTORDIAG` | See what the plugin resolved on this machine. |

## 3. Examine and repair one project (LSEDOCTOR)

Always work on a copy of a project the first few times.

1. Type `LSEDOCTOR`. The doctor asks AutoCAD Electrical for the active project. If it gets none it looks for a single `.wdp` in the active drawing's folder. If a project is found, the dialog opens and examines it at once. Otherwise the status line says "No active project was detected. Browse to a project .wdp and press Examine."
2. The `Standard` line shows which template is in use, with counts. Red text means the bundled copy is in use (the Vault template was not reachable) or the standard could not be loaded.
3. Pick a project: type or choose a path in the box (recent projects are listed), press Browse, or press Active project.
4. Decide on `Check every drawing: WD_M block, LSE title block and wire layers against the template drawing (opens each DWG; slower)`. On: the doctor opens the template drawing `DRAWING-#.dwg` once and then every project DWG (read only, in memory), and compares each drawing's `WD_M` block, title block and wire layers with the template drawing. Off: only the text files are checked and the drawing list is checked for files missing on disk. The initial state comes from settings. The wire-layer part needs the Vault template folder: with the bundled standard there is no template drawing and the Checks section of the report says `Wire layers: no template drawing next to the standard .wdp; check skipped`.
5. Press Examine. The status line shows "Healthy: the project matches the LSE standard." or "N error(s), M warning(s). K recommended repair(s) are ticked; review them and press Repair ticked."
6. Read the grid (section 5). Tooltips on the Item, Project has, Standard and Repair cells give the full text and the note.
7. Change the ticks. Buttons: Tick recommended, Tick all (every finding that has a repair), Untick all, and the "Show informational rows" check box (on by default; hides or shows Info rows; hidden rows keep their tick state).
8. Optional: press Report... to see the full text report; Save as... in that window writes it to a file.
9. Press Repair ticked. A confirmation lists the number of repairs, the project and the backup folder. If the project is the active one, it also says the project will be reloaded when the doctor closes.
10. After the repair the repair log opens (unless switched off in settings). Read the last `Status:` line. The project is examined again automatically.
11. Close. If the active project was repaired and `Reload the active project after it is repaired` is on, the doctor queues the reload LISP. Check the command line for the "LSE_Doctor: reloaded project" message.

If the reload message says the function is not loaded, open the project again from the Project Manager by hand. Otherwise AutoCAD Electrical may later overwrite the repaired `.wdp` with settings it still holds in memory (README statement; not verified).

## 4. Scan a folder (LSEDOCTORSCAN)

1. Type `LSEDOCTORSCAN`. The folder box starts with `Projects root` from settings.
2. Pick a folder with Browse or type one. Tick or untick `Check drawing contents too (opens every DWG; much slower)`.
3. Press Scan. The doctor walks the folder tree, skipping the template project itself, folders named like the backup folder and folders named `_V`. It examines each `.wdp` found, one after another, with the status line showing progress. There is no cancel button. The folder and the drawing-check choice are saved to settings.
4. Each row shows the project, its folder, its status and the number of recommended repairs. Colours: green healthy, yellow warnings only, red has errors. A project that could not be examined shows `ERROR: <message>` and cannot be ticked. Projects with recommended repairs start ticked.
5. Double-click a row, or select it and press Open in Doctor..., to review and repair that project in the full dialog. The row is refreshed on return.
6. Press Repair ticked (recommended fixes). A confirmation lists the number of projects. For each ticked project the doctor selects exactly its recommended findings and repairs it, each with its own backup and rollback. One failure does not stop the others. A report window lists every project's log and each failure.
7. If the active project was among those repaired it is reloaded when the scan window closes.

Batch repair never touches unticked findings (blank descriptions, drawing removals, `.aepx` creation, protected lines, enforcement-off differences). Use Open in Doctor... for those. With `Check drawing contents too` on, the recommended set includes the drawing repairs: inserting `WD_M`, setting `WD_M` wire-layer values, creating and repairing wire layers and purging unused non-standard wire layers (with the default rules). Every such drawing is rewritten and backed up. Run the batch on copies first.

## 5. The findings grid

| Column | Meaning |
|---|---|
| Fix | Tick to apply. Disabled for report-only rows. |
| Severity | Error (red row), Warning (yellow row), Info (white row). |
| Category | See the catalogue below. |
| Item | The line, attribute or file, such as `*[2] QUOTE#` or `+[1] Schematic symbol library paths`. For wire-layer rows: the drawing path as listed in the `.wdp`, a colon, then the `WD_M` attribute tag or the layer name, such as `STOCKCODE-E001.dwg : WIRELAYS` or `STOCKCODE-E001.dwg : _WIRES`. |
| Project has | Current value, or `(line missing)`, `(missing)`, `(file missing)`, `(blank)`, `(attribute missing)`, `(layer missing)`, `(blank = every layer)`. For layers: `color 7, linetype Continuous, lineweight default` and, when the drawing's layer lacks wire-type data, `, no wire-type data`. |
| Standard | The template value, the template layer's summary, `(not in the standard)` or `(not in the template)`. |
| Repair | Add line, Replace value, Create file, Insert WD_M, Remove from project, Set WD_M value, Create layer from template, Repair layer, Purge layer, or (report only). |

The summary line under the grid reads: status, number of findings, number recommended, number ticked. Repair ticked is enabled only when at least one repair is ticked.

### Findings catalogue

| Category | Condition | Severity | Repair | Recommended |
|---|---|---|---|---|
| Project descriptions | `*[n]` missing | Error | Add (placeholder from the template, or empty if the rule is off) | yes |
| Project descriptions | `*[n]` blank and the template has text | Info | Replace with the placeholder | no |
| Library and menu paths | `+[n]` in `pathListOptions` or `pathOptions` missing | Error | Add | yes unless protected |
| Library and menu paths | differs (list or single path, case-insensitive) | Warning | Replace (merge mode: template paths first, then extras) | if `enforceOptions` and not protected |
| Project options | other `+[n]` missing or differing | Error / Warning | Add / Replace | as above |
| Project options | project has a `+[n]` the template lacks | Info | none | no |
| Project properties | `?[n]` missing or differing | Error / Warning | Add / Replace | missing: yes; differing: if `enforceProperties`; unless protected |
| Project properties | project has a `?[n]` the template lacks | Info | none | no |
| Title block mapping (.wdt) | file missing | Error (Info if not required) | Create file | if required |
| Title block mapping (.wdt) | `BLOCK` line missing | Error | Add | yes |
| Title block mapping (.wdt) | `BLOCK` lacks the LSE block name | Warning | Replace: names kept, LSE name appended | if `enforceMappings` |
| Title block mapping (.wdt) | attribute mapping missing | Error | Add | yes |
| Title block mapping (.wdt) | code differs (case-insensitive) | Warning | Replace | if `enforceMappings` |
| Title block mapping (.wdt) | extra attribute | Info | none | no |
| Description labels (.wdl) | file missing | Error (Info if not required) | Create file | if required |
| Description labels (.wdl) | `LINEn` missing | Error | Add | yes |
| Description labels (.wdl) | label differs (case-insensitive, trimmed) | Warning | Replace | if `enforceLabels` |
| Description labels (.wdl) | extra `LINEn` | Info | none | no |
| Project configuration (.aepx) | file missing | Info (Error if required) | Create file | only if required |
| Drawings | no drawings listed | Info | none | no |
| Drawings | listed file not on disk | Warning | Remove from project | no |
| Drawings | open in the editor | Info | none | no |
| Drawings | DWG could not be read | Info | none | no |
| Drawings | no `WD_M` block | Error | Insert WD_M | if `InsertMissingWdm` and a template drawing exists |
| Drawings | no LSE title block | Warning | none (insert by hand) | no |
| Wire layer settings (WD_M) | the drawing's `WD_M` lacks an attribute the template's has (`WIRELAYS`, `WIRENO_LAY`, `WIRECOPY_LAY`, `WIREFIXED_LAY`, `WIREREF_LAY`, `FAN_INOUT_LAYS`) | Warning | none; run Project > Other Tools > Update to New WD_M Block (keep values) in AutoCAD Electrical, then examine again | no |
| Wire layer settings (WD_M) | attribute value differs from the template (lists compared as unordered sets, wildcards kept as text; single names trimmed, case ignored) | Warning | Set WD_M value | if `enforceWireLayers` |
| Wire layer settings (WD_M) | attribute matches the template but names a layer that exists in neither the drawing nor the template drawing | Info | none | no |
| Wire layers | a wire layer of the template drawing is missing | Error | Create layer from template (cloned with linetype and wire-type data) | yes |
| Wire layers | template wire layer present but colour, linetype, lineweight or plot flag differ, or the template layer has wire-type data and this one has none | Warning | Repair layer (colour, linetype, lineweight, plot flag, description and wire-type data set from the template; what is drawn on it stays) | if `enforceWireLayers` |
| Wire layers | the drawing has a wire layer the template does not (matches the drawing's `WIRELAYS` or carries wire-type data) with nothing drawn on it | Warning | Purge layer | if `purgeUnusedWireLayers` |
| Wire layers | the same, but entities are on the layer | Warning | none; move the entities to a standard wire layer or add the layer to the template drawing, then examine again | no |

The wire-layer rows appear only with "Check every drawing" on, `checkWireLayers` true in the rules, and a readable template drawing that is not open in the editor. The template drawing's own wire layers and `WD_M` values are the standard; nothing about them is in the rules file. Layers named in `neverPurgeLayers` (`0`, `DEFPOINTS`) are never offered for purge. Whether a layer is on, off, frozen or locked is never compared or changed. The `WD_M` attributes are compared only when the drawing has a `WD_M`; a drawing without one gets the whole block, with the template's values, from Insert WD_M.

Verify on a workstation: none of the wire-layer repairs has run inside AutoCAD Electrical. Before trusting Repair layer or Create layer from template on a job, repair a copy of one drawing and confirm in AutoCAD Electrical that the layer is treated as a wire layer (for example that Insert Wire picks it up and the wire numbering sees it). See the [developer guide](DEVELOPER-GUIDE.md#9-known-gaps).

Protected lines (`protectedOptions`, `protectedProperties`) are shown as Info and are not recommended, but they keep their repair and can still be ticked by hand or with Tick all.

Important: the Healthy status ignores Info rows. A project with drawings that were open in the editor or unreadable, or whose `.aepx` is missing, can still show Healthy.

## 6. Settings (LSEDOCTORSETTINGS)

Per user. Saved to `%APPDATA%\LSE\LSE_Doctor\settings.json` ([DATA-STORES.md](DATA-STORES.md#2-settingsjson)). Browse buttons exist for the first three rows.

| Field | Meaning |
|---|---|
| Template project (.wdp) = the standard | The LSE template. If missing, the bundled copy is used. |
| Doctor rules JSON | Rules file. Default is next to the DLL. |
| Projects root (scan default) | Starting folder for Browse and scan. |
| Backup folder name (inside the project) | Default `_LSE_DOCTOR_BACKUP`. Must be a valid folder name. |
| Backups to keep per project | Whole number, 1 or more. Default 10. |
| Active project LISP (sets USERS1) | Expression that stores the active `.wdp` path in `USERS1`. |
| Reload project LISP ({WDP}) | Expression run after the active project is repaired. `{WDP}` becomes the project path. |
| Check drawing contents by default (LSEDOCTOR) | Initial state of that check box. |
| Recommend inserting a missing WD_M block from the template drawing | Makes `Insert WD_M` recommended. |
| Folder scan checks drawing contents too (slow) | Initial state in the scan. |
| Reload the active project after it is repaired | Sends the reload LISP. |
| Show the repair report after each repair | Opens the log window after `LSEDOCTOR` repairs. The batch scan always shows its report. |

Save closes the dialog; Diagnostics opens the same report as `LSEDOCTORDIAG`. Validation errors appear as a message box titled "LSE_Doctor Settings".

## 7. What a successful run leaves behind

| Item | Where |
|---|---|
| Edited files | the `.wdp`, and the `.wdt`, `.wdl` and DWGs that were ticked. A DWG is rewritten once however many of its repairs were ticked, and may come back in the host's current DWG version. |
| New files | `.wdt`, `.wdl`, `.aepx` if created |
| Backup | `<project folder>\_LSE_DOCTOR_BACKUP\<stamp>\` with the original of each file that was changed, including every rewritten DWG |
| Log | appended to `<project folder>\LSE_DoctorReport.txt` |
| History | project added to the recent list |

The backup folder is inside the project folder. Exclude it from Vault check-in if you do not want it versioned.

## 8. Reading the log

The log is a list of lines. Illustrative example of a successful run (names and values are examples):

```
Started: 2026-11-10T09:15:02 by jsmith@ENG-PC12
Project: C:\_Lake_Shore_E_Vault\Engineering\Custom Sales Order\Job\Job.wdp
Standard: C:\_Lake_Shore_E_Vault\...\E-CAD CUSTOM PROJECT TEMPLATE 2.wdp
Rules: C:\Tools\LSE\LseDoctorRules.json
Repairs ticked: 6
Backed up Job.wdp to C:\...\Job\_LSE_DOCTOR_BACKUP\20261110_091502
Backed up Job-E001.dwg to C:\...\Job\_LSE_DOCTOR_BACKUP\20261110_091502
Add line: *[8] = NTS
Replace value: +[4] = C:\_Lake_Shore_E_Vault\...\ACE_PANEL_MENU_NFPA.DAT
Project file written (UTF-16 LE): C:\...\Job\Job.wdp
Created C:\...\Job\Job.wdt from C:\...\E-CAD CUSTOM PROJECT TEMPLATE 2.wdt
Repairing drawing Job-E001.dwg (3 repair(s))
  Created layer _WIRES_RED from the template (properties, linetype and wire-type data)
  WD_M WIRELAYS: "_WIRES*" -> "_WIRES*,_WIRES_RED"
  Purged layer WIRES_OLD
  Saved C:\...\Job\Job-E001.dwg
Verifying
Status: SUCCESS (6 repair(s) applied, all verified)
```

| Line | Meaning |
|---|---|
| `Started:` | Time, user, machine. |
| `Backed up <file> to <folder>` | One per touched file that already existed. |
| `<Action>: <prefix>[n] = value` | A `.wdp` edit. Action is Add line or Replace value. |
| `Removed from the project list: <path>` | A drawing entry deleted. |
| `Add line` / `Replace value: <file> <item> = <value>` | A `.wdt` or `.wdl` edit. |
| `Written (<encoding>): <path>` | The file was saved with its original encoding. |
| `Created <path> [from <path>]` | A new file. |
| `Repairing drawing <file> (n repair(s))` | One per rewritten DWG, followed by one indented line per step in the order applied: `WD_M inserted into model space with N attribute value(s) from <template file>` (N attributes received the template's text), `Created layer <name> from the template (...)` or `Layer <name> already exists; left as it is`, `Repaired layer <name>: color, linetype, lineweight, plot flag and wire-type data set from the template` (with `(linetype <name> could not be copied)` when that part failed), `WD_M <tag>: "<old>" -> "<new>"`, `Purged layer <name>` or `Layer <name> is already gone`, then `Saved <path>`. |
| `Skipped: ...` | A ticked repair that no longer applies (file already exists, drawing entry gone, file to edit does not exist). |
| `Old backup removed: <folder>` | Pruning. |
| `UNRESOLVED: <category> / <item>` | After the repair the same finding is still reported. |
| `Status: SUCCESS (...)` | Every ticked repair verified. |
| `Status: PARTIAL (N repair(s) applied, M still reported)` | Applied but not all verified. Examine again and read the remaining rows. |
| `ERROR: <message>` | The run failed and was rolled back. Followed by `Rollback restored <file>` and `Rollback removed <file>` lines. |

The `Examine` report (Report button) ends with a "Checks" section: one line per check such as `Project descriptions: 8/8 present` or `Drawings: 11 listed, 10 pass, 1 missing on disk, 0 not inspected`. With drawing checks on it also holds `Inspecting <drawing>` per drawing and one of these wire-layer lines before them:

| Line | Meaning |
|---|---|
| `Wire layers: standard is DRAWING-#.dwg with N wire layer(s): <names>; WIRELAYS = "<value>"` | The template drawing was read; these are the layers every drawing is compared with. |
| `Wire layers: no template drawing next to the standard .wdp; check skipped` | Bundled standard or no drawing in the template folder. Wire layers were not checked. |
| `Wire layers: template drawing is open in the editor; check skipped` | Close `DRAWING-#.dwg` and examine again. |
| `Wire layers: template drawing could not be read: <error>; check skipped` | The template DWG is damaged, locked or of a version the host cannot read. |

A drawing counts as "pass" only when it has `WD_M`, a title block and no wire-layer finding.

## 9. Messages and what to do

| Message | Cause | Action |
|---|---|---|
| Pick an existing project .wdp first. | The project box is empty or the file does not exist. | Browse to the `.wdp`. |
| No active project was detected when the command started (ace_getactiveproject returned nothing). Browse to the project .wdp instead. | Active project lookup returned nothing. | Browse. If it never works, see Known Gaps. |
| That is the LSE template itself. Pick a project to examine, not the standard. | The chosen `.wdp` is the standard. | Pick a different project. |
| Standard could not be loaded: ... | Rules or template could not be read. | Fix the path or the rules JSON in settings. |
| The LSE template project (.wdp) was not found and no bundled copy exists in the 'standard' folder... | Template path wrong and `standard\` missing next to the DLL. | Fix `Template project (.wdp)` or restore the `standard\` folder. |
| The template project has no *[n], +[n] or ?[n] lines... | The template path points at the wrong file. | Point at the real `.wdp`. |
| pathListMode must be "replace" or "merge" in <file> | Bad rules value. | Fix the JSON. |
| The doctor rules JSON is empty: <file> | File parsed to nothing. | Restore the file. |
| Nothing is ticked. Tick the repairs to apply, then press Repair. | Repair run with no selection. | Tick something. |
| Read-only file: <file>. If the project is under Vault control check it out first; otherwise clear the read-only attribute. Nothing was changed. | A file to be edited is read-only. | Check the project out of Vault, then examine and repair again. |
| Repair failed and was rolled back. (status line) | Any exception after the backup was taken; the originals were restored. | Read the log window. |
| Repair refused before any change was made. (status line) | The two checks above (nothing ticked, or a read-only file); nothing was changed. | Tick a repair, or check the file out of Vault / clear the read-only attribute, then repair again. |
| Not inspected while open. Close the drawing and examine again. | The DWG is open in the editor. | Close it and examine again. |
| The drawing is open in the editor. Close it first: <path> | A drawing repair on an open drawing. The run is rolled back. | Close it, examine and repair again. |
| Drawing was not found. / Template drawing was not found. | The DWG or `DRAWING-#.dwg` disappeared between examine and repair. | Restore it; examine again. |
| The template drawing has no WD_M block definition: <path> / The WD_M block definition could not be copied into the drawing. | Template drawing problem. | Check the Vault template folder; use Drawing Properties by hand. |
| No template drawing with a WD_M block is available next to the standard .wdp... | The bundled standard has no drawings. | Make the Vault template reachable. |
| This drawing's WD_M block is older and has no <tag> attribute. In AutoCAD Electrical use Project > Other Tools > Update to New WD_M Block (keep values), then examine again. | (Finding note.) The drawing's `WD_M` predates the attribute. | Run that AutoCAD Electrical tool on the project, then examine again. |
| The WD_M block in this drawing has no attribute <tag>. Use Project > Other Tools > Update to New WD_M Block in AutoCAD Electrical first. | A `Set WD_M value` repair found the tag gone (the block changed since examine). Rolled back. | As above. |
| No WD_M block was found in the drawing, so <tag> could not be set. | The `WD_M` disappeared since examine. Rolled back. | Examine again; tick Insert WD_M. |
| The template drawing has no layer <name>. / Layer <name> could not be copied from the template drawing. | The template changed since examine, or the clone failed in the host. Rolled back. | Examine again. If it repeats, record it under Known Gaps; the clone is unverified. |
| Layer <name> is still referenced (entities, block definitions or viewport overrides) and cannot be purged. | `Database.Purge` would not release the layer although the doctor counted no entities on it (for example a viewport override or an xref). Rolled back. | Untick Purge layer for that drawing, or clean the reference by hand in AutoCAD and examine again. |
| Move the entities to a standard wire layer (or add the layer to the template drawing), then examine again to purge it. The doctor does not move entities. | (Finding note.) A non-standard wire layer is in use. | Decide in AutoCAD: move the entities, or make the layer standard by adding it to `DRAWING-#.dwg`. |
| Listed in the project but missing on disk: <path>. Restore the file, or tick to drop it from the project list. | The `.wdp` lists a file that is not there. | Restore it, or tick Remove from project. |
| Backup pruning skipped: ... / Report could not be written to the project folder: ... | Non-fatal; the repair itself succeeded. | Check folder permissions. |
| Rollback could not restore <file>: ... | Rollback itself failed. | Restore by hand from the backup folder (next section). |
| Backups to keep must be a whole number of 1 or more. / Backup folder name must be a valid folder name. | Settings validation. | Correct the value. |
| Pick an existing folder to scan. / Tick at least one project that has recommended repairs. / Examine a project first. | Missing input. | Provide it. |

Any other `Examine failed: ...` or `ERROR: ...` text is the .NET exception message (for example a locked file or a malformed rules JSON).

## 10. Recovering by hand

If a repair went wrong or a result is not what you want:

1. Close the project in the Project Manager and close any drawing you are about to restore.
2. Open `<project folder>\_LSE_DOCTOR_BACKUP\`. Each sub-folder is one run, named `yyyyMMdd_HHmmss`. Pick the run you want to undo.
3. Copy the files from that sub-folder back over the same-named files in the project (the `.wdp`, `.wdt`, `.wdl` and any DWGs). If two drawings with the same name are in different folders, the second one is stored with a "(2)" suffix; match it to its folder by the run log before copying it back.
4. A purged layer, a repaired layer's old properties and an overwritten `WD_M` value exist only in the backup copy of that DWG. Nothing in AutoCAD Electrical undoes a side-database save, so copy the whole DWG back from `_LSE_DOCTOR_BACKUP\<stamp>\` (or get the previous version from Vault); do not try to recreate the layer by hand unless the backup is gone. The run log names each rewritten drawing on its `Repairing drawing ...` line.
5. If the run created files (`.wdt`, `.wdl`, `.aepx` that did not exist before), delete them. The run log in `LSE_DoctorReport.txt` lists `Created ...` lines.
6. Reopen the project from the Project Manager so it rereads the `.wdp`.
7. For a failed run, also read `%APPDATA%\LSE\LSE_Doctor\Logs\DoctorRepair_<stamp>.txt`; it ends with the exception text.

A project under Vault control can also be restored by getting the previous version from Vault.
