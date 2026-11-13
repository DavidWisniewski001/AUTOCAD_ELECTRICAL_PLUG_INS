# LSE_Env (AutoCAD Electrical 2026 plugin)

Sets up an existing AutoCAD Electrical project the way a customer wants it, and switches it back. A **customer profile**
holds the project-wide settings a job must meet (wire numbering, component tagging, cross-references, ladder, optional
title-block mapping and description labels); a **wire scheme** is the part engineers change most often, offered on its
own for two-click switching. LSE_Env writes each setting in both places AutoCAD Electrical keeps it: the project file's
`?[n]` / `+[n]` line and every drawing's `WD_M` block attribute.

Wire-number schemes shipped in the rules file:

| Scheme | What the wires get |
|---|---|
| LSE standard (template) | Whatever the live LSE custom project template says (sequential `%N` today). Values are read from the template at apply time. |
| Sequential with sheet prefix | `%S-%N`: 1-100, 1-101, 2-100 ... |
| Line reference (grid) | Reference-based numbers (`WIREMODE` R) from the drawing's format referencing (ladder references, X zones or X-Y grid). |
| From/To device | Each wire network is numbered `FROM-TO` from the tags of the devices it connects (`PB1-CR1`, left device first, terminal strips last). Written as **fixed** wire numbers so AutoCAD Electrical's own retag leaves them alone. |
| From/To with pins | As above with pins: `PB1:3-CR1:A1`. |

From/To is not an AutoCAD Electrical feature. LSE_Env traces the wires itself (lines on the wire layers, symbol
connection attributes `X?TERMnn`, `WD_WN*` wire-number blocks, `SIGCODE` source/destination arrows for cross-sheet
networks), composes the number, and writes it into the existing wire-number block or inserts one. Switching back to a
native scheme un-fixes those numbers so the normal retag can renumber them.

It is the environment counterpart of `acade-project-doctor` (LSE_Doctor, which pulls a project back to the LSE
template) and follows the same shape: settings in `%APPDATA%\LSE\LSE_Env\settings.json`, a shareable rules JSON, a
transaction with backup, verify and rollback, WinForms GUI in the LSE code style. Where the Doctor says "this differs
from the template", LSE_Env says "this is what the customer asked for" and records it in `<project>.lseenv.json`.

**Status (2026-11-12, r6):** compiles in Release with no warnings against the AutoCAD.NET 25.1.0 reference package; the
smoke test in `tests\LSE_Env.SmokeTest` (89 checks: byte-identical `.wdp` round trip, rules validation, profile round trip,
wire-network tracing, From/To composition, planning, apply, verify, rollback, read-only refusal, capture, snapshot diff,
the help texts, the error log, and since r6 the `LSEENV` plan grid filled on a real `DataGridView` and the build label)
passes, all through fake drawing bridges. First run inside AutoCAD Electrical on 2026-11-12 (the dialog, Plan and the
error dialog have been seen in the host; the writes have not). r6 reworks the plan grid so note rows are never built by
replacing a cell (`PlanGridService`, the cause of the r3 "ReadOnly property of a cell" crash) and stamps the build tag
(`BuildInfo.Release`) on every window title, the error dialog, the error log and `LSEENVDIAG`, so a stale DLL is obvious.
See "Not verified on a workstation" before trusting it on a live job. In particular the **setting map** (which `?[n]`
line and `WD_M` attribute hold each setting) is inferred from the template's values, not documented by Autodesk, and
`LSEENVSNAPSHOT` exists to confirm it on the workstation in ten minutes.

## Commands

| Command | Purpose |
|---|---|
| `LSEENV` | Pick a project (defaults to the active one), a customer profile and/or a wire scheme, press Plan, review the grid of changes, Apply with backup. Help button and tooltips on every control |
| `LSEENVWIRES` | Quick switch: pick a wire scheme, Switch. Same transaction, no grid. Help button and tooltips |
| `LSEENVPROFILES` | Create, duplicate, capture from a project, edit, import, export or retire customer profiles. Help button (what a profile is, five steps, worked example, every field) and a tooltip on every editor control |
| `LSEENVSNAPSHOT` | Snapshot the project's `?[n]`/`+[n]` lines and every drawing's `WD_M` values, change a setting in AutoCAD Electrical, snapshot again, diff: tells you which line and attribute a setting lives in |
| `LSEENVSETTINGS` | Rules JSON, profiles folder, template path, backup policy, LISP bridges, defaults for the tick boxes |
| `LSEENVDIAG` | Runtime, settings, rules (setting map with confidence per row), schemes, profiles |

