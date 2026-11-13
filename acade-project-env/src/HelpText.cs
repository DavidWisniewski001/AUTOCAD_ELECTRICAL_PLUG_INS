namespace LSE.AutoCADElectrical.ProjectEnvironment;
public static class HelpText{
 public static string Fix(string t)=>t.Replace("\r\n","\n").Replace("\n","\r\n");
 public const string Profiles=@"CUSTOMER PROFILES - HELP

What a profile is
A profile is a saved list of project settings that one customer insists on: how wire numbers look, how component tags are built, which title block mapping to use, and so on. You write it once. From then on LSEENV can set any new project up to that customer's standard in one go, with a backup, instead of you clicking through Project Properties by hand.

A profile only changes what you fill in. A blank value means: this profile has no opinion, leave the project as it is.

The five steps
1. Press New (start from nothing), Duplicate (start from the selected profile) or Capture from project (start from a project that is already set up the way the customer wants).
2. Name it after the customer, for example 'Acme Foods'. Fill in Customer and a one-line Description so a colleague knows what it is for.
3. Pick the Wire scheme. Read the grey text under it: it says what the scheme does.
4. In the settings grid fill in only the values the customer mandates. Leave the rest blank.
5. Press Save. Then run LSEENV, pick the project and this profile, press Plan, read the list, tick what you want and press Apply. Every file is backed up before anything is written.

Worked example
The customer wants FROM-TO wire numbers with pin numbers, and component tags like %F%N.
1. Press New. Name: Acme Foods. Customer: Acme Foods.
2. Wire scheme: From/To with pins. Nothing else is needed for the wire numbers.
3. In the settings grid find the tag format row and type %F%N in the Value column.
4. Save. In LSEENV pick the project and 'Acme Foods', Plan, Apply.
If you want push buttons always on the FROM side, tick 'Own From/To settings' and put ^PB in Device priority.

The fields
Profile name: the file name and the name shown in LSEENV. Must be unique.
Customer, Description: for people only.
Wire scheme: a ready-made way of numbering wires. (none) means the profile only carries the values below.
Settings grid: one row per setting LSE knows about.
  Setting: what it is, with its key in brackets.
  Where it lives: the project line (?[n] or +[n]) or drawing attribute (WD_M) that holds it. The word in brackets is how sure LSE is: inferred or unverified. Check such rows once with LSEENVSNAPSHOT.
  Value: what you want. Blank = leave alone.
  Choices / note: the allowed values.
  Grey rows are not mapped yet. They cannot be applied; LSE will only print a note.
Raw ?[n] overrides: for a project line the grid does not list. Format 6=S;9=%N (line number = value, separated by ;). Use only after LSEENVSNAPSHOT showed you the line number.
Raw +[n] overrides: the same for + lines. Lines +[1] to +[4] (library and menu paths) are LSE's and are never changed.
Raw WD_M overrides: the same for drawing attributes, for example WIREFMT=%N.
Title block mapping (.wdt) and Description labels (.wdl): full path of a file to copy into the project. Copies are listed in the plan but not ticked, because they replace the project's own file.
Notes: free text.

Own From/To settings
Tick this only if the scheme's From/To behaviour is not what the customer wants. The fields below then replace the scheme's.
From/To format: how the number is built. Tokens: {FROM} {TO} {FROMPIN} {TOPIN} {SHEET} {DRAWING}. Example {FROM}-{TO} gives PB1-CR1.
Duplicate suffix: added when two networks would get the same number. {N} is 2, 3, 4... Example -{N}.
Pin format: how a device and pin are written. {TAG}:{PIN} gives PB1:3.
More than two devices: firstLast uses the first and last device; all lists every device joined by the separator.
Join separator: used by 'all'.
Device priority: regular expressions separated by ;. Devices matching an earlier one go on the FROM side. ^PB;^CR puts push buttons before relays.
One tagged device: keep the old number, use the device only, or skip the network.
No tagged device: keep the old number or skip.
Maximum length: cut longer numbers to this many characters. 0 means no limit.
Include pin numbers: add pins to the number.
Terminal strips last: terminals (TB, X) go on the TO side.
Upper case: force capitals.
Write as fixed wire numbers: AutoCAD Electrical's retag will not renumber them. Switching back to another scheme can un-fix them.
Overwrite existing fixed numbers: replace numbers that are already fixed.

The left buttons
New: empty profile. Duplicate: copy the selected one. Capture from project: reads the project's current values into a new profile. Import and Export: JSON files for sharing by mail. Retire: moves the profile to the _retired folder; nothing is deleted.

The sample profiles
LSE Standard: the LSE template as it is, a starting point and a reference.
Example - Grid customer: line-reference wire numbers.
Example - FromTo customer: FROM-TO numbering with device priority.
Duplicate a sample and change it; do not edit the samples.

Where the files are
Profiles: %APPDATA%\LSE\LSE_Env\Profiles (or the shared folder set in LSEENVSETTINGS), one .json per profile.
Backups: <project>\_LSE_ENV_BACKUP\<date and time>\.
Result log: <project>\LSE_EnvReport.txt.
The profile last applied to a project: <project>.lseenv.json.
";
 public const string Environment=@"PROJECT ENVIRONMENT (LSEENV) - HELP

What this window does
It sets a project up to a customer's standard. You pick the project, pick a customer profile and/or a wire scheme, look at exactly what would change, tick the changes you want, and apply. Nothing is written until you press Apply ticked.

The steps
1. Project: the .wdp to change. Active project takes the one open in AutoCAD Electrical. Browse picks another.
2. Customer profile: the saved settings for the customer. Manage opens the profile manager (create or edit profiles). Capture reads the project's current settings into a new profile.
3. Wire scheme: leave on '(from the profile)' to use the profile's own scheme, or pick one to override it. With no profile, the scheme alone is applied.
4. Press Plan. The list shows every setting, what the project has now and what it would become.
5. Tick the changes you want. Tick recommended ticks the safe ones. Tick all and Untick all do what they say. Notes (rows that show the word 'note' instead of a box) cannot be applied; they explain why.
6. Apply ticked. Every file that will change is backed up first. After writing, LSE re-reads everything to prove each change is there. If anything fails, all files go back as they were.

The three tick boxes
Read every drawing: also read and write the WD_M attributes of the project's drawings. Needed for numbering changes to reach existing sheets. Drawings open in AutoCAD are changed in the session and you must save them.
Trace wires: follow the wires to write From/To numbers or to un-fix them.
Queue retag after apply: after the window closes, AutoCAD Electrical's Insert Wire Numbers is started for you.

Worked example
The customer wants FROM-TO wire numbers with pins and tag format %F%N.
1. Make a profile for them (Manage, see its help), with the scheme From/To with pins and tag format %F%N.
2. Here: pick the project, pick the profile, tick Read every drawing and Trace wires.
3. Plan. Read the list. Tick recommended. Apply ticked.
4. Open a sheet and check a few wire numbers.

Good to know
Recommended means safe and expected. Copies of .wdt and .wdl files, and un-fixing numbers when LSE did not write them, are never recommended: tick them on purpose.
Lines the customer must not change (library and menu paths) are never touched.
Report shows the plan as text. Backups are in <project>\_LSE_ENV_BACKUP. The log is <project>\LSE_EnvReport.txt.
The build tag is at the end of every LSE_Env window title, for example [2026-11-12-r7]. If it is older than the release you installed, AutoCAD Electrical is still running the previous LSE_Env.dll: close AutoCAD Electrical completely, replace the DLL in the bundle's Contents folder, and start it again.
";
 public const string WireScheme=@"SWITCH WIRE-NUMBER SCHEME (LSEENVWIRES) - HELP

What this window does
A quick way to change how a project numbers its wires, without choosing a customer profile. You pick a scheme and press Switch. It is the same safe process as LSEENV: backup first, verify afterwards, roll back on a failure.

The steps
1. Project: the .wdp to change. The line under it shows which profile and scheme LSE last applied to it.
2. Scheme: pick one. The grey text under each scheme says what it does.
3. Press Preview to see the changes as text. Nothing is written.
4. Press Switch. You are asked to confirm, with the number of lines and drawings that will change.

The tick boxes
Write every drawing's WD_M too: changes the numbering settings inside each drawing. Without it existing sheets keep their old settings.
Trace wires: follow the wires to write From/To numbers, or to un-fix them when going back to another scheme.
Queue Insert Wire Numbers: after the window closes, AutoCAD Electrical's retag is started so sequential numbers are renumbered.

What the schemes mean
LSE standard: plain sequential numbers as the LSE template has them.
Sequential with sheet prefix: sheet number then a number, 1-100.
Line reference (grid): numbers from the position on the sheet.
From/To device: each wire is named after the two devices it joins, for example PB1-CR1. The numbers are written as fixed, so retag leaves them alone. Wires with fewer than two tagged devices keep their number and are listed in the report.
From/To with pins: the same with pins, PB1:3-CR1:A1.

Worked example
The customer wants FROM-TO wire numbers with pins.
1. Pick the project.
2. Pick From/To with pins.
3. Tick Write every drawing's WD_M and Trace wires.
4. Preview, read it, then Switch.
5. Open a sheet and check. To go back, pick another scheme and Switch: the fixed numbers are un-fixed and retagged.

Customer formats
Need a different From/To format, pins, or device priority? Make a customer profile (LSEENVPROFILES) with 'Own From/To settings' and use LSEENV.
";
}
