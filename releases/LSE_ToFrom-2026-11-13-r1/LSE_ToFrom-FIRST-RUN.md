# LSE_ToFrom 2026-11-13-r1: first run on an AutoCAD Electrical 2026 seat

Build `2026-11-13-r1` of **LSE_ToFrom** (true To/From wire identifiers `FROMTAG:PIN/TOTAG:PIN` as a label layer, plus
the label-printer CSV). Compiled Release, x64, .NET 8, 0 warnings, against Autodesk's `AutoCAD.NET` 25.1.0 reference
package; 109-check smoke test passed on the build machine (no AutoCAD there). **Not yet loaded in AutoCAD Electrical.**
This cut exists to make the first run on a real seat tell you exactly what works and what does not.

This section is written to be pasted into the next `LSE-Plugins-BUILD-AND-INSTALL-GUIDE.md`; until then it is the
guide for this plugin alone. It is not in Releases/Current on Google Drive yet (the user decides when to upload).

## What is in the package

| File | Holds |
|---|---|
| `LSE_ToFrom-NETLOAD-READY-2026-11-13-r1.zip` | compiled binaries: `LSE_ToFrom-NETLOAD\LSE_ToFrom\` (DLL + `tofrom.config.json`), `ApplicationPlugins\LSE_ToFrom.bundle\` (manifest + `Contents\`), `LSE_ToFrom-NETLOAD.lsp` loader, this file as `NETLOAD-README.md` |
| `LSE_ToFrom-2026-11-13-r1.zip` | source: `acade-wire-tofrom\` (project, `src\`, `config\`, `bundle\`, `docs\`, `tests\`), the guardian agent brief, this file |

Commands: `LSETOFROM`, `LSETOFROMEXPORT`, `LSETOFROMVALIDATE`, `LSETOFROMDISCOVER`, `LSETOFROMBLOCKS`,
`LSETOFROMSETTINGS`, `LSETOFROMDIAG`. Every window title ends with `[2026-11-13-r1]`.

## Install (5 minutes)

1. Unblock the zip before extracting, then extract to `C:\` so the folder is `C:\LSE_ToFrom-NETLOAD`:

   ```powershell
   Unblock-File "$env:USERPROFILE\Downloads\LSE_ToFrom-NETLOAD-READY-2026-11-13-r1.zip"
   Expand-Archive "$env:USERPROFILE\Downloads\LSE_ToFrom-NETLOAD-READY-2026-11-13-r1.zip" -DestinationPath "C:\"
   Get-ChildItem "C:\LSE_ToFrom-NETLOAD" -Recurse | Unblock-File
   ```

2. Start AutoCAD Electrical 2026 with any drawing open. Drag `C:\LSE_ToFrom-NETLOAD\LSE_ToFrom-NETLOAD.lsp` into the
   drawing area (or `APPLOAD` it; or `NETLOAD` and browse to `LSE_ToFrom\LSE_ToFrom.dll`). Choose **Always Load** if the
   security dialog asks. `LSETOFROMLOAD` repeats the load in a later session.
3. Permanent alternative: copy `ApplicationPlugins\LSE_ToFrom.bundle` to `%APPDATA%\Autodesk\ApplicationPlugins\` and
   restart; the commands then load on first use. To replace the DLL later, close AutoCAD Electrical completely first.

## First run, in this order

1. **`LSETOFROMDIAG`.** The second line must read `Build: 2026-11-13-r1`. Then read the block **LISP bridge (live check
   from this command)**:
   - `Editor.Command path: works` or `acedEvaluateLisp path: works`: at least one way of running LISP from the plugin
     works. Both "no result": the plugin cannot ask AutoCAD Electrical anything; you browse to the `.wdp` in `LSETOFROM`
     and the database is found by path. Everything else still works. Paste the block into PLUG-IN_TROUBLESHOOTING.
   - `Active project LISP returned:` the `.wdp` path when a project is active, otherwise blank.
   - `Scratch database LISP returned:` the `.mdb` path of the project database, or blank. Blank is expected on the first
     try: the function name is unverified. Note it either way.
   - `ADODB: available` and at least one provider `registered`. If none is, install the 64-bit Microsoft Access Database
     Engine (the same one AutoCAD Electrical's catalog needs).
2. **Open a COPY of a small project** in the Project Manager, run AEWIRENO if the wires have no numbers, then
   **Reports > Schematic > Wire From/To** once. Save that report to a CSV (Save to File) with the fields WIRENO, CMP1,
   PIN1, CMP2, PIN2, WIREHDL1, WIREHDL2, CMPHDL1, CMPHDL2, DWGIX1, DWGIX2 if the dialog offers them. That report is the
   ground truth and the fallback data source.
3. **`LSETOFROMDISCOVER`.** It prints the bridge check, `Database path used`, `Paths looked at` (the pattern path and
   `<project>.mdb` in each `database.searchFolders` entry: project folder, `%TEMP%`, `%LOCALAPPDATA%\Temp`), then every
   table and column of the database with the table that holds the from/to columns marked CANDIDATE.
   - Database MISSING: find where AutoCAD Electrical put `<project>.mdb` (search the drive for it), add that folder to
     `database.searchFolders` in `tofrom.config.json` (or set `database.pathPattern`), run discovery again.
   - A CANDIDATE with 3 or more of the 11 columns: check its first rows look like the report, answer **Yes** to write the
     SELECT into the config. The write keeps the file's comments. A write failure (write-protected folder) is reported in
     the note with what to do.
   - No candidate: compare the column names printed with `database.columns` in the config, edit them, run again.
   - The findings note is saved under `%APPDATA%\LSE\LSE_ToFrom\Logs\Discovery_<stamp>.txt`; keep it.
4. **`LSETOFROMBLOCKS`**: write `TOFROM_LBL_H.dwg` / `TOFROM_LBL_V.dwg` next to the DLL (answer the folder dialog with
   `C:\LSE_ToFrom-NETLOAD\LSE_ToFrom`), and answer Yes to add the layer and blocks to the current drawing if it is the
   department template.
5. **`LSETOFROMVALIDATE`** on the copy: read the validation report (missing pins, missing tags, wires without a handle,
   unresolved drawings, stale database). Nothing is written to drawings.
6. **`LSETOFROM`** on the copy: Project database (or Report file with the CSV from step 2 if discovery is not done),
   **Plan**, read the grid. Click a box (or press the space bar on a row) to tick it; rows that say `note` cannot be
   written and a click explains why on the status line. **Apply ticked**. Closed drawings are backed up to
   `<project>\_LSE_TOFROM_BACKUP\<stamp>\` and saved; open drawings are changed in the session and you save them.
   Open a sheet: labels on layer `LSE_TOFROM` beside the From end of each wire.
7. Compare `<project>\LSE_ToFrom\<project>_tofrom_report.csv` with the saved native report row for row, then work
   through the ten acceptance tests in `acade-wire-tofrom\docs\OPERATIONS.md` section 4.

## What to report back

Paste into PLUG-IN_TROUBLESHOOTING: the whole `LSETOFROMDIAG` text, the `Discovery_<stamp>.txt` note, and for any
error the three-part dialog text plus `%APPDATA%\LSE\LSE_ToFrom\Logs\Error_<stamp>.txt`. Every one of them names the
build.

## Not verified on a workstation (what this run will settle)

- Which LISP evaluation path works and which function names exist (`ace_getactiveproject`, `wd_proj_wdp_fnam`,
  `GBL_wd_prj`, `wd_mdb_get_proj_scratch_dbnam`).
- Where the scratch database lives and which table and columns hold the from/to rows; that `WIREHDL1/2` are the handles of
  the wire segments touching each component and `CMPHDL1/2` the component blocks; that `DWGIX` counts from 1 in `.wdp` order.
- Every AutoCAD write: side databases saved in place, `LockDocument` on open drawings, block definition creation, the
  DWG export of the label blocks, attribute alignment; `.dwl` files as the lock signal.
- The default label offsets and text height (inch-based drawings assumed); placement at the wire end nearest the
  component insertion point.
- That the plan grid's boxes respond to the first click inside AutoCAD's modal dialog host (handled by the form, as in
  LSE_Doctor r7; proven on a real grid outside AutoCAD).
- The saved native report's file layout that the report-file source expects (header row with the field names).
