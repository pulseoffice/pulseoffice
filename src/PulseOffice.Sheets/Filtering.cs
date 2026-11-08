using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets;

public enum ColorFilterTarget { Background, Text }

public sealed record ColorFilterCriterion(ColorFilterTarget Target, string? ColorHex)
{
    public bool IsAutomatic => ColorHex is null;
}

public enum CriteriaJoin { And, Or }

public sealed record TopItemsCriterion(bool Bottom, bool Percent, double Count);

public sealed record ColumnFilter(int ColumnOffset)
{
    public ColorFilterCriterion? Color { get; init; }

    public TopItemsCriterion? Top { get; init; }

    public IReadOnlySet<string>? AllowedValues { get; init; }

    public string? Criteria { get; init; }

    public string? SecondCriteria { get; init; }

    public CriteriaJoin Join { get; init; }

    public IReadOnlyList<string> Conditions =>
        [.. new[] { Criteria, SecondCriteria }.Where(criteria => !string.IsNullOrEmpty(criteria)).Select(criteria => criteria!)];

    public bool IncludeBlanks { get; init; } = true;

    public bool IsEmpty => AllowedValues is null && IncludeBlanks && string.IsNullOrEmpty(Criteria) && string.IsNullOrEmpty(SecondCriteria) && Color is null && Top is null;
}

public sealed class AutoFilterState
{
    private readonly List<ColumnFilter> _filters = [];

    public CellRange? Range { get; private set; }

    public bool HasHeader { get; private set; } = true;

    public IReadOnlyList<ColumnFilter> Filters => _filters;

    public bool IsActive => Range is not null;

    public SortSettings? Sort { get; private set; }

    public void Remember(SortSettings? sort) => Sort = Range is { } range && sort is not null && sort.FitsWithin(range.ColumnCount) ? sort : null;

    public void Activate(CellRange range, bool hasHeader = true)
    {
        if (Range != range) Sort = null;
        Range = range;
        HasHeader = hasHeader;
    }

    public void Deactivate()
    {
        Range = null;
        Sort = null;
        _filters.Clear();
    }

    public ColumnFilter? For(int columnOffset) =>
        _filters.FirstOrDefault(filter => filter.ColumnOffset == columnOffset);

    public void Set(ColumnFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        _filters.RemoveAll(existing => existing.ColumnOffset == filter.ColumnOffset);

        if (!filter.IsEmpty) _filters.Add(filter);
    }

    public void Clear() => _filters.Clear();

    public bool IsFiltered(int columnOffset) => For(columnOffset) is not null;

    public AutoFilterMemento Save() => new(Range, HasHeader, [.. _filters]) { Sort = Sort };

    public void Restore(AutoFilterMemento memento)
    {
        ArgumentNullException.ThrowIfNull(memento);

        Range = memento.Range;
        HasHeader = memento.HasHeader;
        _filters.Clear();
        _filters.AddRange(memento.Filters);
        Remember(memento.Sort);
    }
}

public sealed record AutoFilterMemento(CellRange? Range, bool HasHeader, IReadOnlyList<ColumnFilter> Filters)
{
    public SortSettings? Sort { get; init; }
}
