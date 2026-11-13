# Glossary: LSE Catalog Tools

Maintained by DCW. Last reviewed 2026-11-13.

Terms, acronyms, products and file types a newcomer to Catalog Tools may meet, one or two sentences each.

| Term | Meaning |
|---|---|
| ACE OLEDB | Microsoft Access Database Engine provider (`Microsoft.ACE.OLEDB.16.0` or `12.0`) used to open `.mdb` files. Must be 64-bit to match AutoCAD |
| AcquireFiles | Vault Framework call that checks out and/or downloads files in one request |
| ADODB | The classic COM data-access library (`ADODB.Connection`). The plugin uses it late-bound through `dynamic` |
| Assign | Writing a catalog row's values into the attributes of a component block in the drawing |
| Attribute | A named text value (tag) carried by a block reference, such as `TAG1`, `MFG`, `CAT` |
| AutoCAD Electrical (AcadE) | AutoCAD toolset for electrical controls design; version 2026 is release R25.1 |
| Backup folder | `LSE_Catalog_Backups\<yyyyMMdd_HHmmss>\` beside the catalog, holding a full copy of the MDB and `TransactionReport.txt` |
| Block reference | An inserted instance of a block definition in a drawing; AcadE components are block references with attributes |
| Bundle | A `<name>.bundle` folder with `PackageContents.xml` under `ApplicationPlugins`, which AutoCAD loads automatically |
| Check-in / check-out | Vault operations that lock a file to one user for editing and then publish a new version |
| Company Standards | The LSE-approved subset of the catalog, listed by record key in `CompanyStandards.json` |
| `default_cat.mdb` | AutoCAD Electrical's parts catalog database, one table per family |
| Family | A component category; in the catalog, one table (`PB`, `CR`, `TRMS` ...). The block's `FAMILY` attribute names it |
| `FAMILY` | Block attribute holding the family code; used to pre-select the family and for the compatibility warning |
| FilePathAbsolute / FolderPathAbsolute | Vault Framework path types passed to check-in and download calls |
| Jet OLEDB 4.0 | Old 32-bit Access provider; the last fallback, normally unavailable in 64-bit AutoCAD |
| Legacy | A `CompanyStandards.json` status that hides a record from the Company Standards view |
| LSE | Lake Shore Electric, the company that owns these plugins |
| LSE part number | LSE's internal part number, kept in the catalog column `USER1`; format `CCMMMTTNNNNNNN` (issued by the Librarian or the Inventor add-in, not here) |
| LSE part attribute | The block attribute that receives `USER1` on assign; setting `LsePartAttribute`, default `USER1` |
| `.mdb` | Microsoft Access database file |
| NETLOAD | AutoCAD command that loads a .NET DLL for the current session |
| `PackageContents.xml` | Bundle manifest naming the DLL, the AutoCAD versions and the commands |
| Preserve | Check box in the assign preview; a ticked attribute is never written |
| R25.1 | AutoCAD's internal series number for the 2026 release |
| Read-back | Re-querying the catalog after the insert to prove the row exists |
| Record key | `TABLE\|MANUFACTURER\|CATALOG` upper-case; identifies a catalog row for favorites, recents and standards |
| `TEXTVALUE` | Catalog column; Catalog Tools reads it as `KEY=VALUE;KEY=VALUE` attribute overrides |
| `TransactionReport.txt` | The line-per-step log of one catalog add, in its backup folder |
| `USER1`, `USER2`, `USER3` | Free user columns in the catalog; LSE uses `USER1` for the LSE part number |
| Vault | Autodesk Vault Professional, LSE's document management server (`lse-cep-sql`, vault `Lake_Shore_E_Vault`) |
| Vault add-in | The Autodesk Vault add-in inside AutoCAD; when logged in, Catalog Tools uses its session |
| Vault Explorer folder | `C:\Program Files\Autodesk\Vault Client 2026\Explorer`, holding the Vault SDK DLLs |
| Vault path | A file's location inside Vault, starting `$/` |
| Vault working folder | The local mirror of Vault files, `C:\_Lake_Shore_E_Vault\` at LSE; files not checked out are read-only |
| `WD_M` | AutoCAD Electrical's hidden drawing-settings block (not used by Catalog Tools; mentioned by sibling plugins) |
| `WDBLKNAM` | Catalog column naming the block(s) a row applies to; used for the compatibility warning |
| Windows authentication | Vault login with the current Windows account, the third connection route |
