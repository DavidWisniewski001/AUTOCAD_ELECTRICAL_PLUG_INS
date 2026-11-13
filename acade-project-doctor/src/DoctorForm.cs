using System.Drawing;
using System.Windows.Forms;

namespace LSE.AutoCADElectrical.ProjectDoctor;

public sealed class DoctorForm : Form
{
    private readonly AppSettings settings = SettingsService.Load();
    private readonly string activeProject;
    private DoctorRules? rules;
    private ProjectStandard? standard;
    private Diagnosis? diagnosis;

    public string? ReloadProjectPath { get; private set; }

    private readonly ComboBox projectBox = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown };
    private readonly Button browseButton = new() { Text = "Browse...", Dock = DockStyle.Fill };
    private readonly Button activeButton = new() { Text = "Active project", Dock = DockStyle.Fill };
    private readonly Label standardLabel = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, ForeColor = Color.FromArgb(80, 80, 80) };
    private readonly CheckBox checkDrawingsBox = new() { Text = "Check every drawing: WD_M block, LSE title block and wire layers against the template drawing (opens each DWG; slower)", AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly Button examineButton = new() { Text = "Examine", Dock = DockStyle.Fill };
    private readonly DataGridView grid = new()
    {
        Dock = DockStyle.Fill,
        AutoGenerateColumns = false,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false,
        RowHeadersVisible = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false,
        AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
        BackgroundColor = SystemColors.Window,
        BorderStyle = BorderStyle.FixedSingle
    };
    private readonly Button tickRecommendedButton = new() { Text = "Tick recommended", AutoSize = true };
    private readonly Button tickAllButton = new() { Text = "Tick all", AutoSize = true };
    private readonly Button untickAllButton = new() { Text = "Untick all", AutoSize = true };
    private readonly CheckBox showInfoBox = new() { Text = "Show informational rows", AutoSize = true, Checked = true };
    private readonly Label summaryLabel = new() { AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold), Padding = new Padding(12, 6, 0, 0) };
    private readonly Label statusLabel = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
    private readonly Button helpButton = new() { Text = "Help", Dock = DockStyle.Fill };
    private readonly Button reportButton = new() { Text = "Report...", Dock = DockStyle.Fill };
    private readonly Button repairButton = new() { Text = "Repair ticked", Dock = DockStyle.Fill, Enabled = false };
    private readonly Button closeButton = new() { Text = "Close", Dock = DockStyle.Fill };
    private readonly ToolTip tips = new() { AutoPopDelay = 30000, InitialDelay = 300, ReshowDelay = 100 };

    public DoctorForm(string initialProject, string activeProject)
    {
        this.activeProject = activeProject;
        Text = BuildInfo.Title("examine and repair a project");
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1240, 760);
        MinimumSize = new Size(900, 560);
        checkDrawingsBox.Checked = settings.CheckDrawings;
        BuildLayout();
        foreach (string recent in settings.RecentProjects)
        {
            projectBox.Items.Add(recent);
        }

        projectBox.Text = initialProject;
        Shown += (_, _) =>
        {
            LoadStandard();
            if (projectBox.Text.Trim().Length > 0 && File.Exists(projectBox.Text.Trim()))
            {
                Examine();
            }
            else if (projectBox.Text.Trim().Length == 0)
            {
                Status(activeProject.Length == 0
                    ? "No active project was detected. Browse to a project .wdp and press Examine. Press Help for a guide to this window."
                    : "Press Examine. Press Help for a guide to this window.");
            }
        };
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(10) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 3 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        header.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        header.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        header.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        header.Controls.Add(new Label { Text = "Project (.wdp)", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        header.Controls.Add(projectBox, 1, 0);
        header.Controls.Add(browseButton, 2, 0);
        header.Controls.Add(activeButton, 3, 0);
        header.Controls.Add(new Label { Text = "Standard", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        header.Controls.Add(standardLabel, 1, 1);
        header.SetColumnSpan(standardLabel, 3);
        header.Controls.Add(checkDrawingsBox, 1, 2);
        header.Controls.Add(examineButton, 2, 2);
        header.SetColumnSpan(examineButton, 2);
        root.Controls.Add(header, 0, 0);

        GridService.AddFindingColumns(grid);
        grid.CellClick += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == 0)
            {
                ToggleRow(e.RowIndex);
            }
            else if (e.RowIndex >= 0 && GridService.IsNoteRow(grid, e.RowIndex))
            {
                ExplainNoteRow(e.RowIndex);
            }
        };
        grid.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Space && grid.CurrentRow is not null)
            {
                ToggleRow(grid.CurrentRow.Index);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        };
        grid.CellPainting += (_, e) => GridService.PaintFindingCell(grid, e);
        root.Controls.Add(grid, 0, 1);

        var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        bar.Controls.Add(tickRecommendedButton);
        bar.Controls.Add(tickAllButton);
        bar.Controls.Add(untickAllButton);
        showInfoBox.Margin = new Padding(16, 6, 0, 0);
        bar.Controls.Add(showInfoBox);
        bar.Controls.Add(summaryLabel);
        root.Controls.Add(bar, 0, 2);

        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5 };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        footer.Controls.Add(statusLabel, 0, 0);
        footer.Controls.Add(helpButton, 1, 0);
        footer.Controls.Add(reportButton, 2, 0);
        footer.Controls.Add(repairButton, 3, 0);
        footer.Controls.Add(closeButton, 4, 0);
        root.Controls.Add(footer, 0, 3);
        Controls.Add(root);

        browseButton.Click += (_, _) => Browse();
        activeButton.Click += (_, _) => UseActiveProject();
        examineButton.Click += (_, _) => Examine();
        tickRecommendedButton.Click += (_, _) => Tick(f => f.Recommended);
        tickAllButton.Click += (_, _) => Tick(f => f.Action != RepairAction.None);
        untickAllButton.Click += (_, _) => Tick(_ => false);
        showInfoBox.CheckedChanged += (_, _) => FillGrid();
        helpButton.Click += (_, _) => ShowHelp();
        reportButton.Click += (_, _) => ShowReport();
        repairButton.Click += (_, _) => Repair();
        closeButton.Click += (_, _) => Close();
        AcceptButton = examineButton;
        CancelButton = closeButton;

        tips.SetToolTip(projectBox, "The AutoCAD Electrical project (.wdp) to examine. Recent projects are in the list.");
        tips.SetToolTip(browseButton, "Pick a project .wdp file. It is examined straight away.");
        tips.SetToolTip(activeButton, "Examine the project that is active in AutoCAD Electrical now.");
        tips.SetToolTip(standardLabel, "The LSE template the project is compared with. Red means the bundled copy is in use because the template path in Settings is not reachable.");
        tips.SetToolTip(checkDrawingsBox, "Also open every drawing of the project in the background and compare its WD_M block, LSE title block and wire layers with the template drawing. Slower. Drawings that are open in AutoCAD are skipped; close them first.");
        tips.SetToolTip(examineButton, "Compare the project with the LSE template and list the findings. Nothing is changed.");
        tips.SetToolTip(grid, "One row per finding. Click the Fix box to tick or untick a repair; rows that say note are information only. Hover over a cell for the full text and the doctor's note.");
        tips.SetToolTip(tickRecommendedButton, "Tick the safe repairs LSE recommends (missing lines, missing files, missing WD_M, missing or off-standard wire layers). Judgement calls stay unticked.");
        tips.SetToolTip(tickAllButton, "Tick every repair the doctor can make, judgement calls included. Read them before you repair.");
        tips.SetToolTip(untickAllButton, "Clear every tick.");
        tips.SetToolTip(showInfoBox, "Show or hide the Info rows (notes with nothing to repair).");
        tips.SetToolTip(summaryLabel, "Status, number of findings, how many repairs LSE recommends and how many are ticked.");
        tips.SetToolTip(helpButton, "A guide to this window: the steps, the grid, why some rows cannot be ticked, what Repair ticked does, how to undo.");
        tips.SetToolTip(reportButton, "The findings as text you can save or copy, for example into the troubleshooting document.");
        tips.SetToolTip(repairButton, "Apply the ticked repairs. Every file is backed up first, each change is checked afterwards, and everything is rolled back if one step fails.");
        tips.SetToolTip(closeButton, "Close this window. If the active project was repaired it is reloaded so the Project Manager shows the new settings.");
    }

    private void LoadStandard()
    {
        try
        {
            rules = RulesService.Load(settings.RulesConfigPath);
            standard = StandardService.Load(settings, rules);
            standardLabel.Text = StandardService.Describe(standard);
            standardLabel.ForeColor = standard.Bundled ? Color.Firebrick : Color.FromArgb(80, 80, 80);
        }
        catch (Exception ex)
        {
            rules = null;
            standard = null;
            standardLabel.Text = "Standard could not be loaded: " + ex.Message;
            standardLabel.ForeColor = Color.Firebrick;
            Status("Fix the template path in LSEDOCTORSETTINGS, then press Examine again. Press Help for details.");
        }
    }

    private void Browse()
    {
        using var dialog = new OpenFileDialog { Title = "Pick the AutoCAD Electrical project to examine", Filter = "AutoCAD Electrical project (*.wdp)|*.wdp" };
        string current = projectBox.Text.Trim();
        if (current.Length > 0 && Directory.Exists(Path.GetDirectoryName(current) ?? ""))
        {
            dialog.InitialDirectory = Path.GetDirectoryName(current);
        }
        else if (Directory.Exists(settings.ProjectsRootFolder))
        {
            dialog.InitialDirectory = settings.ProjectsRootFolder;
        }

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            projectBox.Text = dialog.FileName;
            Examine();
        }
    }

    private void UseActiveProject()
    {
        if (activeProject.Length == 0)
        {
            MessageBox.Show(this, "No active project was detected when the command started (ace_getactiveproject returned nothing). Browse to the project .wdp instead.", "LSE_Doctor");
            return;
        }

        projectBox.Text = activeProject;
        Examine();
    }

    private void Examine()
    {
        string path = projectBox.Text.Trim();
        if (path.Length == 0 || !File.Exists(path))
        {
            MessageBox.Show(this, "Pick an existing project .wdp first.", "LSE_Doctor");
            return;
        }

        if (standard is null || rules is null)
        {
            LoadStandard();
            if (standard is null || rules is null)
            {
                return;
            }
        }

        UseWaitCursor = true;
        examineButton.Enabled = false;
        try
        {
            diagnosis = DiagnosisService.Examine(path, standard, rules, checkDrawingsBox.Checked, settings.InsertMissingWdm, Status);
            SettingsService.Remember(settings, diagnosis.WdpPath);
            if (!projectBox.Items.Contains(diagnosis.WdpPath))
            {
                projectBox.Items.Insert(0, diagnosis.WdpPath);
            }

            FillGrid();
            string open = diagnosis.OpenDrawings > 0
                ? $" {diagnosis.OpenDrawings} drawing(s) are open in AutoCAD and were not checked; close them and press Examine again."
                : "";
            if (diagnosis.Healthy)
            {
                Status("Healthy: the project matches the LSE standard." + (diagnosis.DrawingsChecked ? "" : " Drawing contents were not checked.") + open);
            }
            else if (diagnosis.Recommended > 0)
            {
                Status($"{diagnosis.Status}. {diagnosis.Recommended} recommended repair(s) are ticked; review them and press Repair ticked.{open}");
            }
            else
            {
                Status($"{diagnosis.Status}. No repair is recommended automatically; tick the rows you agree with, or press Help.{open}");
            }
        }
        catch (Exception ex)
        {
            diagnosis = null;
            FillGrid();
            Status("Examine failed: " + ex.Message);
            MessageBox.Show(this, ex.Message, "LSE_Doctor");
        }
        finally
        {
            UseWaitCursor = false;
            examineButton.Enabled = true;
        }
    }

    private void FillGrid()
    {
        GridService.FillFindings(grid, diagnosis, showInfoBox.Checked);
        UpdateSummary();
    }

    private void ToggleRow(int rowIndex)
    {
        Finding? finding = GridService.ToggleFinding(grid, rowIndex);
        if (finding is null)
        {
            ExplainNoteRow(rowIndex);
            return;
        }

        UpdateSummary();
        Status(finding.Selected
            ? $"Ticked: {finding.Item} ({finding.ActionText}). Press Repair ticked when you have ticked everything you want."
            : $"Unticked: {finding.Item}.");
    }

    private void ExplainNoteRow(int rowIndex)
    {
        if (GridService.IsNoteRow(grid, rowIndex) && grid.Rows[rowIndex].Tag is Finding finding)
        {
            Status("Information only, nothing to tick: " + finding.Item + ". " + finding.Note);
        }
    }

    private void UpdateSummary()
    {
        if (diagnosis is null)
        {
            summaryLabel.Text = "";
            repairButton.Enabled = false;
            return;
        }

        summaryLabel.Text = $"{diagnosis.Status}  |  {diagnosis.Findings.Count} finding(s)  |  {diagnosis.Recommended} recommended  |  {diagnosis.Selected} ticked";
        repairButton.Enabled = diagnosis.Selected > 0;
    }

    private void Tick(Func<Finding, bool> select)
    {
        if (diagnosis is null)
        {
            return;
        }

        foreach (Finding finding in diagnosis.Findings)
        {
            finding.Selected = finding.Action != RepairAction.None && select(finding);
        }

        FillGrid();
        Status(diagnosis.Selected == 0 ? "Nothing is ticked." : $"{diagnosis.Selected} repair(s) ticked. Press Repair ticked to apply them.");
    }

    private void ShowHelp()
    {
        using var form = new TextReportForm("LSE_Doctor - help", HelpText.Fix(HelpText.Doctor + "\n\n\n" + HelpText.Findings), true);
        form.ShowDialog(this);
    }

    private void ShowReport()
    {
        if (diagnosis is null)
        {
            MessageBox.Show(this, "Examine a project first.", "LSE_Doctor");
            return;
        }

        using var form = new TextReportForm("LSE_Doctor - " + diagnosis.ProjectName, DiagnosisService.Render(diagnosis));
        form.ShowDialog(this);
    }

    private void Repair()
    {
        if (diagnosis is null || diagnosis.Selected == 0)
        {
            return;
        }

        string backupFolder = Path.Combine(diagnosis.Folder, settings.BackupFolderName);
        bool isActive = activeProject.Length > 0 && activeProject.Equals(diagnosis.WdpPath, StringComparison.OrdinalIgnoreCase);
        string question = $"Apply {diagnosis.Selected} repair(s) to\n{diagnosis.WdpPath}?\n\nOriginals are backed up to\n{backupFolder}\\<timestamp>\n";
        if (isActive)
        {
            question += "\nThis is the active project. It will be reloaded when the doctor closes so the Project Manager picks up the repaired settings.";
        }

        if (MessageBox.Show(this, question, "LSE_Doctor", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        UseWaitCursor = true;
        repairButton.Enabled = false;
        try
        {
            RepairResult result = RepairService.Execute(diagnosis, settings, Status);
            if (isActive && settings.ReactivateAfterRepair)
            {
                ReloadProjectPath = diagnosis.WdpPath;
            }

            if (settings.ShowReportAfterRepair)
            {
                using var form = new TextReportForm("LSE_Doctor - repair " + (result.Unresolved.Count == 0 ? "complete" : "partially complete"), result.Report);
                form.ShowDialog(this);
            }

            Examine();
            Status(result.Unresolved.Count == 0
                ? $"Repair complete: {result.Applied} change(s) applied and verified. Backup: {result.BackupFolder}"
                : $"Repair partially complete: {result.Applied} change(s) applied, {result.Unresolved.Count} still reported. Backup: {result.BackupFolder}");
        }
        catch (Exception ex)
        {
            bool refused = ex.Message.Contains("Nothing was changed", StringComparison.Ordinal) || ex.Message.Contains("Nothing is ticked", StringComparison.Ordinal);
            Status(refused ? "Repair refused before any change was made. Press Help for what the message means." : "Repair failed and was rolled back. Save the message and report it.");
            using var form = new TextReportForm(refused ? "LSE_Doctor - repair refused" : "LSE_Doctor - repair FAILED (rolled back)", ex.Message);
            form.ShowDialog(this);
            Examine();
        }
        finally
        {
            UseWaitCursor = false;
            UpdateSummary();
        }
    }

    private void Status(string message)
    {
        statusLabel.Text = message;
        tips.SetToolTip(statusLabel, message);
        statusLabel.Refresh();
    }
}
