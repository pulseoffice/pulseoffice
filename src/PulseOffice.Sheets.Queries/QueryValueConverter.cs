using System.Globalization;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Queries;

public interface IQueryValueConverter
{
    CellValue Convert(object? value);
}

public sealed class QueryValueConverter(Func<DateTime, double>? toSerial = null) : IQueryValueConverter
{
    private readonly Func<DateTime, double> _toSerial = toSerial ?? (moment => (moment - new DateTime(1899, 12, 30)).TotalDays);

    public CellValue Convert(object? value) => value switch
    {
        null or DBNull => CellValue.Empty,
        bool flag => CellValue.Boolean(flag),
        DateTime moment => CellValue.Number(_toSerial(moment)),
        DateTimeOffset moment => CellValue.Number(_toSerial(moment.DateTime)),
        DateOnly day => CellValue.Number(_toSerial(day.ToDateTime(TimeOnly.MinValue))),
        TimeOnly time => CellValue.Number(time.ToTimeSpan().TotalDays),
        TimeSpan span => CellValue.Number(span.TotalDays),
        string text => CellValue.Text(text),
        byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal =>
            CellValue.Number(System.Convert.ToDouble(value, CultureInfo.InvariantCulture)),
        _ => CellValue.Text(value.ToString() ?? string.Empty),
    };
}
