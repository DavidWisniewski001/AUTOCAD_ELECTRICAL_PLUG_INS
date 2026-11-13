using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace LSE.AutoCADElectrical.Librarian;

// LSECATALOG / LSEASSIGNCOMPONENT: a curated view of default_cat.mdb (company standards, favourites, recent, full
// catalog) with a preview of the attribute changes an assignment would make to the selected block. Merged from the
// former LSE Catalog Tools plugin on 2026-11-12. Reads the catalog through CatalogService; the only catalog write it can
// start is "New record...", which opens CatalogRecordForm (so CatalogTransactionService stays the only writer).
public sealed class CatalogBrowserForm : Form
{
    private readonly AppSettings settings;
    private readonly string partColumn;
    private readonly SelectedBlockInfo? selectedBlock;
    private readonly TreeView navigation = new() { Dock = DockStyle.Fill, HideSelection = false };
    private readonly ComboBox familyBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly ComboBox manufacturerBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly ComboBox typeBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly ComboBox ratingBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly TextBox searchBox = new() { Dock = DockStyle.Fill, PlaceholderText = "Search catalog, manufacturer, description, type, rating, or LSE part number" };
    private readonly DataGridView catalogGrid = new();
    private readonly DataGridView changeGrid = new();
    private readonly Label statusLabel = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    private readonly List<StandardEntry> standards;
    private Dictionary<string, string> familyDescriptions = new(StringComparer.OrdinalIgnoreCase);
    private List<CatalogRecord> records = new();
    private BindingList<AttributeChange> proposedChanges = new();

