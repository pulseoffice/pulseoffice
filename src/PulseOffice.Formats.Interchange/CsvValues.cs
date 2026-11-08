using System.Globalization;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Formats.Interchange;

public enum CsvFieldKind { Plain, Percent, Date, DateTime, Time }

public sealed record CsvField(CellValue Value, CsvFieldKind Kind = CsvFieldKind.Plain);

public interface ICsvValueReader
{
    CsvField Interpret(string field, CsvOptions options);
}

public sealed class CsvValueReader(ICsvNumberParser numbers) : ICsvValueReader
{
    private static readonly string[] DateFormats =
        ["yyyy-MM-dd", "yyyy/M/d", "d.M.yyyy", "d-M-yyyy"];

    private static readonly string[] DateTimeFormats =
        ["yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm"];

    private static readonly string[] TimeFormats = ["H:mm", "H:mm:ss"];

    private static readonly DateTime Epoch = new(1899, 12, 30);

    public CsvValueReader() : this(new CsvNumberParser())
    {
    }

    public CsvField Interpret(string field, CsvOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrEmpty(field)) return new(CellValue.Empty);
        if (!options.DetectTypes) return new(CellValue.Text(field));

        var trimmed = field.Trim();
        if (trimmed.Length == 0) return new(CellValue.Text(field));

        if (LeadingZero(trimmed)) return new(CellValue.Text(field));

        if (numbers.TryParse(trimmed, options.Culture, out var number))
            return new(CellValue.Number(number));

        if (trimmed.Length > 1 && trimmed.EndsWith('%')
            && numbers.TryParse(trimmed[..^1].TrimEnd(), options.Culture, out var scaled))
            return new(CellValue.Number(scaled / 100), CsvFieldKind.Percent);

        if (Moment(trimmed, options.Culture) is { } moment) return new(CellValue.Number(moment.Serial), moment.Kind);

        if (bool.TryParse(trimmed, out var flag)) return new(CellValue.Boolean(flag));

        return new(CellValue.Text(field));
    }

    private sealed record MomentPatterns(string[] Stamps, string[] Dates, string[] Times);

    private readonly System.Collections.Concurrent.ConcurrentDictionary<CultureInfo, MomentPatterns> _patterns = new();

    private MomentPatterns Patterns(CultureInfo culture) => _patterns.GetOrAdd(culture, chosen =>
    {
        var format = chosen.DateTimeFormat;
        var dates = DateFormats.Append("d/M/yyyy").Append(format.ShortDatePattern).Distinct().ToArray();
        var stamps = DateTimeFormats
            .Concat(dates.SelectMany(date => TimeFormats.Select(time => date + " " + time)))
            .Append(format.ShortDatePattern + " " + format.LongTimePattern)
            .Append(format.ShortDatePattern + " " + format.ShortTimePattern)
            .Distinct()
            .ToArray();
        return new MomentPatterns(stamps, dates, [.. TimeFormats, format.LongTimePattern]);
    });

    private (double Serial, CsvFieldKind Kind)? Moment(string text, CultureInfo culture)
    {
        if (!text.Any(char.IsAsciiDigit)) return null;
        var patterns = Patterns(culture);

        if (DateTime.TryParseExact(text, patterns.Stamps, culture, DateTimeStyles.None, out var stamp))
            return ((stamp - Epoch).TotalDays, CsvFieldKind.DateTime);

        if (DateTime.TryParseExact(text, patterns.Dates, culture, DateTimeStyles.None, out var moment))
            return ((moment - Epoch).TotalDays, CsvFieldKind.Date);

        if (DateTime.TryParseExact(text, patterns.Times, culture, DateTimeStyles.NoCurrentDateDefault, out var time))
            return (time.TimeOfDay.TotalDays, CsvFieldKind.Time);

        return null;
    }

    private static bool LeadingZero(string text) =>
        text.Length > 1 && text[0] == '0' && text.All(char.IsAsciiDigit);
}
