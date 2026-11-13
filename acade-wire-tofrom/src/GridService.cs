using System.Drawing;
using System.Windows.Forms;

namespace LSE.AutoCADElectrical.WireToFrom;

// The plan grid of LSETOFROM. The Apply? column is a read-only check-box column: the form toggles it itself on a click
// or the space bar (ToggleRow in ToFromForm) instead of relying on the DataGridView edit cycle, which does not commit
// reliably inside AutoCAD's modal dialog host. Rows that cannot be written (Keep, Skip, a skipped drawing) show the
// word "note" instead of a box. Everything here runs without AutoCAD, so the smoke test covers it on a real grid.
public static class GridService
{
    public const string NoteText = "note";

    public static readonly Color NoteForeColor = Color.FromArgb(110, 110, 110);

    public static readonly Color SkippedBackColor = Color.FromArgb(255, 240, 220);

    public static readonly Color OpenBackColor = Color.FromArgb(255, 250, 205);

    public static void AddColumns(DataGridView grid)
    {
        Add(grid, new DataGridViewCheckBoxColumn { HeaderText = "Apply?", Width = 60, Name = "Apply", ReadOnly = true, ToolTipText = "Click a box (or press the space bar on the row) to tick or untick that label change. Rows that say note cannot be written." });
        Add(grid, new DataGridViewTextBoxColumn { HeaderText = "Drawing", Width = 190, ReadOnly = true, ToolTipText = "The drawing the label sits in." });
        Add(grid, new DataGridViewTextBoxColumn { HeaderText = "Change", Width = 130, ReadOnly = true, ToolTipText = "Insert, update, delete stale label, keep (already right) or skip (cannot be written; the Note says why)." });
        Add(grid, new DataGridViewTextBoxColumn { HeaderText = "Wire no", Width = 80, ReadOnly = true, ToolTipText = "The native wire number (node) of the wire; it is not changed." });
        Add(grid, new DataGridViewTextBoxColumn { HeaderText = "Label", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 30, ReadOnly = true, ToolTipText = "The To/From identifier that will be on the schematic." });
        Add(grid, new DataGridViewTextBoxColumn { HeaderText = "Drawing has", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 25, ReadOnly = true, ToolTipText = "What the existing label reads now (blank for a new label)." });
        Add(grid, new DataGridViewTextBoxColumn { HeaderText = "Key", Width = 110, ReadOnly = true, ToolTipText = "The hidden key (wire handle / end) that ties the label to its wire across runs." });
        Add(grid, new DataGridViewTextBoxColumn { HeaderText = "Note", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 45, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { WrapMode = DataGridViewTriState.True }, ToolTipText = "Why the row is what it is." });
    }

    public static int Fill(DataGridView grid, RunPlan? plan, bool showKeep)
    {
        grid.Rows.Clear();
        if (plan is null)
        {
            return 0;
        }

        int added = 0;
        foreach (DrawingPlan drawing in plan.Drawings)
        {
            if (drawing.SkipReason.Length > 0)
            {
                int skippedRow = grid.Rows.Add(false, drawing.Name, "Drawing skipped", "", "", "", "", drawing.SkipReason);
                DataGridViewRow skipped = grid.Rows[skippedRow];
                skipped.Tag = drawing;
                skipped.ReadOnly = true;
                skipped.DefaultCellStyle.BackColor = SkippedBackColor;
                skipped.DefaultCellStyle.ForeColor = NoteForeColor;
                skipped.Cells[0].ToolTipText = "This drawing was not read: " + drawing.SkipReason;
                added++;
            }

            IEnumerable<LabelChange> changes = drawing.Changes.OrderBy(c => c.Action switch { LabelAction.Insert => 0, LabelAction.Update => 1, LabelAction.Delete => 2, LabelAction.Skip => 3, _ => 4 }).ThenBy(c => c.Key, StringComparer.OrdinalIgnoreCase);
            foreach (LabelChange change in changes)
            {
                if (!showKeep && !change.Writes)
                {
                    continue;
                }

                bool open = drawing.State.OpenInEditor && change.Writes;
                string note = change.Note + (open ? (change.Note.Length > 0 ? "; " : "") + "open in the editor: written in the session, not saved" : "");
                int index = grid.Rows.Add(change.Writes && change.Selected, drawing.Name, change.ActionText, change.End?.Row.WireNo ?? change.Node, change.Text, change.Existing, change.Key, note);
                DataGridViewRow row = grid.Rows[index];
                row.Tag = change;
                row.Cells[0].ToolTipText = change.Writes ? "Click to tick or untick. Ticked rows are written by Apply ticked." : Explain(change);
                row.Cells[7].ToolTipText = note;
                if (open)
                {
                    row.DefaultCellStyle.BackColor = OpenBackColor;
                }

                if (!change.Writes)
                {
                    row.ReadOnly = true;
                    row.DefaultCellStyle.ForeColor = NoteForeColor;
                }

                added++;
            }
        }

        return added;
    }

    public static bool IsNoteRow(DataGridView grid, int rowIndex) => rowIndex >= 0 && rowIndex < grid.Rows.Count && grid.Rows[rowIndex].Tag is not LabelChange { Writes: true };

    public static LabelChange? Toggle(DataGridView grid, int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= grid.Rows.Count || grid.Rows[rowIndex].Tag is not LabelChange { Writes: true } change)
        {
            return null;
        }

        change.Selected = !change.Selected;
        grid.Rows[rowIndex].Cells[0].Value = change.Selected;
        grid.InvalidateRow(rowIndex);
        return change;
    }

    public static string Explain(DataGridView grid, int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= grid.Rows.Count)
        {
            return "";
        }

        return grid.Rows[rowIndex].Tag switch
        {
            DrawingPlan drawing => "Drawing skipped: " + drawing.SkipReason + ". Its labels were not planned; fix the cause and press Plan again.",
            LabelChange change => Explain(change),
            _ => ""
        };
    }

    public static string Explain(LabelChange change) => change.Action switch
    {
        LabelAction.Keep => "Keep: the label already reads \"" + change.Text + "\"; nothing to write.",
        LabelAction.Skip => "Skip: " + (change.Note.Length > 0 ? change.Note : "this row cannot be written") + ".",
        _ => change.ActionText + ": " + change.Note
    };

    public static void PaintCell(DataGridView grid, DataGridViewCellPaintingEventArgs e)
    {
        if (e.ColumnIndex == 0 && IsNoteRow(grid, e.RowIndex))
        {
            PaintWord(grid, e, NoteText);
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
