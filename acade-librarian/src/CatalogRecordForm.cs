using System.Drawing;
using System.Windows.Forms;

namespace LSE.AutoCADElectrical.Librarian;

// Adds a part to default_cat.mdb under a new LSE part number built from the naming scheme in the parts catalog
// workbook: pick the workbook tab, the category / manufacturer / type codes it defines, and the dialog shows the next
// unused sequence (checked against every family table of the catalog and the numbers already listed in the workbook).
// Stand-alone (LSECATALOGADD) it writes the record itself; embedded in the Librarian wizard it hands the request back
// so the record is written inside the library transaction.
public sealed class CatalogRecordForm : Form
{
    private readonly AppSettings settings;
    private readonly bool embedded;
    private readonly CatalogAddRequest? prefill;
    private readonly PartNumberRequest? prefillPart;
    private NamingStandard? standard;
    private CatalogService? catalog;
    private Dictionary<string, string> familyNames = new(StringComparer.OrdinalIgnoreCase);
    private PartNumberPreview? preview;
    private string previewKey = "";
    private bool familyChosenByUser;
    private bool loading;
    private readonly List<(DescriptionField Field, TextBox Box)> fieldBoxes = new();

    public PartNumberRequest? PartRequest { get; private set; }
    public CatalogAddRequest? CatalogRequest { get; private set; }
    public CatalogAddResult? Result { get; private set; }

