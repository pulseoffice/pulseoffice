using PulseOffice.Sheets.Pivot;

namespace PulseOffice.Sheets.UI;

public sealed record PivotTableOptions(PivotLayout Layout, bool GrandTotalRows, bool GrandTotalColumns, bool RepeatLabels)
{
    public static PivotTableOptions Default { get; } = new(PivotLayout.Compact, true, true, false);

    public static PivotTableOptions Of(PivotDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new(definition.Layout, definition.ShowGrandTotalRows, definition.ShowGrandTotalColumns, definition.RepeatItemLabels);
    }

    public PivotDefinition ApplyTo(PivotDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return definition with
        {
            Layout = Layout,
            ShowGrandTotalRows = GrandTotalRows,
            ShowGrandTotalColumns = GrandTotalColumns,
            RepeatItemLabels = RepeatLabels,
        };
    }
}

public sealed record PivotChoice(IReadOnlyList<PivotField> Fields, string DestinationSheet, PivotTableOptions? Options = null);

public sealed class PivotLayoutModel
{
    private readonly IReadOnlyList<string> _columns;
    private readonly Dictionary<PivotArea, List<int>> _zones = new()
    {
        [PivotArea.Filter] = [],
        [PivotArea.Column] = [],
        [PivotArea.Row] = [],
        [PivotArea.Value] = [],
    };
    private readonly Dictionary<int, PivotAggregation> _aggregations = [];
    private readonly Dictionary<int, PivotField> _settings = [];
    private readonly List<PivotField> _calculated = [];
    private readonly List<PivotField> _unplacedCalculated = [];
    private PivotTableOptions _options = PivotTableOptions.Default;

    public PivotLayoutModel(IReadOnlyList<string> columns) => _columns = columns ?? throw new ArgumentNullException(nameof(columns));

    public PivotLayoutModel(IReadOnlyList<string> columns, PivotDefinition existing) : this(columns)
    {
        ArgumentNullException.ThrowIfNull(existing);
        _options = PivotTableOptions.Of(existing);
        foreach (var area in Areas)
            foreach (var field in existing.In(area))
            {
                if (field.IsCalculated || field.SourceColumnIndex < 0 || field.SourceColumnIndex >= _columns.Count)
                {
                    if (field.IsCalculated && area == PivotArea.Value)
                    {
                        _zones[area].Add(_columns.Count + _calculated.Count);
                        _calculated.Add(field);
                    }
                    else _unplacedCalculated.Add(field);
                    continue;
                }

                if (AreaOf(field.SourceColumnIndex) != PivotArea.Unused) continue;
                _zones[area].Add(field.SourceColumnIndex);
                _aggregations[field.SourceColumnIndex] = field.Aggregation;
                _settings[field.SourceColumnIndex] = field;
            }
    }

    public event EventHandler? Changed;

    public IReadOnlyList<string> Columns => _columns;

    public IReadOnlyList<PivotArea> Areas { get; } = [PivotArea.Filter, PivotArea.Column, PivotArea.Row, PivotArea.Value];

    public PivotTableOptions Options
    {
        get => _options;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value == _options) return;
            _options = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool IsCalculated(int column) => column >= _columns.Count && column - _columns.Count < _calculated.Count;

    public string NameOf(int column)
    {
        if (IsCalculated(column)) return _calculated[column - _columns.Count].SourceName;
        return column >= 0 && column < _columns.Count ? _columns[column] : string.Empty;
    }

    public bool NameTaken(string name, int except) =>
        Enumerable.Range(0, _columns.Count + _calculated.Count)
            .Any(column => column != except && string.Equals(NameOf(column), name, StringComparison.CurrentCultureIgnoreCase));

    public IReadOnlyList<int> In(PivotArea area) => _zones.TryGetValue(area, out var zone) ? zone : [];

    public PivotArea AreaOf(int column)
    {
        foreach (var (area, zone) in _zones)
            if (zone.Contains(column)) return area;
        return PivotArea.Unused;
    }

    public PivotAggregation AggregationOf(int column) => _aggregations.GetValueOrDefault(column, PivotAggregation.Sum);

    public PivotField SettingsOf(int column) =>
        IsCalculated(column) ? _calculated[column - _columns.Count]
        : _settings.TryGetValue(column, out var field)
            ? field
            : new PivotField { SourceName = _columns[column], SourceColumnIndex = column };

    public void Configure(int column, Func<PivotField, PivotField> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (column < 0 || (column >= _columns.Count && !IsCalculated(column))) return;
        var before = SettingsOf(column);
        var after = change(before);
        if (after == before) return;
        if (IsCalculated(column)) _calculated[column - _columns.Count] = after;
        else _settings[column] = after;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Place(int column, PivotArea area, int index = int.MaxValue)
    {
        if (column < 0 || (column >= _columns.Count && !IsCalculated(column))) return;
        if (IsCalculated(column) && area is not (PivotArea.Value or PivotArea.Unused)) return;
        var valuesBefore = _zones[PivotArea.Value].ToList();
        foreach (var zone in _zones.Values) zone.Remove(column);
        if (area != PivotArea.Unused)
        {
            var target = _zones[area];
            target.Insert(Math.Clamp(index, 0, target.Count), column);
        }

        Retarget(valuesBefore);

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Retarget(List<int> valuesBefore)
    {
        var valuesAfter = _zones[PivotArea.Value];
        if (valuesBefore.SequenceEqual(valuesAfter)) return;
        int? Moved(int position) =>
            position >= 0 && position < valuesBefore.Count && valuesAfter.IndexOf(valuesBefore[position]) is var now and >= 0 ? now : null;
        foreach (var (key, field) in _settings.ToList())
        {
            var sort = field.SortByValue is { } byValue ? Moved(byValue) : null;
            var top = field.TopFilter is { } filter && Moved(filter.ValueField) is { } measure ? filter with { ValueField = measure } : null;
            _settings[key] = field with { SortByValue = sort, TopFilter = top };
        }
    }

    public void Toggle(int column, bool used, Func<int, bool> numeric)
    {
        if (!used) Place(column, PivotArea.Unused);
        else if (AreaOf(column) == PivotArea.Unused) Place(column, numeric(column) ? PivotArea.Value : PivotArea.Row);
    }

    public void Aggregate(int column, PivotAggregation aggregation)
    {
        _aggregations[column] = aggregation;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlyList<PivotField> Fields()
    {
        var placed = Areas.SelectMany(area => In(area).Select((column, order) => IsCalculated(column)
            ? SettingsOf(column) with { Area = area, AreaOrder = order }
            : SettingsOf(column) with
            {
                SourceName = _columns[column],
                SourceColumnIndex = column,
                Area = area,
                AreaOrder = order,
                Aggregation = AggregationOf(column),
            })).ToList();
        return [.. placed, .. _unplacedCalculated];
    }
}
