namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Numerics;

public static class DayCount
{
    public static double? Days(DateTime start, DateTime end, int basis) => basis switch
    {
        0 => Days360Us(start, end),
        1 or 2 or 3 => (end.Date - start.Date).TotalDays,
        4 => Days360European(start, end),
        _ => null,
    };

    public static double? SecuritySpan(DateTime start, DateTime end, int basis)
    {
        if (basis is < 0 or > 4) return null;
        if (start > end) (start, end) = (end, start);
        var actual = (end.Date - start.Date).TotalDays;
        return basis switch
        {
            1 => actual / (DateTime.IsLeapYear(start.Year) ? 366 : 365),
            2 => actual / 360,
            3 => actual / 365,
            _ => ThirtyDayMonths(start, end, basis) / 360,
        };
    }

    private static double ThirtyDayMonths(DateTime start, DateTime end, int basis)
    {
        var days = ((end.Year - start.Year) * 12 + end.Month - start.Month) * 30 + end.Day - start.Day;
        if (basis == 0 && start.Month == 2 && end.Month != 2 && start.Year == end.Year)
            days -= DateTime.IsLeapYear(start.Year) ? 1 : 2;
        return days;
    }

    public static double? YearFraction(DateTime start, DateTime end, int basis)
    {
        if (basis is < 0 or > 4) return null;
        if (start.Date == end.Date) return 0;
        if (start > end) (start, end) = (end, start);

        double days = basis switch
        {
            0 => YearFractionDays360Us(start, end),
            4 => Days360European(start, end),
            _ => (end.Date - start.Date).TotalDays,
        };
        double year = basis switch
        {
            1 => ActualYearLength(start, end),
            3 => 365,
            _ => 360,
        };
        return days / year;
    }

    private static double YearFractionDays360Us(DateTime start, DateTime end)
    {
        int startDay = start.Day, endDay = end.Day;
        if (startDay == 31) startDay--;
        if (startDay == 30 && endDay == 31) endDay--;
        else if (start.Month == 2 && startDay == DateTime.DaysInMonth(start.Year, 2))
        {
            startDay = 30;
            if (end.Month == 2 && endDay == DateTime.DaysInMonth(end.Year, 2)) endDay = 30;
        }
        return (end.Year - start.Year) * 360 + (end.Month - start.Month) * 30 + (endDay - startDay);
    }

    private static double ActualYearLength(DateTime start, DateTime end)
    {
        var sameYear = start.Year == end.Year;
        var withinYear = sameYear || (end.Year == start.Year + 1
            && (start.Month > end.Month || (start.Month == end.Month && start.Day >= end.Day)));
        if (!withinYear) return AverageYearLength(start, end);
        if (sameYear) return DateTime.IsLeapYear(start.Year) ? 366 : 365;

        var startCoversLeapDay = DateTime.IsLeapYear(start.Year) && (start.Month < 2 || (start.Month == 2 && start.Day <= 29));
        var endCoversLeapDay = DateTime.IsLeapYear(end.Year) && (end.Month > 2 || (end.Month == 2 && end.Day == 29));
        return startCoversLeapDay || endCoversLeapDay ? 366 : 365;
    }

    private static double Days360Us(DateTime start, DateTime end)
    {
        int startDay = start.Day, endDay = end.Day;
        if (startDay == 31) startDay = 30;
        if (endDay == 31 && startDay == 30) endDay = 30;
        return (end.Year - start.Year) * 360 + (end.Month - start.Month) * 30 + (endDay - startDay);
    }

    private static double Days360European(DateTime start, DateTime end)
    {
        var startDay = System.Math.Min(start.Day, 30);
        var endDay = System.Math.Min(end.Day, 30);
        return (end.Year - start.Year) * 360 + (end.Month - start.Month) * 30 + (endDay - startDay);
    }

    private static double AverageYearLength(DateTime start, DateTime end)
    {
        if (start.Year == end.Year)
            return DateTime.IsLeapYear(start.Year) ? 366 : 365;

        var total = 0.0;
        for (var year = start.Year; year <= end.Year; year++)
            total += DateTime.IsLeapYear(year) ? 366 : 365;
        return total / (end.Year - start.Year + 1);
    }
}
