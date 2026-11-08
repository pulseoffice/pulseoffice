using System.Globalization;
using PulseOffice.Sheets.Engine.Evaluation.Functions.Numerics;
using PulseOffice.Sheets.Values;
using M = System.Math;
using F = PulseOffice.Sheets.Engine.Evaluation.Functions.FunctionBuilder;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Providers;

public sealed class DateTimeFunctions(IDateSystem dates, ISystemClock clock, NumericText numbers, WorkdayCalculator workdays) : IFunctionProvider
{
    private static readonly System.Text.RegularExpressions.Regex Duration = new(
        @"^(?<hours>[0-9]{1,5}):(?<minutes>[0-5]?[0-9])(?::(?<seconds>[0-5]?[0-9](?:\.[0-9]+)?))?$",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    public string Category => "Data e ora";

    public IEnumerable<FunctionDefinition> GetFunctions() => new FunctionBuilder(Category)
        .Constant("TODAY", () => CellValue.Number(dates.ToSerial(clock.Today.ToDateTime(TimeOnly.MinValue))))
        .Constant("NOW", () => CellValue.Number(dates.ToSerial(clock.Now)))

        .Raw("DATE", 3, 3, r =>
        {
            var year = r.Integer(0);
            var month = r.Integer(1);
            var day = r.Integer(2);
            if (r.HasError) return r.Error;

            if (year is >= 0 and < 30) year += 2000;
            else if (year is >= 30 and < 1900) year += 1900;
            if (year is < 1 or > 9999) return F.OutOfDomain();

            try
            {
                var value = new DateTime(year, 1, 1).AddMonths(month - 1).AddDays(day - 1);
                return F.Number(dates.ToSerial(value));
            }
            catch (ArgumentOutOfRangeException)
            {
                return F.OutOfDomain();
            }
        })

        .Raw("TIME", 3, 3, r =>
        {
            var hours = r.Integer(0);
            var minutes = r.Integer(1);
            var seconds = r.Number(2);
            if (r.HasError) return r.Error;

            var fraction = dates.TimeToFraction(hours, minutes, seconds);
            if (fraction < 0) return F.OutOfDomain();

            return F.Number(fraction - M.Floor(fraction));
        })

        .Raw("DATEVALUE", 1, 1, r =>
        {
            var text = r.Text(0);
            if (r.HasError) return r.Error;
            if (TimeSpan.TryParseExact(text.Trim(), [@"h\:mm", @"h\:mm\:ss", @"hh\:mm", @"hh\:mm\:ss"], System.Globalization.CultureInfo.InvariantCulture, out var clock))
                return F.Number(clock.TotalDays - M.Floor(clock.TotalDays));
            return numbers.TryReadDate(text, out var value)
                ? F.Number(M.Floor(dates.ToSerial(value)))
                : F.Invalid();
        })

        .Raw("TIMEVALUE", 1, 1, r =>
        {
            var text = r.Text(0);
            if (r.HasError) return r.Error;
            if (Duration.Match(text.Trim()) is { Success: true } duration)
            {
                var days = (double.Parse(duration.Groups["hours"].Value, CultureInfo.InvariantCulture) * 3600
                    + double.Parse(duration.Groups["minutes"].Value, CultureInfo.InvariantCulture) * 60
                    + (duration.Groups["seconds"].Success ? double.Parse(duration.Groups["seconds"].Value, CultureInfo.InvariantCulture) : 0)) / 86400;
                return F.Number(days - M.Floor(days));
            }
            return numbers.TryReadDate(text, out var value)
                ? F.Number(dates.ToSerial(value) - M.Floor(dates.ToSerial(value)))
                : F.Invalid();
        })

        .Numeric1("YEAR", s => Component(s, d => d.Year))
        .Numeric1("MONTH", s => Component(s, d => d.Month))
        .Numeric1("DAY", s => Component(s, d => d.Day))
        .Numeric1("HOUR", s => Clock(s, (seconds, _) => M.Floor(seconds / 3600)))
        .Numeric1("MINUTE", s => Clock(s, (seconds, _) => M.Floor(seconds / 60) % 60))
        .Numeric1("SECOND", s => Clock(s, (seconds, fraction) => (seconds % 60 + (fraction >= 0.5 ? 1 : 0)) % 60))

        .Raw("WEEKDAY", 1, 2, r =>
        {
            var serial = r.Number(0);
            var type = r.Integer(1, 1);
            if (r.HasError) return r.Error;

            var day = (int)dates.FromSerial(serial).DayOfWeek;
            return type switch
            {
                1 => CellValue.Number(day + 1),
                2 => CellValue.Number((day + 6) % 7 + 1),
                3 => CellValue.Number((day + 6) % 7),
                11 => CellValue.Number((day + 6) % 7 + 1),
                12 => CellValue.Number((day + 5) % 7 + 1),
                13 => CellValue.Number((day + 4) % 7 + 1),
                14 => CellValue.Number((day + 3) % 7 + 1),
                15 => CellValue.Number((day + 2) % 7 + 1),
                16 => CellValue.Number((day + 1) % 7 + 1),
                17 => CellValue.Number(day + 1),
                _ => F.OutOfDomain(),
            };
        })

        .Raw("WEEKNUM", 1, 2, r =>
        {
            var serial = r.Number(0);
            var mode = r.Integer(1, 1);
            if (r.HasError) return r.Error;

            var date = dates.FromSerial(serial);
            if (mode is 21 or 150) return CellValue.Number(IsoWeek(date));

            var firstDay = mode switch
            {
                1 or 17 => DayOfWeek.Sunday,
                2 or 11 => DayOfWeek.Monday,
                12 => DayOfWeek.Tuesday,
                13 => DayOfWeek.Wednesday,
                14 => DayOfWeek.Thursday,
                15 => DayOfWeek.Friday,
                16 => DayOfWeek.Saturday,
                _ => (DayOfWeek?)null,
            };
            if (firstDay is null) return F.OutOfDomain();

            var week = CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
                date, CalendarWeekRule.FirstDay, firstDay.Value);
            return CellValue.Number(week);
        })