## What a plan contains

| Scope | Change | Ticked by default |
|---|---|---|
| Project properties / options | `?[n]` or `+[n]` line set (or added in index order) to the profile/scheme value | yes, unless the index is protected by the rules JSON (`+[1]..+[4]` library and menu paths by default) |
| Drawing settings (WD_M) | the matching `WD_M` attribute set in every drawing that has the block and the attribute. A drawing open in the editor is read and changed in the session (the row says "open in the editor: written in the session, not saved"; the engineer saves); a closed one is saved in place | yes |
| Project files | customer `.wdt` / `.wdl` copied over the project's | no |
| Wire numbers (From/To) | per drawing: every traced network renumbered or given a new wire-number block | yes |
| Wire numbers (fixed) | per drawing: wire numbers on the fixed layer moved back to the normal layer | yes when the project record says From/To was applied, otherwise no |
| Project | `<project>.lseenv.json` written with profile, scheme, changed indexes and values | yes |

Notes (never applied) cover: a setting the map has not located yet (`referenceStyle` for X-Y grid), a protected
line, a drawing that is open, unreadable or has no `WD_M`, an older `WD_M` that lacks the attribute (with the Autodesk
"Update to New WD_M Block" hint), a template-based scheme whose template is unreachable, networks with fewer than two
tagged devices.

## The apply transaction

