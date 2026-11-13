# Operations guide: LSE Footprint Tools

Maintained by DCW. Last reviewed 2026-11-08.

For the engineer who installs the add-in, configures a workstation, creates parts with it, and sorts out problems. Build instructions are in [DEVELOPER-GUIDE.md](DEVELOPER-GUIDE.md). Terms are in [GLOSSARY.md](GLOSSARY.md).

**The add-in has not yet been compiled or run on any machine.** The first installation is also the first test. Follow the "Verify on first build" list in the [README](../README.md) before letting anyone issue a real part number.

## 1. Workstation requirements

| Requirement | Why | Check |
|---|---|---|
| Autodesk Inventor 2026 (or later 2025+ release running .NET 8 add-ins) | Host application | Inventor starts. |
| 64-bit Access Database Engine, ACE 16 or 12 | Reads and writes the two `.mdb` files | Settings > Test shows `Catalog read: PASS`. |
| AutoCAD Electrical (any release registering `AutoCAD.Application`) | Converts DXF to DWG; needed only when that setting is on | Settings > Test shows `AutoCAD COM registered on this PC: yes`. |
| Vault Explorer client | Only when `Catalog Vault path` is set | The three SDK DLLs exist in the Vault Explorer folder. |
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
7. **Vault** rows: leave *Catalog Vault path* blank unless the catalog is Vault-controlled.
8. Press **Test**. All of these should pass: catalog read with a family-table count, footprint lookup read, naming standard with tab and code counts. The AutoCAD line should say `yes` if conversion is on.
9. Press **Save**.

Settings are stored per Windows user in `%APPDATA%\LSE\InventorFootprintTools\settings.json`.

## 4. Creating a part

Open **LSE Footprint**. The window is modeless; it stays open beside Inventor. Every action writes to the log pane at the bottom with a timestamp.

### Step 1: Model

1. **Browse...** to the vendor's `.stl`, `.stp`, `.step`, or an existing `.ipt` / `.iam`.
2. Set **Unit correction** if needed. STL files carry no units; if Inventor imported millimetres as inches the extents will be 25.4 times too small. Choose the matching correction.
3. **Import into Inventor**. The model opens in Inventor and the wizard shows its X x Y x Z extents in footprint units. Check them against the datasheet before continuing.

### Step 2: View to footprint

1. Choose the face that mounts to the panel: Top, Bottom, Front, Back, Left, Right, or Current Inventor view. **Preview view in Inventor** rotates Inventor's camera so you can confirm.
2. Options: **Include hidden lines** and **Include tangent edges** add detail; **Outline rectangle only** replaces all geometry with the bounding rectangle. The outline option is ticked automatically for STL files because mesh facet edges are not useful footprint lines.
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

Press **Generate footprint and add to catalog**. A summary asks for confirmation, then the transaction runs. On success a message shows the part number and the footprint path, and the full report appears in the log. The projected geometry is cleared so a second part needs a fresh projection.

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
| Extents are 25.4 times off | STL unit mismatch | Change Unit correction and re-import. |
| `Another workstation is issuing a part number right now` | Lock held by another create, or a stale lock | Wait and retry. If no create is running anywhere, delete `LSE_PartNumber.lock` beside the catalog. |
| `That manufacturer/catalog number already exists in the selected family.` | Duplicate MPN for that manufacturer | Find the existing row in AutoCAD Electrical's catalog browser; it already has a part number. |
| `Table X has no USER1 column` | Family table cannot hold a part number | Choose another family or add the column in AutoCAD Electrical. |
| `A footprint drawing already exists for <PN>` | Leftover files from an earlier failed run whose rollback could not delete them | Inspect and remove the files, then retry. |
| `Catalog is already checked out.` | Someone holds the Vault checkout | Have them check in, or undo in Vault Explorer. |
| `AutoCAD did not become ready within 60 seconds.` | AutoCAD start-up blocked by a dialog or licence prompt | Start AutoCAD by hand, clear any dialogs, leave it running, retry. |
| Report ends with `Catalog rollback: FAILED` | The backup could not be copied back (file locked by AutoCAD Electrical or another user) | Stop all catalog users and copy `LSE_Catalog_Backups\<stamp>\default_cat.mdb` over the live catalog by hand. Compare first. |
| Footprint does not resolve in AutoCAD Electrical | `MANUFACTURER` text differs between catalog and lookup table, or `BLKNAM` path is not reachable from that PC | Compare the two values exactly; use a shared library folder. |

## 7. Recovering from a bad create

Every attempt leaves `LSE_Catalog_Backups\<stamp>\` with both databases as they were before the write and a report. To undo a completed create by hand: stop all users of the catalog, copy both `.mdb` files from the stamp folder over the live ones, delete `<PN>.dxf` and `<PN>.dwg` from the library folder, and if Vault is in use check in the restored catalog. Remember that any row anyone else added after that stamp is lost by a whole-file restore.

Part numbers are never reused, so a number consumed by a rolled-back or manually undone create simply leaves a gap.

## 8. Housekeeping

- `LSE_Catalog_Backups` grows by two full database copies per attempt and is never pruned. Archive old stamps periodically.
- Regenerate `LseNamingStandard.json` whenever the LSE Electrical Parts Catalog Manager workbook gains codes or manufacturers, and replace the shared copy.
- Before go-live reconcile the workbook's *LSE Recommended Part Number* columns against `USER1` in the catalog: the add-in only knows numbers that are in the catalog, so a recommended-but-unimported number can collide.