    private readonly Label sourceLabel = new() { AutoSize = true, MaximumSize = new Size(760, 0), ForeColor = Color.FromArgb(80, 80, 80) };
    private readonly ComboBox tabBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly ComboBox categoryBox = new() { DropDownStyle = ComboBoxStyle.DropDown, Dock = DockStyle.Fill, MaxLength = 2 };
    private readonly Label categoryRuleLabel = new() { AutoSize = true, MaximumSize = new Size(760, 0), ForeColor = Color.FromArgb(80, 80, 80) };
    private readonly ComboBox manufacturerCodeBox = new() { DropDownStyle = ComboBoxStyle.DropDown, Dock = DockStyle.Fill, AutoCompleteMode = AutoCompleteMode.SuggestAppend, AutoCompleteSource = AutoCompleteSource.ListItems };
    private readonly ComboBox typeBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly Label partNumberLabel = new() { AutoSize = true, Font = new Font("Segoe UI", 13f, FontStyle.Bold), Text = "(press Preview)" };
    private readonly Label partNumberDetailLabel = new() { AutoSize = true, MaximumSize = new Size(760, 0), ForeColor = Color.FromArgb(80, 80, 80) };
    private readonly CheckBox manualCheck = new() { Text = "Keep an existing LSE number instead (rule 7): type the full part number", AutoSize = true };
    private readonly TextBox manualBox = new() { Dock = DockStyle.Fill, MaxLength = 20, CharacterCasing = CharacterCasing.Upper, Enabled = false };
    private readonly ComboBox familyBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly Label familyHintLabel = new() { AutoSize = true, MaximumSize = new Size(760, 0), ForeColor = Color.FromArgb(80, 80, 80) };
    private readonly ComboBox manufacturerBox = new() { DropDownStyle = ComboBoxStyle.DropDown, Dock = DockStyle.Fill, MaxLength = 24, AutoCompleteMode = AutoCompleteMode.SuggestAppend, AutoCompleteSource = AutoCompleteSource.ListItems };
    private readonly TextBox catalogBox = new() { Dock = DockStyle.Fill, MaxLength = 60 };
    private readonly TextBox descriptionBox = new() { Dock = DockStyle.Fill, MaxLength = 150, CharacterCasing = CharacterCasing.Upper };
    private readonly Label descriptionPatternLabel = new() { AutoSize = true, MaximumSize = new Size(760, 0), ForeColor = Color.FromArgb(80, 80, 80) };
    private readonly Label descriptionProblemsLabel = new() { AutoSize = true, MaximumSize = new Size(760, 0), ForeColor = Color.Firebrick };
    private readonly TableLayoutPanel fieldsGrid = new() { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Fill };
    private readonly CheckBox purchaseCheck = new() { Text = "USER2 = purchase description (Description + \", MPN \" + catalog number)", AutoSize = true };
    private readonly TextBox user2Box = new() { Dock = DockStyle.Fill, MaxLength = 100 };
    private readonly TextBox typeTextBox = new() { Dock = DockStyle.Fill, MaxLength = 100 };
    private readonly TextBox ratingBox = new() { Dock = DockStyle.Fill, MaxLength = 100 };
    private readonly TextBox voltageBox = new() { Dock = DockStyle.Fill, MaxLength = 100 };
    private readonly TextBox user3Box = new() { Dock = DockStyle.Fill, MaxLength = 100 };
    private readonly TextBox textValueBox = new() { Dock = DockStyle.Fill, MaxLength = 255 };
    private readonly TextBox webLinkBox = new() { Dock = DockStyle.Fill, MaxLength = 255 };
    private readonly TextBox wdBlkNamBox = new() { Dock = DockStyle.Fill, MaxLength = 60 };
    private readonly CheckBox mapCheck = new() { Text = "Also map this part to a footprint drawing (adds a footprint_lookup.mdb row)", AutoSize = true };
    private readonly TextBox mapBox = new() { Dock = DockStyle.Fill, Enabled = false };
    private readonly Button mapBrowse = new() { Text = "Browse...", AutoSize = true, Height = 30, Padding = new Padding(6, 0, 6, 0), Anchor = AnchorStyles.Left, Enabled = false };
    private readonly TextBox helpBox = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 9f), BackColor = Color.White, Height = 150, MinimumSize = new Size(0, 150) };
    private readonly TextBox logBox = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Font = new Font("Consolas", 9f), BackColor = Color.White, Height = 110, MinimumSize = new Size(0, 110) };
    private readonly Label statusLabel = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
    private readonly Button previewButton = new() { Text = "Preview next number", Dock = DockStyle.Fill };
    private readonly Button addButton = new() { Dock = DockStyle.Fill };
    private readonly Button closeButton = new() { Text = "Cancel", Dock = DockStyle.Fill };

    public CatalogRecordForm(AppSettings settings, bool embedded, CatalogAddRequest? prefill, PartNumberRequest? prefillPart)
    {
        this.settings = settings;
        this.embedded = embedded;
        this.prefill = prefill;
        this.prefillPart = prefillPart;
        Text = embedded ? "LSE Librarian: catalog record" : "LSE Librarian: add a catalog record (catalog only, no symbol or footprint)";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1180, 940);
        MinimumSize = new Size(960, 700);
        addButton.Text = embedded ? "Use in Librarian" : "Add to catalog";
        BuildInterface();
        Shown += (_, _) => LoadReferenceData();
    }

    private void BuildInterface()
    {
        var grid = new TableLayoutPanel { AutoSize = true, ColumnCount = 3, Padding = new Padding(0, 0, 0, 8), Dock = DockStyle.Top };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 290));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        int row = 0;

        if (!embedded)
        {
            AddRow(grid, "", new Label { Text = "Catalog record only: this writes one row to the catalog MDB. No schematic symbol, footprint, icon menu or library folder is created or changed, and none is required. WDBLKNAM is optional. Only if you tick the optional footprint mapping below does it also add a footprint_lookup.mdb row pointing at a footprint drawing that already exists.", AutoSize = true, MaximumSize = new Size(760, 0), ForeColor = Color.FromArgb(0, 90, 0) }, row++);
        }
        AddRow(grid, "Naming standard source", sourceLabel, row++, CreateButton("Reload", () => Guard(() => { LoadStandard(true); TabChanged(); })));
        AddRow(grid, "Workbook tab (LSE category sheet)", tabBox, row++);
        AddRow(grid, "Category code (2 digits)", categoryBox, row++);
        AddRow(grid, "", categoryRuleLabel, row++);
        AddRow(grid, "Manufacturer code (3 letters)", manufacturerCodeBox, row++);
        AddRow(grid, "Type code (2 letters)", typeBox, row++);
        AddRow(grid, "LSE part number", partNumberLabel, row++, CreateButton("Preview next number", () => Guard(() => Preview(true))));
        AddRow(grid, "", partNumberDetailLabel, row++);
        AddRow(grid, "", manualCheck, row++);
        AddRow(grid, "Existing part number", manualBox, row++);
        AddRow(grid, "AutoCAD Electrical family table", familyBox, row++);
        AddRow(grid, "", familyHintLabel, row++);
        AddRow(grid, "MANUFACTURER (as spelled in the catalog)", manufacturerBox, row++, CreateButton("Reload list", () => Guard(LoadManufacturers)));
        AddRow(grid, "CATALOG (manufacturer part number)", catalogBox, row++);
        AddRow(grid, "DESCRIPTION (ALL CAPS, 100 or fewer)", descriptionBox, row++);
        AddRow(grid, "", descriptionPatternLabel, row++);
        AddRow(grid, "", descriptionProblemsLabel, row++);
        fieldsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230));
        fieldsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(grid, "Description builder (fields in standard order)", fieldsGrid, row++, CreateButton("Build description", BuildDescription));
        AddRow(grid, "", purchaseCheck, row++);
        AddRow(grid, "USER2", user2Box, row++);
        AddRow(grid, "TYPE", typeTextBox, row++);
        AddRow(grid, "RATING", ratingBox, row++);
        AddRow(grid, "VOLTAGE (if the table has it)", voltageBox, row++);
        AddRow(grid, "USER3", user3Box, row++);
        AddRow(grid, "TEXTVALUE (KEY=VALUE;KEY=VALUE)", textValueBox, row++);
        AddRow(grid, "WEBLINK", webLinkBox, row++);
        AddRow(grid, "WDBLKNAM (optional symbol block name)", wdBlkNamBox, row++);
        if (!embedded)
        {
            AddRow(grid, "", mapCheck, row++);
            AddRow(grid, "Footprint drawing (optional)", mapBox, row++, mapBrowse);
        }
        AddRow(grid, "Tab rules and examples", helpBox, row++);
        AddRow(grid, embedded ? "Notes" : "Run log", logBox, row++);

        var body = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(12) };
        body.Controls.Add(grid);

        var footer = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 60, ColumnCount = 4, Padding = new Padding(8) };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int index = 1; index < 4; index++)
        {
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        }
        footer.Controls.Add(statusLabel, 0, 0);
        footer.Controls.Add(previewButton, 1, 0);
        footer.Controls.Add(addButton, 2, 0);
        footer.Controls.Add(closeButton, 3, 0);

        Controls.Add(body);
        Controls.Add(footer);

        tabBox.SelectedIndexChanged += (_, _) => { if (!loading) TabChanged(); };
        categoryBox.TextChanged += (_, _) => PartInputChanged();
        manufacturerCodeBox.TextChanged += (_, _) => { PartInputChanged(); SuggestManufacturerSpelling(); };
        typeBox.SelectedIndexChanged += (_, _) => PartInputChanged();
        manualCheck.CheckedChanged += (_, _) => { manualBox.Enabled = manualCheck.Checked; PartInputChanged(); };
        manualBox.TextChanged += (_, _) => PartInputChanged();
        familyBox.SelectedIndexChanged += (_, _) => { if (!loading) { familyChosenByUser = true; Guard(LoadManufacturers); } };
        descriptionBox.TextChanged += (_, _) => DescriptionChanged();
        catalogBox.TextChanged += (_, _) => UpdatePurchaseDescription();
        purchaseCheck.CheckedChanged += (_, _) => { user2Box.ReadOnly = purchaseCheck.Checked; UpdatePurchaseDescription(); };
        previewButton.Click += (_, _) => Guard(() => Preview(true));
        addButton.Click += (_, _) => Add();
        closeButton.Click += (_, _) => Close();
        mapCheck.CheckedChanged += (_, _) => { mapBox.Enabled = mapCheck.Checked; mapBrowse.Enabled = mapCheck.Checked; };
        mapBrowse.Click += (_, _) => BrowseFootprint();
    }

    private void LoadReferenceData()
    {
        loading = true;
        try
        {
            LoadStandard(false);
            catalog = new CatalogService(settings.CatalogPath);
            if (!File.Exists(settings.CatalogPath))
            {
                throw new FileNotFoundException("Catalog database was not found. Set 'Catalog MDB' in LSELIBRARYSETTINGS.", settings.CatalogPath);
            }
            familyNames = catalog.GetFamilyDescriptions();
            familyBox.Items.Clear();
            foreach (string table in catalog.GetTables())
            {
                familyBox.Items.Add(new FamilyItem(table, familyNames.TryGetValue(table, out string? name) ? name : ""));
            }
            purchaseCheck.Checked = settings.PurchaseDescriptionInUser2;
            user2Box.ReadOnly = purchaseCheck.Checked;

            tabBox.Items.Clear();
            foreach (NamingTab tab in standard!.Tabs)
            {
                tabBox.Items.Add(tab);
            }
            int wanted = prefillPart is null ? -1 : standard.Tabs.FindIndex(t => t.Name.Equals(prefillPart.Tab, StringComparison.OrdinalIgnoreCase));
            tabBox.SelectedIndex = wanted >= 0 ? wanted : (tabBox.Items.Count > 0 ? 0 : -1);
            loading = false;
            TabChanged();
            ApplyPrefill();
            statusLabel.Text = $"{familyBox.Items.Count} family tables in {Path.GetFileName(settings.CatalogPath)}";
        }
        catch (Exception ex)
        {
            loading = false;
            MessageBox.Show(this, ex.Message, "LSE Librarian", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
        }
    }

    private void LoadStandard(bool force)
    {
        standard = force && File.Exists(settings.NamingWorkbookPath) ? NamingWorkbookService.LoadWorkbook(settings.NamingWorkbookPath, true) : NamingWorkbookService.Load(settings);
        sourceLabel.Text = (standard.FromWorkbook ? "Workbook: " : "JSON snapshot (workbook not reachable): ") + standard.Source + Environment.NewLine + NamingWorkbookService.Describe(standard);
    }

    private void ApplyPrefill()
    {
        if (prefillPart is not null)
        {
            if (prefillPart.Category.Length > 0) categoryBox.Text = prefillPart.Category;
            if (prefillPart.ManufacturerCode.Length > 0) SelectCode(manufacturerCodeBox, prefillPart.ManufacturerCode);
            if (prefillPart.Type.Length > 0) SelectCode(typeBox, prefillPart.Type);
            if (prefillPart.IsManual) { manualCheck.Checked = true; manualBox.Text = prefillPart.ManualPartNumber; }
        }
        if (prefill is null)
        {
            return;
        }
        if (prefill.TableName.Length > 0)
        {
            for (int index = 0; index < familyBox.Items.Count; index++)
            {
                if (familyBox.Items[index] is FamilyItem item && item.Code.Equals(prefill.TableName, StringComparison.OrdinalIgnoreCase))
                {
                    familyBox.SelectedIndex = index;
                    familyChosenByUser = true;
                    break;
                }
            }
        }
        if (prefill.Manufacturer.Length > 0) manufacturerBox.Text = prefill.Manufacturer;
        catalogBox.Text = prefill.Catalog;
        descriptionBox.Text = prefill.Description;
        typeTextBox.Text = prefill.Type;
        ratingBox.Text = prefill.Rating;
        voltageBox.Text = prefill.Voltage;
        if (prefill.User2.Length > 0) { purchaseCheck.Checked = false; user2Box.Text = prefill.User2; }
        user3Box.Text = prefill.User3;
        textValueBox.Text = prefill.TextValue;
        webLinkBox.Text = prefill.WebLink;
        wdBlkNamBox.Text = prefill.WdBlkNam;
    }

    private NamingTab? SelectedTab => tabBox.SelectedItem as NamingTab;

    private void TabChanged()
    {
        NamingTab? tab = SelectedTab;
        if (tab is null || standard is null)
        {
            return;
        }
        loading = true;
        string category = categoryBox.Text, manufacturer = Code(manufacturerCodeBox), type = Code(typeBox);

        categoryBox.Items.Clear();
        foreach (string code in tab.CategoryCodes)
        {
            categoryBox.Items.Add(code);
        }
        categoryBox.Text = tab.CategoryCodes.Contains(category) ? category : (tab.CategoryCodes.Count > 0 ? tab.CategoryCodes[0] : "");
        categoryRuleLabel.Text = tab.CategoryRule;

        manufacturerCodeBox.Items.Clear();
        foreach (var pair in tab.Manufacturers.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            manufacturerCodeBox.Items.Add(new CodeItem(pair.Key, pair.Value));
        }
        foreach (var pair in standard.Manufacturers.Where(p => !tab.Manufacturers.ContainsKey(p.Key)).OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            manufacturerCodeBox.Items.Add(new CodeItem(pair.Key, pair.Value.Name + "  (other tab)"));
        }
        if (!SelectCode(manufacturerCodeBox, manufacturer) && manufacturerCodeBox.Items.Count > 0)
        {
            manufacturerCodeBox.SelectedIndex = 0;
        }

        typeBox.Items.Clear();
        foreach (var pair in tab.Types)
        {
            typeBox.Items.Add(new CodeItem(pair.Key, pair.Value));
        }
        if (!SelectCode(typeBox, type) && typeBox.Items.Count > 0)
        {
            typeBox.SelectedIndex = 0;
        }

        descriptionPatternLabel.Text = tab.DescriptionPattern.Length > 0 ? "Pattern: " + tab.DescriptionPattern : "";
        fieldsGrid.Controls.Clear();
        fieldsGrid.RowStyles.Clear();
        fieldBoxes.Clear();
        int row = 0;
        foreach (DescriptionField field in tab.DescriptionFields)
        {
            var label = new Label { Text = field.Name + (field.Format.Length > 0 ? "  (" + field.Format + ")" : ""), AutoSize = true, Anchor = AnchorStyles.Left };
            var box = new TextBox { Dock = DockStyle.Fill, CharacterCasing = CharacterCasing.Upper, PlaceholderText = field.Example.Length > 0 ? "e.g. " + field.Example : "" };
            var tip = new ToolTip();
            tip.SetToolTip(box, field.AllowedValues.Length > 0 ? "Allowed: " + field.AllowedValues : field.Name);
            fieldsGrid.Controls.Add(label, 0, row);
            fieldsGrid.Controls.Add(box, 1, row);
            fieldsGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            fieldBoxes.Add((field, box));
            row++;
        }
        if (tab.DescriptionFields.Count == 0)
        {
            var none = new Label { Text = "(this tab defines no description fields; type the description above)", AutoSize = true, ForeColor = Color.FromArgb(80, 80, 80) };
            fieldsGrid.Controls.Add(none, 0, 0);
            fieldsGrid.SetColumnSpan(none, 2);
            fieldsGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        var help = new List<string>();
        if (tab.Title.Length > 0) help.Add(tab.Title);
        if (tab.PartNumberRule.Length > 0) help.Add("Part number: " + tab.PartNumberRule);
        if (tab.PurchaseDescriptionPattern.Length > 0) help.Add("Purchase description: " + tab.PurchaseDescriptionPattern);
        if (tab.Rules.Count > 0) { help.Add(""); help.AddRange(tab.Rules); }
        if (tab.Examples.Count > 0)
        {
            help.Add("");
            help.Add("Examples:");
            foreach (NamingExample example in tab.Examples)
            {
                help.Add($"  {example.PartNumber}  {example.Description}");
            }
        }
        help.Add("");
        help.Add($"Numbers listed on this tab: {tab.KnownPartNumbers.Count}. Type codes: {string.Join(", ", tab.Types.Keys)}. Manufacturer codes: {string.Join(", ", tab.Manufacturers.Keys)}.");
        helpBox.Text = string.Join(Environment.NewLine, help);
        loading = false;
        PartInputChanged();
        DescriptionChanged();
    }

    private void PartInputChanged()
    {
        if (loading)
        {
            return;
        }
        string key = PartRequestFromForm().Prefix + "|" + (manualCheck.Checked ? manualBox.Text.Trim() : "");
        if (key != previewKey)
        {
            preview = null;
            partNumberLabel.Text = manualCheck.Checked ? manualBox.Text.Trim().ToUpperInvariant() + "  (not verified yet)" : PartRequestFromForm().Prefix + "???????  (press Preview next number)";
            partNumberDetailLabel.Text = "";
        }
    }

    private void SuggestManufacturerSpelling()
    {
        if (loading || standard is null || manufacturerBox.Text.Trim().Length > 0)
        {
            return;
        }
        string code = Code(manufacturerCodeBox);
        if (code.Length == 0)
        {
            return;
        }
        NamingTab? tab = SelectedTab;
        if (tab is not null && tab.ManufacturerSpellings.TryGetValue(code, out string? spelling))
        {
            manufacturerBox.Text = spelling;
        }
        else if (standard.Manufacturers.TryGetValue(code, out ManufacturerCode? mc))
        {
            manufacturerBox.Text = mc.AcadeManufacturer.Length > 0 ? mc.AcadeManufacturer : mc.Name.Split('/')[0].Split('(')[0].Trim().ToUpperInvariant();
        }
    }

    private void LoadManufacturers()
    {
        if (catalog is null || familyBox.SelectedItem is not FamilyItem family)
        {
            return;
        }
        string current = manufacturerBox.Text;
        manufacturerBox.Items.Clear();
        foreach (string value in catalog.ReadDistinct(family.Code, "MANUFACTURER"))
        {
            manufacturerBox.Items.Add(value);
        }
        manufacturerBox.Text = current;
        if (current.Trim().Length == 0)
        {
            SuggestManufacturerSpelling();
        }
    }

    private void DescriptionChanged()
    {
        descriptionProblemsLabel.Text = string.Join(Environment.NewLine, PartNumberService.DescriptionProblems(SelectedTab, descriptionBox.Text).Where(p => !p.StartsWith("Description is required")));
        UpdatePurchaseDescription();
    }

    private void UpdatePurchaseDescription()
    {
        if (purchaseCheck.Checked)
        {
            string purchase = PartNumberService.PurchaseDescription(descriptionBox.Text, catalogBox.Text);
            user2Box.Text = purchase.Length > 100 ? purchase[..100] : purchase;
        }
    }

    private void BuildDescription()
    {
        var parts = fieldBoxes.Select(pair => pair.Box.Text.Trim()).Where(text => text.Length > 0).ToList();
        if (parts.Count == 0)
        {
            MessageBox.Show(this, "Fill in at least one description field first.", "LSE Librarian", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        descriptionBox.Text = string.Join(", ", parts).ToUpperInvariant();
    }

    private PartNumberRequest PartRequestFromForm() => new()
    {
        Tab = SelectedTab?.Name ?? "",
        Category = categoryBox.Text.Trim(),
        ManufacturerCode = Code(manufacturerCodeBox).ToUpperInvariant(),
        Type = Code(typeBox).ToUpperInvariant(),
        ManualPartNumber = manualCheck.Checked ? manualBox.Text.Trim().ToUpperInvariant() : ""
    };

    private CatalogAddRequest CatalogRequestFromForm() => new()
    {
        TableName = (familyBox.SelectedItem as FamilyItem)?.Code ?? "",
        Manufacturer = manufacturerBox.Text.Trim(),
        Catalog = catalogBox.Text.Trim(),
        Description = descriptionBox.Text.Trim(),
        Type = typeTextBox.Text.Trim(),
        Rating = ratingBox.Text.Trim(),
        Voltage = voltageBox.Text.Trim(),
        User2 = user2Box.Text.Trim(),
        User3 = user3Box.Text.Trim(),
        TextValue = textValueBox.Text.Trim(),
        WebLink = webLinkBox.Text.Trim(),
        WdBlkNam = wdBlkNamBox.Text.Trim()
    };

    private void Preview(bool showHint)
    {
        if (standard is null)
        {
            throw new InvalidOperationException("The naming standard is not loaded.");
        }
        PartNumberRequest request = PartRequestFromForm();
        NamingTab? tab = standard.Tab(request.Tab);
        if (tab is null || !tab.Types.ContainsKey(request.Type))
        {
            throw new InvalidOperationException("Choose the type code.");
        }
        Cursor = Cursors.WaitCursor;
        try
        {
            preview = CatalogTransactionService.Preview(settings, standard, request);
            previewKey = request.Prefix + "|" + request.ManualPartNumber;
            partNumberLabel.Text = preview.PartNumber;
            partNumberDetailLabel.Text = preview.Explanation;
            if (showHint)
            {
                var usage = CatalogTransactionService.FamilyUsage(settings, standard, request.Category);
                familyHintLabel.Text = usage.Count == 0
                    ? $"No standard-form {request.Category}... number is issued in the catalog yet; choose the family table that fits the component."
                    : "Family tables already holding " + request.Category + "... numbers: " + string.Join(", ", usage.Take(4).Select(u => $"{u.Table} ({u.Count})"));
                if (!familyChosenByUser && usage.Count > 0)
                {
                    for (int index = 0; index < familyBox.Items.Count; index++)
                    {
                        if (familyBox.Items[index] is FamilyItem item && item.Code.Equals(usage[0].Table, StringComparison.OrdinalIgnoreCase))
                        {
                            loading = true;
                            familyBox.SelectedIndex = index;
                            loading = false;
                            LoadManufacturers();
                            break;
                        }
                    }
                }
            }
            statusLabel.Text = "Next number for " + request.Prefix + ": " + preview.PartNumber;
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void Add()
    {
        if (standard is null)
        {
            return;
        }
        PartNumberRequest part = PartRequestFromForm();
        CatalogAddRequest record = CatalogRequestFromForm();
        FootprintMapRequest? mapRequest = null;
        try
        {
            CatalogTransactionService.Validate(settings, standard, part, record);
            if (!embedded && mapCheck.Checked)
            {
                mapRequest = new FootprintMapRequest { Manufacturer = record.Manufacturer, Catalog = record.Catalog, Footprint = mapBox.Text, Description = record.Description };
                string mapFull = FootprintMappingService.Validate(settings, mapRequest);
                string current = FootprintMappingService.Existing(settings, record.Manufacturer, record.Catalog);
                if (current.Length > 0 && !current.Equals(mapFull, StringComparison.OrdinalIgnoreCase))
                {
                    if (MessageBox.Show(this, "footprint_lookup.mdb already maps this manufacturer and catalog number to:" + Environment.NewLine + current + Environment.NewLine + Environment.NewLine + "Replace it with:" + Environment.NewLine + mapFull + "?", "LSE Librarian", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                    {
                        return;
                    }
                    mapRequest.Overwrite = true;
                }
            }
            var warnings = PartNumberService.DescriptionProblems(SelectedTab, record.Description);
            if (warnings.Count > 0 && MessageBox.Show(this, string.Join(Environment.NewLine, warnings) + Environment.NewLine + Environment.NewLine + "Continue anyway?", "LSE Librarian", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }
            if (preview is null)
            {
                Preview(false);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "LSE Librarian", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (embedded)
        {
            PartRequest = part;
            CatalogRequest = record;
            DialogResult = DialogResult.OK;
            Close();
            return;
        }

        if (MessageBox.Show(this, $"Add {record.Summary} to {Path.GetFileName(settings.CatalogPath)} as {preview?.PartNumber ?? part.Prefix + "..."}?" + Environment.NewLine + Environment.NewLine + "The catalog is backed up first; the number is re-checked under the issuance lock." + (mapRequest is null ? "" : Environment.NewLine + "Then footprint_lookup.mdb gets a row mapping it to " + Path.GetFileName(mapRequest.Footprint.Trim()) + " (own backup, own rollback)."), "LSE Librarian", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
        {
            return;
        }
        logBox.Text = "";
        addButton.Enabled = false;
        Cursor = Cursors.WaitCursor;
        try
        {
            Result = CatalogTransactionService.Execute(settings, standard, part, record, line =>
            {
                logBox.AppendText(line + Environment.NewLine);
                logBox.Update();
            });
            string mapNote = "";
            if (mapRequest is not null)
            {
                try
                {
                    var mapped = FootprintMappingService.Execute(settings, mapRequest, line =>
                    {
                        logBox.AppendText(line + Environment.NewLine);
                        logBox.Update();
                    });
                    mapNote = Environment.NewLine + "Footprint mapping: " + (mapped.Written > 0 ? "written" : "already correct") + (mapped.BackupFolder.Length > 0 ? " (backup and report: " + mapped.BackupFolder + ")" : "") + ".";
                }
                catch (Exception mx)
                {
                    logBox.AppendText(mx.Message + Environment.NewLine);
                    mapNote = Environment.NewLine + Environment.NewLine + "The footprint mapping failed and footprint_lookup.mdb was restored: " + mx.Message + Environment.NewLine + "The catalog row stays. Map it later with LSEFOOTPRINTMAP.";
                }
            }
            partNumberLabel.Text = Result.PartNumber;
            statusLabel.Text = $"Added {Result.PartNumber} to {Result.TableName}";
            closeButton.Text = "Close";
            MessageBox.Show(this, $"Part number {Result.PartNumber} was added to family table {Result.TableName}." + Environment.NewLine + "Catalog backup and report: " + Result.BackupFolder + mapNote, "LSE Librarian");
            preview = null;
            previewKey = "";
            catalogBox.Text = "";
            addButton.Enabled = true;
        }
        catch (Exception ex)
        {
            logBox.AppendText(ex.Message + Environment.NewLine);
            addButton.Enabled = true;
            MessageBox.Show(this, ex.Message, "LSE Librarian: the catalog was not changed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void BrowseFootprint()
    {
        string folder = Directory.Exists(settings.FootprintLibraryFolder) ? settings.FootprintLibraryFolder : settings.StockFootprintLibraryFolder;
        using var dialog = new OpenFileDialog { Title = "Choose the footprint drawing", Filter = "Drawings (*.dwg)|*.dwg", CheckFileExists = true };
        if (Directory.Exists(folder))
        {
            dialog.InitialDirectory = folder;
        }
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            mapBox.Text = dialog.FileName;
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

    private static string Code(ComboBox box)
    {
        if (box.SelectedItem is CodeItem item)
        {
            return item.Code;
        }
        string text = box.Text.Trim();
        int space = text.IndexOf(' ');
        return (space > 0 ? text[..space] : text).ToUpperInvariant();
    }

    private static bool SelectCode(ComboBox box, string code)
    {
        for (int index = 0; index < box.Items.Count; index++)
        {
            if (box.Items[index] is CodeItem item && item.Code.Equals(code, StringComparison.OrdinalIgnoreCase))
            {
                box.SelectedIndex = index;
                return true;
            }
        }
        if (code.Length > 0 && box.DropDownStyle == ComboBoxStyle.DropDown)
        {
            box.Text = code.ToUpperInvariant();
            return true;
        }
        return false;
    }

    private static void AddRow(TableLayoutPanel grid, string label, Control control, int row, Control? button = null)
    {
        grid.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        grid.Controls.Add(control, 1, row);
        if (button is not null)
        {
            grid.Controls.Add(button, 2, row);
        }
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    }

    private static Button CreateButton(string text, Action action)
    {
        var button = new Button { Text = text, AutoSize = true, Height = 30, Padding = new Padding(6, 0, 6, 0), Anchor = AnchorStyles.Left };
        button.Click += (_, _) => action();
        return button;
    }

    private sealed record CodeItem(string Code, string Meaning)
    {
        public override string ToString() => Code + "  " + Meaning;
    }

    private sealed record FamilyItem(string Code, string Name)
    {
        public override string ToString() => Name.Length > 0 ? Code + "  " + Name : Code;
    }
}
