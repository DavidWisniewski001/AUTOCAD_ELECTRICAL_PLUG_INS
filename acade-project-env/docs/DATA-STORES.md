# Data stores: LSE_Env

Maintained by DCW. Last reviewed 2026-11-13.

Every file LSE_Env reads or writes: per-user settings, the shared rules JSON, profiles, the project's own files, the
per-project marker, backups, reports, logs and snapshots. LSE_Env uses no database and no registry key; it does not
talk to the Vault server, it only refuses to write read-only (not checked out) files.

## 1. Inventory

| Store | Where | Written by | Read by |
|---|---|---|---|
| `settings.json` | `%APPDATA%\LSE\LSE_Env\` | `EnvSettingsForm`, `SettingsService.Remember` (after every successful apply), `EnvironmentForm` and `WireSchemeForm` (last profile/scheme after an apply). The window tick boxes are never written back | every command |
| `LseEnvRules.json` | next to the DLL by default; `RulesConfigPath` may point at a share | nobody at run time (edited by hand) | every command |
| Profiles `*.json` | `ProfilesFolder` (default `%APPDATA%\LSE\LSE_Env\Profiles\`, share it on the vault); `_retired\` inside it | `ProfilesForm`, `EnvironmentForm` Capture, first-use seeding from `profiles\` next to the DLL | `LSEENV`, `LSEENVPROFILES`, `LSEENVDIAG` |
| `<project>.wdp` | the project folder | `ApplyService` (line edits only) | plan, snapshot, capture |
| `<project>.wdt`, `<project>_wdtitle.wdl` | the project folder | `ApplyService` (whole-file copy when ticked) | plan (byte comparison) |
| Drawings `*.dwg` | the project | `DrawingSettingsService.Apply` (`WD_M` values, wire-number blocks, layers of wire-number blocks). Closed drawings are saved in place; drawings open in the editor are changed in the session and left unsaved | inspect, wire read |
| `<project>.lseenv.json` (marker) | next to the `.wdp` | `ApplyService` | plan, `LSEENVWIRES` label |
| `_LSE_ENV_BACKUP\<stamp>\` | the project folder | `ApplyService` | rollback, recovery by hand |
| `LSE_EnvReport.txt` | the project folder | `ApplyService` (append) | engineers |
| `Logs\EnvApply_<stamp>.txt` | `%APPDATA%\LSE\LSE_Env\` | `ApplyService` on failure | engineers |
| `Logs\Error_<stamp>.txt` | `%APPDATA%\LSE\LSE_Env\` | `ErrorService` on any form or command error | engineers, support |
| `Snapshots\last_A.json`, `last_B.json` | `%APPDATA%\LSE\LSE_Env\` | `SnapshotForm` | `SnapshotForm` |

## 2. settings.json

| Field | Default | Meaning |
|---|---|---|
| `RulesConfigPath` | `<DLL folder>\LseEnvRules.json` | the shared rules |
| `ProfilesFolder` | `%APPDATA%\LSE\LSE_Env\Profiles` | where profiles live; seeded from `profiles\` next to the DLL when empty |
| `TemplateProjectPath` | the vault template `.wdp` (same as LSE_Doctor) | source of values for `fromTemplate` schemes |
| `BackupFolderName`, `BackupsToKeep` | `_LSE_ENV_BACKUP`, 10 | per-project backups |
| `ApplyToDrawings` | true | starting state of "Read every drawing" (`LSEENV`) and "Write every drawing's WD_M too" (`LSEENVWIRES`) |
| `ComputeFromTo` | true | starting state of "Trace wires" in both |
| `RetagAfterApply` | false | starting state of "Queue retag" in both |
| `ReactivateAfterApply` | true | reload the project after an apply when it was the active one at command start |
| `ShowReportAfterApply` | true | show the run log after apply |
| `RecentProjects` | empty | up to 25, newest first; the `LSEENV` project list |
| `ActiveProjectLisp` | `ace_getactiveproject` into `USERS1` | how the active project is found |
| `ActivateProjectLisp` | `c:wd_makeproj_current "{WDP}"` | how it is reloaded |
| `RetagLisp` | `(c:wd_wnum)` with a `boundp` guard | what "Queue retag" sends |
| `LastProfile`, `LastScheme` | empty | dialog presets, saved after an apply. While both are empty, `LSEENV` opened on an active project starts with no profile and no scheme and its automatic first plan stops with "Pick a customer profile or a wire scheme first." |

`ConnectTolerance` (0.02) is not in the shipped rules file; the code default applies.

## 3. LseEnvRules.json

Comments are allowed. Keys are case-insensitive.

| Key | Meaning |
|---|---|
| `wdmBlockName` | the drawing-settings block (`WD_M`) |
| `settings[]` | the setting map. Per row: `key` (used by profiles and schemes), `label`, `group`, `property` (`?[n]` index or -1), `option` (`+[n]` index or -1), `wdm` (attribute tag or `""`), `kind` (`text`, `choice`, `number`), `choices` (`value=meaning;...`), `confidence` (`documented`, `inferred`, `unknown`, `verified`), `note`, `protected` |
| `wireSchemes[]` | presets: `name`, `description`, `values` (key -> value), `fromTemplate` + `templateKeys` (read those keys from the template `.wdp`), `fromTo` (a From/To settings block; its presence makes the scheme a From/To scheme) |
| `fromTo` | the From/To rules: `wireNumberBlocks`, `wireNumberAttribute`, `insertBlockName`, `insertMissing`, `insertOffset`, `connectionAttributePattern`, `pinAttributePrefix`, `tagAttributes`, `terminalStripAttribute`, `signalCodeAttribute`, `ignoreBlocks`, `joinTolerance`, `connectTolerance`, `gapTolerance`, `markTolerance` |
| `protectedOptions`, `protectedProperties` | indexes no profile may change (default `+[1]..+[4]`) |
| `labels` | friendly names for lines the map does not cover (`"+1"`, `"?25"`) |

The shipped map (19 rows): wire numbers (`wireMode` ?[6]/`WIREMODE`, `wireFormat` ?[9]/`WIREFMT`, `wireStart`
?[7]/`WIRE_START`, `wireIncrement` ?[10]/`WIREINC`, `wireSuffixes` ?[8]/`WIRE_RSUF`, `referenceStyle` unmapped),
component tags (`tagMode` ?[2]/`TAGMODE`, `tagFormat` ?[5]/`TAGFMT`, `tagStart` ?[3]/`TAG_START`, `tagSuffixes`
?[4]/`TAG_RSUF`), `xrefFormat` ?[14]/`XREFFMT`, ladder (`rungOrientation` ?[0]/`RUNGHORV`, `rungIncrement`
?[1]/`RUNGINC`), and the six protected layer rows (?[26..29], ?[54], ?[56]). Every row but the layers is `inferred`
or `unknown`; see [OPERATIONS.md](OPERATIONS.md) section 3 for how to verify them.

A From/To settings block: `format` (`{FROM}`, `{TO}`, `{FROMPIN}`, `{TOPIN}`, `{SHEET}`, `{DRAWING}`),
`duplicateSuffix` (`{N}`), `includeTerminalPin`, `pinFormat` (`{TAG}`, `{PIN}`), `multiDevice` (`firstLast` | `all`),
`allSeparator`, `devicePriority` (regex list), `terminalsLast`, `uppercase`, `maxLength`, `fixed`, `singleDevice` and
`noDevice` (`keep` | `device` | `skip`; `device` only for `singleDevice`), `overwriteFixed`.

## 4. Profiles

One JSON file per profile, file name derived from the profile name with invalid characters replaced by `_`.

```json
{
  "name": "ACME", "customer": "ACME Corp", "description": "...", "scheme": "From/To device",
  "values": { "tagFormat": "%F%N" },
  "properties": { "7": "500" }, "options": {}, "drawing": { "WIREINC": "2" },
  "fromTo": { "format": "{FROM}-{TO}", "...": "..." },
  "wdtSource": "", "wdlSource": "", "notes": "", "capturedFrom": "", "captured": null
}
```

`values` go through the setting map and respect `protected`; `properties` / `options` / `drawing` are raw line or
attribute overrides that bypass map-level protection but not the rules-level `protectedOptions` /
`protectedProperties`. A raw override for the same line as a `values` entry wins. `fromTo` (written only when "Own
From/To settings" is ticked in the profile editor) replaces the scheme's block when the scheme is a From/To scheme, is
used on its own when the profile has no scheme, and is ignored with any other scheme.

## 5. The marker `<project>.lseenv.json`

```json
{ "profile": "ACME", "customer": "ACME Corp", "scheme": "From/To device", "applied": "2026-11-11T14:02:11", "by": "user",
  "fromTo": true, "properties": [6, 7], "options": [], "drawingAttributes": ["WIREMODE", "WIRE_START"], "values": { "wireMode": "S", "...": "..." } }
