using System.Drawing;
using System.Windows.Forms;
using LSE.AutoCADElectrical.ProjectDoctor;

int passed = 0;
int failed = 0;

void Check(string name, bool ok, string detail = "")
{
    if (ok)
    {
        passed++;
        Console.WriteLine("PASS  " + name);
    }
    else
    {
        failed++;
        Console.WriteLine("FAIL  " + name + (detail.Length == 0 ? "" : "  (" + detail + ")"));
    }
}

Finding Make(string item, Severity severity, RepairAction action, bool recommended) => new()
{
    Category = "Test",
    Item = item,
    Current = "cur " + item,
    Standard = "std " + item,
    Note = "note for " + item,
    Severity = severity,
    Action = action,
    Recommended = recommended,
    Selected = recommended
};

DataGridViewCellPaintingEventArgs PaintArgs(DataGridView grid, Graphics g, int rowIndex)
{
    var bounds = new Rectangle(0, 0, 44, 22);
    return new DataGridViewCellPaintingEventArgs(grid, g, bounds, bounds, rowIndex, 0, DataGridViewElementStates.Visible, false, false, "", grid.Rows[rowIndex].Cells[0].InheritedStyle, new DataGridViewAdvancedBorderStyle(), DataGridViewPaintParts.All);
}

// ---- build tag and help texts
Check("build tag in title", BuildInfo.Title("x").Contains("[" + BuildInfo.Release + "]"));
Check("build tag is a release tag", BuildInfo.Release.StartsWith("2026-") && BuildInfo.Release.Contains("-r"));
Check("help: doctor text", HelpText.Doctor.Contains("Why can I not tick this row?") && HelpText.Doctor.Contains("Repair ticked"));
Check("help: scan text", HelpText.Scan.Contains("LSEDOCTORSCAN") && HelpText.Scan.Contains("review"));
Check("help: settings text", HelpText.Settings.Contains("Template project") && HelpText.Settings.Contains("settings.json"));
Check("help: findings text", HelpText.Findings.Contains("Wire layers") && HelpText.Findings.Contains("DEFPOINTS"));
Check("help: All carries every part", HelpText.All.Contains(HelpText.Fix(HelpText.Doctor)) && HelpText.All.Contains(HelpText.Fix(HelpText.Scan)) && HelpText.All.Contains(HelpText.Fix(HelpText.Settings)) && HelpText.All.Contains(HelpText.Fix(HelpText.Findings)));
Check("help: Fix normalises line endings", HelpText.Fix("a\nb\r\nc") == "a\r\nb\r\nc" && !HelpText.Fix(HelpText.Doctor).Contains("\r\r"));
Check("help: no doubled quotes left from verbatim escaping", !HelpText.Doctor.Contains("\"\"") && !HelpText.Scan.Contains("\"\"") && !HelpText.Settings.Contains("\"\"") && !HelpText.Findings.Contains("\"\""));

// ---- findings grid
var diagnosis = new Diagnosis { WdpPath = @"C:\nowhere\JOB.wdp" };
diagnosis.Findings.Add(Make("missing line", Severity.Error, RepairAction.Add, true));
diagnosis.Findings.Add(Make("different value", Severity.Warning, RepairAction.Replace, false));
diagnosis.Findings.Add(Make("extra line", Severity.Info, RepairAction.None, false));
diagnosis.Findings.Add(Make("no title block", Severity.Warning, RepairAction.None, false));
diagnosis.OpenDrawings = 2;
Check("diagnosis: OpenDrawings property", diagnosis.OpenDrawings == 2);
Check("diagnosis: recommended and selected counts", diagnosis.Recommended == 1 && diagnosis.Selected == 1);

