# Operations guide: LSE_Env

Maintained by DCW. Last reviewed 2026-11-13.

This is the engineer's walkthrough for LSE_Env: install, the six commands, every tick box and field, what a run leaves
behind, the messages and recovery by hand. The `LSEENV` dialog has been opened inside AutoCAD Electrical (2026-11-12:
the window, Plan and the error dialog were seen in the host); its writes have not been run on a live job yet, so the
steps below say where to stop and check.

## 1. Install

1. Build Release (see [DEVELOPER-GUIDE.md](DEVELOPER-GUIDE.md)) or unpack the release zip's build output.
2. Load it one of two ways:
   - **Bundle (automatic):** copy `bundle\LSE_Env.bundle` to `%APPDATA%\Autodesk\ApplicationPlugins\` (one user) or
     `C:\ProgramData\Autodesk\ApplicationPlugins\` (all users) and put the build output (`LSE_Env.dll`,
     `LseEnvRules.json`, `profiles\`) in its `Contents\` folder. The manifest loads the DLL the first time one of the six
     commands is typed (`LoadOnCommandInvocation`), not at start-up.
   - **NETLOAD (one session):** `NETLOAD` and pick `LSE_Env.dll` from the build output. Keep `LseEnvRules.json` and
     `profiles\` next to it; the default rules path and the sample profiles are found relative to the DLL.
3. **Updating:** a .NET DLL cannot be unloaded, so close AutoCAD Electrical completely before replacing the files in
   `Contents\`, then start it again. Every LSE_Env window title ends with the build tag (for example
   `[2026-11-12-r7]`), and `LSEENVDIAG` prints `Build:` on its second line; if an older tag shows, the previous DLL is
   still loaded.
4. `LSEENVSETTINGS`: confirm the rules path, set the profiles folder to a shared vault folder if the whole department
   should see the same customers, confirm the template `.wdp` path. Save. (Fields: section 9.)
5. `LSEENVDIAG`: rules load with 19 setting rows and 5 schemes, the profiles folder lists the three samples, the
   template is found, and the three Vault/runtime lines (host .NET, installed Vault client, Vault add-in login) read
   sensibly.

## 2. Commands

| Command | Window | When |
|---|---|---|
| `LSEENV` | project environment | setting a job up for a customer, or reviewing what a profile would change, change by change |
| `LSEENVWIRES` | switch wire-number scheme | switching the wire-number scheme only, as fast as possible (no grid, no ticking) |
| `LSEENVPROFILES` | customer profiles | defining a customer, copying one, capturing a project's settings as a new profile |
| `LSEENVSNAPSHOT` | setting snapshot and diff | finding out which `?[n]` line / `WD_M` attribute a setting lives in |
| `LSEENVSETTINGS` | settings | paths, backup policy, LISP bridges, default state of the tick boxes |
| `LSEENVDIAG` | diagnostics | health check: build, settings, rules, profiles, Vault/runtime |

`LSEENV`, `LSEENVWIRES`, `LSEENVPROFILES` and `LSEENVSNAPSHOT` start with the project that is active in AutoCAD
Electrical (found through the `ActiveProjectLisp` setting). If none is found, they use the only `.wdp` in the folder of
the active drawing, and if there is not exactly one, the project box starts empty.

## 3. First: confirm the setting map (ten minutes, once per workstation)

The map in `LseEnvRules.json` is inferred. Confirm the rows you will rely on:

1. Open a scratch copy of a project in AutoCAD Electrical and make it active.
2. `LSEENVSNAPSHOT`, leave "Include every drawing's WD_M" ticked, **Take A**. Close the dialog.
3. In AutoCAD Electrical, Project Properties > Wire Numbers: change the format from `%N` to `%S%N`, OK. If asked to
   apply to drawings, say yes for at least one drawing.
4. `LSEENVSNAPSHOT`, **Take B**, **Diff A -> B**. Expect `wdp ?[9] (wireFormat): "%N" -> "%S%N"` and
   `<dwg> WD_M WIREFMT (wireFormat): "%N" -> "%S%N"`. If a different line moved, edit the `wireFormat` row of the
   rules JSON (`property`, `wdm`) and set `"confidence": "verified"`.
5. Repeat for the mode (Sequential vs Line reference), start, increment, suffix list, and for Format Referencing
   (Drawing Format tab: X-Y grid). The lines that move for Format Referencing go into the `referenceStyle` row, which
   ships unmapped. Put the whole change back afterwards (or discard the scratch copy). **B becomes A** saves taking a
   fresh A between two experiments.
6. `LSEENVDIAG` now counts those rows as verified.

The snapshot tick box ("Include every drawing's WD_M") is read when you press Take A or Take B. Ticked, every drawing
listed in the `.wdp` is read for its `WD_M` attributes; a drawing open in the editor is read from the open session
(the label still says "closed drawings only", which is out of date since r2). Unticked, only the `.wdp` lines are
captured. Take A and B the same way, or every drawing shows as "only in A" / "only in B" in the diff.

## 4. Apply a customer profile (LSEENV)

### 4.1 How the window works

The window works in two stages. **The header (project, profile, scheme and the three tick boxes on the right of Plan)
only describes what to plan. Nothing in the header takes effect until you press Plan.** Plan reads the project and
fills the grid. The grid's **Apply?** boxes then choose which of the planned changes **Apply ticked** writes.

```
header: project, profile, scheme, [Read every drawing] [Trace wires] [Queue retag]
   |  press Plan  (reads the header and the project; rebuilds the grid; resets the ticks)
   v
