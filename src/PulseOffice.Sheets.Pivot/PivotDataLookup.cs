using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Pivot;

public sealed class PivotDataLookup : IPivotDataSource
{
    private readonly Workbook _workbook;
    private readonly IPivotEngine _engine;
    private readonly PivotLabels _labels;
    private readonly IPivotSources _sources;
    private readonly Dictionary<PivotDefinition, (object Owner, long Revision, PivotTable Table)> _built = new(ReferenceEqualityComparer.Instance);

    public PivotDataLookup(Workbook workbook, IPivotEngine engine, PivotLabels labels, IPivotSources? sources = null)
    {
        _workbook = workbook;
        _engine = engine;
        _labels = labels;
        _sources = sources ?? new PivotSources();
    }

    public CellValue? Lookup(
        int sheetIndex, CellAddress target, string dataField,
        IReadOnlyList<(string Field, string Item)> filters)
    {
        if (sheetIndex < 0 || sheetIndex >= _workbook.Sheets.Count) return null;

        var sheetName = _workbook.Sheets[sheetIndex].Name;

        var definition = Containing(sheetName, target);
        if (definition is null) return null;

        if (_sources.Open(_workbook, definition) is not { } source) return null;

        var table = Built(definition, source);
        if (table.IsEmpty) return null;

        if (HiddenSubtotal(definition, filters)) return UniqueLeaf(table, definition, dataField, filters);
        return InLayout(table, definition, dataField, filters) ?? Aggregated(source, definition, dataField, filters);
    }

    private PivotTable Built(PivotDefinition definition, IPivotSource source)
    {
        var owner = (object?)definition.ExternalSource?.Cache ?? _workbook.FindSheet(definition.SourceSheet)!;
        var revision = _sources.RevisionOf(_workbook, definition);
        if (_built.TryGetValue(definition, out var cached) && ReferenceEquals(cached.Owner, owner) && cached.Revision == revision)
            return cached.Table;
        foreach (var stale in _built.Keys.Where(key => !_workbook.Pivots.Contains(key)).ToList()) _built.Remove(stale);
        var table = _engine.Build(definition, source);
        _built[definition] = (owner, revision, table);
        return table;
    }

    private CellValue? UniqueLeaf(PivotTable table, PivotDefinition definition, string dataField, IReadOnlyList<(string Field, string Item)> filters)
    {
        if (Coordinates(definition, filters) is not var (rowPath, columnPath)) return null;
        var rows = Leaves(table.Rows, rowPath, definition.In(PivotArea.Row).Count());
        if (rows.Count != 1 || Hidden(definition.In(PivotArea.Column).ToList(), filters)) return null;
        var column = Column(table, definition, dataField, columnPath);
        return column < 0 ? null : table.Value(rows[0], column);
    }

    private List<int> Leaves(IReadOnlyList<PivotNode> nodes, IReadOnlyList<string> prefix, int depth) =>
        [.. Enumerable.Range(0, nodes.Count).Where(index =>
            nodes[index] is { Kind: PivotNodeKind.Item } node && node.Path.Count == depth
            && node.Path.Take(prefix.Count).Zip(prefix).All(pair => string.Equals(pair.First, pair.Second, StringComparison.OrdinalIgnoreCase)))];

    private bool HiddenSubtotal(PivotDefinition definition, IReadOnlyList<(string Field, string Item)> filters) =>
        Hidden(definition.In(PivotArea.Row).ToList(), filters) || Hidden(definition.In(PivotArea.Column).ToList(), filters);

    private bool Hidden(IReadOnlyList<PivotField> fields, IReadOnlyList<(string Field, string Item)> filters)
    {
        var named = fields.Select(field => filters.Any(filter => Names(field, filter.Field))).ToList();
        var deepest = named.LastIndexOf(true);
        if (deepest < 0 || deepest == fields.Count - 1) return false;
        return named.Take(deepest + 1).All(present => present) && !fields[deepest].ShowSubtotals;
    }

    private CellValue? InLayout(PivotTable table, PivotDefinition definition, string dataField, IReadOnlyList<(string Field, string Item)> filters)
    {
        if (Coordinates(definition, filters) is not var (rowPath, columnPath)) return null;

        var column = Column(table, definition, dataField, columnPath);
        if (column < 0) return null;

        var row = Row(table, rowPath);
        return row < 0 ? null : table.Value(row, column);
    }

