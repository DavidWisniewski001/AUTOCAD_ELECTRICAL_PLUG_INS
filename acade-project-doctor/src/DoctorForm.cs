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
    private bool loadingGrid;

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
    private readonly Button reportButton = new() { Text = "Report...", Dock = DockStyle.Fill };
    private readonly Button repairButton = new() { Text = "Repair ticked", Dock = DockStyle.Fill, Enabled = false };
    private readonly Button closeButton = new() { Text = "Close", Dock = DockStyle.Fill };

    public DoctorForm(string initialProject, string activeProject)
    {
        this.activeProject = activeProject;
        Text = "LSE_Doctor";
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
                    ? "No active project was detected. Browse to a project .wdp and press Examine."
                    : "Press Examine.");
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

        AddColumn(new DataGridViewCheckBoxColumn { HeaderText = "Fix", Width = 44, Name = "Fix" });
        AddColumn(new DataGridViewTextBoxColumn { HeaderText = "Severity", Width = 72, ReadOnly = true });
        AddColumn(new DataGridViewTextBoxColumn { HeaderText = "Category", Width = 170, ReadOnly = true });
        AddColumn(new DataGridViewTextBoxColumn { HeaderText = "Item", Width = 200, ReadOnly = true });
        AddColumn(new DataGridViewTextBoxColumn { HeaderText = "Project has", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 45, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { WrapMode = DataGridViewTriState.True } });
        AddColumn(new DataGridViewTextBoxColumn { HeaderText = "Standard", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 45, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { WrapMode = DataGridViewTriState.True } });
        AddColumn(new DataGridViewTextBoxColumn { HeaderText = "Repair", Width = 130, ReadOnly = true });
        grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (grid.IsCurrentCellDirty)
            {
                grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        };
        grid.CellValueChanged += (_, e) =>
        {
            if (loadingGrid || e.RowIndex < 0 || e.ColumnIndex != 0)
            {
                return;
            }

            if (grid.Rows[e.RowIndex].Tag is Finding finding)
            {
                finding.Selected = grid.Rows[e.RowIndex].Cells[0].Value is true;
                UpdateSummary();
            }
        };
        root.Controls.Add(grid, 0, 1);

        var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        bar.Controls.Add(tickRecommendedButton);
        bar.Controls.Add(tickAllButton);
        bar.Controls.Add(untickAllButton);
        showInfoBox.Margin = new Padding(16, 6, 0, 0);
        bar.Controls.Add(showInfoBox);
        bar.Controls.Add(summaryLabel);
        root.Controls.Add(bar, 0, 2);

        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4 };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        footer.Controls.Add(statusLabel, 0, 0);
        footer.Controls.Add(reportButton, 1, 0);
        footer.Controls.Add(repairButton, 2, 0);
        footer.Controls.Add(closeButton, 3, 0);
        root.Controls.Add(footer, 0, 3);
        Controls.Add(root);

        browseButton.Click += (_, _) => Browse();
        activeButton.Click += (_, _) => UseActiveProject();
        examineButton.Click += (_, _) => Examine();
        tickRecommendedButton.Click += (_, _) => Tick(f => f.Recommended);
        tickAllButton.Click += (_, _) => Tick(f => f.Action != RepairAction.None);
        untickAllButton.Click += (_, _) => Tick(_ => false);
        showInfoBox.CheckedChanged += (_, _) => FillGrid();
        reportButton.Click += (_, _) => ShowReport();
        repairButton.Click += (_, _) => Repair();
        closeButton.Click += (_, _) => Close();
        AcceptButton = examineButton;
        CancelButton = closeButton;
    }

    private void AddColumn(DataGridViewColumn column)
    {
        column.SortMode = DataGridViewColumnSortMode.NotSortable;
        grid.Columns.Add(column);
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
            Status("Fix the template path in LSEDOCTORSETTINGS, then press Examine again.");
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
            Status(diagnosis.Healthy
                ? "Healthy: the project matches the LSE standard." + (diagnosis.DrawingsChecked ? "" : " Drawing contents were not checked.")
                : $"{diagnosis.Status}. {diagnosis.Recommended} recommended repair(s) are ticked; review them and press Repair ticked.");
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
        loadingGrid = true;
        try
        {
            grid.Rows.Clear();
            if (diagnosis is not null)
            {
                foreach (Finding finding in diagnosis.Findings)
                {
                    if (!showInfoBox.Checked && finding.Severity == Severity.Info)
                    {
                        continue;
                    }

                    int index = grid.Rows.Add(finding.Selected, finding.Severity.ToString(), finding.Category, finding.Item, DiagnosisService.Short(finding.Current, 400), DiagnosisService.Short(finding.Standard, 400), finding.ActionText);
                    DataGridViewRow row = grid.Rows[index];
                    row.Tag = finding;
                    row.Cells[0].ReadOnly = finding.Action == RepairAction.None;
                    row.Cells[4].ToolTipText = finding.Current;
                    row.Cells[5].ToolTipText = finding.Standard;
                    row.Cells[6].ToolTipText = finding.Note;
                    row.Cells[3].ToolTipText = finding.Note;
                    row.DefaultCellStyle.BackColor = finding.Severity switch
                    {
                        Severity.Error => Color.FromArgb(255, 228, 225),
                        Severity.Warning => Color.FromArgb(255, 250, 205),
                        _ => SystemColors.Window
                    };
                }
            }
        }
        finally
        {
            loadingGrid = false;
        }

        UpdateSummary();
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
            Status(refused ? "Repair refused before any change was made." : "Repair failed and was rolled back.");
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
        statusLabel.Refresh();
    }
}
