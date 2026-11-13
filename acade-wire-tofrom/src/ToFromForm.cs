using System.Drawing;
using System.Windows.Forms;

namespace LSE.AutoCADElectrical.WireToFrom;

public sealed class ToFromForm : Form
{
    private readonly AppSettings settings = SettingsService.Load();
    private readonly string activeProject;
    private readonly string scratchPath;
    private readonly string currentDrawing;
    private readonly string refreshNote;
    private ToFromConfig? config;
    private HostBridge? bridge;
    private RunPlan? plan;

    private readonly ComboBox projectBox = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown };
    private readonly Button browseButton = new() { Text = "Browse...", Dock = DockStyle.Fill };
    private readonly Button activeButton = new() { Text = "Active project", Dock = DockStyle.Fill };
    private readonly RadioButton databaseRadio = new() { Text = "Project database", AutoSize = true };
    private readonly RadioButton reportRadio = new() { Text = "Report file:", AutoSize = true };
    private readonly TextBox reportBox = new() { Dock = DockStyle.Fill };
    private readonly Button reportBrowseButton = new() { Text = "Browse...", Dock = DockStyle.Fill };
    private readonly CheckBox annotateBox = new() { Text = "Annotate drawings (place and update labels)", AutoSize = true };
    private readonly CheckBox currentOnlyBox = new() { Text = "Current drawing only", AutoSize = true };
    private readonly CheckBox exportBox = new() { Text = "Export CSV", AutoSize = true, Checked = true };
    private readonly Button planButton = new() { Text = "Plan", Dock = DockStyle.Fill };
    private readonly Label infoLabel = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, ForeColor = Color.FromArgb(80, 80, 80) };
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
    private readonly CheckBox showKeepBox = new() { Text = "Show unchanged", AutoSize = true, Checked = false };
    private readonly Label summaryLabel = new() { AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold), Padding = new Padding(12, 6, 0, 0) };
    private readonly Label statusLabel = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
    private readonly Button reportButton = new() { Text = "Plan text...", Dock = DockStyle.Fill };
    private readonly Button validationButton = new() { Text = "Validation...", Dock = DockStyle.Fill };
    private readonly Button applyButton = new() { Text = "Apply ticked", Dock = DockStyle.Fill, Enabled = false };
    private readonly Button helpButton = new() { Text = "Help", Dock = DockStyle.Fill };
    private readonly Button closeButton = new() { Text = "Close", Dock = DockStyle.Fill };
    private readonly ToolTip tips = new() { AutoPopDelay = 30000, InitialDelay = 300, ReshowDelay = 100 };

    public ToFromForm(string initialProject, string activeProject, string scratchPath, string currentDrawing, string refreshNote)
    {
        this.activeProject = activeProject;
        this.scratchPath = scratchPath;
        this.currentDrawing = currentDrawing;
        this.refreshNote = refreshNote;
        Text = BuildInfo.Title("true To/From wire identifiers");
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1280, 780);
        MinimumSize = new Size(960, 580);
        annotateBox.Checked = true;
        currentOnlyBox.Checked = settings.CurrentDrawingOnly && currentDrawing.Length > 0;
        currentOnlyBox.Enabled = currentDrawing.Length > 0;
        databaseRadio.Checked = !settings.DataSource.Equals("report", StringComparison.OrdinalIgnoreCase);
        reportRadio.Checked = !databaseRadio.Checked;
        reportBox.Text = settings.LastReportFile;
        BuildLayout();
        foreach (string recent in settings.RecentProjects)
        {
            projectBox.Items.Add(recent);
        }

        projectBox.Text = initialProject;
        Shown += (_, _) =>
        {
            LoadConfig();
            bool projectReady = projectBox.Text.Trim().Length > 0 && File.Exists(projectBox.Text.Trim());
            bool sourceReady = reportRadio.Checked ? File.Exists(reportBox.Text.Trim()) : config is not null && config.Database.WireSql.Trim().Length > 0;
            if (projectReady && sourceReady)
            {
                Plan();
            }
            else if (!projectReady)
            {
                Status(activeProject.Length == 0 ? "No active project was detected. Browse to a project .wdp and press Plan." : "Press Plan.");
            }
            else if (reportRadio.Checked)
            {
                Status("Pick the saved Wire From/To report file (Browse...), then press Plan.");
            }
        };
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(10) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 3 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        for (int i = 0; i < 3; i++)
        {
            header.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        }

        header.Controls.Add(new Label { Text = "Project (.wdp)", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        header.Controls.Add(projectBox, 1, 0);
        header.Controls.Add(browseButton, 2, 0);
        header.Controls.Add(activeButton, 3, 0);

        var source = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
        source.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        source.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        source.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        databaseRadio.Margin = new Padding(0, 7, 0, 0);
        reportRadio.Margin = new Padding(0, 7, 0, 0);
        source.Controls.Add(databaseRadio, 0, 0);
        source.Controls.Add(reportRadio, 1, 0);
        source.Controls.Add(reportBox, 2, 0);
        header.Controls.Add(new Label { Text = "Data source", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        header.Controls.Add(source, 1, 1);
        header.Controls.Add(reportBrowseButton, 2, 1);
        header.Controls.Add(infoLabel, 3, 1);

        var options = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        annotateBox.Margin = new Padding(0, 7, 16, 0);
        currentOnlyBox.Margin = new Padding(0, 7, 16, 0);
        exportBox.Margin = new Padding(0, 7, 0, 0);
        options.Controls.Add(annotateBox);
        options.Controls.Add(currentOnlyBox);
        options.Controls.Add(exportBox);
        header.Controls.Add(options, 1, 2);
        header.Controls.Add(planButton, 2, 2);
        header.SetColumnSpan(planButton, 2);
        root.Controls.Add(header, 0, 0);
        root.Controls.Add(new Label { Text = "1 Pick the project and data source  2 Plan  3 Tick the labels to write  4 Apply ticked (closed drawings are backed up; open ones must be saved by you)", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(80, 80, 80) }, 0, 1);

        GridService.AddColumns(grid);
        grid.CellClick += (_, e) =>
        {
            if (e.RowIndex < 0)
            {
                return;
            }

            if (e.ColumnIndex == 0 || GridService.IsNoteRow(grid, e.RowIndex))
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
        grid.CellPainting += (_, e) => GridService.PaintCell(grid, e);
        root.Controls.Add(grid, 0, 2);

        var gridButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        gridButtons.Controls.Add(tickRecommendedButton);
        gridButtons.Controls.Add(tickAllButton);
        gridButtons.Controls.Add(untickAllButton);
        showKeepBox.Margin = new Padding(16, 8, 0, 0);
        gridButtons.Controls.Add(showKeepBox);
        gridButtons.Controls.Add(summaryLabel);
        root.Controls.Add(gridButtons, 0, 3);

        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 6 };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        footer.Controls.Add(statusLabel, 0, 0);
        footer.Controls.Add(reportButton, 1, 0);
        footer.Controls.Add(validationButton, 2, 0);
        footer.Controls.Add(applyButton, 3, 0);
        footer.Controls.Add(helpButton, 4, 0);
        footer.Controls.Add(closeButton, 5, 0);
        root.Controls.Add(footer, 0, 4);
        Controls.Add(root);

        browseButton.Click += (_, _) =>
        {
            using var dialog = new OpenFileDialog { Filter = "AutoCAD Electrical project (*.wdp)|*.wdp", FileName = projectBox.Text };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                projectBox.Text = dialog.FileName;
                Plan();
            }
        };
        activeButton.Click += (_, _) =>
        {
            if (activeProject.Length == 0)
            {
                Status("AutoCAD Electrical reported no active project. Browse to the .wdp instead.");
                return;
            }

            projectBox.Text = activeProject;
            Plan();
        };
        reportBrowseButton.Click += (_, _) =>
        {
            using var dialog = new OpenFileDialog { Filter = "Report files (*.csv;*.txt;*.xls)|*.csv;*.txt;*.xls|All files (*.*)|*.*", FileName = reportBox.Text };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                reportBox.Text = dialog.FileName;
                reportRadio.Checked = true;
            }
        };
        planButton.Click += (_, _) => Plan();
        tickRecommendedButton.Click += (_, _) => Tick(c => c.Action is LabelAction.Insert or LabelAction.Update || c.Action == LabelAction.Delete && c.Note.Contains("duplicate", StringComparison.OrdinalIgnoreCase));
        tickAllButton.Click += (_, _) => Tick(c => c.Writes);
        untickAllButton.Click += (_, _) => Tick(_ => false);
        showKeepBox.CheckedChanged += (_, _) => FillGrid();
        reportButton.Click += (_, _) =>
        {
            if (plan is null)
            {
                return;
            }

            using var form = new TextReportForm("LSE_ToFrom plan: " + plan.Project.Name, IdentifierService.Render(plan));
            form.ShowDialog(this);
        };
        validationButton.Click += (_, _) =>
        {
            if (plan is null)
            {
                return;
            }

            using var form = new TextReportForm("LSE_ToFrom validation: " + plan.Project.Name, Export.Validation(plan, null));
            form.ShowDialog(this);
        };
        applyButton.Click += (_, _) => Apply();
        helpButton.Click += (_, _) =>
        {
            using var form = new TextReportForm("LSE_ToFrom: help", HelpText.Fix(HelpText.Main));
            form.ShowDialog(this);
        };
        closeButton.Click += (_, _) => Close();
        CancelButton = closeButton;
        annotateBox.CheckedChanged += (_, _) => InvalidatePlan("Annotate drawings changed");
        currentOnlyBox.CheckedChanged += (_, _) => InvalidatePlan("Current drawing only changed");
        databaseRadio.CheckedChanged += (_, _) => InvalidatePlan("Data source changed");
        exportBox.CheckedChanged += (_, _) => UpdateSummary();

        tips.SetToolTip(projectBox, "The AutoCAD Electrical project (.wdp) whose wires get identifiers. Recent projects are in the list.");
        tips.SetToolTip(browseButton, "Pick a project .wdp file.");
        tips.SetToolTip(activeButton, "Use the project that is active in AutoCAD Electrical now.");
        tips.SetToolTip(databaseRadio, "Read the from/to rows from the project's scratch database (read-only). Needs the query confirmed once with LSETOFROMDISCOVER.");
        tips.SetToolTip(reportRadio, "Read the rows from a Wire From/To report that AutoCAD Electrical saved to a file. Use this until the database query is confirmed, and to compare.");
        tips.SetToolTip(reportBox, "The saved Wire From/To report file (CSV or tab/semicolon text with a header row).");
        tips.SetToolTip(reportBrowseButton, "Pick the saved report file.");
        tips.SetToolTip(annotateBox, "Place, update and delete the label blocks in the drawings. Untick for CSV and validation only.");
        tips.SetToolTip(currentOnlyBox, "Only the drawing you are in is read and written. The CSV still covers the whole project.");
        tips.SetToolTip(exportBox, "Write the label CSV (one row per wire end) and the report CSV (one row per wire) into <project>\\LSE_ToFrom.");
        tips.SetToolTip(planButton, "Read the data and the drawings and list every label change. Nothing is written until you press Apply ticked.");
        tips.SetToolTip(tickRecommendedButton, "Tick inserts, updates and the removal of duplicate labels. Stale-label deletions are left for you to decide.");
        tips.SetToolTip(tickAllButton, "Tick every change that can be written, including stale-label deletions.");
        tips.SetToolTip(untickAllButton, "Clear all ticks.");
        tips.SetToolTip(showKeepBox, "Also list labels that are already correct (Keep) and rows that cannot be written (Skip).");
        tips.SetToolTip(reportButton, "Show the plan as text, which you can save.");
        tips.SetToolTip(validationButton, "Show the validation report: missing pins and tags, wires without a label, labels without a wire, skipped drawings, stale database.");
        tips.SetToolTip(applyButton, "Write the ticked labels and the CSV files. Closed drawings are backed up first and saved; open ones are changed in the session. Each change is checked afterwards.");
        tips.SetToolTip(helpButton, "A guide to this window and the config file.");
        tips.SetToolTip(closeButton, "Close this window.");
    }

    private void LoadConfig()
    {
        try
        {
            config = ConfigService.Load(settings.ConfigPath);
            bridge = AcadeInterop.Bridge(config);
            infoLabel.Text = config.Schematic.LabelAt + " end, layer " + config.Schematic.LabelLayer;
            tips.SetToolTip(infoLabel, "Config: " + config.Source + "\nFormat: " + config.Format + "\nLabel at: " + config.Schematic.LabelAt + "\nLayer: " + config.Schematic.LabelLayer + (refreshNote.Length > 0 ? "\n" + refreshNote : ""));
            if (config.Database.WireSql.Trim().Length == 0 && databaseRadio.Checked)
            {
                Status("The database query is not set yet (run LSETOFROMDISCOVER once), so use the report file source for now, or run discovery.");
            }
        }
        catch (Exception ex)
        {
            Status("Config could not be loaded: " + ex.Message);
            ErrorService.Show(this, "Loading config", ex);
            planButton.Enabled = false;
        }
    }

    private string? OnlyDrawing => currentOnlyBox.Checked && currentDrawing.Length > 0 ? currentDrawing : null;

    private void Plan()
    {
        if (config is null || bridge is null)
        {
            return;
        }

        try
        {
            string wdp = projectBox.Text.Trim();
            if (!File.Exists(wdp))
            {
                throw new FileNotFoundException("Project file was not found.", wdp);
            }

            settings.DataSource = reportRadio.Checked ? "report" : "scratch";
            settings.LastReportFile = reportBox.Text.Trim();
            try
            {
                SettingsService.Save(settings);
            }
            catch
            {
            }

            UseWaitCursor = true;
            planButton.Enabled = false;
            applyButton.Enabled = false;
            try
            {
                Status("Reading project and from/to data");
                var project = ProjectService.Read(wdp);
                var issues = new List<Issue>();
                var notes = new List<string>();
                var rows = DataSourceService.Load(settings, config, project, scratchPath, reportBox.Text.Trim(), issues, notes, out string sourceText);
                var result = IdentifierService.Plan(project, rows, config, settings, bridge, annotateBox.Checked, OnlyDrawing, Status);
                result.DataSource = sourceText;
                result.Issues.InsertRange(0, issues);
                result.Log.InsertRange(1, notes);
                if (refreshNote.Length > 0)
                {
                    result.Log.Insert(1, refreshNote);
                }

                plan = result;
                FillGrid();
                int stale = plan.Count(IssueKind.StaleDatabase);
                Status($"Planned: {plan.Rows.Count} wire(s), {plan.SchematicEnds.Count} label(s) wanted, {plan.Writable} change(s) to write, {plan.Issues.Count} validation issue(s)" + (stale > 0 ? $"  |  WARNING: {stale} drawing(s) are newer than the database" : ""));
            }
            finally
            {
                UseWaitCursor = false;
                planButton.Enabled = true;
            }
        }
        catch (Exception ex)
        {
            plan = null;
            grid.Rows.Clear();
            summaryLabel.Text = "";
            Status("Plan failed: " + ex.Message.Split('\n')[0]);
            ErrorService.Show(this, "Plan", ex);
        }
    }

    private void FillGrid()
    {
        GridService.Fill(grid, plan, showKeepBox.Checked);
        UpdateSummary();
    }

    private void InvalidatePlan(string reason)
    {
        if (plan is null)
        {
            return;
        }

        plan = null;
        grid.Rows.Clear();
        summaryLabel.Text = "";
        applyButton.Enabled = false;
        Status(reason + ": the plan no longer matches the options. Press Plan again.");
    }

    private void ToggleRow(int rowIndex)
    {
        if (GridService.IsNoteRow(grid, rowIndex))
        {
            Status(GridService.Explain(grid, rowIndex));
            return;
        }

        if (GridService.Toggle(grid, rowIndex) is not null)
        {
            UpdateSummary();
        }
    }

    private void Tick(Func<LabelChange, bool> rule)
    {
        if (plan is null)
        {
            return;
        }

        foreach (DrawingPlan drawing in plan.Drawings.Where(d => d.SkipReason.Length == 0))
        {
            foreach (LabelChange change in drawing.Changes.Where(c => c.Writes))
            {
                change.Selected = rule(change);
            }
        }

        FillGrid();
    }

    private void UpdateSummary()
    {
        if (plan is null)
        {
            summaryLabel.Text = "";
            applyButton.Enabled = false;
            return;
        }

        int drawings = plan.Drawings.Count(d => d.HasWork);
        summaryLabel.Text = $"{plan.Selected} of {plan.Writable} label change(s) ticked in {drawings} drawing(s)  |  {plan.Issues.Count} validation issue(s)";
        applyButton.Enabled = plan.Selected > 0 || exportBox.Checked;
    }

    private void Apply()
    {
        if (plan is null || config is null || bridge is null)
        {
            return;
        }

        try
        {
            if (plan.Selected == 0 && !exportBox.Checked)
            {
                MessageBox.Show(IdentifierService.NothingToDo(plan), "LSE_ToFrom");
                return;
            }

            int inserts = plan.AllChanges.Count(c => c.Selected && c.Action == LabelAction.Insert);
            int updates = plan.AllChanges.Count(c => c.Selected && c.Action == LabelAction.Update);
            int deletes = plan.AllChanges.Count(c => c.Selected && c.Action == LabelAction.Delete);
            int drawings = plan.Drawings.Count(d => d.HasWork);
            int open = plan.Drawings.Count(d => d.HasWork && d.State.OpenInEditor);
            string message = $"Write To/From labels in {plan.Project.Name}?\n\n{inserts} insert, {updates} update, {deletes} delete in {drawings} drawing(s)" + (open > 0 ? $" ({open} open in the editor: changed in the session, you save them)" : "") + (exportBox.Checked ? $"\nCSV: {plan.CsvEnds.Count} label rows and {plan.Rows.Count} report rows into {ProjectService.OutputFolder(plan.Project, config.Csv)}" : "") + "\n\nClosed drawings are backed up first.";
            if (MessageBox.Show(message, "LSE_ToFrom", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
            {
                Status("Cancelled.");
                return;
            }

            UseWaitCursor = true;
            applyButton.Enabled = false;
            planButton.Enabled = false;
            RunResult result;
            try
            {
                result = RunService.Execute(plan, settings, bridge, exportBox.Checked, Status);
            }
            finally
            {
                UseWaitCursor = false;
                planButton.Enabled = true;
            }

            if (settings.ShowReportAfterRun)
            {
                using var form = new TextReportForm("LSE_ToFrom result: " + plan.Project.Name, result.Report);
                form.ShowDialog(this);
            }

            Status(result.Unresolved.Count == 0 ? $"Done: {result.Applied} label change(s) written and verified. " + (result.LabelCsv.Length > 0 ? "CSV in " + Path.GetDirectoryName(result.LabelCsv) : "") : $"Done with {result.Unresolved.Count} unverified change(s); see the report.");
            Plan();
        }
        catch (Exception ex)
        {
            Status("Apply failed: " + ex.Message.Split('\n')[0]);
            ErrorService.Show(this, "Apply", ex, "Nothing partial was left behind: backed-up drawings were restored. Read the log, fix the cause, and run Plan again. Drawings open in the editor keep any session change until you close them without saving.");
        }
    }

    private void Status(string message)
    {
        statusLabel.Text = message;
        statusLabel.Refresh();
    }
}
