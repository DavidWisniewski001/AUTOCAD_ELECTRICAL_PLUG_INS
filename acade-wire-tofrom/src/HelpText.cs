namespace LSE.AutoCADElectrical.WireToFrom;
public static class HelpText{
 public static string Fix(string t)=>t.Replace("\r\n","\n").Replace("\n","\r\n");
 public const string Main=@"TRUE TO/FROM WIRE IDENTIFIERS (LSETOFROM) - HELP

What this window does
It gives every physical wire an identifier of the form FROMTAG:PIN/TOTAG:PIN, for example K1:13/TB2:1, built from AutoCAD Electrical's own from/to data. The identifier is placed on the schematic as a small label block next to the wire end, and the same data is written to a CSV for the wire label printer. Native wire numbers are not touched: the node number stays in WIRENO and the to/from text is its own label.

Before you run it
1. Draw the wires (AEWIRE) and number them (AEWIRENO).
2. Make sure the project database is current. Run a native report (Reports > Schematic > Wire From/To) once, or configure the refresh LISP in LSETOFROMSETTINGS. The window warns when a drawing was saved after the database was written.
3. The first time on a workstation, run LSETOFROMDISCOVER so the tool knows which table of the project database holds the from/to rows. Until then use the 'report file' data source: save the Wire From/To report to a file and pick it here.

The steps
1. Project: the .wdp to work on. Active project is used when AutoCAD Electrical has one open.
2. Data source: Project database (default) or a saved Wire From/To report file.
3. Tick boxes: Annotate drawings places and updates the labels; Current drawing only limits it to the drawing you are in; Export CSV writes the two CSV files.
4. Press Plan. The grid lists every label: insert, update, keep, delete (stale) or skip, with the reason.
5. Tick what you want (Tick all / Untick all / Tick recommended) and press Apply ticked. Closed drawings are backed up first and saved; drawings open in AutoCAD are changed in the session and you must save them.

What a run leaves behind
<project>\LSE_ToFrom\<project>_tofrom_labels.csv: one row per wire end (Drawing, Sheet, WireNo, End, Label, Tag, Pin, OtherTag, OtherPin, WireHandle, ComponentHandle, Key). At the far end the label is reversed (TB2:1/K1:13) unless the config says otherwise.
<project>\LSE_ToFrom\<project>_tofrom_report.csv: one row per wire with the native report columns (WIRENO, CMP1, PIN1, CMP2, PIN2, handles, drawing indexes) for a row-by-row check against the native report.
<project>\LSE_ToFrom\<project>_tofrom_validation.txt: missing pins (the label shows ?), missing tags, wires with no label, labels with no wire, skipped drawings, stale database.
<project>\LSE_ToFromReport.txt: the run log, appended. Backups of closed drawings in <project>\_LSE_TOFROM_BACKUP\<date and time>\.

Re-running
Every label carries a hidden key (wire handle plus end). A re-run finds the label by its key and updates the text in place, removes labels whose wire is gone, and never creates a second label for the same wire end. Running twice without edits changes nothing.

Which end is From
The From/To report decides: CMP1 is From, CMP2 is To, following the network's wire sequence. If the whole project reads backwards, set swapEnds in the config instead of editing wires.

The config file (tofrom.config.json, next to the DLL)
format and farEndFormat: how the identifier is built ({FROM}, {TO}, {FROMTAG}, {FROMPIN}, {TOTAG}, {TOPIN}, {WIRENO}, {FROMLOC}, {TOLOC}).
tagPinFormat: {TAG}:{PIN}. missingPinText and missingTagText: what to show when the data is blank (default ?).
schematic: label at from, to or both ends; layer name; block names; text height; offsets from the wire end; collision steps; whether a re-run re-places labels; whether stale labels are deleted.
csv: delimiter, encoding, reverse at the far end, output folder and file names.
database: where the project database is, the SELECT that returns the from/to rows, the column names, the drawing index base.
reportFile: the column names expected in a saved report.

Other commands
LSETOFROMEXPORT: CSV and validation only, no dialog. LSETOFROMVALIDATE: validation only, nothing written to drawings. LSETOFROMDISCOVER: list the project database's tables and set the query. LSETOFROMBLOCKS: write the two label block drawings and add layer and blocks to the current drawing (do it once in the department template). LSETOFROMSETTINGS and LSETOFROMDIAG.
";
}
