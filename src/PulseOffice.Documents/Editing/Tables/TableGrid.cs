using PulseOffice.Document.Tables;
using PulseOffice.Documents.Blocks;

namespace PulseOffice.Documents.Editing.Tables;

public sealed record CellOrigin(TableCell Cell, int Row, int Column);

public sealed record TableSelection(TableCellLocation At, TableCellRange Range)
{
    public TableBlock Table => At.Table;
}

public interface ITableGrid
{
    CellOrigin? Origin(TableBlock table, int row, int column);

    IReadOnlyList<CellSpan> Spans(TableBlock table);

    IEnumerable<CellOrigin> Cells(TableBlock table, TableCellRange range);

    TableCellRange Whole(TableBlock table);

    string PlainText(TableCell cell);
}

public sealed class TableGrid(IBlockTree tree) : ITableGrid
{
    public CellOrigin? Origin(TableBlock table, int row, int column)
    {
        for (var r = 0; r <= row && r < table.Rows.Count; r++)
        {
            var cells = table.Rows[r].Cells;
            for (var c = 0; c <= column && c < cells.Count; c++)
            {
                var cell = cells[c];
                if (!cell.Covered && r + Math.Max(1, cell.RowSpan) > row && c + Math.Max(1, cell.ColumnSpan) > column) return new CellOrigin(cell, r, c);
            }
        }
        return null;
    }

    public IReadOnlyList<CellSpan> Spans(TableBlock table)
    {
        var spans = new List<CellSpan>();
        for (var r = 0; r < table.Rows.Count; r++)
        {
            var cells = table.Rows[r].Cells;
            for (var c = 0; c < cells.Count; c++)
            {
                var cell = cells[c];
                if (!cell.Covered && (cell.RowSpan > 1 || cell.ColumnSpan > 1)) spans.Add(new CellSpan(r, c, Math.Max(1, cell.RowSpan), Math.Max(1, cell.ColumnSpan)));
            }
        }
        return spans;
    }

    public IEnumerable<CellOrigin> Cells(TableBlock table, TableCellRange range)
    {
        for (var r = range.Top; r <= range.Bottom && r < table.Rows.Count; r++)
        {
            var cells = table.Rows[r].Cells;
            for (var c = range.Left; c <= range.Right && c < cells.Count; c++)
                if (!cells[c].Covered) yield return new CellOrigin(cells[c], r, c);
        }
    }

    public TableCellRange Whole(TableBlock table) =>
        new(0, 0, Math.Max(0, table.Rows.Count - 1), Math.Max(0, table.Rows.Select(row => row.Cells.Count).DefaultIfEmpty(0).Max() - 1));

    public string PlainText(TableCell cell) =>
        string.Join("\n", tree.Paragraphs(cell.Content).Select(paragraph => paragraph.Text.Replace('\u2028', '\n')));
}
