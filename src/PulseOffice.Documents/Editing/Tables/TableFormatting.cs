using PulseOffice.Document.Commands;
using PulseOffice.Document.Tables;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Editing.Tables;

public enum CellBorderPreset
{
    None,
    All,
    Outer,
    Inner,
    OuterThickInnerThin,
    Top,
    Bottom,
    Left,
    Right,
    Horizontal,
}

public interface ITableFormatting
{
    DocumentEdit SetBorders(TextDocument document, UndoStack history, TableSelection selection, CellBorderPreset preset, BorderLine line);

    DocumentEdit ClearBorders(TextDocument document, UndoStack history, TableSelection selection);

    DocumentEdit SetBackground(TextDocument document, UndoStack history, TableSelection selection, uint? color);

    DocumentEdit SetVerticalAlign(TextDocument document, UndoStack history, TableSelection selection, CellVerticalAlign align);

    DocumentEdit SetTextDirection(TextDocument document, UndoStack history, TableSelection selection, CellTextDirection direction);

    DocumentEdit SetHeaderRows(TextDocument document, UndoStack history, TableBlock table, int count, bool repeat);

    DocumentEdit SetPlacement(TextDocument document, UndoStack history, TableBlock table, TableAlignment alignment, Twip indent);

    DocumentEdit SetRowBreak(TextDocument document, UndoStack history, TableSelection selection, bool allow);

    DocumentEdit SetName(TextDocument document, UndoStack history, TableBlock table, string? name);

    DocumentEdit ApplyStyle(TextDocument document, UndoStack history, TableBlock table, string styleName);

    string EnsurePreset(TextDocument document, UndoStack history, TableLook look, string name);
}

public sealed class TableFormatting(ITableGrid grid, IStyleEditing styles, IBlockTree tree) : ITableFormatting
{
    public DocumentEdit SetBorders(TextDocument document, UndoStack history, TableSelection selection, CellBorderPreset preset, BorderLine line)
    {
        var range = selection.Range;
        var thick = line with { Width = line.Width * 2.5 };
        var recorder = new EditRecorder(document, "set-table-borders").Touch(selection.Table);
        foreach (var cell in grid.Cells(selection.Table, range))
        {
            var top = cell.Row == range.Top;
            var bottom = cell.Row + Math.Max(1, cell.Cell.RowSpan) - 1 >= range.Bottom;
            var left = cell.Column == range.Left;
            var right = cell.Column + Math.Max(1, cell.Cell.ColumnSpan) - 1 >= range.Right;
            var current = cell.Cell.Borders ?? new BoxBorders();
            cell.Cell.Borders = preset switch
            {
                CellBorderPreset.None => new BoxBorders(),
                CellBorderPreset.All => BoxBorders.Uniform(line, 0),
                CellBorderPreset.Outer => new BoxBorders { Top = top ? line : current.Top, Bottom = bottom ? line : current.Bottom, Left = left ? line : current.Left, Right = right ? line : current.Right },
                CellBorderPreset.Inner => new BoxBorders { Top = top ? current.Top : line, Bottom = bottom ? current.Bottom : line, Left = left ? current.Left : line, Right = right ? current.Right : line },
                CellBorderPreset.OuterThickInnerThin => new BoxBorders { Top = top ? thick : line, Bottom = bottom ? thick : line, Left = left ? thick : line, Right = right ? thick : line },
                CellBorderPreset.Top => current with { Top = top ? line : current.Top },
                CellBorderPreset.Bottom => current with { Bottom = bottom ? line : current.Bottom },
                CellBorderPreset.Left => current with { Left = left ? line : current.Left },
                CellBorderPreset.Right => current with { Right = right ? line : current.Right },
                _ => current with { Top = line, Bottom = line },
            };
        }
        return recorder.Commit(history);
    }

    public DocumentEdit ClearBorders(TextDocument document, UndoStack history, TableSelection selection)
    {
        var recorder = new EditRecorder(document, "reset-table-borders").Touch(selection.Table);
        foreach (var cell in grid.Cells(selection.Table, selection.Range)) cell.Cell.Borders = null;
        return recorder.Commit(history);
    }

