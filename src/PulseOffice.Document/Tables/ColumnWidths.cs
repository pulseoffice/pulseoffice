namespace PulseOffice.Document.Tables;

public interface IColumnWidths
{
    IReadOnlyList<double> Normalize(IReadOnlyList<double> weights);

    IReadOnlyList<double> Distribute(IReadOnlyList<double> widths, int first, int last);

    IReadOnlyList<double> Resize(IReadOnlyList<double> widths, int column, double width, double minimum);

    IReadOnlyList<double> Fit(IReadOnlyList<double> widths, int first, int last, IReadOnlyList<double> wanted, double minimum, double? limit);
}

public sealed class ColumnWidths : IColumnWidths
{
    public IReadOnlyList<double> Normalize(IReadOnlyList<double> weights)
    {
        var total = weights.Sum();
        return total <= 0
            ? [.. weights.Select(_ => 1.0 / weights.Count)]
            : [.. weights.Select(weight => weight / total)];
    }

    public IReadOnlyList<double> Distribute(IReadOnlyList<double> widths, int first, int last)
    {
        if (widths.Count == 0) return widths;
        first = Math.Clamp(first, 0, widths.Count - 1);
        last = Math.Clamp(last, first, widths.Count - 1);
        var share = widths.Skip(first).Take(last - first + 1).Sum() / (last - first + 1);
        return [.. widths.Select((width, index) => index >= first && index <= last ? share : width)];
    }

    public IReadOnlyList<double> Resize(IReadOnlyList<double> widths, int column, double width, double minimum)
    {
        if (column < 0 || column >= widths.Count) return widths;
        var result = widths.ToList();
        if (column + 1 < widths.Count)
        {
            var pair = widths[column] + widths[column + 1];
            var left = Math.Clamp(width, minimum, Math.Max(minimum, pair - minimum));
            result[column] = left;
            result[column + 1] = pair - left;
        }
        else
        {
            result[column] = Math.Max(minimum, width);
        }
        return result;
    }

    public IReadOnlyList<double> Fit(IReadOnlyList<double> widths, int first, int last, IReadOnlyList<double> wanted, double minimum, double? limit)
    {
        if (widths.Count == 0 || wanted.Count != widths.Count) return widths;
        first = Math.Clamp(first, 0, widths.Count - 1);
        last = Math.Clamp(last, first, widths.Count - 1);
        var result = widths.ToList();
        for (var column = first; column <= last; column++) result[column] = Math.Max(minimum, wanted[column]);
        if (limit is { } total && result.Sum() > total)
        {
            var fixedPart = result.Where((_, index) => index < first || index > last).Sum();
            var room = Math.Max(minimum * (last - first + 1), total - fixedPart);
            var fitted = result.Skip(first).Take(last - first + 1).Sum();
            for (var column = first; column <= last; column++) result[column] = Math.Max(minimum, result[column] * room / fitted);
        }
        return result;
    }
}
