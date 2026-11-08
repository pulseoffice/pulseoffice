using System.Globalization;

namespace PulseOffice.Presentations;

public enum HeaderFooterDateFormat
{
    ShortDate = 1,
    LongDate = 2,
    DayMonthYear = 3,
    MonthDayYear = 4,
    DayShortMonthShortYear = 5,
    MonthShortYear = 6,
    ShortMonthShortYear = 7,
    DateAndTime = 8,
    DateAndTimeWithSeconds = 9,
    Time24 = 10,
    Time24WithSeconds = 11,
    Time12 = 12,
    Time12WithSeconds = 13,
}

public enum DateFieldPartKind
{
    DayOfWeek,
    Day,
    Month,
    Year,
    Hours,
    Minutes,
    Seconds,
    AmPm,
    Literal,
}

public sealed record DateFieldPart(DateFieldPartKind Kind, bool Long = false, bool Textual = false, string Text = "")
{
    public string Signature => Kind == DateFieldPartKind.Literal ? string.Empty : $"{Kind}{(Long ? "+" : "")}{(Textual ? "*" : "")}";
}

public sealed record HeaderFooterDateFormatInfo(
    HeaderFooterDateFormat Format, string Key, string Pattern, bool AutomaticOrder, IReadOnlyList<DateFieldPart> Parts)
{
    public string PptxType => $"datetime{(int)Format}";

    public bool HasDate => Parts.Any(part => part.Kind is DateFieldPartKind.Day or DateFieldPartKind.Month or DateFieldPartKind.Year or DateFieldPartKind.DayOfWeek);

    public string Signature => string.Join(" ", Parts.Select(part => part.Signature).Where(signature => signature.Length > 0));
}

public interface IHeaderFooterDateFormats
{
    IReadOnlyList<HeaderFooterDateFormatInfo> All { get; }

    HeaderFooterDateFormatInfo For(HeaderFooterDateFormat format);

    string Render(HeaderFooterDateFormat format, DateTime moment, CultureInfo culture);

    HeaderFooterDateFormat? FromPptxType(string? type);

    HeaderFooterDateFormat? FromSignature(string signature);

    HeaderFooterDateFormat Nearest(IReadOnlyCollection<DateFieldPartKind> kinds, bool textualMonth, bool longMonth);
}

public sealed class HeaderFooterDateFormats : IHeaderFooterDateFormats
{
    private static readonly DateFieldPart Space = new(DateFieldPartKind.Literal, Text: " ");
    private static readonly DateFieldPart Slash = new(DateFieldPartKind.Literal, Text: "/");
    private static readonly DateFieldPart Dash = new(DateFieldPartKind.Literal, Text: "-");
    private static readonly DateFieldPart Colon = new(DateFieldPartKind.Literal, Text: ":");
    private static readonly DateFieldPart Comma = new(DateFieldPartKind.Literal, Text: ", ");
    private static readonly DateFieldPart Day = new(DateFieldPartKind.Day);
    private static readonly DateFieldPart NumericMonth = new(DateFieldPartKind.Month);
    private static readonly DateFieldPart LongMonth = new(DateFieldPartKind.Month, Long: true, Textual: true);
    private static readonly DateFieldPart ShortMonth = new(DateFieldPartKind.Month, Textual: true);
    private static readonly DateFieldPart LongYear = new(DateFieldPartKind.Year, Long: true);
    private static readonly DateFieldPart ShortYear = new(DateFieldPartKind.Year);
    private static readonly DateFieldPart Hours24 = new(DateFieldPartKind.Hours, Long: true);
    private static readonly DateFieldPart Hours12 = new(DateFieldPartKind.Hours);
    private static readonly DateFieldPart Minutes = new(DateFieldPartKind.Minutes, Long: true);
    private static readonly DateFieldPart Seconds = new(DateFieldPartKind.Seconds, Long: true);
    private static readonly DateFieldPart AmPm = new(DateFieldPartKind.AmPm);