1. Validate: at least one change is ticked, the `.wdp` exists, a drawing writer is available when drawing changes are ticked.
2. Refuse read-only files (before any backup, with the Vault check-out hint).
3. Back up every file that will be touched to `<project folder>\_LSE_ENV_BACKUP\<yyyyMMdd_HHmmss>\`.
4. Edit the `.wdp` line by line and write it once; copy `.wdt`/`.wdl`; then per drawing (a closed one opened once as a side database and saved in place; an open one changed through its Document under `LockDocument` and left unsaved): `WD_M` values, From/To wire numbers, un-fix; then write the marker.
5. Verify every ticked change by reading the files and drawings again; anything still different is listed as UNRESOLVED.
6. Append the run log to `<project folder>\LSE_EnvReport.txt`; prune backups beyond "Backups to keep" (default 10).
7. On any exception: restore every backed-up file, delete every file the run created, write the log to `%APPDATA%\LSE\LSE_Env\Logs\`, and show the log.

Any other error in a form or command shows a three-part dialog (What happened / Where / What to do) and writes the full
exception, step, time, user and machine to `%APPDATA%\LSE\LSE_Env\Logs\Error_<stamp>.txt` (`ErrorService`).

If the project is the active one, LSE_Env queues the reload LISP (`c:wd_makeproj_current`) after the dialog closes so
the Project Manager rereads the `.wdp`; with "Queue retag" ticked it then queues `c:wd_wnum` (Insert Wire Numbers) so
the engineer can run it project-wide. Both are settings, both unverified.

## Configuration

- `LSEENVSETTINGS` (per user, `%APPDATA%\LSE\LSE_Env\settings.json`): rules JSON path, profiles folder (default
  `%APPDATA%\LSE\LSE_Env\Profiles`; point it at a vault share so every engineer sees the same customers), template
  `.wdp` (same default as LSE_Doctor), backup folder name and count, the three LISP expressions, default tick boxes.
- `config\LseEnvRules.json` (shared): the **setting map** (key, label, group, `?[n]` index, `+[n]` index, `WD_M`
  tag, kind, choices, confidence, protected), the **wire schemes**, the **From/To rules** (block names, attributes,
  tolerances), protected indexes, friendly labels. Every key is explained in the file header.
- `config\profiles\` (copied to `profiles\` next to the DLL, seeded into the profiles folder on first use): `LSE
  Standard`, `Example - Grid customer`, `Example - FromTo customer`. A profile is one JSON file: name, customer,
  description, scheme, `values` (setting keys), raw `properties` / `options` / `drawing` overrides, optional `fromTo`
  block, optional `.wdt` / `.wdl` sources, notes.

## Build

- Target `net8.0-windows`, WinForms, x64, nullable. No NuGet packages at runtime.
- With AutoCAD Electrical 2026 installed: `dotnet build -c Release` (references `accoremgd/acdbmgd/acmgd` from
  `C:\Program Files\Autodesk\AutoCAD 2026`, or pass `-p:AcadDir=...`).
- Without an install the project compiles against the `AutoCAD.NET` 25.1.0 reference package (compile-time only).
- Smoke test: `dotnet build tests\LSE_Env.SmokeTest -c Release` then run `tests\LSE_Env.SmokeTest\bin\Release\LSE_Env.SmokeTest.dll`
  (it references `bin\Release\LSE_Env.dll` and works in a temp folder; no AutoCAD needed).

## Deploy / load

1. Build Release; copy the output folder (DLL, `LseEnvRules.json`, `profiles\`) to a share or each workstation.
2. `NETLOAD` the DLL, or copy `bundle\LSE_Env.bundle` to `%APPDATA%\Autodesk\ApplicationPlugins\` with the build
   output in `Contents\` for automatic loading.
3. Run `LSEENVDIAG`: the rules must load with 19 setting rows and 5 schemes; the profiles folder shows the three samples.
4. **Confirm the setting map first** (`docs\OPERATIONS.md` section 3): `LSEENVSNAPSHOT` A, change the wire number format
   in Project Properties, B, Diff. Fix any row of the rules JSON that disagrees and set its confidence to `verified`.
5. Run `LSEENV` on a copy of a project, Plan, read the grid, then Apply. Try `LSEENVWIRES` both ways on the copy.

## Not verified on a workstation

- The setting map: that `?[6]`/`WIREMODE` is the wire-number mode with values `S`/`R`, `?[9]`/`WIREFMT` the format,
  `?[7]`/`WIRE_START`, `?[10]`/`WIREINC`, `?[8]`/`WIRE_RSUF`, `?[2..5]` the component-tag block, `?[14]`/`XREFFMT`,
  `?[0]`/`RUNGHORV`. These were inferred from the LSE template's values. Where the X-Y grid / format referencing choice
  lives is unknown; the "Line reference (grid)" scheme sets `WIREMODE` R and tells the engineer to set Format
  Referencing by hand until the snapshot diff locates it.
- Writing into a drawing that is open in the editor (`DocumentLock` on `Document.Database`, no save): standard AutoCAD .NET practice, untested here. Rolling back an apply restores files on disk only; discard session changes by closing the drawing without saving.
- Whether changing a drawing's `WD_M` attributes on a side database is enough for AutoCAD Electrical to use the new
  numbering on the next retag, or whether the Project Properties dialog must be opened once.
- The wire-number block name (`WD_WN*`), its `WIRENO` attribute, that fixed wire numbers are the ones on the
  `WIREFIXED_LAY` layer, and that `WD_WN.dwg` can be imported from the schematic library path when a drawing lacks it.
- The symbol connection attributes (`X1TERM01` .. `X8TERMnn`), that their position is the wire end, that `TERMnn` holds
  the pin, `TAG1`/`TAG2`/`TAGSTRIP` the tag, `SIGCODE` the signal code; the tolerances (join 0.001, connect 0.02, gap
  0.25, mark 0.35 drawing units).
- `(ace_getactiveproject)`, `c:wd_makeproj_current` and `c:wd_wnum` as LISP entry points.
- LSE_Doctor does not yet read `<project>.lseenv.json`; after a customer profile is applied the Doctor will report the
  changed `?[n]` lines as off-standard. Until its guardian teaches it the marker, list those indexes in the Doctor's
  `protectedProperties`, or simply untick them in the Doctor's grid.

## Documentation

| Document | Read it for |
|---|---|
| this README | overview, commands, what a plan contains, the transaction, configuration, build, deploy, what is not verified |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | components, dependency diagram, plan and apply data flow, the From/To engine, rollback, design decisions, relationship to LSE_Doctor |
| [docs/CODE-REFERENCE.md](docs/CODE-REFERENCE.md) | every file, type and public member, the six commands, the project and manifest files |
| [docs/DATA-STORES.md](docs/DATA-STORES.md) | settings.json, the rules JSON, profiles, the marker, snapshots, backups, reports and logs |
| [docs/OPERATIONS.md](docs/OPERATIONS.md) | install, confirming the setting map, applying a profile, switching schemes, profiles, reading the log, messages, recovery by hand |
| [docs/DEVELOPER-GUIDE.md](docs/DEVELOPER-GUIDE.md) | build with or without AutoCAD, the smoke test, conventions, recipes, first-build checklist, known gaps |
| [docs/GLOSSARY.md](docs/GLOSSARY.md) | terms and file extensions |

These documents are maintained with the `lse-documenter` agent definition in `../agents/lse-documenter.md`.

Maintained by DCW. Last reviewed 2026-11-12.
