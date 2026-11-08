using System.Globalization;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Pivot;

public interface IPivotEngine
{
    PivotTable Build(PivotDefinition definition, IPivotSource source);
}

public sealed class PivotEngine(IPivotAggregator aggregator, IPivotGrouping grouping, IPivotFormulas formulas) : IPivotEngine
{
    private readonly ValueDisplays _displays = new();

    private sealed record SourceRow(IReadOnlyList<string> RowPath, IReadOnlyList<string> ColumnPath, int Index);

    public PivotTable Build(PivotDefinition definition, IPivotSource source)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(source);

        var rowFields = definition.In(PivotArea.Row).ToList();
        var columnFields = definition.In(PivotArea.Column).ToList();
        var valueFields = definition.In(PivotArea.Value).ToList();
        var filterFields = definition.In(PivotArea.Filter).ToList();

        if (valueFields.Count == 0) return new PivotTable([], [], new CellValue[0, 0]);

        var rows = new List<SourceRow>();

        for (var index = 0; index < source.RowCount; index++)
        {
            if (IsExcluded(source, index, rowFields.Concat(columnFields).Concat(filterFields))) continue;

            rows.Add(new SourceRow(
                PathOf(source, index, rowFields), PathOf(source, index, columnFields), index));
        }

        var rowNodes = Axis(rows.Select(row => row.RowPath), rowFields, definition.ShowGrandTotalRows);
        var columnNodes = Axis(rows.Select(row => row.ColumnPath), columnFields, definition.ShowGrandTotalColumns);

        var columns = new List<PivotColumn>();

        foreach (var node in columnNodes)
            for (var index = 0; index < valueFields.Count; index++)
                columns.Add(new PivotColumn(node, valueFields[index], index));

        var cells = new CellValue[rowNodes.Count, columns.Count];
        var compiled = valueFields
            .Where(field => field.IsCalculated)
            .Distinct()
            .ToDictionary(field => field, field => formulas.Compile(field.Formula!, source.Headers));

        var buckets = Buckets(rows, rowNodes, columnNodes);

        for (var r = 0; r < rowNodes.Count; r++)
            for (var c = 0; c < columns.Count; c++)
            {
                var inCell = buckets.TryGetValue((r, c / valueFields.Count), out var bucket) ? bucket : [];

                if (compiled.TryGetValue(columns[c].Field, out var formula))
                {
                    cells[r, c] = inCell.Count == 0
                        ? CellValue.Empty
                        : formulas.Evaluate(formula, [.. formula.Columns.Select(column => SumOf(source, inCell, column))]);
                    continue;
                }

                var matching = inCell
                    .Select(row => source.ValueAt(row.Index, columns[c].Field.SourceColumnIndex))
                    .ToList();

                cells[r, c] = matching.Count == 0
                    ? CellValue.Empty
                    : aggregator.Aggregate(columns[c].Field.Aggregation, matching);
            }

        _displays.Apply(cells, rowNodes, columns);