        .Numeric1("ISOWEEKNUM", s => CellValue.Number(IsoWeek(dates.FromSerial(s))))

        .Raw("EDATE", 2, 2, r =>
        {
            var serial = r.Number(0);
            var months = r.Integer(1);
            if (r.HasError) return r.Error;
            return F.Number(M.Floor(dates.ToSerial(dates.FromSerial(serial).AddMonths(months))));
        })

        .Raw("EOMONTH", 2, 2, r =>
        {
            var serial = r.Number(0);
            var months = r.Integer(1);
            if (r.HasError) return r.Error;

            var shifted = dates.FromSerial(serial).AddMonths(months);
            var lastDay = new DateTime(shifted.Year, shifted.Month,
                                       DateTime.DaysInMonth(shifted.Year, shifted.Month));
            return F.Number(M.Floor(dates.ToSerial(lastDay)));
        })

        .Numeric2("DAYS", (end, start) => F.Number(M.Floor(end) - M.Floor(start)))

        .Raw("DAYS360", 2, 3, r =>
        {
            var start = M.Floor(r.Number(0));
            var end = M.Floor(r.Number(1));
            var european = r.Boolean(2, false);
            if (r.HasError) return r.Error;

            var sign = 1;
            if (european && end < start) { (start, end) = (end, start); sign = -1; }
            return CellValue.Number(sign * Days360(dates.FromSerial(start), dates.FromSerial(end), european));
        })

        .Raw("DATEDIF", 3, 3, r =>
        {
            var start = dates.FromSerial(r.Number(0));
            var end = dates.FromSerial(r.Number(1));
            var unit = r.Text(2).ToUpperInvariant();
            if (r.HasError) return r.Error;
            if (end < start) return F.OutOfDomain();
            return DateDifference(start, end, unit);
        })

