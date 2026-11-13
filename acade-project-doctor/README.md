# LSE_Doctor (AutoCAD Electrical 2026 plugin)

Examines an existing AutoCAD Electrical project against the LSE custom project template and repairs what is missing
or off-standard: the `*[n]` project description lines, the `+[n]` library and icon-menu paths and other options, the
`?[n]` project properties, the `<project>.wdt` title block mapping, the `<project>_wdtitle.wdl` description labels,
the `.aepx` project configuration, and (optionally, per drawing) the `WD_M` drawing-settings block, the wire-layer
settings it carries, and the wire layers themselves (missing ones created, off-standard ones repaired, unused
non-standard ones purged).

The standard is not hard-coded. The doctor reads the live LSE template project (`.wdp` + `.wdt` + `_wdtitle.wdl`
+ `DRAWING-#.dwg`) from the vault and diffs the project against it, so updating the template updates the standard.
A copy of the template's three text files ships next to the DLL as a fallback when the vault path is not reachable.

It is the repair-shop counterpart of `acade-project-creator` (LSE Project Tools, which creates new projects) and
follows the same shape: settings in `%APPDATA%\LSE\...\settings.json`, a shareable rules JSON, a transaction with
backup, verify and rollback, WinForms GUI in the LSE code style.

**Status (2026-11-10):** compiles in Release with no warnings against the AutoCAD.NET 25.1.0 reference package; the
text-file diagnosis and repair logic and the wire-layer comparison logic passed a 90-check console smoke test on a copy
of the real LSE template; the plugin has not yet been loaded into AutoCAD Electrical. See "Not verified on a
workstation" below before trusting it on a live job.

## Commands

| Command | Purpose |
|---|---|
| `LSEDOCTOR` | Examine one project (defaults to the active project), review the findings grid, tick repairs, repair with backup |
| `LSEDOCTORSCAN` | Walk a folder tree (default: the Custom Sales Order root), triage every `.wdp` found, batch-apply the recommended repairs |
| `LSEDOCTORSETTINGS` | Template path, rules JSON, scan root, backup policy, drawing checks, LISP bridges |
| `LSEDOCTORDIAG` | Runtime, settings, rules load, standard parse (counts, title block names, template drawing) |

## What it checks and what it repairs

| Category | Check | Repair (recommended = ticked by default) |
|---|---|---|
| Project descriptions | every `*[n]` line the template has (1..8) exists | missing line added with the template placeholder (`PROJECT NAME`, `#####`, `CUST 1` ...); blank lines are reported but left alone; lines with text are never changed |
| Library and menu paths | `+[1]` schematic library list, `+[2]` schematic icon menu, `+[3]` footprint library list, `+[4]` panel icon menu match the template (case-insensitive) | missing line added; differing value replaced (`replace` mode) or template paths put first and the project's extras kept (`merge` mode) |
| Project options | every other `+[n]` equals the template | missing added; differing replaced when `enforceOptions` is true, otherwise listed unticked |
| Project properties | every `?[n]` equals the template | missing added; differing replaced when `enforceProperties` is true |
| Title block mapping (.wdt) | file exists; `BLOCK` line names the LSE title block; every attribute mapping of the template is present and equal | missing file copied from the template; missing `BLOCK` or attribute lines added; LSE block name appended to an existing `BLOCK` line; differing codes replaced when `enforceMappings` is true |
| Description labels (.wdl) | file exists; `LINE1..LINE14` labels match | missing file copied from the template; missing labels added; differing labels replaced when `enforceLabels` is true |
| Project configuration (.aepx) | file exists | information only by default; tick to create a minimal one (AutoCAD Electrical 2024+ recreates it itself) |
| Drawings | every listed drawing exists on disk; with "Check every drawing" on, each DWG carries a `WD_M` block and the LSE title block (`ECAD_LSE_CUSTOM_TB_2.0` from the template `.wdt`) | missing file: unticked "remove from project list"; missing `WD_M`: block definition and attribute values copied from the template `DRAWING-#.dwg` into model space; missing title block: reported only |
| Wire layer settings (WD_M) | with "Check every drawing" on: the drawing's `WD_M` values for `WIRELAYS` (comma list, wildcards), `WIRENO_LAY`, `WIRECOPY_LAY`, `WIREFIXED_LAY`, `WIREREF_LAY`, `FAN_INOUT_LAYS` equal the template drawing's | differing value set to the template value when `enforceWireLayers` is true; an attribute the drawing's older `WD_M` lacks is reported with the Autodesk "Update to New WD_M Block" hint |
| Wire layers | with "Check every drawing" on: every wire layer of the template drawing (layers matching its `WIRELAYS`, named by its `*_LAY` attributes, or carrying wire-type xdata) exists with the same color, linetype, lineweight and wire-type data; no other wire layers exist | missing layer cloned from the template with its linetype and wire-type xdata; off-standard layer repaired in place (entities stay); non-standard wire layer with nothing on it purged when `purgeUnusedWireLayers` is true; non-standard wire layer still in use reported only (the doctor never moves entities) |

