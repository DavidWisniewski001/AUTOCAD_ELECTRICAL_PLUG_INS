# Operations guide: LSE Footprint Tools

Maintained by DCW. Last reviewed 2026-11-13.

For the engineer who installs the add-in, configures a workstation, creates parts with it, and sorts out problems. Build instructions are in [DEVELOPER-GUIDE.md](DEVELOPER-GUIDE.md). Terms are in [GLOSSARY.md](GLOSSARY.md).

**The add-in has not yet been compiled or run on any machine.** The first installation is also the first test. Follow the "Verify on first build" list in the [README](../README.md) before letting anyone issue a real part number.

## 1. Workstation requirements

| Requirement | Why | Check |
|---|---|---|
| Autodesk Inventor 2026 or later | Host application. The manifest (`SupportedSoftwareVersionGreaterThan` 29) excludes Inventor 2025 and earlier. | Inventor starts; the add-in is listed in the Add-In Manager. |
| 64-bit Access Database Engine, ACE 16 or 12 | Reads and writes the two `.mdb` files | Settings > Test shows `Catalog read: PASS`. |
| AutoCAD Electrical (any release registering `AutoCAD.Application`) | Converts DXF to DWG; needed only when that setting is on | Settings > Test shows `AutoCAD COM registered on this PC: yes`. |
| Vault client 2026.4 | Only when `Catalog Vault path` is set | Installed at the same generation as Inventor (both .NET 10); the Vault add-in logged in is the easiest route, otherwise a login dialog appears during Create. Settings > Test shows any mismatch. |
| Read and write access to the catalog folder on the share | Backups, lock file, footprint library default location | Create a file in that folder by hand. |

## 2. Installation

