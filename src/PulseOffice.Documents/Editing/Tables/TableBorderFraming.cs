using PulseOffice.Document.Borders;
using PulseOffice.Document.Commands;
using PulseOffice.Documents.Editing.Borders;

namespace PulseOffice.Documents.Editing.Tables;

public interface ITableBorderFraming
{
    BorderFrameScope Scope(TableSelection selection);

    BorderFrame FrameOf(TableSelection selection);

    DocumentEdit Apply(TextDocument document, UndoStack history, TableSelection selection, BorderFrame before, BorderFrame after);
}

public sealed class TableBorderFraming(ITableGrid grid, IBoxBorderFrames frames, IBorderFrameResolver resolver) : ITableBorderFraming
{
    public BorderFrameScope Scope(TableSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        return new BorderFrameScope(selection.Range.RowCount > 1, selection.Range.ColumnCount > 1, false);
    }

    public BorderFrame FrameOf(TableSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        var range = selection.Range;
        var cells = grid.Cells(selection.Table, range).ToList();
        if (cells.Count == 0) return BorderFrame.Empty;
        var first = cells[0].Cell.Borders;
        var last = cells[^1].Cell.Borders;
        return frames.Frame(first, last, range.RowCount > 1, range.ColumnCount > 1);
    }

    public DocumentEdit Apply(TextDocument document, UndoStack history, TableSelection selection, BorderFrame before, BorderFrame after)
    {
        ArgumentNullException.ThrowIfNull(selection);
        var range = selection.Range;
        var recorder = new EditRecorder(document, "set-table-borders").Touch(selection.Table);
        foreach (var cell in grid.Cells(selection.Table, range))
        {
            var placement = resolver.Place(
                cell.Row, cell.Column, range.Top, range.Left,
                range.Bottom - Math.Max(1, cell.Cell.RowSpan) + 1, range.Right - Math.Max(1, cell.Cell.ColumnSpan) + 1);
            var edits = resolver.Resolve(before, after, placement);
            if (edits.IsEmpty) continue;
            cell.Cell.Borders = frames.Apply(cell.Cell.Borders, edits);
        }
        return recorder.Commit(history);
    }
}