        return new PivotTable(rowNodes, columns, cells);
    }

    private Dictionary<(int Row, int Column), List<SourceRow>> Buckets(
        IReadOnlyList<SourceRow> rows, IReadOnlyList<PivotNode> rowNodes, IReadOnlyList<PivotNode> columnNodes)
    {
        var rowIndex = NodesByPath(rowNodes);
        var columnIndex = NodesByPath(columnNodes);
        var buckets = new Dictionary<(int, int), List<SourceRow>>();

        foreach (var row in rows)
        {
            var rowTargets = Targets(rowIndex, row.RowPath);
            var columnTargets = Targets(columnIndex, row.ColumnPath);
            foreach (var r in rowTargets)
                foreach (var c in columnTargets)
                {
                    if (!buckets.TryGetValue((r, c), out var bucket)) buckets[(r, c)] = bucket = [];
                    bucket.Add(row);
                }
        }

        return buckets;
    }

    private Dictionary<IReadOnlyList<string>, List<int>> NodesByPath(IReadOnlyList<PivotNode> nodes)
    {
        var index = new Dictionary<IReadOnlyList<string>, List<int>>(PathComparer.Instance);
        for (var position = 0; position < nodes.Count; position++)
        {
            if (!index.TryGetValue(nodes[position].Path, out var list)) index[nodes[position].Path] = list = [];
            list.Add(position);
        }
        return index;
    }

    private List<int> Targets(Dictionary<IReadOnlyList<string>, List<int>> index, IReadOnlyList<string> path)
    {
        var targets = new List<int>();
        for (var length = 0; length <= path.Count; length++)
            if (index.TryGetValue(new PathPrefix(path, length), out var nodes)) targets.AddRange(nodes);
        return targets;
    }

    private double SumOf(IPivotSource source, IReadOnlyList<SourceRow> rows, int column) =>
        rows.Select(row => source.ValueAt(row.Index, column))
            .Where(value => value.Kind == ValueKind.Number)
            .Sum(value => value.AsNumber());

    private bool IsExcluded(IPivotSource source, int row, IEnumerable<PivotField> fields)
    {
        foreach (var field in fields)
        {
            if (field.ExcludedValues.Count == 0) continue;

            var key = KeyOf(source, row, field);
            if (field.ExcludedValues.Contains(key)) return true;
        }

        return false;
    }

    private IReadOnlyList<string> PathOf(IPivotSource source, int row, IReadOnlyList<PivotField> fields) =>
        [.. fields.Select(field => KeyOf(source, row, field))];

    private string KeyOf(IPivotSource source, int row, PivotField field) =>
        grouping.KeyOf(source.ValueAt(row, field.SourceColumnIndex), field);

    private List<PivotNode> Axis(
        IEnumerable<IReadOnlyList<string>> paths, IReadOnlyList<PivotField> fields, bool grandTotal)
    {
        var nodes = new List<PivotNode>();

        if (fields.Count == 0)
        {
            nodes.Add(new PivotNode([], PivotNodeKind.GrandTotal));
            return nodes;
        }

        void Walk(IReadOnlyList<string> prefix, int depth, IReadOnlyList<IReadOnlyList<string>> subset)
        {
            var field = fields[depth];

            var groups = new Dictionary<string, List<IReadOnlyList<string>>>(StringComparer.Ordinal);
            foreach (var path in subset)
            {
                if (!groups.TryGetValue(path[depth], out var members)) groups[path[depth]] = members = [];
                members.Add(path);
            }

            var items = groups.Keys.ToList();
            items.Sort((left, right) => Compare(left, right, field));

            foreach (var item in items)
            {
                var path = new List<string>(prefix) { item };
                nodes.Add(new PivotNode(path, PivotNodeKind.Item));

                if (depth + 1 >= fields.Count) continue;

                Walk(path, depth + 1, groups[item]);

                if (field.ShowSubtotals) nodes.Add(new PivotNode(path, PivotNodeKind.Subtotal));
            }
        }

        Walk([], 0, [.. paths]);

        if (grandTotal) nodes.Add(new PivotNode([], PivotNodeKind.GrandTotal));

        return nodes;
    }

    private int Compare(string left, string right, PivotField field)
    {
        var result = grouping.OrderOf(left, field) is { } leftStart && grouping.OrderOf(right, field) is { } rightStart
            ? leftStart.CompareTo(rightStart)
            : double.TryParse(left, NumberStyles.Any, CultureInfo.InvariantCulture, out var leftNumber)
            && double.TryParse(right, NumberStyles.Any, CultureInfo.InvariantCulture, out var rightNumber)
                ? leftNumber.CompareTo(rightNumber)
                : string.Compare(left, right, StringComparison.CurrentCulture);

        return field.SortDescending ? -result : result;
    }
}

internal sealed class PathPrefix(IReadOnlyList<string> path, int length) : IReadOnlyList<string>
{
    public string this[int index] => path[index];

    public int Count => length;

    public IEnumerator<string> GetEnumerator() => path.Take(length).GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

internal sealed class PathComparer : IEqualityComparer<IReadOnlyList<string>>
{
    public static readonly PathComparer Instance = new();

    public bool Equals(IReadOnlyList<string>? left, IReadOnlyList<string>? right)
    {
        if (left is null || right is null) return ReferenceEquals(left, right);
        if (left.Count != right.Count) return false;
        for (var index = 0; index < left.Count; index++)
            if (!string.Equals(left[index], right[index], StringComparison.Ordinal)) return false;
        return true;
    }

    public int GetHashCode(IReadOnlyList<string> path)
    {
        var hash = new HashCode();
        for (var index = 0; index < path.Count; index++) hash.Add(path[index], StringComparer.Ordinal);
        return hash.ToHashCode();
    }
}

internal sealed class ValueDisplays
{
    private readonly IReadOnlyDictionary<ValueDisplayMode, ValueDisplay> _displays = new ValueDisplay[]
    {
        new ShareOfTotal(),
        new ShareOfColumn(),
        new ShareOfRow(),
        new RunningTotal(),
        new PercentRunningTotal(),
        new Rank(ValueDisplayMode.RankAscending, (value, number) => value < number),
        new Rank(ValueDisplayMode.RankDescending, (value, number) => value > number),
    }.ToDictionary(display => display.Mode);

