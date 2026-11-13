using System.Drawing;
using System.Windows.Forms;

namespace LSE.AutoCADElectrical.ProjectEnvironment;

/// <summary>Fills the LSEENV plan grid. Kept out of the form so the smoke test can run it on a real DataGridView.
/// Rows that are notes keep their check-box cell (a cell is never replaced after the row exists, which is what
/// crashed r3) and are made read-only at row level; the form paints the word "note" over the box.</summary>
public static class PlanGridService
{
    public const string NoteText = "note";

    public static readonly Color NoteForeColor = Color.FromArgb(110, 110, 110);

    public static readonly Color NotRecommendedBackColor = Color.FromArgb(255, 250, 230);

    public static void AddColumns(DataGridView grid)
    {
        Add(grid, new DataGridViewCheckBoxColumn { HeaderText = "Apply?", Width = 60, Name = "Apply" });
        Add(grid, new DataGridViewTextBoxColumn { HeaderText = "Scope", Width = 170, ReadOnly = true });
        Add(grid, new DataGridViewTextBoxColumn { HeaderText = "Item", Width = 230, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { WrapMode = DataGridViewTriState.True } });
        Add(grid, new DataGridViewTextBoxColumn { HeaderText = "Project has", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 30, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { WrapMode = DataGridViewTriState.True } });
        Add(grid, new DataGridViewTextBoxColumn { HeaderText = "Becomes", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 30, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { WrapMode = DataGridViewTriState.True } });
        Add(grid, new DataGridViewTextBoxColumn { HeaderText = "Change", Width = 150, ReadOnly = true });
        Add(grid, new DataGridViewTextBoxColumn { HeaderText = "Note", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 40, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { WrapMode = DataGridViewTriState.True } });
    }

    /// <summary>Replaces the grid rows with the plan's changes. Returns the number of rows added.</summary>
    public static int Fill(DataGridView grid, EnvPlan? plan, bool showNotes)
    {
        grid.Rows.Clear();
        if (plan is null)
        {
            return 0;
        }

        int added = 0;
        foreach (Change change in plan.Changes)
        {
            if (!change.Applies && !showNotes)
            {
                continue;
            }

            int index = grid.Rows.Add(change.Applies && change.Selected, change.Scope, change.Item, change.Current, change.New, change.Applies ? change.KindText : NoteText, change.Note);
            DataGridViewRow row = grid.Rows[index];
            row.Tag = change;
            if (!change.Applies)
            {
                row.ReadOnly = true;
                row.DefaultCellStyle.ForeColor = NoteForeColor;
            }
            else if (!change.Recommended)
            {
                row.DefaultCellStyle.BackColor = NotRecommendedBackColor;
            }

            added++;
        }

        return added;
    }

    public static bool IsNoteRow(DataGridView grid, int rowIndex) => rowIndex >= 0 && rowIndex < grid.Rows.Count && grid.Rows[rowIndex].Tag is Change change && !change.Applies;

    /// <summary>Paints the word "note" instead of a check box in the Apply? column of a note row.</summary>
    public static void PaintCell(DataGridView grid, DataGridViewCellPaintingEventArgs e)
    {
        if (e.ColumnIndex != 0 || !IsNoteRow(grid, e.RowIndex) || e.Graphics is null)
        {
            return;
        }

        e.PaintBackground(e.ClipBounds, true);
        TextRenderer.DrawText(e.Graphics, NoteText, e.CellStyle?.Font ?? grid.Font, e.CellBounds, NoteForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        e.Handled = true;
    }

    private static void Add(DataGridView grid, DataGridViewColumn column)
    {
        column.SortMode = DataGridViewColumnSortMode.NotSortable;
        grid.Columns.Add(column);
    }
}
