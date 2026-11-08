using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Numerics;

public sealed class HolidayCalendar(IDateSystem dates, NumericText numbers)
{
    public CellValue? Collect(ArgumentReader r, int index, HashSet<DateTime> holidays)
    {
        if (!r.Has(index)) return null;
        foreach (var value in r.Values(index))
        {
            if (value.IsError) return value;
            if (value.Kind == ValueKind.Number) holidays.Add(dates.FromSerial(value.AsNumber()).Date);
            else if (value.Kind == ValueKind.Text && numbers.TryReadDate(value.AsText(), out var holiday)) holidays.Add(holiday.Date);
        }
        return null;
    }
}
