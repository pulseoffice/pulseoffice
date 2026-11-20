using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace PulseOffice.Documents.Fields;

public enum MergeFilterOperator
{
    Contains,
    Equals,
    NotEquals,
    Empty,
    NotEmpty,
    Less,
    Greater,
}

public enum MergeJoin
{
    And,
    Or,
}

public sealed record MergeCondition(string Column, MergeFilterOperator Operator, string Value)
{
    public MergeJoin Join { get; init; }
}

public sealed record MergeFilter(IReadOnlyList<MergeCondition> Conditions)
{
    public static readonly MergeFilter None = new([]);

    public MergeFilter(string column, MergeFilterOperator @operator, string value) : this([new MergeCondition(column, @operator, value)])
    {
    }

    public bool Equals(MergeFilter? other) => other is not null && Conditions.SequenceEqual(other.Conditions);

    public override int GetHashCode() => Conditions.Count;
}

public sealed record MergeSort(string Column, bool Descending);

public sealed record MergeSelection(MergeFilter Filter, IReadOnlyList<MergeSort> Sort, IReadOnlySet<int> Excluded)
{
    public static readonly MergeSelection All = new(MergeFilter.None, [], new HashSet<int>());

    public IReadOnlySet<string> ExcludedRecords { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    public string KeyColumn { get; init; } = string.Empty;

    public bool IsEmpty => Filter.Conditions.All(condition => condition.Column.Length == 0) && Sort.Count == 0 && Excluded.Count == 0 && ExcludedRecords.Count == 0;

    public bool Equals(MergeSelection? other) =>
        other is not null && Filter.Equals(other.Filter) && Sort.SequenceEqual(other.Sort) && Excluded.SetEquals(other.Excluded) && ExcludedRecords.SetEquals(other.ExcludedRecords)
        && string.Equals(KeyColumn, other.KeyColumn, StringComparison.Ordinal);

    public override int GetHashCode() => HashCode.Combine(Filter, Sort.Count, Excluded.Count);
}

public interface IMergeRecipients
{
    bool Matches(MergeData data, int row, MergeFilter? filter);

    IReadOnlyList<int> Ordered(MergeData data, IEnumerable<int> rows, IReadOnlyList<MergeSort> sort);

    IReadOnlyList<int> Rows(MergeData data, MergeFilter? filter, IReadOnlySet<int> excluded);

    IReadOnlyList<int> Rows(MergeData data, MergeSelection selection);

    string RecordIdentity(MergeData data, int row);

    IReadOnlySet<int> ExcludedRows(MergeData data, MergeSelection selection);

    MergeSelection Keyed(MergeData data, MergeSelection selection);

    IReadOnlyList<string> UniqueColumns(MergeData data);
}

public sealed class MergeRecipients : IMergeRecipients
{
    public bool Matches(MergeData data, int row, MergeFilter? filter)
    {
        ArgumentNullException.ThrowIfNull(data);
        var conditions = filter?.Conditions.Where(condition => condition.Column.Length > 0).ToList() ?? [];
        if (conditions.Count == 0) return true;
        var group = true;
        for (var index = 0; index < conditions.Count; index++)
        {
            if (index > 0 && conditions[index].Join == MergeJoin.Or)
            {
                if (group) return true;
                group = true;
            }
            group = group && Holds(data, row, conditions[index]);
        }
        return group;
    }

    private bool Holds(MergeData data, int row, MergeCondition condition)
    {
        var value = (data.Value(row, condition.Column) ?? string.Empty).Trim();
        var wanted = condition.Value.Trim();
        var compare = CultureInfo.CurrentCulture.CompareInfo;
        return condition.Operator switch
        {
            MergeFilterOperator.Contains => compare.IndexOf(value, wanted, CompareOptions.IgnoreCase) >= 0,
            MergeFilterOperator.Equals => Compared(value, wanted) == 0,
            MergeFilterOperator.NotEquals => Compared(value, wanted) != 0,
            MergeFilterOperator.Empty => value.Length == 0,
            MergeFilterOperator.Less => value.Length > 0 && Compared(value, wanted) < 0,
            MergeFilterOperator.Greater => value.Length > 0 && Compared(value, wanted) > 0,
            _ => value.Length > 0,
        };
    }