    private readonly IReadOnlyList<HeaderFooterDateFormatInfo> _all =
    [
        new(HeaderFooterDateFormat.ShortDate, "shortDate", "d", true, [Day, Slash, NumericMonth, Slash, LongYear]),
        new(HeaderFooterDateFormat.LongDate, "longDate", "D", true,
            [new DateFieldPart(DateFieldPartKind.DayOfWeek, Long: true), Comma, Day, Space, LongMonth, Space, LongYear]),
        new(HeaderFooterDateFormat.DayMonthYear, "dayMonthYear", "d MMMM yyyy", false, [Day, Space, LongMonth, Space, LongYear]),
        new(HeaderFooterDateFormat.MonthDayYear, "monthDayYear", "MMMM d, yyyy", false, [LongMonth, Space, Day, Comma, LongYear]),
        new(HeaderFooterDateFormat.DayShortMonthShortYear, "dayShortMonth", "d-MMM-yy", false, [Day, Dash, ShortMonth, Dash, ShortYear]),
        new(HeaderFooterDateFormat.MonthShortYear, "monthYear", "MMMM yy", false, [LongMonth, Space, ShortYear]),
        new(HeaderFooterDateFormat.ShortMonthShortYear, "shortMonthYear", "MMM-yy", false, [ShortMonth, Dash, ShortYear]),
        new(HeaderFooterDateFormat.DateAndTime, "dateTime", "d HH:mm", true,
            [Day, Slash, NumericMonth, Slash, LongYear, Space, Hours24, Colon, Minutes]),
        new(HeaderFooterDateFormat.DateAndTimeWithSeconds, "dateTimeSeconds", "d HH:mm:ss", true,
            [Day, Slash, NumericMonth, Slash, LongYear, Space, Hours24, Colon, Minutes, Colon, Seconds]),
        new(HeaderFooterDateFormat.Time24, "time24", "HH:mm", false, [Hours24, Colon, Minutes]),
        new(HeaderFooterDateFormat.Time24WithSeconds, "time24Seconds", "HH:mm:ss", false, [Hours24, Colon, Minutes, Colon, Seconds]),
        new(HeaderFooterDateFormat.Time12, "time12", "h:mm tt", false, [Hours12, Colon, Minutes, Space, AmPm]),
        new(HeaderFooterDateFormat.Time12WithSeconds, "time12Seconds", "h:mm:ss tt", false, [Hours12, Colon, Minutes, Colon, Seconds, Space, AmPm]),
    ];

    public IReadOnlyList<HeaderFooterDateFormatInfo> All => _all;

    public HeaderFooterDateFormatInfo For(HeaderFooterDateFormat format) =>
        _all.FirstOrDefault(info => info.Format == format) ?? _all[0];

    public string Render(HeaderFooterDateFormat format, DateTime moment, CultureInfo culture)
    {
        var info = For(format);
        return info.Pattern switch
        {
            "d HH:mm" => moment.ToString("d", culture) + " " + moment.ToString("HH:mm", culture),
            "d HH:mm:ss" => moment.ToString("d", culture) + " " + moment.ToString("HH:mm:ss", culture),
            "h:mm tt" or "h:mm:ss tt" => moment.ToString(info.Pattern, TwelveHour(culture)),
            _ => moment.ToString(info.Pattern, culture),
        };
    }

    public HeaderFooterDateFormat? FromPptxType(string? type)
    {
        if (type is null || !type.StartsWith("datetime", StringComparison.Ordinal)) return null;
        var suffix = type["datetime".Length..];
        if (suffix.Length == 0) return HeaderFooterDateFormat.ShortDate;
        return int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && Enum.IsDefined((HeaderFooterDateFormat)number)
            ? (HeaderFooterDateFormat)number
            : HeaderFooterDateFormat.ShortDate;
    }

    public HeaderFooterDateFormat? FromSignature(string signature) =>
        _all.FirstOrDefault(info => info.Signature == signature)?.Format;

    public HeaderFooterDateFormat Nearest(IReadOnlyCollection<DateFieldPartKind> kinds, bool textualMonth, bool longMonth)
    {
        var hasDate = kinds.Contains(DateFieldPartKind.Day) || kinds.Contains(DateFieldPartKind.Month) || kinds.Contains(DateFieldPartKind.Year);
        var hasTime = kinds.Contains(DateFieldPartKind.Hours);
        var seconds = kinds.Contains(DateFieldPartKind.Seconds);
        if (!hasDate && hasTime)
            return kinds.Contains(DateFieldPartKind.AmPm)
                ? seconds ? HeaderFooterDateFormat.Time12WithSeconds : HeaderFooterDateFormat.Time12
                : seconds ? HeaderFooterDateFormat.Time24WithSeconds : HeaderFooterDateFormat.Time24;
        if (hasTime) return seconds ? HeaderFooterDateFormat.DateAndTimeWithSeconds : HeaderFooterDateFormat.DateAndTime;
        if (kinds.Contains(DateFieldPartKind.DayOfWeek)) return HeaderFooterDateFormat.LongDate;
        if (textualMonth && !kinds.Contains(DateFieldPartKind.Day)) return longMonth ? HeaderFooterDateFormat.MonthShortYear : HeaderFooterDateFormat.ShortMonthShortYear;
        if (textualMonth) return longMonth ? HeaderFooterDateFormat.DayMonthYear : HeaderFooterDateFormat.DayShortMonthShortYear;
        return HeaderFooterDateFormat.ShortDate;
    }

    private CultureInfo TwelveHour(CultureInfo culture)
    {
        if (culture.DateTimeFormat.AMDesignator.Length > 0) return culture;
        var clone = (CultureInfo)culture.Clone();
        clone.DateTimeFormat.AMDesignator = CultureInfo.InvariantCulture.DateTimeFormat.AMDesignator;
        clone.DateTimeFormat.PMDesignator = CultureInfo.InvariantCulture.DateTimeFormat.PMDesignator;
        return clone;
    }
}
