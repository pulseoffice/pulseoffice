using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets.Engine.Operations;

public enum FilterCondition
{
    Equal, NotEqual, Greater, GreaterOrEqual, Less, LessOrEqual,
    Contains, NotContains, BeginsWith, EndsWith,
    IsEmpty, IsNotEmpty,
}

public enum FilterJoin { And, Or }

public sealed record StandardFilterRow(FilterJoin Join, int Field, FilterCondition Condition, string Value);

public interface IStandardFilter
{
    string Criteria(FilterCondition condition, string value);

    IReadOnlySet<int> RowsToHide(Worksheet sheet, CellRange range, IReadOnlyList<StandardFilterRow> rows);

    FilterNode? Compose(IReadOnlyList<StandardFilterRow> rows);

    IReadOnlyList<StandardFilterRow>? Decompose(FilterNode condition, int maximumRows);

    bool Applies(StandardFilterRow row);
}

public sealed class StandardFilter(ISheetFilter filters, IFilterValueText values) : IStandardFilter
{
    public StandardFilter(ISheetFilter filters) : this(filters, new FilterValueText(PulseOffice.Sheets.Formatting.FixedSpreadsheetLocale.Invariant()))
    {
    }

    public string Criteria(FilterCondition condition, string value) => Compose(condition,
        condition is FilterCondition.Contains or FilterCondition.NotContains or FilterCondition.BeginsWith or FilterCondition.EndsWith
            ? value
            : values.Canonical(value));

    private string Compose(FilterCondition condition, string value) => condition switch
    {
        FilterCondition.IsEmpty => "=",
        FilterCondition.IsNotEmpty => "<>",
        FilterCondition.NotEqual => "<>" + value,
        FilterCondition.Greater => ">" + value,
        FilterCondition.GreaterOrEqual => ">=" + value,
        FilterCondition.Less => "<" + value,
        FilterCondition.LessOrEqual => "<=" + value,
        FilterCondition.Contains => "*" + value + "*",
        FilterCondition.NotContains => "<>*" + value + "*",
        FilterCondition.BeginsWith => value + "*",
        FilterCondition.EndsWith => "*" + value,
        _ => "=" + value,
    };

    public IReadOnlySet<int> RowsToHide(Worksheet sheet, CellRange range, IReadOnlyList<StandardFilterRow> rows)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        return Compose(rows) is { } condition ? filters.RowsToHide(sheet, range, condition) : new HashSet<int>();
    }

    public FilterNode? Compose(IReadOnlyList<StandardFilterRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        FilterNode? composed = null;

        foreach (var row in rows)
        {
            FilterNode term = new FilterTerm(row.Field, Criteria(row.Condition, row.Value));
            var join = row.Join == FilterJoin.Or ? FilterGroupJoin.Or : FilterGroupJoin.And;
            composed = composed switch
            {
                null => term,
                FilterGroup group when group.Join == join => group with { Children = [.. group.Children, term] },
                _ => new FilterGroup(join, [composed, term]),
            };
        }

        return composed;
    }

    public IReadOnlyList<StandardFilterRow>? Decompose(FilterNode condition, int maximumRows)
    {
        ArgumentNullException.ThrowIfNull(condition);

        var rows = new List<StandardFilterRow>();
        if (!Unfold(condition, FilterJoin.And, rows) || rows.Count > maximumRows) return null;

        return rows;
    }

    private bool Unfold(FilterNode node, FilterJoin join, List<StandardFilterRow> rows)
    {
        switch (node)
        {
            case FilterTerm term:
                if (Parse(term.Criteria) is not var (condition, value)) return false;
                rows.Add(new StandardFilterRow(rows.Count == 0 ? FilterJoin.And : join, term.Field, condition, value));
                return true;
            case FilterGroup { Children.Count: > 0 } group:
                var groupJoin = group.Join == FilterGroupJoin.Or ? FilterJoin.Or : FilterJoin.And;
                if (!Unfold(group.Children[0], join, rows)) return false;
                return group.Children.Skip(1).All(child => child is FilterTerm && Unfold(child, groupJoin, rows));
            default:
                return false;
        }
    }

    private (FilterCondition Condition, string Value)? Parse(string criteria)
    {
        if (criteria.Length == 0) return null;
        if (criteria.Length > 4 && criteria.StartsWith("<>*", StringComparison.Ordinal) && criteria.EndsWith('*'))
            return (FilterCondition.NotContains, criteria[3..^1]);

        foreach (var (prefix, condition) in _operators)
            if (criteria.StartsWith(prefix, StringComparison.Ordinal))
                return criteria.Length > prefix.Length ? (condition, criteria[prefix.Length..])
                    : condition switch
                    {
                        FilterCondition.Equal => (FilterCondition.IsEmpty, string.Empty),
                        FilterCondition.NotEqual => (FilterCondition.IsNotEmpty, string.Empty),
                        _ => null,
                    };

        if (criteria.Length > 2 && criteria[0] == '*' && criteria[^1] == '*') return (FilterCondition.Contains, criteria[1..^1]);
        if (criteria.Length > 1 && criteria[0] == '*') return (FilterCondition.EndsWith, criteria[1..]);
        if (criteria.Length > 1 && criteria[^1] == '*') return (FilterCondition.BeginsWith, criteria[..^1]);

        return (FilterCondition.Equal, criteria);
    }

    public bool Applies(StandardFilterRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return row.Condition is FilterCondition.IsEmpty or FilterCondition.IsNotEmpty || row.Value.Trim().Length > 0;
    }

    private readonly IReadOnlyList<(string Prefix, FilterCondition Condition)> _operators =
    [
        ("<>", FilterCondition.NotEqual), (">=", FilterCondition.GreaterOrEqual), ("<=", FilterCondition.LessOrEqual),
        (">", FilterCondition.Greater), ("<", FilterCondition.Less), ("=", FilterCondition.Equal),
    ];
}
