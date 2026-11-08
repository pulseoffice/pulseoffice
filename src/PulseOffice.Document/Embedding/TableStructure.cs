using PulseOffice.Document.Tables;

namespace PulseOffice.Document.Embedding;

public interface ITableStructure
{
    DataTableObject InsertRow(DataTableObject table, int at);

    DataTableObject DeleteRows(DataTableObject table, int first, int last);

    DataTableObject InsertColumn(DataTableObject table, int at, DataColumn column);

    DataTableObject DeleteColumns(DataTableObject table, int first, int last);
}

public sealed class TableStructure(ITableSpans spans, IColumnWidths widths) : ITableStructure
{
    public DataTableObject InsertRow(DataTableObject table, int at)
    {
        ArgumentNullException.ThrowIfNull(table);

        var snapshot = table.Snapshot;
        at = Math.Clamp(at, 0, snapshot.Rows.Count);
        var rows = snapshot.Rows.ToList();
        rows.Insert(at, new object?[snapshot.Columns.Count]);

        return table with
        {
            Snapshot = snapshot with { Rows = rows },
            Merges = [.. table.Merges.Select(merge => spans.Inserted(merge.Row, merge.RowSpan, at) is var (row, span) ? merge with { Row = row, RowSpan = span } : merge)],
            CellFormats = [.. table.CellFormats.Select(format => format.Row >= at ? format with { Row = format.Row + 1 } : format),
                .. snapshot.Rows.Count == 0 ? [] : table.CellFormats.Where(format => format.Row == Math.Max(0, at - 1)).Select(format => format with { Row = at })],
            Layout = table.Layout with { RowHeights = InsertedHeight(table.Layout.RowHeights, at + Header(table)) },
        };
    }

    public DataTableObject DeleteRows(DataTableObject table, int first, int last)
    {
        ArgumentNullException.ThrowIfNull(table);

        var count = last - first + 1;
        if (count <= 0) return table;

        return table with
        {
            Snapshot = table.Snapshot with { Rows = [.. table.Snapshot.Rows.Where((_, index) => index < first || index > last)] },
            Merges = [.. table.Merges
                .Select(merge => spans.Deleted(merge.Row, merge.RowSpan, first, last) is var (row, span) ? merge with { Row = row, RowSpan = span } : null)
                .OfType<TableMerge>().Where(Meaningful)],
            CellFormats = [.. table.CellFormats
                .Where(format => format.Row < first || format.Row > last)
                .Select(format => format.Row > last ? format with { Row = format.Row - count } : format)],
            Layout = table.Layout with { RowHeights = Without(table.Layout.RowHeights, first + Header(table), last + Header(table)) },
        };
    }

    public DataTableObject InsertColumn(DataTableObject table, int at, DataColumn column)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(column);

        var snapshot = table.Snapshot;
        at = Math.Clamp(at, 0, snapshot.Columns.Count);
        var columns = snapshot.Columns.ToList();
        columns.Insert(at, column);

        return table with
        {
            Snapshot = snapshot with
            {
                Columns = columns,
                Rows = [.. snapshot.Rows.Select(row =>
                {
                    var values = row.ToList();
                    values.Insert(Math.Min(at, values.Count), null);
                    return (IReadOnlyList<object?>)values;
                })],
            },
            Merges = [.. table.Merges.Select(merge => spans.Inserted(merge.Column, merge.ColumnSpan, at) is var (start, span) ? merge with { Column = start, ColumnSpan = span } : merge)],
            CellFormats = [.. table.CellFormats.Select(format => format.Column >= at ? format with { Column = format.Column + 1 } : format),
                .. snapshot.Columns.Count == 0 ? [] : table.CellFormats.Where(format => format.Column == Math.Max(0, at - 1)).Select(format => format with { Column = at })],
            Layout = table.Layout with { ColumnFractions = InsertedFraction(table.Layout.ColumnFractions, at) },
        };
    }

    public DataTableObject DeleteColumns(DataTableObject table, int first, int last)
    {
        ArgumentNullException.ThrowIfNull(table);

        var count = last - first + 1;
        if (count <= 0) return table;
        bool Keep(int index) => index < first || index > last;

        return table with
        {
            Snapshot = table.Snapshot with
            {
                Columns = [.. table.Snapshot.Columns.Where((_, index) => Keep(index))],
                Rows = [.. table.Snapshot.Rows.Select(row => (IReadOnlyList<object?>)[.. row.Where((_, index) => Keep(index))])],
            },
            Merges = [.. table.Merges
                .Select(merge => spans.Deleted(merge.Column, merge.ColumnSpan, first, last) is var (start, span) ? merge with { Column = start, ColumnSpan = span } : null)
                .OfType<TableMerge>().Where(Meaningful)],
            CellFormats = [.. table.CellFormats
                .Where(format => Keep(format.Column))
                .Select(format => format.Column > last ? format with { Column = format.Column - count } : format)],
            Layout = table.Layout with { ColumnFractions = Normalised(Without(table.Layout.ColumnFractions, first, last)) },
        };
    }

    private int Header(DataTableObject table) => table.Style.HeaderRow ? 1 : 0;

    private bool Meaningful(TableMerge merge) => merge.RowSpan > 1 || merge.ColumnSpan > 1;

    private IReadOnlyList<double> InsertedHeight(IReadOnlyList<double> heights, int at)
    {
        if (heights.Count == 0) return heights;

        var list = heights.ToList();
        var position = Math.Clamp(at, 0, list.Count);
        list.Insert(position, list[Math.Clamp(position - 1, 0, list.Count - 1)]);
        return list;
    }

    private IReadOnlyList<double> InsertedFraction(IReadOnlyList<double> fractions, int at)
    {
        if (fractions.Count == 0) return fractions;

        var list = fractions.ToList();
        list.Insert(Math.Clamp(at, 0, list.Count), 1.0 / list.Count);
        return Normalised(list);
    }

    private IReadOnlyList<double> Without(IReadOnlyList<double> values, int first, int last) =>
        values.Count == 0 ? values : [.. values.Where((_, index) => index < first || index > last)];

    private IReadOnlyList<double> Normalised(IReadOnlyList<double> fractions) =>
        fractions.Sum() <= 0 ? [] : widths.Normalize(fractions);
}
