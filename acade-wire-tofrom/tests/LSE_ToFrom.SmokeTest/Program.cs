using LSE.AutoCADElectrical.WireToFrom;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

// LSE_ToFrom smoke test. Runs without AutoCAD: the drawing side is faked through HostBridge delegates; the plan grid is a real DataGridView.
int passed = 0, failed = 0;
void Check(bool ok, string name) { if (ok) { passed++; Console.WriteLine("PASS " + name); } else { failed++; Console.WriteLine("FAIL " + name); } }
void Throws(Action a, string contains, string name) { try { a(); Check(false, name + " (no exception)"); } catch (Exception ex) { bool ok = ex.Message.Contains(contains, StringComparison.OrdinalIgnoreCase); Check(ok, name + (ok ? "" : ": " + ex.Message)); } }
DataGridViewCellPaintingEventArgs PaintArgs(DataGridView grid, Graphics g, int rowIndex)
{
    var bounds = new Rectangle(0, 0, 60, 22);
    return new DataGridViewCellPaintingEventArgs(grid, g, bounds, bounds, rowIndex, 0, DataGridViewElementStates.Visible, false, false, "", grid.Rows[rowIndex].Cells[0].InheritedStyle, new DataGridViewAdvancedBorderStyle(), DataGridViewPaintParts.All);
}

