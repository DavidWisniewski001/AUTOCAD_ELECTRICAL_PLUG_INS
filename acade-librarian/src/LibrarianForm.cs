using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace LSE.AutoCADElectrical.Librarian;

public sealed class LibrarianForm : Form
{
    private const int StepCount = 4;
    private static readonly string[] StepTitles =
    {
        "What are you filing?",
        "Standard name and library folder",
        "Icon menu, icon image and footprint lookup",
        "Review and add"
    };

    private readonly AppSettings settings = SettingsService.Load();
    private readonly string? prefillSource;
    private LibraryStandard? standard;
    private KindRule? rule;
    private IconMenu? menu;
    private LibraryKind menuKind;
    private bool imagesLoaded;
    private int currentStep;
    private bool switchingStep;
    private bool suspendNamePreview;
    private string? tempExportPath;
    private string? tempExportBlock;

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
    private readonly Button addButton = new() { Text = "Add to library", Dock = DockStyle.Fill };
    private readonly Button closeButton = new() { Text = "Cancel", Dock = DockStyle.Fill };

    // Step 1: item and source
    private readonly FlowLayoutPanel kindPanel = new() { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
    private readonly Dictionary<LibraryKind, RadioButton> kindButtons = new();
    private readonly RadioButton fileSourceButton = new() { Text = "A drawing file (.dwg) saved anywhere", AutoSize = true, Checked = true };
    private readonly RadioButton blockSourceButton = new() { Text = "A block definition in the current drawing (exported with WBLOCK)", AutoSize = true };
    private readonly TextBox sourceBox = new() { Dock = DockStyle.Fill };
    private readonly ComboBox blockBox = new() { DropDownStyle = ComboBoxStyle.DropDown, Dock = DockStyle.Fill };
    private readonly TextBox descriptionBox = new() { Dock = DockStyle.Fill, MaxLength = 150 };
    private readonly TextBox inspectionBox = new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Both,
        WordWrap = false,
        Font = new Font("Consolas", 9.5f),
        BackColor = Color.White
    };

    // Step 2: name and folder
    private readonly TableLayoutPanel nameGrid = NewGrid();
    private readonly Dictionary<string, (Label Label, Control Control)> nameRows = new();
    private readonly ComboBox orientationBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly ComboBox familyBox = new() { DropDownStyle = ComboBoxStyle.DropDown, Dock = DockStyle.Fill, AutoCompleteMode = AutoCompleteMode.SuggestAppend, AutoCompleteSource = AutoCompleteSource.ListItems };
    private readonly ComboBox roleBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly TextBox variantBox = new() { Dock = DockStyle.Fill, MaxLength = 2, CharacterCasing = CharacterCasing.Upper };
    private readonly ComboBox prefixBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly TextBox manufacturerBox = new() { Dock = DockStyle.Fill, MaxLength = 20, CharacterCasing = CharacterCasing.Upper };
    private readonly TextBox seriesBox = new() { Dock = DockStyle.Fill, MaxLength = 3 };
    private readonly TextBox namePartBox = new() { Dock = DockStyle.Fill, MaxLength = 64, CharacterCasing = CharacterCasing.Upper };
    private readonly CheckBox customNameCheck = new() { Text = "Type the full name myself (it must still pass the standard)", AutoSize = true };
    private readonly TextBox customNameBox = new() { Dock = DockStyle.Fill, MaxLength = 80, Enabled = false, CharacterCasing = CharacterCasing.Upper };
    private readonly Label nameLabel = new() { AutoSize = true, Font = new Font("Segoe UI", 12f, FontStyle.Bold), Text = "(no name yet)" };
    private readonly Label nameProblemsLabel = new() { AutoSize = true, ForeColor = Color.Firebrick, MaximumSize = new Size(900, 0) };
    private readonly Label nameHelpLabel = new() { AutoSize = true, ForeColor = Color.FromArgb(80, 80, 80), MaximumSize = new Size(900, 0) };
    private readonly ComboBox categoryBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly TextBox subfolderBox = new() { Dock = DockStyle.Fill };
    private readonly Label targetLabel = new() { AutoSize = true, MaximumSize = new Size(900, 0), ForeColor = Color.FromArgb(80, 80, 80) };

