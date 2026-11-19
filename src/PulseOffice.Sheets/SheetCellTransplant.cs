using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets;

public interface ISheetCellTransplant
{
    void Copy(Workbook source, Worksheet from, Workbook target, Worksheet into);
}

public sealed class SheetCellTransplant(IStyleTransplant styles, ICalendarFormats calendars) : ISheetCellTransplant
{
    public SheetCellTransplant() : this(new StyleTransplant(), new CalendarFormats())
    {
    }

    public void Copy(Workbook source, Worksheet from, Workbook target, Worksheet into)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(into);

        var shift = source.Calculation.DateEpoch.DayNumber - target.Calculation.DateEpoch.DayNumber;
        var carried = new Dictionary<int, int>();
        var dated = new Dictionary<int, bool>();

        foreach (var (address, cell) in from.NonEmptyCells())
        {
            if (cell.Formula is { } formula) into.SetFormula(address, formula);
            else into.SetValue(address, Rebased(cell, shift, source, dated));

            if (!cell.HasStyle) continue;
            if (!carried.TryGetValue(cell.StyleId, out var id))
            {
                id = target.Styles.Intern(styles.Carry(source.Styles.Get(cell.StyleId), source.NumberFormats, target.NumberFormats));
                carried[cell.StyleId] = id;
            }
            into.SetStyle(address, id);
        }
    }

    private CellValue Rebased(Cell cell, int shift, Workbook source, Dictionary<int, bool> dated)
    {
        if (shift == 0 || cell.Value.Kind != ValueKind.Number || cell.Value.AsNumber() < 1) return cell.Value;
        var styleId = cell.StyleId;
        if (!dated.TryGetValue(styleId, out var isDate))
        {
            isDate = calendars.NamesDays(source, styleId);
            dated[styleId] = isDate;
        }
        return isDate ? CellValue.Number(cell.Value.AsNumber() + shift) : cell.Value;
    }
}