1. Obtain the Release build output: `LSE.Inventor.FootprintTools.dll`, `LseNamingStandard.json` and `LSE.Inventor.FootprintTools.addin` (plus any `.deps.json` / `.runtimeconfig.json` the build produces).
2. Copy the output folder to a share or to each workstation.
3. Copy `LSE.Inventor.FootprintTools.addin` into one of Inventor's add-in folders:
   - per user: `%APPDATA%\Autodesk\Inventor 2026\Addins\`
   - all users: `C:\ProgramData\Autodesk\Inventor Addins\`
4. If the DLL is not next to the `.addin`, edit the manifest's `<Assembly>` element to hold the full path of the DLL.
5. Start Inventor. On the **Tools** tab an **LSE** panel appears with **LSE Footprint** and **LSE Settings**. It is added to the zero-document, part, assembly and drawing ribbons.

If the panel does not appear, open Inventor's **Add-In Manager** (Tools tab) and look for *LSE Footprint Tools*. A load error there usually means the DLL path in the manifest is wrong or the DLL was blocked after download (right-click, Properties, Unblock).

## 3. First-time configuration (per user)

Open **LSE Settings** and check every row. Defaults are listed in [DATA-STORES.md](DATA-STORES.md) section 1.

1. **Catalog MDB**: the live `default_cat.mdb`. Auto-discovered under `Documents\Acade 2026\AeData\en-US\Catalogs\` or `Documents\AutoCAD_Electrical_2026\Catalogs\`. On a multi-user site point this at the shared catalog that AutoCAD Electrical uses.
2. **Footprint lookup MDB**: `footprint_lookup.mdb`, normally in the same folder.
3. **Footprint library folder**: where the DWGs go. AutoCAD Electrical must be able to open the path that ends up in `BLKNAM`, so use a shared path everyone can see, not a local one.
4. **Naming standard JSON**: point everyone at one shared copy so all workstations use the same codes.
5. **Footprint units**: `in` or `mm` to match the site's panel drawings.
6. **Convert DXF to DWG with AutoCAD**: leave on where AutoCAD Electrical is installed; turn off elsewhere and convert by hand.
7. **Vault** rows: *Catalog Vault path* (`$/folder/.../default_cat.mdb`; blank = no Vault) and *Vault Explorer folder* (auto-discovered when blank). Leave the path blank unless the catalog is Vault-controlled. **Test** now also connects to Vault (it may show the Vault login dialog) and prints the host .NET, the Vault client and whether they match.
8. Press **Test**. All of these should pass: catalog read with a family-table count, footprint lookup read, naming standard with tab and code counts. The AutoCAD line should say `yes` if conversion is on.
9. Press **Save**.

Settings are stored per Windows user in `%APPDATA%\LSE\InventorFootprintTools\settings.json`. The path is shown at the bottom of the form. The wizard reads settings when it opens, so close and reopen **LSE Footprint** after saving.

### Settings field by field

| Row on the form | Setting | What it does | Checked on Save |
|---|---|---|---|
| Catalog MDB (default_cat.mdb) | `CatalogPath` | Catalog read for family tables and part numbers; one row inserted per create. Lock and backups go in its folder. | no |
| Footprint lookup MDB | `FootprintLookupPath` | Footprint registration target. | no |
| Footprint library folder | `FootprintLibraryFolder` | Where `<PN>.dxf` and `<PN>.dwg` are written; created if missing. | no |
| Naming standard JSON | `NamingStandardPath` | Codes for step 3. | no |
| Inventor drawing template (blank = default) | `DrawingTemplatePath` | Template for the hidden projection drawing. A path that does not exist silently falls back to the default. | no |
| Footprint units (in or mm) | `FootprintUnits` | Units of the DXF and of every size in the wizard. | must be `in` or `mm` |
| Geometry layer | `GeometryLayer` | DXF layer of the curves (blank = `0`). | no |
| Attribute layer | `AttributeLayer` | DXF layer of the attributes (blank = `0`). | no |
| Tag text height (in) | `TagTextHeight` | Attribute text height, always typed in inches. | positive number |
| Curve tolerance (footprint units) | `CurveTolerance` | Only affects ellipses and splines: smaller means more polyline points (8 to 256 per curve). | positive number |
| Max curve segments per view | `MaxCurves` | Projection fails above this count. | positive whole number |
| LSE part attribute on footprint | `LsePartAttribute` | Name of the hidden footprint attribute that receives the part number. Stored upper-case. Do not use `MFG` or `CAT` (the create then fails with a duplicate-key error). | not blank |
| Convert DXF to DWG with AutoCAD (check box) | `ConvertToDwgWithAutoCad` | Ticked: AutoCAD saves the DXF as `<PN>.dwg` during the create. Unticked: only the DXF is written, the log says SKIPPED, and you must save the DWG by hand at the path already registered in `footprint_lookup.mdb`. Also controls the "AutoCAD is not registered" warning when the wizard opens. | no |
| Append ", MPN <catalog>" to DESCRIPTION (check box) | `AppendMpnToDescription` | Ticked: the catalog `DESCRIPTION` becomes `<description>, MPN <catalog>`. Unticked: the description as typed. The footprint lookup description never gets the MPN. The wizard refuses a description containing the MPN either way. | no |
| Vault Explorer folder (auto-discovered when blank) | `VaultExplorerPath` | Folder holding the Vault SDK DLLs. If it does not exist the newest `Program Files\Autodesk\Vault*\Explorer` is used. | no |
| Vault server | `VaultServer` | Login dialog pre-fill and Windows authentication. | no |
| Vault name | `VaultName` | Same. | no |
| Catalog Vault path (...; blank = no Vault) | `CatalogVaultPath` | Blank: Vault is off and the catalog is written in place (it must then be writable). Filled: checkout before, check-in after the insert. | no |

Buttons: **Browse** on path rows (file or folder picker; files need not exist yet), **Test** (runs the checks below using what is typed, without saving), **Save** (validates, writes, closes). Closing the window without Save discards changes.

Test prints, in one message box: `Catalog read: PASS (<n> family tables)` or `FAIL`, `Footprint lookup read: PASS` or `FAIL`, `Naming standard: PASS (<tabs> tabs, <codes> manufacturer codes; <source>)` or `FAIL`, `AutoCAD COM registered on this PC: yes|no`, then the Vault block: host .NET, Vault client folder and whether it can load, Vault add-in session, server and vault, and either "Vault is OFF" or a live connection result ending `Vault test: PASS` or `Vault test: FAIL - <reason>`.

## 4. Creating a part

Open **LSE Footprint**. The window is modeless; it stays open beside Inventor. Every action writes to the log pane at the bottom with a timestamp.

### Step 1: Model

1. **Browse...** to the vendor's `.stl`, `.stp`, `.step`, or an existing `.ipt` / `.iam`.
2. Set **Unit correction** if needed. STL files carry no units; if Inventor imported millimetres as inches the extents will be 25.4 times too small. Choose the matching correction.
3. **Import into Inventor**. The model opens in Inventor and the wizard shows its X x Y x Z extents in footprint units. Check them against the datasheet before continuing.

### Step 2: View to footprint

1. Choose the face that mounts to the panel: Top, Bottom, Front, Back, Left, Right, or Current Inventor view. **Preview view in Inventor** rotates Inventor's camera so you can confirm.
2. Options (three check boxes, all read only when you press **Project view to footprint**):
   - **Include hidden lines**: the projection view uses Inventor's hidden-line style, so edges behind the visible face are also written. Unticked: hidden-line-removed style.
   - **Include tangent edges**: sets the view's tangent-edge display, adding the lines where smooth faces meet.
   - **Outline rectangle only (recommended for STL meshes)**: writes only four lines, the bounding rectangle of the view. While it is ticked the other two boxes are greyed out and have no effect. It is ticked automatically when you browse to an `.stl` file because mesh facet edges are not useful footprint lines; you can untick it.
   Changing a box after projecting has no effect until you project again.
3. **Project view to footprint**. The wizard reports the curve count and the width x height. If it reports more than the maximum curve count (default 4000), tick Outline rectangle only or raise the limit in Settings.

### Step 3: Part number and catalog record

1. **LSE catalog tab**: the workbook tab the part belongs to. This sets the category, type and manufacturer code lists.
2. **Category code** (2 digits), **Type code** (2 letters), **Manufacturer code** (3 letters). The manufacturer list shows the tab's codes first, then every other known code. Codes can be typed as `XYZ` or `XYZ - Name` if they are not listed.
3. **MANUFACTURER** is pre-filled with the value the catalog already uses for that code. Keep it identical to existing rows for the same vendor; AutoCAD Electrical matches footprints by this exact text.
4. **Catalog number**: the manufacturer part number (MPN), up to 60 characters.
5. **Description**: the LSE standard description, upper-case ASCII, up to 100 characters, **without** the MPN. The MPN is appended automatically as `, MPN <catalog>` when that setting is on. The box rejects a description that already contains the catalog number.
6. **Datasheet / web link**: optional, stored in `WEBLINK` when the family table has that column.
7. **AutoCAD Electrical family table**: pre-selected from the category code when a hint exists; otherwise pick the family (`PB`, `FU`, `TRMS`, ...). The table must have a `USER1` column.
8. **Preview next part number** shows the number that would be issued. It is not reserved; another workstation may take it first. The real number is assigned inside the create.

### Step 4: Create

Press **Generate footprint and add to catalog**. It refuses until a model is imported, a view is projected and a family table is chosen. A summary asks for confirmation, then the transaction runs. On failure the error box shows the whole transaction log, including the rollback lines. On success a message shows the part number and the footprint path, and the full report appears in the log. The projected geometry is cleared so a second part needs a fresh projection.

Expect a few seconds on a LAN share: the part-number scan reads `USER1` from every family table, and AutoCAD may need to start.

### What a successful report looks like

```
Started: 2026-11-08T14:02:11 by jdoe@ENG-07
Catalog: \\server\acade\Catalogs\default_cat.mdb
Footprint lookup: \\server\acade\Catalogs\footprint_lookup.mdb
Backup folder: \\server\acade\Catalogs\LSE_Catalog_Backups\20261108_140211
Record: FU | BUSSMANN | CHCC1 | prefix 45BUSFH
Issuance lock: PASS
Backups: PASS
Part number issued: 45BUSFH0000012 (11 existing 45BUSFH value(s) scanned across all family tables)
Footprint DXF written: \\server\acade\LSE_Footprints\45BUSFH0000012.dxf (14 curve(s), 1.25 x 2.5 in)
AutoCAD DXF to DWG: PASS \\server\acade\LSE_Footprints\45BUSFH0000012.dwg
Catalog insert into FU (USER1=45BUSFH0000012): PASS
Footprint lookup [BUSSMANN] CHCC1 -> \\server\acade\LSE_Footprints\45BUSFH0000012.dwg: PASS
Status: SUCCESS
```

The same text is saved as `TransactionReport.txt` in the backup folder.

With a Catalog Vault path set, extra lines appear between the DWG line and the insert: the connection route (`Vault connection: using the Vault add-in's session (...)`, `... logged in through the Vault login dialog ...` or `... Windows authentication ...`), `Vault file: $/... version N (...)`, a check-out line, and `Vault checkout: PASS`; after the insert, `Vault check-in: PASS (now version N)` and `Vault checkin: PASS`. When any touched file is in the Vault working folder the report ends with `Vault: these N item(s) are in the Vault working folder and are not checked in yet...` and one line per file.

