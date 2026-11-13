namespace LSE.AutoCADElectrical.ProjectDoctor;
public static class HelpText{
 public static string Fix(string t)=>t.Replace("\r\n","\n").Replace("\n","\r\n");
 public static string All=>Fix(Doctor+"\n\n"+Rule+"\n\n"+Scan+"\n\n"+Rule+"\n\n"+Settings+"\n\n"+Rule+"\n\n"+Findings);
 const string Rule="------------------------------------------------------------------------";
 public const string Doctor=@"LSE_DOCTOR - HELP (the LSEDOCTOR window)

What this window does
LSE_Doctor compares one AutoCAD Electrical project with the LSE custom project template and lists every difference it finds. Nothing in the project is changed until you tick rows and press Repair ticked. Every file the repair touches is backed up first, every change is checked afterwards, and the whole repair is rolled back if one step fails.

The five steps
1. Project (.wdp): the active project is filled in for you. Use Browse... to pick another project, or Active project to go back to the one open in AutoCAD Electrical.
2. Decide whether to tick ""Check every drawing"". Ticked: every DWG of the project is opened in the background and its WD_M block, LSE title block and wire layers are compared with the template drawing (slower). Not ticked: only the project text files (.wdp, .wdt, .wdl, .aepx) are checked.
3. Press Examine. The grid fills with findings. The recommended repairs are already ticked.
4. Read the grid. Tick or untick the Fix box on any row that has one. Tick recommended, Tick all and Untick all change every row at once.
5. Press Repair ticked, read the question and answer Yes. A report opens when it is done and the project is examined again, so the grid shows what is left.

The grid, column by column
Fix: a box means ""repair this row"" when ticked. Click the box to tick or untick it (or select the row and press the space bar). The word note means the row is information only: the doctor has nothing it can change for it, so there is no box. Click a note row and the status line at the bottom says why.
Severity: Error = something the LSE standard needs is missing. Warning = present but different from the standard, or a drawing the project lists is missing on disk. Info = a note; nothing to do.
Category: the part of the project the row is about: project descriptions, library and menu paths, project options, project properties, title block mapping (.wdt), description labels (.wdl), project configuration (.aepx), drawings, wire layer settings (WD_M), wire layers.
Item: the line, attribute, file or drawing concerned. Hover over it for the doctor's note.
Project has / Standard: the value in your project and the value in the LSE template. Hover over a long value to read all of it.
Repair: what ticking the box will do: Add line, Replace value, Create file, Insert WD_M, Set WD_M value, Create layer from template, Repair layer, Purge layer, Remove from project. ""(report only)"" means the doctor cannot fix it; the Item tooltip says what to do by hand.
Colours: red rows are errors, yellow rows are warnings, white rows are information.

Why is a box already ticked, or not?
Recommended repairs are the safe ones: adding a missing line, copying a missing file, inserting a missing WD_M block, creating a missing wire layer, repairing an off-standard layer. They are ticked for you.
Rows that need your judgement are listed but left unticked: putting the template placeholder text into a blank description, dropping a drawing the project lists but which is not on disk, creating an .aepx file, lines the rules JSON protects, and differences where enforcement is switched off in the rules. Tick them yourself if you agree with the change.

Why can I not tick this row?
Rows that show note instead of a box are report-only. They are:
- a line, mapping or label your project has but the template does not (left alone on purpose);
- a drawing that is open in AutoCAD (close it and press Examine again to have it checked and repaired);
- a drawing that could not be read (the note says why);
- a drawing without the LSE title block (insert it by hand from the template drawing; the doctor does not place title blocks);
- a non-standard wire layer that still has objects on it (move or delete the objects first, then examine again);
- a WD_M attribute an older WD_M block does not have (use the AutoCAD Electrical command Update to New WD_M Block, then examine again);
- a project that lists no drawings.
Untick ""Show informational rows"" to hide the ones whose Severity is Info.

Drawings that are open in AutoCAD
The doctor never writes to a drawing that is open in the editor. When you examine the active project its open drawings are listed as ""open in the editor"" and skipped, and the status line counts them. Close those drawings (the project stays active), press Examine again, and their WD_M and wire-layer checks run.

What Repair ticked does, in order
1. Backs up every file it is about to touch into <project folder>\_LSE_DOCTOR_BACKUP\<date_time>\.
2. Edits the .wdp line by line (existing lines keep their order and encoding), then the .wdt, .wdl and .aepx, then each drawing (opened in the background once, repaired, saved in place).
3. Examines everything again. Anything still wrong is listed as UNRESOLVED in the report.
4. Appends the run to <project folder>\LSE_DoctorReport.txt and keeps the newest backups (Backups to keep in Settings, default 10).
If any step fails, every file is put back from the backup and the window says ""rolled back"".

Undo a repair
Copy the files from the newest <project folder>\_LSE_DOCTOR_BACKUP\<date_time>\ folder back over the project files, then re-open the project in the Project Manager.

The buttons
Browse...: pick a project .wdp.  Active project: use the project open in AutoCAD Electrical.  Examine: run the checks.
Tick recommended / Tick all / Untick all: change the ticks on every row.  Show informational rows: show or hide the note rows.
Report...: the findings as text you can save or copy.  Repair ticked: apply the ticked repairs.  Help: this text.
Close: leave the window. If the active project was repaired it is reloaded so the Project Manager shows the new settings.

If something looks wrong
""Standard could not be loaded"": the LSE template is not where Settings says. Run LSEDOCTORSETTINGS, fix Template project (.wdp), Save, press Examine again. Until then a bundled copy of the template text files is used (shown in red under Standard) and drawing repairs are not possible.
""Repair refused"": nothing was changed. The message says why; usually a project file is read-only, so check it out of the Vault first.
""Repair FAILED (rolled back)"": the files are back as they were. Save the message (it holds the whole log) and paste it into the PLUG-IN_TROUBLESHOOTING document with the project name.
The Report... button and LSEDOCTORDIAG give the text to attach to any trouble report. LSEDOCTORHELP shows all of this help at the command line.";
 public const string Scan=@"LSE_DOCTOR - HELP (the LSEDOCTORSCAN window)

What this window does
Walks a folder tree, examines every project (.wdp) it finds and shows one row per project, so you can see which jobs are off the LSE standard and repair several in one go.

The steps
1. Folder to scan: starts at the Custom Sales Order root from Settings. Browse... to change it. Subfolders are included; backup folders and Vault _V folders are skipped.
2. Tick ""Check drawing contents too"" only when you need the WD_M and wire-layer checks. It opens every DWG of every project and is much slower.
3. Press Scan and wait. The status line counts the projects as they are examined.
4. Read the rows. A row with a Fix box has recommended repairs and is ticked for you. ok means the project is healthy. review means the findings need a decision in the Doctor window (nothing is recommended). error means the project could not be examined; the Status column says why.
5. Either press Repair ticked (recommended fixes) to apply the safe repairs to every ticked project, or select a row and press Open in Doctor... (or double-click the row) to review and repair that one project by hand.

Columns
Fix: the tick box, or ok / review / error.  Project: the .wdp file name.  Folder: where it is.  Status: Healthy, or the count of errors and warnings; ""(text files only)"" means the drawings were not opened.  Recommended: how many safe repairs the doctor would apply.
Colours: green healthy, yellow warnings only, red errors.

Repair ticked (recommended fixes)
Applies only the recommended repairs to each ticked project, one project at a time, each with its own _LSE_DOCTOR_BACKUP\<date_time> folder. Judgement calls are never applied from here; open the project in the Doctor for those. A combined report opens at the end and every row is examined again.

The buttons
Tick all needing repair / Untick all: change the ticks on every row that has a box.  Open in Doctor...: the full findings grid for the selected project.  Help: this text.  Close: leave the window.";
 public const string Settings=@"LSE_DOCTOR - HELP (LSEDOCTORSETTINGS)

Template project (.wdp) = the standard: the LSE custom project template on the Vault working folder. Its .wdp, .wdt, _wdtitle.wdl and DRAWING-#.dwg define what ""correct"" means. If the path is wrong a bundled copy of the three text files is used instead; then there is no template drawing, so WD_M insertion and wire-layer repairs are not possible.
Doctor rules JSON: how strictly to compare (enforce options, properties, mappings, labels and wire layers; protected lines; purge unused wire layers). Leave it blank to use the LseDoctorRules.json next to the plugin DLL.
Projects root (scan default): the folder LSEDOCTORSCAN starts in.
Backup folder name (inside the project): created in each project folder before a repair. Default _LSE_DOCTOR_BACKUP.
Backups to keep per project: older backup folders are removed after a successful repair. Default 10.
Active project LISP (sets USERS1) and Reload project LISP ({WDP}): the AutoCAD Electrical calls used to find the active project and to reload it after a repair. Leave them unless support asks you to change them.
Check drawing contents by default (LSEDOCTOR): whether ""Check every drawing"" starts ticked.
Recommend inserting a missing WD_M block: whether a missing WD_M block is ticked for repair by default.
Folder scan checks drawing contents too (slow): whether LSEDOCTORSCAN opens drawings.
Reload the active project after it is repaired: so the Project Manager shows the repaired settings.
Show the repair report after each repair: the log window after Repair ticked.
Diagnostics: the runtime, the settings, the rules and whether the standard parses. Paste it into a trouble report.
Save writes %APPDATA%\LSE\LSE_Doctor\settings.json.";
 public const string Findings=@"LSE_DOCTOR - WHAT EACH FINDING MEANS

Project descriptions (*[n] lines): the eight description lines the Project Manager shows (PROJECT NAME, QUOTE#, CUST 1 ...). A missing line is added with the template placeholder (recommended). A blank line is allowed; tick to put the placeholder in. Lines with text are never changed.
Library and menu paths (+[1] to +[4]): the schematic and panel library lists and icon menus. A missing or different line is set to the LSE paths (recommended); in merge mode the project's extra paths are kept after the LSE ones.
Project options (other +[n] lines) and Project properties (?[n] lines): every other setting in the .wdp (wire numbering, tagging, ladders, reports ...). Missing lines are added; differing values are replaced when enforcement is on in the rules JSON, otherwise listed unticked for you to decide. Lines the template does not have are left alone (note).
Title block mapping (.wdt): tells Title Block Update which project line fills which title block attribute. A missing file is copied from the template; a missing BLOCK line or attribute mapping is added; the LSE title block name is added to an existing BLOCK line; a differing code is replaced when enforcement is on.
Description labels (_wdtitle.wdl): the names the Project Manager shows for the description lines. A missing file is copied from the template; missing labels are added; differing labels are replaced when enforcement is on.
Project configuration (.aepx): AutoCAD Electrical 2024 and later writes it itself, so a missing one is information only; tick to create a minimal file now.
Drawings: a drawing the project lists but which is not on disk is a warning; tick Remove from project to drop it from the list (never recommended). With ""Check every drawing"" on: a drawing without a WD_M block gets the block and its values copied from the template drawing (recommended); a drawing without the LSE title block is reported only; a drawing open in AutoCAD or unreadable is skipped (note).
Wire layer settings (WD_M): WIRELAYS, WIRENO_LAY, WIRECOPY_LAY, WIREFIXED_LAY, WIREREF_LAY and FAN_INOUT_LAYS in the drawing's WD_M block, compared with the template drawing. A differing value is set when enforcement is on. An attribute an older WD_M block lacks is reported with the Update to New WD_M Block hint.
Wire layers: every wire layer of the template drawing must exist with the same colour, linetype, lineweight and wire-type data, and no other wire layer may exist. A missing layer is created from the template (recommended); an off-standard layer is repaired in place, objects untouched; an unused non-standard wire layer is purged when the rules allow; a non-standard wire layer still in use is reported only. Layers are never switched on or off, frozen, thawed or unlocked; layers 0 and DEFPOINTS are never purged.";
}
