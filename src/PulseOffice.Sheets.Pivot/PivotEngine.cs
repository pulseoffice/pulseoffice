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

        var hiddenRows = new HashSet<IReadOnlyList<string>>(PathComparer.Instance);
        var hiddenColumns = new HashSet<IReadOnlyList<string>>(PathComparer.Instance);
        var rowNodes = Axis(rows, row => row.RowPath, rowFields, valueFields, source, definition.ShowGrandTotalRows, hiddenRows);
        var columnNodes = Axis(rows, row => row.ColumnPath, columnFields, valueFields, source, definition.ShowGrandTotalColumns, hiddenColumns);
        if (hiddenRows.Count > 0 || hiddenColumns.Count > 0)
            rows = [.. rows.Where(row => !HiddenBy(hiddenRows, row.RowPath) && !HiddenBy(hiddenColumns, row.ColumnPath))];

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

        _displays.Apply(cells, rowNodes, columns, rowFields);

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

    private bool HiddenBy(HashSet<IReadOnlyList<string>> hidden, IReadOnlyList<string> path)
    {
        for (var length = 1; length <= path.Count; length++)
            if (hidden.Contains(new PathPrefix(path, length))) return true;
        return false;
    }

    private List<PivotNode> Axis(
        IReadOnlyList<SourceRow> rows, Func<SourceRow, IReadOnlyList<string>> pathOf, IReadOnlyList<PivotField> fields,
        IReadOnlyList<PivotField> valueFields, IPivotSource source, bool grandTotal, HashSet<IReadOnlyList<string>> hidden)
    {
        var nodes = new List<PivotNode>();

        if (fields.Count == 0)
        {
            nodes.Add(new PivotNode([], PivotNodeKind.GrandTotal));
            return nodes;
        }

        void Walk(IReadOnlyList<string> prefix, int depth, IReadOnlyList<SourceRow> subset)
        {
            var field = fields[depth];

            var groups = new Dictionary<string, List<SourceRow>>(StringComparer.Ordinal);
            foreach (var row in subset)
            {
                var key = pathOf(row)[depth];
                if (!groups.TryGetValue(key, out var members)) groups[key] = members = [];
                members.Add(row);
            }

            var items = groups.Keys.ToList();
            items.Sort((left, right) => Compare(left, right, field));

            if (field.SortByValue is { } sortIndex && sortIndex >= 0 && sortIndex < valueFields.Count)
            {
                var measured = items.ToDictionary(item => item, item => Measured(source, groups[item], valueFields[sortIndex]), StringComparer.Ordinal);
                var labelOrder = items.Select((item, position) => (item, position)).ToDictionary(pair => pair.item, pair => pair.position, StringComparer.Ordinal);
                items.Sort((left, right) =>
                {
                    var byValue = measured[left].CompareTo(measured[right]);
                    if (field.SortDescending) byValue = -byValue;
                    return byValue != 0 ? byValue : labelOrder[left].CompareTo(labelOrder[right]);
                });
            }

            if (field.TopFilter is { Count: > 0 } top && top.ValueField >= 0 && top.ValueField < valueFields.Count && top.Count < items.Count)
            {
                var ranked = items
                    .OrderBy(item => Measured(source, groups[item], valueFields[top.ValueField]) * (top.FromBottom ? 1 : -1))
                    .Take(top.Count)
                    .ToHashSet(StringComparer.Ordinal);
                foreach (var item in items.Where(item => !ranked.Contains(item)))
                    hidden.Add(new List<string>(prefix) { item });
                items = [.. items.Where(ranked.Contains)];
            }

            foreach (var item in items)
            {
                var path = new List<string>(prefix) { item };
                nodes.Add(new PivotNode(path, PivotNodeKind.Item));

                if (depth + 1 >= fields.Count) continue;

                Walk(path, depth + 1, groups[item]);

                if (field.ShowSubtotals) nodes.Add(new PivotNode(path, PivotNodeKind.Subtotal));
            }
        }

        Walk([], 0, rows);

        if (grandTotal) nodes.Add(new PivotNode([], PivotNodeKind.GrandTotal));

        return nodes;
    }

    private double Measured(IPivotSource source, IReadOnlyList<SourceRow> rows, PivotField valueField)
    {
        if (valueField.IsCalculated) return 0;
        var value = aggregator.Aggregate(valueField.Aggregation, [.. rows.Select(row => source.ValueAt(row.Index, valueField.SourceColumnIndex))]);
        return value.IsNumeric ? value.AsNumber() : double.NegativeInfinity;
    }

    private int Compare(string left, string right, PivotField field)
    {
        var result = CompareAscending(left, right, field);
        return field.SortDescending ? -result : result;
    }

    private int CompareAscending(string left, string right, PivotField field)
    {
        var leftKey = SortKeyOf(left, field);
        var rightKey = SortKeyOf(right, field);

        if (leftKey.Rank != rightKey.Rank) return leftKey.Rank.CompareTo(rightKey.Rank);

        var result = leftKey.Rank == ItemSortKey.TextRank
            ? string.Compare(left, right, StringComparison.CurrentCulture)
            : leftKey.Number.CompareTo(rightKey.Number);

        return result != 0 ? result : string.CompareOrdinal(left, right);
    }

    private ItemSortKey SortKeyOf(string item, PivotField field)
    {
        if (grouping.OrderOf(item, field) is { } start) return new ItemSortKey(ItemSortKey.GroupRank, start);
        return double.TryParse(item, NumberStyles.Any, CultureInfo.InvariantCulture, out var number)
            ? new ItemSortKey(ItemSortKey.NumberRank, number)
            : new ItemSortKey(ItemSortKey.TextRank, 0);
    }

    private readonly record struct ItemSortKey(int Rank, double Number)
    {
        public const int GroupRank = 0;
        public const int NumberRank = 1;
        public const int TextRank = 2;
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
        new ShareOfParent(),
        new ShareOfParentColumn(),
        new RunningTotal(),
        new PercentRunningTotal(),
        new Rank(ValueDisplayMode.RankAscending, (value, number) => value < number),
        new Rank(ValueDisplayMode.RankDescending, (value, number) => value > number),
        new IndexDisplay(),
        new PreviousDifference(ValueDisplayMode.DifferenceFrom, percent: false),
        new PreviousDifference(ValueDisplayMode.PercentDifferenceFrom, percent: true),
    }.ToDictionary(display => display.Mode);

    public void Apply(CellValue[,] cells, IReadOnlyList<PivotNode> rows, IReadOnlyList<PivotColumn> columns, IReadOnlyList<PivotField> rowFields)
    {
        var raw = (CellValue[,])cells.Clone();

        for (var c = 0; c < columns.Count; c++)
        {
            if (!_displays.TryGetValue(columns[c].Field.DisplayMode, out var display)) continue;

            var pass = new DisplayPass(raw, rows, columns, c) { BaseLevel = BaseLevelOf(columns[c].Field, rowFields), BaseItem = columns[c].Field.BaseItem };
            for (var r = 0; r < rows.Count; r++)
                if (raw[r, c].IsNumeric && display.Show(pass, r, raw[r, c].AsNumber()) is { } shown)
                    cells[r, c] = shown;
        }
    }

    private int BaseLevelOf(PivotField field, IReadOnlyList<PivotField> rowFields)
    {
        if (field.BaseField is not { Length: > 0 } name) return 0;
        for (var level = 0; level < rowFields.Count; level++)
            if (string.Equals(rowFields[level].SourceName, name, StringComparison.Ordinal)) return level;
        return 0;
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

    public int BaseLevel { get; init; }

    public PivotBaseItem BaseItem { get; init; } = PivotBaseItem.Previous;

    public IReadOnlyList<string>? RunningScope { get; set; }

    public double ParentTotal(int row)
    {
        var node = Rows[row];
        if (node.Kind == PivotNodeKind.GrandTotal || node.Path.Count == 0) return Total;
        var parent = node.Path.Take(node.Path.Count - 1).ToList();
        if (parent.Count == 0) return Total;
        var sum = 0.0;
        for (var r = 0; r < Rows.Count; r++)
            if (IsItem(r) && Cells[r, Column].IsNumeric && StartsWith(Rows[r].Path, parent)) sum += Cells[r, Column].AsNumber();
        return sum;
    }

    public void Restart(int row)
    {
        var scope = ScopeOf(row);
        if (RunningScope is not null && PathComparer.Instance.Equals(RunningScope, scope)) return;
        RunningScope = scope;
        Running = 0;
    }

    public double ScopeTotal(int row)
    {
        var scope = ScopeOf(row);
        if (scope.Count == 0) return Total;
        var sum = 0.0;
        for (var r = 0; r < Rows.Count; r++)
            if (IsItem(r) && Cells[r, Column].IsNumeric && StartsWith(Rows[r].Path, scope)) sum += Cells[r, Column].AsNumber();
        return sum;
    }

    public IReadOnlyList<string> ScopeOf(int row) => [.. Rows[row].Path.Take(Math.Min(BaseLevel, Rows[row].Path.Count))];

    private static bool StartsWith(IReadOnlyList<string> path, IReadOnlyList<string> prefix)
    {
        if (path.Count < prefix.Count) return false;
        for (var index = 0; index < prefix.Count; index++)
            if (!string.Equals(path[index], prefix[index], StringComparison.Ordinal)) return false;
        return true;
    }

    private readonly int _leafDepth;

    public CellValue? PreviousOuter(int row)
    {
        if (Rows.Any(node => node.Path.Count > 0))
        {
            var node = Rows[row];
            if (node.Path.Count <= BaseLevel) return null;
            var target = BasePath(Rows, node, BaseLevel, BaseItem);
            if (target is null) return null;
            for (var r = 0; r < Rows.Count; r++)
                if (Rows[r].Kind == node.Kind && PathComparer.Instance.Equals(Rows[r].Path, target)) return Cells[r, Column];
            return null;
        }

        var current = Columns[Column];
        if (current.Node.Path.Count == 0) return null;
        var nodes = Columns.Select(column => column.Node).ToList();
        var previous = BasePath(nodes, current.Node, 0, BaseItem);
        if (previous is null) return null;
        for (var c = 0; c < Columns.Count; c++)
            if (Columns[c].FieldIndex == current.FieldIndex && Columns[c].Node.Kind == current.Node.Kind
                && PathComparer.Instance.Equals(Columns[c].Node.Path, previous))
                return Cells[row, c];
        return null;
    }

    private static List<string>? BasePath(IReadOnlyList<PivotNode> nodes, PivotNode node, int level, PivotBaseItem baseItem)
    {
        var prefix = node.Path.Take(level).ToList();
        var outer = nodes.Where(candidate => candidate.Path.Count > level && StartsWith(candidate.Path, prefix))
            .Select(candidate => candidate.Path[level])
            .Distinct(StringComparer.Ordinal).ToList();
        var position = outer.IndexOf(node.Path[level]);
        if (position < 0) return null;
        var target = baseItem.Kind switch
        {
            PivotBaseItemKind.Next => position + 1,
            PivotBaseItemKind.Named => outer.IndexOf(baseItem.Key ?? string.Empty),
            _ => position - 1,
        };
        if (target < 0 || target >= outer.Count || target == position) return null;
        return [.. prefix, outer[target], .. node.Path.Skip(level + 1)];
    }

    public double ParentColumnTotal(int row)
    {
        var current = Columns[Column];
        var path = current.Node.Path;
        if (current.Node.Kind == PivotNodeKind.GrandTotal || path.Count == 0)
            return Cells[row, Column].IsNumeric ? Cells[row, Column].AsNumber() : 0;
        var parent = path.Take(path.Count - 1).ToList();
        var sum = 0.0;
        for (var c = 0; c < Columns.Count; c++)
            if (Columns[c].FieldIndex == current.FieldIndex && Columns[c].Node.Kind == PivotNodeKind.Item
                && Columns[c].Node.Path.Count == path.Count && StartsWith(Columns[c].Node.Path, parent) && Cells[row, c].IsNumeric)
                sum += Cells[row, c].AsNumber();
        return sum;
    }

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

internal sealed class ShareOfParent() : ValueDisplay(ValueDisplayMode.PercentOfParent)
{
    public override CellValue? Show(DisplayPass pass, int row, double number) => Ratio(number, pass.ParentTotal(row));
}

internal sealed class ShareOfParentColumn() : ValueDisplay(ValueDisplayMode.PercentOfParentColumn)
{
    public override CellValue? Show(DisplayPass pass, int row, double number) => Ratio(number, pass.ParentColumnTotal(row));
}

internal sealed class RunningTotal() : ValueDisplay(ValueDisplayMode.RunningTotal)
{
    public override CellValue? Show(DisplayPass pass, int row, double number)
    {
        if (!pass.IsItem(row)) return null;
        pass.Restart(row);
        pass.Running += number;
        return CellValue.Number(pass.Running);
    }
}

internal sealed class PercentRunningTotal() : ValueDisplay(ValueDisplayMode.PercentRunningTotal)
{
    public override CellValue? Show(DisplayPass pass, int row, double number)
    {
        if (!pass.IsItem(row)) return null;
        pass.Restart(row);
        pass.Running += number;
        return Ratio(pass.Running, pass.ScopeTotal(row));
    }
}

internal sealed class IndexDisplay() : ValueDisplay(ValueDisplayMode.Index)
{
    public override CellValue? Show(DisplayPass pass, int row, double number) =>
        Ratio(number * pass.GrandTotal, pass.RowTotal(row) * pass.Total);
}

internal sealed class PreviousDifference(ValueDisplayMode mode, bool percent) : ValueDisplay(mode)
{
    public override CellValue? Show(DisplayPass pass, int row, double number)
    {
        if (pass.PreviousOuter(row) is not { IsNumeric: true } previous) return CellValue.Empty;
        var difference = number - previous.AsNumber();
        return percent ? Ratio(difference, previous.AsNumber()) : CellValue.Number(difference);
    }
}

internal sealed class Rank(ValueDisplayMode mode, Func<double, double, bool> before) : ValueDisplay(mode)
{
    public override CellValue? Show(DisplayPass pass, int row, double number) =>
        pass.IsItem(row)
            ? CellValue.Number(pass.Values.Count(value => before(value, number)) + 1)
            : CellValue.Empty;
}