using (var grid = new DataGridView { AutoGenerateColumns = false, AllowUserToAddRows = false })
{
    GridService.AddFindingColumns(grid);
    Check("findings grid: 7 columns, Fix first and read-only", grid.Columns.Count == 7 && grid.Columns[0] is DataGridViewCheckBoxColumn && grid.Columns[0].ReadOnly);
    int rows = GridService.FillFindings(grid, diagnosis, true);
    Check("findings grid: every finding shown", rows == 4 && grid.Rows.Count == 4);
    Check("findings grid: recommended row ticked", grid.Rows[0].Cells[0].Value is true && grid.Rows[0].Tag == diagnosis.Findings[0]);
    Check("findings grid: judgement row unticked", grid.Rows[1].Cells[0].Value is false);
    Check("findings grid: note rows keep a check-box cell", grid.Rows[2].Cells[0] is DataGridViewCheckBoxCell && grid.Rows[3].Cells[0] is DataGridViewCheckBoxCell);
    Check("findings grid: note rows are read-only, grey", grid.Rows[2].ReadOnly && grid.Rows[3].ReadOnly && grid.Rows[2].DefaultCellStyle.ForeColor == GridService.NoteForeColor && !grid.Rows[0].ReadOnly);
    Check("findings grid: IsNoteRow", !GridService.IsNoteRow(grid, 0) && !GridService.IsNoteRow(grid, 1) && GridService.IsNoteRow(grid, 2) && GridService.IsNoteRow(grid, 3) && !GridService.IsNoteRow(grid, 9));
    Check("findings grid: severity colours", grid.Rows[0].DefaultCellStyle.BackColor == GridService.ErrorBackColor && grid.Rows[1].DefaultCellStyle.BackColor == GridService.WarningBackColor);
    Check("findings grid: tooltips carry the note", grid.Rows[2].Cells[0].ToolTipText.Contains("Information only") && grid.Rows[0].Cells[3].ToolTipText == "note for missing line");

    Finding? toggled = GridService.ToggleFinding(grid, 1);
    Check("toggle: judgement row ticks", toggled == diagnosis.Findings[1] && diagnosis.Findings[1].Selected && grid.Rows[1].Cells[0].Value is true && diagnosis.Selected == 2);
    toggled = GridService.ToggleFinding(grid, 1);
    Check("toggle: judgement row unticks again", toggled is not null && !diagnosis.Findings[1].Selected && grid.Rows[1].Cells[0].Value is false && diagnosis.Selected == 1);
    toggled = GridService.ToggleFinding(grid, 0);
    Check("toggle: recommended row unticks", toggled is not null && !diagnosis.Findings[0].Selected && diagnosis.Selected == 0);
    toggled = GridService.ToggleFinding(grid, 2);
    Check("toggle: note row refuses", toggled is null && grid.Rows[2].Cells[0].Value is false && !diagnosis.Findings[2].Selected);
    toggled = GridService.ToggleFinding(grid, 3);
    Check("toggle: warning note row refuses", toggled is null && !diagnosis.Findings[3].Selected);
    Check("toggle: out of range refuses", GridService.ToggleFinding(grid, -1) is null && GridService.ToggleFinding(grid, 4) is null);

    diagnosis.Findings[0].Selected = true;
    rows = GridService.FillFindings(grid, diagnosis, false);
    Check("findings grid: hiding Info rows keeps Error, Warning and warning-notes", rows == 3 && grid.Rows.Count == 3 && grid.Rows[2].Tag == diagnosis.Findings[3]);
    Check("findings grid: refill keeps Selected", grid.Rows[0].Cells[0].Value is true);
    rows = GridService.FillFindings(grid, null, true);
    Check("findings grid: null diagnosis empties", rows == 0 && grid.Rows.Count == 0);

    GridService.FillFindings(grid, diagnosis, true);
    using var bitmap = new Bitmap(60, 30);
    using var g = Graphics.FromImage(bitmap);
    try
    {
        var e = PaintArgs(grid, g, 2);
        GridService.PaintFindingCell(grid, e);
        Check("paint: note row is painted as a word", e.Handled);
        e = PaintArgs(grid, g, 0);
        GridService.PaintFindingCell(grid, e);
        Check("paint: fixable row keeps the check box", !e.Handled);
    }
    catch (Exception ex)
    {
        Check("paint: note row is painted as a word", false, ex.GetType().Name + ": " + ex.Message);
    }
}

