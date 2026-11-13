using System.Drawing;
using System.Windows.Forms;
using Inv = Inventor;
namespace LSE.InventorFootprintTools;

public sealed class FootprintWizardForm : Form
{
    private readonly Inv.Application application;
    private readonly InventorGeometryService geometryService;
    private readonly AppSettings settings = SettingsService.Load();
    private NamingStandard? standard;
    private Inv.Document? model;
    private FootprintGeometry? geometry;

    // 1. Model
    private readonly TextBox modelPath = new() { ReadOnly = true };
    private readonly ComboBox scaleCorrection = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Label extentsLabel = new() { AutoSize = true, Text = "(import a model)" };
    private readonly Button browseButton, importButton;

    // 2. View
    private readonly Dictionary<FootprintView, RadioButton> viewButtons = new();
    private readonly CheckBox hiddenLines = new() { Text = "Include hidden lines", AutoSize = true };
    private readonly CheckBox tangentEdges = new() { Text = "Include tangent edges", AutoSize = true };
    private readonly CheckBox outlineOnly = new() { Text = "Outline rectangle only (recommended for STL meshes)", AutoSize = true };
    private readonly Label geometryLabel = new() { AutoSize = true, Text = "(project a view)" };
    private readonly Button previewButton, projectButton;

    // 3. Part number and catalog record
    private readonly ComboBox tabBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox categoryBox = new();
    private readonly ComboBox typeBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox manufacturerCodeBox = new();
    private readonly TextBox manufacturerBox = new() { MaxLength = 24 };
    private readonly TextBox catalogBox = new() { MaxLength = 60 };
    private readonly TextBox descriptionBox = new() { MaxLength = 100 };
    private readonly TextBox webLinkBox = new() { MaxLength = 255 };
    private readonly ComboBox familyBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Label partNumberLabel = new() { AutoSize = true, Font = new Font(FontFamily.GenericSansSerif, 11, FontStyle.Bold), Text = "(not issued yet)" };
    private readonly Button previewPartNumberButton, createButton;