string root = args.Length > 0 ? args[0] : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
string configPath = Path.Combine(root, "config", "tofrom.config.json");
string work = Path.Combine(Path.GetTempPath(), "LSE_ToFrom_smoke_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
Directory.CreateDirectory(work);
Console.WriteLine("Work folder: " + work);

// 1. Config
ToFromConfig config = ConfigService.Load(configPath);
{
    Check(config.Format == "{FROM}/{TO}" && config.Schematic.LabelLayer == "LSE_TOFROM" && config.Database.WireSql.Length == 0 && config.Csv.ReverseAtFarEnd, "config loaded with the documented defaults");
    Check(config.Database.Columns.All().Count(c => c.Required) == 3 && config.Database.CandidateColumns.Count == 11, "column map: 3 required roles, 11 candidate columns");
    string bad = Path.Combine(work, "bad.json");
    File.WriteAllText(bad, "{ \"database\": { \"wireSql\": \"DELETE FROM x\" } }");
    Throws(() => ConfigService.Load(bad), "SELECT", "non-SELECT wireSql is refused");
    File.WriteAllText(bad, "{ \"schematic\": { \"labelAt\": \"middle\" } }");
    Throws(() => ConfigService.Load(bad), "labelAt", "bad labelAt is refused");
    File.WriteAllText(bad, "{ \"schematic\": { \"labelLayer\": \"WIRE/NO\" } }");
    Throws(() => ConfigService.Load(bad), "layer name", "invalid layer name is refused");
    var defaults = ConfigService.Load(Path.Combine(work, "missing.json"));
    Check(defaults.Source.Contains("built-in") && defaults.Csv.Delimiter == ",", "built-in defaults when the config is missing");
    string saved = Path.Combine(work, "saved.json");
    config.Database.WireSql = "SELECT * FROM [WFROMTO]";
    ConfigService.Save(config, saved);
    var back = ConfigService.Load(saved);
    Check(back.Database.WireSql == "SELECT * FROM [WFROMTO]" && back.Source == saved, "config round trip through Save");
    config = ConfigService.Load(configPath);
}

// 2. Project file
string projectFolder = Path.Combine(work, "JOB");
Directory.CreateDirectory(projectFolder);
string wdp = Path.Combine(projectFolder, "123456-R.wdp");
string[] dwgs = { "123456-R-E001.dwg", "123456-R-E002.dwg", "123456-R-E003.dwg" };
{
    var lines = new List<string> { "*[1]PROJECT NAME", "+[1]C:\\libs", "?[6]S", "===Sheet one", dwgs[0], "===Sheet two", dwgs[1], "====REF", "===Reference sheet", "ref.dwg", "===Sheet three", dwgs[2] };
    File.WriteAllText(wdp, string.Join("\r\n", lines) + "\r\n", new UTF8Encoding(true));
    foreach (string d in dwgs) File.WriteAllText(Path.Combine(projectFolder, d), "fake");
    var p = ProjectService.Read(wdp);
    Check(p.Drawings.Count == 4 && p.Drawings[1].Index == 2 && p.Drawings[2].ReferenceOnly && p.Drawings[2].Description == "Reference sheet" && p.ByIndex(4)!.Path.EndsWith("E003.dwg"), "wdp drawing list read in order with reference flag");
    Check(p.ByName("123456-r-e002")!.Index == 2 && p.ByName("nope") is null, "drawing lookup by name (case-insensitive, extension optional)");
    Check(ProjectService.DatabasePath(p, config.Database, "") == Path.Combine(projectFolder, "123456-R.mdb"), "scratch database path from pattern: " + ProjectService.DatabasePath(p, config.Database, ""));
    string lisp = Path.Combine(work, "other.mdb"); File.WriteAllText(lisp, "x");
    Check(ProjectService.DatabasePath(p, config.Database, lisp) == lisp, "LISP-reported path wins when it exists");
    Check(ProjectService.OutputFile(p, config.Csv, config.Csv.LabelFile) == Path.Combine(projectFolder, "LSE_ToFrom", "123456-R_tofrom_labels.csv"), "output file naming");
}

// 3. Identifier composition
{
    var r = new WireRow { WireNo = "101", Cmp1 = "K1", Pin1 = "13", Cmp2 = "TB2", Pin2 = "1", Loc1 = "MCC1", Loc2 = "MCC1" };
    Check(IdentifierService.Compose(r, false, config) == "K1:13/TB2:1", "K1:13/TB2:1");
    Check(IdentifierService.Compose(r, true, config) == "TB2:1/K1:13", "reversed at the far end: TB2:1/K1:13");
    var blank = new WireRow { WireNo = "102", Cmp1 = "K1", Pin1 = "", Cmp2 = "", Pin2 = "A1" };
    Check(IdentifierService.Compose(blank, false, config) == "K1:?/?:A1", "missing pin and tag show ?: " + IdentifierService.Compose(blank, false, config));
    var loc = new ToFromConfig { IncludeLocation = true, Format = "{FROM}/{TO}" };
    Check(IdentifierService.Compose(r, false, loc) == "MCC1+K1:13/MCC1+TB2:1", "location prefix when includeLocation: " + IdentifierService.Compose(r, false, loc));
    var other = new ToFromConfig { Format = "{FROMTAG}.{FROMPIN}-{TOTAG}.{TOPIN} ({WIRENO})", Uppercase = false, MaxLength = 12 };
    Check(IdentifierService.Compose(new WireRow { WireNo = "7", Cmp1 = "pb1", Pin1 = "3", Cmp2 = "cr1", Pin2 = "a1" }, false, other) == "pb1.3-cr1.a1", "token format, no upper case, maxLength cut: " + IdentifierService.Compose(new WireRow { WireNo = "7", Cmp1 = "pb1", Pin1 = "3", Cmp2 = "cr1", Pin2 = "a1" }, false, other));
    Check(IdentifierService.Handle("0x2f3a") == "2F3A" && IdentifierService.Handle("00A1") == "00A1".ToUpperInvariant() && IdentifierService.Key("2f3a", 1) == "2F3A/1" && IdentifierService.Handle("") == "", "handle normalisation and key");
    var e1 = IdentifierService.MakeEnd(r, 1, config, true); var e2 = IdentifierService.MakeEnd(r, 2, config, true); var s2 = IdentifierService.MakeEnd(r, 2, config, false);
    Check(e1.Text == "K1:13/TB2:1" && e2.Text == "TB2:1/K1:13" && s2.Text == "K1:13/TB2:1" && e2.Tag == "TB2" && e2.OtherTag == "K1", "CSV ends reversed at the far end, schematic ends not");
    var swapped = r.Swapped();
    Check(swapped.Cmp1 == "TB2" && swapped.Pin2 == "13" && IdentifierService.Compose(swapped, false, config) == "TB2:1/K1:13", "swapEnds row");
}

// 4. Report import
string reportFile = Path.Combine(work, "fromto.csv");
{
    var lines = new List<string>
    {
        "Wire From/To Report  Project: 123456-R",
        "",
        "WIRENO,CMP1,PIN1,CMP2,PIN2,WIREHDL1,WIREHDL2,CMPHDL1,CMPHDL2,DWGIX1,DWGIX2",
        "101,K1,13,TB2,1,2F3A,2F3A,1A00,1A10,1,1",
        "101,TB2,1,CR1,A1,2F3B,2F3C,1A10,1A20,1,1",
        "102,PB1,,LT1,X1,2F40,2F41,1B00,1B01,2,2",
        "103,S1,3,K5,A2,2F50,2F60,1C00,1C10,1,4",
        "104,M1,T1,,,2F70,,1D00,,2,2",
        "\"105\",\"X1\",\"5\",\"X2\",\"6\",\"2F80\",\"2F81\",\"1E00\",\"1E01\",\"9\",\"9\"",
    };
    File.WriteAllText(reportFile, string.Join("\r\n", lines) + "\r\n", Encoding.Latin1);
    var notes = new List<string>();
    var rows = ReportImport.Read(reportFile, config.ReportFile, notes);
    Check(rows.Count == 6 && rows[0].Cmp1 == "K1" && rows[0].WireHdl1 == "2F3A" && rows[3].Pin2 == "A2" && rows[3].DwgIx2 == "4" && rows[5].Cmp1 == "X1" && rows[5].DwgIx2 == "9" && notes[0].Contains("header on line 3"), "report file: title lines skipped, header found on line 3, quoted fields read");
    string tabbed = Path.Combine(work, "fromto.txt");
    File.WriteAllText(tabbed, "WIRENO\tCMP1\tPIN1\tCMP2\tPIN2\tWIREHDL1\r\n9\tA\t1\tB\t2\tFF\r\n", new UTF8Encoding(true));
    rows = ReportImport.Read(tabbed, config.ReportFile, new List<string>());
    Check(rows.Count == 1 && rows[0].Cmp2 == "B" && rows[0].WireHdl2 == "", "tab-delimited report with a BOM and missing optional columns");
    File.WriteAllText(tabbed, "WIRE,FROM,TO\r\n1,a,b\r\n");
    Throws(() => ReportImport.Read(tabbed, config.ReportFile, new List<string>()), "No header row", "report without the expected header is refused");
    Check(ReportImport.Split("a,\"b,c\",\"d\"\"e\",,f", ',').SequenceEqual(new[] { "a", "b,c", "d\"e", "", "f" }), "CSV splitter handles quotes, escaped quotes and empty fields");
}

// 5. Planning against fake drawings
var settings = new AppSettings { ConfigPath = configPath, BackupFolderName = "_LSE_TOFROM_BACKUP", BackupsToKeep = 1, DataSource = "report", LastReportFile = reportFile };
var project = ProjectService.Read(wdp);
var fakeLabels = new Dictionary<string, List<ExistingLabel>>(StringComparer.OrdinalIgnoreCase);
var lockedDrawings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
var openDrawings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
var writes = new List<DrawingWriteRequest>();
bool failNextWrite = false;
string wireNoLayerOf = "WIRENO";
List<string> wireLayers = new() { "WIRES", "_MULTI_*" };
DrawingLabels FakeRead(string path)
{
    var d = new DrawingLabels { Path = path, Exists = File.Exists(path), OpenInEditor = openDrawings.Contains(path), Locked = lockedDrawings.Contains(path), Sheet = project.ByName(path)?.Index.ToString() ?? "", WireNoLayer = wireNoLayerOf, WireLayers = wireLayers };
    if (fakeLabels.TryGetValue(path, out var list)) d.Labels.AddRange(list.Select(l => new ExistingLabel { Handle = l.Handle, Key = l.Key, Text = l.Text, Node = l.Node, Block = l.Block, Layer = l.Layer, Point = l.Point }));
    return d;
}
int handleSeed = 0x100;
List<string> FakeWrite(DrawingWriteRequest req)
{
    if (failNextWrite) throw new InvalidOperationException("simulated drawing failure");
    writes.Add(req);
    if (!fakeLabels.TryGetValue(req.DrawingPath, out var list)) { list = new(); fakeLabels[req.DrawingPath] = list; }
    foreach (var c in req.Changes)
    {
        if (c.Action == LabelAction.Delete) list.RemoveAll(l => l.Handle.Equals(c.ExistingHandle, StringComparison.OrdinalIgnoreCase));
        else if (c.Action == LabelAction.Update) { var l = list.First(x => x.Handle.Equals(c.ExistingHandle, StringComparison.OrdinalIgnoreCase)); l.Text = c.Text; l.Node = c.Node; }
        else list.Add(new ExistingLabel { Handle = (handleSeed++).ToString("X"), Key = c.Key, Text = c.Text, Node = c.Node, Block = "TOFROM_LBL_H", Layer = req.Schematic.LabelLayer });
    }
    if (!openDrawings.Contains(req.DrawingPath)) File.WriteAllText(req.DrawingPath, "fake+" + list.Count);
    return new List<string> { "fake write " + req.Changes.Count };
}
var bridge = new HostBridge { ReadLabels = FakeRead, WriteLabels = FakeWrite };
RunPlan Plan(bool annotate = true, string? only = null)
{
    var issues = new List<Issue>(); var notes = new List<string>();
    var rows = DataSourceService.Load(settings, config, project, "", reportFile, issues, notes, out string src);
    var plan = IdentifierService.Plan(project, rows, config, settings, bridge, annotate, only);
    plan.DataSource = src; plan.Issues.InsertRange(0, issues); plan.Log.InsertRange(1, notes);
    return plan;
}
{
    var plan = Plan();
    Check(plan.Rows.Count == 6 && plan.CsvEnds.Count == 12 && plan.SchematicEnds.Count == 5, $"rows 6, csv ends 12, schematic ends 5 (one row without a resolvable drawing): {plan.Rows.Count}/{plan.CsvEnds.Count}/{plan.SchematicEnds.Count}");
    Check(plan.Count(IssueKind.MissingPin) == 2 && plan.Count(IssueKind.MissingTag) == 1, $"validation: 2 missing pins, 1 missing tag: {plan.Count(IssueKind.MissingPin)}/{plan.Count(IssueKind.MissingTag)}");
    Check(plan.Count(IssueKind.DrawingUnresolved) == 1 && plan.Count(IssueKind.WireNotFound) == 1 && plan.Issues.Any(i => i.Kind == IssueKind.DrawingUnresolved && i.Detail.Contains("index 9")), "drawing index 9 unresolved and its label reported as WireNotFound");
    Check(plan.Drawings.Count == 3 && plan.Drawings.All(d => d.SkipReason.Length == 0), "three project drawings planned (reference drawing excluded)");
    var e001 = plan.Drawings.Single(d => d.Name == dwgs[0]); var e002 = plan.Drawings.Single(d => d.Name == dwgs[1]);
    Check(e001.Count(LabelAction.Insert) == 3 && e002.Count(LabelAction.Insert) == 2 && plan.Writable == 5, "all five labels are inserts on first run");
    Check(e001.Changes.Any(c => c.Key == "2F3A/1" && c.Text == "K1:13/TB2:1") && e001.Changes.Any(c => c.Key == "2F3B/1" && c.Text == "TB2:1/CR1:A1"), "three-connection network gives two wires with their own identifiers");
    Check(e002.Changes.Any(c => c.Key == "2F70/1" && c.Text == "M1:T1/?:?"), "blank far end shows ?:?");
    Check(IdentifierService.Render(plan).Contains("Insert label") && IdentifierService.Render(plan).Contains("Validation:"), "render");

    // apply
    var res = RunService.Execute(plan, settings, bridge, true);
    Check(res.Unresolved.Count == 0 && res.Applied == 5 && res.Log.Any(l => l.StartsWith("Status: SUCCESS")), "run succeeded and verified: " + res.Log.Last(l => l.StartsWith("Status")));
    Check(Directory.Exists(res.BackupFolder) && File.Exists(Path.Combine(res.BackupFolder, dwgs[0])) && File.Exists(Path.Combine(res.BackupFolder, dwgs[1])) && !File.Exists(Path.Combine(res.BackupFolder, dwgs[2])), "only the drawings that change were backed up");
    Check(File.Exists(res.LabelCsv) && File.Exists(res.ReportCsv) && File.Exists(res.ValidationPath) && File.Exists(Path.Combine(projectFolder, "LSE_ToFromReport.txt")), "CSV, report CSV, validation and run log written");
    var labelLines = File.ReadAllLines(res.LabelCsv);
    Check(labelLines.Length == 13 && labelLines[0] == string.Join(",", Export.LabelColumns) && labelLines.Any(l => l.Contains(",101,2,TB2:1/K1:13,TB2,1,K1,13,2F3A,1A10,2F3A/2")), "label CSV: header + 12 rows, far end reversed: " + labelLines.FirstOrDefault(l => l.Contains("TB2:1/K1:13")));
    var reportLines = File.ReadAllLines(res.ReportCsv);
    Check(reportLines.Length == 7 && reportLines[0] == string.Join(",", Export.ReportColumns) && reportLines[1] == "101,K1,13,TB2,1,2F3A,2F3A,1A00,1A10,1,1", "report CSV matches the native rows column for column");
    byte[] bom = File.ReadAllBytes(res.LabelCsv);
    Check(bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF, "label CSV written with a UTF-8 BOM");
    Check(File.ReadAllText(res.ValidationPath).Contains("MissingPin (2)") && File.ReadAllText(res.ValidationPath).Contains("has no pin number"), "validation report lists the missing pins");

    // run twice with no edits: nothing to write, no duplicates
    plan = Plan();
    Check(plan.Writable == 0 && plan.AllChanges.Count(c => c.Action == LabelAction.Keep) == 5 && fakeLabels.Values.Sum(l => l.Count) == 5, "second run with no edits: 5 keep, nothing to write, still 5 labels");
    Check(IdentifierService.NothingToDo(plan).Contains("up to date"), "nothing-to-do message");

    // retag K1 to K5: labels update in place, no K1 text remains
    File.WriteAllText(reportFile, File.ReadAllText(reportFile, Encoding.Latin1).Replace("101,K1,13,TB2,1", "101,K5,13,TB2,1"), Encoding.Latin1);
    plan = Plan();
    var upd = plan.AllChanges.Single(c => c.Action == LabelAction.Update);
    Check(upd.Key == "2F3A/1" && upd.Text == "K5:13/TB2:1" && upd.Existing == "K1:13/TB2:1" && plan.Writable == 1, "retag plans one in-place update: " + upd.Text);
    res = RunService.Execute(plan, settings, bridge, false);
    Check(res.Applied == 1 && res.Unresolved.Count == 0 && !fakeLabels.Values.SelectMany(l => l).Any(l => l.Text.Contains("K1:")) && fakeLabels.Values.Sum(l => l.Count) == 5 && res.LabelCsv.Length == 0, "after the update no K1 text remains, still 5 labels, no CSV when export is off");

    // wire removed: its label is stale and deleted; duplicate label with the same key removed
    File.WriteAllText(reportFile, string.Join("\r\n", File.ReadAllLines(reportFile, Encoding.Latin1).Where(l => !l.StartsWith("102,"))) + "\r\n", Encoding.Latin1);
    fakeLabels[Path.Combine(projectFolder, dwgs[0])].Add(new ExistingLabel { Handle = "DUP1", Key = "2F3A/1", Text = "K5:13/TB2:1", Block = "TOFROM_LBL_H", Layer = "LSE_TOFROM" });
    plan = Plan();
    var dels = plan.AllChanges.Where(c => c.Action == LabelAction.Delete).ToList();
    Check(dels.Count == 2 && dels.Any(d => d.Key == "2F40/1" && d.Note.Contains("stale")) && dels.Any(d => d.ExistingHandle == "DUP1" && d.Note.Contains("duplicate")) && plan.Count(IssueKind.StaleLabel) == 1, "stale label and duplicate label planned for deletion");
    res = RunService.Execute(plan, settings, bridge, false);
    Check(res.Unresolved.Count == 0 && fakeLabels.Values.Sum(l => l.Count) == 4, "deletions verified; 4 labels remain");

    // deleteStale off keeps the stale label as a skip
    fakeLabels[Path.Combine(projectFolder, dwgs[0])].Add(new ExistingLabel { Handle = "OLD1", Key = "9999/1", Text = "OLD", Block = "TOFROM_LBL_H", Layer = "LSE_TOFROM" });
    config.Schematic.DeleteStale = false;
    plan = Plan();
    Check(plan.AllChanges.Any(c => c.Key == "9999/1" && c.Action == LabelAction.Skip && !c.Selected) && plan.Writable == 0, "deleteStale=false lists the stale label as a skip");
    config.Schematic.DeleteStale = true;
    fakeLabels[Path.Combine(projectFolder, dwgs[0])].RemoveAll(l => l.Handle == "OLD1");

    // both ends, repositionOnRerun
    config.Schematic.LabelAt = "both";
    plan = Plan();
    Check(plan.SchematicEnds.Count == 8 && plan.AllChanges.Count(c => c.Action == LabelAction.Insert) == 4 && plan.AllChanges.Any(c => c.Key == "2F3A/2" && c.Text == "K5:13/TB2:1"), "labelAt=both: a second label per wire with the same (not reversed) text");
    config.Schematic.LabelAt = "from";
    config.Schematic.RepositionOnRerun = true;
    plan = Plan();
    Check(plan.AllChanges.Count(c => c.Action == LabelAction.Update) == 4 && plan.AllChanges.All(c => c.Action != LabelAction.Keep), "repositionOnRerun turns keeps into updates");
    config.Schematic.RepositionOnRerun = false;

    // locked and open drawings, current-drawing scope
    lockedDrawings.Add(Path.Combine(projectFolder, dwgs[1]));
    openDrawings.Add(Path.Combine(projectFolder, dwgs[0]));
    File.WriteAllText(reportFile, File.ReadAllText(reportFile, Encoding.Latin1).Replace("103,S1,3,K5,A2", "103,S1,4,K5,A2").Replace("104,M1,T1,,,", "104,M1,T2,,,"), Encoding.Latin1);
    plan = Plan();
    var lockedPlan = plan.Drawings.Single(d => d.Name == dwgs[1]);
    Check(lockedPlan.SkipReason.Contains("locked") && lockedPlan.Changes.All(c => c.Action == LabelAction.Skip && !c.Selected) && plan.Count(IssueKind.DrawingSkipped) == 1, "locked drawing skipped and reported, its label not written");
    Check(plan.Drawings.Single(d => d.Name == dwgs[0]).State.OpenInEditor && plan.Writable == 1, "open drawing planned; one update in it");
    res = RunService.Execute(plan, settings, bridge, false);
    Check(res.BackupFolder.Length == 0 && res.Log.Any(l => l.Contains("Open in the editor")) && res.Applied == 1, "open drawing: no backup folder, session note, written");
    lockedDrawings.Clear();
    plan = Plan(true, Path.Combine(projectFolder, dwgs[1]));
    Check(plan.Drawings.Count == 1 && plan.Drawings[0].Name == dwgs[1] && plan.Writable == 1 && plan.CsvEnds.Count == 10, "current drawing only: one drawing planned, CSV still covers the project");
    res = RunService.Execute(plan, settings, bridge, false);
    Check(res.Applied == 1 && res.Unresolved.Count == 0, "scoped run written");
    openDrawings.Clear();

    // layer conflict
    wireNoLayerOf = "LSE_TOFROM";
    plan = Plan();
    Check(plan.Drawings.All(d => d.SkipReason.Contains("wire-number layer")) && plan.Count(IssueKind.LayerConflict) == 3, "label layer equal to WIRENO_LAY: every drawing refused");
    wireNoLayerOf = "WIRENO";
    wireLayers = new() { "LSE_*" };
    plan = Plan();
    Check(plan.Count(IssueKind.LayerConflict) == 3 && plan.Issues[plan.Issues.Count - 1].Detail.Contains("WIRELAYS"), "label layer matching a WIRELAYS pattern: refused");
    wireLayers = new() { "WIRES" };

    // no annotation: export only
    plan = Plan(false);
    Check(plan.Drawings.Count == 0 && plan.Log.Any(l => l.Contains("not requested")), "annotation off: no drawings read");
    res = RunService.Execute(plan, settings, bridge, true);
    Check(res.Applied == 0 && File.Exists(res.LabelCsv) && res.Log.Any(l => l.StartsWith("Status: SUCCESS")), "export-only run writes the CSVs");

    // rollback
    File.WriteAllText(reportFile, File.ReadAllText(reportFile, Encoding.Latin1).Replace("103,S1,4,K5,A2", "103,S1,5,K5,A2"), Encoding.Latin1);
    plan = Plan();
    string target = Path.Combine(projectFolder, dwgs[0]);
    byte[] before = File.ReadAllBytes(target);
    string labelsCsv = Path.Combine(projectFolder, "LSE_ToFrom", "123456-R_tofrom_labels.csv");
    byte[] csvBefore = File.ReadAllBytes(labelsCsv);
    failNextWrite = true;
    Throws(() => RunService.Execute(plan, settings, bridge, true), "simulated drawing failure", "run fails on a drawing error");
    failNextWrite = false;
    Check(File.ReadAllBytes(target).AsSpan().SequenceEqual(before) && File.ReadAllBytes(labelsCsv).AsSpan().SequenceEqual(csvBefore), "rollback restored the drawing and left the previous CSV untouched");
    Check(File.ReadAllText(Path.Combine(projectFolder, "LSE_ToFromReport.txt")).Contains("Status: FAILED (rolled back)"), "failed run appended to the project report");
    // a run that fails after the drawings were written (the label CSV is open in another program): drawings restored, previous CSV kept
    byte[] drawingBefore = File.ReadAllBytes(target);
    using (var holder = new FileStream(labelsCsv, FileMode.Open, FileAccess.Read, FileShare.None))
    {
        Throws(() => RunService.Execute(plan, settings, bridge, true), "being used by another process", "a locked label CSV fails the run");
    }

    Check(File.ReadAllBytes(target).AsSpan().SequenceEqual(drawingBefore) && File.ReadAllBytes(labelsCsv).AsSpan().SequenceEqual(csvBefore) && !File.Exists(labelsCsv + ".tmp"), "after the locked-CSV failure: drawing rolled back, previous CSV intact, no .tmp left");
    Check(Directory.Exists(SettingsService.LogFolder) && Directory.GetFiles(SettingsService.LogFolder, "ToFromRun_*.txt").Length > 0, "failed run logged to the Logs folder");
    int backupsBefore = Directory.GetDirectories(Path.Combine(projectFolder, settings.BackupFolderName)).Length;
    File.SetAttributes(target, FileAttributes.ReadOnly);
    Throws(() => RunService.Execute(plan, settings, bridge, false), "Read-only", "read-only drawing refused");
    File.SetAttributes(target, FileAttributes.Normal);
    Check(Directory.GetDirectories(Path.Combine(projectFolder, settings.BackupFolderName)).Length == backupsBefore, "no backup made for the refused run");
    res = RunService.Execute(plan, settings, bridge, false);
    Check(Directory.GetDirectories(Path.Combine(projectFolder, settings.BackupFolderName)).Length == 1, "backups pruned to BackupsToKeep=1");

    // stale database detection
    string mdb = Path.Combine(projectFolder, "123456-R.mdb");
    File.WriteAllText(mdb, "x");
    File.SetLastWriteTime(mdb, DateTime.Now.AddHours(-1));
    project.DatabaseWritten = File.GetLastWriteTime(mdb);
    var staleIssues = new List<Issue>(); var staleNotes = new List<string>();
    DataSourceService.Stale(settings, project, staleIssues, staleNotes);
    Check(staleIssues.Count == 3 && staleIssues.All(i => i.Kind == IssueKind.StaleDatabase) && staleNotes.Any(n => n.StartsWith("WARNING")), "drawings newer than the database are listed with a warning");
    settings.StalePolicy = "stop";
    Throws(() => DataSourceService.Stale(settings, project, new List<Issue>(), new List<string>()), "stalePolicy is 'stop'", "stalePolicy=stop refuses");
    settings.StalePolicy = "ignore";
    var none = new List<Issue>(); DataSourceService.Stale(settings, project, none, new List<string>());
    Check(none.Count == 0, "stalePolicy=ignore reports nothing");
    settings.StalePolicy = "warn";
    settings.DataSource = "scratch";
    Throws(() => DataSourceService.Load(settings, config, project, "", null, new List<Issue>(), new List<string>(), out _), "LSETOFROMDISCOVER", "scratch source without wireSql points at discovery");
    settings.DataSource = "report";
}

// 6. CSV field quoting and encodings
{
    Check(Export.Field("K1:13/TB2:1", ",") == "K1:13/TB2:1" && Export.Field("a,b", ",") == "\"a,b\"" && Export.Field("say \"hi\"", ",") == "\"say \"\"hi\"\"\"" && Export.Field("a;b", ";") == "\"a;b\"" && Export.Field("a;b", ",") == "a;b", "CSV quoting only when needed");
    Check(Export.EncodingFor(new CsvConfig { Encoding = "ansi" }).CodePage == 28591 && Export.EncodingFor(new CsvConfig { Encoding = "utf8" }).GetPreamble().Length == 0 && Export.EncodingFor(new CsvConfig()).GetPreamble().Length == 3, "CSV encodings");
    var tabCfg = ConfigService.Load(configPath); tabCfg.Csv.Delimiter = "tab"; ConfigService.Validate(tabCfg, "x");
    Check(tabCfg.Csv.Delimiter == "\t", "delimiter 'tab' becomes a tab character");
}

// 7. Error log and help
{
    string logPath = ErrorService.Log("Plan", new InvalidOperationException("Sample failure for the smoke test."));
    string logText = logPath.Length > 0 && File.Exists(logPath) ? File.ReadAllText(logPath) : "";
    Check(logPath.StartsWith(SettingsService.LogFolder, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(logPath).StartsWith("Error_") && logText.Contains("Step: Plan") && logText.Contains("Sample failure"), "ErrorService writes Error_<stamp>.txt with step and exception");
    Check(HelpText.Main.Length > 800 && HelpText.Main.Contains("LSETOFROMDISCOVER") && HelpText.Fix(HelpText.Main).Contains("\r\n") && !HelpText.Fix(HelpText.Main).Replace("\r\n", "").Contains('\n'), "help text present with CRLF line endings");
    Check(ScratchDb.SuggestSql(new SchemaTable { Name = "WFROMTO" }) == "SELECT * FROM [WFROMTO]" && ScratchDb.Describe(new List<SchemaTable> { new() { Name = "T", Columns = { "CMP1", "CMP2", "WIRENO" }, Score = 3 } }, config.Database).Contains("CANDIDATE T"), "discovery text marks candidate tables");
}

// 8. Build tag, the plan grid on a real DataGridView, database search folders, settings upgrade
{
    Check(System.Text.RegularExpressions.Regex.IsMatch(BuildInfo.Release, @"^\d{4}-\d{2}-\d{2}-r\d+$") && BuildInfo.Title("x").EndsWith("[" + BuildInfo.Release + "]") && BuildInfo.Label.Contains(BuildInfo.Release), "build tag " + BuildInfo.Release + " in titles and label");
    string? informational = System.Diagnostics.FileVersionInfo.GetVersionInfo(typeof(BuildInfo).Assembly.Location).ProductVersion;
    Check(informational is not null && informational.Contains(BuildInfo.Release) && typeof(BuildInfo).Assembly.GetName().Version?.ToString() == "1.1.0.0", "assembly version 1.1.0 names the build tag: " + informational);
    string tagLog = ErrorService.Log("Grid", new InvalidOperationException("tag check"));
    Check(tagLog.Length > 0 && File.ReadAllText(tagLog).Contains("Build: " + BuildInfo.Label), "error log names the build");

    // one update (103 pin changed), one stale label to delete, keeps for the rest, one locked drawing with skip rows
    File.WriteAllText(reportFile, File.ReadAllText(reportFile, Encoding.Latin1).Replace("103,S1,5,K5,A2", "103,S1,6,K5,A2"), Encoding.Latin1);
    fakeLabels[Path.Combine(projectFolder, dwgs[0])].Add(new ExistingLabel { Handle = "OLD2", Key = "8888/1", Text = "OLD", Block = "TOFROM_LBL_H", Layer = "LSE_TOFROM" });
    lockedDrawings.Add(Path.Combine(projectFolder, dwgs[1]));
    var plan = Plan();
    Check(plan.AllChanges.Count(c => c.Action == LabelAction.Update) == 1 && plan.AllChanges.Count(c => c.Action == LabelAction.Delete) == 1 && plan.AllChanges.Any(c => c.Action == LabelAction.Keep) && plan.Drawings.Count(d => d.SkipReason.Length > 0) == 1, "grid fixture: update, delete, keeps and a locked drawing");

    using var grid = new DataGridView { AutoGenerateColumns = false, AllowUserToAddRows = false };
    GridService.AddColumns(grid);
    Check(grid.Columns.Count == 8 && grid.Columns[0] is DataGridViewCheckBoxColumn && grid.Columns[0].ReadOnly, "grid: 8 columns, Apply? first and read-only");
    int rows = GridService.Fill(grid, plan, false);
    int skippedDrawings = plan.Drawings.Count(d => d.SkipReason.Length > 0);
    Check(rows == plan.Writable + skippedDrawings && plan.Writable > 0 && grid.Rows.Cast<DataGridViewRow>().All(r => r.Tag is LabelChange { Writes: true } or DrawingPlan), "grid without unchanged rows lists the writable changes plus the skipped-drawing warning: " + rows);
    int all = GridService.Fill(grid, plan, true);
    int expected = plan.Drawings.Sum(d => d.Changes.Count) + plan.Drawings.Count(d => d.SkipReason.Length > 0);
    Check(all == expected && all > rows && grid.Rows.Count == all, "grid with unchanged rows lists every change plus one row per skipped drawing: " + all);
    DataGridViewRow updateRow = grid.Rows.Cast<DataGridViewRow>().First(r => r.Tag is LabelChange { Action: LabelAction.Update });
    var change = (LabelChange)updateRow.Tag!;
    bool wasSelected = change.Selected;
    Check(updateRow.Cells[0].Value is bool shown && shown == wasSelected && !updateRow.ReadOnly && updateRow.Cells[0].ReadOnly, "update row shows its tick, row writable, cell handled by the form");
    LabelChange? toggled = GridService.Toggle(grid, updateRow.Index);
    Check(toggled == change && change.Selected == !wasSelected && updateRow.Cells[0].Value is bool after && after == !wasSelected, "toggle flips Selected and the cell");
    GridService.Toggle(grid, updateRow.Index);
    Check(change.Selected == wasSelected && plan.Selected == plan.AllChanges.Count(c => c.Writes && c.Selected), "toggle again restores; plan.Selected follows");
    DataGridViewRow keepRow = grid.Rows.Cast<DataGridViewRow>().First(r => r.Tag is LabelChange { Action: LabelAction.Keep });
    Check(keepRow.ReadOnly && GridService.IsNoteRow(grid, keepRow.Index) && GridService.Toggle(grid, keepRow.Index) is null && keepRow.Cells[0].Value is false && keepRow.DefaultCellStyle.ForeColor == GridService.NoteForeColor, "keep row is a note: read-only, grey, refuses toggle");
    Check(GridService.Explain(grid, keepRow.Index).StartsWith("Keep:"), "keep row explanation: " + GridService.Explain(grid, keepRow.Index));
    DataGridViewRow skippedDrawingRow = grid.Rows.Cast<DataGridViewRow>().First(r => r.Tag is DrawingPlan);
    Check(skippedDrawingRow.ReadOnly && GridService.IsNoteRow(grid, skippedDrawingRow.Index) && GridService.Explain(grid, skippedDrawingRow.Index).Contains("locked") && skippedDrawingRow.DefaultCellStyle.BackColor == GridService.SkippedBackColor, "skipped drawing row is a note naming the lock");
    DataGridViewRow skipRow = grid.Rows.Cast<DataGridViewRow>().First(r => r.Tag is LabelChange { Action: LabelAction.Skip });
    Check(GridService.IsNoteRow(grid, skipRow.Index) && GridService.Toggle(grid, skipRow.Index) is null && GridService.Explain(grid, skipRow.Index).StartsWith("Skip:"), "skip row is a note");
    Check(!GridService.IsNoteRow(grid, -1) && GridService.Toggle(grid, grid.Rows.Count) is null && GridService.Explain(grid, 99) == "", "out of range refused");
    using (var bitmap = new Bitmap(60, 22))
    using (var graphics = Graphics.FromImage(bitmap))
    {
        DataGridViewCellPaintingEventArgs notePaint = PaintArgs(grid, graphics, keepRow.Index);
        GridService.PaintCell(grid, notePaint);
        DataGridViewCellPaintingEventArgs boxPaint = PaintArgs(grid, graphics, updateRow.Index);
        GridService.PaintCell(grid, boxPaint);
        Check(notePaint.Handled && !boxPaint.Handled, "note rows paint the word, change rows keep the box");
    }

    Check(grid.Rows.Cast<DataGridViewRow>().All(r => r.Cells[0].ReadOnly), "every Apply? cell is read-only: the grid's own edit cycle is never used");
    Check(GridService.Fill(grid, null, true) == 0 && grid.Rows.Count == 0, "null plan empties the grid");
    lockedDrawings.Clear();

    // comment-preserving config update (what discovery writes)
    string commented = Path.Combine(work, "commented.json");
    File.WriteAllText(commented, "{\n  // keep me\n  \"format\": \"{FROM}/{TO}\",\n  \"database\": {\n    \"wireSql\": \"\", // trailing note\n    \"confidence\": \"unverified\"\n  }\n}\n");
    Check(ConfigService.SetStringValues(commented, ("wireSql", "SELECT * FROM [T\"Q]"), ("confidence", "discovered 2026-11-13")), "SetStringValues finds both keys");
    string commentedText = File.ReadAllText(commented);
    var commentedConfig = ConfigService.Load(commented);
    Check(commentedText.Contains("// keep me") && commentedText.Contains("// trailing note") && commentedText.Contains("\"wireSql\"") && commentedConfig.Database.WireSql == "SELECT * FROM [T\"Q]" && commentedConfig.Database.Confidence == "discovered 2026-11-13", "comments kept, values replaced and escaped");
    Check(!ConfigService.SetStringValues(commented, ("nosuchkey", "x")) && !ConfigService.SetStringValues(Path.Combine(work, "none.json"), ("wireSql", "x")) && File.ReadAllText(commented) == commentedText, "a missing key or file returns false and changes nothing");

    // the word tab as the report delimiter; the provider fallback keeps Jet 4
    string tabbedWord = Path.Combine(work, "fromto_tabword.txt");
    File.WriteAllText(tabbedWord, "WIRENO\tCMP1\tPIN1\tCMP2\tPIN2\tWIREHDL1\r\n9\tA\t1\tB\t2\tFF\r\n");
    Check(ReportImport.Read(tabbedWord, new ReportFileConfig { Delimiter = "tab" }, new List<string>()).Single().Cmp2 == "B", "reportFile.delimiter 'tab' means a tab character");
    var noProviders = new ToFromConfig();
    noProviders.Database.Providers.Clear();
    ConfigService.Validate(noProviders, "x");
    Check(noProviders.Database.Providers.Count == 3 && noProviders.Database.Providers[2].Contains("Jet"), "empty providers fall back to ACE 16, ACE 12 and Jet 4");

    // the drawing's sheet number reaches the label CSV
    plan = Plan();
    RunResult sheetRun = RunService.Execute(plan, settings, bridge, true);
    string[] sheetLines = File.ReadAllLines(sheetRun.LabelCsv);
    Check(sheetLines.Length > 1 && sheetLines.Skip(1).Where(l => l.StartsWith(dwgs[0] + ",")).All(l => l.StartsWith(dwgs[0] + ",1,")) && sheetLines.Skip(1).Any(l => l.StartsWith(dwgs[2] + ",4,")), "label CSV Sheet column carries each drawing's sheet number");

    // database search folders
    var dcfg = new DatabaseConfig { PathPattern = Path.Combine(work, "nowhere", "{PROJECT}.mdb"), SearchFolders = new() { Path.Combine(work, "s1"), Path.Combine(work, "s2"), "{TEMP}" } };
    Directory.CreateDirectory(Path.Combine(work, "s1"));
    Directory.CreateDirectory(Path.Combine(work, "s2"));
    var searched = new List<string>();
    Check(ProjectService.DatabasePath(project, dcfg, "", searched) == Path.GetFullPath(Path.Combine(work, "nowhere", "123456-R.mdb")) && searched.Count == 4, "no database anywhere: pattern path returned, 4 paths looked at");
    File.WriteAllText(Path.Combine(work, "s1", "123456-R.mdb"), "a");
    File.WriteAllText(Path.Combine(work, "s2", "123456-R.mdb"), "b");
    File.SetLastWriteTime(Path.Combine(work, "s1", "123456-R.mdb"), DateTime.Now.AddMinutes(-5));
    Check(ProjectService.DatabasePath(project, dcfg, "") == Path.Combine(work, "s2", "123456-R.mdb"), "newest file among the search folders wins");
    Directory.CreateDirectory(Path.Combine(work, "nowhere"));
    File.WriteAllText(Path.Combine(work, "nowhere", "123456-R.mdb"), "p");
    Check(ProjectService.DatabasePath(project, dcfg, "") == Path.GetFullPath(Path.Combine(work, "nowhere", "123456-R.mdb")), "pattern path wins when it exists");
    Check(ProjectService.Expand("{TEMP}|{PROJECT}|{FOLDER}", project) == Path.GetTempPath().TrimEnd('\\') + "|123456-R|" + projectFolder, "token expansion");
    Check(ConfigService.Load(configPath).Database.SearchFolders.SequenceEqual(new[] { "{FOLDER}", "{TEMP}", "{LOCALAPPDATA}\\Temp" }), "config ships three search folders");

    // settings upgrade of the first cut's bridge expressions
    var legacy = new AppSettings { ActiveProjectLisp = AppSettings.LegacyActiveProjectLisp, ScratchDbLisp = AppSettings.LegacyScratchDbLisp };
    SettingsService.Upgrade(legacy);
    Check(legacy.ActiveProjectLisp == AppSettings.DefaultActiveProjectLisp && legacy.ScratchDbLisp == AppSettings.DefaultScratchDbLisp, "first-cut bridge expressions are upgraded to the multi-candidate defaults");
    var custom = new AppSettings { ActiveProjectLisp = "(setvar \"USERS1\" \"custom\")", ScratchDbLisp = "" };
    SettingsService.Upgrade(custom);
    Check(custom.ActiveProjectLisp == "(setvar \"USERS1\" \"custom\")" && custom.ScratchDbLisp == AppSettings.DefaultScratchDbLisp, "a customised expression is kept; a blank one gets the default");
    bool balanced = AppSettings.DefaultActiveProjectLisp.Count(ch => ch == '(') == AppSettings.DefaultActiveProjectLisp.Count(ch => ch == ')') && AppSettings.DefaultScratchDbLisp.Count(ch => ch == '(') == AppSettings.DefaultScratchDbLisp.Count(ch => ch == ')');
    Check(balanced && AppSettings.DefaultActiveProjectLisp.Contains("GBL_wd_prj") && AppSettings.DefaultActiveProjectLisp.Contains("boundp 'ace_getactiveproject") && AppSettings.DefaultScratchDbLisp.Contains("wd_mdb_get_proj_scratch_dbnam"), "default bridge expressions are balanced and name every candidate");
    string diag = DiagnosticsService.BuildReport();
    Check(diag.Contains("Build: " + BuildInfo.Release) && diag.Contains("not run (outside AutoCAD)") && diag.Contains("search folders"), "diagnostics name the build, the bridge state and the search folders");
}

Console.WriteLine();
Console.WriteLine($"{passed} passed, {failed} failed");
return failed == 0 ? 0 : 1;
