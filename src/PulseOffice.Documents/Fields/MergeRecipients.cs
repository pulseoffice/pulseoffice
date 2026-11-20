using System.Globalization;

namespace PulseOffice.Documents.Fields;

public enum MergeFilterOperator
{
    Contains,
    Equals,
    NotEquals,
    Empty,
    NotEmpty,
}

public sealed record MergeFilter(string Column, MergeFilterOperator Operator, string Value);

public interface IMergeRecipients
{
    bool Matches(MergeData data, int row, MergeFilter? filter);

    IReadOnlyList<int> Rows(MergeData data, MergeFilter? filter, IReadOnlySet<int> excluded);
}

public sealed class MergeRecipients : IMergeRecipients
{
    public bool Matches(MergeData data, int row, MergeFilter? filter)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (filter is null || filter.Column.Length == 0) return true;
        var value = (data.Value(row, filter.Column) ?? string.Empty).Trim();
        var wanted = filter.Value.Trim();
        var compare = CultureInfo.CurrentCulture.CompareInfo;
        return filter.Operator switch
        {
            MergeFilterOperator.Contains => compare.IndexOf(value, wanted, CompareOptions.IgnoreCase) >= 0,
            MergeFilterOperator.Equals => compare.Compare(value, wanted, CompareOptions.IgnoreCase) == 0,
            MergeFilterOperator.NotEquals => compare.Compare(value, wanted, CompareOptions.IgnoreCase) != 0,
            MergeFilterOperator.Empty => value.Length == 0,
            _ => value.Length > 0,
        };
    }

    public IReadOnlyList<int> Rows(MergeData data, MergeFilter? filter, IReadOnlySet<int> excluded)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(excluded);
        return [.. Enumerable.Range(0, data.Rows.Count).Where(row => !excluded.Contains(row) && Matches(data, row, filter))];
    }
}