    // 4. Log
    private readonly TextBox log = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Font = new Font(FontFamily.GenericMonospace, 8.5f), MinimumSize = new Size(0, 160) };

    public FootprintWizardForm(Inv.Application app)
    {
        application = app;
        geometryService = new InventorGeometryService(app);
        Text = "LSE Footprint + Part Number";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1000, 940);
        MinimumSize = new Size(820, 700);

        browseButton = CreateButton("Browse...", Browse);
        importButton = CreateButton("Import into Inventor", Import);
        previewButton = CreateButton("Preview view in Inventor", Preview);
        projectButton = CreateButton("Project view to footprint", Project);
        previewPartNumberButton = CreateButton("Preview next part number", PreviewPartNumber);
        createButton = CreateButton("Generate footprint and add to catalog", Create);
        createButton.Font = new Font(FontFamily.GenericSansSerif, 10, FontStyle.Bold);
        createButton.Height = 40;

        Build();
        Shown += (_, _) => LoadReferenceData();
    }

    private void Build()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(12), AutoScroll = true };
        for (int i = 0; i < 4; i++) root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var step1 = NewGrid();
        int row = 0;
        AddRow(step1, "STL / STEP file", modelPath, row++, browseButton);
        scaleCorrection.Items.AddRange(new object[]
        {
            "No correction (model units were read correctly)",
            "Model was in millimetres but imported as inches (multiply by 25.4)",
            "Model was in inches but imported as millimetres (divide by 25.4)",
        });
        scaleCorrection.SelectedIndex = 0;
        AddRow(step1, "Unit correction", scaleCorrection, row++, importButton);
        AddRow(step1, "Model extents", extentsLabel, row++);
        root.Controls.Add(Group("1. Model", step1), 0, 0);

        var step2 = NewGrid();
        var views = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
        foreach (FootprintView view in Enum.GetValues<FootprintView>())
        {
            var button = new RadioButton
            {
                Text = view == FootprintView.Current ? "Current Inventor view" : view.ToString(),
                AutoSize = true,
                Checked = view == FootprintView.Top,
                Margin = new Padding(3, 3, 14, 3),
            };
            viewButtons[view] = button;
            views.Controls.Add(button);
        }
        var options = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
        options.Controls.AddRange(new Control[] { hiddenLines, tangentEdges, outlineOnly });
        row = 0;
        AddRow(step2, "View to project", views, row++, previewButton);
        AddRow(step2, "Options", options, row++, projectButton);
        AddRow(step2, "Footprint geometry", geometryLabel, row++);
        root.Controls.Add(Group("2. View to footprint", step2), 0, 1);

        var step3 = NewGrid();
        row = 0;
        AddRow(step3, "LSE catalog tab", tabBox, row++);
        AddRow(step3, "Category code (2 digits)", categoryBox, row++);
        AddRow(step3, "Type code (2 letters)", typeBox, row++);
        AddRow(step3, "Manufacturer code (3 letters)", manufacturerCodeBox, row++);
        AddRow(step3, "MANUFACTURER (AutoCAD Electrical, max 24)", manufacturerBox, row++);
        AddRow(step3, "Catalog number (MPN, max 60)", catalogBox, row++);
        AddRow(step3, "Description (LSE standard, no MPN, max 100)", descriptionBox, row++);
        AddRow(step3, "Datasheet / web link", webLinkBox, row++);
        AddRow(step3, "AutoCAD Electrical family table", familyBox, row++);
        AddRow(step3, "Part number", partNumberLabel, row++, previewPartNumberButton);
        root.Controls.Add(Group("3. Part number and catalog record", step3), 0, 2);

        var step4 = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 1, Padding = new Padding(6) };
        createButton.Dock = DockStyle.Fill;
        step4.Controls.Add(createButton, 0, 0);
        root.Controls.Add(Group("4. Create", step4), 0, 3);

        root.Controls.Add(log, 0, 4);
        Controls.Add(root);

        tabBox.SelectedIndexChanged += (_, _) => TabChanged();
        categoryBox.TextChanged += (_, _) => SelectFamilyHint();
        manufacturerCodeBox.SelectedIndexChanged += (_, _) => ManufacturerChanged();
        manufacturerCodeBox.TextChanged += (_, _) => ManufacturerChanged();
        outlineOnly.CheckedChanged += (_, _) => { hiddenLines.Enabled = tangentEdges.Enabled = !outlineOnly.Checked; };
    }

    private void LoadReferenceData()
    {
        Guard(() =>
        {
            standard = NamingStandardService.Load(settings.NamingStandardPath);
            tabBox.Items.Clear();
            foreach (NamingTab tab in standard.Tabs) tabBox.Items.Add(tab);
            if (tabBox.Items.Count > 0) tabBox.SelectedIndex = 0;
            Log($"Naming standard: {standard.Tabs.Count} tabs, {standard.Manufacturers.Count} manufacturer codes ({standard.Source}).");
        });
        Guard(() =>
        {
            var service = new AccessCatalogService(settings.CatalogPath);
            Dictionary<string, string> names = service.GetFamilyDescriptions();
            familyBox.Items.Clear();
            foreach (string table in service.GetTables())
                familyBox.Items.Add(new FamilyItem(table, names.TryGetValue(table, out string? name) ? name : ""));
            Log($"Catalog: {settings.CatalogPath} ({familyBox.Items.Count} family tables).");
            Log($"Footprint lookup: {settings.FootprintLookupPath}");
            Log($"Footprint library: {settings.FootprintLibraryFolder} ({settings.FootprintUnits})");
            SelectFamilyHint();
        });
        if (settings.ConvertToDwgWithAutoCad && !AutoCadConvertService.IsInstalled())
            Log("WARNING: AutoCAD is not registered on this PC, so the DXF to DWG step will fail. Turn the setting off or run on a workstation with AutoCAD Electrical.");
    }

    private void TabChanged()
    {
        if (tabBox.SelectedItem is not NamingTab tab || standard is null) return;

        categoryBox.Items.Clear();
        foreach (string code in tab.CategoryCodes) categoryBox.Items.Add(code);
        if (categoryBox.Items.Count > 0) categoryBox.SelectedIndex = 0; else categoryBox.Text = "";

        typeBox.Items.Clear();
        foreach (var pair in tab.Types) typeBox.Items.Add(new CodeItem(pair.Key, pair.Value));
        if (typeBox.Items.Count > 0) typeBox.SelectedIndex = 0;

        manufacturerCodeBox.Items.Clear();
        foreach (var pair in tab.Manufacturers) manufacturerCodeBox.Items.Add(new CodeItem(pair.Key, pair.Value));
        foreach (var pair in standard.Manufacturers.OrderBy(p => p.Key, StringComparer.Ordinal))
            if (!tab.Manufacturers.ContainsKey(pair.Key)) manufacturerCodeBox.Items.Add(new CodeItem(pair.Key, pair.Value.Name));
        if (manufacturerCodeBox.Items.Count > 0) manufacturerCodeBox.SelectedIndex = 0;

        descriptionBox.PlaceholderText = tab.DescriptionPattern;
        partNumberLabel.Text = "(not issued yet)";
    }

    private void ManufacturerChanged()
    {
        if (standard is null) return;
        string code = Code(manufacturerCodeBox);
        if (standard.Manufacturers.TryGetValue(code, out ManufacturerCode? entry))
        {
            string value = entry.AcadeManufacturer.Length > 0 ? entry.AcadeManufacturer : entry.Name.ToUpperInvariant();
            manufacturerBox.Text = value.Length > 24 ? value[..24] : value;
        }
        else if (manufacturerCodeBox.SelectedItem is CodeItem item)
        {
            string value = item.Name.ToUpperInvariant();
            manufacturerBox.Text = value.Length > 24 ? value[..24] : value;
        }
        partNumberLabel.Text = "(not issued yet)";
    }

    private void SelectFamilyHint()
    {
        partNumberLabel.Text = "(not issued yet)";
        if (standard is null || familyBox.Items.Count == 0) return;
        if (!standard.FamilyHints.TryGetValue(categoryBox.Text.Trim(), out List<string>? hints) || hints.Count == 0) return;
        foreach (string hint in hints)
            for (int i = 0; i < familyBox.Items.Count; i++)
                if (familyBox.Items[i] is FamilyItem item && item.Code.Equals(hint, StringComparison.OrdinalIgnoreCase))
                {
                    familyBox.SelectedIndex = i;
                    return;
                }
    }

    private void Browse()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Select the 3D model to turn into a footprint",
            Filter = "3D models (*.stl;*.stp;*.step;*.ipt;*.iam)|*.stl;*.stp;*.step;*.ipt;*.iam|All files (*.*)|*.*",
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            modelPath.Text = dialog.FileName;
            if (dialog.FileName.EndsWith(".stl", StringComparison.OrdinalIgnoreCase)) outlineOnly.Checked = true;
        }
    }

    private void Import()
    {
        Guard(() =>
        {
            if (modelPath.Text.Length == 0) throw new InvalidOperationException("Choose an STL or STEP file first.");
            model = geometryService.ImportModel(modelPath.Text);
            geometry = null;
            geometryLabel.Text = "(project a view)";
            ShowExtents();
            Log($"Imported {modelPath.Text} as {model.DisplayName}.");
        });
    }

    private void ShowExtents()
    {
        if (model is null) return;
        var (x, y, z) = geometryService.ModelExtentsCm(model);
        bool metric = settings.FootprintUnits.Equals("mm", StringComparison.OrdinalIgnoreCase);
        double factor = (metric ? 10.0 : 1.0 / 2.54) * ScaleCorrection();
        extentsLabel.Text = $"{x * factor:0.###} x {y * factor:0.###} x {z * factor:0.###} {settings.FootprintUnits} (X x Y x Z, after unit correction). If this is 25.4 times off, change the unit correction.";
    }

    private void Preview()
    {
        Guard(() =>
        {
            if (model is null) throw new InvalidOperationException("Import the model first.");
            geometryService.PreviewView(SelectedView());
        });
    }

    private void Project()
    {
        Guard(() =>
        {
            if (model is null) throw new InvalidOperationException("Import the model first.");
            ShowExtents();
            geometry = geometryService.Project(model, Request(), settings);
            geometryLabel.Text = $"{geometry.Curves.Count} curve(s), {geometry.Width:0.###} x {geometry.Height:0.###} {geometry.Units}, {SelectedView()} view";
            Log("Projected: " + geometryLabel.Text);
        });
    }

    private void PreviewPartNumber()
    {
        Guard(() =>
        {
            if (standard is null) throw new InvalidOperationException("The naming standard is not loaded.");
            PartNumberRequest request = PartRequest();
            PartNumberService.Validate(standard.Format, request);
            List<string> existing = new AccessCatalogService(settings.CatalogPath).ReadUser1StartingWith(request.Prefix);
            string next = PartNumberService.Next(standard.Format, request.Prefix, existing);
            partNumberLabel.Text = $"{next}  (preview; the number is assigned when the record is created)";
            Log($"Next {request.Prefix} number would be {next}; {existing.Count} existing value(s) start with {request.Prefix}.");
        });
    }

    private void Create()
    {
        Guard(() =>
        {
            if (standard is null) throw new InvalidOperationException("The naming standard is not loaded.");
            if (model is null) throw new InvalidOperationException("Import the model first (step 1).");
            if (geometry is null) throw new InvalidOperationException("Project a view first (step 2).");
            if (familyBox.SelectedItem is not FamilyItem family) throw new InvalidOperationException("Choose the AutoCAD Electrical family table.");

            PartNumberRequest partRequest = PartRequest();
            PartNumberService.Validate(standard.Format, partRequest);

            string manufacturer = manufacturerBox.Text.Trim().ToUpperInvariant();
            string catalog = catalogBox.Text.Trim();
            string description = descriptionBox.Text.Trim().ToUpperInvariant();
            if (manufacturer.Length == 0 || catalog.Length == 0) throw new InvalidOperationException("MANUFACTURER and Catalog number are required.");
            if (description.Length == 0) throw new InvalidOperationException("Description is required.");
            if (description.Any(c => c > 127)) throw new InvalidOperationException("Description may only contain plain ASCII characters (LSE rule: no special symbols).");
            if (description.Contains(catalog, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Do not put the MPN in the description; it is appended automatically as \", MPN <catalog>\".");

            var catalogRequest = new CatalogAddRequest
            {
                TableName = family.Code,
                Catalog = catalog,
                Manufacturer = manufacturer,
                Description = settings.AppendMpnToDescription ? $"{description}, MPN {catalog}" : description,
                WebLink = webLinkBox.Text.Trim(),
            };

            string summary = $"Family table: {family}\nManufacturer: {manufacturer}\nCatalog: {catalog}\nDescription: {catalogRequest.Description}\nPart number prefix: {partRequest.Prefix}\nFootprint: {geometry.Curves.Count} curve(s), {geometry.Width:0.###} x {geometry.Height:0.###} {geometry.Units}\n\nThis writes to default_cat.mdb and footprint_lookup.mdb and creates the footprint drawing. Continue?";
            if (MessageBox.Show(this, summary, "Create part", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            createButton.Enabled = false;
            try
            {
                FootprintResult result = FootprintTransactionService.Execute(settings, standard, partRequest, catalogRequest, geometry, description);
                partNumberLabel.Text = result.PartNumber;
                geometry = null;
                geometryLabel.Text = $"used for {result.PartNumber}; project again for another part";
                Log(result.Report);
                MessageBox.Show(this, $"Part number {result.PartNumber} was added to {family.Code}.\nFootprint: {(result.DwgPath.Length > 0 ? result.DwgPath : result.DxfPath)}", "Part created", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            finally
            {
                createButton.Enabled = true;
            }
        });
    }

    private PartNumberRequest PartRequest() => new()
    {
        Category = categoryBox.Text.Trim(),
        ManufacturerCode = Code(manufacturerCodeBox),
        Type = Code(typeBox),
    };

    private FootprintRequest Request() => new()
    {
        SourceFile = modelPath.Text,
        View = SelectedView(),
        HiddenLines = hiddenLines.Checked,
        TangentEdges = tangentEdges.Checked,
        OutlineOnly = outlineOnly.Checked,
        ScaleCorrection = ScaleCorrection(),
    };

    private double ScaleCorrection() => scaleCorrection.SelectedIndex switch { 1 => 25.4, 2 => 1 / 25.4, _ => 1.0 };

    private FootprintView SelectedView() => viewButtons.First(pair => pair.Value.Checked).Key;

    private static string Code(ComboBox box)
    {
        if (box.SelectedItem is CodeItem item) return item.Code.ToUpperInvariant();
        string text = box.Text.Trim();
        int split = text.IndexOfAny(new[] { ' ', '-' });
        return (split > 0 ? text[..split] : text).ToUpperInvariant();
    }

    private void Guard(Action action)
    {
        try
        {
            Cursor = Cursors.WaitCursor;
            action();
        }
        catch (Exception ex)
        {
            Log("ERROR: " + ex.Message);
            MessageBox.Show(this, ex.Message, "LSE Footprint Tools", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void Log(string message)
    {
        log.AppendText($"{DateTime.Now:HH:mm:ss}  {message}{Environment.NewLine}");
    }

    private static TableLayoutPanel NewGrid()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 3, Padding = new Padding(6) };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230));
        return panel;
    }

    private static void AddRow(TableLayoutPanel panel, string label, Control content, int row, Control? button = null)
    {
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        content.Dock = DockStyle.Fill;
        panel.Controls.Add(content, 1, row);
        if (button is not null)
        {
            button.Dock = DockStyle.Fill;
            panel.Controls.Add(button, 2, row);
        }
        else
        {
            panel.SetColumnSpan(content, 2);
        }
    }

    private static GroupBox Group(string title, Control content)
    {
        var group = new GroupBox { Text = title, Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(8) };
        group.Controls.Add(content);
        return group;
    }

    private static Button CreateButton(string text, Action action)
    {
        var button = new Button { Text = text, AutoSize = true, Height = 30 };
        button.Click += (_, _) => action();
        return button;
    }

    private sealed record CodeItem(string Code, string Name)
    {
        public override string ToString() => Name.Length > 0 ? $"{Code} - {Name}" : Code;
    }

    private sealed record FamilyItem(string Code, string Name)
    {
        public override string ToString() => Name.Length > 0 ? $"{Code} - {Name}" : Code;
    }
}
