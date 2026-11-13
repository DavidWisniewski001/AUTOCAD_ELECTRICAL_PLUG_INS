using System.Drawing;
using System.Windows.Forms;

namespace LSE.AutoCADElectrical.Librarian;

// LSEFOOTPRINTMAP: lists every LSE part in default_cat.mdb with its schematic symbol link (WDBLKNAM) and its footprint_lookup.mdb
// state. Two actions on the selected parts: map them to a footprint drawing (FootprintMappingService.Execute) or set their
// schematic symbol (CatalogTransactionService.SetSymbols). Each has its own lock, backup, read-back and rollback; this form only collects the choice.
public sealed class FootprintMapForm : Form
{
    private readonly AppSettings settings;
    private List<FootprintMapRow> allRows = new();

    private readonly TextBox searchBox = new() { Width = 280, PlaceholderText = "Filter by manufacturer, catalog, part number or description" };
    private readonly ComboBox statusFilter = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 170 };
    private readonly ComboBox familyFilter = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
    private readonly DataGridView grid = new();
    private readonly TextBox symbolBox = new() { Dock = DockStyle.Fill, MaxLength = 60 };
    private readonly Button symbolButton = new() { Text = "Set symbol", AutoSize = true, Height = 30, Padding = new Padding(6, 0, 6, 0) };
    private readonly ComboBox symbolFilter = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };
    private readonly TextBox footprintBox = new() { Dock = DockStyle.Fill };
    private readonly Button mapButton = new() { Text = "Map selected parts", Dock = DockStyle.Fill };
    private readonly TextBox logBox = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Font = new Font("Consolas", 9f), BackColor = Color.White };
    private readonly Label statusLabel = new() { Dock = DockStyle.Bottom, Height = 26, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0) };

    public FootprintMapForm(AppSettings settings)
    {
        this.settings = settings;
        Text = "LSE Librarian: catalog symbol and footprint links";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1500, 900);
        MinimumSize = new Size(1100, 600);
        BuildInterface();
        Shown += (_, _) => Guard(Reload);
    }

    private void BuildInterface()
    {
        var info = new Label
        {
            Text = "Each row is an LSE part in default_cat.mdb with its schematic symbol (the WDBLKNAM column) and its footprint. SET SYMBOL writes WDBLKNAM on the selected catalog rows only (no other column, no new or deleted rows); the symbol must exist in the LSE or stock symbol library, default_cat.mdb is backed up first and restored if anything fails, and a symbol that is already set is replaced only after you confirm. A footprint_lookup.mdb row (manufacturer table, CATALOG, BLKNAM = full path of the footprint DWG) lets AutoCAD Electrical place the footprint from that manufacturer and catalog number. " +
                   "Select one or more parts, choose a footprint drawing from the footprint library, and press Map. footprint_lookup.mdb is backed up first and restored if anything fails. An existing mapping is only replaced after you confirm.",
            Dock = DockStyle.Top,
            AutoSize = true,
            MaximumSize = new Size(1400, 0),
            Padding = new Padding(8),
            ForeColor = Color.FromArgb(0, 90, 0)
        };

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8), WrapContents = true };
        toolbar.Controls.Add(CreateButton("Reload", () => Guard(Reload)));
        toolbar.Controls.Add(new Label { Text = "Show", AutoSize = true, Margin = new Padding(16, 8, 4, 0) });
        statusFilter.Items.AddRange(new object[] { "All", FootprintMappingService.Unmapped, FootprintMappingService.Mapped, FootprintMappingService.MissingDrawing, FootprintMappingService.BlockName });
        statusFilter.SelectedIndex = 1;
        statusFilter.SelectedIndexChanged += (_, _) => ApplyFilter();
        toolbar.Controls.Add(statusFilter);
        toolbar.Controls.Add(new Label { Text = "Family", AutoSize = true, Margin = new Padding(16, 8, 4, 0) });
        familyFilter.SelectedIndexChanged += (_, _) => ApplyFilter();
        toolbar.Controls.Add(familyFilter);
        toolbar.Controls.Add(new Label { Text = "Symbol", AutoSize = true, Margin = new Padding(16, 8, 4, 0) });
        symbolFilter.Items.AddRange(new object[] { "All", "No symbol", "Has symbol" });
        symbolFilter.SelectedIndex = 0;
        symbolFilter.SelectedIndexChanged += (_, _) => ApplyFilter();
        toolbar.Controls.Add(symbolFilter);
        searchBox.TextChanged += (_, _) => ApplyFilter();
        toolbar.Controls.Add(searchBox);

        grid.Dock = DockStyle.Fill;
        grid.ReadOnly = true;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.RowHeadersVisible = false;
        grid.MultiSelect = true;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.AutoGenerateColumns = false;
        grid.BackgroundColor = Color.White;
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(FootprintMapRow.Status), HeaderText = "Status", Width = 110 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(FootprintMapRow.Manufacturer), HeaderText = "MANUFACTURER", Width = 170 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(FootprintMapRow.Catalog), HeaderText = "CATALOG", Width = 200 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(FootprintMapRow.PartNumber), HeaderText = "LSE part number", Width = 140 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(FootprintMapRow.Family), HeaderText = "Family", Width = 80 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(FootprintMapRow.Description), HeaderText = "DESCRIPTION", Width = 380 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(FootprintMapRow.Symbol), HeaderText = "Symbol (WDBLKNAM)", Width = 200 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(FootprintMapRow.BlkNam), HeaderText = "Footprint (BLKNAM)", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        grid.SelectionChanged += (_, _) => UpdateButtons();

        var bottom = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 260, ColumnCount = 3, RowCount = 3, Padding = new Padding(8) };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        bottom.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        bottom.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        bottom.Controls.Add(new Label { Text = "Footprint drawing", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        bottom.Controls.Add(footprintBox, 1, 0);
        var browse = CreateButton("Browse...", Browse);
        browse.Dock = DockStyle.Fill;
        bottom.Controls.Add(browse, 2, 0);
        bottom.Controls.Add(new Label { Text = "Schematic symbol (block)", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        bottom.Controls.Add(symbolBox, 1, 1);
        var symbolButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        symbolButton.Click += (_, _) => Guard(SetSymbolSelected);
        symbolButtons.Controls.Add(CreateButton("Browse...", BrowseSymbol));
        symbolButtons.Controls.Add(symbolButton);
        bottom.Controls.Add(symbolButtons, 2, 1);
        bottom.Controls.Add(new Label { Text = "Run log", AutoSize = true, Anchor = AnchorStyles.Top | AnchorStyles.Left }, 0, 2);
        bottom.Controls.Add(logBox, 1, 2);
        mapButton.Click += (_, _) => Guard(MapSelected);
        mapButton.Height = 36;
        mapButton.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        bottom.Controls.Add(mapButton, 2, 2);

        Controls.Add(grid);
        Controls.Add(bottom);
        Controls.Add(toolbar);
        Controls.Add(info);
        Controls.Add(statusLabel);
        UpdateButtons();
    }

    private void Reload()
    {
        allRows = FootprintMappingService.Load(settings, true);
        string keep = familyFilter.SelectedItem as string ?? "All families";
        familyFilter.Items.Clear();
        familyFilter.Items.Add("All families");
        foreach (string family in allRows.Select(r => r.Family).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            familyFilter.Items.Add(family);
        }
        int index = familyFilter.Items.IndexOf(keep);
        familyFilter.SelectedIndex = index >= 0 ? index : 0;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        string status = statusFilter.SelectedItem as string ?? "All";
        string family = familyFilter.SelectedItem as string ?? "All families";
        string sym = symbolFilter.SelectedItem as string ?? "All";
        string text = searchBox.Text.Trim();
        var view = allRows.Where(r =>
            (status == "All" || r.Status == status) &&
            (sym == "All" || (sym == "No symbol") == (r.Symbol.Length == 0)) &&
            (family == "All families" || r.Family.Equals(family, StringComparison.OrdinalIgnoreCase)) &&
            (text.Length == 0 || r.Manufacturer.Contains(text, StringComparison.OrdinalIgnoreCase) || r.Catalog.Contains(text, StringComparison.OrdinalIgnoreCase) ||
             r.PartNumber.Contains(text, StringComparison.OrdinalIgnoreCase) || r.Description.Contains(text, StringComparison.OrdinalIgnoreCase))).ToList();
        grid.DataSource = view;
        foreach (DataGridViewRow row in grid.Rows)
        {
            if (row.DataBoundItem is FootprintMapRow item)
            {
                row.DefaultCellStyle.ForeColor = item.Status == FootprintMappingService.MissingDrawing ? Color.Firebrick : item.Status == FootprintMappingService.Unmapped ? Color.DimGray : Color.Black;
            }
        }
        grid.ClearSelection();
        statusLabel.Text = $"{view.Count} shown of {allRows.Count} LSE part(s): {allRows.Count(r => r.Symbol.Length == 0)} without a symbol, {allRows.Count(r => r.Status == FootprintMappingService.Unmapped)} unmapped,{allRows.Count(r => r.Status == FootprintMappingService.MissingDrawing)} mapped to a missing drawing.";
        UpdateButtons();
    }

    private List<FootprintMapRow> SelectedRows() =>
        grid.SelectedRows.Cast<DataGridViewRow>().Select(r => r.DataBoundItem).OfType<FootprintMapRow>().ToList();

    private void UpdateButtons() => mapButton.Enabled = symbolButton.Enabled = grid.SelectedRows.Count > 0;

    private void BrowseSymbol()
    {
        string folder = Directory.Exists(settings.SymbolLibraryFolder) ? settings.SymbolLibraryFolder : settings.StockSymbolLibraryFolder;
        using var dialog = new OpenFileDialog { Title = "Choose the schematic symbol", Filter = "Symbols (*.dwg)|*.dwg", CheckFileExists = true };
        if (Directory.Exists(folder))
        {
            dialog.InitialDirectory = folder;
        }
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            symbolBox.Text = Path.GetFileNameWithoutExtension(dialog.FileName);
        }
    }

    private void SetSymbolSelected()
    {
        var selected = SelectedRows();
        if (selected.Count == 0)
        {
            return;
        }
        LibraryStandard standard = LibraryStandardService.Load(settings.LibraryStandardPath);
        string symbol = CatalogTransactionService.ResolveSymbol(settings, standard, symbolBox.Text);
        symbolBox.Text = symbol;
        var requests = selected.Select(r => new SymbolLinkRequest { Table = r.Family, Manufacturer = r.Manufacturer, Catalog = r.Catalog, PartNumber = r.PartNumber, Symbol = symbol }).ToList();

        var replacing = selected.Where(r => r.Symbol.Length > 0 && !r.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase)).ToList();
        if (replacing.Count > 0)
        {
            string sample = string.Join(Environment.NewLine, replacing.Take(5).Select(r => $"  {r.Manufacturer} {r.Catalog}: {r.Symbol}"));
            if (MessageBox.Show(this, $"{replacing.Count} of the selected part(s) already link another symbol:" + Environment.NewLine + sample + (replacing.Count > 5 ? Environment.NewLine + "  ..." : "") + Environment.NewLine + Environment.NewLine + "Replace the existing symbol with " + symbol + "?", "LSE Librarian", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            {
                return;
            }
            foreach (var request in requests)
            {
                request.Overwrite = true;
            }
        }
        if (MessageBox.Show(this, $"Set the symbol of {selected.Count} catalog row(s) to {symbol}?" + Environment.NewLine + Environment.NewLine + "Only the WDBLKNAM column changes. default_cat.mdb is backed up first and restored if anything fails.", "LSE Librarian", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
        {
            return;
        }

        logBox.Text = "";
        try
        {
            var result = CatalogTransactionService.SetSymbols(settings, standard, requests, line =>
            {
                logBox.AppendText(line + Environment.NewLine);
                logBox.Update();
            });
            string where = result.BackupFolder.Length > 0 ? Environment.NewLine + "Backup and report: " + result.BackupFolder : "";
            MessageBox.Show(this, $"{result.Written} symbol link(s) written, {result.Unchanged} already correct." + where, "LSE Librarian");
        }
        catch (Exception ex)
        {
            logBox.AppendText(ex.Message + Environment.NewLine);
            MessageBox.Show(this, ex.Message, "LSE Librarian: default_cat.mdb was not changed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        Reload();
    }

    private void Browse()
    {
        string folder = Directory.Exists(settings.FootprintLibraryFolder) ? settings.FootprintLibraryFolder : settings.StockFootprintLibraryFolder;
        using var dialog = new OpenFileDialog { Title = "Choose the footprint drawing", Filter = "Drawings (*.dwg)|*.dwg", CheckFileExists = true };
        if (Directory.Exists(folder))
        {
            dialog.InitialDirectory = folder;
        }
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            footprintBox.Text = dialog.FileName;
        }
    }

    private void MapSelected()
    {
        var selected = SelectedRows();
        if (selected.Count == 0)
        {
            return;
        }
        string footprint = footprintBox.Text.Trim();
        var requests = selected.GroupBy(r => FootprintLookupService.Key(r.Manufacturer, r.Catalog), StringComparer.OrdinalIgnoreCase).Select(g => g.First()).Select(r => new FootprintMapRequest { Manufacturer = r.Manufacturer, Catalog = r.Catalog, Footprint = footprint, Description = r.Description }).ToList();
        string full = FootprintMappingService.Validate(settings, requests[0]);

        var replacing = selected.Where(r => r.BlkNam.Length > 0 && !r.BlkNam.Equals(full, StringComparison.OrdinalIgnoreCase)).ToList();
        if (replacing.Count > 0)
        {
            string sample = string.Join(Environment.NewLine, replacing.Take(5).Select(r => $"  {r.Manufacturer} {r.Catalog}: {r.BlkNam}"));
            if (MessageBox.Show(this, $"{replacing.Count} of the selected part(s) already map to another footprint:" + Environment.NewLine + sample + (replacing.Count > 5 ? Environment.NewLine + "  ..." : "") + Environment.NewLine + Environment.NewLine + "Replace the existing mapping with" + Environment.NewLine + full + "?", "LSE Librarian", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            {
                return;
            }
            foreach (var request in requests)
            {
                request.Overwrite = true;
            }
        }
        if (MessageBox.Show(this, $"Map {selected.Count} part(s) to" + Environment.NewLine + full + Environment.NewLine + Environment.NewLine + "footprint_lookup.mdb is backed up first and restored if anything fails.", "LSE Librarian", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
        {
            return;
        }

        logBox.Text = "";
        try
        {
            var result = FootprintMappingService.Execute(settings, requests, line =>
            {
                logBox.AppendText(line + Environment.NewLine);
                logBox.Update();
            });
            string where = result.BackupFolder.Length > 0 ? Environment.NewLine + "Backup and report: " + result.BackupFolder : "";
            MessageBox.Show(this, $"{result.Written} mapping(s) written, {result.Unchanged} already correct." + where, "LSE Librarian");
        }
        catch (Exception ex)
        {
            logBox.AppendText(ex.Message + Environment.NewLine);
            MessageBox.Show(this, ex.Message, "LSE Librarian: footprint_lookup.mdb was not changed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        Reload();
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

    private static Button CreateButton(string text, Action action)
    {
        var button = new Button { Text = text, AutoSize = true, Height = 30, Padding = new Padding(6, 0, 6, 0) };
        button.Click += (_, _) => action();
        return button;
    }
}
