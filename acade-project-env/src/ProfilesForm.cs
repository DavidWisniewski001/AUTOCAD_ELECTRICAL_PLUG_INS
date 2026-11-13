using System.Drawing;
using System.Windows.Forms;

namespace LSE.AutoCADElectrical.ProjectEnvironment;

public sealed class ProfilesForm : Form
{
    private readonly AppSettings settings = SettingsService.Load();
    private readonly string projectPath;
    private EnvRules? rules;
    private List<EnvironmentProfile> profiles = new();
    private EnvironmentProfile? current;
    private bool loading;
    private bool dirty;

    private readonly ListBox list = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly TextBox nameBox = new() { Dock = DockStyle.Fill };
    private readonly TextBox customerBox = new() { Dock = DockStyle.Fill };
    private readonly TextBox descriptionBox = new() { Dock = DockStyle.Fill };
    private readonly ComboBox schemeBox = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Label schemeInfo = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, ForeColor = Color.FromArgb(80, 80, 80) };
    private readonly DataGridView grid = new()
    {
        Dock = DockStyle.Fill,
        AutoGenerateColumns = false,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false,
        RowHeadersVisible = false,
        SelectionMode = DataGridViewSelectionMode.CellSelect,
        MultiSelect = false,
        AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
        BackgroundColor = SystemColors.Window,
        BorderStyle = BorderStyle.FixedSingle
    };
    private readonly TextBox propertiesBox = new() { Dock = DockStyle.Fill };
    private readonly TextBox optionsBox = new() { Dock = DockStyle.Fill };
    private readonly TextBox drawingBox = new() { Dock = DockStyle.Fill };
    private readonly TextBox wdtBox = new() { Dock = DockStyle.Fill };
    private readonly TextBox wdlBox = new() { Dock = DockStyle.Fill };
    private readonly TextBox notesBox = new() { Dock = DockStyle.Fill };
    private readonly CheckBox ownFromToBox = new() { Text = "Own From/To settings (otherwise the scheme's are used)", AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly TextBox formatBox = new() { Dock = DockStyle.Fill };
    private readonly TextBox duplicateBox = new() { Dock = DockStyle.Fill };
    private readonly TextBox pinFormatBox = new() { Dock = DockStyle.Fill };
    private readonly ComboBox multiBox = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox separatorBox = new() { Dock = DockStyle.Fill };
    private readonly TextBox priorityBox = new() { Dock = DockStyle.Fill };
    private readonly ComboBox singleBox = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox noneBox = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox maxLengthBox = new() { Dock = DockStyle.Fill };
    private readonly CheckBox pinBox = new() { Text = "Include pin numbers", AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly CheckBox terminalsLastBox = new() { Text = "Terminal strips last", AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly CheckBox uppercaseBox = new() { Text = "Upper case", AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly CheckBox fixedBox = new() { Text = "Write as fixed wire numbers", AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly CheckBox overwriteFixedBox = new() { Text = "Overwrite existing fixed numbers", AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly Label statusLabel = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
    private readonly Button newButton = new() { Text = "New", Dock = DockStyle.Fill };
    private readonly Button duplicateButton = new() { Text = "Duplicate", Dock = DockStyle.Fill };
    private readonly Button captureButton = new() { Text = "Capture from project...", Dock = DockStyle.Fill };
    private readonly Button importButton = new() { Text = "Import...", Dock = DockStyle.Fill };
    private readonly Button exportButton = new() { Text = "Export...", Dock = DockStyle.Fill };
    private readonly Button retireButton = new() { Text = "Retire", Dock = DockStyle.Fill };
    private readonly Button helpButton = new() { Text = "Help", Dock = DockStyle.Fill };
    private readonly ToolTip tips = new() { AutoPopDelay = 30000, InitialDelay = 300, ReshowDelay = 100 };
    private readonly Button saveButton = new() { Text = "Save", Dock = DockStyle.Fill };
    private readonly Button closeButton = new() { Text = "Close", Dock = DockStyle.Fill };

    public ProfilesForm(string projectPath)
    {
        this.projectPath = projectPath;
        Text = BuildInfo.Title("customer profiles");
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1320, 820);
        MinimumSize = new Size(1040, 640);
        BuildLayout();
        Shown += (_, _) => Reload(null);
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(10) };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        var left = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 200));
        left.Controls.Add(list, 0, 0);
        var leftButtons = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6 };
        foreach (Button button in new[] { newButton, duplicateButton, captureButton, importButton, exportButton, retireButton })
        {
            leftButtons.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            leftButtons.Controls.Add(button);
        }

        left.Controls.Add(leftButtons, 0, 1);
        root.Controls.Add(left, 0, 0);

        var right = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, AutoScroll = true };
        right.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        right.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        right.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        right.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        int row = 0;
        AddRow(right, "Profile name", nameBox, "Customer", customerBox, ref row);
        AddRow(right, "Description", descriptionBox, "Wire scheme", schemeBox, ref row);
        AddWide(right, "", schemeInfo, ref row, 30);
        AddWide(right, "Settings (blank = not set by this profile)", grid, ref row, 300);
        AddRow(right, "Raw ?[n] overrides (6=S;9=%N)", propertiesBox, "Raw +[n] overrides", optionsBox, ref row);
        AddRow(right, "Raw WD_M overrides (WIREFMT=%N)", drawingBox, "Notes", notesBox, ref row);
        AddRow(right, "Title block mapping (.wdt) to copy", wdtBox, "Description labels (.wdl) to copy", wdlBox, ref row);
        AddWide(right, "", ownFromToBox, ref row, 30);
        AddRow(right, "From/To format", formatBox, "Duplicate suffix", duplicateBox, ref row);
        AddRow(right, "Pin format", pinFormatBox, "More than two devices", multiBox, ref row);
        AddRow(right, "Join separator (all devices)", separatorBox, "Device priority (regex; separated)", priorityBox, ref row);
        AddRow(right, "One tagged device", singleBox, "No tagged device", noneBox, ref row);
        AddRow(right, "Maximum length (0 = none)", maxLengthBox, "", new Panel(), ref row);
        var flags = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, AutoSize = true };
        foreach (CheckBox box in new[] { pinBox, terminalsLastBox, uppercaseBox, fixedBox, overwriteFixedBox })
        {
            box.Margin = new Padding(0, 6, 18, 0);
            flags.Controls.Add(box);
        }

        AddWide(right, "", flags, ref row, 40);
        root.Controls.Add(right, 1, 0);

        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4 };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        footer.Controls.Add(statusLabel, 0, 0);
        footer.Controls.Add(helpButton, 1, 0);
        footer.Controls.Add(saveButton, 2, 0);
        footer.Controls.Add(closeButton, 3, 0);
        root.Controls.Add(footer, 0, 1);
        root.SetColumnSpan(footer, 2);
        Controls.Add(root);

        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Setting", Width = 230, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Where it lives", Width = 190, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Value", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 30 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Choices / note", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 50, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { WrapMode = DataGridViewTriState.True } });
        foreach (DataGridViewColumn column in grid.Columns)
        {
            column.SortMode = DataGridViewColumnSortMode.NotSortable;
        }

        grid.CellValueChanged += (_, _) => MarkDirty();
        multiBox.Items.AddRange(new object[] { "firstLast", "all" });
        singleBox.Items.AddRange(new object[] { "keep", "device", "skip" });
        noneBox.Items.AddRange(new object[] { "keep", "skip" });
        foreach (Control control in new Control[] { nameBox, customerBox, descriptionBox, propertiesBox, optionsBox, drawingBox, notesBox, wdtBox, wdlBox, formatBox, duplicateBox, pinFormatBox, separatorBox, priorityBox, maxLengthBox })
        {
            control.TextChanged += (_, _) => MarkDirty();
        }

        foreach (ComboBox combo in new[] { schemeBox, multiBox, singleBox, noneBox })
        {
            combo.SelectedIndexChanged += (_, _) => MarkDirty();
        }

        foreach (CheckBox box in new[] { ownFromToBox, pinBox, terminalsLastBox, uppercaseBox, fixedBox, overwriteFixedBox })
        {
            box.CheckedChanged += (_, _) => MarkDirty();
        }

        schemeBox.SelectedIndexChanged += (_, _) => ShowSchemeInfo();
        ownFromToBox.CheckedChanged += (_, _) => EnableFromTo();
        list.SelectedIndexChanged += (_, _) => SelectProfile();
        newButton.Click += (_, _) => NewProfile(null);
        duplicateButton.Click += (_, _) => { if (current is not null) { Commit(); NewProfile(current.Clone()); } };
        captureButton.Click += (_, _) => CaptureFromProject();
        importButton.Click += (_, _) => Import();
        exportButton.Click += (_, _) => Export();
        retireButton.Click += (_, _) => Retire();
        helpButton.Click += (_, _) => { using var form = new TextReportForm("LSE_Env: profile help", HelpText.Fix(HelpText.Profiles)); form.ShowDialog(this); };
        saveButton.Click += (_, _) => Save();
        closeButton.Click += (_, _) => Close();
        CancelButton = closeButton;
        SetTips();
        FormClosing += (_, e) =>
        {
            if (dirty && MessageBox.Show("Discard the unsaved changes to this profile?", "LSE_Env", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                e.Cancel = true;
            }
        };
    }

    private void SetTips()
    {
        tips.SetToolTip(list, "Your saved customer profiles. Click one to edit it. A profile holds the settings one customer insists on.");
        tips.SetToolTip(newButton, "Start an empty profile. Name it after the customer, fill in what they mandate, then Save.");
        tips.SetToolTip(duplicateButton, "Copy the selected profile under a new name. Best way to start from one of the samples.");
        tips.SetToolTip(captureButton, "Read the current settings of a project that is already set up the right way into a new profile. Name it and Save.");
        tips.SetToolTip(importButton, "Load a profile .json file sent by a colleague. It is kept only when you press Save.");
        tips.SetToolTip(exportButton, "Save the selected profile as a .json file you can mail to a colleague.");
        tips.SetToolTip(retireButton, "Move the selected profile to the _retired folder. Nothing is deleted; you can move it back by hand.");
        tips.SetToolTip(helpButton, "What a profile is, the steps to make one, a worked example and what every field means.");
        tips.SetToolTip(saveButton, "Save this profile to the profiles folder. Then use it from LSEENV (Plan, then Apply).");
        tips.SetToolTip(closeButton, "Close the profile manager. Asks first if there are unsaved changes.");
        tips.SetToolTip(nameBox, "Profile name, normally the customer's name. It is also the file name and the name shown in LSEENV. Must be unique.");
        tips.SetToolTip(customerBox, "The customer this profile is for. For people to read only.");
        tips.SetToolTip(descriptionBox, "One line saying what this profile is for, so a colleague can pick the right one.");
        tips.SetToolTip(schemeInfo, "What the selected wire scheme does.");
        tips.SetToolTip(grid, "One row per setting LSE knows about. Type a value only where the customer mandates one; a blank value leaves the project as it is. 'Where it lives' is the project line or drawing attribute that holds it, with LSE's confidence in brackets. Grey rows are not mapped yet and are never applied.");
        tips.SetToolTip(propertiesBox, "Project lines (?[n]) that the grid does not list. Format: 6=S;9=%N (line number, equals, value; separate entries with a semicolon). Use only after LSEENVSNAPSHOT showed you the line number.");
        tips.SetToolTip(optionsBox, "The same for + lines, for example 16=value. Lines +[1] to +[4] are the library and menu paths: LSE never changes them.");
        tips.SetToolTip(drawingBox, "Attributes of the WD_M block in each drawing that the grid does not list. Format: WIREFMT=%N (tag, equals, value; separate entries with a semicolon).");
        tips.SetToolTip(notesBox, "Free notes about this profile. For people only.");
        tips.SetToolTip(wdtBox, "Full path of a title block mapping (.wdt) file to copy into the project. Listed in the plan but not ticked, because it replaces the project's own file.");
        tips.SetToolTip(wdlBox, "Full path of a description labels (.wdl) file to copy into the project. Listed in the plan but not ticked, because it replaces the project's own file.");
        tips.SetToolTip(ownFromToBox, "Tick only if this customer's From/To numbering differs from the scheme's. The fields below then replace the scheme's From/To settings.");
        tips.SetToolTip(formatBox, "How the number is built. Tokens: {FROM} {TO} {FROMPIN} {TOPIN} {SHEET} {DRAWING}. Must contain {FROM}. Example {FROM}-{TO} gives PB1-CR1.");
        tips.SetToolTip(duplicateBox, "Added when two wire networks would get the same number. {N} counts 2, 3, 4... Example -{N} gives PB1-CR1-2.");
        tips.SetToolTip(pinFormatBox, "How a device and its pin are written when pins are included. Tokens {TAG} and {PIN}. Example {TAG}:{PIN} gives PB1:3.");
        tips.SetToolTip(multiBox, "When a network joins more than two devices. firstLast: use the first and the last. all: list every device, joined by the separator.");
        tips.SetToolTip(separatorBox, "Text placed between the devices when 'More than two devices' is set to all. Example /.");
        tips.SetToolTip(priorityBox, "Regular expressions on the device tag, separated by ;. A device matching an earlier one goes on the FROM side. Example ^PB;^CR puts push buttons before relays.");
        tips.SetToolTip(singleBox, "Network with only one tagged device. keep: leave the old number. device: use that device alone. skip: leave it and list it in the report.");
        tips.SetToolTip(noneBox, "Network with no tagged device. keep: leave the old number. skip: leave it and list it in the report.");
        tips.SetToolTip(maxLengthBox, "Longest wire number allowed, in characters. Longer ones are cut. 0 means no limit.");
        tips.SetToolTip(pinBox, "Add the pin numbers of the devices to the wire number.");
        tips.SetToolTip(terminalsLastBox, "Terminal strips go on the TO side, so PB1-X1 rather than X1-PB1.");
        tips.SetToolTip(uppercaseBox, "Force the wire number to capital letters.");
        tips.SetToolTip(fixedBox, "Write the numbers as fixed wire numbers, so AutoCAD Electrical's retag does not renumber them. Switching to another scheme later can un-fix them.");
        tips.SetToolTip(overwriteFixedBox, "Also replace wire numbers that are already fixed. Leave off to keep numbers someone set by hand.");
    }

    private void SetSchemeTip()
    {
        string text = rules is null || rules.WireSchemes.Count == 0 ? "No wire schemes are defined in the rules file." : "Wire schemes:\n" + string.Join("\n", rules.WireSchemes.Select(w => "- " + w.Name + ": " + w.Description));
        tips.SetToolTip(schemeBox, "How wires are numbered. (none) applies only the values below.\n" + text);
    }

    private static void AddRow(TableLayoutPanel panel, string label1, Control control1, string label2, Control control2, ref int row)
    {
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        panel.Controls.Add(new Label { Text = label1, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        panel.Controls.Add(control1, 1, row);
        panel.Controls.Add(new Label { Text = label2, AutoSize = true, Anchor = AnchorStyles.Left }, 2, row);
        panel.Controls.Add(control2, 3, row);
        row++;
    }

    private static void AddWide(TableLayoutPanel panel, string label, Control control, ref int row, int height)
    {
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        if (label.Length > 0)
        {
            panel.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Top }, 0, row);
            panel.Controls.Add(control, 1, row);
            panel.SetColumnSpan(control, 3);
        }
        else
        {
            panel.Controls.Add(control, 1, row);
            panel.SetColumnSpan(control, 3);
        }

        row++;
    }

    private void Reload(string? selectName)
    {
        try
        {
            rules = RulesService.Load(settings.RulesConfigPath);
            schemeBox.Items.Clear();
            schemeBox.Items.Add("(none)");
            foreach (WireScheme scheme in rules.WireSchemes)
            {
                schemeBox.Items.Add(scheme.Name);
            }

            SetSchemeTip();
            profiles = ProfileService.Load(settings);
            loading = true;
            list.Items.Clear();
            foreach (EnvironmentProfile profile in profiles)
            {
                list.Items.Add(profile.Display);
            }

            loading = false;
            int index = profiles.FindIndex(p => p.Name.Equals(selectName ?? "", StringComparison.OrdinalIgnoreCase));
            list.SelectedIndex = index >= 0 ? index : (profiles.Count > 0 ? 0 : -1);
            if (profiles.Count == 0)
            {
                NewProfile(null);
            }

            Status($"{profiles.Count} profile(s) in {settings.ProfilesFolder}");
        }
        catch (Exception ex)
        {
            Status("Could not load: " + ex.Message);
            ErrorService.Show(this, "Loading profiles", ex);
        }
    }

    private void SelectProfile()
    {
        if (loading)
        {
            return;
        }

        if (dirty && MessageBox.Show("Discard the unsaved changes to the current profile?", "LSE_Env", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            loading = true;
            list.SelectedIndex = profiles.IndexOf(current!);
            loading = false;
            return;
        }

        current = list.SelectedIndex >= 0 && list.SelectedIndex < profiles.Count ? profiles[list.SelectedIndex] : null;
        Show(current);
    }

    private void NewProfile(EnvironmentProfile? template)
    {
        current = template ?? new EnvironmentProfile { Scheme = rules?.WireSchemes.FirstOrDefault()?.Name ?? "" };
        current.FilePath = "";
        current.Name = template is null ? "New customer" : template.Name + " (copy)";
        loading = true;
        list.SelectedIndex = -1;
        loading = false;
        Show(current);
        dirty = true;
        nameBox.Focus();
        nameBox.SelectAll();
    }

    private void Show(EnvironmentProfile? profile)
    {
        loading = true;
        try
        {
            nameBox.Text = profile?.Name ?? "";
            customerBox.Text = profile?.Customer ?? "";
            descriptionBox.Text = profile?.Description ?? "";
            notesBox.Text = profile?.Notes ?? "";
            wdtBox.Text = profile?.WdtSource ?? "";
            wdlBox.Text = profile?.WdlSource ?? "";
            int schemeIndex = profile is null ? 0 : schemeBox.Items.IndexOf(profile.Scheme);
            schemeBox.SelectedIndex = schemeIndex >= 0 ? schemeIndex : 0;
            propertiesBox.Text = profile is null ? "" : string.Join(";", profile.Properties.Select(p => p.Key + "=" + p.Value));
            optionsBox.Text = profile is null ? "" : string.Join(";", profile.Options.Select(p => p.Key + "=" + p.Value));
            drawingBox.Text = profile is null ? "" : string.Join(";", profile.Drawing.Select(p => p.Key + "=" + p.Value));
            grid.Rows.Clear();
            if (rules is not null)
            {
                foreach (SettingMap map in rules.Settings.Where(m => !m.Protected))
                {
                    string value = profile is not null && profile.Values.TryGetValue(map.Key, out string? v) ? v : "";
                    string note = (map.Choices.Length > 0 ? map.Choices.Replace(";", "   ") : "") + (map.Note.Length > 0 ? (map.Choices.Length > 0 ? "   " : "") + map.Note : "");
                    int row = grid.Rows.Add(map.Label + "  [" + map.Key + "]", map.Where + (map.Confidence.Equals("verified", StringComparison.OrdinalIgnoreCase) ? "" : "  (" + map.Confidence + ")"), value, note);
                    grid.Rows[row].Tag = map;
                    grid.Rows[row].Cells[2].ToolTipText = map.Label + (map.Note.Length > 0 ? ": " + map.Note : "") + (map.Choices.Length > 0 ? "\nChoices: " + map.Choices.Replace(";", "   ") : "") + "\nBlank = this profile does not set it.";
                    if (!map.Mapped)
                    {
                        grid.Rows[row].DefaultCellStyle.ForeColor = Color.FromArgb(110, 110, 110);
                    }
                }
            }

            FromToSettings fromTo = profile?.FromTo ?? new FromToSettings();
            ownFromToBox.Checked = profile?.FromTo is not null;
            formatBox.Text = fromTo.Format;
            duplicateBox.Text = fromTo.DuplicateSuffix;
            pinFormatBox.Text = fromTo.PinFormat;
            multiBox.SelectedItem = multiBox.Items.Contains(fromTo.MultiDevice) ? fromTo.MultiDevice : "firstLast";
            separatorBox.Text = fromTo.AllSeparator;
            priorityBox.Text = string.Join(";", fromTo.DevicePriority);
            singleBox.SelectedItem = singleBox.Items.Contains(fromTo.SingleDevice) ? fromTo.SingleDevice : "keep";
            noneBox.SelectedItem = noneBox.Items.Contains(fromTo.NoDevice) ? fromTo.NoDevice : "keep";
            maxLengthBox.Text = fromTo.MaxLength.ToString();
            pinBox.Checked = fromTo.IncludeTerminalPin;
            terminalsLastBox.Checked = fromTo.TerminalsLast;
            uppercaseBox.Checked = fromTo.Uppercase;
            fixedBox.Checked = fromTo.Fixed;
            overwriteFixedBox.Checked = fromTo.OverwriteFixed;
            EnableFromTo();
            ShowSchemeInfo();
            bool any = profile is not null;
            foreach (Control control in new Control[] { nameBox, customerBox, descriptionBox, schemeBox, grid, propertiesBox, optionsBox, drawingBox, notesBox, wdtBox, wdlBox, ownFromToBox, exportButton, retireButton, duplicateButton, saveButton })
            {
                control.Enabled = any;
            }

            retireButton.Enabled = any && profile!.FilePath.Length > 0;
        }
        finally
        {
            loading = false;
            dirty = false;
        }
    }

    private void ShowSchemeInfo()
    {
        WireScheme? scheme = rules?.Scheme(schemeBox.SelectedItem as string ?? "");
        schemeInfo.Text = scheme is null
            ? "No wire scheme: only the values and overrides below are applied."
            : scheme.Description + (scheme.Values.Count > 0 ? "   (" + string.Join(", ", scheme.Values.Select(v => v.Key + "=" + v.Value)) + ")" : "") + (scheme.IsFromTo ? "   From/To scheme." : "");
    }

    private void EnableFromTo()
    {
        bool on = ownFromToBox.Checked && ownFromToBox.Enabled;
        foreach (Control control in new Control[] { formatBox, duplicateBox, pinFormatBox, multiBox, separatorBox, priorityBox, singleBox, noneBox, maxLengthBox, pinBox, terminalsLastBox, uppercaseBox, fixedBox, overwriteFixedBox })
        {
            control.Enabled = on;
        }
    }

    private void MarkDirty()
    {
        if (!loading)
        {
            dirty = true;
        }
    }

    private void Commit()
    {
        if (current is null)
        {
            return;
        }

        grid.EndEdit();
        current.Name = nameBox.Text.Trim();
        current.Customer = customerBox.Text.Trim();
        current.Description = descriptionBox.Text.Trim();
        current.Notes = notesBox.Text.Trim();
        current.WdtSource = wdtBox.Text.Trim();
        current.WdlSource = wdlBox.Text.Trim();
        current.Scheme = schemeBox.SelectedIndex > 0 ? (schemeBox.SelectedItem as string ?? "") : "";
        current.Values.Clear();
        foreach (DataGridViewRow row in grid.Rows)
        {
            if (row.Tag is SettingMap map)
            {
                string value = (row.Cells[2].Value as string ?? "").Trim();
                if (value.Length > 0)
                {
                    current.Values[map.Key] = value;
                }
            }
        }

        current.Properties = Pairs(propertiesBox.Text, "Raw ?[n] overrides", true);
        current.Options = Pairs(optionsBox.Text, "Raw +[n] overrides", true);
        current.Drawing = new Dictionary<string, string>(Pairs(drawingBox.Text, "Raw WD_M overrides", false), StringComparer.OrdinalIgnoreCase);
        if (ownFromToBox.Checked)
        {
            if (!int.TryParse(maxLengthBox.Text.Trim(), out int maxLength) || maxLength < 0)
            {
                throw new InvalidOperationException("Maximum length must be 0 or a positive whole number.");
            }

            if (!formatBox.Text.Contains("{FROM}", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The From/To format must contain {FROM} (and normally {TO}).");
            }

            current.FromTo = new FromToSettings
            {
                Format = formatBox.Text.Trim(),
                DuplicateSuffix = duplicateBox.Text,
                PinFormat = pinFormatBox.Text.Trim(),
                MultiDevice = multiBox.SelectedItem as string ?? "firstLast",
                AllSeparator = separatorBox.Text,
                DevicePriority = priorityBox.Text.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0).ToList(),
                SingleDevice = singleBox.SelectedItem as string ?? "keep",
                NoDevice = noneBox.SelectedItem as string ?? "keep",
                MaxLength = maxLength,
                IncludeTerminalPin = pinBox.Checked,
                TerminalsLast = terminalsLastBox.Checked,
                Uppercase = uppercaseBox.Checked,
                Fixed = fixedBox.Checked,
                OverwriteFixed = overwriteFixedBox.Checked,
            };
        }
        else
        {
            current.FromTo = null;
        }
    }

    private static Dictionary<string, string> Pairs(string text, string what, bool numeric)
    {
        var result = new Dictionary<string, string>();
        foreach (string part in text.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            int at = part.IndexOf('=');
            if (at <= 0)
            {
                throw new InvalidOperationException(what + ": each entry is key=value separated by ';' (" + part.Trim() + ").");
            }

            string key = part[..at].Trim();
            if (numeric && !int.TryParse(key, out _))
            {
                throw new InvalidOperationException(what + ": '" + key + "' is not a line index.");
            }

            result[key] = part[(at + 1)..];
        }

        return result;
    }

    private void Save()
    {
        try
        {
            if (current is null)
            {
                return;
            }

            Commit();
            string path = ProfileService.Save(settings, current);
            dirty = false;
            Reload(current.Name);
            Status("Saved " + path);
        }
        catch (Exception ex)
        {
            Status("Save failed: " + ex.Message);
            ErrorService.Show(this, "Save a profile", ex);
        }
    }

    private void CaptureFromProject()
    {
        try
        {
            if (rules is null)
            {
                return;
            }

            string wdp = projectPath;
            if (!File.Exists(wdp))
            {
                using var dialog = new OpenFileDialog { Filter = "AutoCAD Electrical project (*.wdp)|*.wdp" };
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                wdp = dialog.FileName;
            }

            bool drawings = MessageBox.Show("Read every drawing's WD_M as well? (closed drawings only; slower)\n\nProject: " + wdp, "LSE_Env", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
            ProjectState state = EnvironmentService.ReadProject(wdp, rules, AcadeBridgeService.Bridge(rules), drawings, Status);
            EnvironmentProfile profile = EnvironmentService.Capture(state, rules, Path.GetFileNameWithoutExtension(wdp) + " settings");
            NewProfile(profile);
            Status($"Captured {profile.Values.Count} value(s) from {wdp}. Name it and press Save.");
        }
        catch (Exception ex)
        {
            Status("Capture failed: " + ex.Message);
            ErrorService.Show(this, "Capture from a project", ex);
        }
    }

    private void Import()
    {
        try
        {
            using var dialog = new OpenFileDialog { Filter = "LSE_Env profile (*.json)|*.json" };
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            EnvironmentProfile? profile = ProfileService.Read(dialog.FileName);
            if (profile is null)
            {
                throw new InvalidOperationException("The file holds no profile.");
            }

            profile.FilePath = "";
            NewProfile(profile);
            current!.Name = profile.Name;
            nameBox.Text = profile.Name;
            Status("Imported " + dialog.FileName + ". Press Save to keep it.");
        }
        catch (Exception ex)
        {
            Status("Import failed: " + ex.Message);
            ErrorService.Show(this, "Import a profile", ex);
        }
    }

    private void Export()
    {
        try
        {
            if (current is null)
            {
                return;
            }

            Commit();
            using var dialog = new SaveFileDialog { Filter = "LSE_Env profile (*.json)|*.json", FileName = ProfileService.FileName(current.Name) + ".json" };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                ProfileService.Export(current, dialog.FileName);
                Status("Exported to " + dialog.FileName);
            }
        }
        catch (Exception ex)
        {
            Status("Export failed: " + ex.Message);
            ErrorService.Show(this, "Export a profile", ex);
        }
    }

    private void Retire()
    {
        try
        {
            if (current is null || current.FilePath.Length == 0)
            {
                return;
            }

            if (MessageBox.Show("Move the profile '" + current.Name + "' to the _retired folder? It can be restored by hand from there.", "LSE_Env", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            string target = ProfileService.Retire(settings, current);
            dirty = false;
            Reload(null);
            Status("Retired to " + target);
        }
        catch (Exception ex)
        {
            Status("Retire failed: " + ex.Message);
            ErrorService.Show(this, "Retire a profile", ex);
        }
    }

    private void Status(string message)
    {
        statusLabel.Text = message;
        statusLabel.Refresh();
    }
}
