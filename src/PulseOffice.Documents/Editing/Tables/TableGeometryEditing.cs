using PulseOffice.Document.Commands;
using PulseOffice.Document.Tables;
using PulseOffice.Documents.Blocks;
using PulseOffice.Foundation.Units;

namespace PulseOffice.Documents.Editing.Tables;

public interface ITableGeometryEditing
{
    DocumentEdit SetColumnWidths(TextDocument document, UndoStack history, TableBlock table, IReadOnlyList<double> points, string description);

    DocumentEdit ResizeColumn(TextDocument document, UndoStack history, TableBlock table, int column, double points);

    DocumentEdit DistributeColumns(TextDocument document, UndoStack history, TableSelection selection);

    DocumentEdit FitColumns(TextDocument document, UndoStack history, TableSelection selection, IReadOnlyList<double> wanted, double? limit, string description);

    DocumentEdit SetRowHeight(TextDocument document, UndoStack history, TableSelection selection, double? points, bool minimum);

    DocumentEdit DistributeRows(TextDocument document, UndoStack history, TableSelection selection, IReadOnlyList<double> measured);
}

public sealed class TableGeometryEditing(IColumnWidths widths) : ITableGeometryEditing
{
    public const double MinimumColumn = 14;

    public DocumentEdit SetColumnWidths(TextDocument document, UndoStack history, TableBlock table, IReadOnlyList<double> points, string description)
    {
        var recorder = new EditRecorder(document, description).Touch(table);
        table.Columns.Clear();
        table.Columns.AddRange(points.Select(Twip.FromPoints));
        return recorder.Commit(history);
    }

    public DocumentEdit ResizeColumn(TextDocument document, UndoStack history, TableBlock table, int column, double points) =>
        SetColumnWidths(document, history, table, widths.Resize(Points(table), column, points, MinimumColumn), "resize-table-column");

    public DocumentEdit DistributeColumns(TextDocument document, UndoStack history, TableSelection selection)
    {
        var range = selection.Range.IsSingleCell ? new TableCellRange(0, 0, 0, selection.Table.ColumnCount - 1) : selection.Range;
        return SetColumnWidths(document, history, selection.Table, widths.Distribute(Points(selection.Table), range.Left, range.Right), "distribute-table-columns");
    }

    public DocumentEdit FitColumns(TextDocument document, UndoStack history, TableSelection selection, IReadOnlyList<double> wanted, double? limit, string description)
    {
        var range = selection.Range.IsSingleCell ? new TableCellRange(0, selection.Range.Left, 0, selection.Range.Left) : selection.Range;
        return SetColumnWidths(document, history, selection.Table, widths.Fit(Points(selection.Table), range.Left, range.Right, wanted, MinimumColumn, limit), description);
    }

    public DocumentEdit SetRowHeight(TextDocument document, UndoStack history, TableSelection selection, double? points, bool minimum)
    {
        var recorder = new EditRecorder(document, "set-table-row-height").Touch(selection.Table);
        foreach (var row in Rows(selection))
        {
            row.Height = points is { } value ? Twip.FromPoints(value) : null;
            row.MinimumHeight = minimum;
        }
        return recorder.Commit(history);
    }

    public DocumentEdit DistributeRows(TextDocument document, UndoStack history, TableSelection selection, IReadOnlyList<double> measured)
    {
        var recorder = new EditRecorder(document, "distribute-table-rows").Touch(selection.Table);
        var rows = Rows(selection).ToList();
        var first = selection.Range.Top;
        var heights = rows.Select((_, index) => first + index < measured.Count ? measured[first + index] : 0).ToList();
        var share = heights.Count == 0 ? 0 : heights.Max();
        foreach (var row in rows)
        {
            row.Height = Twip.FromPoints(share);
            row.MinimumHeight = true;
        }
        return recorder.Commit(history);
    }

    private IEnumerable<TableRow> Rows(TableSelection selection) =>
        selection.Table.Rows.Skip(selection.Range.Top).Take(selection.Range.RowCount);

    private List<double> Points(TableBlock table) => [.. table.Columns.Select(column => column.ToPoints())];
}
