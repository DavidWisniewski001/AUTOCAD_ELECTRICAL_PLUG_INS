# Glossary: LSE_Doctor

Maintained by DCW. Last reviewed 2026-11-10.

Terms used across the code and documentation, in alphabetical order. Shared AutoCAD Electrical terms are also defined in the glossary of LSE Project Tools (`../../acade-project-creator/docs/GLOSSARY.md`).

| Term | Meaning |
|---|---|
| `.aepx` | Project configuration file beside a `.wdp`. AutoCAD Electrical 2024 and later is said to write it itself. The doctor can create a minimal one; it is off by default. |
| `.bundle`, `PackageContents.xml` | AutoCAD's autoloader format: a folder named `<name>.bundle` with a manifest, placed in `ApplicationPlugins`. The doctor ships one in `bundle\`. |
| `.wdl`, `<project>_wdtitle.wdl` | Label file. `LINEn=label` lines give the Project Manager description dialog its prompts. |
| `.wdp` | AutoCAD Electrical project definition file: description lines, options, properties and the drawing list. Text. |
| `.wdt` | Title block mapping file read by Title Block Update: `BLOCK = names` then `ATTRIBUTE = CODE` lines. |
| `*[n]` | A project description line in the `.wdp`. The template has 8. |
| `+[n]` | A project option line in the `.wdp`: library path lists, icon menus, numbering and format options. The template has 38. |
| `?[n]` | A project property line in the `.wdp`. The template has 75 (indexes 0 to 76 without 13 and 46). |
| `=`, `==`, `===`, `====REF`, `=====SUB=` | `.wdp` markers before a drawing path: section, subsection, description, reference only, Project Manager folder. |
| `_V` | Vault version folder inside the template folder. The scan skips folders with this name. |
| `0`, `DEFPOINTS` | AutoCAD's built-in layers. Listed in `neverPurgeLayers`; `CLAYER` is moved to `0` before a layer is purged. |
| `ace_getactiveproject` | AutoLISP function the default active-project expression calls to get the active `.wdp` path. Unverified. |
| AcadE | AutoCAD Electrical. |
| `AcadDir` | MSBuild property naming the AutoCAD install folder used for references. |
| Active project | The project currently selected in the Project Manager. |
| `AutoCAD.NET` | NuGet reference package of AutoCAD's managed API. 25.1.0 corresponds to AutoCAD 2026 on .NET 8. Compile time only. |
| Backup folder | `_LSE_DOCTOR_BACKUP\<yyyyMMdd_HHmmss>` inside the project folder; holds the originals of files a repair changed. |
| Bundled standard | The copy of the template `.wdp`, `.wdt` and `_wdtitle.wdl` shipped in `standard\` next to the DLL; used when the Vault template is not reachable. |
| `c:wd_makeproj_current` | AutoLISP function used to reload a project. From a forum answer; unverified. |
| Category | Grouping of findings in the grid (for example "Title block mapping (.wdt)", "Wire layers", "Wire layer settings (WD_M)"). |
| `CLAYER` | The current layer of a drawing (`Database.Clayer`). A layer cannot be purged while current, so the doctor moves it to `0` first. |
| `CommandFlags.Modal` | AutoCAD command flag: the command runs in the document context and blocks until it returns. All doctor commands use it. |
| `DD1` | `.wdt` code for the first drawing description line. The template maps `DWG_TYPE` to it. |
| Description lines | The `*[n]` lines. |
| Diagnosis | The result of examining one project: findings, status and log. |
| `DRAWING-#.dwg` | The template drawing; see Template drawing. |
| Drawing action | A repair that rewrites a DWG: Insert WD_M, Set WD_M value, Create layer from template, Repair layer, Purge layer (`Finding.IsDrawingAction`). All drawing actions for one DWG are applied together and the file is saved once. |
| `DrawingRepairRequest` | The list of drawing actions for one DWG handed to `DrawingInspectionService.Repair`. |
| `DWG_NUMBER`, `DWG_TYPE` | Title block attributes in the template `.wdt`, filled from the file name and `DD1`. |
| `ECAD_LSE_CUSTOM_TB_2.0` | The LSE title block name on the template `.wdt` `BLOCK` line. |
| Enforce | A rules switch. On: a differing value is a recommended replacement. Off: listed but unticked. `enforceWireLayers` does the same for Set WD_M value and Repair layer. |
| Entity count | Number of entities on a layer across every block table record of a drawing. A non-standard wire layer with 0 is offered for purge; with more it is only reported. |
| `FAN_INOUT_LAYS` | `WD_M` attribute: comma-delimited list of layers used for fan-in/fan-out wire sources and destinations. Treated as a list attribute. |
| `FILENAME` | `.wdt` code for the drawing file name without path or extension. |
| Finding | One difference between project and standard, with severity, repair action and the data to apply it. |
| Healthy | A diagnosis with no Error and no Warning findings. Info findings do not count. |
| Info | Lowest severity: a note, an optional repair, or something not inspected. |
| Key | `Target|Category|Item` of a finding; used to match text-file findings before and after a repair. Drawing findings are verified by re-inspecting the drawing instead. |
| Layer table | The DWG's list of layers (`LayerTable`, one `LayerTableRecord` per layer: name, colour, linetype, lineweight, plot flag, on/off/frozen/locked, description, xdata). The doctor reads it into `LayerInfo` objects. |
| `LINEn` | `.wdt` and `.wdl` code and label for project description line n. |
| Linetype | The dash pattern of a layer (`Continuous`, `HIDDEN` ...), stored as a linetype table record. A repaired layer gets the template's linetype; the record is cloned from the template if the drawing lacks it. |
| Lineweight | Line thickness of a layer in hundredths of a millimetre; -3 default, -2 ByBlock, -1 ByLayer. Shown in the grid as `0.35 mm`, `default`, `ByBlock` or `ByLayer`. |
| List attribute | A `WD_M` attribute whose value is a comma-delimited list of layer names or wildcard patterns (`wireLayerListAttributes`: `WIRELAYS`, `FAN_INOUT_LAYS`). Compared as an unordered, case-insensitive set. |
| LSE | Lake Shore Electric. |
| Merge mode | Rules `pathListMode: merge`: template paths first, then the project's extra paths. |
| `NETLOAD` | AutoCAD command that loads a managed DLL for the session. |
| Path list | A `;`-separated list of folders, as in `+[1]` and `+[3]`. |
| Placeholder | The template's description line text (`PROJECT NAME`, `#####`, `CUST 1` ...). |
| Project Manager | AutoCAD Electrical's project palette. |
| Protected | An index listed in `protectedOptions` or `protectedProperties`; shown as Info and not recommended. |
| Purge | Removing an unreferenced layer table record from a drawing. The doctor asks `Database.Purge` whether the layer is unreferenced, then erases the record. A purged layer comes back only from the backup DWG. |
| Recommended | A repair ticked by default. |
| Regapp, registered application | A name in the DWG's `RegAppTable` under which xdata is stored. Before copying layer xdata the doctor registers every application name it names. The wire-type application name is assumed to match `VIA_WD*`. |
| Reload | Making AutoCAD Electrical reread a project, done by sending `ActivateProjectLisp` after the dialog closes. |
| Replace mode | Rules `pathListMode: replace`: the project's list becomes exactly the template's. |
| Rollback | Restoring backed-up files and deleting files the run created after a failed repair. |
| Scan | `LSEDOCTORSCAN`: examine every `.wdp` under a folder. |
| Severity | Error, Warning or Info. |
| Side database | An AutoCAD `Database` object loaded from a DWG file without opening it as a document. The doctor reads every drawing, and the template drawing, this way. |
| Single-name attribute | A `WD_M` wire-layer attribute that holds exactly one layer name (`WIRENO_LAY`, `WIRECOPY_LAY`, `WIREFIXED_LAY`, `WIREREF_LAY`). Compared trimmed, ignoring case. |
| Smoke test | The console checks (90 on 2026-11-10) run against the built DLL for the logic that does not need AutoCAD, including the wire-layer logic. Not shipped. |
| Standard | The LSE custom project template (`.wdp` + `.wdt` + `_wdtitle.wdl`) that projects are compared with. For wire layers, the template drawing. |
| Standard wire layer | A layer name that is a wire layer of the template drawing, or matches the template's `WIRELAYS` patterns, or is one of the template's single-name attribute values (`WireLayerService.IsStandard`). Any other wire layer in a project drawing is non-standard. |
| Template drawing | The first existing drawing in the template `.wdp` whose file name starts with `DRAWING` (`DRAWING-#.dwg`); the source of the `WD_M` block for insertion and the wire-layer standard. Only available when the Vault template folder is reachable. |
| Title Block Update | AutoCAD Electrical command that fills title block attributes project-wide from the `.wdp` and the `.wdt`. |
| Update to New WD_M Block | AutoCAD Electrical tool (Project > Other Tools) that replaces an old `WD_M` with the current definition, keeping values. The doctor asks for it when a drawing's `WD_M` lacks a wire-layer attribute the template's has. |
| `USERS1` | AutoCAD user system variable (string). Used to carry the active project path back from LISP. |
| Vault | The LSE file vault; its root is `C:\_Lake_Shore_E_Vault` in the configured paths. The doctor only hints at it: a read-only file is assumed to be not checked out. Which product manages it is not stated in the tree. |
| `VIA_WD*` | The default pattern for the registered-application name of wire-type xdata on a layer (`wireLayerXdataApps`). A guess; Autodesk does not document the name. |
| `WD_M` | AutoCAD Electrical's drawing settings block. Its attributes hold sheet number, IEC codes, numbering defaults and the wire-layer settings (`WIRELAYS` and the other layer-naming tags). Every drawing in a project should carry one. |
| `WblockCloneObjects` | AutoCAD .NET call that copies objects between databases; used to copy the `WD_M` definition, a layer table record and a linetype table record from the template drawing into a project drawing. |
| Wildcard | `*` (any text) and `?` (one character) in title block name patterns, `WIRELAYS` entries and xdata application name patterns, case-insensitive. |
| Wire layer | A layer AutoCAD Electrical treats as carrying wires. To the doctor, a layer of a drawing is a wire layer when it matches an entry of a list attribute, is named by a single-name attribute, or carries wire-type xdata. |
| Wire layer settings (WD_M) | Finding category for the `WD_M` attributes that name wire layers. |
| Wire type | AutoCAD Electrical's classification of a layer as a wire layer, which it stores as xdata on the layer table record. The doctor detects it by the registered-application name. |
| `WIRELAYS` | `WD_M` attribute: "valid wire layer names where "" = all valid (comma-delimited)" (Autodesk help, "About the WD_M block"). Wildcards allowed. Blank means every layer is a wire layer. |
| `WIRENO_LAY`, `WIRECOPY_LAY`, `WIREFIXED_LAY`, `WIREREF_LAY` | `WD_M` single-name attributes: the layers for wire number text, wire number copies, fixed wire numbers and wire reference text. |
| Working database | `HostApplicationServices.WorkingDatabase`; swapped to the side database during a drawing repair and restored afterwards. |
| Xdata, extended entity data | Application-specific data attached to a DWG object under a registered application name. AutoCAD Electrical marks wire layers with it. The doctor reads the application names on each layer and copies the template layer's xdata when repairing or creating a layer. |