Lines, mappings and labels that the project has but the template does not are listed as information and left alone.
Layers are never switched on/off, thawed, frozen or unlocked; layers `0` and `DEFPOINTS` are never purged.
The `.wdp` is edited line by line: existing lines keep their order, encoding (UTF-8 with BOM, UTF-16 or ANSI) and
line endings; new lines are inserted in index order inside their `*` / `+` / `?` group.

## The repair transaction

1. Validate: at least one finding is ticked, the `.wdp` exists.
2. Back up every file that will be touched to `<project folder>\_LSE_DOCTOR_BACKUP\<yyyyMMdd_HHmmss>\`.
3. Apply `.wdp` edits, then `.wdt`, `.wdl`, `.aepx`, then the drawing repairs grouped per DWG (each drawing is opened
   once as a side database: `WD_M` insertion, layer creation, layer repair, `WD_M` value changes, layer purge, then
   saved in place).
4. Re-examine the text files and re-inspect each repaired drawing; anything still reported is listed as UNRESOLVED.
5. Append the run log to `<project folder>\LSE_DoctorReport.txt`; prune backups beyond "Backups to keep" (default 10).
6. On any exception: restore every backed-up file, delete every file the run created, write the log to
   `%APPDATA%\LSE\LSE_Doctor\Logs\`, and show the log.

If the repaired project is the active one, the doctor sends the reload LISP (`c:wd_makeproj_current`) after the dialog
closes so the Project Manager rereads the `.wdp`. If that function is not present on your release, re-open the project
from the Project Manager by hand; otherwise AutoCAD Electrical may later overwrite the repaired file with the settings it
still holds in memory.

## Configuration

- `LSEDOCTORSETTINGS` (per user, `%APPDATA%\LSE\LSE_Doctor\settings.json`): template `.wdp`
  (default `C:\_Lake_Shore_E_Vault\CAD Standards\AutoCAD\ECAD Standards\Project Templates\CUSTOM TEMPLATES\CUSTOM PROJECT TEMPLATE 2.0\E-CAD CUSTOM PROJECT TEMPLATE 2.wdp`),
  rules JSON, scan root, backup folder name and count, drawing-check defaults, the two LISP expressions.
- `config\LseDoctorRules.json` (shared standard, point Settings at a vault copy): description line count, placeholder
  policy, enforce switches, `replace`/`merge` path list mode, which `+[n]` are path lists, protected indexes, whether
  the `.wdt`/`.wdl`/`.aepx` are required, the `WD_M` block name, title block names, friendly labels for `+[n]`/`?[n]`,
  and the wire-layer policy (`checkWireLayers`, `enforceWireLayers`, `purgeUnusedWireLayers`, which `WD_M` attributes
  name wire layers, which of them are lists, the xdata application pattern that marks a wire type, layers never to
  purge). Every key is explained in the file header.
- `config\standard\` (copied to `standard\` next to the DLL): the template's `.wdp`, `.wdt` and `_wdtitle.wdl` as the
  fallback standard. No drawing is bundled, so `WD_M` insertion and the wire-layer check need the vault template folder.

## Build

- Target `net8.0-windows`, WinForms, x64, nullable. No NuGet packages at runtime.
- With AutoCAD Electrical 2026 installed: `dotnet build -c Release` (references `accoremgd/acdbmgd/acmgd` from
  `C:\Program Files\Autodesk\AutoCAD 2026`, or pass `-p:AcadDir=...`).
- Without an install the project compiles against the `AutoCAD.NET` 25.1.0 reference package (compile-time only).

## Deploy / load

1. Build Release; copy the output folder (DLL, `LseDoctorRules.json`, `standard\`) to a share or each workstation.
2. `NETLOAD` the DLL, or copy `bundle\LSE_Doctor.bundle` to `%APPDATA%\Autodesk\ApplicationPlugins\` with the
   build output in `Contents\` for automatic loading.
3. Run `LSEDOCTORDIAG`: the standard must parse with 8 descriptions, 38 options, 75 properties, 11 mappings,
   14 labels and (only when the Vault template folder is reachable; the bundled copy has no drawings) a template drawing. Fix anything marked MISSING or FAIL in `LSEDOCTORSETTINGS`.
4. Run `LSEDOCTOR` on a copy of a project first and read the findings before repairing a live job.

## Not verified on a workstation

- `(ace_getactiveproject)` returning the active `.wdp` path, and `Editor.Command` evaluating the LISP synchronously so
  the `USERS1` round trip works. If it does not, the dialog simply opens without a preset project; browse to it.
- `c:wd_makeproj_current` as the reload function (forum answer, not the official API). Change it in Settings if your
  release names it differently.
- That AutoCAD Electrical accepts a `.wdp` whose `+[n]` group precedes the `?[n]` group and vice versa (the LSE template
  writes `*`, `+`, `?`; LSE Project Tools writes `*`, `?`, `+`; the doctor preserves whichever order a project has).
- `WD_M` insertion via `WblockCloneObjects` + `SaveAs` on a side database, and that AutoCAD Electrical then treats the
  drawing as configured. Test on a copy of one drawing before batch use.
- Layer repairs on a side database: cloning a layer table record from the template drawing (`WblockCloneObjects` into
  the layer table), copying layer xdata between databases, `Database.Purge` + `Erase` for the purge, and that AutoCAD
  Electrical then treats the created or repaired layer as a wire type. The xdata application pattern `VIA_WD*` in the
  rules JSON is a guess: Autodesk documents the `WD_M` attribute names but not the layer xdata that marks a wire type.
  If a repaired layer is still not a wire type, inspect a template wire layer with the xdata editor and fix the pattern.
- Meaning of the individual `?[n]` / `+[n]` indexes beyond `+[1]..+[4]`; the doctor compares them as opaque values.

## Documentation

| Document | Read it for |
|---|---|
| this README | overview, commands, checks and repairs, the repair transaction, configuration, build, deploy, what is not verified |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | components, dependency diagram, examine and repair data flow, rollback, threading model, design decisions, relationship to LSE Project Tools |
| [docs/CODE-REFERENCE.md](docs/CODE-REFERENCE.md) | every file, type and public member, the four commands, the project and manifest files |
| [docs/DATA-STORES.md](docs/DATA-STORES.md) | settings.json and the rules JSON field by field, the LSE template files, project files, backups, reports and logs |
| [docs/OPERATIONS.md](docs/OPERATIONS.md) | install, examine and repair step by step, the scan, the findings catalogue, settings, reading the log, every message, recovery by hand |
| [docs/DEVELOPER-GUIDE.md](docs/DEVELOPER-GUIDE.md) | build with or without AutoCAD, debug, conventions, recipes, first-build checklist, test strategy, known gaps |
| [docs/GLOSSARY.md](docs/GLOSSARY.md) | terms and file extensions |

These documents are maintained with the `lse-documenter` agent definition in `../agents/lse-documenter.md`.