    // Step 3: menu, icon, lookup
    private readonly Label menuFileLabel = new() { AutoSize = true, MaximumSize = new Size(900, 0) };
    private readonly ComboBox pageBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly CheckBox newPageCheck = new() { Text = "Create a new page for it instead", AutoSize = true };
    private readonly TextBox newPageTitleBox = new() { Dock = DockStyle.Fill, MaxLength = 60, Enabled = false };
    private readonly ComboBox parentPageBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, Enabled = false };
    private readonly TextBox labelBox = new() { Dock = DockStyle.Fill, MaxLength = 80 };
    private readonly RadioButton renderIconButton = new() { Text = "Render from the drawing geometry", AutoSize = true, Checked = true };
    private readonly RadioButton thumbnailIconButton = new() { Text = "Use the drawing's embedded preview image", AutoSize = true };
    private readonly RadioButton existingIconButton = new() { Text = "Reuse an existing image from the images folder", AutoSize = true };
    private readonly RadioButton importIconButton = new() { Text = "Import an image file (png, bmp, jpg)", AutoSize = true };
    private readonly RadioButton noIconButton = new() { Text = "No icon image", AutoSize = true };
    private readonly ComboBox existingImageBox = new() { DropDownStyle = ComboBoxStyle.DropDown, Dock = DockStyle.Fill, AutoCompleteMode = AutoCompleteMode.SuggestAppend, AutoCompleteSource = AutoCompleteSource.ListItems };
    private readonly TextBox importImageBox = new() { Dock = DockStyle.Fill };
    private readonly PictureBox previewPicture = new() { Size = new Size(96, 96), SizeMode = PictureBoxSizeMode.Zoom, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White };
    private readonly CheckBox lookupCheck = new() { Text = "Register in footprint_lookup.mdb (catalog number resolves to this footprint)", AutoSize = true };
    private readonly TextBox lookupManufacturerBox = new() { Dock = DockStyle.Fill, MaxLength = 24, CharacterCasing = CharacterCasing.Upper };
    private readonly TextBox lookupCatalogBox = new() { Dock = DockStyle.Fill, MaxLength = 60 };
    private readonly GroupBox lookupGroup;
    private readonly GroupBox catalogGroup;
    private readonly CheckBox catalogCheck = new() { Text = "Also add a catalog record to default_cat.mdb with a new LSE part number", AutoSize = true };
    private readonly Label catalogSummaryLabel = new() { AutoSize = true, MaximumSize = new Size(900, 0), ForeColor = Color.FromArgb(80, 80, 80), Text = "(none yet)" };
    private PartNumberRequest? partRequest;
    private CatalogAddRequest? catalogRequest;

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

    public LibraryResult? Result { get; private set; }

    public LibrarianForm(string? sourcePath)
    {
        prefillSource = sourcePath;
        Text = "LSE Librarian";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1400, 920);
        MinimumSize = new Size(1100, 720);
        lookupGroup = new GroupBox { Text = "Footprint lookup", Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8) };
        catalogGroup = new GroupBox { Text = "Catalog record (default_cat.mdb, LSE part number from the parts catalog workbook)", Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8) };
        BuildInterface();
        Shown += (_, _) => LoadReferenceData();
        FormClosed += (_, _) => CleanupTemp();
    }

    private void BuildInterface()
    {
        var left = new Panel { Dock = DockStyle.Left, Width = 250, Padding = new Padding(8), BackColor = Color.FromArgb(240, 242, 245) };
        stepList.BackColor = left.BackColor;
        stepList.Items.AddRange(new object[] { "1   Item", "2   Name & folder", "3   Menu & icon", "4   Review & add" });
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
        addButton.Click += (_, _) => Add();
        closeButton.Click += (_, _) => Close();
        footer.Controls.Add(statusLabel, 0, 0);
        footer.Controls.Add(backButton, 1, 0);
        footer.Controls.Add(nextButton, 2, 0);
        footer.Controls.Add(addButton, 3, 0);
        footer.Controls.Add(closeButton, 4, 0);

        stepPanels[0] = BuildItemStep();
        stepPanels[1] = BuildNameStep();
        stepPanels[2] = BuildMenuStep();
        stepPanels[3] = BuildReviewStep();
        foreach (Panel panel in stepPanels)
        {
            panel.Dock = DockStyle.Fill;
            panel.Visible = false;
            stepHost.Controls.Add(panel);
        }

        var right = new Panel { Dock = DockStyle.Fill };
        right.Controls.Add(stepHost);
        right.Controls.Add(headerLabel);
        Controls.Add(right);
        Controls.Add(footer);
        Controls.Add(left);
    }

    private Panel BuildItemStep()
    {
        var grid = NewGrid();
        int row = 0;
        AddRow(grid, "Kind of library item", kindPanel, row++);

        var sources = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown };
        sources.Controls.Add(fileSourceButton);
        sources.Controls.Add(blockSourceButton);
        AddRow(grid, "Where it is now", sources, row++);
        AddRow(grid, "Drawing file", sourceBox, row++, CreateButton("Browse...", BrowseSource));
        AddRow(grid, "Block in current drawing", blockBox, row++, CreateButton("Pick in drawing", PickBlock), CreateButton("Refresh list", RefreshBlocks));
        AddRow(grid, "Plain description (menu label, lookup text)", descriptionBox, row++);
        AddRow(grid, "Inspection", new Label { Text = "Opens the drawing and checks attributes and extents against the standard.", AutoSize = true, ForeColor = Color.FromArgb(80, 80, 80) }, row++, CreateButton("Inspect drawing", Inspect));

        fileSourceButton.CheckedChanged += (_, _) => SourceModeChanged();
        blockSourceButton.CheckedChanged += (_, _) => SourceModeChanged();
        sourceBox.TextChanged += (_, _) => { tempExportBlock = null; };

        var panel = new Panel { Padding = new Padding(12) };
        panel.Controls.Add(inspectionBox);
        grid.Dock = DockStyle.Top;
        panel.Controls.Add(grid);
        return panel;
    }

    private Panel BuildNameStep()
    {
        int row = 0;
        orientationBox.Items.AddRange(new object[] { "H  horizontal (ladder rung)", "V  vertical" });
        orientationBox.SelectedIndex = 0;
        roleBox.Items.AddRange(new object[] { "1  parent (carries the tag)", "2  child (contact, auxiliary)" });
        roleBox.SelectedIndex = 0;

        AddNameRow("orientation", "Orientation (H or V)", orientationBox, row++);
        AddNameRow("family", "AutoCAD Electrical family code", familyBox, row++);
        AddNameRow("role", "Parent or child", roleBox, row++);
        AddNameRow("variant", "Variant (0 to 2 characters, optional)", variantBox, row++);
        AddNameRow("prefix", "Prefix", prefixBox, row++);
        AddNameRow("manufacturer", "Manufacturer (EATON, WEID, LSE, ...)", manufacturerBox, row++);
        AddNameRow("series", "Series (3 digits)", seriesBox, row++);
        AddNameRow("namepart", "Description part of the name", namePartBox, row++);
        AddNameRow("custom", "Custom name", customNameBox, row++);
        nameGrid.Controls.Add(customNameCheck, 1, row);
        nameGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        row++;
        AddNameRow("name", "Resulting name", nameLabel, row++);
        AddNameRow("help", "", nameHelpLabel, row++);
        AddNameRow("problems", "", nameProblemsLabel, row++);
        AddNameRow("category", "Library category", categoryBox, row++);
        AddNameRow("subfolder", "Subfolder inside the category (optional)", subfolderBox, row++);
        AddNameRow("target", "Will be saved as", targetLabel, row++);

        foreach (Control control in new Control[] { orientationBox, familyBox, roleBox, variantBox, prefixBox, manufacturerBox, seriesBox, namePartBox, customNameBox, subfolderBox })
        {
            control.TextChanged += (_, _) => UpdateNamePreview();
        }
        foreach (ComboBox combo in new[] { orientationBox, familyBox, roleBox, prefixBox, categoryBox })
        {
            combo.SelectedIndexChanged += (_, _) => UpdateNamePreview();
        }
        customNameCheck.CheckedChanged += (_, _) =>
        {
            customNameBox.Enabled = customNameCheck.Checked;
            if (customNameCheck.Checked && customNameBox.Text.Trim().Length == 0)
            {
                customNameBox.Text = ComputeName();
            }
            UpdateNamePreview();
        };

        var panel = new Panel { Padding = new Padding(12), AutoScroll = true };
        nameGrid.Dock = DockStyle.Top;
        panel.Controls.Add(nameGrid);
        return panel;
    }

    private Panel BuildMenuStep()
    {
        var grid = NewGrid();
        int row = 0;
        AddRow(grid, "Icon menu file", menuFileLabel, row++);
        AddRow(grid, "Menu page", pageBox, row++, CreateButton("Reload menu", () => LoadMenu(true)));
        AddRow(grid, "", newPageCheck, row++);
        AddRow(grid, "New page title", newPageTitleBox, row++);
        AddRow(grid, "Parent page for the new page", parentPageBox, row++);
        AddRow(grid, "Menu label (what engineers click)", labelBox, row++);

        var icons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown };
        icons.Controls.AddRange(new Control[] { renderIconButton, thumbnailIconButton, existingIconButton, importIconButton, noIconButton });
        AddRow(grid, "Icon image", icons, row++, CreateButton("Preview icon", PreviewIcon));
        AddRow(grid, "Existing image", existingImageBox, row++, CreateButton("Reload images", LoadImages));
        AddRow(grid, "Image file to import", importImageBox, row++, CreateButton("Browse...", BrowseImage));
        AddRow(grid, "Preview", previewPicture, row++);

        var lookupGrid = NewGrid();
        int lookupRow = 0;
        AddRow(lookupGrid, "", lookupCheck, lookupRow++);
        AddRow(lookupGrid, "MANUFACTURER (table, max 24)", lookupManufacturerBox, lookupRow++);
        AddRow(lookupGrid, "Catalog number (max 60)", lookupCatalogBox, lookupRow++);
        lookupGrid.Dock = DockStyle.Fill;
        lookupGroup.Controls.Add(lookupGrid);

        var catalogGrid = NewGrid();
        int catalogRow = 0;
        AddRow(catalogGrid, "", catalogCheck, catalogRow++);
        AddRow(catalogGrid, "Record", catalogSummaryLabel, catalogRow++, CreateButton("Catalog record...", EditCatalogRecord));
        catalogGrid.Dock = DockStyle.Fill;
        catalogGroup.Controls.Add(catalogGrid);
        catalogCheck.CheckedChanged += (_, _) =>
        {
            if (catalogCheck.Checked && catalogRequest is null && !switchingStep)
            {
                EditCatalogRecord();
            }
        };

        newPageCheck.CheckedChanged += (_, _) =>
        {
            newPageTitleBox.Enabled = newPageCheck.Checked;
            parentPageBox.Enabled = newPageCheck.Checked;
            pageBox.Enabled = !newPageCheck.Checked;
        };
        existingIconButton.CheckedChanged += (_, _) => { if (existingIconButton.Checked && !imagesLoaded) LoadImages(); };
        existingImageBox.SelectedIndexChanged += (_, _) => { if (existingIconButton.Checked) PreviewIcon(); };
        lookupCheck.CheckedChanged += (_, _) =>
        {
            lookupManufacturerBox.Enabled = lookupCheck.Checked;
            lookupCatalogBox.Enabled = lookupCheck.Checked;
        };

        var panel = new Panel { Padding = new Padding(12), AutoScroll = true };
        panel.Controls.Add(catalogGroup);
        panel.Controls.Add(lookupGroup);
        grid.Dock = DockStyle.Top;
        panel.Controls.Add(grid);
        panel.Controls.SetChildIndex(grid, 2);
        return panel;
    }

    private Panel BuildReviewStep()
    {
        var panel = new Panel { Padding = new Padding(12) };
        panel.Controls.Add(reviewBox);
        return panel;
    }

    private void LoadReferenceData()
    {
        try
        {
            standard = LibraryStandardService.Load(settings.LibraryStandardPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "LSE Librarian", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
            return;
        }

        suspendNamePreview = true;
        foreach (LibraryKind kind in Enum.GetValues<LibraryKind>())
        {
            var button = new RadioButton { Text = standard.Rule(kind).DisplayName, AutoSize = true, Margin = new Padding(3, 3, 18, 3), Tag = kind };
            button.CheckedChanged += (_, _) => { if (button.Checked) KindChanged(); };
            kindButtons[kind] = button;
            kindPanel.Controls.Add(button);
        }

        familyBox.Items.Clear();
        foreach (KeyValuePair<string, string> family in standard.SymbolFamilies.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            familyBox.Items.Add(family.Key.ToUpperInvariant() + "  " + family.Value);
        }
        familyBox.Text = "DV";
        seriesBox.Text = settings.DefaultSeries;
        lookupCheck.Checked = settings.RegisterFootprintLookup;
        lookupManufacturerBox.Enabled = lookupCheck.Checked;
        lookupCatalogBox.Enabled = lookupCheck.Checked;

        LibraryKind initial = LibraryKind.SchematicSymbol;
        if (prefillSource is not null)
        {
            sourceBox.Text = prefillSource;
            fileSourceButton.Checked = true;
            initial = NamingService.KindFor(settings, prefillSource) ?? initial;
        }
        kindButtons[initial].Checked = true;
        suspendNamePreview = false;
        KindChanged();

        if (prefillSource is not null)
        {
            PrefillFromName(Path.GetFileNameWithoutExtension(prefillSource));
        }

        try
        {
            RefreshBlocks();
        }
        catch
        {
            // No active drawing, or AutoCAD refused: the list stays empty.
        }

        SourceModeChanged();
        ShowStep(0);
    }

    private LibraryKind SelectedKind => kindButtons.FirstOrDefault(pair => pair.Value.Checked).Key;

    private void KindChanged()
    {
        if (standard is null || suspendNamePreview)
        {
            return;
        }

        rule = standard.Rule(SelectedKind);
        bool symbol = SelectedKind == LibraryKind.SchematicSymbol;
        bool footprint = SelectedKind is LibraryKind.PanelFootprint or LibraryKind.Nameplate;
        bool circuit = SelectedKind == LibraryKind.UserCircuit;

        ShowNameRow("orientation", symbol);
        ShowNameRow("family", symbol);
        ShowNameRow("role", symbol);
        ShowNameRow("variant", symbol);
        ShowNameRow("prefix", footprint);
        ShowNameRow("manufacturer", footprint);
        ShowNameRow("series", symbol || footprint);
        nameRows["namepart"].Label.Text = circuit ? "Circuit name" : "Description part of the name";
        nameHelpLabel.Text = rule.NameHelp + "  Example: " + rule.NameExample;

        prefixBox.Items.Clear();
        foreach (string prefix in rule.Prefixes)
        {
            prefixBox.Items.Add(prefix);
        }
        if (prefixBox.Items.Count > 0)
        {
            prefixBox.SelectedIndex = 0;
        }

        categoryBox.Items.Clear();
        string root = settings.RootFor(SelectedKind);
        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (LibraryCategory category in rule.Categories)
        {
            categoryBox.Items.Add(category);
            known.Add(category.Folder);
        }
        if (Directory.Exists(root) && !circuit)
        {
            foreach (string folder in Directory.EnumerateDirectories(root).Select(d => Path.GetFileName(d) ?? "").Where(name => name.Length > 0 && !NamingService.IsIgnoredFolder(name, standard) && !known.Contains(name)).OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
            {
                categoryBox.Items.Add(new LibraryCategory { Name = "(existing folder) " + folder, Folder = folder, MenuPage = "" });
            }
        }
        if (!rule.Categories.Any(category => category.Folder.Length == 0) && !circuit)
        {
            categoryBox.Items.Add(new LibraryCategory { Name = "(library root)", Folder = "", MenuPage = "" });
        }
        if (categoryBox.Items.Count > 0)
        {
            categoryBox.SelectedIndex = 0;
        }

        lookupGroup.Visible = footprint;
        catalogGroup.Visible = !circuit && settings.OfferCatalogRecord;
        menu = null;
        pageBox.Items.Clear();
        parentPageBox.Items.Clear();
        UpdateNamePreview();
    }

    private void PrefillFromName(string name)
    {
        if (rule is null)
        {
            return;
        }

        Match match = NamingService.Parse(rule, name);
        descriptionBox.Text = (match.Success && match.Groups["description"].Success ? match.Groups["description"].Value : name).Replace('_', ' ');
        if (!match.Success)
        {
            customNameCheck.Checked = true;
            customNameBox.Text = name;
            return;
        }

        suspendNamePreview = true;
        if (match.Groups["orientation"].Success)
        {
            orientationBox.SelectedIndex = match.Groups["orientation"].Value.Equals("V", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        }
        if (match.Groups["family"].Success)
        {
            familyBox.Text = match.Groups["family"].Value;
        }
        if (match.Groups["role"].Success)
        {
            roleBox.SelectedIndex = match.Groups["role"].Value == "2" ? 1 : 0;
        }
        if (match.Groups["variant"].Success)
        {
            variantBox.Text = match.Groups["variant"].Value;
        }
        if (match.Groups["prefix"].Success)
        {
            int index = prefixBox.Items.IndexOf(match.Groups["prefix"].Value.ToUpperInvariant());
            if (index >= 0)
            {
                prefixBox.SelectedIndex = index;
            }
        }
        if (match.Groups["manufacturer"].Success)
        {
            manufacturerBox.Text = match.Groups["manufacturer"].Value;
        }
        if (match.Groups["series"].Success)
        {
            seriesBox.Text = match.Groups["series"].Value;
        }
        if (match.Groups["description"].Success)
        {
            namePartBox.Text = match.Groups["description"].Value;
        }
        suspendNamePreview = false;

        string root = settings.RootFor(SelectedKind);
        if (NamingService.Inside(prefillSource ?? "", root))
        {
            string relativeFolder = Path.GetDirectoryName(NamingService.Relative(prefillSource!, root)) ?? "";
            string top = relativeFolder.Split('\\', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
            for (int index = 0; index < categoryBox.Items.Count; index++)
            {
                if (categoryBox.Items[index] is LibraryCategory category && category.Folder.Equals(SelectedKind == LibraryKind.UserCircuit ? relativeFolder : top, StringComparison.OrdinalIgnoreCase))
                {
                    categoryBox.SelectedIndex = index;
                    break;
                }
            }
            if (SelectedKind != LibraryKind.UserCircuit && relativeFolder.Length > top.Length)
            {
                subfolderBox.Text = relativeFolder[(top.Length + 1)..];
            }
        }
        UpdateNamePreview();
    }

    private void SourceModeChanged()
    {
        bool file = fileSourceButton.Checked;
        sourceBox.Enabled = file;
        blockBox.Enabled = !file;
        thumbnailIconButton.Enabled = file;
        if (!file && thumbnailIconButton.Checked)
        {
            renderIconButton.Checked = true;
        }
    }

    private string ComputeName()
    {
        if (customNameCheck.Checked)
        {
            return customNameBox.Text.Trim();
        }

        return SelectedKind switch
        {
            LibraryKind.SchematicSymbol => NamingService.SymbolName(
                orientationBox.SelectedIndex == 1 ? "V" : "H",
                FamilyCode(),
                roleBox.SelectedIndex == 1 ? "2" : "1",
                variantBox.Text,
                seriesBox.Text,
                namePartBox.Text),
            LibraryKind.UserCircuit => NamingService.Sanitize(namePartBox.Text, true),
            _ => NamingService.FootprintName(prefixBox.SelectedItem?.ToString() ?? (rule?.Prefixes.FirstOrDefault() ?? "FP"), seriesBox.Text, manufacturerBox.Text, namePartBox.Text),
        };
    }

    private string FamilyCode()
    {
        string text = familyBox.Text.Trim();
        int space = text.IndexOf(' ');
        return (space > 0 ? text[..space] : text).ToUpperInvariant();
    }

    private string TargetFolder()
    {
        string root = settings.RootFor(SelectedKind);
        string folder = (categoryBox.SelectedItem as LibraryCategory)?.Folder ?? "";
        string sub = subfolderBox.Text.Trim().Trim('\\', '/');
        string path = root;
        if (folder.Length > 0)
        {
            path = Path.Combine(path, folder);
        }
        if (sub.Length > 0)
        {
            path = Path.Combine(path, sub);
        }
        return path;
    }

    private void UpdateNamePreview()
    {
        if (rule is null || suspendNamePreview)
        {
            return;
        }

        string name = ComputeName();
        nameLabel.Text = name.Length == 0 ? "(no name yet)" : name;
        var problems = NamingService.Validate(rule, name);
        string target = Path.Combine(TargetFolder(), name + ".dwg");
        if (name.Length > 0 && File.Exists(target) && !IsInPlace(target))
        {
            problems.Add("A drawing with this name already exists at the target location.");
        }
        nameProblemsLabel.Text = string.Join(Environment.NewLine, problems);
        targetLabel.Text = target;
        if (labelBox.Text.Trim().Length == 0 || labelBox.Tag is null)
        {
            labelBox.Text = DefaultLabel();
            labelBox.Tag = null;
        }
    }

    private bool IsInPlace(string target)
    {
        try
        {
            return fileSourceButton.Checked && sourceBox.Text.Trim().Length > 0 &&
                   Path.GetFullPath(sourceBox.Text.Trim()).Equals(Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private string DefaultLabel()
    {
        string description = descriptionBox.Text.Trim();
        if (description.Length > 0)
        {
            return description.Length > 60 ? description[..60] : description;
        }
        string part = namePartBox.Text.Trim().Replace('_', ' ');
        return part.Length > 0 ? part : ComputeName();
    }

    private void BrowseSource()
    {
        using var dialog = new OpenFileDialog { Title = "Choose the drawing to file in the library", Filter = "Drawing (*.dwg)|*.dwg", FileName = sourceBox.Text };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            sourceBox.Text = dialog.FileName;
            fileSourceButton.Checked = true;
            if (descriptionBox.Text.Trim().Length == 0 && rule is not null)
            {
                PrefillFromName(Path.GetFileNameWithoutExtension(dialog.FileName));
            }
        }
    }

    private void BrowseImage()
    {
        using var dialog = new OpenFileDialog { Title = "Choose the icon image", Filter = "Images (*.png;*.bmp;*.jpg;*.jpeg;*.gif)|*.png;*.bmp;*.jpg;*.jpeg;*.gif", FileName = importImageBox.Text };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            importImageBox.Text = dialog.FileName;
            importIconButton.Checked = true;
            PreviewIcon();
        }
    }

    private void RefreshBlocks()
    {
        string current = blockBox.Text;
        blockBox.Items.Clear();
        foreach (string name in DrawingInspectionService.ListBlockNames())
        {
            blockBox.Items.Add(name);
        }
        blockBox.Text = current;
    }

    private void PickBlock()
    {
        Guard(() =>
        {
            string? name = DrawingInspectionService.PromptForBlock();
            if (name is null)
            {
                return;
            }
            blockBox.Text = name;
            blockSourceButton.Checked = true;
            tempExportBlock = null;
            if (descriptionBox.Text.Trim().Length == 0 && rule is not null)
            {
                PrefillFromName(name);
            }
        });
    }

    private string SourceDrawing()
    {
        if (fileSourceButton.Checked)
        {
            string path = sourceBox.Text.Trim();
            if (path.Length == 0 || !File.Exists(path))
            {
                throw new FileNotFoundException("Choose an existing .dwg file first.", path);
            }
            return path;
        }

        string block = blockBox.Text.Trim();
        if (block.Length == 0)
        {
            throw new InvalidOperationException("Choose or pick the block to export first.");
        }
        if (tempExportPath is not null && File.Exists(tempExportPath) && block.Equals(tempExportBlock, StringComparison.OrdinalIgnoreCase))
        {
            return tempExportPath;
        }
        CleanupTemp();
        tempExportPath = DrawingInspectionService.ExportBlockToTemp(block);
        tempExportBlock = block;
        return tempExportPath;
    }

    private void Inspect()
    {
        Guard(() =>
        {
            string path = SourceDrawing();
            DrawingInfo info = DrawingInspectionService.Inspect(path);
            var lines = new List<string>
            {
                "Drawing: " + path,
                $"Format: {info.DwgVersion}   Units: {info.Units}   Entities in model space: {info.EntityCount}   Embedded preview: {(info.HasThumbnail ? "yes" : "no")}",
                info.HasExtents ? $"Extents: {info.Width:0.###} x {info.Height:0.###} ({info.MinX:0.###},{info.MinY:0.###}) to ({info.MaxX:0.###},{info.MaxY:0.###})" : "Extents: none (no visible geometry)",
                ""
            };
            lines.Add(info.Attributes.Count == 0 ? "Attributes: none" : "Attributes (" + info.Attributes.Count + "):");
            foreach (AttributeInfo attribute in info.Attributes.OrderBy(a => a.Tag, StringComparer.OrdinalIgnoreCase))
            {
                lines.Add($"  {attribute.Tag,-14} {(attribute.Invisible ? "invisible" : "visible  ")} {(attribute.Constant ? "constant " : "")} default=\"{attribute.Default}\"  prompt=\"{attribute.Prompt}\"");
            }
            if (info.BlockReferences.Count > 0)
            {
                lines.Add("");
                lines.Add("Nested blocks: " + string.Join(", ", info.BlockReferences.Distinct(StringComparer.OrdinalIgnoreCase)));
            }
            lines.Add("");
            if (rule is not null)
            {
                var checks = RuleCheckService.Check(rule, info);
                lines.Add(checks.Count == 0 ? "Standard check: PASS" : "Standard check (" + rule.DisplayName + "):");
                foreach (var (severity, message) in checks)
                {
                    lines.Add($"  {severity.ToString().ToUpperInvariant(),-8} {message}");
                }
            }
            inspectionBox.Text = string.Join(Environment.NewLine, lines);
            statusLabel.Text = "Inspected " + Path.GetFileName(path);
        });
    }

    private void LoadMenu(bool force)
    {
        if (rule is null)
        {
            return;
        }
        if (menu is not null && menuKind == SelectedKind && !force)
        {
            return;
        }

        string path = settings.MenuPathFor(rule);
        menuFileLabel.Text = path;
        menu = IconMenuService.Load(path);
        menuKind = SelectedKind;
        pageBox.Items.Clear();
        parentPageBox.Items.Clear();
        var tree = IconMenuService.Tree(menu, rule.MenuRootPages);
        if (tree.Count == 0)
        {
            var crumbs = IconMenuService.Breadcrumbs(menu);
            tree = menu.Pages.Select(page => (page, crumbs.TryGetValue(page.Id, out var trail) ? trail : page.Title)).ToList();
        }
        foreach (var (page, trail) in tree)
        {
            var item = new PageItem(page, trail);
            pageBox.Items.Add(item);
            parentPageBox.Items.Add(item);
        }

        string wanted = (categoryBox.SelectedItem as LibraryCategory)?.MenuPage ?? "";
        int selected = -1;
        for (int index = 0; index < pageBox.Items.Count; index++)
        {
            if (pageBox.Items[index] is PageItem item && item.Page.Id.Equals(wanted, StringComparison.OrdinalIgnoreCase))
            {
                selected = index;
                break;
            }
        }
        pageBox.SelectedIndex = selected >= 0 ? selected : (pageBox.Items.Count > 0 ? 0 : -1);
        parentPageBox.SelectedIndex = parentPageBox.Items.Count > 0 ? 0 : -1;
    }

    private void LoadImages()
    {
        existingImageBox.Items.Clear();
        if (Directory.Exists(settings.ImagesFolder))
        {
            foreach (string file in Directory.EnumerateFiles(settings.ImagesFolder).Where(f => Path.GetExtension(f).ToLowerInvariant() is ".png" or ".bmp" or ".jpg" or ".jpeg" or ".gif").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                existingImageBox.Items.Add(Path.GetFileName(file));
            }
        }
        imagesLoaded = true;
    }

    private IconSource SelectedIcon()
    {
        if (thumbnailIconButton.Checked) return IconSource.Thumbnail;
        if (existingIconButton.Checked) return IconSource.ExistingImage;
        if (importIconButton.Checked) return IconSource.ImageFile;
        if (noIconButton.Checked) return IconSource.None;
        return IconSource.Render;
    }

    private string IconPath()
    {
        return SelectedIcon() switch
        {
            IconSource.ExistingImage => Path.Combine(settings.ImagesFolder, existingImageBox.Text.Trim()),
            IconSource.ImageFile => importImageBox.Text.Trim(),
            _ => ""
        };
    }

    private void PreviewIcon()
    {
        Guard(() =>
        {
            IconSource source = SelectedIcon();
            previewPicture.Image?.Dispose();
            previewPicture.Image = null;
            if (source == IconSource.None)
            {
                return;
            }
            string drawing = source is IconSource.Render or IconSource.Thumbnail ? SourceDrawing() : "";
            using Bitmap bitmap = IconRenderService.Build(source, drawing, IconPath(), settings.IconPixels);
            previewPicture.Image = new Bitmap(bitmap);
            statusLabel.Text = $"Icon preview: {settings.IconPixels} x {settings.IconPixels} px";
        });
    }

    private string? ValidateStep(int step)
    {
        if (rule is null)
        {
            return "The library standard is not loaded.";
        }

        switch (step)
        {
            case 0:
                if (fileSourceButton.Checked)
                {
                    string path = sourceBox.Text.Trim();
                    if (path.Length == 0 || !File.Exists(path)) return "Choose an existing .dwg file.";
                    if (!path.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase)) return "The source must be a .dwg file.";
                }
                else if (blockBox.Text.Trim().Length == 0)
                {
                    return "Choose or pick the block in the current drawing.";
                }
                return null;
            case 1:
            {
                string name = ComputeName();
                var problems = NamingService.Validate(rule, name);
                if (problems.Count > 0) return string.Join(Environment.NewLine, problems);
                string root = settings.RootFor(SelectedKind);
                if (root.Length == 0 || !Directory.Exists(root)) return "The library folder for this kind is not set or does not exist (LSELIBRARYSETTINGS): " + root;
                string target = Path.Combine(TargetFolder(), name + ".dwg");
                if (File.Exists(target) && !IsInPlace(target)) return "A drawing with this name already exists: " + target;
                string? clash = LibraryTransactionService.FindByName(root, name + ".dwg", standard!);
                if (clash is not null && !clash.Equals(target, StringComparison.OrdinalIgnoreCase)) return "That name is already used elsewhere in the library: " + clash;
                return null;
            }
            case 2:
                if (menu is null) return "The icon menu is not loaded.";
                if (newPageCheck.Checked)
                {
                    if (newPageTitleBox.Text.Trim().Length == 0) return "Give the new page a title.";
                    if (parentPageBox.SelectedItem is null) return "Choose the parent page for the new page.";
                }
                else if (pageBox.SelectedItem is null)
                {
                    return "Choose the icon menu page.";
                }
                if (labelBox.Text.Trim().Length == 0) return "Type the menu label.";
                if (labelBox.Text.Contains('|')) return "The menu label cannot contain '|'.";
                if (existingIconButton.Checked && (existingImageBox.Text.Trim().Length == 0 || !File.Exists(IconPath()))) return "Choose an existing image from the images folder.";
                if (importIconButton.Checked && !File.Exists(importImageBox.Text.Trim())) return "Choose the image file to import.";
                if (lookupGroup.Visible && lookupCheck.Checked)
                {
                    if (lookupManufacturerBox.Text.Trim().Length == 0 || lookupCatalogBox.Text.Trim().Length == 0) return "Enter MANUFACTURER and catalog number for the footprint lookup, or untick the registration box.";
                }
                if (catalogGroup.Visible && catalogCheck.Checked)
                {
                    if (catalogRequest is null || partRequest is null) return "Press 'Catalog record...' to fill in the catalog record, or untick the catalog option.";
                    try
                    {
                        CatalogTransactionService.Validate(settings, NamingWorkbookService.Load(settings), partRequest, catalogRequest);
                    }
                    catch (Exception ex)
                    {
                        return "Catalog record: " + ex.Message;
                    }
                }
                return null;
            default:
                return null;
        }
    }

    private void TryGoTo(int step)
    {
        if (step < 0 || step >= StepCount || step == currentStep)
        {
            ShowStep(currentStep);
            return;
        }

        if (step > currentStep)
        {
            for (int index = currentStep; index < step; index++)
            {
                string? problem = ValidateStep(index);
                if (problem is not null)
                {
                    ShowStep(index);
                    MessageBox.Show(this, problem, "LSE Librarian", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }
        }

        ShowStep(step);
    }

    private void ShowStep(int step)
    {
        currentStep = step;
        switchingStep = true;
        stepList.SelectedIndex = step;
        switchingStep = false;
        headerLabel.Text = StepTitles[step];
        for (int index = 0; index < StepCount; index++)
        {
            stepPanels[index].Visible = index == step;
        }
        backButton.Enabled = step > 0;
        nextButton.Enabled = step < StepCount - 1;
        addButton.Visible = step == StepCount - 1;
        addButton.Enabled = step == StepCount - 1 && Result is null;

        if (step == 2)
        {
            Guard(() => LoadMenu(false));
            if (!imagesLoaded)
            {
                LoadImages();
            }
            if (labelBox.Text.Trim().Length == 0)
            {
                labelBox.Text = DefaultLabel();
            }
        }
        if (step == 3 && Result is null)
        {
            reviewBox.Text = Review();
        }
        stepList.Invalidate();
    }

    private LibraryRequest BuildRequest()
    {
        var request = new LibraryRequest
        {
            Kind = SelectedKind,
            SourcePath = fileSourceButton.Checked ? sourceBox.Text.Trim() : "",
            SourceBlockName = blockSourceButton.Checked ? blockBox.Text.Trim() : "",
            Name = ComputeName(),
            TargetFolder = TargetFolder(),
            Description = descriptionBox.Text.Trim().Length > 0 ? descriptionBox.Text.Trim() : labelBox.Text.Trim(),
            MenuPage = (pageBox.SelectedItem as PageItem)?.Page.Id ?? "",
            CreatePage = newPageCheck.Checked,
            NewPageTitle = newPageTitleBox.Text.Trim(),
            ParentPage = (parentPageBox.SelectedItem as PageItem)?.Page.Id ?? "",
            MenuLabel = labelBox.Text.Trim(),
            Icon = SelectedIcon(),
            IconPath = IconPath(),
            RegisterLookup = lookupGroup.Visible && lookupCheck.Checked,
            LookupManufacturer = lookupManufacturerBox.Text.Trim(),
            LookupCatalog = lookupCatalogBox.Text.Trim(),
            AddCatalogRecord = catalogGroup.Visible && catalogCheck.Checked && catalogRequest is not null && partRequest is not null,
            PartNumber = partRequest,
            CatalogRecord = catalogRequest
        };
        return request;
    }

    private string Review()
    {
        LibraryRequest request = BuildRequest();
        var lines = new List<string>
        {
            "LSE Librarian will:",
            "",
            $"  Kind            {rule?.DisplayName}",
            $"  Source          {(request.FromCurrentDrawing ? "block " + request.SourceBlockName + " in the current drawing (WBLOCK)" : request.SourcePath)}",
            $"  Library file    {request.TargetPath}" + (request.InPlace ? "   (already in place, not copied)" : ""),
            $"  Icon menu       {(rule is null ? "" : settings.MenuPathFor(rule))}",
            request.CreatePage
                ? $"  Menu page       new page \"{request.NewPageTitle}\" under {request.ParentPage}"
                : $"  Menu page       {request.MenuPage}  {(pageBox.SelectedItem as PageItem)?.Trail}",
            $"  Menu label      {request.MenuLabel}",
            $"  Icon            {request.Icon}{(request.IconPath.Length > 0 ? "  " + request.IconPath : "")}" + (request.Icon is IconSource.Render or IconSource.Thumbnail or IconSource.ImageFile ? $"  ->  {Path.Combine(settings.ImagesFolder, request.Name + ".png")}" : ""),
            request.RegisterLookup
                ? $"  Lookup row      [{request.LookupManufacturer}] {request.LookupCatalog} -> {request.TargetPath}"
                : "  Lookup row      none",
            request.AddCatalogRecord
                ? $"  Catalog record  {request.CatalogRecord!.Summary}  part number {request.PartNumber!.Prefix}{(request.PartNumber.IsManual ? " = " + request.PartNumber.ManualPartNumber : "+ next sequence, issued at run time")}  (catalog backed up next to it)"
                : "  Catalog record  none",
            $"  Backups         {settings.BackupFolder}\\<timestamp>\\",
            "",
            "Every step is logged. If any step fails, the icon menu and lookup database are restored from the backup and the copied drawing and icon are removed.",
            "",
            "Press 'Add to library' to proceed."
        };
        return string.Join(Environment.NewLine, lines);
    }

    private void Add()
    {
        if (standard is null)
        {
            return;
        }

        string? problem = ValidateStep(0) ?? ValidateStep(1) ?? ValidateStep(2);
        if (problem is not null)
        {
            MessageBox.Show(this, problem, "LSE Librarian", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        LibraryRequest request = BuildRequest();
        reviewBox.Text = "";
        addButton.Enabled = false;
        Cursor = Cursors.WaitCursor;
        try
        {
            Result = LibraryTransactionService.Execute(settings, standard, request, line =>
            {
                reviewBox.AppendText(line + Environment.NewLine);
                reviewBox.Update();
            });
            statusLabel.Text = "Added " + Result.DrawingPath;
            closeButton.Text = "Close";
            MessageBox.Show(this, "Filed in the library:" + Environment.NewLine + Result.DrawingPath + Environment.NewLine + Environment.NewLine + "Icon menu page " + Result.MenuPage + " in " + Path.GetFileName(Result.MenuPath) + ". Open the icon menu in AutoCAD Electrical to see it." + (Result.PartNumber.Length > 0 ? Environment.NewLine + Environment.NewLine + "Catalog record added with LSE part number " + Result.PartNumber + "." : ""), "LSE Librarian");
        }
        catch (Exception ex)
        {
            reviewBox.Text = ex.Message;
            addButton.Enabled = true;
            MessageBox.Show(this, ex.Message, "LSE Librarian: nothing was changed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void EditCatalogRecord()
    {
        Guard(() =>
        {
            CatalogAddRequest seed = catalogRequest ?? new CatalogAddRequest
            {
                Manufacturer = lookupManufacturerBox.Text.Trim(),
                Catalog = lookupCatalogBox.Text.Trim(),
                Description = descriptionBox.Text.Trim().ToUpperInvariant(),
                WdBlkNam = SelectedKind == LibraryKind.SchematicSymbol ? ComputeName() : ""
            };
            using var form = new CatalogRecordForm(settings, true, seed, partRequest);
            if (form.ShowDialog(this) != DialogResult.OK || form.CatalogRequest is null || form.PartRequest is null)
            {
                if (catalogRequest is null)
                {
                    catalogCheck.Checked = false;
                }
                return;
            }
            catalogRequest = form.CatalogRequest;
            partRequest = form.PartRequest;
            catalogCheck.Checked = true;
            catalogSummaryLabel.Text = $"{catalogRequest.Summary}   part number {partRequest.Prefix}{(partRequest.IsManual ? " = " + partRequest.ManualPartNumber : "+ next sequence (issued when the item is added)")}";
            if (lookupGroup.Visible)
            {
                if (lookupManufacturerBox.Text.Trim().Length == 0) lookupManufacturerBox.Text = catalogRequest.Manufacturer;
                if (lookupCatalogBox.Text.Trim().Length == 0) lookupCatalogBox.Text = catalogRequest.Catalog;
            }
        });
    }

    private void CleanupTemp()
    {
        if (tempExportPath is not null)
        {
            try
            {
                File.Delete(tempExportPath);
            }
            catch
            {
                // Temporary export; a leftover file in %TEMP% is harmless.
            }
            tempExportPath = null;
            tempExportBlock = null;
        }
    }

    private void Guard(Action action)
    {
        Cursor = Cursors.WaitCursor;
        try
        {
            action();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "LSE Librarian", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void DrawStepItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0)
        {
            return;
        }

        bool current = e.Index == currentStep;
        using var background = new SolidBrush(current ? Color.FromArgb(0, 102, 180) : stepList.BackColor);
        e.Graphics.FillRectangle(background, e.Bounds);
        using var font = new Font(stepList.Font, current ? FontStyle.Bold : FontStyle.Regular);
        TextRenderer.DrawText(e.Graphics, stepList.Items[e.Index].ToString(), font, new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height), current ? Color.White : Color.Black, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
    }

    private void AddNameRow(string key, string label, Control control, int row)
    {
        var labelControl = new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left };
        nameGrid.Controls.Add(labelControl, 0, row);
        nameGrid.Controls.Add(control, 1, row);
        nameGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        nameRows[key] = (labelControl, control);
    }

    private void ShowNameRow(string key, bool visible)
    {
        if (nameRows.TryGetValue(key, out var row))
        {
            row.Label.Visible = visible;
            row.Control.Visible = visible;
        }
    }

    private static TableLayoutPanel NewGrid()
    {
        var grid = new TableLayoutPanel { AutoSize = true, ColumnCount = 4, Padding = new Padding(0, 0, 0, 8) };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        return grid;
    }

    private static void AddRow(TableLayoutPanel grid, string label, Control control, int row, Control? button = null, Control? secondButton = null)
    {
        grid.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        grid.Controls.Add(control, 1, row);
        if (button is not null)
        {
            grid.Controls.Add(button, 2, row);
        }
        if (secondButton is not null)
        {
            grid.Controls.Add(secondButton, 3, row);
        }
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    }

    private static Button CreateButton(string text, Action action)
    {
        var button = new Button { Text = text, AutoSize = true, Height = 30, Padding = new Padding(6, 0, 6, 0), Anchor = AnchorStyles.Left };
        button.Click += (_, _) => action();
        return button;
    }

    private sealed record PageItem(IconMenuPage Page, string Trail)
    {
        public override string ToString() => Page.Id.PadRight(6) + Trail;
    }
}
