using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace LSE.AutoCADElectrical.ProjectTools;

public sealed class NewProjectForm : Form
{
    private const int StepCount = 4;
    private static readonly string[] StepTitles =
    {
        "Job and project descriptions",
        "Vault location and templates",
        "Drawing list",
        "Review and create"
    };

    private readonly AppSettings settings = SettingsService.Load();
    private ProjectTemplates? templates;
    private int currentStep;
    private bool folderNameEdited;
    private bool projectNameEdited;
    private bool autoNamed = true;
    private bool switchingStep;
    private bool loadingLocation;

    private readonly ListBox stepList = new()
    {
        Dock = DockStyle.Fill,
        IntegralHeight = false,
        Font = new Font("Segoe UI", 11f),
        BorderStyle = BorderStyle.None,
        ItemHeight = 34,
        DrawMode = DrawMode.OwnerDrawFixed
    };
    private readonly Label headerLabel = new()
    {
        Dock = DockStyle.Top,
        Height = 48,
        Font = new Font("Segoe UI", 14f, FontStyle.Bold),
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(12, 0, 0, 0)
    };
    private readonly Panel stepHost = new() { Dock = DockStyle.Fill };
    private readonly Panel[] stepPanels = new Panel[StepCount];
    private readonly Label statusLabel = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
    private readonly Button backButton = new() { Text = "< Back", Dock = DockStyle.Fill };
    private readonly Button nextButton = new() { Text = "Next >", Dock = DockStyle.Fill };
    private readonly Button createButton = new() { Text = "Create project", Dock = DockStyle.Fill };
    private readonly Button closeButton = new() { Text = "Cancel", Dock = DockStyle.Fill };

    // Step 1: job
    private readonly TextBox jobNumberBox = new() { Dock = DockStyle.Fill, CharacterCasing = CharacterCasing.Upper, Font = new Font("Segoe UI", 11f, FontStyle.Bold) };
    private readonly Label jobHintLabel = new() { Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(80, 80, 80) };
    private readonly ComboBox productLineBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly ComboBox projectTypeBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly Label projectTypeDescription = new() { Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(80, 80, 80) };
    private readonly TableLayoutPanel descriptionTable = new() { Dock = DockStyle.Fill, ColumnCount = 2, AutoScroll = true, Padding = new Padding(8) };
    private readonly Dictionary<int, TextBox> descriptionBoxes = new();
    private readonly ComboBox unitTypeBox = new() { DropDownStyle = ComboBoxStyle.DropDown, Width = 90 };
    private readonly ComboBox ampsBox = new() { DropDownStyle = ComboBoxStyle.DropDown, Width = 80 };
    private readonly ComboBox voltsBox = new() { DropDownStyle = ComboBoxStyle.DropDown, Width = 120 };
    private readonly ComboBox kaicBox = new() { DropDownStyle = ComboBoxStyle.DropDown, Width = 70 };
    private readonly ComboBox nemaBox = new() { DropDownStyle = ComboBoxStyle.DropDown, Width = 120 };
    private readonly Label specsCaption = new() { Text = "Unit specs builder:", AutoSize = true, Margin = new Padding(0, 7, 6, 0), Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
    private readonly Button composeButton = CreateToolButton("Compose unit specs", () => { });
    private readonly TextBox iecProjectBox = new() { Width = 90 };
    private readonly TextBox iecInstallationBox = new() { Width = 90 };
    private readonly TextBox iecLocationBox = new() { Width = 90 };

    // Step 2: location
    private readonly TableLayoutPanel locationTable = new() { Dock = DockStyle.Top, ColumnCount = 6, Height = 44, Padding = new Padding(8, 4, 8, 0) };
    private readonly List<ComboBox> levelBoxes = new();
    private readonly List<Label> levelLabels = new();
    private readonly TextBox rootBox = new() { Dock = DockStyle.Fill };
    private readonly TextBox folderNameBox = new() { Dock = DockStyle.Fill };
    private readonly TextBox projectNameBox = new() { Dock = DockStyle.Fill };
    private readonly TextBox baseProjectBox = new() { Dock = DockStyle.Fill };
    private readonly TextBox templateBox = new() { Dock = DockStyle.Fill };
    private readonly Label resultPathLabel = new() { Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(19, 77, 42), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
    private readonly CheckBox fillTitleBlocksCheck = new() { Text = "Fill the LSE title block (ECAD_LSE_CUSTOM_TB_2.0): project lines, drawing number, drawing type", AutoSize = true };
    private readonly CheckBox setWdmCheck = new() { Text = "Set WD_M sheet number and IEC project / installation / location codes", AutoSize = true };
    private readonly CheckBox writeWdtCheck = new() { Text = "Write <job>.wdt so Title Block Update knows the LSE mapping", AutoSize = true };
    private readonly CheckBox writeWdlCheck = new() { Text = "Write <job>_wdtitle.wdl so the Project Manager shows the LSE description labels", AutoSize = true };
    private readonly CheckBox copySidecarsCheck = new() { Text = "Copy the template's own .wdt and .wdl (recommended; otherwise they are generated from the JSON)", AutoSize = true };
    private readonly CheckBox writeAepxCheck = new() { Text = "Write <job>.aepx project configuration (same shape as the template's)", AutoSize = true };
    private readonly CheckBox activateCheck = new() { Text = "Activate the project in the Project Manager when finished", AutoSize = true };
    private readonly CheckBox openFirstCheck = new() { Text = "Open the first drawing when finished", AutoSize = true };
    private readonly CheckBox allowExistingCheck = new() { Text = "Allow an existing, non-empty project folder", AutoSize = true };

    // Step 3: drawings
    private readonly DataGridView drawingGrid = new();
    private BindingList<DrawingRow> drawings = new();
    private readonly TextBox patternBox = new() { Width = 180 };

    // Step 4: review
    private readonly TextBox reviewBox = new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Both,
        WordWrap = false,
        Font = new Font("Consolas", 9.5f),
        BackColor = Color.White
    };

    public ProjectResult? Result { get; private set; }

    public NewProjectForm()
    {
        Text = "LSE New AutoCAD Electrical Project (Lake Shore Electric)";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1440, 920);
        MinimumSize = new Size(1150, 720);
        BuildInterface();
        Shown += (_, _) => LoadTemplates();
    }

