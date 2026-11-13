using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace LSE.AutoCADElectrical.Librarian;

public sealed class AuditForm : Form
{
    private readonly AppSettings settings = SettingsService.Load();
    private readonly CheckBox deepCheck = new() { Text = "Open every drawing (slow)", AutoSize = true };
    private readonly CheckBox stockCheck = new() { Text = "Include stock AutoCAD Electrical pages", AutoSize = true };
    private readonly ComboBox filterBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
    private readonly TextBox searchBox = new() { Width = 260, PlaceholderText = "Filter by path or message" };
    private readonly DataGridView grid = new();
    private readonly TextBox summaryBox = new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        Font = new Font("Consolas", 9f),
        BackColor = Color.White
    };
    private readonly Label statusLabel = new() { Dock = DockStyle.Bottom, Height = 26, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0) };
    private readonly Button openButton;
    private readonly Button folderButton;
    private readonly Button exportButton;
    private AuditReport? report;
    private BindingList<AuditFinding> rows = new();

    public string? OpenInLibrarian { get; private set; }

    public AuditForm()
    {
        Text = "LSE Library Audit";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1600, 900);
        MinimumSize = new Size(1100, 600);
        deepCheck.Checked = settings.DeepAudit;
        stockCheck.Checked = settings.AuditStockEntries;
        openButton = CreateButton("Open in Librarian", OpenSelected);
        folderButton = CreateButton("Open containing folder", OpenFolder);
        exportButton = CreateButton("Export report...", Export);
        BuildInterface();
        Shown += (_, _) => Run();
    }

    private void BuildInterface()
    {
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8), WrapContents = true };
        toolbar.Controls.Add(CreateButton("Run audit", Run));
        toolbar.Controls.Add(deepCheck);
        toolbar.Controls.Add(stockCheck);
        toolbar.Controls.Add(new Label { Text = "Show", AutoSize = true, Margin = new Padding(16, 8, 4, 0) });
        filterBox.Items.AddRange(new object[] { "Errors only", "Errors and warnings", "Everything" });
        filterBox.SelectedIndex = 1;
        filterBox.SelectedIndexChanged += (_, _) => ApplyFilter();
        toolbar.Controls.Add(filterBox);
        searchBox.TextChanged += (_, _) => ApplyFilter();
        toolbar.Controls.Add(searchBox);
        toolbar.Controls.Add(openButton);
        toolbar.Controls.Add(folderButton);
        toolbar.Controls.Add(exportButton);

        grid.AutoGenerateColumns = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = false;
        grid.ReadOnly = true;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.RowHeadersVisible = false;
        grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCells;
        grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        grid.Dock = DockStyle.Fill;
        grid.Columns.Add(TextColumn("Severity", "Severity", "SeverityText", 80));
        grid.Columns.Add(TextColumn("Area", "Area", "Area", 80));
        grid.Columns.Add(TextColumn("Kind", "Kind", "Kind", 120));
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Path", HeaderText = "Where", DataPropertyName = "Path", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 45, DefaultCellStyle = new DataGridViewCellStyle { WrapMode = DataGridViewTriState.True } });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Message", HeaderText = "Finding", DataPropertyName = "Message", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 35, DefaultCellStyle = new DataGridViewCellStyle { WrapMode = DataGridViewTriState.True } });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Suggestion", HeaderText = "What to do", DataPropertyName = "Suggestion", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 30, DefaultCellStyle = new DataGridViewCellStyle { WrapMode = DataGridViewTriState.True } });
        grid.CellFormatting += (_, e) =>
        {
            if (grid.Rows[e.RowIndex].DataBoundItem is AuditFinding finding)
            {
                e.CellStyle!.BackColor = finding.Severity switch
                {
                    Severity.Error => Color.FromArgb(255, 228, 225),
                    Severity.Warning => Color.FromArgb(255, 247, 214),
                    _ => Color.White
                };
            }
        };
        grid.SelectionChanged += (_, _) => UpdateButtons();
        grid.CellDoubleClick += (_, _) => OpenSelected();

        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 560 };
        split.Panel1.Controls.Add(grid);
        split.Panel2.Controls.Add(summaryBox);

        Controls.Add(split);
        Controls.Add(statusLabel);
        Controls.Add(toolbar);
        UpdateButtons();
    }

    private void Run()
    {
        Cursor = Cursors.WaitCursor;
        summaryBox.Text = "";
        statusLabel.Text = "Auditing...";
        try
        {
            LibraryStandard standard = LibraryStandardService.Load(settings.LibraryStandardPath);
            var options = new AuditOptions { Deep = deepCheck.Checked, IncludeStockEntries = stockCheck.Checked };
            report = LibraryAuditService.Run(settings, standard, options, line =>
            {
                summaryBox.AppendText(line + Environment.NewLine);
                summaryBox.Update();
                statusLabel.Text = line;
                statusLabel.Update();
            });
            ApplyFilter();
            statusLabel.Text = $"{report.Errors} error(s), {report.Warnings} warning(s), {report.Infos} note(s). Double-click a drawing finding to open it in the Librarian.";
        }
        catch (Exception ex)
        {
            statusLabel.Text = "Audit failed.";
            MessageBox.Show(this, ex.Message, "LSE Library Audit", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void ApplyFilter()
    {
        if (report is null)
        {
            return;
        }

        Severity minimum = filterBox.SelectedIndex switch { 0 => Severity.Error, 1 => Severity.Warning, _ => Severity.Info };
        string search = searchBox.Text.Trim();
        IEnumerable<AuditFinding> filtered = report.Findings.Where(f => f.Severity <= minimum);
        if (search.Length > 0)
        {
            filtered = filtered.Where(f => f.Path.Contains(search, StringComparison.OrdinalIgnoreCase) || f.Message.Contains(search, StringComparison.OrdinalIgnoreCase) || f.Kind.Contains(search, StringComparison.OrdinalIgnoreCase));
        }
        rows = new BindingList<AuditFinding>(filtered.OrderBy(f => f.Severity).ThenBy(f => f.Area).ThenBy(f => f.Path, StringComparer.OrdinalIgnoreCase).ToList());
        grid.DataSource = rows;
        UpdateButtons();
    }

    private AuditFinding? Selected => grid.CurrentRow?.DataBoundItem as AuditFinding;

    private static string? DrawingPath(AuditFinding? finding)
    {
        if (finding is null)
        {
            return null;
        }
        string path = finding.Path;
        return path.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase) && File.Exists(path) ? path : null;
    }

    private void UpdateButtons()
    {
        openButton.Enabled = DrawingPath(Selected) is not null;
        folderButton.Enabled = Selected is not null && (File.Exists(Selected.Path) || Directory.Exists(Selected.Path));
        exportButton.Enabled = report is not null;
    }

    private void OpenSelected()
    {
        string? path = DrawingPath(Selected);
        if (path is null)
        {
            return;
        }
        OpenInLibrarian = path;
        Close();
    }

    private void OpenFolder()
    {
        AuditFinding? finding = Selected;
        if (finding is null)
        {
            return;
        }
        try
        {
            if (File.Exists(finding.Path))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + finding.Path + "\"") { UseShellExecute = true });
            }
            else if (Directory.Exists(finding.Path))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", "\"" + finding.Path + "\"") { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "LSE Library Audit");
        }
    }

    private void Export()
    {
        if (report is null)
        {
            return;
        }
        using var dialog = new SaveFileDialog
        {
            Title = "Export audit report",
            Filter = "Text report (*.txt)|*.txt",
            FileName = "LSE_LibraryAudit_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".txt",
            InitialDirectory = Directory.Exists(settings.BackupFolder) ? settings.BackupFolder : ""
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }
        try
        {
            LibraryAuditService.WriteReport(report, dialog.FileName);
            statusLabel.Text = "Report written: " + dialog.FileName;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "LSE Library Audit", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static DataGridViewTextBoxColumn TextColumn(string name, string header, string property, int width)
    {
        return new DataGridViewTextBoxColumn { Name = name, HeaderText = header, DataPropertyName = property, Width = width };
    }

    private static Button CreateButton(string text, Action action)
    {
        var button = new Button { Text = text, AutoSize = true, Height = 30, Padding = new Padding(6, 0, 6, 0) };
        button.Click += (_, _) => action();
        return button;
    }
}
