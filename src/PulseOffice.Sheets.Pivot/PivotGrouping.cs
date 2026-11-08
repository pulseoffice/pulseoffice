using System.Globalization;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Pivot;

public interface IPivotGrouping
{
    string KeyOf(CellValue value, PivotField field);

    double? OrderOf(string key, PivotField field);
}

public sealed class PivotGrouping(Func<double, DateTime>? serialToDate = null) : IPivotGrouping
{
    private readonly Func<double, DateTime> _serialToDate = serialToDate
        ?? (serial => new DateOnly(1899, 12, 30).ToDateTime(TimeOnly.MinValue).AddDays(serial));

    public string KeyOf(CellValue value, PivotField field)
    {
        ArgumentNullException.ThrowIfNull(field);

        if (field.DateGrouping != DateGrouping.None && value.TryCoerceToNumber(out var serial) && serial is > MinimumSerial and < MaximumSerial)
            return DateKey(_serialToDate(serial), field.DateGrouping);

        if (field.NumericGroupInterval is { } interval && interval > 0
            && value.TryCoerceToNumber(out var number))
        {
            var start = _bins.StartOf(number, interval);
            return string.Create(CultureInfo.InvariantCulture,
                $"{_bins.Format(start)}{BinSeparator}{_bins.Format(start + interval)}");
        }

        return PivotValues.KeyOf(value);
    }

    public double? OrderOf(string key, PivotField field)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(field);

        if (field.NumericGroupInterval is not { } interval || interval <= 0) return null;

        var separator = key.IndexOf(BinSeparator, 1, StringComparison.Ordinal);
        return separator > 0
            && double.TryParse(key[..separator], NumberStyles.Float, CultureInfo.InvariantCulture, out var start)
                ? start
                : null;
    }

    private const string BinSeparator = "-";
    private const double MinimumSerial = -657435;
    private const double MaximumSerial = 2958466;

    private readonly NumericBins _bins = new();

    private static string DateKey(DateTime moment, DateGrouping grouping) => grouping switch
    {
        DateGrouping.Years => moment.ToString("yyyy", CultureInfo.InvariantCulture),
        DateGrouping.Quarters => string.Create(CultureInfo.InvariantCulture,
            $"{moment:yyyy}-T{(moment.Month - 1) / 3 + 1}"),
        DateGrouping.Months => moment.ToString("yyyy-MM", CultureInfo.InvariantCulture),
        DateGrouping.Days => moment.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateGrouping.Hours => moment.ToString("yyyy-MM-dd HH", CultureInfo.InvariantCulture),
        DateGrouping.Minutes => moment.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
        DateGrouping.Seconds => moment.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        _ => moment.ToString("O", CultureInfo.InvariantCulture),
    };
}

internal sealed class NumericBins
{
    private const double RelativeTolerance = 1e-9;

    public double StartOf(double number, double interval)
    {
        var ratio = number / interval;
        var nearest = Math.Round(ratio);
        var index = Math.Abs(ratio - nearest) <= RelativeTolerance * Math.Max(1, Math.Abs(ratio))
            ? nearest
            : Math.Floor(ratio);

        return index * interval;
    }

    public string Format(double value) =>
        Math.Round(value, 12).ToString("0.############", CultureInfo.InvariantCulture);
}