    private int Compared(string left, string right)
    {
        if (Number(left) is { } a && Number(right) is { } b) return a.CompareTo(b);
        if (Date(left) is { } c && Date(right) is { } d) return c.CompareTo(d);
        return CultureInfo.CurrentCulture.CompareInfo.Compare(left, right, CompareOptions.IgnoreCase);
    }

    private decimal? Number(string text) =>
        decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out var value) || decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value)
            ? value
            : null;

    private DateTime? Date(string text) =>
        DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out var value) || DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out value)
            ? value
            : null;

    public IReadOnlyList<int> Ordered(MergeData data, IEnumerable<int> rows, IReadOnlyList<MergeSort> sort)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(sort);
        var list = rows.ToList();
        var keys = sort.Where(key => key.Column.Length > 0).ToList();
        if (keys.Count == 0) return list;
        return [.. list.Order(Comparer<int>.Create((left, right) =>
        {
            foreach (var key in keys)
            {
                var order = Compared((data.Value(left, key.Column) ?? string.Empty).Trim(), (data.Value(right, key.Column) ?? string.Empty).Trim());
                if (order != 0) return key.Descending ? -order : order;
            }
            return left.CompareTo(right);
        }))];
    }

    public IReadOnlyList<int> Rows(MergeData data, MergeFilter? filter, IReadOnlySet<int> excluded)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(excluded);
        return [.. Enumerable.Range(0, data.Rows.Count).Where(row => !excluded.Contains(row) && Matches(data, row, filter))];
    }

    public IReadOnlyList<int> Rows(MergeData data, MergeSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        return Ordered(data, Rows(data, selection.Filter, ExcludedRows(data, selection)), selection.Sort);
    }

    public string RecordIdentity(MergeData data, int row)
    {
        ArgumentNullException.ThrowIfNull(data);
        var values = row >= 0 && row < data.Rows.Count ? data.Rows[row] : [];
        var text = string.Join('\u001F', values.Select(value => (value ?? string.Empty).Trim()));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)), 0, 12).ToLowerInvariant();
    }

    public IReadOnlySet<int> ExcludedRows(MergeData data, MergeSelection selection)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(selection);
        if (selection.ExcludedRecords.Count == 0) return new HashSet<int>(selection.Excluded.Where(row => row >= 0 && row < data.Rows.Count));
        var column = Column(data, selection.KeyColumn);
        if (column is not null && selection.ExcludedRecords.Any(record => record.StartsWith(KeyPrefix, StringComparison.Ordinal)))
            return new HashSet<int>(Enumerable.Range(0, data.Rows.Count).Where(row => selection.ExcludedRecords.Contains(KeyIdentity(data, row, column))));
        return new HashSet<int>(Enumerable.Range(0, data.Rows.Count).Where(row => selection.ExcludedRecords.Contains(RecordIdentity(data, row))));
    }

    private const string KeyPrefix = "key:";

    private string KeyIdentity(MergeData data, int row, string column) => KeyPrefix + (data.Value(row, column) ?? string.Empty).Trim();

    private string? Column(MergeData data, string name) =>
        name.Length == 0 ? null : data.Columns.FirstOrDefault(column => string.Equals(column, name, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<string> UniqueColumns(MergeData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Rows.Count == 0) return [];
        return [.. data.Columns.Where(column =>
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var row = 0; row < data.Rows.Count; row++)
            {
                var value = (data.Value(row, column) ?? string.Empty).Trim();
                if (value.Length == 0 || !seen.Add(value)) return false;
            }
            return true;
        })];
    }

    public MergeSelection Keyed(MergeData data, MergeSelection selection)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(selection);
        var rows = selection.Excluded.Where(row => row >= 0 && row < data.Rows.Count).ToList();
        var column = Column(data, selection.KeyColumn);
        var key = column is not null && UniqueColumns(data).Contains(column, StringComparer.Ordinal) ? column : null;
        var records = new HashSet<string>(rows.Select(row => RecordIdentity(data, row)), StringComparer.Ordinal);
        if (key is not null) records.UnionWith(rows.Select(row => KeyIdentity(data, row, key)));
        return selection with { ExcludedRecords = records, KeyColumn = key ?? string.Empty };
    }
}