    private void BuildInterface()
    {
        var left = new Panel { Dock = DockStyle.Left, Width = 250, Padding = new Padding(8), BackColor = Color.FromArgb(240, 242, 245) };
        stepList.BackColor = left.BackColor;
        stepList.Items.AddRange(new object[] { "1   Job", "2   Vault location", "3   Drawings", "4   Review & create" });
        stepList.DrawItem += DrawStepItem;
        stepList.SelectedIndexChanged += (_, _) =>
        {
            if (!switchingStep && stepList.SelectedIndex >= 0 && stepList.SelectedIndex != currentStep)
            {
                TryGoTo(stepList.SelectedIndex);
            }
        };
        left.Controls.Add(stepList);

        var footer = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 60, ColumnCount = 5, Padding = new Padding(8) };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int index = 1; index < 5; index++)
        {
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        }
        backButton.Click += (_, _) => TryGoTo(currentStep - 1);
        nextButton.Click += (_, _) => TryGoTo(currentStep + 1);
        createButton.Click += (_, _) => CreateProject();
        closeButton.Click += (_, _) => Close();
        footer.Controls.Add(statusLabel, 0, 0);
        footer.Controls.Add(backButton, 1, 0);
        footer.Controls.Add(nextButton, 2, 0);
        footer.Controls.Add(createButton, 3, 0);
        footer.Controls.Add(closeButton, 4, 0);

        stepPanels[0] = BuildJobStep();
        stepPanels[1] = BuildLocationStep();
        stepPanels[2] = BuildDrawingsStep();
        stepPanels[3] = BuildReviewStep();
        foreach (Panel panel in stepPanels)
        {
            panel.Dock = DockStyle.Fill;
            panel.Visible = false;
            stepHost.Controls.Add(panel);
        }

        Controls.Add(stepHost);
        Controls.Add(headerLabel);
        Controls.Add(left);
        Controls.Add(footer);
    }

    private void DrawStepItem(object? sender, DrawItemEventArgs eventArgs)
    {
        if (eventArgs.Index < 0) return;
        bool selected = eventArgs.Index == currentStep;
        bool done = eventArgs.Index < currentStep;
        using var background = new SolidBrush(selected ? Color.FromArgb(31, 79, 191) : stepList.BackColor);
        using var foreground = new SolidBrush(selected ? Color.White : done ? Color.FromArgb(19, 77, 42) : Color.FromArgb(26, 31, 36));
        eventArgs.Graphics.FillRectangle(background, eventArgs.Bounds);
        string text = stepList.Items[eventArgs.Index].ToString() ?? string.Empty;
        var bounds = new Rectangle(eventArgs.Bounds.X + 8, eventArgs.Bounds.Y, eventArgs.Bounds.Width - 8, eventArgs.Bounds.Height);
        TextRenderer.DrawText(eventArgs.Graphics, text, stepList.Font, bounds, foreground.Color, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
    }

    private Panel BuildJobStep()
    {
        var panel = new Panel();
        var top = new TableLayoutPanel { Dock = DockStyle.Top, Height = 118, ColumnCount = 4, Padding = new Padding(8) };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int row = 0; row < 3; row++)
        {
            top.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        }

        top.Controls.Add(CreateLabel("LSE job number (sales order) *"), 0, 0);
        top.Controls.Add(jobNumberBox, 1, 0);
        top.Controls.Add(jobHintLabel, 2, 0);
        top.SetColumnSpan(jobHintLabel, 2);
        top.Controls.Add(CreateLabel("Product line (vault folder)"), 0, 1);
        top.Controls.Add(productLineBox, 1, 1);
        top.Controls.Add(CreateLabel("Drawing set"), 2, 1);
        top.Controls.Add(projectTypeBox, 3, 1);
        top.Controls.Add(projectTypeDescription, 1, 2);
        top.SetColumnSpan(projectTypeDescription, 3);

        jobNumberBox.TextChanged += (_, _) => OnJobNumberChanged();
        productLineBox.SelectedIndexChanged += (_, _) => OnProductLineChanged();
        projectTypeBox.SelectedIndexChanged += (_, _) => OnProjectTypeChanged();

        descriptionTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280));
        descriptionTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var group = new GroupBox
        {
            Text = "Project description lines of the LSE template (*[n] in the .wdp, shown in Project Manager > Descriptions, pushed to the title block). * = required",
            Dock = DockStyle.Fill,
            Padding = new Padding(8)
        };
        group.Controls.Add(descriptionTable);

        var specs = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 84, Padding = new Padding(8, 4, 8, 0), WrapContents = true };
        specs.Controls.Add(specsCaption);
        AddCombo(specs, "Type", unitTypeBox);
        AddCombo(specs, "Amps", ampsBox);
        AddCombo(specs, "Voltage", voltsBox);
        AddCombo(specs, "kAIC", kaicBox);
        AddCombo(specs, "NEMA", nemaBox);
        composeButton.Click += (_, _) => ComposeUnitSpecs();
        specs.Controls.Add(composeButton);
        specs.Controls.Add(new Label { Text = "      IEC codes (optional, written to WD_M):", AutoSize = true, Margin = new Padding(12, 7, 6, 0) });
        specs.Controls.Add(new Label { Text = "%P", AutoSize = true, Margin = new Padding(0, 7, 3, 0) });
        specs.Controls.Add(iecProjectBox);
        specs.Controls.Add(new Label { Text = "%I", AutoSize = true, Margin = new Padding(6, 7, 3, 0) });
        specs.Controls.Add(iecInstallationBox);
        specs.Controls.Add(new Label { Text = "%L", AutoSize = true, Margin = new Padding(6, 7, 3, 0) });
        specs.Controls.Add(iecLocationBox);

        panel.Controls.Add(group);
        panel.Controls.Add(specs);
        panel.Controls.Add(top);
        return panel;
    }

    private static void AddCombo(FlowLayoutPanel host, string label, ComboBox box)
    {
        host.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(6, 7, 3, 0) });
        box.Margin = new Padding(0, 3, 0, 0);
        host.Controls.Add(box);
    }

    private Panel BuildLocationStep()
    {
        var panel = new Panel();
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Padding = new Padding(8), AutoScroll = true };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));

        int row = 0;
        AddRow(table, "Jobs root (Custom Sales Order)", rootBox, row++, () => BrowseFolder(rootBox));

        for (int level = 0; level < 6; level++)
        {
            locationTable.ColumnStyles.Add(new ColumnStyle(level % 2 == 0 ? SizeType.Absolute : SizeType.Percent, level % 2 == 0 ? 120 : 33));
        }
        locationTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        table.Controls.Add(CreateLabel("Product line folders"), 0, row);
        table.Controls.Add(locationTable, 1, row);
        table.SetColumnSpan(locationTable, 2);
        row++;

        AddRow(table, "Job folder name ({JOB}_{CUSTOMER})", folderNameBox, row++, null);
        AddRow(table, "Project name (<name>.wdp)", projectNameBox, row++, null);
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        table.Controls.Add(CreateLabel("Project file"), 0, row);
        table.Controls.Add(resultPathLabel, 1, row);
        table.SetColumnSpan(resultPathLabel, 2);
        row++;
        AddRow(table, "LSE template project (.wdp) whose settings are copied", baseProjectBox, row++, () => BrowseFile(baseProjectBox, "AutoCAD Electrical project (*.wdp)|*.wdp"));
        AddRow(table, "Drawing template (DRAWING-#.dwg)", templateBox, row++, () => BrowseFile(templateBox, "Drawing template (*.dwg;*.dwt)|*.dwg;*.dwt"));

        var options = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(4) };
        foreach (CheckBox check in new[] { fillTitleBlocksCheck, setWdmCheck, writeWdtCheck, writeWdlCheck, copySidecarsCheck, writeAepxCheck, activateCheck, openFirstCheck, allowExistingCheck })
        {
            check.Margin = new Padding(4, 5, 4, 5);
            options.Controls.Add(check);
        }
        var group = new GroupBox { Text = "Options", Dock = DockStyle.Fill, Padding = new Padding(8) };
        group.Controls.Add(options);
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        table.Controls.Add(group, 0, row);
        table.SetColumnSpan(group, 3);

        folderNameBox.TextChanged += (_, _) =>
        {
            if (folderNameBox.Focused) folderNameEdited = true;
            UpdateResultPath();
        };
        projectNameBox.TextChanged += (_, _) =>
        {
            if (projectNameBox.Focused) projectNameEdited = true;
            if (autoNamed) RegenerateFileNames();
            UpdateResultPath();
        };
        rootBox.TextChanged += (_, _) => UpdateResultPath();

        panel.Controls.Add(table);
        return panel;
    }

    private Panel BuildDrawingsStep()
    {
        var panel = new Panel();
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(8, 6, 8, 0), WrapContents = false };
        toolbar.Controls.Add(new Label { Text = "File name pattern", AutoSize = true, Margin = new Padding(0, 7, 6, 0) });
        toolbar.Controls.Add(patternBox);
        toolbar.Controls.Add(CreateToolButton("Load drawing set", () => LoadTypeDrawings(true)));
        toolbar.Controls.Add(CreateToolButton("Add drawing", AddDrawing));
        toolbar.Controls.Add(CreateToolButton("Remove", RemoveDrawing));
        toolbar.Controls.Add(CreateToolButton("Move up", () => MoveDrawing(-1)));
        toolbar.Controls.Add(CreateToolButton("Move down", () => MoveDrawing(1)));
        toolbar.Controls.Add(CreateToolButton("Regenerate file names", () => { autoNamed = true; RegenerateFileNames(); }));
        toolbar.Controls.Add(CreateToolButton("Renumber 001..n", RenumberSequentially));

        var hint = new Label
        {
            Dock = DockStyle.Top,
            Height = 26,
            Padding = new Padding(8, 0, 0, 0),
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(80, 80, 80),
            Text = "Sheet is the E-number (001, 100, 300...). File name follows the pattern ({JOB}-E{SHEET:3} gives 001096-1-E001.dwg) until you edit it. Description 1 is the DRAWING TYPE on the LSE title block. Template: a file in the template folder (STOCKCODE-E001.dwg) or a full path."
        };

        ConfigureGrid();
        panel.Controls.Add(drawingGrid);
        panel.Controls.Add(hint);
        panel.Controls.Add(toolbar);
        return panel;
    }

    private Panel BuildReviewStep()
    {
        var panel = new Panel { Padding = new Padding(8) };
        panel.Controls.Add(reviewBox);
        return panel;
    }

    private void ConfigureGrid()
    {
        drawingGrid.Dock = DockStyle.Fill;
        drawingGrid.AutoGenerateColumns = false;
        drawingGrid.AllowUserToAddRows = false;
        drawingGrid.AllowUserToDeleteRows = false;
        drawingGrid.AllowUserToResizeRows = false;
        drawingGrid.RowHeadersVisible = false;
        drawingGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        drawingGrid.MultiSelect = false;
        drawingGrid.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
        drawingGrid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        drawingGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        drawingGrid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;

        drawingGrid.Columns.Add(TextColumn("Sheet", "Sheet (E-no.)", nameof(DrawingRow.Sheet), 90));
        drawingGrid.Columns.Add(TextColumn("FileName", "File name", nameof(DrawingRow.FileName), 190));
        drawingGrid.Columns.Add(FillColumn("Description1", "Drawing type (description 1)", nameof(DrawingRow.Description1), 220));
        drawingGrid.Columns.Add(FillColumn("Description2", "Description 2", nameof(DrawingRow.Description2), 140));
        drawingGrid.Columns.Add(TextColumn("Description3", "Description 3", nameof(DrawingRow.Description3), 120));
        drawingGrid.Columns.Add(TextColumn("Section", "Section", nameof(DrawingRow.Section), 90));
        drawingGrid.Columns.Add(TextColumn("Subsection", "Subsection", nameof(DrawingRow.Subsection), 90));
        drawingGrid.Columns.Add(TextColumn("Folder", "PM folder", nameof(DrawingRow.Folder), 110));
        drawingGrid.Columns.Add(TextColumn("Template", "Template (optional)", nameof(DrawingRow.Template), 170));

        drawingGrid.CellValueChanged += (_, eventArgs) =>
        {
            if (eventArgs.RowIndex < 0) return;
            string column = drawingGrid.Columns[eventArgs.ColumnIndex].Name;
            if (column == "FileName")
            {
                autoNamed = false;
            }
            else if (column == "Sheet" && autoNamed)
            {
                RegenerateFileNames();
            }
        };
        drawingGrid.DataError += (_, eventArgs) => eventArgs.ThrowException = false;
        drawingGrid.DataSource = drawings;
    }

    private static DataGridViewTextBoxColumn TextColumn(string name, string header, string property, int width) => new()
    {
        Name = name,
        HeaderText = header,
        DataPropertyName = property,
        Width = width,
        MinimumWidth = Math.Min(width, 60),
        SortMode = DataGridViewColumnSortMode.NotSortable
    };

    private static DataGridViewTextBoxColumn FillColumn(string name, string header, string property, int minimumWidth) => new()
    {
        Name = name,
        HeaderText = header,
        DataPropertyName = property,
        AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
        MinimumWidth = minimumWidth,
        SortMode = DataGridViewColumnSortMode.NotSortable
    };

    private static Label CreateLabel(string text) => new() { Text = text, AutoSize = true, Anchor = AnchorStyles.Left };

    private static Button CreateToolButton(string text, Action action)
    {
        var button = new Button { Text = text, AutoSize = true, Margin = new Padding(6, 0, 0, 0), Height = 28 };
        button.Click += (_, _) => action();
        return button;
    }

    private static void AddRow(TableLayoutPanel table, string label, Control control, int row, Action? browse)
    {
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        table.Controls.Add(CreateLabel(label), 0, row);
        table.Controls.Add(control, 1, row);
        if (browse is null)
        {
            table.SetColumnSpan(control, 2);
            return;
        }
        var button = new Button { Text = "Browse", Dock = DockStyle.Fill };
        button.Click += (_, _) => browse();
        table.Controls.Add(button, 2, row);
    }

    private static void BrowseFolder(TextBox box)
    {
        using var dialog = new FolderBrowserDialog { SelectedPath = Directory.Exists(box.Text) ? box.Text : string.Empty };
        if (dialog.ShowDialog() == DialogResult.OK) box.Text = dialog.SelectedPath;
    }

    private static void BrowseFile(TextBox box, string filter)
    {
        using var dialog = new OpenFileDialog { Filter = filter, FileName = File.Exists(box.Text) ? box.Text : string.Empty };
        if (dialog.ShowDialog() == DialogResult.OK) box.Text = dialog.FileName;
    }

    private void LoadTemplates()
    {
        try
        {
            templates = TemplateService.Load(settings.TemplatesConfigPath);
            BuildDescriptionRows();
            FillUnitSpecChoices();

            DescriptionLineDef? specsLine = templates.DescriptionLines.FirstOrDefault(line => line.HasRole("specs"));
            specsCaption.Text = specsLine is null ? "Unit specs builder (no line with role \"specs\"):" : $"Unit specs builder (line {specsLine.Index}, {specsLine.Label}):";
            composeButton.Text = specsLine is null ? "Compose unit specs" : $"Compose line {specsLine.Index}";
            composeButton.Enabled = specsLine is not null;
            jobHintLabel.Text = templates.Job.NumberHint + (templates.Job.NumberExample.Length > 0 ? ". Example: " + templates.Job.NumberExample : string.Empty);
            rootBox.Text = settings.ProjectsRootFolder;
            baseProjectBox.Text = settings.BaseProjectPath;
            templateBox.Text = settings.DrawingTemplatePath;
            fillTitleBlocksCheck.Checked = settings.FillTitleBlocks;
            setWdmCheck.Checked = settings.SetWdmValues;
            writeWdtCheck.Checked = settings.WriteWdtFile;
            writeWdlCheck.Checked = settings.WriteWdlFile;
            copySidecarsCheck.Checked = settings.CopyTemplateSidecars;
            writeAepxCheck.Checked = settings.WriteAepxFile;
            activateCheck.Checked = settings.ActivateProject;
            openFirstCheck.Checked = settings.OpenFirstDrawing;

            projectTypeBox.DataSource = templates.ProjectTypes.ToList();
            int typeIndex = templates.ProjectTypes.FindIndex(type => type.Name.Equals(settings.DefaultProjectType, StringComparison.OrdinalIgnoreCase));
            projectTypeBox.SelectedIndex = typeIndex >= 0 ? typeIndex : 0;

            var lines = templates.ProductLines.ToList();
            if (lines.Count == 0)
            {
                lines.Add(new ProductLineDef { Name = "(no product lines in the templates JSON)" });
            }
            productLineBox.DataSource = lines;
            int lineIndex = lines.FindIndex(line => line.Name.Equals(settings.DefaultProductLine, StringComparison.OrdinalIgnoreCase));
            productLineBox.SelectedIndex = lineIndex >= 0 ? lineIndex : 0;
            OnProductLineChanged();
            OnProjectTypeChanged();
            OnJobNumberChanged();
            ShowStep(0);
            statusLabel.Text = "Templates: " + templates.Source;
            jobNumberBox.Focus();
        }
        catch (System.Exception exception)
        {
            MessageBox.Show(exception.Message, "LSE Project Tools");
            Close();
        }
    }

    private void BuildDescriptionRows()
    {
        descriptionTable.Controls.Clear();
        descriptionTable.RowStyles.Clear();
        descriptionBoxes.Clear();
        int row = 0;
        foreach (DescriptionLineDef line in templates!.DescriptionLines)
        {
            descriptionTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            var label = CreateLabel($"{line.Index}. {line.Label}{(line.Required ? " *" : string.Empty)}   ({line.Attribute})");
            var box = new TextBox
            {
                Dock = DockStyle.Fill,
                PlaceholderText = line.Placeholder,
                Text = TemplateService.Expand(line.Default, string.Empty, 0, string.Empty, DateTime.Now, settings.DrawnBy, settings.DateFormat)
            };
            if (line.HasRole("customer"))
            {
                box.TextChanged += (_, _) => OnJobNumberChanged();
            }
            if (line.HasRole("job"))
            {
                box.CharacterCasing = CharacterCasing.Upper;
            }
            descriptionTable.Controls.Add(label, 0, row);
            descriptionTable.Controls.Add(box, 1, row);
            descriptionBoxes[line.Index] = box;
            row++;
        }
        descriptionTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
    }

    private void FillUnitSpecChoices()
    {
        UnitSpecsDef specs = templates!.UnitSpecs;
        Fill(unitTypeBox, specs.UnitTypes);
        Fill(ampsBox, specs.Amperages);
        Fill(voltsBox, specs.Voltages);
        Fill(kaicBox, specs.Kaic);
        Fill(nemaBox, specs.Nema);
    }

    private static void Fill(ComboBox box, IEnumerable<string> items)
    {
        box.Items.Clear();
        foreach (string item in items.Where(item => item.Trim().Length > 0))
        {
            box.Items.Add(item.Trim());
        }
    }

    private void ComposeUnitSpecs()
    {
        DescriptionLineDef? line = templates?.DescriptionLines.FirstOrDefault(item => item.HasRole("specs"));
        if (line is null || !descriptionBoxes.TryGetValue(line.Index, out TextBox? box))
        {
            MessageBox.Show("The templates JSON has no description line with role \"specs\" to receive the unit specs.", "LSE Project Tools");
            return;
        }
        box.Text = TemplateService.ComposeUnitSpecs(templates!.UnitSpecs, unitTypeBox.Text, ampsBox.Text, voltsBox.Text, kaicBox.Text, nemaBox.Text);
    }

    private ProductLineDef? SelectedProductLine => productLineBox.SelectedItem as ProductLineDef;

    private ProjectTypeDef? SelectedType => projectTypeBox.SelectedItem as ProjectTypeDef;

    private void OnProductLineChanged()
    {
        ProductLineDef? line = SelectedProductLine;
        if (line is null || templates is null) return;

        if (line.UnitType.Trim().Length > 0)
        {
            unitTypeBox.Text = line.UnitType.Trim();
        }
        if (line.ProjectType.Trim().Length > 0)
        {
            int index = templates.ProjectTypes.FindIndex(type => type.Name.Equals(line.ProjectType, StringComparison.OrdinalIgnoreCase));
            if (index >= 0 && projectTypeBox.SelectedIndex != index)
            {
                projectTypeBox.SelectedIndex = index;
            }
        }
        BuildLevelBoxes(line);
        UpdateResultPath();
    }

    private void BuildLevelBoxes(ProductLineDef line)
    {
        loadingLocation = true;
        locationTable.Controls.Clear();
        levelBoxes.Clear();
        levelLabels.Clear();
        int levels = Math.Max(line.Levels.Count, Depth(line.Children));
        for (int level = 0; level < levels && level < 3; level++)
        {
            var label = CreateLabel(level < line.Levels.Count ? line.Levels[level] : "Level " + (level + 1));
            var box = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, Dock = DockStyle.Fill };
            int captured = level;
            box.SelectedIndexChanged += (_, _) => OnLevelChanged(captured);
            box.TextChanged += (_, _) => { if (!loadingLocation) UpdateResultPath(); };
            levelLabels.Add(label);
            levelBoxes.Add(box);
            locationTable.Controls.Add(label, level * 2, 0);
            locationTable.Controls.Add(box, level * 2 + 1, 0);
        }
        if (levels == 0)
        {
            locationTable.Controls.Add(new Label
            {
                Text = line.Folder.Trim().Length == 0 ? "No product line folder: the job folder is created directly under the jobs root (or browse to another root)." : "The job folder is created directly under " + line.Folder,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                ForeColor = Color.FromArgb(80, 80, 80)
            }, 0, 0);
            locationTable.SetColumnSpan(locationTable.Controls[0], 6);
        }
        loadingLocation = false;
        FillLevel(0, line.Children);
    }

    private static int Depth(List<LocationNode> nodes) => nodes.Count == 0 ? 0 : 1 + nodes.Max(node => Depth(node.Children));

    private void FillLevel(int level, List<LocationNode> nodes)
    {
        if (level >= levelBoxes.Count) return;
        loadingLocation = true;
        ComboBox box = levelBoxes[level];
        box.Items.Clear();
        foreach (LocationNode node in nodes)
        {
            box.Items.Add(node);
        }
        box.Text = string.Empty;
        box.Enabled = true;
        for (int deeper = level + 1; deeper < levelBoxes.Count; deeper++)
        {
            levelBoxes[deeper].Items.Clear();
            levelBoxes[deeper].Text = string.Empty;
        }
        loadingLocation = false;
        if (nodes.Count == 1)
        {
            box.SelectedIndex = 0;
        }
        else
        {
            UpdateResultPath();
        }
    }

    private void OnLevelChanged(int level)
    {
        if (loadingLocation) return;
        var node = levelBoxes[level].SelectedItem as LocationNode;
        FillLevel(level + 1, node?.Children ?? new List<LocationNode>());
        UpdateResultPath();
    }

    private List<string> LocationChoices()
    {
        var choices = new List<string>();
        foreach (ComboBox box in levelBoxes)
        {
            string text = box.SelectedItem is LocationNode node ? node.FolderName : box.Text.Trim();
            if (text.Length == 0) break;
            choices.Add(text);
        }
        return choices;
    }

    private string ProjectFolder()
    {
        string root = rootBox.Text.Trim();
        if (root.Length == 0) return string.Empty;
        var parts = new List<string> { root };
        string lineFolder = SelectedProductLine?.Folder.Trim() ?? string.Empty;
        if (lineFolder.Length > 0) parts.Add(lineFolder);
        parts.AddRange(LocationChoices());
        parts.Add(folderNameBox.Text.Trim());
        return Path.Combine(parts.ToArray());
    }

    private void OnProjectTypeChanged()
    {
        ProjectTypeDef? type = SelectedType;
        if (type is null) return;
        projectTypeDescription.Text = type.Description;
        patternBox.Text = type.FileNamePattern;
        if (drawings.Count == 0 || autoNamed)
        {
            LoadTypeDrawings(false);
        }
    }

    private void OnJobNumberChanged()
    {
        if (templates is null) return;
        var tokens = Tokens();
        if (!folderNameEdited)
        {
            folderNameBox.Text = TemplateService.SafeName(TemplateService.Expand(templates.Job.FolderNamePattern, JobNumber(), 0, string.Empty, DateTime.Now, settings.DrawnBy, settings.DateFormat, tokens)).TrimEnd('_', '-', ' ');
        }
        if (!projectNameEdited)
        {
            projectNameBox.Text = TemplateService.SafeName(TemplateService.Expand(templates.Job.ProjectNamePattern, JobNumber(), 0, string.Empty, DateTime.Now, settings.DrawnBy, settings.DateFormat, tokens));
        }
        if (autoNamed) RegenerateFileNames();
        UpdateResultPath();
    }

    private void LoadTypeDrawings(bool confirm)
    {
        ProjectTypeDef? type = SelectedType;
        if (type is null) return;
        if (confirm && drawings.Count > 0 &&
            MessageBox.Show($"Replace the current {drawings.Count} drawing(s) with the {type.Drawings.Count} drawing(s) of \"{type.Name}\"?",
                "Load drawing set", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        var rows = new List<DrawingRow>();
        int previous = 0;
        foreach (DrawingDef definition in type.Drawings)
        {
            int number = definition.Sheet.Trim().Length > 0 ? ProjectTransactionService.SheetNumber(definition.Sheet, previous + 1) : previous + 1;
            previous = number;
            rows.Add(new DrawingRow
            {
                Sheet = number.ToString("000"),
                Description1 = definition.Description1,
                Description2 = definition.Description2,
                Description3 = definition.Description3,
                Section = definition.Section,
                Subsection = definition.Subsection,
                Folder = definition.Folder,
                Template = definition.Template
            });
        }
        drawings = new BindingList<DrawingRow>(rows);
        drawingGrid.DataSource = drawings;
        autoNamed = true;
        RegenerateFileNames();
    }

    private string Pattern() => patternBox.Text.Trim().Length > 0 ? patternBox.Text.Trim() : templates?.Job.FileNamePattern ?? "{JOB}-{SHEET:3}";

    private void RegenerateFileNames()
    {
        if (templates is null) return;
        drawingGrid.EndEdit();
        var tokens = Tokens();
        for (int index = 0; index < drawings.Count; index++)
        {
            int number = ProjectTransactionService.SheetNumber(drawings[index].Sheet, index + 1);
            drawings[index].FileName = TemplateService.Expand(Pattern(), JobNumber(), number, projectNameBox.Text.Trim(), DateTime.Now, settings.DrawnBy, settings.DateFormat, tokens);
        }
        drawingGrid.Refresh();
    }

    private void RenumberSequentially()
    {
        drawingGrid.EndEdit();
        for (int index = 0; index < drawings.Count; index++)
        {
            drawings[index].Sheet = (index + 1).ToString("000");
        }
        autoNamed = true;
        RegenerateFileNames();
    }

    private int NextSheetNumber()
    {
        int highest = 0;
        foreach (DrawingRow row in drawings)
        {
            highest = Math.Max(highest, ProjectTransactionService.SheetNumber(row.Sheet, 0));
        }
        return highest + 1;
    }

    private void AddDrawing()
    {
        drawingGrid.EndEdit();
        int position = drawingGrid.CurrentRow is null ? drawings.Count : drawingGrid.CurrentRow.Index + 1;
        int number = NextSheetNumber();
        var row = new DrawingRow
        {
            Sheet = number.ToString("000"),
            FileName = TemplateService.Expand(Pattern(), JobNumber(), number, projectNameBox.Text.Trim(), DateTime.Now, settings.DrawnBy, settings.DateFormat, Tokens()),
            Description1 = "DRAWING TYPE",
            Section = templates?.Sections.FirstOrDefault() ?? string.Empty
        };
        drawings.Insert(position, row);
        drawingGrid.CurrentCell = drawingGrid.Rows[position].Cells["Description1"];
    }

    private void RemoveDrawing()
    {
        drawingGrid.EndEdit();
        if (drawingGrid.CurrentRow is null) return;
        drawings.RemoveAt(drawingGrid.CurrentRow.Index);
    }

    private void MoveDrawing(int delta)
    {
        drawingGrid.EndEdit();
        if (drawingGrid.CurrentRow is null) return;
        int from = drawingGrid.CurrentRow.Index;
        int to = from + delta;
        if (to < 0 || to >= drawings.Count) return;
        DrawingRow row = drawings[from];
        drawings.RemoveAt(from);
        drawings.Insert(to, row);
        drawingGrid.CurrentCell = drawingGrid.Rows[to].Cells[0];
    }

    private string JobNumber() => jobNumberBox.Text.Trim();

    private string RoleValue(string role)
    {
        DescriptionLineDef? line = templates?.DescriptionLines.FirstOrDefault(item => item.HasRole(role));
        return line is not null && descriptionBoxes.TryGetValue(line.Index, out TextBox? box) ? box.Text.Trim() : string.Empty;
    }

    private Dictionary<string, string> Tokens() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["CUSTOMER"] = TemplateService.SafeName(RoleValue("customer")),
        ["QUOTE"] = TemplateService.SafeName(RoleValue("quote")),
        ["PRODUCTLINE"] = TemplateService.SafeName(SelectedProductLine?.Name ?? string.Empty)
    };

    private void UpdateResultPath()
    {
        string folder = ProjectFolder();
        string name = projectNameBox.Text.Trim();
        resultPathLabel.Text = folder.Length == 0 || folderNameBox.Text.Trim().Length == 0 || name.Length == 0
            ? "(fill in the jobs root, the job folder name and the project name)"
            : Path.Combine(folder, name + ".wdp");
    }

    private string? ValidateStep(int step)
    {
        if (templates is null) return "Templates are not loaded.";
        switch (step)
        {
            case 0:
                if (!TemplateService.JobNumberOk(templates.Job, JobNumber()))
                {
                    return templates.Job.NumberHint + (templates.Job.NumberExample.Length > 0 ? ". Example: " + templates.Job.NumberExample : string.Empty) + ".";
                }
                foreach (DescriptionLineDef line in templates.DescriptionLines.Where(line => line.Required))
                {
                    if (!descriptionBoxes.TryGetValue(line.Index, out TextBox? box) || box.Text.Trim().Length == 0)
                    {
                        return $"Enter description line {line.Index} ({line.Label}).";
                    }
                }
                return null;
            case 1:
                if (rootBox.Text.Trim().Length == 0 || !Path.IsPathRooted(rootBox.Text.Trim())) return "The jobs root must be a full path.";
                foreach (string choice in LocationChoices())
                {
                    if (choice.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return "Product line folder names cannot contain \\ / : * ? \" < > |: " + choice;
                }
                if (folderNameBox.Text.Trim().Length == 0 || folderNameBox.Text.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return "The job folder name must be a valid folder name.";
                if (projectNameBox.Text.Trim().Length == 0 || projectNameBox.Text.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return "The project name must be a valid file name.";
                if (!File.Exists(templateBox.Text.Trim())) return "The drawing template was not found: " + templateBox.Text;
                if (baseProjectBox.Text.Trim().Length > 0 && !File.Exists(baseProjectBox.Text.Trim())) return "The LSE template project was not found: " + baseProjectBox.Text;
                return null;
            case 2:
                drawingGrid.EndEdit();
                if (drawings.Count == 0) return "Add at least one drawing.";
                try
                {
                    ProjectTransactionService.Validate(templates, BuildRequest());
                    return null;
                }
                catch (System.Exception exception)
                {
                    return exception.Message;
                }
            default:
                return null;
        }
    }

    private void TryGoTo(int target)
    {
        if (target < 0 || target >= StepCount || templates is null) return;
        if (Result is not null && target != StepCount - 1)
        {
            MessageBox.Show("The project has been created. Close this wizard and run LSENEWPROJECT again for another project.", "LSE Project Tools");
            ShowStep(currentStep);
            return;
        }

        for (int step = currentStep; step < target; step++)
        {
            string? error = ValidateStep(step);
            if (error is not null)
            {
                statusLabel.Text = error;
                MessageBox.Show(error, StepTitles[step], MessageBoxButtons.OK, MessageBoxIcon.Warning);
                ShowStep(step);
                return;
            }
        }

        if (target >= 2 && currentStep < 2 && autoNamed)
        {
            RegenerateFileNames();
        }
        if (target == StepCount - 1)
        {
            BuildReview();
        }
        ShowStep(target);
    }

    private void ShowStep(int step)
    {
        switchingStep = true;
        currentStep = step;
        for (int index = 0; index < StepCount; index++)
        {
            stepPanels[index].Visible = index == step;
        }
        headerLabel.Text = $"Step {step + 1} of {StepCount}: {StepTitles[step]}";
        stepList.SelectedIndex = step;
        stepList.Invalidate();
        backButton.Enabled = step > 0 && Result is null;
        nextButton.Visible = step < StepCount - 1;
        createButton.Visible = step == StepCount - 1;
        createButton.Enabled = Result is null;
        switchingStep = false;
    }

    private ProjectRequest BuildRequest()
    {
        ProjectTypeDef? type = SelectedType;
        var request = new ProjectRequest
        {
            JobNumber = JobNumber(),
            Title = RoleValue("title"),
            Customer = RoleValue("customer"),
            Quote = RoleValue("quote"),
            ProductLine = SelectedProductLine?.Name ?? string.Empty,
            LocationChoices = LocationChoices(),
            ProjectFolder = ProjectFolder(),
            ProjectName = projectNameBox.Text.Trim(),
            BaseProjectPath = baseProjectBox.Text.Trim(),
            DrawingTemplatePath = templateBox.Text.Trim(),
            IecProject = iecProjectBox.Text.Trim(),
            IecInstallation = iecInstallationBox.Text.Trim(),
            IecLocation = iecLocationBox.Text.Trim(),
            ProjectType = type?.Name ?? string.Empty,
            FillTitleBlocks = fillTitleBlocksCheck.Checked,
            SetWdmValues = setWdmCheck.Checked,
            WriteWdtFile = writeWdtCheck.Checked,
            WriteWdlFile = writeWdlCheck.Checked,
            CopyTemplateSidecars = copySidecarsCheck.Checked,
            WriteAepxFile = writeAepxCheck.Checked,
            ActivateProject = activateCheck.Checked,
            OpenFirstDrawing = openFirstCheck.Checked,
            AllowExistingFolder = allowExistingCheck.Checked
        };
        foreach (KeyValuePair<int, TextBox> pair in descriptionBoxes)
        {
            request.DescriptionLines[pair.Key] = pair.Value.Text.Trim();
        }
        request.Drawings = drawings.Select(row => new DrawingRow
        {
            Sheet = row.Sheet.Trim(),
            FileName = row.FileName.Trim(),
            Description1 = row.Description1.Trim(),
            Description2 = row.Description2.Trim(),
            Description3 = row.Description3.Trim(),
            Section = row.Section.Trim(),
            Subsection = row.Subsection.Trim(),
            Folder = row.Folder.Trim(),
            Template = row.Template.Trim()
        }).ToList();
        return request;
    }

    private void BuildReview()
    {
        drawingGrid.EndEdit();
        ProjectRequest request = BuildRequest();
        string templateFolder = ProjectTransactionService.TemplateFolder(request);
        var lines = new List<string>
        {
            "JOB",
            $"  Job number        {request.JobNumber}",
            $"  Project name      {request.Title}",
            $"  Customer          {request.Customer}",
            $"  Product line      {request.ProductLine}{(request.LocationChoices.Count == 0 ? string.Empty : "  >  " + string.Join("  >  ", request.LocationChoices))}",
            $"  Drawing set       {request.ProjectType}",
            $"  Project file      {request.WdpPath}",
            $"  Template project  {(request.BaseProjectPath.Length == 0 ? "(none - AutoCAD Electrical defaults; run the E-CAD Doctor afterwards)" : request.BaseProjectPath)}",
            $"  Drawing template  {request.DrawingTemplatePath}",
            $"  IEC codes         P={request.IecProject} I={request.IecInstallation} L={request.IecLocation}",
            string.Empty,
            "DESCRIPTION LINES"
        };
        foreach (DescriptionLineDef line in templates!.DescriptionLines)
        {
            lines.Add($"  *[{line.Index}] {line.Label,-20} {line.Attribute,-24} {(request.DescriptionLines.TryGetValue(line.Index, out string? value) ? value : string.Empty)}");
        }
        lines.Add(string.Empty);
        lines.Add("OPTIONS");
        lines.Add($"  Fill title blocks: {request.FillTitleBlocks}   Set WD_M: {request.SetWdmValues}   Write .wdt: {request.WriteWdtFile}   Write .wdl: {request.WriteWdlFile}   Copy template sidecars: {request.CopyTemplateSidecars}   Write .aepx: {request.WriteAepxFile}");
        lines.Add($"  Activate project: {request.ActivateProject}   Open first drawing: {request.OpenFirstDrawing}   Allow existing folder: {request.AllowExistingFolder}");
        lines.Add(string.Empty);
        lines.Add($"DRAWINGS ({request.Drawings.Count})");
        lines.Add($"  {"Sheet",-6} {"File name",-26} {"Drawing type",-40} {"Template",-24} PM folder");
        foreach (DrawingRow row in request.Drawings)
        {
            string template = row.Template.Length == 0 ? "(drawing template)" : Path.GetFileName(ProjectTransactionService.RowTemplate(row, request, templateFolder));
            lines.Add($"  {row.Sheet,-6} {ProjectTransactionService.EnsureDwg(row.FileName),-26} {Truncate(row.Description1, 40),-40} {Truncate(template, 24),-24} {row.Folder}");
        }
        lines.Add(string.Empty);
        try
        {
            ProjectTransactionService.Validate(templates, request);
            lines.Add("Ready. Click 'Create project' to create the job folder, the drawings and the project files.");
            statusLabel.Text = "Ready to create " + request.WdpPath;
        }
        catch (System.Exception exception)
        {
            lines.Add("NOT READY: " + exception.Message);
            statusLabel.Text = exception.Message;
        }
        reviewBox.Text = string.Join(Environment.NewLine, lines);
    }

    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..(length - 1)] + "…";

    private void CreateProject()
    {
        if (templates is null || Result is not null) return;
        drawingGrid.EndEdit();
        ProjectRequest request = BuildRequest();
        reviewBox.Clear();
        createButton.Enabled = false;
        backButton.Enabled = false;
        closeButton.Enabled = false;
        UseWaitCursor = true;
        try
        {
            Result = ProjectTransactionService.Execute(settings, templates, request, line =>
            {
                reviewBox.AppendText(line + Environment.NewLine);
                reviewBox.Refresh();
            });
            statusLabel.Text = "Project created: " + Result.WdpPath;
            closeButton.Text = "Finish";
            MessageBox.Show(
                $"Project created.\r\n\r\n{Result.WdpPath}\r\n{Result.DrawingPaths.Count} drawing(s)\r\n\r\n" +
                (request.ActivateProject ? "The project will be activated in the Project Manager when this wizard closes." : "Open the project from the Project Manager (Open Project) to start working.") +
                "\r\n\r\nRemember to check the new job folder into the vault.",
                "LSE Project Tools", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (System.Exception exception)
        {
            reviewBox.AppendText(Environment.NewLine + "FAILED. Everything created by this run was rolled back." + Environment.NewLine);
            statusLabel.Text = "Project creation failed. Fix the problem and try again.";
            MessageBox.Show(exception.Message, "Project creation failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            createButton.Enabled = true;
            backButton.Enabled = true;
        }
        finally
        {
            closeButton.Enabled = true;
            UseWaitCursor = false;
        }
    }
}
