using PulseOffice.Sheets;

namespace PulseOffice.Formats.OpenXml;

public sealed class XlsxDateEpochs
{
    private readonly XlsxLeapYearSerials _leapYear = new();

    public bool IsNative(DateOnly epoch) =>
        epoch == CalculationSettings.StandardDateEpoch || epoch == CalculationSettings.Mac1904DateEpoch;

    public bool Writes1904(DateOnly epoch) => epoch == CalculationSettings.Mac1904DateEpoch;

    public DateOnly Read(bool date1904) => date1904 ? CalculationSettings.Mac1904DateEpoch : CalculationSettings.StandardDateEpoch;

    public double ToStandard(double serial, DateOnly epoch) =>
        IsNative(epoch) ? serial : serial + (epoch.DayNumber - CalculationSettings.StandardDateEpoch.DayNumber);

    public double ToExcel(double serial, DateOnly epoch) =>
        Writes1904(epoch) ? serial : _leapYear.ToExcel(ToStandard(serial, epoch));

    public bool PrecedesExcelCalendar(double serial, DateOnly epoch) =>
        Writes1904(epoch) ? serial < 0 : _leapYear.PrecedesExcelCalendar(ToStandard(serial, epoch));
}
