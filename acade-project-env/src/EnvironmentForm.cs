using System.Drawing;
using System.Windows.Forms;

namespace LSE.AutoCADElectrical.ProjectEnvironment;

public sealed class EnvironmentForm : Form
{
    private readonly AppSettings settings = SettingsService.Load();
    private readonly string activeProject;
    private EnvRules? rules;
    private HostBridge? bridge;
    private List<EnvironmentProfile> profiles = new();
    private ProjectState? state;
    private EnvPlan? plan;
    private bool loadingGrid;

    public string? ReloadProjectPath { get; private set; }

    public bool RetagRequested { get; private set; }

    private readonly ComboBox projectBox = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown };
    private readonly Button browseButton = new() { Text = "Browse...", Dock = DockStyle.Fill };
    private readonly Button activeButton = new() { Text = "Active project", Dock = DockStyle.Fill };
    private readonly ComboBox profileBox = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Button manageButton = new() { Text = "Manage...", Dock = DockStyle.Fill };
    private readonly Button captureButton = new() { Text = "Capture...", Dock = DockStyle.Fill };
    private readonly ComboBox schemeBox = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Label infoLabel = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, ForeColor = Color.FromArgb(80, 80, 80) };
    private readonly CheckBox drawingsBox = new() { Text = "Read every drawing (WD_M values; closed drawings only; slower)", AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly CheckBox wiresBox = new() { Text = "Trace wires (From/To numbering, un-fixing)", AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly CheckBox retagBox = new() { Text = "Queue retag after apply", AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly Button planButton = new() { Text = "Plan", Dock = DockStyle.Fill };
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
    private readonly CheckBox showInfoBox = new() { Text = "Show notes", AutoSize = true, Checked = true };
    private readonly Label summaryLabel = new() { AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold), Padding = new Padding(12, 6, 0, 0) };
    private readonly Label statusLabel = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
    private readonly Button reportButton = new() { Text = "Report...", Dock = DockStyle.Fill };
    private readonly Button applyButton = new() { Text = "Apply ticked", Dock = DockStyle.Fill, Enabled = false };
    private readonly Button helpButton = new() { Text = "Help", Dock = DockStyle.Fill };
    private readonly ToolTip tips = new() { AutoPopDelay = 30000, InitialDelay = 300, ReshowDelay = 100 };
    private readonly Button closeButton = new() { Text = "Close", Dock = DockStyle.Fill };

    public EnvironmentForm(string initialProject, string activeProject)
    {
        this.activeProject = activeProject;
        Text = BuildInfo.Title("project environment");
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1280, 780);
        MinimumSize = new Size(960, 580);
        drawingsBox.Checked = settings.ApplyToDrawings;
        wiresBox.Checked = settings.ComputeFromTo;
        retagBox.Checked = settings.RetagAfterApply;
        BuildLayout();
        foreach (string recent in settings.RecentProjects)
        {
            projectBox.Items.Add(recent);
        }

        projectBox.Text = initialProject;
        Shown += (_, _) =>
        {
            LoadRulesAndProfiles();
            if (projectBox.Text.Trim().Length > 0 && File.Exists(projectBox.Text.Trim()))
            {
                Plan();
            }
            else
            {
                Status(activeProject.Length == 0
                    ? "No active project was detected. Browse to a project .wdp, pick a profile and press Plan."
                    : "Pick a profile and press Plan.");
            }
        };
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(10) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 146));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 4 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        for (int i = 0; i < 4; i++)
        {
            header.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        }

        header.Controls.Add(new Label { Text = "Project (.wdp)", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        header.Controls.Add(projectBox, 1, 0);
        header.Controls.Add(browseButton, 2, 0);
        header.Controls.Add(activeButton, 3, 0);
        header.Controls.Add(new Label { Text = "Customer profile", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        header.Controls.Add(profileBox, 1, 1);
        header.Controls.Add(manageButton, 2, 1);
        header.Controls.Add(captureButton, 3, 1);
        header.Controls.Add(new Label { Text = "Wire scheme", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        header.Controls.Add(schemeBox, 1, 2);
        header.Controls.Add(infoLabel, 2, 2);
        header.SetColumnSpan(infoLabel, 2);

        var options = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        drawingsBox.Margin = new Padding(0, 7, 16, 0);
        wiresBox.Margin = new Padding(0, 7, 16, 0);
        retagBox.Margin = new Padding(0, 7, 0, 0);
        options.Controls.Add(drawingsBox);
        options.Controls.Add(wiresBox);
        options.Controls.Add(retagBox);
        header.Controls.Add(options, 1, 3);
        header.Controls.Add(planButton, 2, 3);
        header.SetColumnSpan(planButton, 2);
        root.Controls.Add(header, 0, 0);
        root.Controls.Add(new Label { Text = "1 Pick a profile or scheme  2 Plan  3 Tick the changes to apply (notes have no box)  4 Apply ticked", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(80, 80, 80) }, 0, 1);

        PlanGridService.AddColumns(grid);
        grid.CellPainting += (_, e) => PlanGridService.PaintCell(grid, e);
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

            if (grid.Rows[e.RowIndex].Tag is Change change && change.Applies)
            {
                change.Selected = grid.Rows[e.RowIndex].Cells[0].Value is true;
                grid.InvalidateRow(e.RowIndex);
                UpdateSummary();
            }
        };
        root.Controls.Add(grid, 0, 2);

        var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        bar.Controls.Add(tickRecommendedButton);
        bar.Controls.Add(tickAllButton);
        bar.Controls.Add(untickAllButton);
        showInfoBox.Margin = new Padding(16, 6, 0, 0);
        bar.Controls.Add(showInfoBox);
        bar.Controls.Add(summaryLabel);
        root.Controls.Add(bar, 0, 3);

        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5 };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        footer.Controls.Add(statusLabel, 0, 0);
        footer.Controls.Add(reportButton, 1, 0);
        footer.Controls.Add(applyButton, 2, 0);
        footer.Controls.Add(helpButton, 3, 0);
        footer.Controls.Add(closeButton, 4, 0);
        root.Controls.Add(footer, 0, 4);
        Controls.Add(root);

        browseButton.Click += (_, _) => Browse();
        activeButton.Click += (_, _) => UseActiveProject();
        manageButton.Click += (_, _) => ManageProfiles();
        captureButton.Click += (_, _) => CaptureProfile();
        profileBox.SelectedIndexChanged += (_, _) => ProfileChanged();
        planButton.Click += (_, _) => Plan();
        tickRecommendedButton.Click += (_, _) => Tick(c => c.Recommended);
        tickAllButton.Click += (_, _) => Tick(c => c.Applies);
        untickAllButton.Click += (_, _) => Tick(_ => false);
        showInfoBox.CheckedChanged += (_, _) => FillGrid();
        reportButton.Click += (_, _) => ShowReport();
        applyButton.Click += (_, _) => Apply();
        helpButton.Click += (_, _) => { using var form = new TextReportForm("LSE_Env: help", HelpText.Fix(HelpText.Environment)); form.ShowDialog(this); };
        SetTips();
        closeButton.Click += (_, _) => Close();
        AcceptButton = planButton;
        CancelButton = closeButton;
    }

    private void SetTips()
    {
        tips.SetToolTip(projectBox, "The AutoCAD Electrical project (.wdp) to set up. Recent projects are in the list.");
        tips.SetToolTip(browseButton, "Pick a project .wdp file.");
        tips.SetToolTip(activeButton, "Use the project that is active in AutoCAD Electrical now.");
        tips.SetToolTip(profileBox, "The customer's saved settings. (none: scheme only) applies just the wire scheme.");
        tips.SetToolTip(manageButton, "Open the profile manager to create, edit, import, export or retire customer profiles. Press Help there for a guide.");
        tips.SetToolTip(captureButton, "Read the settings of this project as they are now and save them as a new customer profile. Use it on a project that is already set up the way the customer wants.");
        tips.SetToolTip(schemeBox, "How wires are numbered. '(from the profile)' uses the profile's own scheme.");
        tips.SetToolTip(drawingsBox, "Also read and write each drawing's WD_M attributes. Needed for numbering changes to reach existing sheets. Drawings open in AutoCAD are changed in the session; you must save them.");
        tips.SetToolTip(wiresBox, "Follow the wires to write From/To numbers, or to un-fix them when going back to another scheme. Slower on big projects.");
        tips.SetToolTip(retagBox, "When this window closes, start AutoCAD Electrical's Insert Wire Numbers so sequential numbers are renumbered.");
        tips.SetToolTip(planButton, "Work out what would change and list it. Nothing is written until you press Apply ticked.");
        tips.SetToolTip(tickRecommendedButton, "Tick the changes LSE recommends. File copies and un-fixing without a record are left unticked.");
        tips.SetToolTip(tickAllButton, "Tick every change that can be applied.");
        tips.SetToolTip(untickAllButton, "Clear all ticks.");
        tips.SetToolTip(showInfoBox, "Show or hide the rows that are notes only and have no tick box.");
        tips.SetToolTip(reportButton, "Show the plan as text, which you can save.");
        tips.SetToolTip(applyButton, "Write the ticked changes. Every file is backed up first, each change is checked afterwards, and everything is rolled back if one fails.");
        tips.SetToolTip(helpButton, "A guide to this window: the steps, the tick boxes and a worked example.");
        tips.SetToolTip(closeButton, "Close this window.");
    }

    private void LoadRulesAndProfiles()
    {
        try
        {
            rules = RulesService.Load(settings.RulesConfigPath);
            bridge = AcadeBridgeService.Bridge(rules);
            profiles = ProfileService.Load(settings);
            profileBox.Items.Clear();
            profileBox.Items.Add("(none: scheme only)");
            foreach (EnvironmentProfile profile in profiles)
            {
                profileBox.Items.Add(profile);
            }

            profileBox.DisplayMember = nameof(EnvironmentProfile.Display);
            int index = profiles.FindIndex(p => p.Name.Equals(settings.LastProfile, StringComparison.OrdinalIgnoreCase));
            profileBox.SelectedIndex = index >= 0 ? index + 1 : 0;
            FillSchemes();
            infoLabel.Text = $"{profiles.Count} profile(s), {rules.WireSchemes.Count} wire scheme(s), {rules.Settings.Count(m => m.Mapped)} mapped setting(s)";
        }
        catch (Exception ex)
        {
            Status("Rules or profiles could not be loaded: " + ex.Message);
            ErrorService.Show(this, "Loading rules and profiles", ex);
        }
    }

    private void FillSchemes()
    {
        if (rules is null)
        {
            return;
        }

        string previous = schemeBox.SelectedItem is WireScheme selected ? selected.Name : settings.LastScheme;
        schemeBox.Items.Clear();
        schemeBox.Items.Add("(from the profile)");
        foreach (WireScheme scheme in rules.WireSchemes)
        {
            schemeBox.Items.Add(scheme);
        }

        schemeBox.DisplayMember = nameof(WireScheme.Name);
        tips.SetToolTip(schemeBox, "How wires are numbered. '(from the profile)' uses the profile's own scheme.\n" + string.Join("\n", rules.WireSchemes.Select(w => "- " + w.Name + ": " + w.Description)));
        int index = rules.WireSchemes.FindIndex(s => s.Name.Equals(previous, StringComparison.OrdinalIgnoreCase));
        schemeBox.SelectedIndex = SelectedProfile is not null ? 0 : (index >= 0 ? index + 1 : 0);
    }

    private EnvironmentProfile? SelectedProfile => profileBox.SelectedItem as EnvironmentProfile;

    private WireScheme? SelectedScheme
    {
        get
        {
            if (schemeBox.SelectedItem is WireScheme scheme)
            {
                return scheme;
            }

            EnvironmentProfile? profile = SelectedProfile;
            return profile is not null && rules is not null ? rules.Scheme(profile.Scheme) : null;
        }
    }

    private void ProfileChanged()
    {
        EnvironmentProfile? profile = SelectedProfile;
        if (profile is not null)
        {
            schemeBox.SelectedIndex = 0;
            infoLabel.Text = profile.Description.Length > 0 ? profile.Description : profile.Display;
        }
    }

    private void Browse()
    {
        using var dialog = new OpenFileDialog { Filter = "AutoCAD Electrical project (*.wdp)|*.wdp", FileName = projectBox.Text };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            projectBox.Text = dialog.FileName;
            Plan();
        }
    }

    private void UseActiveProject()
    {
        if (activeProject.Length == 0)
        {
            Status("No active project was detected when the command started.");
            return;
        }

        projectBox.Text = activeProject;
        Plan();
    }

    private void ManageProfiles()
    {
        using var form = new ProfilesForm(projectBox.Text.Trim());
        form.ShowDialog(this);
        string keep = SelectedProfile?.Name ?? "";
        LoadRulesAndProfiles();
        int index = profiles.FindIndex(p => p.Name.Equals(keep, StringComparison.OrdinalIgnoreCase));
        profileBox.SelectedIndex = index >= 0 ? index + 1 : profileBox.SelectedIndex;
    }

    private void CaptureProfile()
    {
        try
        {
            if (rules is null || bridge is null)
            {
                throw new InvalidOperationException("Rules are not loaded.");
            }

            string wdp = projectBox.Text.Trim();
            if (!File.Exists(wdp))
            {
                throw new FileNotFoundException("Project file was not found.", wdp);
            }

            string? name = PromptForName("Capture profile from " + Path.GetFileNameWithoutExtension(wdp), Path.GetFileNameWithoutExtension(wdp) + " settings");
            if (name is null)
            {
                return;
            }

            ProjectState captured = EnvironmentService.ReadProject(wdp, rules, bridge, drawingsBox.Checked, Status);
            EnvironmentProfile profile = EnvironmentService.Capture(captured, rules, name);
            string path = ProfileService.Save(settings, profile);
            Status("Profile saved: " + path);
            LoadRulesAndProfiles();
            int index = profiles.FindIndex(p => p.FilePath.Equals(path, StringComparison.OrdinalIgnoreCase));
            profileBox.SelectedIndex = index >= 0 ? index + 1 : 0;
        }
        catch (Exception ex)
        {
            Status("Capture failed: " + ex.Message);
            ErrorService.Show(this, "Capture a profile", ex);
        }
    }

    private string? PromptForName(string title, string suggested)
    {
        using var prompt = new Form { Text = title, StartPosition = FormStartPosition.CenterParent, Size = new Size(520, 150), FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false };
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2, Padding = new Padding(12) };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        var box = new TextBox { Text = suggested, Dock = DockStyle.Fill };
        var ok = new Button { Text = "OK", Dock = DockStyle.Fill, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancel", Dock = DockStyle.Fill, DialogResult = DialogResult.Cancel };
        panel.Controls.Add(box, 0, 0);
        panel.SetColumnSpan(box, 3);
        panel.Controls.Add(ok, 1, 1);
        panel.Controls.Add(cancel, 2, 1);
        prompt.Controls.Add(panel);
        prompt.AcceptButton = ok;
        prompt.CancelButton = cancel;
        return prompt.ShowDialog(this) == DialogResult.OK && box.Text.Trim().Length > 0 ? box.Text.Trim() : null;
    }

    private void Plan()
    {
        try
        {
            if (rules is null || bridge is null)
            {
                throw new InvalidOperationException("Rules are not loaded. Check LSEENVSETTINGS.");
            }

            string wdp = projectBox.Text.Trim();
            if (!File.Exists(wdp))
            {
                throw new FileNotFoundException("Project file was not found.", wdp);
            }

            EnvironmentProfile? profile = SelectedProfile;
            WireScheme? scheme = SelectedScheme;
            if (profile is null && scheme is null)
            {
                throw new InvalidOperationException("Pick a customer profile or a wire scheme first.");
            }

            UseWaitCursor = true;
            planButton.Enabled = false;
            applyButton.Enabled = false;
            try
            {
                state = EnvironmentService.ReadProject(wdp, rules, bridge, drawingsBox.Checked, Status);
                plan = EnvironmentService.Plan(state, profile, scheme, rules, settings, bridge, drawingsBox.Checked, wiresBox.Checked, Status);
            }
            finally
            {
                UseWaitCursor = false;
                planButton.Enabled = true;
            }

            FillGrid();
            applyButton.Enabled = plan.Applicable > 0;
            if (plan.Applicable == 0)
            {
                string why = EnvironmentService.NothingToApply(plan);
                Status(why.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim() + " (" + plan.ProfileName + " / " + plan.SchemeName + ")");
                MessageBox.Show(why, "LSE_Env", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                Status($"{plan.Applicable} change(s) planned, {plan.Selected} ticked. Review the grid, then press Apply ticked.");
            }
        }
        catch (Exception ex)
        {
            plan = null;
            FillGrid();
            applyButton.Enabled = false;
            Status("Plan failed: " + ex.Message);
            ErrorService.Show(this, "Plan", ex);
        }
    }

    private void FillGrid()
    {
        loadingGrid = true;
        try
        {
            PlanGridService.Fill(grid, plan, showInfoBox.Checked);
        }
        finally
        {
            loadingGrid = false;
        }

        UpdateSummary();
    }

    private void Tick(Func<Change, bool> select)
    {
        if (plan is null)
        {
            return;
        }

        foreach (Change change in plan.Changes)
        {
            change.Selected = change.Applies && select(change);
        }

        FillGrid();
    }

    private void UpdateSummary()
    {
        summaryLabel.Text = plan is null ? "" : $"{plan.Selected} of {plan.Applicable} change(s) ticked; {plan.Changes.Count - plan.Applicable} note(s)";
        applyButton.Enabled = plan is not null && plan.Selected > 0;
    }

    private void ShowReport()
    {
        if (plan is null)
        {
            Status("Press Plan first.");
            return;
        }

        using var form = new TextReportForm("LSE_Env plan: " + plan.Project.ProjectName, EnvironmentService.Render(plan));
        form.ShowDialog(this);
    }

    private void Apply()
    {
        if (plan is null || rules is null || bridge is null)
        {
            return;
        }

        int drawings = plan.Changes.Where(c => c.Selected && c.IsDrawing).Select(c => c.Target).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        string message = $"Apply {plan.Selected} change(s) to {plan.Project.ProjectName}?\n\nProfile: {plan.ProfileName}\nWire scheme: {plan.SchemeName}\nDrawings written: {drawings}\n\nEvery touched file is backed up to {settings.BackupFolderName} first.{(retagBox.Checked ? "\nInsert Wire Numbers will be queued afterwards." : "")}";
        if (MessageBox.Show(message, "LSE_Env", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
        {
            return;
        }

        try
        {
            UseWaitCursor = true;
            applyButton.Enabled = false;
            ApplyResult result;
            try
            {
                result = ApplyService.Execute(plan, settings, bridge, Status);
            }
            finally
            {
                UseWaitCursor = false;
            }

            settings.LastProfile = plan.Profile?.Name ?? "";
            settings.LastScheme = schemeBox.SelectedItem is WireScheme chosen ? chosen.Name : "";
            try
            {
                SettingsService.Save(settings);
            }
            catch
            {
            }

            if (settings.ReactivateAfterApply && activeProject.Length > 0 && activeProject.Equals(plan.Project.WdpPath, StringComparison.OrdinalIgnoreCase))
            {
                ReloadProjectPath = plan.Project.WdpPath;
            }

            RetagRequested = retagBox.Checked;
            if (settings.ShowReportAfterApply)
            {
                using var form = new TextReportForm("LSE_Env result: " + plan.Project.ProjectName, result.Report);
                form.ShowDialog(this);
            }

            Status(result.Unresolved.Count == 0
                ? $"Applied {result.Applied} change(s), all verified. Backup in {result.BackupFolder}. Planning again..."
                : $"Applied {result.Applied} change(s), {result.Unresolved.Count} not verified; see the report. Planning again...");
            Plan();
        }
        catch (Exception ex)
        {
            Status("Apply failed and was rolled back. See the log.");
            ErrorService.Log("Apply", ex);
            using var form = new TextReportForm("LSE_Env apply failed (rolled back)", ex.Message);
            form.ShowDialog(this);
            applyButton.Enabled = plan.Selected > 0;
        }
    }

    private void Status(string message)
    {
        statusLabel.Text = message;
        statusLabel.Refresh();
    }
}