```

`properties`, `options` and `drawingAttributes` accumulate across runs (a later run merges the earlier lists), so the
list always names every line LSE_Env has ever changed in this project. `fromTo` is true when the last applied scheme
wrote From/To numbers; a later non-From/To switch plans the un-fix as recommended because of it.

## 6. Backups, reports and logs

Backups: one folder per run, every touched file copied with its original name (numbered on collision), attributes
normalised. Pruned to `BackupsToKeep` newest folders after a successful run. The report `LSE_EnvReport.txt` gets the
whole run log appended (start, project, profile, scheme, rules, every change, verification, status). A failed run
writes `Logs\EnvApply_<stamp>.txt` with the exception detail and shows the log in a dialog. Any other form or command error writes `Logs\Error_<stamp>.txt` (step, time, user,
machine, full exception) through `ErrorService.Log`; a failed write of that file is swallowed. Nothing prunes `Logs\`.

## 7. Snapshots

`Snapshot` holds every `*[n]`, `+[n]`, `?[n]` line of the `.wdp` and, per readable drawing (closed, or open in the editor) with a `WD_M`, every
attribute value. `last_A.json` / `last_B.json` survive closing the dialog so AutoCAD Electrical's dialogs can be used
between the two snapshots. The diff lists `wdp ?[n] (mapKey): "a" -> "b"` and `<dwg> WD_M TAG (mapKey): "a" -> "b"`.

## 8. The USERS1 system variable

As in LSE_Doctor: `ActiveProjectLisp` stores the active `.wdp` path in `USERS1`, the plugin reads and clears it.
