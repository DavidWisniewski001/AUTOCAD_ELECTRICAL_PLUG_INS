# Operations guide: LSE_Env

Maintained by DCW. Last reviewed 2026-11-12 (r6).

Nothing in this guide has yet been run inside AutoCAD Electrical; the steps describe the intended behaviour and say
where to stop and check.

## 1. Install

1. Build Release (see [DEVELOPER-GUIDE.md](DEVELOPER-GUIDE.md)) or unpack the release zip's build output.
2. `NETLOAD` `LSE_Env.dll`, or copy `bundle\LSE_Env.bundle` to `%APPDATA%\Autodesk\ApplicationPlugins\` with the
   build output (DLL, `LseEnvRules.json`, `profiles\`) in `Contents\`.
   **Updating:** a .NET DLL cannot be unloaded, so close AutoCAD Electrical completely before replacing the files in
   `Contents\`, then start it again. Every LSE_Env window title ends with the build tag (for example `[2026-11-12-r6]`),
   and `LSEENVDIAG` prints `Build:`; if an older tag shows, the previous DLL is still loaded.
3. `LSEENVSETTINGS`: confirm the rules path, set the profiles folder to a shared vault folder if the whole department
   should see the same customers, confirm the template `.wdp` path. Save.
4. `LSEENVDIAG`: rules load with 19 setting rows and 5 schemes, the profiles folder lists the three samples, the
   template is found.

## 2. Commands

| Command | When |
|---|---|
| `LSEENV` | setting a job up for a customer, or reviewing what a profile would change |
| `LSEENVWIRES` | switching the wire-number scheme only, as fast as possible |
| `LSEENVPROFILES` | defining a customer, copying one, capturing a project's settings as a new profile |
| `LSEENVSNAPSHOT` | finding out which `?[n]` line / `WD_M` attribute a setting lives in |
| `LSEENVSETTINGS`, `LSEENVDIAG` | configuration and health |

## 3. First: confirm the setting map (ten minutes, once per workstation)

The map in `LseEnvRules.json` is inferred. Confirm the rows you will rely on:

1. Open a scratch copy of a project in AutoCAD Electrical and make it active. Close its drawings.
2. `LSEENVSNAPSHOT`, tick "Include every drawing's WD_M", **Take A**. Close the dialog.
3. In AutoCAD Electrical, Project Properties > Wire Numbers: change the format from `%N` to `%S%N`, OK. If asked to
   apply to drawings, say yes for at least one drawing (or set it in Drawing Properties of one closed drawing by
   opening it, changing, saving, closing).
4. `LSEENVSNAPSHOT`, **Take B**, **Diff A -> B**. Expect `wdp ?[9] (wireFormat): "%N" -> "%S%N"` and
   `<dwg> WD_M WIREFMT (wireFormat): "%N" -> "%S%N"`. If a different line moved, edit the `wireFormat` row of the
   rules JSON (`property`, `wdm`) and set `"confidence": "verified"`.
5. Repeat for the mode (Sequential vs Line reference), start, increment, suffix list, and for Format Referencing
   (Drawing Format tab: X-Y grid). The lines that move for Format Referencing go into the `referenceStyle` row, which
   ships unmapped. Put the whole change back afterwards (or discard the scratch copy).
6. `LSEENVDIAG` now shows those rows as verified.

## 4. Apply a customer profile (LSEENV)

1. `LSEENV`. The active project is preset; otherwise Browse.
2. Pick the **Customer profile**. The wire scheme box shows "(from the profile)"; override it only to try another
   scheme with the same profile.
3. Tick boxes: **Read every drawing** (needed for any `WD_M` change; drawings open in the editor are read too), **Trace wires** (needed for
   From/To numbering and for un-fixing), **Queue retag** (sends Insert Wire Numbers after the dialog closes).
4. **Plan**. The grid lists every change: Apply? (a box), Scope, Item, what the project has, what it becomes, the change kind, a note. Rows marked "note" have no box: they are information and cannot be applied. When nothing can be applied a message counts the notes by reason.
   Yellow rows are applicable but unticked by default (file copies, un-fixing without a record). Grey rows are notes.
   **Report...** shows the full plan including every From/To network per drawing.
5. Review. Untick anything the customer did not ask for. Tick file copies only when the customer's title block or
   description labels really differ.
6. **Apply ticked**, confirm. The status line shows progress; the report dialog shows the log with the backup folder.
   The grid re-plans; it should now show notes only.
7. If the project was the active one, the Project Manager reloads it when the dialog closes. If retag was ticked,
   Insert Wire Numbers opens next: choose project-wide.

## 5. Switch the wire scheme (LSEENVWIRES)

1. `LSEENVWIRES`. The label shows what is on record for the project.
2. Pick a scheme. **Preview...** shows the plan as text; **Switch** confirms the counts and applies.
3. Going **to From/To**: every drawing (open ones through the session) is traced; networks with two or more tagged devices get `FROM-TO`
   numbers on the fixed layer; the report lists each network with its devices and action. Networks with fewer than
   two tagged devices keep their number and are listed with the reason.
4. Going **back to a native scheme**: the `WD_M` and `.wdp` lines change, the From/To numbers are un-fixed (moved to
   the normal wire-number layer), and the retag (if queued) renumbers them. Check the un-fix count in the confirmation.
5. Repeat as often as needed; each switch is a backed-up transaction.

## 6. Profiles (LSEENVPROFILES)

- **Help** (footer, left of Save): opens a plain-language guide: what a profile is, the five steps, a worked example
  (FROM-TO numbers with pins and tag format `%F%N`), what every field means, the sample profiles and where the files
  live. `LSEENV` and `LSEENVWIRES` have a Help button next to Close as well.
- **Tooltips**: rest the pointer on any control of the editor (and on the left buttons) for a short explanation. The
  wire scheme tooltip lists every scheme from the rules file with its description. In the settings grid, the Value
  cell tooltip shows the setting's label, note and choices. On `LSEENV`, the project, profile, scheme, tick boxes,
  Plan, tick buttons, Manage..., Capture... and Apply ticked all have tooltips.
- **New** / **Duplicate**: fill name, customer, description, pick a scheme. In the settings grid type a value in the
  Value column for each setting the customer mandates; leave blank what should not change. The "Where it lives"
  column shows the mapped line and the confidence; grey rows are unmapped and will only produce a note.
- **Raw overrides**: `6=S;9=%N` for `?[n]` lines, `16=...` for `+[n]`, `WIREFMT=%N` for `WD_M` attributes. Use these for
  lines the map does not know.
- **Own From/To settings**: tick to override the scheme's block (format, pins, device priority regexes such as
  `^PB` to make push buttons FROM, duplicate suffix, fixed, policies).
- **Capture from project...**: reads the active or chosen project's current values into a new profile; name it and
  Save. Handy after setting a job up by hand once.
- **Import... / Export...**: JSON files for sharing by mail. **Retire** moves a profile to `_retired\`.

## 7. What a successful run leaves behind

`_LSE_ENV_BACKUP\<stamp>\` with every touched file, `LSE_EnvReport.txt` with the log appended, `<project>.lseenv.json`
with profile, scheme and the indexes changed, the `.wdp` with the lines changed in place (encoding and order kept),
each written drawing saved in place with its `WD_M` values and wire-number blocks changed.

## 8. Reading the log

```
Started: 2026-11-11T14:02:11 by user@host
Project: C:\...\123456-R.wdp
Profile: ACME  |  Wire scheme: From/To device
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
```

`UNRESOLVED:` lines mean the read-back did not show the change; the file was still written. For wire numbers the
line names the network and what it reads now.

## 9. Messages and what to do

Any error in a form or command opens a dialog titled `LSE_Env: <step>` (for example `LSE_Env: Plan`) with three parts:
**What happened** (the error text), **Where** (the step that failed) and **What to do**. The full detail (step, time,
user, machine and the complete exception with stack) is saved to `%APPDATA%\LSE\LSE_Env\Logs\Error_<yyyyMMdd_HHmmss>.txt`
and the dialog gives the path. If the same error returns, send that file with the project name. A failed Apply is the
exception: it shows the whole rollback log in a report window (and writes `EnvApply_<stamp>.txt` as before).

| Message | Meaning | Do |
|---|---|---|
| Read-only file ... Nothing was changed | a touched file is read-only (Vault) | check out, or clear the attribute |
| Not mapped yet: set it by hand ... | the setting map has no location for this key | section 3, or set it by hand in Project Properties |
| ... has no WIREINC. ... Update to New WD_M Block | an older `WD_M` | Project > Other Tools > Update to New WD_M Block, plan again |
| ... (open in the editor: written in the session, not saved) | the drawing is open; the change is made in the open session | after Apply, save the drawing (or close it without saving to discard). Rollback restores the file on disk only |
| ... is open in the editor and could not be read: ... | an open drawing whose database could not be read | check the reason after the colon; save or close it and plan again |
| Protected by the rules JSON | the profile tries to change a library/menu path | leave it; LSE owns those |
| No WD_WN block definition in the drawing or the schematic library paths | nothing to insert for networks without a number | run Insert Wire Numbers once so the blocks exist, apply From/To again |
| only one tagged device ... existing number kept | a wire network with one device | number by hand, or set `singleDevice: device` |
| The project already uses this scheme | nothing to change | nothing |
| Apply failed and was rolled back | an exception during apply | read the log dialog; the backups were restored |
| LSE_Env: <step> with What happened / Where / What to do | an unexpected error in that step | follow "What to do"; send the `Error_<stamp>.txt` file if it repeats |

## 10. Recovering by hand

Copy the files from the newest `_LSE_ENV_BACKUP\<stamp>\` over the project folder (the `.wdp`, any `.wdt`/`.wdl`,
the drawings listed). Delete `<project>.lseenv.json` if the run that created it was the failed one. Re-open the
project in the Project Manager so it rereads the `.wdp`.
