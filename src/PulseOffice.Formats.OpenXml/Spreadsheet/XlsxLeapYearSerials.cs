using PulseOffice.Sheets;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Formats.OpenXml;

public sealed class XlsxLeapYearSerials
{
    public const double FirstExcelDay = 1;
    public const double FictitiousDay = 60;

    public bool IsFictitiousDay(double excelSerial) => excelSerial >= FictitiousDay && excelSerial < FictitiousDay + 1;

    public double FromExcel(double excelSerial) => excelSerial switch
    {
        >= FirstExcelDay and < FictitiousDay => excelSerial + 1,
        _ => excelSerial,
    };

    public double ToExcel(double serial) => serial switch
    {
        >= FirstExcelDay + 1 and < FictitiousDay + 1 => serial - 1,
        _ => serial,
    };

    public bool PrecedesExcelCalendar(double serial) => serial < 0 || (serial >= FirstExcelDay && serial < FirstExcelDay + 1);

    public (int Shifted, int Fictitious) ShiftToStandardSystem(Workbook workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        var shifted = 0;
        var fictitious = 0;
        foreach (var (sheet, address, cell) in new XlsxCalendarCells(workbook).ConstantDates())
        {
            var serial = cell.Value.AsNumber();
            if (IsFictitiousDay(serial)) fictitious++;
            var standard = FromExcel(serial);
            if (standard == serial) continue;
            sheet[address] = cell with { Value = CellValue.Number(standard) };
            shifted++;
        }
        return (shifted, fictitious);
    }
}
