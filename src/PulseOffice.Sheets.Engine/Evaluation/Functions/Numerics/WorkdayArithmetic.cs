namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Numerics;

public sealed class WorkWeek
{
    private const int DaysPerWeek = 7;

    private readonly bool[] _workdays;

    public WorkWeek(string restMask)
    {
        ArgumentNullException.ThrowIfNull(restMask);
        if (restMask.Length != DaysPerWeek) throw new ArgumentException("A week mask has seven days.", nameof(restMask));
        _workdays = [.. restMask.Select(day => day == '0')];
        WorkdaysPerWeek = _workdays.Count(workday => workday);
    }

    public int WorkdaysPerWeek { get; }

    public bool IsWorkday(long dayNumber) => _workdays[(int)(((dayNumber % DaysPerWeek) + DaysPerWeek) % DaysPerWeek)];
}

public interface IWorkdayArithmetic
{
    long Count(DateOnly start, DateOnly end, WorkWeek week, IEnumerable<DateOnly> holidays);

    DateOnly? Advance(DateOnly start, long workdays, WorkWeek week, IEnumerable<DateOnly> holidays);
}

public sealed class WorkdayArithmetic : IWorkdayArithmetic
{
    private const int DaysPerWeek = 7;

    private static readonly long LastDay = DateOnly.MaxValue.DayNumber;

    public long Count(DateOnly start, DateOnly end, WorkWeek week, IEnumerable<DateOnly> holidays)
    {
        ArgumentNullException.ThrowIfNull(week);
        ArgumentNullException.ThrowIfNull(holidays);
        if (end < start) return -Count(end, start, week, holidays);

        long first = start.DayNumber;
        long last = end.DayNumber;
        var length = last - first + 1;
        var weeks = length / DaysPerWeek;
        var total = weeks * week.WorkdaysPerWeek;
        for (var day = first + weeks * DaysPerWeek; day <= last; day++)
            if (week.IsWorkday(day)) total++;

        foreach (var holiday in WorkingHolidays(holidays, week))
            if (holiday >= first && holiday <= last) total--;
        return total;
    }

    public DateOnly? Advance(DateOnly start, long workdays, WorkWeek week, IEnumerable<DateOnly> holidays)
    {
        ArgumentNullException.ThrowIfNull(week);
        ArgumentNullException.ThrowIfNull(holidays);
        if (workdays == 0) return start;
        if (week.WorkdaysPerWeek == 0 || workdays == long.MinValue || Math.Abs(workdays) > LastDay) return null;

        var sorted = WorkingHolidays(holidays, week);
        var direction = workdays > 0 ? 1L : -1L;
        var remaining = Math.Abs(workdays);
        long cursor = start.DayNumber;

        while (remaining > 0)
        {
            var weeks = (remaining - 1) / week.WorkdaysPerWeek;
            if (weeks == 0)
            {
                cursor += direction;
                if (cursor < 0 || cursor > LastDay) return null;
                if (week.IsWorkday(cursor) && Array.BinarySearch(sorted, cursor) < 0) remaining--;
                continue;
            }

            var next = cursor + direction * weeks * DaysPerWeek;
            if (next < 0 || next > LastDay) return null;
            remaining -= weeks * week.WorkdaysPerWeek;
            remaining += direction > 0 ? Between(sorted, cursor + 1, next) : Between(sorted, next, cursor - 1);
            cursor = next;
        }
        return DateOnly.FromDayNumber((int)cursor);
    }

    private static long[] WorkingHolidays(IEnumerable<DateOnly> holidays, WorkWeek week) =>
        [.. holidays.Select(holiday => (long)holiday.DayNumber).Where(week.IsWorkday).Distinct().Order()];

    private static long Between(long[] sorted, long low, long high) =>
        high < low ? 0 : FirstAbove(sorted, high) - FirstAbove(sorted, low - 1);

    private static int FirstAbove(long[] sorted, long value)
    {
        int low = 0, high = sorted.Length;
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (sorted[middle] <= value) low = middle + 1;
            else high = middle;
        }
        return low;
    }
}
