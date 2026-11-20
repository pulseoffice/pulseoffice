using PulseOffice.Document.Embedding;
using PulseOffice.Document.Tables;

namespace PulseOffice.Presentations.UI;

public sealed record DeckCellLayout(TableCellMargins Margins, CellTextDirection Direction);

public interface IDeckTableCellLayout
{
    DeckCellLayout Of(DataTableObject table, TableCellRange? cells);

    DataTableObject Apply(DataTableObject table, TableCellRange? cells, DeckCellLayout layout);
}

public sealed class DeckTableCellLayout : IDeckTableCellLayout
{
    private const double DefaultSide = 7.2;
    private const double DefaultEdge = 3.6;

    public DeckCellLayout Of(DataTableObject table, TableCellRange? cells)
    {
        ArgumentNullException.ThrowIfNull(table);
        var (row, column) = cells is { } range ? (range.Top, range.Left) : (FirstRow(table), 0);
        var format = new TableCellIndex(table).Format(row, column);
        return new DeckCellLayout(format?.Margins ?? new TableCellMargins(DefaultSide, DefaultSide, DefaultEdge, DefaultEdge),
            format?.Direction ?? CellTextDirection.Horizontal);
    }

    public DataTableObject Apply(DataTableObject table, TableCellRange? cells, DeckCellLayout layout)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(layout);
        var range = (cells ?? new TableCellRange(FirstRow(table), 0, table.Snapshot.Rows.Count - 1, table.Snapshot.Columns.Count - 1)).Normalized;
        var index = new TableCellIndex(table);
        var formats = table.CellFormats.ToList();
        for (var row = Math.Max(range.Top, FirstRow(table)); row <= Math.Min(range.Bottom, table.Snapshot.Rows.Count - 1); row++)
            for (var column = Math.Max(0, range.Left); column <= Math.Min(range.Right, table.Snapshot.Columns.Count - 1); column++)
            {
                if (index.Cover(row, column) is not null) continue;
                var at = formats.FindIndex(format => format.Row == row && format.Column == column);
                var before = at >= 0 ? formats[at] : new TableCellFormat(row, column, CellVerticalAlign.Top);
                var after = before with { Margins = layout.Margins, Direction = layout.Direction };
                if (at >= 0) formats[at] = after;
                else formats.Add(after);
            }
        return table with { CellFormats = formats };
    }

    private static int FirstRow(DataTableObject table) => table.Style.HeaderRow ? TableCellFormat.HeaderRow : 0;
}