grid: one row per change, [Apply?] box per row, "note" rows have no box
   |  tick / untick rows, Tick recommended / Tick all / Untick all
   v
Apply ticked  (writes only the ticked rows of the LAST plan, then plans again)
```

Three consequences engineers have tripped over:

- Ticking or unticking **Read every drawing** or **Trace wires** after a plan changes nothing on screen. Press Plan
  again. The same holds for a new profile, a new scheme or a new project path: the grid keeps showing the old plan,
  and **Apply ticked stays enabled and would apply that old plan** until you press Plan. The confirmation message
  names the profile and scheme of the plan it will apply; read it.
- When the window opens with a project already filled in, it plans immediately with the tick boxes at their saved
  defaults (`LSEENVSETTINGS`). If you want different boxes, change them and press Plan.
- Plan (and pressing Enter, which is the window's default button) throws away the ticks you set in the grid and
  starts again from the recommended set.

### 4.2 Step by step

1. `LSEENV`. The active project is preset; otherwise **Browse...** (which plans straight away) or type a path and
   press Plan. **Active project** puts the project that was active when the command started back in the box and plans.
2. Pick the **Customer profile**. The wire scheme box switches to "(from the profile)". Override it only to try another
   scheme with the same profile. With "(none: scheme only)" you must pick a scheme, or Plan stops with "Pick a customer
   profile or a wire scheme first."
3. Set the three tick boxes (section 4.3). For a typical customer set-up tick **Read every drawing**; for From/To
   numbering, or when leaving a From/To scheme, tick **Read every drawing** and **Trace wires** together.
4. **Plan**. The grid lists every change: Apply? (a box), Scope, Item, what the project has, what it becomes, the change
   kind, a note. Rows that show the word "note" in the Apply? column are information and cannot be applied. Yellow
   rows can be applied but are unticked by default (file copies, un-fixing without a record). When nothing can be
   applied, a message counts the notes by reason. **Report...** shows the full plan as text, including every From/To
   network per drawing and the plan log (which says, for example, why wire tracing was skipped).
5. Review the grid (section 4.4). Untick anything the customer did not ask for. Tick file copies only when the
   customer's title block or description labels really differ.
6. **Apply ticked**, confirm. The status line shows progress; with "Show the report after each apply" on (the default)
   the run log appears with the backup folder. The window then plans again on its own: rows you left unticked, and
   anything that could not be verified, show up again; everything applied is gone from the grid.
7. Close the window. If the project was the active one (and "Reload the active project" is on), the Project Manager
   reloads it. If **Queue retag after apply** was ticked at the moment you applied, Insert Wire Numbers starts next:
   choose project-wide.
8. Save every drawing that was open in the editor. LSE_Env changed them in the session but does not save them.

### 4.3 The three tick boxes in the header

Their starting state comes from `LSEENVSETTINGS` (section 9). Changing them in this window is for this window only;
it is not saved.

| Tick box | Read when | Effect when ticked | Effect when unticked |
|---|---|---|---|
| **Read every drawing (WD_M values; ...)** | Plan, and Capture... | Every drawing listed in the `.wdp` is opened for reading (an open one through the editor session, a closed one as a side database). The plan then gets one `WD_M` row per drawing whose value differs, plus notes for drawings that cannot be read, have no `WD_M`, or have an older `WD_M` without the attribute. Capture also takes `WD_M`-only values from the first readable drawing. | No drawing is read. **No `WD_M` rows and no drawing notes appear at all**, so only the `.wdp` lines (and file copies) are planned; existing sheets keep their old numbering settings. **Wire tracing is also skipped** (next row). |
| **Trace wires (From/To numbering, un-fixing)** | Plan | With a From/To scheme: every drawing's wires are traced and one "From/To wire numbers" row is planned per drawing that needs numbers. With any other scheme (or a project whose record says From/To was applied): drawings with wire numbers on the fixed layer get an "Un-fix wire numbers" row. | No wire is traced: no From/To rows and no un-fix rows. |
| **Queue retag after apply** | Apply ticked (the value at that moment) | After a successful apply, closing the window sends the `RetagLisp` setting (`c:wd_wnum`, Insert Wire Numbers) to the command line. The confirmation message says "Insert Wire Numbers will be queued afterwards." | Nothing is sent; run Insert Wire Numbers yourself if needed. |

**Trace wires needs Read every drawing in this window.** If Trace wires is ticked and Read every drawing is not, Plan
skips tracing without a grid row; the only trace is the line "Wire tracing: drawings were not read; tick 'Read every
drawing' and plan again" in the Report... log. `LSEENVWIRES` does not have this restriction (section 5). This is the
most likely reason for "the check box does not work"; see [DEVELOPER-GUIDE.md](DEVELOPER-GUIDE.md) Known gaps.

Wire tracing also does nothing (and logs why) outside AutoCAD, and when the profile/scheme involves no From/To at all
and the project has no From/To record (then there is nothing to number or un-fix).

### 4.4 The grid and the bar under it

| Control | What it does | When it has an effect |
|---|---|---|
| **Apply?** box (one per row) | Ticks or unticks that change. The tick is committed as soon as you click, and the summary ("n of m change(s) ticked; k note(s)") and the Apply ticked button update at once. | Only on rows that are changes. A "note" row has no box and ignores clicks. Ticks are kept while you use the buttons and Show notes below, but are reset by Plan and by the re-plan after Apply. |
| **Tick recommended** | Ticks exactly the recommended changes, unticks the rest. | After a plan. Recommended = everything except file copies (`.wdt`/`.wdl`) and un-fixing when there is no From/To record. |
| **Tick all** | Ticks every change (not notes). | After a plan. |
| **Untick all** | Clears every tick. Apply ticked turns grey. | After a plan. |
| **Show notes** (ticked by default) | Shows or hides the note rows. Hiding them only changes the view; ticks are kept. | Immediately, on the current plan. |
| **Apply ticked** | Writes the ticked rows (section 4.6). | Enabled only when the current plan has at least one ticked change. |

Row colours: white = change, ticked by default; yellow = change, not recommended, unticked by default; grey text with
"note" = information only.

### 4.5 Other buttons

| Button | What it does |
|---|---|
| **Manage...** | Opens the profile manager (`LSEENVPROFILES`, section 6) for the project in the box. On return the profile list is reloaded and the selected profile kept. The grid is not re-planned: press Plan. |
| **Capture...** | Asks for a name, reads the project's current mapped values (and, with Read every drawing ticked, `WD_M` values) and saves them as a new profile, then selects it. Protected and unmapped settings are not captured. |
| **Report...** | The plan as text with the log; **Save as...** writes it to a `.txt`. |
| **Help** | The built-in guide to this window. |

### 4.6 What Apply ticked does

1. Asks to confirm, showing the number of ticked changes, the profile, the scheme, how many drawings will be written,
   and whether the retag will be queued.
2. Refuses before anything is written when any file it would touch is read-only (Vault: not checked out). One message
   names every such file and ends with "Nothing was changed." (The window's status line then says "Apply failed and was
   rolled back"; for this refusal nothing had been written, so there was nothing to roll back.)
3. Backs up, writes, verifies, appends the log, prunes old backups (transaction details: [ARCHITECTURE.md](ARCHITECTURE.md)
   section 4).
4. Saves the profile and scheme you used as the next defaults, remembers the project in the recent list.
5. Lists, at the end of the log, every touched file in the Vault working folder that still needs checking in (new
   files: Add to Vault). Drawings open in the editor are left out of that list because they are not saved yet.

## 5. Switch the wire scheme (LSEENVWIRES)

1. `LSEENVWIRES`. The grey line under the project says what LSE_Env last applied to it (from `<project>.lseenv.json`).
   Browse... updates that line.
2. Pick a scheme (one radio button each; the grey text says what it does).
3. Set the tick boxes (table below).
4. **Preview...** shows the plan as text; nothing is written. **Switch** plans again, confirms the counts (project
   lines, `WD_M` values and drawings, From/To drawings, un-fix drawings) and applies.
5. Going **to From/To**: every drawing (open ones through the session) is traced; networks with two or more tagged
   devices get `FROM-TO` numbers on the fixed layer; the report lists each network with its devices and action.
   Networks with fewer than two tagged devices keep their number and are listed with the reason.
6. Going **back to a native scheme**: the `.wdp` lines (and with the first box, the `WD_M` values) change, the From/To
   numbers are un-fixed (moved to the normal wire-number layer), and the retag (if queued) renumbers them. Check the
   un-fix count in the confirmation.
7. Repeat as often as needed; each switch is a backed-up transaction.

| Tick box | Read when | Effect when ticked | Effect when unticked |
|---|---|---|---|
| **Write every drawing's WD_M too** | Preview and Switch | `WD_M` numbering values are planned and written in every drawing that has them (open drawings in the session; save them afterwards). | Only the `.wdp` lines change; existing sheets keep their old numbering settings. Drawings are still read if Trace wires is ticked. |
| **Trace wires: write From/To numbers or un-fix the old ones** | Preview and Switch | Drawings are read and traced; From/To numbers are written, or fixed numbers un-fixed. Works with or without the first box. | No tracing; From/To schemes then change only the settings lines. |
| **Queue Insert Wire Numbers (retag) afterwards** | Switch (the value at that moment) | After a successful switch, closing the window sends `RetagLisp`. | Nothing is sent. |

There is no grid in this window: **Switch applies every recommended change and nothing else.** In particular, un-fixing
is applied only when the project's record says LSE_Env wrote From/To numbers. If the fixed numbers came from somewhere
else (or the `.lseenv.json` was deleted), use `LSEENV`, Plan, and tick the yellow "Un-fix wire numbers" rows by hand.

## 6. Profiles (LSEENVPROFILES)

- **Help** (footer, left of Save): a plain-language guide: what a profile is, the five steps, a worked example, every
  field, the sample profiles and where the files live. `LSEENV` and `LSEENVWIRES` have a Help button as well.
- **Tooltips**: rest the pointer on any control. The wire scheme tooltip lists every scheme from the rules file. In the
  settings grid, the Value cell tooltip shows the setting's label, note and choices.
- **New** / **Duplicate**: fill name, customer, description, pick a scheme. In the settings grid type a value in the
  Value column for each setting the customer mandates; leave blank what should not change. The "Where it lives"
  column shows the mapped line and the confidence; grey rows are unmapped and will only produce a note. Protected rows
  (the layer settings) are not listed.
- **Raw overrides**: `6=S;9=%N` for `?[n]` lines, `16=...` for `+[n]`, `WIREFMT=%N` for `WD_M` attributes. Use these for
  lines the map does not know. A malformed entry stops Save with a message naming it.
- **Title block mapping (.wdt) / Description labels (.wdl) to copy**: full path of a file. Planned as an unticked copy
  when the bytes differ from the project's file.
- **Capture from project...**: reads the project `LSEENVPROFILES` was opened with (or one you browse to) into a new
  profile; it asks whether to read every drawing's `WD_M` too. Name it and Save.
- **Import... / Export...**: JSON files for sharing by mail; an import is kept only after Save. **Retire** moves a
  profile to `_retired\` (nothing is deleted).
- **Save** writes the profile file. Close asks before discarding unsaved changes; so does picking another profile in
  the list.

### 6.1 The From/To tick boxes in the profile editor

| Tick box | What it does | When it has an effect |
|---|---|---|
| **Own From/To settings (otherwise the scheme's are used)** | Enables the From/To fields and tick boxes below it. On Save they are stored as the profile's own `fromTo` block; unticked, the block is removed and the scheme's From/To settings are used. | Only when the profile is applied with a **From/To scheme** (its own Wire scheme, or one picked in `LSEENV`), or with no scheme at all. With a non-From/To scheme the block is ignored. Only `LSEENV` uses profiles; `LSEENVWIRES` always uses the scheme's own settings. |
| **Include pin numbers** | Adds the device pins: `PB1:3-CR1:A1` (pin format field). | Only with Own From/To ticked, and only on From/To runs. |
| **Terminal strips last** | Terminal symbols go to the TO side (`PB1-X1`, not `X1-PB1`). | Same. |
| **Upper case** | Forces capitals. | Same. |
| **Write as fixed wire numbers** | Puts the numbers on the fixed wire-number layer so AutoCAD Electrical's retag leaves them alone. Unticked, they go on the normal wire-number layer and a later retag may renumber them. | Same. |
| **Overwrite existing fixed numbers** | Renumbers networks that already carry a fixed number. Unticked, such networks are skipped and listed in the report. | Same. |

These five boxes are greyed out until Own From/To settings is ticked; that is intentional, not a fault. Changing any of
them marks the profile as unsaved. They take effect the next time `LSEENV` is planned with this profile, after Save.

## 7. What a successful run leaves behind

| Item | Where |
|---|---|
| Backup of every touched file | `<project folder>\_LSE_ENV_BACKUP\<yyyyMMdd_HHmmss>\` (the newest 10 kept) |
| Run log, appended | `<project folder>\LSE_EnvReport.txt` |
| Record of the profile and scheme | `<project>.lseenv.json` next to the `.wdp` |
| Changed `.wdp` | lines changed in place; encoding, line endings and order kept |
| Closed drawings | `WD_M` values and wire-number blocks changed, saved in place |
| Drawings open in the editor | changed in the session, **not saved**: save them, or close without saving to discard |

## 8. Reading the log

```
Started: 2026-11-11T14:02:11 by user@host
Project: C:\...\123456-R.wdp
Profile: ACME  |  Wire scheme: From/To device
Rules: C:\...\LseEnvRules.json
Changes ticked: 9
Backed up 123456-R.wdp to C:\...\_LSE_ENV_BACKUP\20261111_140211
Set project property: ?[7] = 500  (?[7] Wire number start)
Project file written (UTF-8 with BOM): ...
Writing drawing 123456-R-E001.dwg (2 WD_M value(s), 14 wire number(s))
  WD_M WIRE_START: "100" -> "500"
  Wire numbers: 12 renumbered, 2 inserted, 0 skipped (on layer WIREFIXED, fixed)
  Saved ...
