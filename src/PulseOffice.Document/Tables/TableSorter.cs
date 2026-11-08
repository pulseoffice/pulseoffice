using System.Globalization;

namespace PulseOffice.Document.Tables;

public enum TableSortKind
{
    Alphanumeric,
    Numeric,
}

public sealed record TableSortKey(int Column, bool Descending = false, TableSortKind Kind = TableSortKind.Alphanumeric);

public interface ITableSorter
{
    IReadOnlyList<int> Order(IReadOnlyList<IReadOnlyList<string>> rows, IReadOnlyList<TableSortKey> keys, CultureInfo culture);
}

public sealed class TableSorter : ITableSorter
{
    public IReadOnlyList<int> Order(IReadOnlyList<IReadOnlyList<string>> rows, IReadOnlyList<TableSortKey> keys, CultureInfo culture)
    {
        var comparer = StringComparer.Create(culture, CompareOptions.IgnoreCase);
        var indexes = Enumerable.Range(0, rows.Count).ToList();
        indexes.Sort((a, b) =>
        {
            foreach (var key in keys)
            {
                var result = Compare(Text(rows[a], key.Column), Text(rows[b], key.Column), key.Kind, comparer, culture);
                if (result != 0) return key.Descending ? -result : result;
            }
            return a.CompareTo(b);
        });
        return indexes;
    }

    private string Text(IReadOnlyList<string> row, int column) => column >= 0 && column < row.Count ? row[column].Trim() : string.Empty;

    private int Compare(string a, string b, TableSortKind kind, StringComparer comparer, CultureInfo culture)
    {
        if (kind == TableSortKind.Numeric)
        {
            var left = Number(a, culture);
            var right = Number(b, culture);
            if (left is { } x && right is { } y) return x.CompareTo(y);
            if (left is not null) return -1;
            if (right is not null) return 1;
        }
        if (a.Length == 0 && b.Length == 0) return 0;
        if (a.Length == 0) return 1;
        if (b.Length == 0) return -1;
        return comparer.Compare(a, b);
    }

    private double? Number(string text, CultureInfo culture) =>
        double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, culture, out var value) ? value
        : double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var invariant) ? invariant
        : null;
}
