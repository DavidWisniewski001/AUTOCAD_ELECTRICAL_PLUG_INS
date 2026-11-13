# Glossary: LSE Footprint Tools

Maintained by DCW. Last reviewed 2026-11-08.

Terms used across the code and documentation, in alphabetical order.

| Term | Meaning |
|---|---|
| ACE | Microsoft Access Database Engine, the OLEDB provider (`Microsoft.ACE.OLEDB.16.0` or `12.0`) that opens `.mdb` files. Must be the 64-bit edition to run inside Inventor. |
| AcadE | AutoCAD Electrical. |
| ADODB | ActiveX Data Objects, the COM database library the add-in drives through `dynamic` to avoid a compile-time dependency. |
| `.addin` manifest | XML file Inventor reads from its Addins folder to find and load the DLL. Contains the add-in GUID, display name and assembly path. |
| ATTDEF | DXF attribute definition entity. When a drawing with ATTDEFs is inserted as a block, each becomes an attribute that AutoCAD Electrical can fill. |
| `BLKNAM` | Column in `footprint_lookup.mdb` holding the footprint drawing's path or block name. The add-in stores the full DWG path. |
| Catalog, `default_cat.mdb` | AutoCAD Electrical's component database, one table per family. |
| Catalog number, CATALOG, MPN | The manufacturer's part number for the component. Limit 60 characters. |
| Category code | First two digits of an LSE part number. Taken from the workbook tab. |
| Create, transaction | The single guarded operation (`FootprintTransactionService.Execute`) that issues the number, writes the drawings and inserts the database rows. |
| Description pattern | The LSE standard format for a description on a given workbook tab, shown as placeholder text in the wizard. |
| DXF, R12, `AC1009` | Drawing Exchange Format; the add-in writes the AutoCAD Release 12 flavour because it is the simplest widely readable version. |
| DWG | AutoCAD's native drawing format. Produced by AutoCAD from the DXF; AutoCAD Electrical inserts footprints from DWG. |
| Family, family table | A component category in the catalog (`PB` push buttons, `FU` fuses, `TRMS` terminals, ...) and the table of the same name. Friendly names come from `_FAMILY_DESCRIPTION`. |
| Family hint | `familyHints` in the naming JSON: the family tables most often used for a category code in the live catalog. |
| Footprint | The 2D panel-layout symbol for a component, drawn to scale, inserted on AutoCAD Electrical panel drawings. |
| Footprint library | The folder holding `<PN>.dxf` and `<PN>.dwg`. Setting `FootprintLibraryFolder`. |
| `footprint_lookup.mdb` | AutoCAD Electrical database mapping manufacturer and catalog number to a footprint drawing. One table per manufacturer. |
| Footprint units | `in` or `mm`; the units of the DXF geometry and of sizes shown in the wizard. Inventor's API works in centimetres internally. |
| Interop assembly | `Autodesk.Inventor.Interop.dll`, the .NET wrapper over Inventor's COM API. The only compile-time Autodesk reference. |
| Issuance lock | `LSE_PartNumber.lock`, an exclusive delete-on-close file beside the catalog that stops two workstations issuing numbers at once. |
| LSE | Lake Shore Electric, the owner of the part-number scheme and the catalog. |
| LSE part attribute | The invisible footprint attribute carrying the LSE part number. Setting `LsePartAttribute`, default `USER1`. |
| LSE part number, PN | `[Category 2][Manufacturer 3][Type 2][Sequence 7]`, 14 characters, stored in catalog column `USER1`. Example `45BUSFH0000001`. |
| Manufacturer code | Three letters inside the part number identifying the vendor, for example `BUS` for Bussmann. Distinct from the catalog `MANUFACTURER` text. |
| `MANUFACTURER` | Catalog column (limit 24) holding the vendor name as AutoCAD Electrical knows it. Also the name of the vendor's table in `footprint_lookup.mdb`. |
| Naming standard, naming JSON | `LseNamingStandard.json`, the codes extracted from the LSE Electrical Parts Catalog Manager workbook. |
| Outline rectangle only | Projection option that replaces all curves with the view's bounding rectangle. Default for STL meshes. |
| `P_TAG1` | The visible footprint attribute showing the panel component tag. |
| Prefix | Category + manufacturer code + type code, the first seven characters of a part number. Sequences are counted per prefix. |
| Preview next part number | Wizard action that runs the same scan as the create and shows the next number without reserving it. |
| Rollback | On any failure inside the transaction: restore both `.mdb` files from the backup, delete the drawings, undo the Vault checkout, write the report, rethrow. |
| Scale correction, unit correction | Factor 1, 25.4 or 1/25.4 applied to an imported model whose STL units were misread. |
| Sequence | Last seven digits of a part number; highest existing + 1 for the prefix; never reused. |
| STEP, STL | 3D model exchange formats. STEP (`.stp`, `.step`) is solid geometry; STL is a facet mesh with no units. |
| Tab (LSE catalog tab) | A worksheet in the LSE workbook, for example `LSE FUSE HOLDERS`, defining the codes for a family of parts. |
| `TransactionReport.txt` | The step-by-step log of a create, saved in its backup folder. |
| Type code | Two letters inside the part number describing the kind of part within a tab, for example `FH` fuse holder. |
| `USER1` | Catalog column AutoCAD Electrical leaves free for the user; LSE stores the part number here. |
| Vault | Autodesk Vault, the document management server. Optional; when the catalog is Vault-controlled the add-in checks it out and in around the insert. |
| VDF | Vault Development Framework, the client SDK loaded by reflection from the Vault Explorer folder. |
| Wizard | The `FootprintWizardForm` window opened by the **LSE Footprint** ribbon button. |
| `WEBLINK` | Catalog column for a datasheet URL, written when the family table has it. |
