using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets;

public enum FilterGroupJoin { And, Or }

public abstract record FilterNode
{
    public abstract IEnumerable<FilterTerm> Terms();

    public abstract int Depth { get; }

    public abstract FilterNode? Remapped(Func<int, int?> field);
}

public sealed record FilterTerm(int Field, string Criteria) : FilterNode
{
    public override IEnumerable<FilterTerm> Terms() => [this];

    public override int Depth => 0;

    public override FilterNode? Remapped(Func<int, int?> field) =>
        field(Field) is { } moved ? this with { Field = moved } : null;
}

public sealed record FilterGroup(FilterGroupJoin Join, IReadOnlyList<FilterNode> Children) : FilterNode
{
    public override IEnumerable<FilterTerm> Terms() => Children.SelectMany(child => child.Terms());

    public override int Depth => 1 + Children.Select(child => child.Depth).DefaultIfEmpty(0).Max();

    public override FilterNode? Remapped(Func<int, int?> field)
    {
        var kept = Children.Select(child => child.Remapped(field)).OfType<FilterNode>().ToList();
        return kept.Count switch
        {
            0 => null,
            1 => kept[0],
            _ => this with { Children = kept },
        };
    }

    public bool Equals(FilterGroup? other) =>
        other is not null && Join == other.Join && Children.SequenceEqual(other.Children);

    public override int GetHashCode() => Children.Aggregate(Join.GetHashCode(), HashCode.Combine);
}

public sealed record AdvancedCriteria(int SheetIndex, CellRange Range);

public sealed record RangeFilter
{
    public FilterNode? Condition { get; init; }

    public AdvancedCriteria? Criteria { get; init; }

    public bool UniqueOnly { get; init; }

    public bool IsEmpty => Condition is null && Criteria is null && !UniqueOnly;

    public RangeFilter? Moved(Func<int, int?> field, Func<AdvancedCriteria, AdvancedCriteria?> criteria)
    {
        var moved = this with
        {
            Condition = Condition?.Remapped(field),
            Criteria = Criteria is { } current ? criteria(current) : null,
        };
        return moved.IsEmpty ? null : moved;
    }

    public RangeFilter? Reindexed(Func<int, int> map)
    {
        if (Criteria is not { } criteria) return this;
        var index = map(criteria.SheetIndex);
        var moved = this with { Criteria = index < 0 ? null : criteria with { SheetIndex = index } };
        return moved.IsEmpty ? null : moved;
    }
}

public sealed record SheetRangeFilter(CellRange Range, bool HasHeader, RangeFilter Filter);