        .Raw("YEARFRAC", 2, 3, r =>
        {
            var start = dates.FromSerial(r.Number(0));
            var end = dates.FromSerial(r.Number(1));
            var basis = r.Integer(2, 0);
            if (r.HasError) return r.Error;
            return DayCount.YearFraction(start, end, basis) is { } fraction
                ? F.Number(fraction)
                : F.OutOfDomain();
        })

        .Raw("NETWORKDAYS", 2, 3, r => Workdays(r, count: true))
        .Raw("WORKDAY", 2, 3, r => Workdays(r, count: false))

        .Numeric1("DAYSINMONTH", s =>
        {
            var d = dates.FromSerial(s);
            return CellValue.Number(DateTime.DaysInMonth(d.Year, d.Month));
        })
        .Numeric1("DAYSINYEAR", s => CellValue.Number(DateTime.IsLeapYear(dates.FromSerial(s).Year) ? 366 : 365))
        .Numeric1("ISLEAPYEAR", s => CellValue.Boolean(DateTime.IsLeapYear(dates.FromSerial(s).Year)))
        .Numeric1("WEEKSINYEAR", s => CellValue.Number(WeeksInYear(dates.FromSerial(s).Year)))

        .Build();

    private static CellValue Clock(double serial, Func<double, double, double> selector)
    {
        if (serial < 0) return F.OutOfDomain();
        var seconds = SpreadsheetMath.ApproxValue((serial - M.Floor(serial)) * 86400);
        var whole = M.Floor(seconds);
        return CellValue.Number(selector(whole, seconds - whole));
    }

    private CellValue Component(double serial, Func<DateTime, int> selector)
    {
        if (serial < 0) return F.OutOfDomain();
        return CellValue.Number(selector(dates.FromSerial(serial)));
    }

    private static int IsoWeek(DateTime date) => ISOWeek.GetWeekOfYear(date);

    private static int WeeksInYear(int year) => ISOWeek.GetWeeksInYear(year);

    private static int Days360(DateTime start, DateTime end, bool european)
    {
        int startDay = start.Day, endDay = end.Day;

        if (startDay == 31) startDay = 30;
        else if (!european && start.Month == 2
                 && (startDay == 29 || (startDay == 28 && !DateTime.IsLeapYear(start.Year))))
            startDay = 30;

        if (endDay == 31 && (european || startDay == 30)) endDay = 30;

        return (end.Year - start.Year) * 360 + (end.Month - start.Month) * 30 + (endDay - startDay);
    }

    private static CellValue DateDifference(DateTime start, DateTime end, string unit)
    {
        var months = (end.Year - start.Year) * 12 + end.Month - start.Month;
        if (end.Day < start.Day) months--;

        return unit switch
        {
            "Y" => CellValue.Number(months / 12),
            "M" => CellValue.Number(months),
            "D" => CellValue.Number((int)(end.Date - start.Date).TotalDays),

            "MD" => CellValue.Number(start.Day <= end.Day
                ? end.Day - start.Day
                : (end.Date - RolledOver(end.Month == 1 ? end.Year - 1 : end.Year, end.Month == 1 ? 12 : end.Month - 1, start.Day)).Days),

            "YM" => CellValue.Number(months % 12),

            "YD" => CellValue.Number((end.Date - RolledOver(
                end.Month > start.Month || (end.Month == start.Month && end.Day >= start.Day) ? end.Year : end.Year - 1,
                start.Month, start.Day)).Days),
            _ => F.Invalid(),
        };
    }

    private static DateTime RolledOver(int year, int month, int day) =>
        new DateTime(year, month, 1).AddDays(day - 1);

    private readonly WorkWeek _weekend = new("0000011");

    private CellValue Workdays(ArgumentReader r, bool count) => workdays.Evaluate(r, count, _weekend, 2);
}
