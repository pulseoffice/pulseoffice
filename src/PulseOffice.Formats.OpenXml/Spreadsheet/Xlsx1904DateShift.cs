using PulseOffice.Sheets;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Formats.OpenXml;

public sealed class Xlsx1904DateShift
{
    public const double OffsetDays = 1462;

    public int ShiftToStandardSystem(Workbook workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        var dates = new XlsxCalendarCells(workbook).ConstantDates();
        foreach (var (sheet, address, cell) in dates)
            sheet[address] = cell with { Value = CellValue.Number(cell.Value.AsNumber() + OffsetDays) };
        return dates.Count;
    }
}
