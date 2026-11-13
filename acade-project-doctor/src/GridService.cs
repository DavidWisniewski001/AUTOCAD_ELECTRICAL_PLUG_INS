using System.Drawing;
using System.Windows.Forms;

namespace LSE.AutoCADElectrical.ProjectDoctor;

public static class GridService
{
    public const string NoteText = "note";

    public static readonly Color NoteForeColor = Color.FromArgb(110, 110, 110);

    public static readonly Color ErrorBackColor = Color.FromArgb(255, 228, 225);

    public static readonly Color WarningBackColor = Color.FromArgb(255, 250, 205);

    public static readonly Color HealthyBackColor = Color.FromArgb(232, 245, 233);

    public static void AddFindingColumns(DataGridView grid)
    {
        Add(grid, new DataGridViewCheckBoxColumn { HeaderText = "Fix", Width = 44, Name = "Fix", ReadOnly = true, ToolTipText = "Click a box to tick or untick the repair on that row. Rows that say note are information only." });
        Add(grid, new DataGridViewTextBoxColumn { HeaderText = "Severity", Width = 72, ReadOnly = true, ToolTipText = "Error: something the standard needs is missing. Warning: present but different, or a drawing is missing on disk. Info: a note." });
        Add(grid, new DataGridViewTextBoxColumn { HeaderText = "Category", Width = 170, ReadOnly = true, ToolTipText = "The part of the project the row is about." });
        Add(grid, new DataGridViewTextBoxColumn { HeaderText = "Item", Width = 200, ReadOnly = true, ToolTipText = "The line, attribute, file or drawing concerned. Hover over a cell for the doctor's note." });
        Add(grid, new DataGridViewTextBoxColumn { HeaderText = "Project has", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 45, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { WrapMode = DataGridViewTriState.True }, ToolTipText = "The value in your project." });
        Add(grid, new DataGridViewTextBoxColumn { HeaderText = "Standard", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 45, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { WrapMode = DataGridViewTriState.True }, ToolTipText = "The value in the LSE template." });
        Add(grid, new DataGridViewTextBoxColumn { HeaderText = "Repair", Width = 130, ReadOnly = true, ToolTipText = "What ticking the box will do. (report only) means the doctor cannot fix it; the Item tooltip says what to do by hand." });
    }

    public static int FillFindings(DataGridView grid, Diagnosis? diagnosis, bool showInfo)
    {
        grid.Rows.Clear();
        if (diagnosis is null)
        {
            return 0;
        }

        int added = 0;
        foreach (Finding finding in diagnosis.Findings)
        {
            if (!showInfo && finding.Severity == Severity.Info)
            {
                continue;
            }

            bool canFix = finding.Action != RepairAction.None;
            int index = grid.Rows.Add(canFix && finding.Selected, finding.Severity.ToString(), finding.Category, finding.Item, DiagnosisService.Short(finding.Current, 400), DiagnosisService.Short(finding.Standard, 400), finding.ActionText);
            DataGridViewRow row = grid.Rows[index];
            row.Tag = finding;
            row.Cells[0].ToolTipText = canFix
                ? "Click to tick or untick. Ticked rows are repaired by Repair ticked." + (finding.Recommended ? " Recommended by LSE." : " Your decision; LSE does not tick it for you.")
                : "Information only: the doctor has nothing it can change on this row. " + finding.Note;
            row.Cells[3].ToolTipText = finding.Note;
            row.Cells[4].ToolTipText = finding.Current;
            row.Cells[5].ToolTipText = finding.Standard;
            row.Cells[6].ToolTipText = finding.Note;
            row.DefaultCellStyle.BackColor = finding.Severity switch
            {
                Severity.Error => ErrorBackColor,
                Severity.Warning => WarningBackColor,
                _ => SystemColors.Window
            };
            if (!canFix)
            {
                row.ReadOnly = true;
                row.DefaultCellStyle.ForeColor = NoteForeColor;
            }

            added++;
        }

        return added;
    }

    public static bool IsNoteRow(DataGridView grid, int rowIndex) => rowIndex >= 0 && rowIndex < grid.Rows.Count && grid.Rows[rowIndex].Tag is Finding finding && finding.Action == RepairAction.None;

    public static Finding? ToggleFinding(DataGridView grid, int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= grid.Rows.Count || grid.Rows[rowIndex].Tag is not Finding finding || finding.Action == RepairAction.None)
        {
            return null;
        }

