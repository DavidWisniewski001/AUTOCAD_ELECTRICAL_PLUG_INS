using LSE.AutoCADElectrical.ProjectEnvironment;
using System.Text;

// LSE_Env smoke test. Runs without AutoCAD: the drawing side is faked through HostBridge delegates.
int passed = 0, failed = 0;
void Check(bool ok, string name) { if (ok) { passed++; Console.WriteLine("PASS " + name); } else { failed++; Console.WriteLine("FAIL " + name); } }
void Throws(Action a, string contains, string name) { try { a(); Check(false, name + " (no exception)"); } catch (Exception ex) { Check(ex.Message.Contains(contains, StringComparison.OrdinalIgnoreCase), name + (ex.Message.Contains(contains, StringComparison.OrdinalIgnoreCase) ? "" : ": " + ex.Message)); } }

string root = args.Length > 0 ? args[0] : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
string templateWdp = Directory.GetFiles(Path.Combine(root, "..", "reference", "CUSTOM PROJECT TEMPLATE 2.0"), "*.wdp").Single();
string rulesPath = Path.Combine(root, "config", "LseEnvRules.json");
string work = Path.Combine(Path.GetTempPath(), "LSE_Env_smoke_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
Directory.CreateDirectory(work);
Console.WriteLine("Work folder: " + work);

// 1. Text file round trip
{
    string copy = Path.Combine(work, "roundtrip.wdp");
    File.Copy(templateWdp, copy);
    var doc = TextFileService.Read(copy);
    TextFileService.Write(doc);
    Check(File.ReadAllBytes(copy).AsSpan().SequenceEqual(File.ReadAllBytes(templateWdp)), "template .wdp round trip is byte-identical");
    Check(TextFileService.Get(doc, '?', 6) == "S" && TextFileService.Get(doc, '?', 9) == "%N", "template ?[6]=S ?[9]=%N");
    TextFileService.Set(doc, '?', 6, "R");
    TextFileService.Set(doc, '?', 13, "NEW");
    int i12 = TextFileService.Find(doc, '?', 12), i13 = TextFileService.Find(doc, '?', 13), i14 = TextFileService.Find(doc, '?', 14);
    Check(i12 >= 0 && i13 == i12 + 1 && i14 == i13 + 1, "new ?[13] inserted between ?[12] and ?[14]");
}

// 2. Rules
EnvRules rules = RulesService.Load(rulesPath);
{
    Check(rules.Settings.Count >= 15 && rules.WireSchemes.Count == 5, $"rules loaded: {rules.Settings.Count} settings, {rules.WireSchemes.Count} schemes");
    Check(rules.Map("wireMode")!.Property == 6 && rules.Map("wireMode")!.Wdm == "WIREMODE", "wireMode -> ?[6] + WIREMODE");
    Check(!rules.Map("referenceStyle")!.Mapped && rules.Map("referenceStyle")!.Where.Contains("not mapped"), "referenceStyle is not mapped");
    Check(rules.Scheme("From/To device")!.IsFromTo && rules.Scheme("LSE standard (template)")!.FromTemplate, "scheme kinds");
    Check(rules.Label('?', 6) == "?[6] Wire number mode" && rules.Label('+', 1).Contains("Schematic"), "labels from map and labels dictionary");
    Check(rules.Map("wireMode")!.Describe("R").Contains("Reference-based"), "choice description");
    string bad = Path.Combine(work, "bad.json");
    File.WriteAllText(bad, "{ \"settings\": [ { \"key\": \"a\", \"property\": 1 } ], \"wireSchemes\": [ { \"name\": \"x\", \"values\": { \"nope\": \"1\" } } ] }");
    Throws(() => RulesService.Load(bad), "does not define", "scheme with unknown key is refused");
    var defaults = RulesService.Load(Path.Combine(work, "missing.json"));
    Check(defaults.Settings.Count > 0 && defaults.Source.Contains("built-in"), "built-in defaults when the rules file is missing");
}

// 3. Profiles
var settings = new AppSettings { ProfilesFolder = Path.Combine(work, "Profiles"), TemplateProjectPath = templateWdp, BackupFolderName = "_LSE_ENV_BACKUP", BackupsToKeep = 1, RulesConfigPath = rulesPath };
{
    var p = new EnvironmentProfile { Name = "ACME: grid/2026", Customer = "ACME", Scheme = "Line reference (grid)", Values = { ["wireFormat"] = "%S-%N" }, Properties = { ["7"] = "500" }, FromTo = new FromToSettings { Format = "{FROM}>{TO}" } };
    string path = ProfileService.Save(settings, p);
    Check(File.Exists(path) && Path.GetFileName(path) == "ACME_ grid_2026.json", "profile saved under a sanitized file name: " + Path.GetFileName(path));
    var back = ProfileService.Read(path)!;
    Check(back.Name == p.Name && back.Values["wireformat"] == "%S-%N" && back.Properties["7"] == "500" && back.FromTo!.Format == "{FROM}>{TO}", "profile round trip (case-insensitive values)");
    Check(ProfileService.Load(settings).Count == 1, "profile list");
    string retired = ProfileService.Retire(settings, back);
    Check(!File.Exists(path) && File.Exists(retired) && ProfileService.Load(settings).Count == 0, "retire moves the file to _retired");
}

// 4. Wire network engine
var ft = rules.FromTo;
WireSegment Seg(double x1, double y1, double x2, double y2) => new() { A = new Pt(x1, y1), B = new Pt(x2, y2), Layer = "WIRES", Handle = Guid.NewGuid().ToString("N")[..8] };
DeviceTerminal Dev(string tag, double x, double y, string pin = "", bool strip = false) => new() { Tag = tag, Pin = pin, Point = new Pt(x, y), IsTerminalStrip = strip };
WireNumberMark Mark(double x, double y, string number, bool fixedMark = false) => new() { Handle = Guid.NewGuid().ToString("N")[..8], Point = new Pt(x, y), Number = number, IsFixed = fixedMark, Layer = fixedMark ? "WIREFIXED" : "WIRENO" };
{
    var d = new DrawingWires { Path = @"C:\x\A.dwg", Sheet = "1" };
    d.Segments.Add(Seg(0, 0, 5, 0)); d.Segments.Add(Seg(5, 0, 10, 0));              // touching, collinear
    d.Segments.Add(Seg(7, 0, 7, 4));                                                   // T-junction onto the first run
    d.Segments.Add(Seg(10.1, 0, 15, 0));                                               // gap 0.1 < gapTolerance
    d.Segments.Add(Seg(0, 2, 5, 2));                                                   // separate network
    d.Segments.Add(Seg(20, 0, 25, 0)); d.Segments.Add(Seg(25.5, 0, 30, 0));           // gap 0.5 > gapTolerance: two networks
    var nets = WireNetworkService.Build(d, ft);
    Check(nets.Count == 4, $"networks: 4 expected (touch, T, gap join, gap split), got {nets.Count}");
    var big = nets.OrderByDescending(n => n.Segments.Count).First();
    Check(big.Segments.Count == 4, "T-junction and small gap joined into one network of 4 segments");
    Check(Math.Abs(big.LabelPoint.Y) < 1e-9 && big.LabelRotation == 0, "label on a horizontal segment");
    Check(WireNetworkService.NaturalCompare("PB2", "PB10") < 0 && WireNetworkService.NaturalCompare("CR1", "cr1") == 0, "natural compare");

    d.Terminals.Add(Dev("PB1", 0, 0, "3")); d.Terminals.Add(Dev("CR1", 15, 0, "A1")); d.Terminals.Add(Dev("TB1", 7, 4, "5", true));
    d.Terminals.Add(Dev("LT1", 0, 2)); d.Terminals.Add(Dev("LT1", 5, 2));            // one device both ends
    d.Marks.Add(Mark(2.5, 0.1, "101")); d.Marks.Add(Mark(2.5, 2.1, "102"));
    nets = WireNetworkService.Build(d, ft);
    big = nets.OrderByDescending(n => n.Devices.Count).First();
    Check(big.Devices.Count == 3 && big.Marks.Count == 1 && big.Marks[0].Number == "101", "devices and mark attached to the network");
    var f = new FromToSettings();
    var plan = new WirePlan { Drawing = d.Path, Sheet = "1", Networks = nets };
    WireNetworkService.Assign(new List<WirePlan> { plan }, f, ft);
    Check(big.Proposed == "PB1-TB1" && big.Action == WireAction.Set && big.Note.Contains("first and last"), "FROM-TO left to right with the terminal strip last: " + big.Proposed);
    var single = nets.Single(n => n.Devices.Count == 1);
    Check(single.Action == WireAction.Keep && single.Note.Contains("only one"), "single device keeps the existing number");
    var empty = nets.Where(n => n.Devices.Count == 0).ToList();
    Check(empty.Count == 2 && empty.All(n => n.Action == WireAction.Keep), "networks without devices keep");

    f = new FromToSettings { IncludeTerminalPin = true, DevicePriority = { "^CR" }, TerminalsLast = false, Uppercase = false };
    WireNetworkService.Assign(new List<WirePlan> { plan }, f, ft);
    Check(big.Proposed == "CR1:A1-TB1:5", "priority regex + pins: " + big.Proposed);
    f = new FromToSettings { MultiDevice = "all", AllSeparator = "/" };
    WireNetworkService.Assign(new List<WirePlan> { plan }, f, ft);
    Check(big.Proposed == "PB1/CR1/TB1", "all devices joined: " + big.Proposed);
    f = new FromToSettings { SingleDevice = "device", NoDevice = "skip", MaxLength = 3 };
    WireNetworkService.Assign(new List<WirePlan> { plan }, f, ft);
    Check(single.Proposed == "LT1" && single.Action == WireAction.Set && big.Proposed == "PB1" && empty.All(n => n.Action == WireAction.Skip), "single=device, noDevice=skip, maxLength");

    // duplicates, cross-sheet signals, insert/keep/skip decisions
    var s1 = new DrawingWires { Path = @"C:\x\S1.dwg", Sheet = "1" }; s1.Segments.Add(Seg(0, 0, 10, 0)); s1.Terminals.Add(Dev("PB1", 0, 0)); s1.Signals.Add(new SignalArrow { Code = "SIG-A", Point = new Pt(10, 0) });
    s1.Segments.Add(Seg(0, 5, 10, 5)); s1.Terminals.Add(Dev("PB1", 0, 5)); s1.Terminals.Add(Dev("CR1", 10, 5)); s1.Marks.Add(Mark(5, 5.1, "PB1-CR1"));
    var s2 = new DrawingWires { Path = @"C:\x\S2.dwg", Sheet = "2" }; s2.Segments.Add(Seg(0, 0, 10, 0)); s2.Signals.Add(new SignalArrow { Code = "sig-a", Point = new Pt(0, 0) }); s2.Terminals.Add(Dev("CR1", 10, 0)); s2.Marks.Add(Mark(5, 0.1, "OLD", true));
    var p1 = new WirePlan { Drawing = s1.Path, Sheet = "1", Networks = WireNetworkService.Build(s1, ft) };
    var p2 = new WirePlan { Drawing = s2.Path, Sheet = "2", Networks = WireNetworkService.Build(s2, ft) };
    WireNetworkService.Assign(new List<WirePlan> { p1, p2 }, new FromToSettings(), ft);
    var viaSignal = p1.Networks.Single(n => n.SignalCodes.Count > 0);
    var direct = p1.Networks.Single(n => n.SignalCodes.Count == 0);
    Check(viaSignal.Logical == p2.Networks[0].Logical && viaSignal.Proposed == p2.Networks[0].Proposed, "cross-sheet networks share one number via SIGCODE (case-insensitive)");
    Check(direct.Proposed == "PB1-CR1" && direct.Action == WireAction.Keep, "existing matching number is kept");
    Check(viaSignal.Proposed == "PB1-CR1-2" && viaSignal.Action == WireAction.Insert, "duplicate gets the -2 suffix and a missing mark is inserted: " + viaSignal.Proposed);
    Check(p2.Networks[0].Action == WireAction.Set && p1.Fixed, "fixed mark overwritten when overwriteFixed is on");
    WireNetworkService.Assign(new List<WirePlan> { p1, p2 }, new FromToSettings { OverwriteFixed = false }, ft);
    Check(p2.Networks[0].Action == WireAction.Skip && p2.Networks[0].Note.Contains("fixed"), "fixed mark skipped when overwriteFixed is off");
    var noInsert = new FromToRules { InsertMissing = false };
    WireNetworkService.Assign(new List<WirePlan> { p1, p2 }, new FromToSettings(), noInsert);
    Check(viaSignal.Action == WireAction.Skip, "insertMissing=false skips networks without a mark");
    WireNetworkService.Assign(new List<WirePlan> { p1, p2 }, new FromToSettings(), ft);
    var after = new DrawingWires { Path = s2.Path, Sheet = "2" }; after.Segments.Add(Seg(0, 0, 10, 0)); after.Marks.Add(Mark(5, 0.1, "PB1-CR1-2", true));
    Check(WireNetworkService.Verify(p2, after, ft).Count == 0, "verify passes when the drawing reads the proposed number");
    after.Marks[0].Number = "WRONG";
    Check(WireNetworkService.Verify(p2, after, ft).Count == 1, "verify reports a wrong number");
    Check(WireNetworkService.Render(p1).Contains("PB1-CR1-2") && p1.Summary.Contains("1 insert"), "render and summary");
}

// 5. Planner and apply with fake drawings
string projectFolder = Path.Combine(work, "JOB");
Directory.CreateDirectory(projectFolder);
string wdp = Path.Combine(projectFolder, "123456-R.wdp");
{
    var lines = File.ReadAllLines(templateWdp, new UTF8Encoding(true)).Where(l => l.StartsWith("*[") || l.StartsWith("+[") || l.StartsWith("?[")).ToList();
    lines.Add("===Sheet one"); lines.Add("123456-R-E001.dwg"); lines.Add("===Sheet two"); lines.Add("123456-R-E002.dwg");
    File.WriteAllText(wdp, string.Join("\r\n", lines) + "\r\n", new UTF8Encoding(true));
    File.WriteAllText(Path.Combine(projectFolder, "123456-R-E001.dwg"), "fake");
    File.WriteAllText(Path.Combine(projectFolder, "123456-R-E002.dwg"), "fake");
}
var fakeWdm = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
foreach (string n in new[] { "123456-R-E001.dwg", "123456-R-E002.dwg" })
{
    fakeWdm[Path.Combine(projectFolder, n)] = new(StringComparer.OrdinalIgnoreCase) { ["WIREMODE"] = "S", ["WIREFMT"] = "%N", ["WIRE_START"] = "100", ["WIREINC"] = "1", ["WIRE_RSUF"] = ",A,B", ["TAGMODE"] = "S", ["WIRELAYS"] = "WIRES", ["WIRENO_LAY"] = "WIRENO", ["WIREFIXED_LAY"] = "WIREFIXED", ["SHEET"] = n.EndsWith("1.dwg") ? "1" : "2" };
}
fakeWdm[Path.Combine(projectFolder, "123456-R-E002.dwg")].Remove("WIREINC");
var fakeMarks = new Dictionary<string, (string Number, bool Fixed)>(StringComparer.OrdinalIgnoreCase);
var applied = new List<DrawingApplyRequest>();
bool failNextApply = false;
DrawingWires FakeWires(string path)
{
    var d = new DrawingWires { Path = path, Sheet = fakeWdm[path]["SHEET"] };
    d.Segments.Add(Seg(0, 0, 10, 0)); d.Terminals.Add(Dev("PB1", 0, 0)); d.Terminals.Add(Dev("CR1", 10, 0));
    if (fakeMarks.TryGetValue(path, out var mark)) d.Marks.Add(new WireNumberMark { Handle = "H" + Path.GetFileName(path), Point = new Pt(5, 0.1), Number = mark.Number, IsFixed = mark.Fixed, Layer = mark.Fixed ? "WIREFIXED" : "WIRENO" });
    return d;
}
var bridge = new HostBridge
{
    Inspect = path => new DrawingState { Path = path, Exists = true, HasWdm = true, WdmValues = new(fakeWdm[path], StringComparer.OrdinalIgnoreCase), WdmTags = fakeWdm[path].Keys.ToList() },
    ReadWires = FakeWires,
    ApplyDrawing = req =>
    {
        if (failNextApply) throw new InvalidOperationException("simulated drawing failure");
        applied.Add(req);
        foreach (var (k, v) in req.Attributes) fakeWdm[req.DrawingPath][k] = v;
        if (req.Wires is not null) fakeMarks[req.DrawingPath] = (req.Wires.Networks[0].Proposed, req.Wires.Fixed);
        foreach (string h in req.UnfixHandles) fakeMarks[req.DrawingPath] = (fakeMarks[req.DrawingPath].Number, false);
        return new List<string> { "fake write" };
    }
};
{
    var st = EnvironmentService.ReadProject(wdp, rules, bridge, true);
    Check(st.Drawings.Count == 2 && st.Drawings.All(d => d.Readable && d.HasWdm) && st.Properties[6] == "S", "project read with fake drawings");

    var grid = rules.Scheme("Line reference (grid)")!;
    var plan = EnvironmentService.Plan(st, null, grid, rules, settings, bridge, true, true);
    Check(plan.Changes.Any(c => c.Kind == ChangeKind.SetProperty && c.Index == 6 && c.Value == "R"), "grid: ?[6] S -> R planned");
    Check(plan.Changes.Count(c => c.Kind == ChangeKind.SetWdm && c.Name == "WIREMODE") == 2, "grid: WIREMODE planned in both drawings");
    Check(plan.Changes.Any(c => c.Kind == ChangeKind.Info && c.Item.Contains("Format referencing") && c.Note.Contains("LSEENVSNAPSHOT")), "grid: referenceStyle reported as not mapped");
    Check(plan.Changes.Any(c => c.Kind == ChangeKind.Marker) && plan.FromTo is null && !plan.Changes.Any(c => c.Kind == ChangeKind.UnfixWireNumbers), "grid: marker planned, no From/To, nothing to un-fix yet");
    Check(!plan.Changes.Any(c => c.Kind == ChangeKind.SetProperty && c.Index == 9), "grid: ?[9] already %N, no change");

    var openBridge = new HostBridge
    {
        Inspect = path => { var d = bridge.Inspect!(path); d.OpenInEditor = true; d.ReadFromEditor = true; return d; },
        ReadWires = bridge.ReadWires,
        ApplyDrawing = bridge.ApplyDrawing
    };
    var openState = EnvironmentService.ReadProject(wdp, rules, openBridge, true);
    Check(openState.Drawings.All(d => d.OpenInEditor && d.Readable && d.HasWdm), "open drawing read without error is Readable");
    var openPlan = EnvironmentService.Plan(openState, null, grid, rules, settings, openBridge, true, true);
    Check(openPlan.Changes.Count(c => c.Kind == ChangeKind.SetWdm && c.Name == "WIREMODE") == 2 && openPlan.Changes.Where(c => c.Kind == ChangeKind.SetWdm).All(c => c.Note.Contains("written in the session, not saved")), "open drawings: SetWdm planned with the session note");
    Check(!openPlan.Changes.Any(c => c.Kind == ChangeKind.Info && c.Note.Contains("close it and plan again")), "open drawings: no 'close it' notes");
    var openFailed = new DrawingState { Path = "x.dwg", Exists = true, OpenInEditor = true, Error = "boom" };
    Check(!openFailed.Readable, "open drawing with a read error is not Readable");
    Check(new Change().KindText == "(note)" && EnvironmentService.NothingToApply(new EnvPlan { Project = openState, Rules = rules }).Contains("Nothing to apply"), "note kind text and nothing-to-apply summary");

    var profile = new EnvironmentProfile { Name = "ACME", Customer = "ACME", Scheme = "From/To device", Values = { ["wireStart"] = "500", ["referenceStyle"] = "XY" }, Properties = { ["7"] = "600", ["x"] = "1" }, Options = { ["1"] = "C:\\nope" }, Drawing = { ["WIREINC"] = "2" } };
    plan = EnvironmentService.Plan(st, profile, null, rules, settings, bridge, true, true);
    Check(plan.Scheme is not null && plan.Scheme.IsFromTo && plan.FromTo is not null, "profile resolves its scheme and From/To settings");
    Check(plan.Changes.Single(c => c.Kind == ChangeKind.SetProperty && c.Index == 7).Value == "600", "raw property override wins over the profile value");
    Check(plan.Changes.Any(c => c.Kind == ChangeKind.Info && c.Item.Contains("+[1]") && c.Note.Contains("Protected")), "protected +[1] is not changed by a profile");
    Check(plan.Changes.Any(c => c.Kind == ChangeKind.Info && c.Item == "x"), "non-numeric raw key reported");
    Check(plan.Changes.Count(c => c.Kind == ChangeKind.SetWdm && c.Name == "WIREINC") == 1 && plan.Changes.Any(c => c.Kind == ChangeKind.Info && c.Item.EndsWith("WIREINC") && c.Note.Contains("Update to New WD_M")), "WD_M override: one drawing changed, the older block reported");
    Check(plan.Changes.Count(c => c.Kind == ChangeKind.WireNumbers) == 2 && plan.Changes.Where(c => c.Kind == ChangeKind.WireNumbers).All(c => c.Wires!.Networks[0].Proposed == "PB1-CR1" || c.Wires!.Networks[0].Proposed == "PB1-CR1-2"), "From/To planned for both drawings");
    Check(EnvironmentService.Render(plan).Contains("From/To {FROM}-{TO}"), "plan report mentions the From/To format");

    // apply
    var res = ApplyService.Execute(plan, settings, bridge);
    Check(res.Unresolved.Count == 0 && res.Log.Any(l => l.StartsWith("Status: SUCCESS")), "apply succeeded and verified");
    Check(Directory.Exists(res.BackupFolder) && File.Exists(Path.Combine(res.BackupFolder, "123456-R.wdp")), "backup holds the .wdp");
    var afterDoc = TextFileService.Read(wdp);
    Check(TextFileService.Get(afterDoc, '?', 7) == "600" && TextFileService.Get(afterDoc, '?', 6) == "S", "wdp updated (?[7]=600, ?[6] untouched)");
    Check(applied.Count == 2 && applied.Any(r => r.Attributes.ContainsKey("WIREINC")) && applied.All(r => r.Wires is not null), "both drawings written once with attributes and wire plans");
    var marker = MarkerService.Read(wdp)!;
    Check(marker.Profile == "ACME" && marker.FromTo && marker.Properties.Contains(7), "marker records profile, From/To and changed lines");
    Check(File.Exists(Path.Combine(projectFolder, "LSE_EnvReport.txt")), "report appended to the project folder");

    // re-plan: nothing left except notes; switching back to grid now un-fixes
    st = EnvironmentService.ReadProject(wdp, rules, bridge, true);
    plan = EnvironmentService.Plan(st, profile, null, rules, settings, bridge, true, true);
    Check(plan.Applicable == 0, "re-plan after apply finds nothing to change");
    plan = EnvironmentService.Plan(st, null, grid, rules, settings, bridge, true, true);
    var unfix = plan.Changes.Where(c => c.Kind == ChangeKind.UnfixWireNumbers).ToList();
    Check(unfix.Count == 2 && unfix.All(c => c.Recommended && c.Handles.Count == 1), "switching back to grid plans un-fixing the From/To numbers (recommended)");
    applied.Clear();
    res = ApplyService.Execute(plan, settings, bridge);
    Check(res.Unresolved.Count == 0 && applied.All(r => r.UnfixHandles.Count == 1) && TextFileService.Get(TextFileService.Read(wdp), '?', 6) == "R", "grid applied: un-fixed and ?[6]=R");
    Check(Directory.GetDirectories(Path.Combine(projectFolder, settings.BackupFolderName)).Length == 1, "backups pruned to BackupsToKeep=1");

    // template scheme: values from the template, so ?[6] goes back to S
    st = EnvironmentService.ReadProject(wdp, rules, bridge, true);
    plan = EnvironmentService.Plan(st, null, rules.Scheme("LSE standard (template)"), rules, settings, bridge, false, false);
    Check(plan.Resolved["wireMode"] == "S" && plan.Resolved["wireStart"] == "100" && plan.Changes.Any(c => c.Kind == ChangeKind.SetProperty && c.Index == 6 && c.Value == "S"), "template scheme reads the LSE template values");
    Check(!plan.Changes.Any(c => c.Kind == ChangeKind.SetWdm) && plan.Log.Any(l => l.Contains("not requested")), "drawings untouched when not requested");

    // rollback
    byte[] before = File.ReadAllBytes(wdp);
    plan = EnvironmentService.Plan(st, profile, null, rules, settings, bridge, true, true);
    failNextApply = true;
    Throws(() => ApplyService.Execute(plan, settings, bridge), "simulated drawing failure", "apply fails on a drawing error");
    failNextApply = false;
    Check(File.ReadAllBytes(wdp).AsSpan().SequenceEqual(before), "rollback restored the .wdp byte for byte");
    Check(MarkerService.Read(wdp)!.Scheme.Contains("grid"), "rollback restored the previous marker");

    // read-only refusal before any backup
    string backups = Path.Combine(projectFolder, settings.BackupFolderName);
    int backupsBefore = Directory.GetDirectories(backups).Length;
    File.SetAttributes(wdp, FileAttributes.ReadOnly);
    Throws(() => ApplyService.Execute(plan, settings, bridge), "Read-only", "read-only project refused");
    File.SetAttributes(wdp, FileAttributes.Normal);
    Check(Directory.GetDirectories(backups).Length == backupsBefore, "no backup made for the refused run");
    string markerFile = MarkerService.PathFor(wdp);
    File.SetAttributes(wdp, FileAttributes.ReadOnly);
    File.SetAttributes(markerFile, FileAttributes.ReadOnly);
    string refusal = "";
    try { ApplyService.Execute(plan, settings, bridge); } catch (InvalidOperationException ex) { refusal = ex.Message; }
    File.SetAttributes(wdp, FileAttributes.Normal);
    File.SetAttributes(markerFile, FileAttributes.Normal);
    Check(refusal.Contains(wdp) && refusal.Contains(markerFile) && refusal.TrimEnd().EndsWith("Nothing was changed."), "two read-only targets are both named in one refusal that ends with Nothing was changed.");
    Check(Directory.GetDirectories(backups).Length == backupsBefore, "no backup folder created for the two-file refusal");
    Throws(() => ApplyService.Execute(new EnvPlan { Project = st }, settings, bridge), "Nothing is ticked", "empty plan refused");

    // capture, snapshot, diff
    st = EnvironmentService.ReadProject(wdp, rules, bridge, true);
    var captured = EnvironmentService.Capture(st, rules, "cap");
    Check(captured.Values["wireMode"] == "R" && captured.Values["tagFormat"] == "%F%N" && !captured.Values.ContainsKey("wireLayers"), "capture takes mapped, unprotected values from the project");
    var a = EnvironmentService.Take(wdp, rules, bridge, true);
    var doc = TextFileService.Read(wdp); TextFileService.Set(doc, '?', 9, "%S%N"); TextFileService.Write(doc);
    fakeWdm[Path.Combine(projectFolder, "123456-R-E001.dwg")]["WIREFMT"] = "%S%N";
    var b = EnvironmentService.Take(wdp, rules, bridge, true);
    var diff = EnvironmentService.Diff(a, b, rules);
    Check(diff.Count == 2 && diff[0].Contains("?[9] (wireFormat)") && diff[1].Contains("WIREFMT (wireFormat)"), "snapshot diff names the moved line and attribute with their map keys");
}

// error log
{
    string logPath = ErrorService.Log("Plan", new InvalidOperationException("Sample failure for the smoke test."));
    string logText = logPath.Length > 0 && File.Exists(logPath) ? File.ReadAllText(logPath) : "";
    Check(logPath.StartsWith(SettingsService.LogFolder, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(logPath).StartsWith("Error_") && logPath.EndsWith(".txt"), "ErrorService writes Error_<stamp>.txt into the Logs folder");
    Check(logText.Contains("Step: Plan") && logText.Contains("Time: ") && logText.Contains("User: ") && logText.Contains("Machine: ") && logText.Contains("InvalidOperationException") && logText.Contains("Sample failure"), "error log carries step, time, user, machine and the full exception");
}

// plan grid (the r3 crash: "ReadOnly property of a cell cannot be set before it is added to a row")
{
    var gridPlan = new EnvPlan();
    gridPlan.Changes.Add(new Change { Kind = ChangeKind.SetProperty, Scope = "Project", Item = "?[6]", Current = "S", New = "R", Selected = true, Recommended = true });
    gridPlan.Changes.Add(new Change { Kind = ChangeKind.CopyFile, Scope = "Project", Item = "x.wdt", Selected = false, Recommended = false });
    gridPlan.Changes.Add(new Change { Kind = ChangeKind.Info, Scope = "Drawing", Item = "E001.dwg", Note = "open in the editor" });
    gridPlan.Changes.Add(new Change { Kind = ChangeKind.Info, Scope = "Drawing", Item = "E002.dwg", Note = "already matches" });
    try
    {
        using var grid = new System.Windows.Forms.DataGridView { AutoGenerateColumns = false, AllowUserToAddRows = false, AutoSizeRowsMode = System.Windows.Forms.DataGridViewAutoSizeRowsMode.AllCells };
        PlanGridService.AddColumns(grid);
        int rows = PlanGridService.Fill(grid, gridPlan, true);
        Check(rows == 4 && grid.Rows.Count == 4, "grid: 4 rows with notes shown");
        Check(grid.Rows[0].Cells[0] is System.Windows.Forms.DataGridViewCheckBoxCell && grid.Rows[0].Cells[0].Value is true && !grid.Rows[0].ReadOnly, "grid: applicable row has a ticked check box and is editable");
        Check(grid.Rows[1].Cells[0].Value is false && grid.Rows[1].DefaultCellStyle.BackColor == PlanGridService.NotRecommendedBackColor, "grid: not-recommended row is unticked and tinted");
        Check(grid.Rows[2].ReadOnly && grid.Rows[2].Cells[0].ReadOnly && PlanGridService.IsNoteRow(grid, 2) && (string)grid.Rows[2].Cells[5].Value! == PlanGridService.NoteText, "grid: note row is read-only and labelled note");
        Check(!PlanGridService.IsNoteRow(grid, 0) && !PlanGridService.IsNoteRow(grid, 99), "grid: IsNoteRow");
        rows = PlanGridService.Fill(grid, gridPlan, false);
        Check(rows == 2 && grid.Rows.Count == 2 && grid.Rows.Cast<System.Windows.Forms.DataGridViewRow>().All(r => r.Tag is Change c && c.Applies), "grid: notes hidden leaves the 2 applicable rows");
        rows = PlanGridService.Fill(grid, null, true);
        Check(rows == 0 && grid.Rows.Count == 0, "grid: null plan clears the grid");
        rows = PlanGridService.Fill(grid, gridPlan, true);
        Check(rows == 4, "grid: refill after clear");
    }
    catch (Exception ex)
    {
        Check(false, "grid fill threw: " + ex.Message);
    }
}

// build label
Check(BuildInfo.Release == "2026-11-12-r7" && BuildInfo.Title("x").Contains(BuildInfo.Release) && BuildInfo.Label.StartsWith("LSE_Env "), "build label is r7 and appears in window titles");
Check(DiagnosticsService.BuildReport().Contains("Build: " + BuildInfo.Release), "diagnostics report names the build");
{
    string logPath = ErrorService.Log("Build check", new InvalidOperationException("Sample failure for the smoke test."));
    Check(logPath.Length > 0 && File.ReadAllText(logPath).Contains("Build: " + BuildInfo.Label), "error log names the build");
}

// help texts
Check(HelpText.Profiles.Length > 500 && HelpText.Environment.Length > 500 && HelpText.WireScheme.Length > 500, "help texts are non-empty");
Check(HelpText.Profiles.Contains("{FROM}") && HelpText.Profiles.Contains("LSEENV") && HelpText.Environment.Contains("LSEENV") && HelpText.WireScheme.Contains("LSEENVWIRES"), "help texts mention LSEENV and {FROM}");
Check(HelpText.Fix(HelpText.Profiles).Contains("\r\n") && !HelpText.Fix(HelpText.Profiles).Replace("\r\n", "").Contains('\n'), "help text line endings are CRLF for the TextBox");

Console.WriteLine();
Console.WriteLine($"{passed} passed, {failed} failed");
return failed == 0 ? 0 : 1;
