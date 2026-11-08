using PulseOffice.Document.Tables;
using System.Globalization;

namespace PulseOffice.Document.Embedding;

public interface ITableSizing
{
    string Key { get; }

    TableLayout Apply(DataTableObject table);
}

public abstract class TableSizing : ITableSizing
{
    protected const double StandardRowFactor = 1.9;
    protected const double MinimalRowFactor = 1.4;
    protected const int MinimumCharacters = 2;
    protected const int MaximumCharacters = 40;

    public abstract string Key { get; }

    public abstract TableLayout Apply(DataTableObject table);

    protected int DisplayedRows(DataTableObject table) =>
        table.Snapshot.Rows.Count + (table.Style.HeaderRow ? 1 : 0);

    protected IEnumerable<string> ColumnTexts(DataTableObject table, int column)
    {
        var snapshot = table.Snapshot;
        if (table.Style.HeaderRow) yield return snapshot.Columns[column].Name;

        foreach (var row in snapshot.Rows)
            yield return column < row.Count
                ? Convert.ToString(row[column], CultureInfo.CurrentCulture) ?? string.Empty
                : string.Empty;
    }

}

public abstract class ColumnSizing(IColumnWidths widths) : TableSizing
{
    public override TableLayout Apply(DataTableObject table)
    {
        var columns = table.Snapshot.Columns.Count;
        if (columns == 0) return table.Layout;

        var weights = Enumerable.Range(0, columns)
            .Select(c => (double)Math.Clamp(Measure(ColumnTexts(table, c).SelectMany(Lines)), MinimumCharacters, MaximumCharacters))
            .ToList();

        return table.Layout with { ColumnFractions = widths.Normalize(weights) };
    }

    protected abstract int Measure(IEnumerable<string> texts);

    private IEnumerable<string> Lines(string text) => text.Split('\n');
}

public sealed class OptimalColumnSizing(IColumnWidths widths) : ColumnSizing(widths)
{
    public override string Key => "optimalColumnWidth";

    protected override int Measure(IEnumerable<string> texts) => texts.Select(t => t.Length).DefaultIfEmpty(0).Max();
}

public sealed class MinimalColumnSizing(IColumnWidths widths) : ColumnSizing(widths)
{
    public override string Key => "minimalColumnWidth";

    protected override int Measure(IEnumerable<string> texts) => texts
        .SelectMany(t => t.Split(' ', StringSplitOptions.RemoveEmptyEntries).DefaultIfEmpty(string.Empty))
        .Select(w => w.Length).DefaultIfEmpty(0).Max();
}

public sealed class DistributedColumnSizing : TableSizing
{
    public override string Key => "distributeColumns";

    public override TableLayout Apply(DataTableObject table)
    {
        var columns = table.Snapshot.Columns.Count;
        return columns == 0
            ? table.Layout
            : table.Layout with { ColumnFractions = [.. Enumerable.Repeat(1.0 / columns, columns)] };
    }
}

public abstract class RowSizing : TableSizing
{
    public override TableLayout Apply(DataTableObject table)
    {
        var rows = DisplayedRows(table);
        return table.Layout with { RowHeights = [.. Heights(table, rows)] };
    }

    protected abstract IEnumerable<double> Heights(DataTableObject table, int rows);
}

public sealed class OptimalRowSizing : RowSizing
{
    public override string Key => "optimalRowHeight";

    protected override IEnumerable<double> Heights(DataTableObject table, int rows) =>
        Enumerable.Repeat(table.Style.FontSize * StandardRowFactor, rows);
}

public sealed class MinimalRowSizing : RowSizing
{
    public override string Key => "minimalRowHeight";

    protected override IEnumerable<double> Heights(DataTableObject table, int rows) =>
        Enumerable.Repeat(table.Style.FontSize * MinimalRowFactor, rows);
}

public sealed class DistributedRowSizing : RowSizing
{
    public override string Key => "distributeRows";

    protected override IEnumerable<double> Heights(DataTableObject table, int rows)
    {
        var standard = table.Style.FontSize * StandardRowFactor;
        var current = Enumerable.Range(0, rows).Select(r => table.Layout.RowHeight(r) ?? standard).ToList();
        var mean = current.Count == 0 ? standard : current.Average();
        return Enumerable.Repeat(mean, rows);
    }
}

public static class TableMetrics
{
    public static double RowHeight(DataTableObject table, int row) =>
        table.Layout.RowHeight(row) ?? table.Style.FontSize * 1.9;

    public static double TotalHeight(DataTableObject table)
    {
        var rows = table.Snapshot.Rows.Count + (table.Style.HeaderRow ? 1 : 0);
        return Enumerable.Range(0, rows).Sum(r => RowHeight(table, r));
    }
}

public sealed class TableGeometry
{
    private readonly OptimalColumnSizing _optimal = new(new ColumnWidths());

    public (int Row, int Column)? CellAt(DataTableObject table, double relativeX, double offsetY)
    {
        ArgumentNullException.ThrowIfNull(table);
        var columns = Columns(table);
        var rows = Rows(table);
        if (relativeX < 0 || relativeX > 1 || offsetY < 0 || offsetY > rows.Sum()) return null;
        if (columns.Length == 0 || rows.Length == 0) return null;

        var column = Index(columns, relativeX);
        var row = Index(rows, offsetY) - (table.Style.HeaderRow ? 1 : 0);
        return (row, column);
    }

    public (double X, double Y, double Width, double Height) Area(DataTableObject table, TableCellRange range)
    {
        ArgumentNullException.ThrowIfNull(table);
        var columns = Columns(table);
        var rows = Rows(table);
        if (columns.Length == 0 || rows.Length == 0) return (0, 0, 0, 0);

        var offset = table.Style.HeaderRow ? 1 : 0;
        var firstColumn = Math.Clamp(Math.Min(range.FirstColumn, range.LastColumn), 0, columns.Length - 1);
        var lastColumn = Math.Clamp(Math.Max(range.FirstColumn, range.LastColumn), 0, columns.Length - 1);
        var firstRow = Math.Clamp(Math.Min(range.FirstRow, range.LastRow) + offset, 0, rows.Length - 1);
        var lastRow = Math.Clamp(Math.Max(range.FirstRow, range.LastRow) + offset, 0, rows.Length - 1);

        var x = columns.Take(firstColumn).Sum();
        var y = rows.Take(firstRow).Sum();
        return (x, y, columns.Skip(firstColumn).Take(lastColumn - firstColumn + 1).Sum(), rows.Skip(firstRow).Take(lastRow - firstRow + 1).Sum());
    }

    private double[] Columns(DataTableObject table)
    {
        var count = table.Snapshot.Columns.Count;
        var fractions = table.Layout.ColumnFractions;
        if (fractions.Count != count || !(fractions.Sum() > 0)) fractions = _optimal.Apply(table).ColumnFractions;
        var total = fractions.Sum();
        return fractions.Count == count && total > 0
            ? [.. fractions.Select(fraction => fraction / total)]
            : [.. Enumerable.Repeat(count == 0 ? 0 : 1.0 / count, count)];
    }

    private double[] Rows(DataTableObject table)
    {
        var count = table.Snapshot.Rows.Count + (table.Style.HeaderRow ? 1 : 0);
        return [.. Enumerable.Range(0, count).Select(row => TableMetrics.RowHeight(table, row))];
    }

    private int Index(double[] sizes, double position)
    {
        var edge = 0.0;
        for (var index = 0; index < sizes.Length; index++)
        {
            edge += sizes[index];
            if (position <= edge) return index;
        }
        return sizes.Length - 1;
    }
}