    public CatalogBrowserForm(AppSettings settings, SelectedBlockInfo? block)
    {
        this.settings = settings;
        partColumn = CatalogTransactionService.PartColumn(settings);
        selectedBlock = block;
        standards = CompanyStandardsService.Load(settings.CompanyStandardsPath);
        Text = block is null ? "LSE Librarian: catalog browser" : "LSE Librarian: assign component - " + block.BlockName;
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1750, 950);
        MinimumSize = new Size(1200, 700);
        BuildInterface();
        Shown += (_, _) => LoadCatalog();
    }

    private void BuildInterface()
    {
        var filters = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 75,
            ColumnCount = 5,
            RowCount = 2,
            Padding = new Padding(8)
        };
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        filters.Controls.Add(CreateLabel("Family"), 0, 0);
        filters.Controls.Add(CreateLabel("Manufacturer"), 1, 0);
        filters.Controls.Add(CreateLabel("Type"), 2, 0);
        filters.Controls.Add(CreateLabel("Rating"), 3, 0);
        filters.Controls.Add(CreateLabel("Search"), 4, 0);
        filters.Controls.Add(familyBox, 0, 1);
        filters.Controls.Add(manufacturerBox, 1, 1);
        filters.Controls.Add(typeBox, 2, 1);
        filters.Controls.Add(ratingBox, 3, 1);
        filters.Controls.Add(searchBox, 4, 1);

        var leftPanel = new Panel { Dock = DockStyle.Left, Width = 250, Padding = new Padding(8) };
        leftPanel.Controls.Add(navigation);

        ConfigureCatalogGrid();
        ConfigureChangeGrid();

        var verticalSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterWidth = 6
        };
        verticalSplit.Panel1.Controls.Add(catalogGrid);
        verticalSplit.Panel2.Controls.Add(changeGrid);
        Shown += (_, _) => ConfigureDetailSplitter(verticalSplit);
        Resize += (_, _) => KeepDetailSplitterValid(verticalSplit);

        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 60,
            ColumnCount = 8,
            Padding = new Padding(8)
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int index = 1; index < 8; index++)
        {
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        }

        footer.Controls.Add(statusLabel, 0, 0);
        footer.Controls.Add(CreateButton("Toggle favorite", ToggleFavorite), 1, 0);
        footer.Controls.Add(CreateButton("Toggle company standard", ToggleStandard), 2, 0);
        footer.Controls.Add(CreateButton("New record...", NewRecord), 3, 0);
        footer.Controls.Add(CreateButton(selectedBlock is null ? "Select block" : "Change block", SelectBlock), 4, 0);
        footer.Controls.Add(CreateButton("Assign component", Assign), 5, 0);
        footer.Controls.Add(CreateButton("Refresh", LoadTable), 6, 0);
        footer.Controls.Add(CreateButton("Close", Close), 7, 0);

        Controls.Add(verticalSplit);
        Controls.Add(leftPanel);
        Controls.Add(filters);
        Controls.Add(footer);

        familyBox.SelectedIndexChanged += (_, _) => LoadTable();
        manufacturerBox.SelectedIndexChanged += (_, _) => ApplyFilter();
        typeBox.SelectedIndexChanged += (_, _) => ApplyFilter();
        ratingBox.SelectedIndexChanged += (_, _) => ApplyFilter();
        searchBox.TextChanged += (_, _) => ApplyFilter();
        navigation.AfterSelect += (_, _) => ApplyFilter();
    }

    private static void ConfigureDetailSplitter(SplitContainer split)
    {
        if (split.Height <= 0) return;
        int desiredPanel2 = Math.Clamp(240, 170, Math.Max(170, split.Height / 2));
        int maxPanel1 = Math.Max(100, split.Height - split.SplitterWidth - 170);
        int desiredPanel1 = Math.Clamp(split.Height - split.SplitterWidth - desiredPanel2, 100, maxPanel1);
        split.Panel1MinSize = 100;
        split.Panel2MinSize = 170;
        split.SplitterDistance = Math.Clamp(desiredPanel1, split.Panel1MinSize, Math.Max(split.Panel1MinSize, split.Height - split.SplitterWidth - split.Panel2MinSize));
    }

    private static void KeepDetailSplitterValid(SplitContainer split)
    {
        if (split.Height <= split.SplitterWidth + 270) return;
        int maximum = split.Height - split.SplitterWidth - split.Panel2MinSize;
        if (split.SplitterDistance > maximum) split.SplitterDistance = maximum;
        if (split.SplitterDistance < split.Panel1MinSize) split.SplitterDistance = split.Panel1MinSize;
    }

    private void ConfigureCatalogGrid()
    {
        catalogGrid.Dock = DockStyle.Fill;
        catalogGrid.ReadOnly = true;
        catalogGrid.AutoGenerateColumns = false;
        catalogGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        catalogGrid.MultiSelect = false;
        catalogGrid.AllowUserToAddRows = false;
        catalogGrid.AllowUserToDeleteRows = false;
        catalogGrid.AllowUserToResizeColumns = false;
        catalogGrid.AllowUserToResizeRows = false;
        catalogGrid.RowHeadersVisible = false;
        catalogGrid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCells;
        catalogGrid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        catalogGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;

        catalogGrid.Columns.Add(TextColumn("Status", "Status", null, 105));
        catalogGrid.Columns.Add(TextColumn("Manufacturer", "Manufacturer", "Manufacturer", 130));
        catalogGrid.Columns.Add(TextColumn("Catalog", "Catalog", "Catalog", 190));
        catalogGrid.Columns.Add(TextColumn("LSEPart", "LSE Part Number (" + partColumn + ")", null, 150));
        catalogGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Description",
            HeaderText = "Description",
            DataPropertyName = "Description",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            MinimumWidth = 260,
            DefaultCellStyle = new DataGridViewCellStyle { WrapMode = DataGridViewTriState.True }
        });
        catalogGrid.Columns.Add(TextColumn("Type", "Type", "Type", 130));
        catalogGrid.Columns.Add(TextColumn("Rating", "Rating", "Rating", 115));
        catalogGrid.Columns.Add(TextColumn("Symbol", "Symbol (WDBLKNAM)", "WdBlkNam", 120));

        catalogGrid.CellFormatting += (_, eventArgs) =>
        {
            if (eventArgs.RowIndex < 0 || catalogGrid.Rows[eventArgs.RowIndex].DataBoundItem is not CatalogRecord record)
            {
                return;
            }

            string column = catalogGrid.Columns[eventArgs.ColumnIndex].Name;
            if (column == "Status") eventArgs.Value = StatusOf(record);
            else if (column == "LSEPart") eventArgs.Value = record.Get(partColumn);
        };
        catalogGrid.SelectionChanged += (_, _) => RefreshChangePreview();
        catalogGrid.CellDoubleClick += (_, eventArgs) =>
        {
            if (eventArgs.RowIndex >= 0) Assign();
        };
    }

    private void ConfigureChangeGrid()
    {
        changeGrid.Dock = DockStyle.Fill;
        changeGrid.AutoGenerateColumns = false;
        changeGrid.AllowUserToAddRows = false;
        changeGrid.AllowUserToDeleteRows = false;
        changeGrid.AllowUserToResizeColumns = false;
        changeGrid.AllowUserToResizeRows = false;
        changeGrid.RowHeadersVisible = false;
        changeGrid.SelectionMode = DataGridViewSelectionMode.CellSelect;
        changeGrid.MultiSelect = false;
        changeGrid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        changeGrid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        changeGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        changeGrid.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;

        changeGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Attribute",
            HeaderText = "Attribute",
            DataPropertyName = "Attribute",
            ReadOnly = true,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells,
            MinimumWidth = 115
        });
        changeGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Current",
            HeaderText = "Current",
            DataPropertyName = "Current",
            ReadOnly = true,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            MinimumWidth = 220,
            DefaultCellStyle = new DataGridViewCellStyle { WrapMode = DataGridViewTriState.True }
        });
        changeGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Proposed",
            HeaderText = "Proposed (editable)",
            DataPropertyName = "Proposed",
            ReadOnly = false,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            MinimumWidth = 260,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                WrapMode = DataGridViewTriState.True,
                BackColor = Color.FromArgb(255, 252, 224)
            }
        });
        changeGrid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = "Preserved",
            HeaderText = "Preserve",
            DataPropertyName = "Preserved",
            ReadOnly = false,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells,
            MinimumWidth = 85
        });

        changeGrid.CellValueChanged += (_, eventArgs) =>
        {
            if (eventArgs.RowIndex < 0 || eventArgs.RowIndex >= proposedChanges.Count) return;
            AttributeChange change = proposedChanges[eventArgs.RowIndex];
            if (changeGrid.Columns[eventArgs.ColumnIndex].Name != "Preserved") return;
            if (change.Preserved)
            {
                change.Proposed = change.Current;
            }
            else if (SelectedRecord() is CatalogRecord record &&
                     ComponentAssignService.BuildMap(record, settings).TryGetValue(change.Attribute, out string? mapped))
            {
                change.Proposed = mapped;
            }
            changeGrid.InvalidateRow(eventArgs.RowIndex);
        };
        changeGrid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (changeGrid.IsCurrentCellDirty)
            {
                changeGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        };
    }

    private static DataGridViewTextBoxColumn TextColumn(string name, string header, string? property, int width)
    {
        return new DataGridViewTextBoxColumn
        {
            Name = name,
            HeaderText = header,
            DataPropertyName = property,
            Width = width,
            MinimumWidth = width,
            SortMode = DataGridViewColumnSortMode.Automatic
        };
    }

    private static Label CreateLabel(string text) => new() { Text = text, AutoSize = true };

    private static Button CreateButton(string text, Action action)
    {
        var button = new Button { Text = text, Dock = DockStyle.Fill };
        button.Click += (_, _) => action();
        return button;
    }

    private void LoadCatalog()
    {
        try
        {
            BuildNavigation();
            var catalog = new CatalogService(settings.CatalogPath);
            List<string> tables = catalog.GetTables();
            try { familyDescriptions = catalog.GetFamilyDescriptions(); } catch { familyDescriptions = new(StringComparer.OrdinalIgnoreCase); }
            familyBox.DataSource = tables.Select(code => new FamilyItem(code, FamilyNames.Friendly(code, familyDescriptions))).ToList();
            familyBox.DisplayMember = nameof(FamilyItem.Name);

            if (selectedBlock is not null && !string.IsNullOrWhiteSpace(selectedBlock.Family))
            {
                int index = tables.FindIndex(code =>
                    code.Equals(selectedBlock.Family, StringComparison.OrdinalIgnoreCase) ||
                    code.Contains(selectedBlock.Family, StringComparison.OrdinalIgnoreCase));
                if (index >= 0) familyBox.SelectedIndex = index;
            }

            if (familyBox.Items.Count > 0 && familyBox.SelectedIndex < 0) familyBox.SelectedIndex = 0;
        }
        catch (System.Exception exception)
        {
            MessageBox.Show(this, exception.Message, "LSE Librarian");
        }
    }

    private void BuildNavigation()
    {
        navigation.Nodes.Clear();
        navigation.Nodes.Add(new TreeNode("Company Standards") { Tag = CatalogScope.CompanyStandards });
        navigation.Nodes.Add(new TreeNode("My Favorites") { Tag = CatalogScope.PersonalFavorites });
        navigation.Nodes.Add(new TreeNode("Recently Used") { Tag = CatalogScope.Recent });
        navigation.Nodes.Add(new TreeNode("Full Catalog") { Tag = CatalogScope.AllCatalog });
        navigation.SelectedNode = navigation.Nodes[standards.Count == 0 ? 3 : 0];
    }

    private FamilyItem? CurrentFamily() => familyBox.SelectedItem as FamilyItem;

    private void LoadTable()
    {
        if (CurrentFamily() is not FamilyItem family) return;
        try
        {
            records = new CatalogService(settings.CatalogPath).ReadTable(family.Code);
            FillFilters();
            ApplyFilter();
        }
        catch (System.Exception exception)
        {
            MessageBox.Show(this, exception.Message, "LSE Librarian");
        }
    }

    private void FillFilters()
    {
        SetFilter(manufacturerBox, records.Select(record => record.Manufacturer));
        SetFilter(typeBox, records.Select(record => record.Type));
        SetFilter(ratingBox, records.Select(record => record.Rating));
    }

    private static void SetFilter(ComboBox box, IEnumerable<string> input)
    {
        string prior = Convert.ToString(box.SelectedItem) ?? string.Empty;
        var values = new List<string> { "(All)" };
        values.AddRange(input.Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase));
        box.DataSource = values;
        box.SelectedItem = values.Contains(prior) ? prior : "(All)";
    }

    private void ApplyFilter()
    {
        IEnumerable<CatalogRecord> query = records;
        CatalogScope scope = navigation.SelectedNode?.Tag is CatalogScope selectedScope
            ? selectedScope
            : CatalogScope.AllCatalog;

        if (scope == CatalogScope.CompanyStandards)
        {
            query = query.Where(record => CompanyStandardsService.Find(standards, record.Key) is { IsLegacy: false });
        }
        if (scope == CatalogScope.PersonalFavorites)
        {
            query = query.Where(record => settings.Favorites.Contains(record.Key, StringComparer.OrdinalIgnoreCase));
        }
        if (scope == CatalogScope.Recent)
        {
            query = query.Where(record => settings.Recent.Contains(record.Key, StringComparer.OrdinalIgnoreCase));
        }

        query = ApplyComboFilter(query, manufacturerBox, record => record.Manufacturer);
        query = ApplyComboFilter(query, typeBox, record => record.Type);
        query = ApplyComboFilter(query, ratingBox, record => record.Rating);

        string search = searchBox.Text.Trim();
        if (search.Length > 0)
        {
            query = query.Where(record => record.Values.Values.Any(value =>
                value.Contains(search, StringComparison.OrdinalIgnoreCase)));
        }

        List<CatalogRecord> displayed = query.ToList();
        catalogGrid.DataSource = new BindingList<CatalogRecord>(displayed);
        statusLabel.Text = $"{displayed.Count:n0} shown of {records.Count:n0} in {CurrentFamily()?.Name} | {settings.CatalogPath}" +
                           (records.Count >= 20000 ? " | first 20,000 rows only; narrow with the filters" : string.Empty);
    }

    private static IEnumerable<CatalogRecord> ApplyComboFilter(
        IEnumerable<CatalogRecord> query,
        ComboBox box,
        Func<CatalogRecord, string> selector)
    {
        string value = Convert.ToString(box.SelectedItem) ?? "(All)";
        return value == "(All)"
            ? query
            : query.Where(record => selector(record).Equals(value, StringComparison.OrdinalIgnoreCase));
    }

    private CatalogRecord? SelectedRecord() => catalogGrid.CurrentRow?.DataBoundItem as CatalogRecord;

    private string StatusOf(CatalogRecord record)
    {
        StandardEntry? standard = CompanyStandardsService.Find(standards, record.Key);
        if (standard is not null)
        {
            return standard.IsLegacy ? "Legacy" : "Standard";
        }
        if (settings.Favorites.Contains(record.Key, StringComparer.OrdinalIgnoreCase)) return "Favorite";
        return "Catalog";
    }

    private void RefreshChangePreview()
    {
        CatalogRecord? record = SelectedRecord();
        proposedChanges = new BindingList<AttributeChange>(
            record is null
                ? new List<AttributeChange>()
                : ComponentAssignService.Preview(selectedBlock, record, settings));
        changeGrid.DataSource = proposedChanges;
    }

    private void SelectBlock()
    {
        Hide();
        try
        {
            SelectedBlockInfo? block = ComponentAssignService.PromptForBlock();
            if (block is not null)
            {
                using var replacement = new CatalogBrowserForm(settings, block);
                replacement.ShowDialog();
                Close();
            }
        }
        catch (System.Exception exception)
        {
            MessageBox.Show(this, exception.Message, "LSE Librarian");
        }
        finally
        {
            if (!IsDisposed) Show();
        }
    }

    private void Assign()
    {
        try
        {
            changeGrid.EndEdit();
            CatalogRecord? record = SelectedRecord();
            if (record is null) return;

            SelectedBlockInfo? block = selectedBlock;
            if (block is null)
            {
                Hide();
                try { block = ComponentAssignService.PromptForBlock(); }
                finally { Show(); }
            }
            if (block is null) return;

            string warning = ComponentAssignService.CompatibilityWarning(block, record);
            if (warning.Length > 0 && MessageBox.Show(
                    this,
                    warning + "\r\n\r\nApply anyway?",
                    "Compatibility warning",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }

            ComponentAssignService.Apply(block, record, settings, proposedChanges.ToList());
            settings.Recent.RemoveAll(key => key.Equals(record.Key, StringComparison.OrdinalIgnoreCase));
            settings.Recent.Insert(0, record.Key);
            if (settings.Recent.Count > 25)
            {
                settings.Recent.RemoveRange(25, settings.Recent.Count - 25);
            }
            SettingsService.Save(settings);
            statusLabel.Text = $"Assigned {record.Manufacturer} {record.Catalog}. {partColumn}/LSE part number: {record.Get(partColumn)}";
            RefreshChangePreview();
        }
        catch (System.Exception exception)
        {
            MessageBox.Show(this, exception.Message, "LSE Librarian", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ToggleFavorite()
    {
        CatalogRecord? record = SelectedRecord();
        if (record is null) return;
        if (settings.Favorites.RemoveAll(key => key.Equals(record.Key, StringComparison.OrdinalIgnoreCase)) == 0)
        {
            settings.Favorites.Add(record.Key);
        }
        SettingsService.Save(settings);
        ApplyFilter();
    }

    // Lists or delists the selected record in CompanyStandards.json (shared when Settings points at a shared file).
    private void ToggleStandard()
    {
        try
        {
            CatalogRecord? record = SelectedRecord();
            if (record is null) return;
            bool listed = CompanyStandardsService.Toggle(standards, record.Key, FamilyNames.Friendly(record.TableName, familyDescriptions));
            CompanyStandardsService.Save(settings.CompanyStandardsPath, standards);
            statusLabel.Text = (listed ? "Listed as a company standard: " : "Removed from the company standards: ") +
                               $"{record.Manufacturer} {record.Catalog} ({settings.CompanyStandardsPath})";
            ApplyFilter();
        }
        catch (System.Exception exception)
        {
            MessageBox.Show(this, exception.Message, "LSE Librarian", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // Opens the catalog record dialog (LSECATALOGADD) pre-set to the current family table, then reloads the table.
    private void NewRecord()
    {
        try
        {
            var prefill = new CatalogAddRequest { TableName = CurrentFamily()?.Code ?? string.Empty };
            using (var form = new CatalogRecordForm(settings, false, prefill, null))
            {
                form.ShowDialog(this);
            }
            LoadTable();
        }
        catch (System.Exception exception)
        {
            MessageBox.Show(this, exception.Message, "LSE Librarian", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private sealed record FamilyItem(string Code, string Name);
}
