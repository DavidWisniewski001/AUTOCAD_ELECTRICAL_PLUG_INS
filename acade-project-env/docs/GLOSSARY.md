# Glossary: LSE_Env

Maintained by DCW. Last reviewed 2026-11-13.

Terms, acronyms and file types used by LSE_Env and its documents.

| Term | Meaning |
|---|---|
| Profile (customer profile) | One JSON file describing how a customer wants a project configured: a wire scheme plus setting values and raw overrides. Kept in the profiles folder. |
| Wire scheme | A named preset for wire numbering in the rules JSON: fixed values, values from the LSE template, or a From/To block. `LSEENVWIRES` switches between them. |
| Setting map | The `settings[]` table in `LseEnvRules.json` that says which `?[n]` line and which `WD_M` attribute hold a given setting. |
| Confidence | Per setting-map row: `documented` (Autodesk documents it), `inferred` (deduced from the template's values), `unknown` (a guess), `verified` (confirmed with `LSEENVSNAPSHOT` on a workstation). |
| Protected line | A `?[n]` / `+[n]` index (rules `protectedOptions` / `protectedProperties`) or a map row (`protected: true`) that no profile may change. Default: the library and menu paths. |
| Plan | The list of changes `LSEENV` computes before anything is written; each change is a row in the grid and is ticked or not. |
| Note row | A plan row that only informs (change kind `Info`): it shows the word "note" where the Apply? box would be and can never be applied. "Show notes" hides them. |
| Recommended | A change LSE_Env ticks by default. Everything is recommended except `.wdt`/`.wdl` copies and un-fixing wire numbers when the project has no From/To record; those rows are yellow. `LSEENVWIRES` applies only recommended changes. |
| Header tick boxes | "Read every drawing", "Trace wires" and "Queue retag" in `LSEENV`: inputs to the next Plan (the first two) or to Apply (the third), not live switches. |
| Build tag | `BuildInfo.Release`, for example `2026-11-12-r7`, shown in square brackets at the end of every window title; tells a stale DLL apart. |
| Vault working folder | The local copy of the Vault (`C:\_Lake_Shore_E_Vault\`). Files there are read-only until checked out; LSE_Env refuses to write read-only files and lists touched files to check in. |
| Change kind | `SetProperty`, `SetOption`, `SetWdm`, `CopyFile`, `WireNumbers`, `UnfixWireNumbers`, `Marker`, or `Info` (a note, never applied). |
| Marker | `<project>.lseenv.json`: the record of the profile and scheme last applied, with every line LSE_Env has changed in that project. |
| Snapshot / diff | `LSEENVSNAPSHOT`'s copy of every `.wdp` line and `WD_M` value, and the comparison of two such copies that locates a setting. |
| `.wdp` `?[n]`, `+[n]`, `*[n]` | AutoCAD Electrical project file lines: project properties, options (paths and switches), description lines. |
| `WD_M` | AutoCAD Electrical's invisible drawing-settings block; its attributes hold the per-drawing copy of the project settings (`WIREMODE`, `WIREFMT`, `TAGFMT`, `WIRELAYS`, `WIRENO_LAY`, `WIREFIXED_LAY`, `SHEET` ...). |
| Sequential / reference-based | Wire-number (and tag) modes: numbered in order (`S`), or derived from the drawing's reference (ladder rung, X zone, X-Y grid) (`R`). |
| Format referencing | The Drawing Format choice between reference numbers, X zones and X-Y grid; its storage is not located yet (`referenceStyle`). |
| From/To | LSE_Env's own wire-number scheme: `FROM-TO` composed from the tags of the devices a wire network connects. |
| Wire network | All wire segments connected to each other (touching, T-junction, across a wire gap), plus the device terminals, signal arrows and wire-number blocks attached to them. |
| Logical network | Wire networks on different sheets joined by the same signal code (source/destination arrows). They share one wire number. |
| Device terminal | A symbol's connection point: the position of an `X?TERMnn` attribute, with the symbol's tag and the `TERMnn` pin. |
| Terminal strip | A terminal symbol (has a `TAGSTRIP` attribute); sorts last when `terminalsLast` is on. |
| Mark (wire-number mark) | A wire-number block (`WD_WN*`) with its `WIRENO` attribute, sitting on a network. |
| Fixed wire number | A wire number AutoCAD Electrical's retag leaves alone; LSE_Env identifies and writes them by the fixed wire-number layer (`WIREFIXED_LAY`). |
| Un-fix | Moving wire-number blocks from the fixed layer back to the normal layer so the retag can renumber them. |
| Retag | AutoCAD Electrical's own renumbering (Insert Wire Numbers, project-wide); queued through `RetagLisp`. |
| Side database | A closed DWG opened with `ReadDwgFile` outside the editor, edited and saved in place; never a document window. A drawing already open in the editor is reached through its Document instead and is not saved by the plugin. |
| HostBridge | The three delegates (`Inspect`, `ReadWires`, `ApplyDrawing`) through which the planner and the transaction reach AutoCAD; faked by the smoke test. |
| Backup folder | `_LSE_ENV_BACKUP\<stamp>\` inside the project, one per run, pruned to `BackupsToKeep`. |
| Report | `LSE_EnvReport.txt` in the project folder (success log appended) or `Logs\EnvApply_<stamp>.txt` (failure). |