Recorded profile in 123456-R.lseenv.json
Verifying
Status: SUCCESS (9 change(s) applied, all verified)
Vault: these 3 item(s) are in the Vault working folder and are not checked in yet. ...
```

`UNRESOLVED:` lines mean the read-back did not show the change; the file was still written and the status reads
`PARTIAL`. For wire numbers the line names the network and what it reads now. A drawing open in the editor logs "Open
in the editor: ..." and "Changes applied in the open editor session; save the drawing to keep them" instead of "Saved".

## 9. Settings (LSEENVSETTINGS)

Stored per user in `%APPDATA%\LSE\LSE_Env\settings.json`. Save checks the values and writes the file; the
**Diagnostics** button shows the `LSEENVDIAG` report. A changed setting is used by the next window you open.

| Field | Default | Meaning |
|---|---|---|
| Rules JSON (setting map, wire schemes) | `LseEnvRules.json` next to the DLL | the shared rules; point every engineer at one copy |
| Profiles folder (share it on the vault) | `%APPDATA%\LSE\LSE_Env\Profiles` | where profiles live; required |
| Template project (.wdp) for template-based schemes | the vault `E-CAD CUSTOM PROJECT TEMPLATE 2.wdp` | source of the "LSE standard (template)" values |
| Backup folder name (inside the project) | `_LSE_ENV_BACKUP` | must be a valid folder name |
| Backups to keep per project | 10 | whole number, 1 or more |
| Active project LISP (sets USERS1) | `ace_getactiveproject` into `USERS1` | how the active project is found |
| Reload project LISP ({WDP}) | `c:wd_makeproj_current` with a guard | sent after an apply to the active project |
| Retag LISP (queued after apply when ticked) | `c:wd_wnum` with a guard | sent when "Queue retag" was ticked |

| Tick box | Default | What it sets |
|---|---|---|
| Read and write every drawing's WD_M by default | on | starting state of "Read every drawing" (`LSEENV`) and "Write every drawing's WD_M too" (`LSEENVWIRES`) |
| Trace wires by default (From/To numbering and un-fixing) | on | starting state of "Trace wires" in both windows |
| Queue the retag LISP after apply by default | off | starting state of "Queue retag" in both windows |
| Reload the active project after it is changed | on | applies at once, no box in the windows: after an apply to the project that was active when the command started, the Reload LISP is sent when the window closes |
| Show the report after each apply | on | applies at once: shows the run log window after every successful apply/switch |

The first three are defaults only. The boxes in `LSEENV` and `LSEENVWIRES` are never written back, so unticking one
in a window lasts until that window closes.

## 10. Messages and what to do

Any error in a form or command opens a dialog titled `LSE_Env: <step>` (for example `LSE_Env: Plan`) with three parts:
**What happened** (the error text), **Where** (the step and the build tag) and **What to do**. The full detail (step,
build, time, user, machine and the complete exception) is saved to `%APPDATA%\LSE\LSE_Env\Logs\Error_<yyyyMMdd_HHmmss>.txt`
and the dialog gives the path. If the same error returns, send that file with the project name. A failed Apply in
`LSEENV` is the exception: it shows the whole rollback log in a report window and writes `EnvApply_<stamp>.txt`.

| Message | Meaning | Do |
|---|---|---|
| This file is read-only / These files are read-only: ... Nothing was changed. | one or more touched files are read-only (Vault: not checked out); all are listed | check them out (Vault tab or Vault Explorer > Check Out), then run again; after a successful apply the log lists what to check in |
| Pick a customer profile or a wire scheme first. | Plan with "(none: scheme only)" and "(from the profile)" | pick a profile or a scheme. This also appears right after opening `LSEENV` on a fresh install with an active project, because no profile or scheme has been used yet |
| Wire tracing: drawings were not read; tick 'Read every drawing' and plan again (in Report...) | Trace wires was ticked without Read every drawing | tick both, Plan |
| Nothing to apply. n note(s): ... | every resolved value already matches, or every candidate is a note | read the reasons; tick Show notes to see each one |
| Nothing to apply: the profile and scheme produced no changes. | the project already has every value | nothing to do |
| Not mapped yet: set it by hand ... | the setting map has no location for this key | section 3, or set it by hand in Project Properties |
| ... has no WIREINC. ... Update to New WD_M Block | an older `WD_M` | Project > Other Tools > Update to New WD_M Block, plan again |
| ... has no WD_M block; open it once in AutoCAD Electrical ... | a drawing with no settings block | open it in AutoCAD Electrical, or repair with LSE_Doctor, plan again |
| ... (open in the editor: written in the session, not saved) | the drawing is open; the change is made in the open session | after Apply, save the drawing (or close it without saving to discard). Rollback restores the file on disk only |
| ... is open in the editor and could not be read: ... | an open drawing whose database could not be read | check the reason after the colon; save or close it and plan again |
| Protected by the rules JSON (protectedOptions); left alone. | the profile tries to change a library/menu path | leave it; LSE owns those |
| Protected in the setting map; a profile's values never change it. | a `protected: true` row (the layer settings) | use a raw override only if the customer really needs it |
| Scheme '...' takes its values from the LSE template, which was not found: ... (in the log) | the template path is wrong or not downloaded from Vault | fix "Template project" in `LSEENVSETTINGS`, or Get Latest in Vault |
| No WD_WN block definition in the drawing or the schematic library paths; ... | nothing to insert for networks without a number | run Insert Wire Numbers once so the blocks exist, apply From/To again |
| only one tagged device (...); existing number kept | a wire network with one device | number by hand, or set "One tagged device" to device in the profile |
| already has a fixed wire number (...) and overwriteFixed is off | the network was skipped | tick Overwrite existing fixed numbers in the profile, or leave it |
| Drawing changes are ticked but no drawing writer is available (outside AutoCAD). | only in the smoke test | none |
| Apply failed and was rolled back. See the log. | an exception during apply (or the read-only refusal above, where nothing had been written) | read the log window; the backups were restored |

## 11. Recovering by hand

Copy the files from the newest `_LSE_ENV_BACKUP\<stamp>\` over the project folder (the `.wdp`, any `.wdt`/`.wdl`,
the drawings listed). A file that collided with another of the same name in the backup carries `(2)`, `(3)` before
its extension. Delete `<project>.lseenv.json` if the run that created it was the failed one. Re-open the project in
the Project Manager so it rereads the `.wdp`. Drawings that were open in the editor during the run: close them without
saving to discard the session changes.