        finding.Selected = !finding.Selected;
        grid.Rows[rowIndex].Cells[0].Value = finding.Selected;
        grid.InvalidateRow(rowIndex);
        return finding;
    }

    public static void PaintFindingCell(DataGridView grid, DataGridViewCellPaintingEventArgs e)
    {
        if (e.ColumnIndex == 0 && IsNoteRow(grid, e.RowIndex))
        {
            PaintWord(grid, e, NoteText);
        }
    }

    public static void AddScanColumns(DataGridView grid)
    {
        Add(grid, new DataGridViewCheckBoxColumn { HeaderText = "Fix", Width = 44, Name = "Fix", ReadOnly = true, ToolTipText = "Tick the projects to repair. ok = healthy, review = needs a decision in the Doctor, error = could not be examined." });
        Add(grid, new DataGridViewTextBoxColumn { HeaderText = "Project", Width = 260, ReadOnly = true, ToolTipText = "The project file name." });
        Add(grid, new DataGridViewTextBoxColumn { HeaderText = "Folder", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true, ToolTipText = "Where the project is." });
        Add(grid, new DataGridViewTextBoxColumn { HeaderText = "Status", Width = 200, ReadOnly = true, ToolTipText = "Healthy, or the count of errors and warnings. (text files only) means the drawings were not opened." });
        Add(grid, new DataGridViewTextBoxColumn { HeaderText = "Recommended", Width = 110, ReadOnly = true, ToolTipText = "How many safe repairs the doctor would apply." });
    }

    public static string? ScanWord(DataGridViewRow row) => row.Tag switch
    {
        Diagnosis { Recommended: > 0 } => null,
        Diagnosis { Healthy: true } => "ok",
        Diagnosis => "review",
        _ => "error"
    };

    public static void SetScanRow(DataGridViewRow row, Diagnosis? diagnosis, string wdp, string? error, bool ticked)
    {
        row.Tag = (object?)diagnosis ?? wdp;
        if (diagnosis is null)
        {
            row.Cells[0].Value = false;
            row.Cells[3].Value = "ERROR: " + error;
            row.Cells[4].Value = 0;
            row.DefaultCellStyle.BackColor = ErrorBackColor;
        }
        else
        {
            row.Cells[0].Value = ticked && diagnosis.Recommended > 0;
            row.Cells[3].Value = diagnosis.Status + (diagnosis.DrawingsChecked ? "" : " (text files only)");
            row.Cells[4].Value = diagnosis.Recommended;
            row.DefaultCellStyle.BackColor = diagnosis.Healthy ? HealthyBackColor : diagnosis.Errors > 0 ? ErrorBackColor : WarningBackColor;
        }

        string? word = ScanWord(row);
        row.ReadOnly = word is not null;
        row.Cells[0].ToolTipText = word switch
        {
            null => "Tick to include this project in Repair ticked (recommended repairs only). Double-click the row to review it in the Doctor.",
            "ok" => "Healthy: nothing to repair.",
            "review" => "The findings need a decision. Double-click the row, or press Open in Doctor..., to review them.",
            _ => "The project could not be examined; see the Status column."
        };
    }

    public static bool ToggleScan(DataGridView grid, int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= grid.Rows.Count || ScanWord(grid.Rows[rowIndex]) is not null)
        {
            return false;
        }

        DataGridViewRow row = grid.Rows[rowIndex];
        row.Cells[0].Value = row.Cells[0].Value is not true;
        grid.InvalidateRow(rowIndex);
        return true;
    }

    public static void PaintScanCell(DataGridView grid, DataGridViewCellPaintingEventArgs e)
    {
        if (e.ColumnIndex != 0 || e.RowIndex < 0 || e.RowIndex >= grid.Rows.Count)
        {
            return;
        }

        string? word = ScanWord(grid.Rows[e.RowIndex]);
        if (word is not null)
        {
            PaintWord(grid, e, word);
        }
    }

    public static void PaintWord(DataGridView grid, DataGridViewCellPaintingEventArgs e, string word)
    {
        if (e.Graphics is null)
        {
            return;
        }

        e.PaintBackground(e.ClipBounds, true);
        TextRenderer.DrawText(e.Graphics, word, e.CellStyle?.Font ?? grid.Font, e.CellBounds, NoteForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        e.Handled = true;
    }

    private static void Add(DataGridView grid, DataGridViewColumn column)
    {
        column.SortMode = DataGridViewColumnSortMode.NotSortable;
        grid.Columns.Add(column);
    }
}