    public void Apply(CellValue[,] cells, IReadOnlyList<PivotNode> rows, IReadOnlyList<PivotColumn> columns)
    {
        var raw = (CellValue[,])cells.Clone();

        for (var c = 0; c < columns.Count; c++)
        {
            if (!_displays.TryGetValue(columns[c].Field.DisplayMode, out var display)) continue;

            var pass = new DisplayPass(raw, rows, columns, c);
            for (var r = 0; r < rows.Count; r++)
                if (raw[r, c].IsNumeric && display.Show(pass, r, raw[r, c].AsNumber()) is { } shown)
                    cells[r, c] = shown;
        }
    }
}

internal sealed class DisplayPass
{
    public DisplayPass(CellValue[,] cells, IReadOnlyList<PivotNode> rows, IReadOnlyList<PivotColumn> columns, int column)
    {
        Cells = cells;
        Rows = rows;
        Columns = columns;
        Column = column;
        _leafDepth = rows.Where(node => node.Kind == PivotNodeKind.Item).Select(node => node.Path.Count).DefaultIfEmpty(0).Max();

        var items = Enumerable.Range(0, rows.Count)
            .Where(r => IsItem(r) && cells[r, column].IsNumeric)
            .Select(r => cells[r, column].AsNumber())
            .ToList();

        Values = items;
        Total = items.Sum();
        GrandTotal = Enumerable.Range(0, rows.Count).Where(IsItem).Sum(RowTotal);
    }

    public CellValue[,] Cells { get; }

    public IReadOnlyList<PivotNode> Rows { get; }

    public IReadOnlyList<PivotColumn> Columns { get; }

    public int Column { get; }

    public IReadOnlyList<double> Values { get; }

    public double Total { get; }

    public double GrandTotal { get; }

    public double Running { get; set; }

    private readonly int _leafDepth;

    public bool IsItem(int row) => Rows[row].Kind == PivotNodeKind.Item && Rows[row].Path.Count == _leafDepth;

    public double RowTotal(int row)
    {
        var field = Columns[Column].FieldIndex;
        var leafColumns = Enumerable.Range(0, Columns.Count)
            .Where(c => Columns[c].FieldIndex == field && Columns[c].Node.Kind == PivotNodeKind.Item)
            .ToList();

        if (leafColumns.Count > 0)
        {
            var depth = leafColumns.Max(c => Columns[c].Node.Path.Count);
            leafColumns = [.. leafColumns.Where(c => Columns[c].Node.Path.Count == depth)];
        }
        else
        {
            leafColumns = [.. Enumerable.Range(0, Columns.Count)
                .Where(c => Columns[c].FieldIndex == field && Columns[c].Node.Kind == PivotNodeKind.GrandTotal)];
        }

        var total = 0.0;
        foreach (var c in leafColumns)
            if (Cells[row, c].IsNumeric)
                total += Cells[row, c].AsNumber();

        return total;
    }
}

internal abstract class ValueDisplay(ValueDisplayMode mode)
{
    public ValueDisplayMode Mode => mode;

    public abstract CellValue? Show(DisplayPass pass, int row, double number);

    protected CellValue Ratio(double part, double whole) =>
        whole == 0 ? CellValue.FromError(CellError.DivideByZero) : CellValue.Number(part / whole);
}

internal sealed class ShareOfTotal() : ValueDisplay(ValueDisplayMode.PercentOfTotal)
{
    public override CellValue? Show(DisplayPass pass, int row, double number) => Ratio(number, pass.GrandTotal);
}

internal sealed class ShareOfColumn() : ValueDisplay(ValueDisplayMode.PercentOfColumn)
{
    public override CellValue? Show(DisplayPass pass, int row, double number) => Ratio(number, pass.Total);
}

internal sealed class ShareOfRow() : ValueDisplay(ValueDisplayMode.PercentOfRow)
{
    public override CellValue? Show(DisplayPass pass, int row, double number) => Ratio(number, pass.RowTotal(row));
}

internal sealed class RunningTotal() : ValueDisplay(ValueDisplayMode.RunningTotal)
{
    public override CellValue? Show(DisplayPass pass, int row, double number)
    {
        if (!pass.IsItem(row)) return null;
        pass.Running += number;
        return CellValue.Number(pass.Running);
    }
}

internal sealed class PercentRunningTotal() : ValueDisplay(ValueDisplayMode.PercentRunningTotal)
{
    public override CellValue? Show(DisplayPass pass, int row, double number)
    {
        if (!pass.IsItem(row)) return null;
        pass.Running += number;
        return Ratio(pass.Running, pass.Total);
    }
}

internal sealed class Rank(ValueDisplayMode mode, Func<double, double, bool> before) : ValueDisplay(mode)
{
    public override CellValue? Show(DisplayPass pass, int row, double number) =>
        pass.IsItem(row)
            ? CellValue.Number(pass.Values.Count(value => before(value, number)) + 1)
            : CellValue.Empty;
}