    private CellValue? Aggregated(IPivotSource source, PivotDefinition definition, string dataField, IReadOnlyList<(string Field, string Item)> filters)
    {
        var layout = definition.Fields.Where(field => field.Area is PivotArea.Row or PivotArea.Column or PivotArea.Filter).ToList();
        var named = new List<PivotField>();
        foreach (var (name, _) in filters)
        {
            if (layout.FirstOrDefault(field => Names(field, name)) is not { } field || named.Contains(field)) return null;
            named.Add(field);
        }
        var values = definition.In(PivotArea.Value).ToList();
        var wanted = values.FindIndex(field => Names(field, dataField) || Measure(field, dataField));
        if (wanted < 0 && values.Count == 1) wanted = 0;
        if (wanted < 0) return null;

        var reduced = definition with
        {
            Fields = [.. definition.Fields.Select(field =>
                named.IndexOf(field) is var order and >= 0 ? field with { Area = PivotArea.Row, AreaOrder = order, ShowSubtotals = false }
                : field == values[wanted] ? field with { AreaOrder = 0 }
                : field.Area is PivotArea.Row or PivotArea.Column ? field with { Area = PivotArea.Filter }
                : field.Area is PivotArea.Value ? field with { Area = PivotArea.Unused }
                : field)],
            ShowGrandTotalRows = true,
            ShowGrandTotalColumns = true,
        };
        var table = _engine.Build(reduced, source);
        if (table.IsEmpty || table.Columns.Count == 0) return null;
        var row = Row(table, [.. filters.Select(filter => filter.Item)]);
        return row < 0 ? null : table.Value(row, 0);
    }

    private PivotDefinition? Containing(string sheetName, CellAddress target)
    {
        var candidates = _workbook.Pivots
            .Where(pivot => string.Equals(pivot.DestinationSheet, sheetName, StringComparison.OrdinalIgnoreCase)
                            && pivot.Destination.Row <= target.Row
                            && pivot.Destination.Column <= target.Column)
            .ToList();

        return candidates.FirstOrDefault(pivot => pivot.OutputRange is { } output && output.Contains(target))
            ?? candidates
                .OrderByDescending(pivot => pivot.Destination.Row)
                .ThenByDescending(pivot => pivot.Destination.Column)
                .FirstOrDefault();
    }

    private (IReadOnlyList<string> Rows, IReadOnlyList<string> Columns)? Coordinates(
        PivotDefinition definition, IReadOnlyList<(string Field, string Item)> filters)
    {
        var rowFields = definition.In(PivotArea.Row).ToList();
        var columnFields = definition.In(PivotArea.Column).ToList();
        var rowItems = new string?[rowFields.Count];
        var columnItems = new string?[columnFields.Count];

        foreach (var (field, item) in filters)
        {
            var rowIndex = rowFields.FindIndex(candidate => Names(candidate, field));
            var columnIndex = columnFields.FindIndex(candidate => Names(candidate, field));

            if (rowIndex >= 0) rowItems[rowIndex] = item;
            else if (columnIndex >= 0) columnItems[columnIndex] = item;
            else return null;
        }

        return Prefix(rowItems) is { } rows && Prefix(columnItems) is { } columns ? (rows, columns) : null;
    }

    private bool Names(PivotField field, string name) =>
        string.Equals(field.Label, name, StringComparison.OrdinalIgnoreCase)
        || string.Equals(field.SourceName, name, StringComparison.OrdinalIgnoreCase);

    private bool Measure(PivotField field, string name)
    {
        var separator = name.IndexOf(" - ", StringComparison.Ordinal);
        return separator > 0 && Names(field, name[(separator + 3)..].Trim());
    }

    private IReadOnlyList<string>? Prefix(string?[] items)
    {
        var length = Array.FindIndex(items, item => item is null);
        if (length < 0) length = items.Length;

        return items.Skip(length).Any(item => item is not null) ? null : [.. items.Take(length).Select(item => item!)];
    }

    private int Column(PivotTable table, PivotDefinition definition, string dataField, IReadOnlyList<string> path)
    {
        var values = definition.In(PivotArea.Value).ToList();

        var wanted = values.FindIndex(field => Names(field, dataField) || Measure(field, dataField));

        if (wanted < 0 && values.Count == 1) wanted = 0;
        if (wanted < 0) return -1;

        for (var index = 0; index < table.Columns.Count; index++)
        {
            var candidate = table.Columns[index];

            if (candidate.FieldIndex == wanted && Same(candidate.Node, path)) return index;
        }

        return -1;
    }

    private int Row(PivotTable table, IReadOnlyList<string> path)
    {
        for (var index = 0; index < table.Rows.Count; index++)
            if (Same(table.Rows[index], path)) return index;

        return -1;
    }

    private bool Same(PivotNode node, IReadOnlyList<string> path)
    {
        if (path.Count == 0) return node.Kind is PivotNodeKind.GrandTotal;
        if (node.Kind is PivotNodeKind.GrandTotal || node.Path.Count != path.Count) return false;

        return node.Path.Zip(path).All(pair => string.Equals(pair.First, pair.Second, StringComparison.OrdinalIgnoreCase));
    }
}
