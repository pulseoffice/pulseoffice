using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Numerics;

public sealed class WorkdayCalculator(IDateSystem dates, HolidayCalendar calendar, IWorkdayArithmetic arithmetic)
{
    public CellValue Evaluate(ArgumentReader r, bool count, WorkWeek week, int holidaysAt)
    {
        var start = r.Number(0);
        var second = r.Number(1);
        if (r.HasError) return r.Error;

        var holidays = new HashSet<DateTime>();
        if (calendar.Collect(r, holidaysAt, holidays) is { } failure) return failure;

        var from = DateOnly.FromDateTime(dates.FromSerial(start));
        var days = holidays.Select(DateOnly.FromDateTime).ToList();
        if (count) return CellValue.Number(arithmetic.Count(from, DateOnly.FromDateTime(dates.FromSerial(second)), week, days));

        var workdays = Math.Truncate(second);
        if (!(Math.Abs(workdays) < long.MaxValue / 2)) return CellValue.FromError(CellError.Number);
        return arithmetic.Advance(from, (long)workdays, week, days) is { } end
            ? FunctionBuilder.Number(Math.Floor(dates.ToSerial(end.ToDateTime(TimeOnly.MinValue))))
            : CellValue.FromError(CellError.Number);
    }
}
