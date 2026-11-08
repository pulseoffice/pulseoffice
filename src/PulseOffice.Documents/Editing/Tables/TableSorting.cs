using System.Globalization;
using PulseOffice.Document.Commands;
using PulseOffice.Document.Tables;
using PulseOffice.Documents.Blocks;

namespace PulseOffice.Documents.Editing.Tables;

public interface ITableSorting
{
    bool CanSort(TableSelection selection);

    TableCellRange SortRange(TableSelection selection);

    DocumentEdit Sort(TextDocument document, UndoStack history, TableSelection selection, IReadOnlyList<TableSortKey> keys, CultureInfo culture);
}

public sealed class TableSorting(ITableGrid grid, ITableSorter sorter) : ITableSorting
{
    public TableCellRange SortRange(TableSelection selection)
    {
        if (!selection.Range.IsSingleCell) return selection.Range;
        var table = selection.Table;
        var whole = grid.Whole(table);
        return whole with { FirstRow = Math.Min(table.HeaderRows, whole.LastRow) };
    }

    public bool CanSort(TableSelection selection)
    {
        var range = SortRange(selection);
        if (range.RowCount < 2 || range.Bottom >= selection.Table.Rows.Count) return false;
        for (var r = range.Top; r <= range.Bottom; r++)
            for (var c = range.Left; c <= range.Right; c++)
            {
                var cells = selection.Table.Rows[r].Cells;
                if (c >= cells.Count) return false;
                var cell = cells[c];
                if (cell.Covered || cell.RowSpan > 1 || c + Math.Max(1, cell.ColumnSpan) - 1 > range.Right) return false;
            }
        return true;
    }

    public DocumentEdit Sort(TextDocument document, UndoStack history, TableSelection selection, IReadOnlyList<TableSortKey> keys, CultureInfo culture)
    {
        var table = selection.Table;
        var range = SortRange(selection);
        var recorder = new EditRecorder(document, "sort-table").Touch(table);
        if (!CanSort(selection)) return recorder.Commit(history);
        var rows = Enumerable.Range(range.Top, range.RowCount).ToList();
        var texts = rows.Select(r => (IReadOnlyList<string>)[.. Enumerable.Range(range.Left, range.ColumnCount).Select(c => grid.PlainText(table.Rows[r].Cells[c]))]).ToList();
        var shifted = keys.Select(key => key with { Column = key.Column - range.Left }).ToList();
        var order = sorter.Order(texts, shifted, culture);
        var slices = rows.Select(r => table.Rows[r].Cells.Skip(range.Left).Take(range.ColumnCount).ToList()).ToList();
        for (var index = 0; index < rows.Count; index++)
        {
            var target = table.Rows[rows[index]].Cells;
            var source = slices[order[index]];
            for (var c = 0; c < source.Count; c++) target[range.Left + c] = source[c];
        }
        return recorder.Commit(history);
    }
}
