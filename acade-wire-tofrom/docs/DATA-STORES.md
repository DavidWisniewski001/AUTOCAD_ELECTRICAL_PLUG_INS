# Data stores: LSE_ToFrom

Maintained by DCW. Last reviewed 2026-11-13.

Every file, database, block and generated artefact the plugin reads or writes.

## 1. Inventory

| Store | Location | Read / write | Owner |
|---|---|---|---|
| settings | `%APPDATA%\LSE\LSE_ToFrom\settings.json` | read and write | the engineer, through `LSETOFROMSETTINGS` |
| config | `tofrom.config.json` next to the DLL (or the path in settings) | read; written only by `LSETOFROMDISCOVER` on request | the department (share one copy) |
| project file | `<project>.wdp` | read only | AutoCAD Electrical |
| project scratch database | `<project folder>\<project>.mdb` by default, or the path AutoCAD Electrical reports | **read only**, SELECT only | AutoCAD Electrical |
| saved Wire From/To report | any `.csv`/`.txt` the engineer picks | read only | the engineer |
| drawings | every non-reference drawing of the project | read; write label blocks and the label layer only | the project |
| label block DWGs | `TOFROM_LBL_H.dwg`, `TOFROM_LBL_V.dwg` in `schematic.blockFolder` or next to the DLL | read when present; written by `LSETOFROMBLOCKS` | the department |
| outputs | `<project folder>\LSE_ToFrom\` | write | LSE_ToFrom |
| run log | `<project folder>\LSE_ToFromReport.txt` | append | LSE_ToFrom |
| backups | `<project folder>\_LSE_TOFROM_BACKUP\<yyyyMMdd_HHmmss>\` | write, prune | LSE_ToFrom |
| logs | `%APPDATA%\LSE\LSE_ToFrom\Logs\` (`Error_*.txt`, `ToFromRun_*.txt`, `Discovery_*.txt`) | write | LSE_ToFrom |
| USERS1 | AutoCAD system variable | set, read, cleared around each LISP bridge call | transient |

## 2. settings.json

| Key | Default | Meaning |
|---|---|---|
| `ConfigPath` | `<DLL folder>\tofrom.config.json` | the shared config |
| `BackupFolderName` | `_LSE_TOFROM_BACKUP` | inside the project folder |
| `BackupsToKeep` | 10 | per project; older stamps deleted after a successful run |
| `DataSource` | `scratch` | or `report` |
| `LastReportFile` | blank | the report file last picked |
| `StalePolicy` | `warn` | `warn`, `stop`, `ignore` |
| `ShowReportAfterRun` | true | |
| `CurrentDrawingOnly` | false | default for the tick box in `LSETOFROM`, and the scope of `LSETOFROMVALIDATE` |
| `ActiveProjectLisp` | `(setvar "USERS1" (cond ((and (boundp 'ace_getactiveproject) ...)) ... ((and (boundp 'GBL_wd_prj) (= (type GBL_wd_prj) 'STR)) GBL_wd_prj) (t "")))` | tries `ace_getactiveproject`, `c:ace_getactiveproject`, `wd_proj_wdp_fnam`, `c:wd_proj_wdp_fnam`, `GBL_wd_prj`; unverified. A file holding the first cut's single-function expression is upgraded on load |
| `ScratchDbLisp` | `(setvar "USERS1" (cond ((and (boundp 'c:wd_mdb_get_proj_scratch_dbnam) ...)) ((and (boundp 'wd_mdb_get_proj_scratch_dbnam) ...)) (t "")))` | unverified; result used only when the file exists; upgraded on load like the one above |
| `RefreshDbLisp` | blank | run before each read when set; unverified which function refreshes the database |
| `RecentProjects` | [] | last 25 `.wdp` paths, added after each successful run |

`DataSource` and `LastReportFile` are written by every Plan in `LSETOFROM`, not only by the settings screen. Load
normalises unknown values: any `DataSource` other than `report` becomes `scratch`, any `StalePolicy` other than `stop` or
`ignore` becomes `warn`, `BackupsToKeep` below 1 becomes 1. A settings file that cannot be parsed is replaced by defaults
silently (and overwritten on the next save).

## 3. tofrom.config.json

JSON with `//` comments. Every key optional; key names are case-insensitive. Validation rules are in
`ConfigService.Validate`; a config that fails validation stops every command with the reason. A missing file means
built-in defaults (the same values as the shipped file).

`LSETOFROMDISCOVER` is the only writer. It serialises the whole object back (temp file then move): comments are lost,
keys are written in PascalCase (`Format`, `Schematic`, ...) and two extra keys appear (`Source`, `Both`), all harmless to
the reader. The write fails with an error when the config sits in a folder the user cannot write to.

| Key | Default | Meaning |
|---|---|---|
| `format` | `{FROM}/{TO}` | the identifier; tokens `{FROM} {TO} {FROMTAG} {FROMPIN} {TOTAG} {TOPIN} {WIRENO} {FROMLOC} {TOLOC} {FROMINST} {TOINST}` |
| `farEndFormat` | `{TO}/{FROM}` | the label CSV's text for end 2 when `csv.reverseAtFarEnd` |
| `tagPinFormat` | `{TAG}:{PIN}` | how `{FROM}` and `{TO}` are built |
| `locationFormat` | `{LOC}+{TAG}` | applied to the tag when `includeLocation` and the row has a location |
| `missingPinText`, `missingTagText` | `?` | shown when blank |
| `includeLocation` | false | |
| `uppercase` | true | |
| `maxLength` | 0 | 0 = no limit |
| `swapEnds` | false | treat CMP2 as From |
| `schematic.labelAt` | `from` | `from`, `to`, `both` |
| `schematic.labelLayer` | `LSE_TOFROM` | created when missing; refused when it matches `WIRELAYS` or `WIRENO_LAY` |
| `schematic.blockH`, `blockV` | `TOFROM_LBL_H`, `TOFROM_LBL_V` | |
| `schematic.blockFolder` | blank | `;`-separated folders searched for `<block>.dwg`; blank = the DLL folder |
| `schematic.textAttribute`, `nodeAttribute`, `keyAttribute` | `TOFROM`, `WIRENO`, `TFKEY` | |
| `schematic.showNode` | false | node attribute visible in created definitions |
| `schematic.textHeight` | 0.08 | |
| `schematic.endOffset`, `sideOffset` | 0.5, 0.0625 | distance along the wire from its end, and to its left |
| `schematic.collisionDistance`, `collisionStep`, `collisionTries` | 0.3, 0.25, 4 | |
| `schematic.repositionOnRerun` | false | re-place existing labels on every run |
| `schematic.deleteStale` | true | |
| `schematic.wdmBlockName` | `WD_M` | |
| `csv.delimiter` | `,` | `tab` or `\t` for a tab |
| `csv.encoding` | `utf8bom` | `utf8bom`, `utf8`, `ansi` |
| `csv.reverseAtFarEnd` | true | |
| `csv.outputFolder` | `LSE_ToFrom` | relative to the project folder unless rooted |
| `csv.labelFile`, `reportFile`, `validationFile` | `{PROJECT}_tofrom_labels.csv`, `{PROJECT}_tofrom_report.csv`, `{PROJECT}_tofrom_validation.txt` | `{DATE}` also expands |
| `database.pathPattern` | `{FOLDER}\{PROJECT}.mdb` | used when the LISP bridge returns nothing usable |
| `database.searchFolders` | `{FOLDER}`, `{TEMP}`, `{LOCALAPPDATA}\Temp` | folders tried for `{PROJECT}.mdb` when the pattern path does not exist; newest file wins. Tokens `{FOLDER}`, `{PROJECT}`, `{TEMP}`, `{LOCALAPPDATA}`, `{APPDATA}` |
| `database.providers` | ACE 16, ACE 12, Jet 4 | tried in order; an empty list falls back to ACE 16 and ACE 12 only |
| `database.wireSql` | blank | the SELECT returning the from/to rows; set by discovery |
| `database.drawingSql` | blank | optional SELECT: index, file name |
| `database.drawingIndexBase` | 1 | the value of DWGIX for the first drawing in the `.wdp` |
| `database.confidence`, `note` | `unverified` | documentation only |
| `database.columns.*` | the native report names | role to column name; `cmp1`, `cmp2`, `wireHdl1` required |
| `database.candidateColumns` | the eleven report fields | discovery scoring |
| `reportFile.hasHeader`, `delimiter`, `columns` | true, blank (detect), the native names | a header row is always required (false only changes the error message). `delimiter` takes one character or `\t`; unlike `csv.delimiter` the word `tab` is not understood (it would split on the letter `t`) |

## 4. The label block

Two block definitions, horizontal and vertical, identical except that the vertical one's attributes are rotated 90
degrees. Created by `LabelWriter.CreateDefinition` when no `<name>.dwg` is found.

| Attribute | Visible | Content | Position |
|---|---|---|---|
| `TOFROM` | yes | the identifier | origin, middle-centre, height `textHeight` |
| `WIRENO` | no unless `showNode` | the node number from the data | 1.6 heights below (H) or beside (V) the origin, 0.8 height |
| `TFKEY` | no | `<wire handle>/<end>` (`#n` suffix for duplicate rows) | origin, half height |

Block references go on `schematic.labelLayer` with rotation 0 (the definition carries the orientation). A re-run finds
labels by `TFKEY` (or by block name when the key is blank, which then plans as stale). Changing the key format orphans
every placed label.

## 5. The scratch database

Opened with `Provider=<provider>;Data Source=<path>;Persist Security Info=False;` and `Mode=1` (read). Only SELECT
statements are executed. The table name is not hard-coded anywhere: `database.wireSql` is the single place. Discovery
(`LSETOFROMDISCOVER`) lists `OpenSchema(adSchemaColumns)` grouped by table, scores tables by how many of
`database.candidateColumns` they hold, and offers `SELECT * FROM [<best table>]`. The expected columns, per the Autodesk
Wire From/To report documentation, are `WIRENO, CMP1, PIN1, CMP2, PIN2, WIREHDL1, WIREHDL2, CMPHDL1, CMPHDL2, DWGIX1, DWGIX2`;
location and installation columns are optional and blank when absent.

## 6. Output files

### `<project>_tofrom_labels.csv` (one row per wire end)

| Column | Content |
|---|---|
| `Drawing` | file name of the drawing holding this end |
| `Sheet` | the `DWGIX` value from the data, or the drawing's position in the `.wdp` list when the row names the drawing instead. Not the `WD_M` SHEET value: the planner copies SHEET onto the schematic ends only, not the CSV ends (see DEVELOPER-GUIDE Known gaps) |
| `WireNo` | the node number |
| `End` | 1 (From) or 2 (To) |
| `Label` | the identifier; end 2 reversed when `csv.reverseAtFarEnd` |
| `Tag`, `Pin` | this end's component and pin |
| `OtherTag`, `OtherPin` | the far end |
| `WireHandle`, `ComponentHandle` | entity handles from the data |
| `Key` | the label key, without the `#n` suffix that duplicate schematic labels get; blank when the end has no wire handle |

Rows are ordered by drawing file name, data row, end. Every row of the data gives two CSV rows, including rows whose
drawing could not be resolved (blank `Drawing`). Fields are quoted only when they hold the delimiter, a quote, a line
break, or a leading or trailing space.

### `<project>_tofrom_report.csv` (one row per wire)

The eleven native columns in native order, values as read (before `swapEnds`), for a row-by-row comparison with the
native Wire From/To report.

### `<project>_tofrom_validation.txt`

Header (time, project, source, config), counts (rows, ends, labels wanted, drawings planned and skipped, label actions),
the run line when written after a run, then one section per issue kind with an explanation and one line per issue, and
the unverified changes after a run.

## 7. Backups, run log and logs

- Backups: only closed drawings that will change are copied, read-only attribute cleared on the copy. Pruned to
  `BackupsToKeep` after a successful run. Open drawings are not backed up (not saved by the plugin).
- `LSE_ToFromReport.txt`: the log of every successful or partial run (from `LSETOFROM` Apply and `LSETOFROMEXPORT`)
  appended, followed by a blank line. Failed runs, and their rollback lines, go only to `Logs\ToFromRun_<stamp>.txt`.
- Output files on failure: the CSVs and the validation report written by a failed run are deleted by the rollback. They
  overwrite the previous run's files of the same name first, so after a failure the previous outputs are gone too.
- `LSETOFROMVALIDATE` writes only `<project>_tofrom_validation.txt` (no backup, no run log).
- `Logs\ToFromRun_<stamp>.txt`: the log of a failed run with the exception detail. `Logs\Error_<stamp>.txt`: any other
  exception (step, time, user, machine, stack). `Logs\Discovery_<stamp>.txt`: the discovery findings note.