    public DocumentEdit SetBackground(TextDocument document, UndoStack history, TableSelection selection, uint? color)
    {
        var recorder = new EditRecorder(document, "set-table-background").Touch(selection.Table);
        foreach (var cell in grid.Cells(selection.Table, selection.Range)) cell.Cell.Background = color;
        return recorder.Commit(history);
    }

    public DocumentEdit SetVerticalAlign(TextDocument document, UndoStack history, TableSelection selection, CellVerticalAlign align)
    {
        var recorder = new EditRecorder(document, "set-table-cell-alignment").Touch(selection.Table);
        foreach (var cell in grid.Cells(selection.Table, selection.Range)) cell.Cell.VerticalAlign = align;
        return recorder.Commit(history);
    }

    public DocumentEdit SetTextDirection(TextDocument document, UndoStack history, TableSelection selection, CellTextDirection direction)
    {
        var recorder = new EditRecorder(document, "set-table-cell-direction").Touch(selection.Table);
        foreach (var cell in grid.Cells(selection.Table, selection.Range)) cell.Cell.TextDirection = direction;
        return recorder.Commit(history);
    }

    public DocumentEdit SetHeaderRows(TextDocument document, UndoStack history, TableBlock table, int count, bool repeat)
    {
        var recorder = new EditRecorder(document, "set-table-header").Touch(table);
        table.HeaderRows = Math.Clamp(count, 0, table.Rows.Count);
        table.RepeatHeaderRows = repeat;
        return recorder.Commit(history);
    }

    public DocumentEdit SetPlacement(TextDocument document, UndoStack history, TableBlock table, TableAlignment alignment, Twip indent)
    {
        var recorder = new EditRecorder(document, "set-table-placement").Touch(table);
        table.Alignment = alignment;
        table.Indent = alignment == TableAlignment.Left ? indent : Twip.Zero;
        return recorder.Commit(history);
    }

    public DocumentEdit SetRowBreak(TextDocument document, UndoStack history, TableSelection selection, bool allow)
    {
        var recorder = new EditRecorder(document, "set-table-row-break").Touch(selection.Table);
        foreach (var row in selection.Table.Rows.Skip(selection.Range.Top).Take(selection.Range.RowCount)) row.AllowBreakAcrossPages = allow;
        return recorder.Commit(history);
    }

    public DocumentEdit SetName(TextDocument document, UndoStack history, TableBlock table, string? name)
    {
        var recorder = new EditRecorder(document, "rename-table").Touch(table);
        table.Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        return recorder.Commit(history);
    }

    public DocumentEdit ApplyStyle(TextDocument document, UndoStack history, TableBlock table, string styleName)
    {
        var recorder = new EditRecorder(document, "apply-table-style").Touch(table);
        table.StyleName = styleName;
        if (table.HeaderRows == 0 && table.Rows.Count > 1) table.HeaderRows = 1;
        foreach (var row in table.Rows)
        {
            foreach (var cell in row.Cells)
            {
                cell.Background = null;
                cell.Borders = null;
            }
        }
        for (var r = 0; r < table.Rows.Count; r++)
        {
            var heading = r < table.HeaderRows;
            foreach (var paragraph in table.Rows[r].Cells.SelectMany(cell => tree.Paragraphs(cell.Content)))
            {
                var wanted = heading ? DefaultStyleNames.TableHeading : DefaultStyleNames.TableContents;
                if (paragraph.StyleName is not (DefaultStyleNames.TableHeading or DefaultStyleNames.TableContents or null) || paragraph.StyleName == wanted) continue;
                recorder.Touch(paragraph);
                paragraph.Content = paragraph.Content with { StyleName = wanted };
            }
        }
        return recorder.Commit(history);
    }

    public string EnsurePreset(TextDocument document, UndoStack history, TableLook look, string name)
    {
        if (document.Styles.Table.Contains(name)) return name;
        styles.Add(document, history, document.Styles.Table, new TableStyle(name)
        {
            ParentName = DefaultStyleNames.DefaultTableStyle,
            Table = new TableProperties
            {
                BorderColor = look.BorderColor,
                BorderWidth = look.BorderWidth,
                HeaderBackground = look.HeaderFill,
                HeaderColor = look.HeaderInk,
                HeaderBold = look.HeaderBold,
                BandBackground = look.BandFill,
                Background = look.BodyFill,
                InnerLines = look.InnerLines,
            },
        }, "add-table-style");
        return name;
    }
}