## 5. After a create

- In AutoCAD Electrical, insert the component from the catalog and confirm the panel footprint resolves to the new DWG and that `USER1` carries the part number.
- Close the imported model in Inventor; the add-in leaves it open.
- If the DXF to DWG step was skipped, open the DXF in AutoCAD and save it as `<PN>.dwg` in the same folder before anyone inserts the footprint.

## 6. Troubleshooting

| Symptom | Cause | Action |
|---|---|---|
| `Catalog read: FAIL` listing three providers | No 64-bit ACE provider, or the path is wrong | Install the 64-bit Access Database Engine 2016 redistributable; check the path. |
| `The LSE naming standard JSON was not found` | `NamingStandardPath` wrong or the file was not deployed | Point Settings at a shared copy, or regenerate it (see [DEVELOPER-GUIDE.md](DEVELOPER-GUIDE.md) section 5). |
| `WARNING: AutoCAD is not registered on this PC` in the log | Conversion is on but AutoCAD is absent | Turn the setting off or run on a workstation with AutoCAD Electrical. |
| `Only STL, STEP (.stp/.step), .ipt and .iam files can be used.` | Unsupported extension | Convert the model in Inventor first. |
| `The view has more than 4000 curve segments...` | Mesh model with many facets | Tick Outline rectangle only or raise Max curve segments. |
| `The drawing view produced no curves.` | Wrong orientation or empty model | Preview the view; check the model has bodies. |
| Extents are 25.4 times off | STL unit mismatch | Change Unit correction, then press Project (or Import) again; the extents line refreshes on either. |
| `DESCRIPTION is limited to 150 characters.` | Description (up to 100) plus `, MPN <catalog>` is over 150 | Shorten the description. |
| `Description may only contain plain ASCII characters` / `Do not put the MPN in the description` | Wizard input rules | Remove special symbols; remove the catalog number from the description. |
| `Another workstation is issuing a part number right now` | Lock held by another create, or a stale lock | Wait and retry. If no create is running anywhere, delete `LSE_PartNumber.lock` beside the catalog. |
| `That manufacturer/catalog number already exists in the selected family.` | Duplicate MPN for that manufacturer | Find the existing row in AutoCAD Electrical's catalog browser; it already has a part number. |
| `Table X has no USER1 column` | Family table cannot hold a part number | Choose another family or add the column in AutoCAD Electrical. |
| `A footprint drawing already exists for <PN>` | Leftover files from an earlier failed run whose rollback could not delete them | Inspect and remove the files, then retry. |
| `The catalog is checked out in Vault by <user> (machine <m>)...` | Someone else holds the Vault check-out | Have them check in, or undo in Vault Explorer. Nothing was changed in Vault. |
| `The catalog was not found in Vault at <path>...` | Catalog Vault path is not the file's path inside Vault | Copy the path from the file's Properties in Vault Explorer. |
| `Vault SDK assembly ... was not found` | No Vault Client on this PC and no Vault add-in session | Install Vault Client 2026, fix the Vault Explorer folder, or blank the Catalog Vault path. |
| `...working copy is older than the Vault version and the download failed...` or `...do Get Latest Version...` | Vault could not refresh a stale working copy | In Vault Explorer do Get Latest Version (or Check Out) on the catalog, then run again. |
| `Vault check-in failed: ...` | Check-in rejected | The run rolls back (catalog restored, checkout undone), despite the message saying the working copy holds the record. Read the rollback lines; run again. |
| Report shows `Vault checkin: PASS` and then an error at the footprint lookup step | The row is already in Vault; the local rollback cannot remove it | Get Latest on the catalog, then register the footprint by hand or ask an administrator; do not re-run with the same MPN (the duplicate check will refuse). |
| `This file is read-only: ...` | Footprint lookup (or the catalog with Vault off) is in the Vault working folder and not checked out | Check it out in Vault, run again. Nothing was changed. |
| Vault login dialog cancelled | User cancelled | The run aborts and rolls back; run again. |
| Test shows a .NET mismatch | Inventor and Vault client on different .NET generations | Update Inventor 2026.4 and Vault 2026.4 together. |
| Report ends with a Vault check-in reminder | DXF/DWG, lookup MDB (and catalog, Vault off) are in the Vault working folder | Add/check them in from Vault so other PCs see them. |
| `AutoCAD did not become ready within 60 seconds.` | AutoCAD start-up blocked by a dialog or licence prompt | Start AutoCAD by hand, clear any dialogs, leave it running, retry. |
| Report ends with `Catalog rollback: FAILED` | The backup could not be copied back (file locked by AutoCAD Electrical or another user) | Stop all catalog users and copy `LSE_Catalog_Backups\<stamp>\default_cat.mdb` over the live catalog by hand. Compare first. |
| Footprint does not resolve in AutoCAD Electrical | `MANUFACTURER` text differs between catalog and lookup table, or `BLKNAM` path is not reachable from that PC | Compare the two values exactly; use a shared library folder. |

## 7. Recovering from a bad create

Every attempt leaves `LSE_Catalog_Backups\<stamp>\` with both databases as they were before the write and a report. To undo a completed create by hand: stop all users of the catalog, copy both `.mdb` files from the stamp folder over the live ones, delete `<PN>.dxf` and `<PN>.dwg` from the library folder, and if Vault is in use check in the restored catalog. Remember that any row anyone else added after that stamp is lost by a whole-file restore.

The next number is always the highest `USER1` in the catalog plus one. A rolled-back or manually undone create removes the row, so its number is issued again by the next create (unless a higher number exists). Only a number that stayed in the catalog and was later removed by other means leaves a gap. If a rolled-back number was already written on a drawing or a label, treat it as unissued.

## 8. Housekeeping

- `LSE_Catalog_Backups` grows by two full database copies per attempt and is never pruned. Archive old stamps periodically.
- Regenerate `LseNamingStandard.json` whenever the LSE Electrical Parts Catalog Manager workbook gains codes or manufacturers, and replace the shared copy.
- Before go-live reconcile the workbook's *LSE Recommended Part Number* columns against `USER1` in the catalog: the add-in only knows numbers that are in the catalog, so a recommended-but-unimported number can collide.
