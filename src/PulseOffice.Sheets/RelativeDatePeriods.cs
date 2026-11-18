namespace PulseOffice.Sheets;

public enum RelativeDatePeriod
{
    Today,
    Yesterday,
    Tomorrow,
    ThisWeek,
    LastWeek,
    NextWeek,
    ThisMonth,
    LastMonth,
    NextMonth,
    ThisQuarter,
    LastQuarter,
    NextQuarter,
    ThisYear,
    LastYear,
    NextYear,
    YearToDate,
}

public sealed record RelativeDateBounds(DateTime Start, DateTime End)
{
    public bool Contains(double serial) => serial >= Start.ToOADate() && serial < End.ToOADate();
}

public sealed class RelativeDatePeriods
{
    public RelativeDateBounds Bounds(RelativeDatePeriod period, DateTime today)
    {
        var day = today.Date;
        var week = day.AddDays(-(int)day.DayOfWeek);
        var month = new DateTime(day.Year, day.Month, 1);
        var quarter = new DateTime(day.Year, (day.Month - 1) / 3 * 3 + 1, 1);
        var year = new DateTime(day.Year, 1, 1);
        return period switch
        {
            RelativeDatePeriod.Today => new(day, day.AddDays(1)),
            RelativeDatePeriod.Yesterday => new(day.AddDays(-1), day),
            RelativeDatePeriod.Tomorrow => new(day.AddDays(1), day.AddDays(2)),
            RelativeDatePeriod.ThisWeek => new(week, week.AddDays(7)),
            RelativeDatePeriod.LastWeek => new(week.AddDays(-7), week),
            RelativeDatePeriod.NextWeek => new(week.AddDays(7), week.AddDays(14)),
            RelativeDatePeriod.ThisMonth => new(month, month.AddMonths(1)),
            RelativeDatePeriod.LastMonth => new(month.AddMonths(-1), month),
            RelativeDatePeriod.NextMonth => new(month.AddMonths(1), month.AddMonths(2)),
            RelativeDatePeriod.ThisQuarter => new(quarter, quarter.AddMonths(3)),
            RelativeDatePeriod.LastQuarter => new(quarter.AddMonths(-3), quarter),
            RelativeDatePeriod.NextQuarter => new(quarter.AddMonths(3), quarter.AddMonths(6)),
            RelativeDatePeriod.ThisYear => new(year, year.AddYears(1)),
            RelativeDatePeriod.LastYear => new(year.AddYears(-1), year),
            RelativeDatePeriod.NextYear => new(year.AddYears(1), year.AddYears(2)),
            _ => new(year, day.AddDays(1)),
        };
    }
}
