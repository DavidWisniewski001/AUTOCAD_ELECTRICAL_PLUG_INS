using LSE.AutoCADElectrical.Librarian;
int pass = 0, fail = 0;
void Check(string name, bool ok, string detail = "") { if (ok) pass++; else fail++; Console.WriteLine((ok ? "PASS " : "FAIL ") + name + (detail.Length > 0 ? "  [" + detail + "]" : "")); }
if (args.Length < 2) { Console.WriteLine("usage: <workbook.xlsx> <default_cat.mdb> [footprint_lookup.mdb] [noaudit]"); return 2; }
string dir = Path.Combine(Path.GetTempPath(), "LSE_CatalogOnly_" + Guid.NewGuid().ToString("N")[..8]);
Directory.CreateDirectory(dir);
string wb = Path.Combine(dir, "wb.xlsx"), mdb = Path.Combine(dir, "default_cat.mdb"), backups = Path.Combine(dir, "bk");
File.Copy(args[0], wb, true); File.Copy(args[1], mdb, true);
string untouched = Path.Combine(dir, "untouched"); Directory.CreateDirectory(untouched);
var s = new AppSettings { CatalogPath = mdb, NamingWorkbookPath = wb, CatalogBackupFolder = backups, LsePartColumn = "USER1", SymbolLibraryFolder = untouched, FootprintLibraryFolder = untouched, FootprintLookupPath = Path.Combine(untouched, "footprint_lookup.mdb") };
var n = NamingWorkbookService.Load(s);
Check("workbook loaded", n.FromWorkbook && n.Tabs.Count > 20, n.Tabs.Count + " tabs");
var tab = n.Tab("LSE FUSE HOLDERS")!;
string type = tab.Types.Keys.First();
var pn = new PartNumberRequest { Tab = tab.Name, Category = "45", ManufacturerCode = "BUS", Type = type };
var fam = CatalogTransactionService.FamilyUsage(s, n, "45");
string table = fam.Count > 0 ? fam[0].Table : new CatalogService(mdb).GetTables()[0];
Console.WriteLine($"tab={tab.Name} prefix={pn.Prefix} family={table}");
var q = new CatalogAddRequest { TableName = table, Manufacturer = "ZZ TEST MFR", Catalog = "ZZ-CATONLY-001", Description = "FUSE HOLDER, TEST ROW" };
string? err = null;
try { CatalogTransactionService.Validate(s, n, pn, q); } catch (Exception ex) { err = ex.Message; }
Check("Validate accepts a record with no WDBLKNAM, symbol or footprint", err is null, err ?? "");
foreach (var (label, mutate) in new (string, Action<CatalogAddRequest>)[] { ("no manufacturer", r => r.Manufacturer = ""), ("no catalog number", r => r.Catalog = ""), ("no family table", r => r.TableName = ""), ("bad weblink", r => r.WebLink = "ftp://x") })
{
    var bad = new CatalogAddRequest { TableName = table, Manufacturer = "ZZ TEST MFR", Catalog = "ZZ-CATONLY-001", Description = q.Description };
    mutate(bad); err = null;
    try { CatalogTransactionService.Validate(s, n, pn, bad); } catch (Exception ex) { err = ex.Message; }
    Check("Validate refuses " + label, err is not null);
}
byte[] before = File.ReadAllBytes(mdb);
var preview = CatalogTransactionService.Preview(s, n, pn);
CatalogAddResult r1 = CatalogTransactionService.Execute(s, n, pn, q);
Check("Execute returns the previewed number", r1.PartNumber == preview.PartNumber, r1.PartNumber + " vs " + preview.PartNumber);
Check("number is in standard form with the prefix", r1.PartNumber.StartsWith(pn.Prefix) && PartNumberService.StandardForm(n.Format).IsMatch(r1.PartNumber));
Check("sequence is below the legacy floor", PartNumberService.Split(n.Format, r1.PartNumber)!.Value.Sequence < PartNumberService.LegacyFloor(n.Format));
var svc = new CatalogService(mdb);
Check("row read back by MANUFACTURER+CATALOG", svc.Exists(table, q.Manufacturer, q.Catalog));
var found = svc.FindByColumn("USER1", r1.PartNumber);
Check("row read back by part number in the right table", found is not null && found.Value.Table.Equals(table, StringComparison.OrdinalIgnoreCase));
Check("backup holds the pre-change MDB byte for byte", File.ReadAllBytes(Path.Combine(r1.BackupFolder, "default_cat.mdb")).SequenceEqual(before));
string rep = Path.Combine(r1.BackupFolder, "CatalogReport.txt");
Check("CatalogReport.txt written with SUCCESS", File.Exists(rep) && File.ReadAllText(rep).Contains("Status: SUCCESS"));
Check("lock file released", !File.Exists(Path.Combine(dir, "LSE_PartNumber.lock")));
Check("no library, icon or lookup file was written", Directory.GetFileSystemEntries(untouched).Length == 0);
var q2 = new CatalogAddRequest { TableName = table, Manufacturer = "ZZ TEST MFR", Catalog = "ZZ-CATONLY-002", Description = q.Description };
var r2 = CatalogTransactionService.Execute(s, n, pn, q2);
Check("second record gets the next number", PartNumberService.Split(n.Format, r2.PartNumber)!.Value.Sequence == PartNumberService.Split(n.Format, r1.PartNumber)!.Value.Sequence + 1, r2.PartNumber);
byte[] afterTwo = File.ReadAllBytes(mdb); err = null;
try { CatalogTransactionService.Execute(s, n, pn, q); } catch (Exception ex) { err = ex.Message; }
Check("duplicate MANUFACTURER+CATALOG refused", err is not null && err.Contains("already exists"), err ?? "");
Check("catalog byte-identical after the refused duplicate", File.ReadAllBytes(mdb).SequenceEqual(afterTwo));
// Failure after the backup and the lock: a part-number column the catalog does not have. The run must restore the catalog and report ERROR.
var bs = new AppSettings { CatalogPath = mdb, NamingWorkbookPath = wb, CatalogBackupFolder = backups, LsePartColumn = "NOSUCHCOL" };
var q3 = new CatalogAddRequest { TableName = table, Manufacturer = "ZZ TEST MFR", Catalog = "ZZ-CATONLY-003", Description = q.Description };
err = null;
try { CatalogTransactionService.Execute(bs, n, pn, q3); } catch (Exception ex) { err = ex.Message; }
Check("failure after backup and lock surfaces an error", err is not null, err ?? "");
Check("catalog byte-identical after the failure (rollback)", File.ReadAllBytes(mdb).SequenceEqual(afterTwo));
Check("failed row not present", !svc.Exists(table, "ZZ TEST MFR", "ZZ-CATONLY-003"));
Check("failed run left a report with ERROR", Directory.GetFiles(backups, "CatalogReport.txt", SearchOption.AllDirectories).Any(f => File.ReadAllText(f).Contains("ERROR")));
Check("every run, refused or failed, left its own backup folder (4)", Directory.GetDirectories(backups).Length == 4);
Check("lock released after failure", !File.Exists(Path.Combine(dir, "LSE_PartNumber.lock")));
Check("still no library, icon or lookup file written", Directory.GetFileSystemEntries(untouched).Length == 0);
// ---- Footprint mapping (LSEFOOTPRINTMAP): copies of the real footprint_lookup.mdb and the catalog; nothing real is touched ----
if (args.Length >= 3)
{
    string fpDir = Path.Combine(dir, "fp"), fpLib = Path.Combine(dir, "fplib", "Panel"), menuDir = Path.Combine(dir, "menu"), libBk = Path.Combine(dir, "libbk");
    Directory.CreateDirectory(fpDir); Directory.CreateDirectory(fpLib); Directory.CreateDirectory(menuDir);
    string lookupMdb = Path.Combine(fpDir, "footprint_lookup.mdb"); File.Copy(args[2], lookupMdb, true);
    string fa = Path.Combine(fpLib, "FP_002-TEST_A.dwg"), fb = Path.Combine(fpLib, "FP_002-TEST_B.dwg"), outside = Path.Combine(dir, "outside.dwg");
    foreach (string f in new[] { fa, fb, outside }) File.WriteAllText(f, "not a real drawing; the mapping only checks that the file exists");
    var ms = new AppSettings { CatalogPath = mdb, NamingWorkbookPath = wb, CatalogBackupFolder = backups, LsePartColumn = "USER1", FootprintLookupPath = lookupMdb, FootprintLibraryFolder = Path.Combine(dir, "fplib"), StockFootprintLibraryFolder = Path.Combine(dir, "nostock"), IconMenuFolder = menuDir, BackupFolder = libBk };
    var fp = new FootprintLookupService(lookupMdb);
    FootprintMapRequest Req(string mfr, string cat, string file, bool over = false) => new() { Manufacturer = mfr, Catalog = cat, Footprint = file, Description = "TEST ROW", Overwrite = over };
    string? Try(Action a) { try { a(); return null; } catch (Exception ex) { return ex.Message; } }
    string K1 = "ZZ-CATONLY-001", K2 = "ZZ-CATONLY-002", Mfr = "ZZ TEST MFR";

    var sw = System.Diagnostics.Stopwatch.StartNew();
    var rows = FootprintMappingService.Load(ms);
    Console.WriteLine($"Load: {rows.Count} LSE part(s) in {sw.Elapsed.TotalSeconds:F1}s; unmapped {rows.Count(r => r.Status == FootprintMappingService.Unmapped)}, mapped {rows.Count(r => r.Status == FootprintMappingService.Mapped)}, block name {rows.Count(r => r.Status == FootprintMappingService.BlockName)}, missing drawing {rows.Count(r => r.Status == FootprintMappingService.MissingDrawing)}");
    { var t1 = System.Diagnostics.Stopwatch.StartNew(); var parts = new CatalogService(mdb).ReadParts("USER1"); double a = t1.Elapsed.TotalSeconds; t1.Restart(); var mp = fp.ReadMappings(parts.Select(p => (p.Manufacturer, p.Catalog))); Console.WriteLine($"  timing: ReadParts {parts.Count} rows {a:F1}s, ReadMappings {mp.Count} hits {t1.Elapsed.TotalSeconds:F1}s, statuses {sw.Elapsed.TotalSeconds:F1}s total"); }
    var mine1 = rows.FirstOrDefault(r => r.Manufacturer == Mfr && r.Catalog == K1);
    Check("Load lists the new catalog-only parts", mine1 is not null && rows.Any(r => r.Manufacturer == Mfr && r.Catalog == K2));
    Check("a new part with no lookup table is Unmapped", mine1?.Status == FootprintMappingService.Unmapped && mine1.PartNumber == r1.PartNumber && mine1.Family.Equals(table, StringComparison.OrdinalIgnoreCase));
    Check("Load found existing real mappings (stock block names or paths)", rows.Any(r => r.BlkNam.Length > 0), "none of the LSE parts is mapped yet is possible, see counts above");

    Check("Validate refuses a missing drawing", Try(() => FootprintMappingService.Validate(ms, Req(Mfr, K1, Path.Combine(fpLib, "nope.dwg")))) is not null);
    Check("Validate refuses a drawing outside the footprint library", (Try(() => FootprintMappingService.Validate(ms, Req(Mfr, K1, outside))) ?? "").Contains("footprint library"));
    string notDwg = Path.Combine(fpLib, "x.txt"); File.WriteAllText(notDwg, "x");
    Check("Validate refuses a non-dwg file", (Try(() => FootprintMappingService.Validate(ms, Req(Mfr, K1, notDwg))) ?? "").Contains(".dwg"));
    Check("Validate refuses a blank manufacturer or catalog", Try(() => FootprintMappingService.Validate(ms, Req("", K1, fa))) is not null && Try(() => FootprintMappingService.Validate(ms, Req(Mfr, "", fa))) is not null);
    Check("Validate refuses a missing lookup MDB", Try(() => FootprintMappingService.Validate(new AppSettings { CatalogPath = mdb, FootprintLookupPath = Path.Combine(dir, "none.mdb"), FootprintLibraryFolder = Path.Combine(dir, "fplib"), IconMenuFolder = menuDir, BackupFolder = libBk }, Req(Mfr, K1, fa))) is not null);
    Check("Validate returns the full path of a good drawing", FootprintMappingService.Validate(ms, Req(Mfr, K1, fa)).Equals(fa, StringComparison.OrdinalIgnoreCase));

    byte[] fpBefore = File.ReadAllBytes(lookupMdb);
    Check("manufacturer table absent before the first mapping", !fp.TableExists(Mfr));
    var m1 = FootprintMappingService.Execute(ms, Req(Mfr, K1, fa));
    Check("create: one row written", m1.Written == 1 && m1.Unchanged == 0);
    Check("create: manufacturer table created and row read back", fp.TableExists(Mfr) && fp.FindPlain(Mfr, K1) == (1, fa));
    Check("create: backup holds the pre-change lookup MDB byte for byte", File.ReadAllBytes(Path.Combine(m1.BackupFolder, "footprint_lookup.mdb")).SequenceEqual(fpBefore));
    Check("create: FootprintMapReport.txt written with SUCCESS", File.Exists(Path.Combine(m1.BackupFolder, "FootprintMapReport.txt")) && File.ReadAllText(Path.Combine(m1.BackupFolder, "FootprintMapReport.txt")).Contains("Status: SUCCESS"));
    Check("create: library lock released", !File.Exists(LibraryLock.PathFor(menuDir)));
    Check("create: Load now shows the part as Mapped to that drawing", FootprintMappingService.Load(ms).First(r => r.Manufacturer == Mfr && r.Catalog == K1) is { Status: FootprintMappingService.Mapped } m && m.BlkNam == fa);
    Check("create: Existing() returns the stored path", FootprintMappingService.Existing(ms, Mfr, K1) == fa);
    Check("create: default_cat.mdb not touched by the mapping", File.ReadAllBytes(mdb).SequenceEqual(afterTwo));
    // r7: a lookup copy that Vault left read-only is refused before the lock and the backups
    byte[] fpRo = File.ReadAllBytes(lookupMdb); int bkRo = Directory.GetDirectories(libBk).Length; File.SetAttributes(lookupMdb, FileAttributes.ReadOnly);
    err = Try(() => FootprintMappingService.Execute(ms, Req(Mfr, "ZZ-RO-KEY", fa)));
    File.SetAttributes(lookupMdb, FileAttributes.Normal);
    Check("read-only lookup refused before writing: names the file, Nothing was changed, MDB identical, no backup, no lock", err is not null && err.Contains(lookupMdb) && err.Contains("Nothing was changed") && File.ReadAllBytes(lookupMdb).SequenceEqual(fpRo) && Directory.GetDirectories(libBk).Length == bkRo && !File.Exists(LibraryLock.PathFor(menuDir)), err ?? "");

    byte[] fpAfterCreate = File.ReadAllBytes(lookupMdb); int bkCount = Directory.GetDirectories(libBk).Length;
    err = Try(() => FootprintMappingService.Execute(ms, Req(Mfr, K1, fb)));
    Check("duplicate with a different footprint refused without confirmation", err is not null && err.Contains("already mapped"), err ?? "");
    Check("refusal leaves the lookup MDB byte-identical and makes no backup folder", File.ReadAllBytes(lookupMdb).SequenceEqual(fpAfterCreate) && Directory.GetDirectories(libBk).Length == bkCount);
    var same = FootprintMappingService.Execute(ms, Req(Mfr, K1, fa));
    Check("mapping to the same footprint again is a no-op", same.Written == 0 && same.Unchanged == 1 && File.ReadAllBytes(lookupMdb).SequenceEqual(fpAfterCreate) && Directory.GetDirectories(libBk).Length == bkCount);

    var upd = FootprintMappingService.Execute(ms, Req(Mfr, K1, fb, true));
    Check("update with confirmation changes the path", upd.Written == 1 && fp.FindPlain(Mfr, K1) == (1, fb), fp.FindPlain(Mfr, K1).ToString());
    Check("update keeps a single row for the catalog number", fp.FindPlain(Mfr, K1).Rows == 1);
    Check("update backup holds the pre-update MDB byte for byte", File.ReadAllBytes(Path.Combine(upd.BackupFolder, "footprint_lookup.mdb")).SequenceEqual(fpAfterCreate));

    // existing manufacturer table, spelled in another case
    string realTable = fp.GetTables().First(t => t.Equals("BUSSMANN", StringComparison.OrdinalIgnoreCase));
    int tablesBefore = fp.GetTables().Count;
    var ex1 = FootprintMappingService.Execute(ms, Req("bussmann", "ZZ-MAP-TEST-001", fa));
    Check("existing manufacturer table is reused whatever the spelling", ex1.Written == 1 && fp.GetTables().Count == tablesBefore && fp.FindPlain(realTable, "ZZ-MAP-TEST-001") == (1, fa));

    // all-or-nothing batch: a failure after the writes (raised from the progress callback at the read-back line) must restore the MDB
    byte[] fpBeforeBatch = File.ReadAllBytes(lookupMdb);
    err = Try(() => FootprintMappingService.Execute(ms, new[] { Req(Mfr, K2, fa), Req("bussmann", "ZZ-MAP-TEST-002", fb) }, line => { if (line.StartsWith("Footprint lookup read-back")) throw new InvalidOperationException("simulated failure after the writes"); }));
    Check("failure after the writes surfaces the error", err is not null && err.Contains("simulated"), err ?? "");
    Check("lookup MDB byte-identical after rollback (both rows gone)", File.ReadAllBytes(lookupMdb).SequenceEqual(fpBeforeBatch) && fp.FindPlain(Mfr, K2).Rows == 0 && fp.FindPlain(realTable, "ZZ-MAP-TEST-002").Rows == 0);
    Check("rollback report records ERROR and the rollback", Directory.GetFiles(libBk, "FootprintMapReport.txt", SearchOption.AllDirectories).Any(f => { string t = File.ReadAllText(f); return t.Contains("ERROR: simulated") && t.Contains("Footprint lookup rollback: PASS"); }));
    Check("library lock released after rollback", !File.Exists(LibraryLock.PathFor(menuDir)));
    var batch = FootprintMappingService.Execute(ms, new[] { Req(Mfr, K2, fa), Req("bussmann", "ZZ-MAP-TEST-002", fb) });
    Check("the same batch succeeds when nothing fails (2 rows, one backup)", batch.Written == 2 && fp.FindPlain(Mfr, K2) == (1, fa) && fp.FindPlain(realTable, "ZZ-MAP-TEST-002") == (1, fb));
    Check("a part listed twice in one batch is refused", Try(() => FootprintMappingService.Execute(ms, new[] { Req(Mfr, K2, fa), Req(Mfr.ToLowerInvariant(), K2.ToLowerInvariant(), fb) })) is not null);
    Check("catalog still untouched after all mapping runs", File.ReadAllBytes(mdb).SequenceEqual(afterTwo));

    // audit: the new note comes from the same Load, run through the real audit entry point (menus and library folders are absent in the sandbox, which only adds unrelated findings)
    string? cfg = null; for (string? d = AppContext.BaseDirectory; d is not null && cfg is null; d = Path.GetDirectoryName(d)) { string c = Path.Combine(d, "config", "LseLibraryStandard.json"); if (File.Exists(c)) cfg = c; }
    if (cfg is not null && !(args.Length >= 4 && args[3] == "noaudit"))
    {
        byte[] lookupBeforeAudit = File.ReadAllBytes(lookupMdb);
        var rep2 = LibraryAuditService.Run(ms, LibraryStandardService.Load(cfg), new AuditOptions());
        var unmappedNotes = rep2.Findings.Where(f => f.Area == "Lookup" && f.Path.StartsWith("default_cat.mdb [")).ToList();
        var expected = FootprintMappingService.Load(ms).Where(r => r.Status == FootprintMappingService.Unmapped).Select(r => r.Family).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        Check("audit adds one Info note per family with unmapped LSE parts", unmappedNotes.Count == expected && expected > 0 && unmappedNotes.All(f => f.Severity == Severity.Info && f.Suggestion.Contains("LSEFOOTPRINTMAP")), $"{unmappedNotes.Count} vs {expected}");
        Check("audit summary reports the catalog parts line", rep2.Summary.Any(l => l.StartsWith("Catalog parts:")));
        Check("audit left both MDBs unchanged (read only)", File.ReadAllBytes(mdb).SequenceEqual(afterTwo) && File.ReadAllBytes(lookupMdb).SequenceEqual(lookupBeforeAudit));
    }
    else Console.WriteLine("SKIP audit checks (noaudit given, or config\\LseLibraryStandard.json not found above the test binary)");
}
else Console.WriteLine("SKIP footprint mapping checks: pass footprint_lookup.mdb as the third argument");
// ---- Symbol link (WDBLKNAM on existing rows): copy of the catalog, throw-away symbol libraries; nothing real is touched ----
{
    string symRoot = Path.Combine(dir, "symlib"), stockRoot = Path.Combine(dir, "stocksym");
    Directory.CreateDirectory(Path.Combine(symRoot, "Sub")); Directory.CreateDirectory(stockRoot);
    foreach (string f in new[] { Path.Combine(symRoot, "Sub", "ZZ_SYM_A.dwg"), Path.Combine(symRoot, "ZZ_SYM_B.dwg"), Path.Combine(stockRoot, "ZZ_STOCK_C.dwg") }) File.WriteAllText(f, "not a real drawing; only the file name matters");
    string? cfg2 = null; for (string? d = AppContext.BaseDirectory; d is not null && cfg2 is null; d = Path.GetDirectoryName(d)) { string c = Path.Combine(d, "config", "LseLibraryStandard.json"); if (File.Exists(c)) cfg2 = c; }
    var std = cfg2 is not null ? LibraryStandardService.Load(cfg2) : new LibraryStandard();
    var ss = new AppSettings { CatalogPath = mdb, CatalogBackupFolder = backups, LsePartColumn = "USER1", SymbolLibraryFolder = symRoot, StockSymbolLibraryFolder = stockRoot };
    string? TryS(Action a) { try { a(); return null; } catch (Exception ex) { return ex.Message; } }
    SymbolLinkRequest L(CatalogAddResult r, string cat, string sym, bool over = false) => new() { Table = table, Manufacturer = "ZZ TEST MFR", Catalog = cat, PartNumber = r.PartNumber, Symbol = sym, Overwrite = over };
    string RowDump(string part) { dynamic c = Ace.Open(mdb); dynamic? rs = null; try { rs = c.Execute($"SELECT * FROM [{table}] WHERE [USER1]='{part}'"); var sb = new System.Text.StringBuilder(); for (int i = 0; i < rs.Fields.Count; i++) { string nm = Convert.ToString(rs.Fields[i].Name) ?? ""; if (nm.Equals("WDBLKNAM", StringComparison.OrdinalIgnoreCase)) continue; sb.Append(nm).Append('=').Append(Convert.ToString(rs.Fields[i].Value)).Append('|'); } return sb.ToString(); } finally { Ace.Release(rs); Ace.Close(c); } }
    int RowCount() { dynamic c = Ace.Open(mdb); dynamic? rs = null; try { rs = c.Execute($"SELECT COUNT(*) FROM [{table}]"); return Convert.ToInt32(rs.Fields[0].Value); } finally { Ace.Release(rs); Ace.Close(c); } }
    string Sym(CatalogAddResult r) { var (rows, sy) = svc.FindSymbolRows(table, "USER1", r.PartNumber, "ZZ TEST MFR", r == r1 ? "ZZ-CATONLY-001" : "ZZ-CATONLY-002"); return rows == 1 ? sy : "<" + rows + " rows>"; }
    string C1 = "ZZ-CATONLY-001", C2 = "ZZ-CATONLY-002";

    Check("symbol: block found in a library subfolder, library spelling returned", CatalogTransactionService.ResolveSymbol(ss, std, "zz_sym_a") == "ZZ_SYM_A");
    Check("symbol: block found in the stock library", CatalogTransactionService.ResolveSymbol(ss, std, "ZZ_STOCK_C") == "ZZ_STOCK_C");
    Check("symbol: missing block refused", (TryS(() => CatalogTransactionService.ResolveSymbol(ss, std, "ZZ_NOPE")) ?? "").Contains("not found"));
    Check("symbol: path or .dwg in the name refused", TryS(() => CatalogTransactionService.ResolveSymbol(ss, std, "Sub\\ZZ_SYM_A")) is not null && TryS(() => CatalogTransactionService.ResolveSymbol(ss, std, "ZZ_SYM_A.dwg")) is not null && TryS(() => CatalogTransactionService.ResolveSymbol(ss, std, "")) is not null);
    Check("symbol: no library folder configured refused", TryS(() => CatalogTransactionService.ResolveSymbol(new AppSettings(), std, "ZZ_SYM_A")) is not null);

    string baseline = Sym(r1), dump1 = RowDump(r1.PartNumber), dump2 = RowDump(r2.PartNumber); int count0 = RowCount(); byte[] pre = File.ReadAllBytes(mdb);
    var set1 = CatalogTransactionService.SetSymbols(ss, std, new[] { L(r1, C1, "zz_sym_a") });
    Check("symbol set: one row written, library spelling stored, read back", set1.Written == 1 && set1.Unchanged == 0 && Sym(r1) == "ZZ_SYM_A", "was '" + baseline + "'");
    Check("symbol set: only WDBLKNAM changed on the row, other row and row count untouched", RowDump(r1.PartNumber) == dump1 && RowDump(r2.PartNumber) == dump2 && Sym(r2) == "" && RowCount() == count0);
    Check("symbol set: backup holds the pre-change catalog byte for byte", File.ReadAllBytes(Path.Combine(set1.BackupFolder, "default_cat.mdb")).SequenceEqual(pre));
    Check("symbol set: SymbolLinkReport.txt written with SUCCESS and the read-back line", File.Exists(Path.Combine(set1.BackupFolder, "SymbolLinkReport.txt")) && File.ReadAllText(Path.Combine(set1.BackupFolder, "SymbolLinkReport.txt")) is var rt && rt.Contains("Status: SUCCESS") && rt.Contains("WDBLKNAM read-back: PASS"));
    Check("symbol set: issuance lock released", !File.Exists(Path.Combine(dir, "LSE_PartNumber.lock")));
    Check("symbol set: ReadSymbols (the grid column) shows the link", new CatalogService(mdb).ReadSymbols("USER1").TryGetValue(table + "|" + r1.PartNumber, out var shown) && shown == "ZZ_SYM_A");

    byte[] afterSet = File.ReadAllBytes(mdb); int bk1 = Directory.GetDirectories(backups).Length;
    var same = CatalogTransactionService.SetSymbols(ss, std, new[] { L(r1, C1, "ZZ_SYM_A") });
    Check("symbol set again to the same block is a no-op (no backup, catalog identical)", same.Written == 0 && same.Unchanged == 1 && File.ReadAllBytes(mdb).SequenceEqual(afterSet) && Directory.GetDirectories(backups).Length == bk1);
    err = TryS(() => CatalogTransactionService.SetSymbols(ss, std, new[] { L(r1, C1, "ZZ_SYM_B") }));
    Check("symbol change without confirmation refused", err is not null && err.Contains("already links"), err ?? "");
    Check("refused change leaves the catalog identical and makes no backup folder", File.ReadAllBytes(mdb).SequenceEqual(afterSet) && Directory.GetDirectories(backups).Length == bk1);
    var chg = CatalogTransactionService.SetSymbols(ss, std, new[] { L(r1, C1, "ZZ_SYM_B", true) });
    Check("symbol change with confirmation replaces the value", chg.Written == 1 && Sym(r1) == "ZZ_SYM_B" && RowDump(r1.PartNumber) == dump1);
    Check("change backup holds the pre-change catalog byte for byte", File.ReadAllBytes(Path.Combine(chg.BackupFolder, "default_cat.mdb")).SequenceEqual(afterSet));

    byte[] afterChg = File.ReadAllBytes(mdb); int bk2 = Directory.GetDirectories(backups).Length;
    err = TryS(() => CatalogTransactionService.SetSymbols(ss, std, new[] { L(r1, C1, "ZZ_NOPE", true) }));
    Check("symbol missing from the library refused before anything is written", err is not null && err.Contains("not found") && File.ReadAllBytes(mdb).SequenceEqual(afterChg) && Directory.GetDirectories(backups).Length == bk2, err ?? "");
    err = TryS(() => CatalogTransactionService.SetSymbols(ss, std, new[] { new SymbolLinkRequest { Table = table, Manufacturer = "ZZ TEST MFR", Catalog = C1, PartNumber = "ZZ-NO-SUCH-PART", Symbol = "ZZ_SYM_A" } }));
    Check("no matching catalog row refused (nothing is ever inserted)", err is not null && err.Contains("No catalog row") && File.ReadAllBytes(mdb).SequenceEqual(afterChg) && RowCount() == count0, err ?? "");
    Check("a row listed twice in one batch is refused", TryS(() => CatalogTransactionService.SetSymbols(ss, std, new[] { L(r2, C2, "ZZ_SYM_A"), L(r2, C2, "ZZ_SYM_B") })) is not null && File.ReadAllBytes(mdb).SequenceEqual(afterChg));

    // all-or-nothing batch: a failure raised at the read-back line (after both rows were written) must restore the catalog
    err = TryS(() => CatalogTransactionService.SetSymbols(ss, std, new[] { L(r1, C1, "ZZ_STOCK_C", true), L(r2, C2, "ZZ_STOCK_C") }, line => { if (line.StartsWith("Catalog WDBLKNAM read-back")) throw new InvalidOperationException("simulated failure after the writes"); }));
    Check("failure after the writes surfaces the error", err is not null && err.Contains("simulated"), err ?? "");
    Check("catalog byte-identical after rollback, both rows as before", File.ReadAllBytes(mdb).SequenceEqual(afterChg) && Sym(r1) == "ZZ_SYM_B" && Sym(r2) == "");
    Check("rollback report records ERROR and the rollback", Directory.GetFiles(backups, "SymbolLinkReport.txt", SearchOption.AllDirectories).Any(f => { string t = File.ReadAllText(f); return t.Contains("ERROR: simulated") && t.Contains("Catalog rollback: PASS"); }));
    Check("issuance lock released after rollback", !File.Exists(Path.Combine(dir, "LSE_PartNumber.lock")));
    var batch2 = CatalogTransactionService.SetSymbols(ss, std, new[] { L(r1, C1, "ZZ_STOCK_C", true), L(r2, C2, "ZZ_STOCK_C") });
    Check("the same batch succeeds when nothing fails (2 rows, one backup)", batch2.Written == 2 && Sym(r1) == "ZZ_STOCK_C" && Sym(r2) == "ZZ_STOCK_C" && RowDump(r1.PartNumber) == dump1 && RowDump(r2.PartNumber) == dump2 && RowCount() == count0);
    Check("symbol libraries untouched (3 files, nothing written)", Directory.GetFiles(symRoot, "*", SearchOption.AllDirectories).Length + Directory.GetFiles(stockRoot, "*", SearchOption.AllDirectories).Length == 3);
    // r7: a catalog copy that Vault left read-only is refused before the lock and the backup
    byte[] catRo = File.ReadAllBytes(mdb); int bkRo2 = Directory.GetDirectories(backups).Length; File.SetAttributes(mdb, FileAttributes.ReadOnly);
    err = TryS(() => CatalogTransactionService.SetSymbols(ss, std, new[] { L(r1, C1, "ZZ_SYM_A", true) }));
    File.SetAttributes(mdb, FileAttributes.Normal);
    Check("read-only catalog refused before writing: names the file, Nothing was changed, catalog identical, no backup, no lock", err is not null && err.Contains(mdb) && err.Contains("Nothing was changed") && File.ReadAllBytes(mdb).SequenceEqual(catRo) && Directory.GetDirectories(backups).Length == bkRo2 && !File.Exists(Path.Combine(dir, "LSE_PartNumber.lock")), err ?? "");
}
Console.WriteLine($"{pass} passed, {fail} failed. Sandbox: {dir}");
return fail == 0 ? 0 : 1;
