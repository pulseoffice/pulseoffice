using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets;

public sealed record DatabaseRange
{
    public required string Name { get; init; }

    public required int SheetIndex { get; init; }

    public required CellRange Range { get; init; }

    public bool HasHeader { get; init; } = true;

    public bool HasTotalsRow { get; init; }

    public CellRange RangeWithoutTotals =>
        HasTotalsRow && Range.RowCount > 1
            ? new CellRange(Range.Start, new CellAddress(Range.End.Row - 1, Range.End.Column))
            : Range;

    public string? StyleName { get; init; }

    public string? QueryBindingId { get; init; }

    public bool ShowsFilterButtons { get; init; }

    public IReadOnlyList<ColumnFilter> Filters { get; init; } = [];

    public RangeFilter? RangeFilter { get; init; }

    public SortSettings? Sort { get; init; }
}

public sealed class DatabaseRangeCollection
{
    private readonly Dictionary<string, DatabaseRange> _ranges =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<DatabaseRange> All => _ranges.Values;

    public int Count => _ranges.Count;

    public void Define(DatabaseRange range)
    {
        ArgumentNullException.ThrowIfNull(range);

        _ranges[range.Name] = range;
    }

    public bool Remove(string name) => _ranges.Remove(name);

    public void ReplaceAll(IEnumerable<DatabaseRange> ranges)
    {
        _ranges.Clear();
        foreach (var range in ranges) _ranges[range.Name] = range;
    }

    public IReadOnlyList<DatabaseRange> Reindex(Func<int, int> map)
    {
        var detached = new List<DatabaseRange>();
        foreach (var range in _ranges.Values.ToList())
        {
            var index = map(range.SheetIndex);
            if (index < 0)
            {
                _ranges.Remove(range.Name);
                detached.Add(range);
            }
            else _ranges[range.Name] = range with { SheetIndex = index, RangeFilter = range.RangeFilter?.Reindexed(map) };
        }
        return detached;
    }

    public DatabaseRange? Find(string name) =>
        _ranges.TryGetValue(name, out var range) ? range : null;

    public DatabaseRange? At(int sheetIndex, CellAddress address) =>
        _ranges.Values.FirstOrDefault(range =>
            range.SheetIndex == sheetIndex && range.Range.Contains(address));
}
