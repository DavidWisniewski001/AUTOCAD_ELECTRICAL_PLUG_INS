using System.Drawing;
using System.Windows.Forms;

namespace LSE.AutoCADElectrical.ProjectDoctor;

public sealed class ScanForm : Form
{
    private readonly AppSettings settings = SettingsService.Load();
    private readonly string activeProject;
    private DoctorRules? rules;
    private ProjectStandard? standard;

    public string? ReloadProjectPath { get; private set; }

    private readonly TextBox folderBox = new() { Dock = DockStyle.Fill };
    private readonly Button browseButton = new() { Text = "Browse...", Dock = DockStyle.Fill };
    private readonly CheckBox checkDrawingsBox = new() { Text = "Check drawing contents too (opens every DWG; much slower)", AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly Button scanButton = new() { Text = "Scan", Dock = DockStyle.Fill };
    private readonly Label standardLabel = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, ForeColor = Color.FromArgb(80, 80, 80) };
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
        BackgroundColor = SystemColors.Window,
        BorderStyle = BorderStyle.FixedSingle
    };
    private readonly Button tickNeedingRepairButton = new() { Text = "Tick all needing repair", AutoSize = true };
    private readonly Button untickAllButton = new() { Text = "Untick all", AutoSize = true };
    private readonly Label summaryLabel = new() { AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold), Padding = new Padding(12, 6, 0, 0) };
    private readonly Label statusLabel = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
    private readonly Button helpButton = new() { Text = "Help", Dock = DockStyle.Fill };
    private readonly Button openButton = new() { Text = "Open in Doctor...", Dock = DockStyle.Fill, Enabled = false };
    private readonly Button repairButton = new() { Text = "Repair ticked (recommended fixes)", Dock = DockStyle.Fill, Enabled = false };
    private readonly Button closeButton = new() { Text = "Close", Dock = DockStyle.Fill };
    private readonly ToolTip tips = new() { AutoPopDelay = 30000, InitialDelay = 300, ReshowDelay = 100 };

    public ScanForm(string activeProject)
    {
        this.activeProject = activeProject;
        Text = BuildInfo.Title("scan a folder");
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1180, 700);
        MinimumSize = new Size(860, 520);
        folderBox.Text = settings.ProjectsRootFolder;
        checkDrawingsBox.Checked = settings.ScanChecksDrawings;
        BuildLayout();
        Shown += (_, _) =>
        {
            LoadStandard();
            Status("Pick the folder to scan and press Scan. Press Help for a guide to this window.");
        };
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(10) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 3 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        header.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        header.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        header.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        header.Controls.Add(new Label { Text = "Folder to scan", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        header.Controls.Add(folderBox, 1, 0);
        header.Controls.Add(browseButton, 2, 0);
        header.Controls.Add(new Label { Text = "Standard", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        header.Controls.Add(standardLabel, 1, 1);
        header.SetColumnSpan(standardLabel, 2);
        header.Controls.Add(checkDrawingsBox, 1, 2);
        header.Controls.Add(scanButton, 2, 2);
        root.Controls.Add(header, 0, 0);

        GridService.AddScanColumns(grid);
        grid.CellClick += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == 0)
            {
                ToggleRow(e.RowIndex);
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
        grid.CellPainting += (_, e) => GridService.PaintScanCell(grid, e);
        grid.SelectionChanged += (_, _) => openButton.Enabled = grid.CurrentRow?.Tag is Diagnosis;
        grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex > 0)
            {
                OpenInDoctor();
            }
        };
        root.Controls.Add(grid, 0, 1);

        var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        bar.Controls.Add(tickNeedingRepairButton);
        bar.Controls.Add(untickAllButton);
        bar.Controls.Add(summaryLabel);
        root.Controls.Add(bar, 0, 2);

        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5 };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        footer.Controls.Add(statusLabel, 0, 0);
        footer.Controls.Add(helpButton, 1, 0);
        footer.Controls.Add(openButton, 2, 0);
        footer.Controls.Add(repairButton, 3, 0);
        footer.Controls.Add(closeButton, 4, 0);
        root.Controls.Add(footer, 0, 3);
        Controls.Add(root);

        browseButton.Click += (_, _) => Browse();
        scanButton.Click += (_, _) => Scan();
        tickNeedingRepairButton.Click += (_, _) => Tick(d => d.Recommended > 0);
        untickAllButton.Click += (_, _) => Tick(_ => false);
        helpButton.Click += (_, _) => ShowHelp();
        openButton.Click += (_, _) => OpenInDoctor();
        repairButton.Click += (_, _) => RepairTicked();
        closeButton.Click += (_, _) => Close();
        AcceptButton = scanButton;
        CancelButton = closeButton;

        tips.SetToolTip(folderBox, "The folder to search for projects (.wdp). Subfolders are included; backup folders and Vault _V folders are skipped.");
        tips.SetToolTip(browseButton, "Pick the folder to scan.");
        tips.SetToolTip(standardLabel, "The LSE template every project is compared with. Red means the bundled copy is in use because the template path in Settings is not reachable.");
        tips.SetToolTip(checkDrawingsBox, "Also open every drawing of every project and check its WD_M block, title block and wire layers. Much slower.");
        tips.SetToolTip(scanButton, "Find and examine every project under the folder. Nothing is changed.");
        tips.SetToolTip(grid, "One row per project. A Fix box means recommended repairs are available; ok = healthy, review = needs a decision in the Doctor, error = could not be examined. Double-click a row to open it in the Doctor.");
        tips.SetToolTip(tickNeedingRepairButton, "Tick every project that has recommended repairs.");
        tips.SetToolTip(untickAllButton, "Clear every tick.");
        tips.SetToolTip(summaryLabel, "How many projects were found, how many need attention and how many are ticked.");
        tips.SetToolTip(helpButton, "A guide to this window: the steps, the columns and what Repair ticked does.");
        tips.SetToolTip(openButton, "Open the selected project in the Doctor window to review every finding and choose the repairs yourself.");
        tips.SetToolTip(repairButton, "Apply only the recommended repairs to every ticked project, each with its own backup. Judgement calls are never applied from here.");
        tips.SetToolTip(closeButton, "Close this window. If the active project was repaired it is reloaded.");
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
        }
    }

    private void Browse()
    {
        using var dialog = new FolderBrowserDialog { SelectedPath = Directory.Exists(folderBox.Text) ? folderBox.Text : "" };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            folderBox.Text = dialog.SelectedPath;
        }
    }

    private void Scan()
    {
        string root = folderBox.Text.Trim();
        if (root.Length == 0 || !Directory.Exists(root))
        {
            MessageBox.Show(this, "Pick an existing folder to scan.", "LSE_Doctor");
            return;
        }

        if (standard is null || rules is null)
        {
            LoadStandard();
            if (standard is null || rules is null)
            {
                MessageBox.Show(this, standardLabel.Text, "LSE_Doctor");
                return;
            }
        }

        settings.ProjectsRootFolder = root;
        settings.ScanChecksDrawings = checkDrawingsBox.Checked;
        try
        {
            SettingsService.Save(settings);
        }
        catch
        {
        }

        UseWaitCursor = true;
        scanButton.Enabled = false;
        try
        {
            grid.Rows.Clear();
            var projects = new List<string>();
            Status("Looking for .wdp files under " + root);
            Walk(root, projects);
            projects.Sort(StringComparer.OrdinalIgnoreCase);
            int n = 0;
            foreach (string wdp in projects)
            {
                n++;
                Status($"Examining {n}/{projects.Count}: {wdp}");
                AddRow(wdp);
            }

            Status(projects.Count == 0
                ? $"No project (.wdp) was found under {root}."
                : $"Scan complete: {projects.Count} project(s) under {root}. Projects with recommended repairs are ticked; press Repair ticked, or double-click a row to review it in the Doctor.");
        }
        finally
        {
            UseWaitCursor = false;
            scanButton.Enabled = true;
        }

        UpdateSummary();
    }

    private void Walk(string folder, List<string> projects)
    {
        try
        {
            foreach (string file in Directory.EnumerateFiles(folder, "*.wdp"))
            {
                if (standard is not null && Path.GetFullPath(file).Equals(standard.WdpPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                projects.Add(Path.GetFullPath(file));
            }

            foreach (string sub in Directory.EnumerateDirectories(folder))
            {
                string name = Path.GetFileName(sub);
                if (name.Equals(settings.BackupFolderName, StringComparison.OrdinalIgnoreCase) || name.Equals("_V", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Walk(sub, projects);
            }
        }
        catch (Exception ex)
        {
            Status("Skipped " + folder + ": " + ex.Message);
        }
    }

    private void AddRow(string wdp)
    {
        int index = grid.Rows.Add(false, Path.GetFileName(wdp), Path.GetDirectoryName(wdp), "", 0);
        RefreshRow(grid.Rows[index], wdp, true);
    }

    private void RefreshRow(DataGridViewRow row, string wdp, bool tickWhenRecommended)
    {
        try
        {
            Diagnosis diagnosis = DiagnosisService.Examine(wdp, standard!, rules!, checkDrawingsBox.Checked, settings.InsertMissingWdm);
            GridService.SetScanRow(row, diagnosis, wdp, null, tickWhenRecommended);
        }
        catch (Exception ex)
        {
            GridService.SetScanRow(row, null, wdp, ex.Message, false);
        }
    }

    private void ToggleRow(int rowIndex)
    {
        if (GridService.ToggleScan(grid, rowIndex))
        {
            UpdateSummary();
            return;
        }

        if (rowIndex >= 0 && rowIndex < grid.Rows.Count)
        {
            Status(GridService.ScanWord(grid.Rows[rowIndex]) switch
            {
                "ok" => "Nothing to tick: this project is healthy.",
                "review" => "Nothing to tick: no repair is recommended automatically for this project. Double-click the row to review its findings in the Doctor.",
                _ => "Nothing to tick: this project could not be examined. See the Status column."
            });
        }
    }

    private void Tick(Func<Diagnosis, bool> select)
    {
        foreach (DataGridViewRow row in grid.Rows)
        {
            if (GridService.ScanWord(row) is null && row.Tag is Diagnosis diagnosis)
            {
                row.Cells[0].Value = select(diagnosis);
            }
        }

        grid.Invalidate();
        UpdateSummary();
    }

    private void UpdateSummary()
    {
        int ticked = grid.Rows.Cast<DataGridViewRow>().Count(r => r.Cells[0].Value is true && r.Tag is Diagnosis);
        int unhealthy = grid.Rows.Cast<DataGridViewRow>().Count(r => r.Tag is Diagnosis d && !d.Healthy);
        summaryLabel.Text = grid.Rows.Count == 0 ? "" : $"{grid.Rows.Count} project(s)  |  {unhealthy} need attention  |  {ticked} ticked";
        repairButton.Enabled = ticked > 0;
    }

    private void ShowHelp()
    {
        using var form = new TextReportForm("LSE_Doctor - scan help", HelpText.Fix(HelpText.Scan), true);
        form.ShowDialog(this);
    }

    private void OpenInDoctor()
    {
        DataGridViewRow? row = grid.CurrentRow;
        string? wdp = row?.Tag is Diagnosis diagnosis ? diagnosis.WdpPath : row?.Tag as string;
        if (row is null || wdp is null)
        {
            return;
        }

        using (var form = new DoctorForm(wdp, activeProject))
        {
            form.ShowDialog(this);
            if (form.ReloadProjectPath is not null)
            {
                ReloadProjectPath = form.ReloadProjectPath;
            }
        }

        if (standard is not null && rules is not null)
        {
            RefreshRow(row, wdp, false);
            UpdateSummary();
        }
    }

    private void RepairTicked()
    {
        var rows = grid.Rows.Cast<DataGridViewRow>().Where(r => r.Cells[0].Value is true && r.Tag is Diagnosis d && d.Recommended > 0).ToList();
        if (rows.Count == 0)
        {
            MessageBox.Show(this, "Tick at least one project that has recommended repairs.", "LSE_Doctor");
            return;
        }

        string question = $"Apply the recommended repairs to {rows.Count} project(s)?\n\nEach project's original files are backed up to its own {settings.BackupFolderName}\\<timestamp> folder first.";
        if (MessageBox.Show(this, question, "LSE_Doctor", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        UseWaitCursor = true;
        repairButton.Enabled = false;
        var report = new List<string>();
        int repaired = 0;
        int failed = 0;
        try
        {
            foreach (DataGridViewRow row in rows)
            {
                var diagnosis = (Diagnosis)row.Tag!;
                foreach (Finding finding in diagnosis.Findings)
                {
                    finding.Selected = finding.Recommended;
                }

                Status("Repairing " + diagnosis.WdpPath);
                try
                {
                    RepairResult result = RepairService.Execute(diagnosis, settings, null);
                    repaired++;
                    report.Add("==== " + diagnosis.WdpPath);
                    report.AddRange(result.Log);
                    report.Add("");
                    if (settings.ReactivateAfterRepair && activeProject.Length > 0 && activeProject.Equals(diagnosis.WdpPath, StringComparison.OrdinalIgnoreCase))
                    {
                        ReloadProjectPath = diagnosis.WdpPath;
                    }
                }
                catch (Exception ex)
                {
                    failed++;
                    report.Add("==== " + diagnosis.WdpPath + " FAILED (rolled back)");
                    report.Add(ex.Message);
                    report.Add("");
                }

                RefreshRow(row, diagnosis.WdpPath, false);
            }
        }
        finally
        {
            UseWaitCursor = false;
            UpdateSummary();
        }

        Status($"Batch repair finished: {repaired} repaired, {failed} failed.");
        using var form = new TextReportForm("LSE_Doctor - batch repair report", string.Join(Environment.NewLine, report));
        form.ShowDialog(this);
    }

    private void Status(string message)
    {
        statusLabel.Text = message;
        tips.SetToolTip(statusLabel, message);
        statusLabel.Refresh();
    }
}
