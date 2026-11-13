# Glossary: LSE Project Tools

Maintained by DCW. Last reviewed 2026-11-10.

Terms used across the code and documentation, in alphabetical order.

| Term | Meaning |
|---|---|
| `.aepx` | Project configuration XML AutoCAD Electrical keeps beside a `.wdp` (`ProjectPath`, `LocalGUID`, one `<Drawing>` per file). The plugin writes `<job>.aepx` in the shape of the template's; AutoCAD Electrical rewrites it when the project is opened. Acceptance of a pre-written one is unverified. |
| `.bundle`, `PackageContents.xml` | AutoCAD's autoloader format: a folder named `<name>.bundle` with a manifest, placed in `ApplicationPlugins`. The plugin ships one in `bundle\`. |
| `.dwt` | AutoCAD drawing template. Accepted as a drawing template, but the LSE standard uses `.dwg` template drawings (`DRAWING-#.dwg`, `STOCKCODE-E001.dwg`). |
| `.wdl`, `<job>_wdtitle.wdl` | AutoCAD Electrical label file. `LINEn=label` lines (no spaces) give the Project Manager's Descriptions dialog its prompts. The plugin copies the template's (renamed) or generates one per project, LINE1 to LINE14. |
| `.wdp` | AutoCAD Electrical project definition file: description lines, options, properties and the drawing list. Text; the LSE template is UTF-8 with BOM, CRLF, groups `*[n]`, `+[n]`, `?[n]` as written by AutoCAD Electrical 2026. |
| `.wdt` | AutoCAD Electrical title block mapping file read by Title Block Update: `BLOCK = names` then `ATTRIBUTE = CODE` lines. The plugin copies the template's as `<job>.wdt` or generates one. |
| `*[n]` | A project description line in the `.wdp`. The plugin writes at least 8 (the LSE template's lines), more when the JSON or the template has a higher index. |
| `?[n]` | A project property line in the `.wdp` (numbering and format settings). Copied from the template project (75 lines, `?[0]` to `?[76]`); otherwise omitted. |
| `+[n]` | A project option line in the `.wdp` (symbol library paths, icon menus, footprint libraries, numbering options). Copied from the template project (38 lines); otherwise omitted. `+[2]` is the schematic icon menu diagnostics reports. |
| `=`, `==`, `===`, `====REF`, `=====SUB=` | `.wdp` markers placed before a drawing path: section, subsection, description line (up to three), reference-only flag, Project Manager tree folder. The LSE template uses only `===`. |
| `%P`, `%I`, `%L`, `%S`, `%D` | AutoCAD Electrical replaceable parameters in tag and wire number formats: project, installation, location, sheet, drawing number. They read WD_M's `IEC_PROJ`, `IEC_INST`, `IEC_LOC`, `SHEET`, `SHEETDWGNAME`. |
| AcadE | AutoCAD Electrical. |
| `AcadDir` | MSBuild property naming the AutoCAD install folder used for references. |
| Activation | Making a project the active one in the Project Manager. Done by sending a LISP expression to the command line after the wizard closes. |
| `AutoCAD.NET` | NuGet reference package holding AutoCAD's managed API assemblies for compiling without an install. 25.1.0 corresponds to AutoCAD 2026 on .NET 8. |
| `AeData` | AutoCAD Electrical's per-user data root, `Documents\Acade 2026\AeData`, holding `Proj`, `en-US\Libs`, `en-US\Catalogs` and more. Only a fallback now; the LSE defaults come from the vault. |
| Base project | V1 name for the template project: the `.wdp` whose `+[n]` and `?[n]` lines are copied into new projects. Setting `BaseProjectPath`. |
| `boundp` | AutoLISP predicate: true when a symbol has a value. Used so the activation expression does nothing harmful when the function does not exist. |
| `c:wd_makeproj_current` | AutoLISP function, quoted from an Autodesk community answer, that makes a `.wdp` the current project. Unverified. |
| `CommandFlags.Session` | AutoCAD command attribute flag: the command runs in application context, so it can open documents and send input after a modal form closes. |
| Cover sheet, `STOCKCODE-E001.dwg` | The LSE cover drawing in the template folder; the first row (`E001`) of every shipped drawing set is created from it. |
| Custom Sales Order | The vault folder `<vault>\Engineering\Custom Sales Order` under which every custom job lives, sorted by product line, manufacturer, breaker type and amperage. The default jobs root. |
| Customer | Description line 3 (`CUSTOMER 1`, role `customer`), required; feeds `{CUSTOMER}` and so the job folder name `<job>_<customer>`. |
| DD1, DD2, DD3 | `.wdt` codes for the three drawing description lines. The LSE block maps `DWG_TYPE = DD1`. |
| Description lines | The `*[n]` lines; in the plugin they come from `descriptionLines` in the JSON and the wizard's step 1. At LSE: PROJECT NAME, QUOTE#, CUSTOMER 1, CUSTOMER 2, CUSTOMER 3, UNIT DESIGNATION, UNIT SPECS, DWG SCALE. |
| Drawing number | AutoCAD Electrical's optional per-drawing number (`%D`), held in WD_M `SHEETDWGNAME`. Blank by default in the shipped JSON. Not to be confused with `DWG_NUMBER`. |
| Drawing set | A named list of sheets in the JSON (`projectTypes`): UL 891 Switchboard, UL 1008 Transfer Switch, UL 508A Control Panel, Template only. The wizard's "Drawing set" combo; the code still calls it a project type. |
| Drawing type | Drawing description 1: the `===` line in the `.wdp` and the `DWG_TYPE` attribute on the LSE title block (`COVER SHEET`, `ONE-LINE DIAGRAM`). |
| `DRAWING-#.dwg` | The blank LSE sheet in the template folder; the default drawing template every non-cover sheet is created from. |
| `DWG_NUMBER`, `DWG_TYPE` | Attributes of `ECAD_LSE_CUSTOM_TB_2.0`: the drawing file name without extension (`001096-1-E001`; `.wdt` code `FILENAME`) and the drawing type (`.wdt` code `DD1`). |
| DWGSEC, DWGSUB | `.wdt` codes for section and subsection. Not used by the LSE block. |
| `ECAD_LSE_CUSTOM_TB_2.0` | The LSE title block name (from the template `.wdt`). Its attributes: `DWG_NUMBER`, `DWG_TYPE`, `PROJECT_NAME`, `QUOTE_NUMBER`, `CUSTOMER`, `CUSTOMER_2`, `CUSTOMER_3`, `CUSTOMER_DESIGNATION`, `SWBD_AMP_VOLT_KAIC_NEMA`, `SCALE`. No sheet or sheet-max attribute is mapped. |
| E-number | The three-digit sheet number in an LSE drawing file name, `<job>-E<nnn>.dwg` (`001096-1-E001.dwg`). The Sheet column of the wizard. The series used by the shipped drawing sets (001 cover, 1xx one-line, 2xx elevations, 3xx schematics, 4xx metering, 5xx schedules) is inferred and unconfirmed. |
| FILENAME | `.wdt` code for the drawing file name without path or extension. Mapped to `DWG_NUMBER`. |
| Fixed values | `titleBlock.fixed` in the JSON: constant or token text written to named title block attributes. Empty in the shipped JSON. |
| Folder (Project Manager) | A virtual folder in the project tree (`=====SUB=`), not a disk folder. The grid column "PM folder". |
| IEC project, installation, location | The three codes AutoCAD Electrical can prefix to component tags; set as drawing defaults in WD_M. |
| Job folder | `<jobs root>\<product line folder>\<level choices>\<job>_<customer>\`, holding the `.wdp`, its sidecars, the sheets and the report. |
| Job number | The LSE sales order number, `NNNNNN-R` (six digits, dash, release: `001096-1`), rule `job.numberPattern`. A field of its own in the wizard (not a description line); default project name, file name prefix and first part of the job folder name. |
| Jobs root | The folder under which the plugin creates the product line folders and job folders. Setting `ProjectsRootFolder`; default `Custom Sales Order`. |
| LINEn | `.wdt` and `.wdl` code for project description line n. |
| LSE | Lake Shore Electric. |
| `NETLOAD` | AutoCAD command that loads a managed DLL for the current session. |
| Product line | A top-level entry of `productLines` in the JSON: the vault folder under `Custom Sales Order` (UL 891 Switchboards, UL1008, UL 508A Control Panels), its folder levels, the unit type and the drawing set it pre-selects. |
| Project Manager | AutoCAD Electrical's project palette: opens projects, lists drawings, edits descriptions and properties. |
| Project type | Code name (`ProjectTypeDef`, `projectTypes`, `DefaultProjectType`) for a drawing set. |
| Rollback | Deleting every file a failed run created (copied sidecars included) and the job folder if the run created it. |
| Section, subsection | AutoCAD Electrical drawing grouping codes (`=` and `==` in the `.wdp`), usable in reports and cross-references. Unused by the LSE template. |
| Sheet, SHEET, SHEETMAX | The drawing's sheet number (`%S`, WD_M `SHEET`) and the total number of drawings. In the wizard, Sheet is the E-number text (`001`), written as is to WD_M `SHEET`; the LSE title block maps neither `SHEET` nor `SHEETMAX`. |
| Sidecar files | The files beside a `.wdp`: `.wdt`, `_wdtitle.wdl` and `.aepx`. The template's `.wdt` and `.wdl` are copied, renamed, into every new job; the `.aepx` is written new. |
| Side database | An AutoCAD `Database` object loaded from a file without opening it as a document. Used for every drawing the plugin creates. |
| Smoke test | The console checks run during authoring against the built DLL for the non-AutoCAD logic (55 checks on 2026-11-10, with the reference template as fixture). Not shipped. |
| Summary info | DWG file properties (title, subject, author, comments). The plugin fills them per drawing. |
| Template (per drawing) | The optional `Template` column or `drawings[].template` field: a file name resolved inside the template folder (`STOCKCODE-E001.dwg`) or a full path, used instead of the drawing template for one row. |
| Template folder | The folder of the template project `.wdp` (or of the drawing template when none is set), `...\CUSTOM TEMPLATES\CUSTOM PROJECT TEMPLATE 2.0\`, in which bare template file names are resolved. |
| Template project | `E-CAD CUSTOM PROJECT TEMPLATE 2.wdp`, the LSE project written by AutoCAD Electrical 2026 on the vault. Its `+[n]`/`?[n]` lines and encoding are copied into every new project and its `.wdt`/`.wdl` copied renamed. Setting `BaseProjectPath`; JSON `vault.templateProject`. |
| Title block | The border block whose attributes carry project and sheet data. Recognised by name wildcards from the JSON; at LSE `ECAD_LSE_CUSTOM_TB_2.0`. |
| Title Block Update | AutoCAD Electrical command that fills title block attributes project-wide from the `.wdp` and the `.wdt` or `WD_TB` mapping. |
| Tokens | `{JOB}`, `{CUSTOMER}`, `{QUOTE}`, `{PRODUCTLINE}`, `{PROJECT}`, `{USER}`, `{DATE}`, `{YEAR}`, `{SHEET}`, `{SHEET:n}` in patterns, defaults and fixed values. |
| Unit specs | Description line 7 (`UNIT SPECS`, attribute `SWBD_AMP_VOLT_KAIC_NEMA`, role `specs`): `SWBD, 2500A, 277/480VAC, 65KA@480V, N1`, composed by the wizard's builder from `unitSpecs` choices. |
| Vault working folder | The local check-out of the LSE vault, `C:\_Lake_Shore_E_Vault` (or `_Lake_Shore_E_Vault` at the root of another fixed or network drive), holding `CAD Standards\AutoCAD\ECAD Standards` and `Engineering\Custom Sales Order`. Found by `SettingsService.DiscoverVaultRoot`; used as plain file system, no Vault API. |
| `wd.env` | AutoCAD Electrical environment file with default paths and settings; applies where a `.wdp` has no `+[n]` entry. |
| WD_M | AutoCAD Electrical's invisible drawing settings block at 0,0; its attributes hold sheet number, IEC codes, ladder and wire numbering defaults. |
| `WD_TB` | An attribute some title blocks carry with embedded mapping; when present, Title Block Update ignores the `.wdt`. |
| `wd_m.dwg` | The library drawing defining the WD_M block, looked for in the vault NFPA symbol library and under `AeData\en-US\Libs\<library>`. Inserted when a template lacks WD_M. |
| Wildcard | `*` (any text) and `?` (one character) in `blockNames`, case-insensitive. |
| Working database | `HostApplicationServices.WorkingDatabase`; swapped to the side database during drawing creation and restored afterwards. |
