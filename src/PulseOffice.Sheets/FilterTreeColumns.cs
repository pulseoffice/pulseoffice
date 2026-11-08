namespace PulseOffice.Sheets;

public sealed class FilterTreeColumns
{
    public IReadOnlyList<ColumnFilter>? ToColumns(FilterNode? node) => node switch
    {
        null => [],
        FilterTerm term => [new ColumnFilter(term.Field) { Criteria = term.Criteria }],
        FilterGroup { Join: FilterGroupJoin.Or } group => OrPair(group) is { } pair ? [pair] : null,
        FilterGroup group => AndColumns(group),
        _ => null,
    };

    public FilterNode? ToTree(IEnumerable<ColumnFilter> columns)
    {
        ArgumentNullException.ThrowIfNull(columns);

        var parts = new List<FilterNode>();
        foreach (var column in columns)
        {
            var terms = column.Conditions.Select(criteria => (FilterNode)new FilterTerm(column.ColumnOffset, criteria)).ToList();
            if (terms.Count == 1) parts.Add(terms[0]);
            else if (terms.Count > 1 && column.Join == CriteriaJoin.Or) parts.Add(new FilterGroup(FilterGroupJoin.Or, terms));
            else parts.AddRange(terms);
        }
        return parts.Count switch
        {
            0 => null,
            1 => parts[0],
            _ => new FilterGroup(FilterGroupJoin.And, parts),
        };
    }

    public FilterNode? And(FilterNode? left, FilterNode? right) => (left, right) switch
    {
        (null, _) => right,
        (_, null) => left,
        _ => new FilterGroup(FilterGroupJoin.And, [.. Flatten(left, FilterGroupJoin.And), .. Flatten(right, FilterGroupJoin.And)]),
    };

    private IEnumerable<FilterNode> Flatten(FilterNode node, FilterGroupJoin join) =>
        node is FilterGroup group && group.Join == join ? group.Children : [node];

    private ColumnFilter? OrPair(FilterGroup group) =>
        group.Children is [FilterTerm first, FilterTerm second] && first.Field == second.Field
            ? new ColumnFilter(first.Field) { Criteria = first.Criteria, SecondCriteria = second.Criteria, Join = CriteriaJoin.Or }
            : null;

    private IReadOnlyList<ColumnFilter>? AndColumns(FilterGroup group)
    {
        var columns = new Dictionary<int, ColumnFilter>();
        foreach (var child in group.Children)
        {
            var column = child switch
            {
                FilterTerm term => new ColumnFilter(term.Field) { Criteria = term.Criteria },
                FilterGroup { Join: FilterGroupJoin.Or } pair => OrPair(pair),
                _ => null,
            };
            if (column is null) return null;
            if (!columns.TryGetValue(column.ColumnOffset, out var existing)) columns[column.ColumnOffset] = column;
            else if (existing.SecondCriteria is null && column.SecondCriteria is null)
                columns[column.ColumnOffset] = existing with { SecondCriteria = column.Criteria, Join = CriteriaJoin.And };
            else return null;
        }
        return [.. columns.Values.OrderBy(column => column.ColumnOffset)];
    }
}
