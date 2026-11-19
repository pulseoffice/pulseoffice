using System.Globalization;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Queries;

public interface IQueryValueConverter
{
    CellValue Convert(object? value, CalculationSettings calculation);

    CellValue Convert(object? value) => Convert(value, CalculationSettings.Default);
}

public sealed class QueryValueConverter(Func<DateTime, double>? toSerial = null) : IQueryValueConverter
{
    public CellValue Convert(object? value) => Convert(value, CalculationSettings.Default);

    public CellValue Convert(object? value, CalculationSettings calculation) => value switch
    {
        null or DBNull => CellValue.Empty,
        bool flag => CellValue.Boolean(flag),
        DateTime moment => CellValue.Number(Serial(moment, calculation)),
        DateTimeOffset moment => CellValue.Number(Serial(moment.DateTime, calculation)),
        DateOnly day => CellValue.Number(Serial(day.ToDateTime(TimeOnly.MinValue), calculation)),
        TimeOnly time => CellValue.Number(time.ToTimeSpan().TotalDays),
        TimeSpan span => CellValue.Number(span.TotalDays),
        string text => CellValue.Text(text),
        byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal =>
            CellValue.Number(System.Convert.ToDouble(value, CultureInfo.InvariantCulture)),
        _ => CellValue.Text(value.ToString() ?? string.Empty),
    };

    private double Serial(DateTime moment, CalculationSettings calculation) =>
        toSerial is null ? calculation.SerialOf(moment) : toSerial(moment);
}