// ---- scan grid
using (var grid = new DataGridView { AutoGenerateColumns = false, AllowUserToAddRows = false })
{
    GridService.AddScanColumns(grid);
    Check("scan grid: 5 columns, Fix first and read-only", grid.Columns.Count == 5 && grid.Columns[0] is DataGridViewCheckBoxColumn && grid.Columns[0].ReadOnly);
    var needsRepair = new Diagnosis { WdpPath = @"C:\a\A.wdp", DrawingsChecked = true };
    needsRepair.Findings.Add(Make("missing", Severity.Error, RepairAction.Add, true));
    var healthy = new Diagnosis { WdpPath = @"C:\b\B.wdp", DrawingsChecked = true };
    var review = new Diagnosis { WdpPath = @"C:\c\C.wdp" };
    review.Findings.Add(Make("different", Severity.Warning, RepairAction.Replace, false));
    for (int i = 0; i < 4; i++)
    {
        grid.Rows.Add(false, "P" + i, @"C:\x", "", 0);
    }

    GridService.SetScanRow(grid.Rows[0], needsRepair, needsRepair.WdpPath, null, true);
    GridService.SetScanRow(grid.Rows[1], healthy, healthy.WdpPath, null, true);
    GridService.SetScanRow(grid.Rows[2], review, review.WdpPath, null, true);
    GridService.SetScanRow(grid.Rows[3], null, @"C:\d\D.wdp", "boom", true);
    Check("scan grid: words", GridService.ScanWord(grid.Rows[0]) is null && GridService.ScanWord(grid.Rows[1]) == "ok" && GridService.ScanWord(grid.Rows[2]) == "review" && GridService.ScanWord(grid.Rows[3]) == "error");
    Check("scan grid: repairable row ticked, others not", grid.Rows[0].Cells[0].Value is true && grid.Rows[1].Cells[0].Value is false && grid.Rows[2].Cells[0].Value is false && grid.Rows[3].Cells[0].Value is false);
    Check("scan grid: status and recommended cells", (string)grid.Rows[0].Cells[3].Value! == "1 error(s), 0 warning(s)" && (int)grid.Rows[0].Cells[4].Value! == 1 && ((string)grid.Rows[2].Cells[3].Value!).EndsWith("(text files only)") && ((string)grid.Rows[3].Cells[3].Value!).StartsWith("ERROR: boom"));
    Check("scan grid: colours", grid.Rows[0].DefaultCellStyle.BackColor == GridService.ErrorBackColor && grid.Rows[1].DefaultCellStyle.BackColor == GridService.HealthyBackColor && grid.Rows[2].DefaultCellStyle.BackColor == GridService.WarningBackColor && grid.Rows[3].DefaultCellStyle.BackColor == GridService.ErrorBackColor);
    Check("scan grid: only word rows are read-only", !grid.Rows[0].ReadOnly && grid.Rows[1].ReadOnly && grid.Rows[2].ReadOnly && grid.Rows[3].ReadOnly);
    Check("scan grid: error row keeps the wdp path as tag", grid.Rows[3].Tag is string s && s.EndsWith("D.wdp"));
    Check("scan toggle: repairable row unticks and ticks", GridService.ToggleScan(grid, 0) && grid.Rows[0].Cells[0].Value is false && GridService.ToggleScan(grid, 0) && grid.Rows[0].Cells[0].Value is true);
    Check("scan toggle: word rows refuse", !GridService.ToggleScan(grid, 1) && !GridService.ToggleScan(grid, 2) && !GridService.ToggleScan(grid, 3) && !GridService.ToggleScan(grid, 7));
    GridService.SetScanRow(grid.Rows[0], needsRepair, needsRepair.WdpPath, null, false);
    Check("scan grid: refresh without tick leaves the box clear", grid.Rows[0].Cells[0].Value is false && GridService.ScanWord(grid.Rows[0]) is null);

    using var bitmap = new Bitmap(60, 30);
    using var g = Graphics.FromImage(bitmap);
    try
    {
        var e = PaintArgs(grid, g, 1);
        GridService.PaintScanCell(grid, e);
        Check("scan paint: ok row is painted as a word", e.Handled);
        e = PaintArgs(grid, g, 0);
        GridService.PaintScanCell(grid, e);
        Check("scan paint: repairable row keeps the check box", !e.Handled);
    }
    catch (Exception ex)
    {
        Check("scan paint: ok row is painted as a word", false, ex.GetType().Name + ": " + ex.Message);
    }
}

Console.WriteLine();
Console.WriteLine($"{passed} passed, {failed} failed  ({BuildInfo.Label})");
return failed == 0 ? 0 : 1;
