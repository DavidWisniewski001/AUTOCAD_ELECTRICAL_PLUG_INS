# Glossary: LSE_ToFrom

Maintained by DCW. Last reviewed 2026-11-13.

| Term | Meaning |
|---|---|
| ACADE | AutoCAD Electrical. |
| AEWIRE, AEWIRENO | The native commands that draw wires and insert wire numbers. LSE_ToFrom runs after both. |
| AEONLISPDEBUG | A native switch that traces the internal LISP calls a command makes; the way to find the database-refresh function. |
| ADODB | ActiveX Data Objects, the COM library used to read the scratch database without a NuGet package. |
| ACE provider | `Microsoft.ACE.OLEDB.12.0` / `16.0`, the 64-bit Access Database Engine OLE DB provider. |
| Acceptance tests | The ten cases in OPERATIONS section 4 that must pass on a copy of a real project before rollout. |
| Bundle | An Autodesk `ApplicationPlugins` folder with `PackageContents.xml` that loads the DLL on first command. |
| CMP1, CMP2 | The From and To component tags in a Wire From/To row. |
| CMPHDL1, CMPHDL2 | Entity handles of the component blocks at each end. |
| Discovery | Phase 1 of the work plan: confirm the scratch-database table, columns and LISP names on a real seat. `LSETOFROMDISCOVER` does the listing. |
| DWGIX1, DWGIX2 | Drawing indexes of each end in the project; resolved to file paths through the `.wdp` list or `drawingSql`. |
| .dwl, .dwl2 | AutoCAD lock files written next to an open drawing; their presence makes the plugin skip the drawing. |
| Far end | End 2 (the To end) of a wire; its printed label is reversed (`TB2:1/K1:13`) by default. |
| Handle | A hexadecimal entity identifier that is stable for the life of the entity in a drawing. |
| Host bridge | The two delegates (`ReadLabels`, `WriteLabels`) that separate the engine from the AutoCAD API. |
| Identifier | The to/from string, `FROMTAG:PIN/TOTAG:PIN`. |
| Key (TFKEY) | The hidden attribute `<wire handle>/<end>` that lets a re-run find a label. |
| Label block | `TOFROM_LBL_H` / `TOFROM_LBL_V`, the block carrying the identifier, the node number and the key. |
| Label layer | `LSE_TOFROM`; never a wire layer or a wire-number layer. |
| NETLOAD | The AutoCAD command that loads a .NET DLL for the session. |
| Network | A set of connected wire segments sharing one wire number; several physical wires when it has three or more connections. |
| Node number | The native wire number (`WIRENO`), kept unchanged. |
| Plan, Apply ticked | The two steps of `LSETOFROM`: Plan reads everything and lists the label changes without writing; Apply ticked writes the ticked changes and the files. The plan is a snapshot of the options when Plan ran. |
| Insert, Update, Keep, Delete, Skip | The planned action per label: new label; change the text of an existing one; already correct; remove (stale or duplicate); cannot be written (drawing unavailable, or a stale label kept). |
| Stale policy | The setting that decides what happens when drawings are newer than the scratch database: warn, stop or ignore. |
| .mdb | Microsoft Access database file; the scratch database's format. |
| Physical wire | One conductor between two connection points; one row of the Wire From/To report. |
| PIN1, PIN2 | Terminal (pin) numbers at each end; blank shows `?`. |
| Report file | A Wire From/To report saved to a text or CSV file; the alternative data source and the ground truth. |
| Scratch database | The project database (`<project>.mdb`) AutoCAD Electrical maintains for reports; read-only here. |
| Side database | A drawing opened with `ReadDwgFile` outside the editor, edited and saved in place. |
| Stale | A label whose wire is not in the data, or a database older than a drawing. |
| USERS1 | An AutoCAD system variable used to hand a LISP result back to .NET. |
| Validation report | `<project>_tofrom_validation.txt`: missing pins and tags, wires without labels, labels without wires, skipped drawings, stale database. |
| WD_M | The hidden block in every AutoCAD Electrical drawing that stores its settings; read for `WIRELAYS`, `WIRENO_LAY`, `SHEET`. |
| WIREHDL1, WIREHDL2 | Entity handles of the wire segment at each end. |
| WIRELAYS, WIRENO_LAY | `WD_M` attributes naming the wire layer patterns and the wire-number layer. |
| Wire sequence | The order AutoCAD Electrical assigns to connections in a network; it decides which end is From. |
| .wdp | The AutoCAD Electrical project file: settings lines and the ordered drawing list. |
| .set | A saved report format file (Report Generator). |
