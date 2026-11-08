using System.Globalization;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Operations;

public sealed record RecognizedInput(CellValue Value, string? FormatCode);

public interface ICellInputRecognizer
{
    RecognizedInput Recognize(string typed);
}

public sealed class CellInputRecognizer(ISpreadsheetLocale locale, ISystemDateCode systemDates) : ICellInputRecognizer
{
    public CellInputRecognizer(ISpreadsheetLocale locale) : this(locale, new SystemDateCode()) { }

    private const string Euro = "€";
    private const string EuroFormat = "#,##0.00 \"€\"";
    private const NumberStyles PlainStyles =
        NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite |
        NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent;
    private const NumberStyles GroupedStyles = PlainStyles | NumberStyles.AllowThousands;

    private static readonly DateTime Epoch = new(1899, 12, 30);
    private static readonly string[] IsoDates = ["yyyy-MM-dd", "yyyy-M-d"];
    private static readonly string[] Times = [@"h\:mm", @"hh\:mm"];
    private static readonly string[] TimesWithSeconds = [@"h\:mm\:ss", @"hh\:mm\:ss"];

    private static readonly Dictionary<string, (string True, string False)> LogicalNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["it"] = ("VERO", "FALSO"),
        ["de"] = ("WAHR", "FALSCH"),
        ["fr"] = ("VRAI", "FAUX"),
        ["es"] = ("VERDADERO", "FALSO"),
    };

    public CellInputRecognizer() : this(new SystemSpreadsheetLocale()) { }

    public RecognizedInput Recognize(string typed)
    {
        ArgumentNullException.ThrowIfNull(typed);
        if (typed.Length == 0) return new RecognizedInput(CellValue.Empty, null);

        var text = typed.Trim();
        var culture = locale.Culture;

        if (Logical(text, culture) is { } logical) return new RecognizedInput(CellValue.Boolean(logical), null);
        if (double.TryParse(text, PlainStyles, culture, out var plain) && double.IsFinite(plain))
            return new RecognizedInput(CellValue.Number(plain), null);

        return Grouped(text, culture)
            ?? Percent(text, culture)
            ?? Currency(text, culture)
            ?? Time(text)
            ?? Date(text, culture)
            ?? Moment(text, culture)
            ?? new RecognizedInput(CellValue.Text(typed), null);
    }

    private bool? Logical(string text, CultureInfo culture)
    {
        if (text.Equals("TRUE", StringComparison.OrdinalIgnoreCase)) return true;
        if (text.Equals("FALSE", StringComparison.OrdinalIgnoreCase)) return false;
        if (!LogicalNames.TryGetValue(culture.TwoLetterISOLanguageName, out var names)) return null;
        if (text.Equals(names.True, StringComparison.OrdinalIgnoreCase)) return true;
        if (text.Equals(names.False, StringComparison.OrdinalIgnoreCase)) return false;
        return null;
    }

    private RecognizedInput? Grouped(string text, CultureInfo culture)
    {
        var group = culture.NumberFormat.NumberGroupSeparator;
        if (group.Length == 0 || !text.Contains(group, StringComparison.Ordinal)) return null;
        if (!WellGrouped(text, culture)) return null;
        if (!double.TryParse(text, GroupedStyles, culture, out var number) || !double.IsFinite(number)) return null;
        var decimals = text.Contains(culture.NumberFormat.NumberDecimalSeparator, StringComparison.Ordinal);
        return new RecognizedInput(CellValue.Number(number), decimals ? "#,##0.00" : "#,##0");
    }

    private bool WellGrouped(string text, CultureInfo culture)
    {
        var integral = text.Split(culture.NumberFormat.NumberDecimalSeparator)[0].TrimStart('-', '+', ' ');
        var groups = integral.Split(culture.NumberFormat.NumberGroupSeparator);
        return groups[0].Length is >= 1 and <= 3 && groups.Skip(1).All(part => part.Length == 3 && part.All(char.IsAsciiDigit));
    }

    private RecognizedInput? Percent(string text, CultureInfo culture)
    {
        if (!text.EndsWith('%')) return null;
        var body = text[..^1].Trim();
        if (!double.TryParse(body, GroupedStyles, culture, out var number) || !double.IsFinite(number)) return null;
        var decimals = body.Contains(culture.NumberFormat.NumberDecimalSeparator, StringComparison.Ordinal);
        return new RecognizedInput(CellValue.Number(number / 100), decimals ? "0.00%" : "0%");
    }

    private RecognizedInput? Currency(string text, CultureInfo culture)
    {
        foreach (var symbol in new[] { culture.NumberFormat.CurrencySymbol, Euro }.Distinct())
        {
            if (symbol.Length == 0) continue;
            string body;
            if (text.StartsWith(symbol, StringComparison.Ordinal)) body = text[symbol.Length..];
            else if (text.EndsWith(symbol, StringComparison.Ordinal)) body = text[..^symbol.Length];
            else continue;

            if (!double.TryParse(body.Trim(), GroupedStyles, culture, out var number) || !double.IsFinite(number)) continue;
            return new RecognizedInput(CellValue.Number(number), symbol == Euro ? EuroFormat : $"\"{symbol}\" #,##0.00");
        }
        return null;
    }

    private RecognizedInput? Time(string text)
    {
        if (TimeSpan.TryParseExact(text, TimesWithSeconds, CultureInfo.InvariantCulture, out var withSeconds))
            return new RecognizedInput(CellValue.Number(withSeconds.TotalDays), "hh:mm:ss");
        if (TimeSpan.TryParseExact(text, Times, CultureInfo.InvariantCulture, out var time))
            return new RecognizedInput(CellValue.Number(time.TotalDays), "hh:mm");
        return null;
    }

    private RecognizedInput? Date(string text, CultureInfo culture)
    {
        if (DateTime.TryParseExact(text, IsoDates, CultureInfo.InvariantCulture, DateTimeStyles.None, out var iso))
            return new RecognizedInput(CellValue.Number((iso - Epoch).TotalDays), "yyyy-mm-dd");

        var monthFirst = MonthFirst(culture);
        foreach (var separator in new[] { '/', '-', '.' })
        {
            var parts = text.Split(separator);
            if (parts.Length != 3 || !parts.All(part => part.Length is > 0 and <= 4 && part.All(char.IsAsciiDigit))) continue;
            var first = int.Parse(parts[0], CultureInfo.InvariantCulture);
            var second = int.Parse(parts[1], CultureInfo.InvariantCulture);
            var year = int.Parse(parts[2], CultureInfo.InvariantCulture);
            if (parts[2].Length <= 2) year = culture.Calendar.ToFourDigitYear(year);
            var (day, month) = monthFirst ? (second, first) : (first, second);
            if (year is < 1 or > 9999 || month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year, month)) return null;
            var value = new DateTime(year, month, day);
            return new RecognizedInput(CellValue.Number((value - Epoch).TotalDays), systemDates.ShortDate(culture));
        }
        return null;
    }

    private RecognizedInput? Moment(string text, CultureInfo culture)
    {
        var parts = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || Date(parts[0], culture) is not { } day || Time(parts[1]) is not { } time) return null;
        var clock = time.FormatCode == "hh:mm" ? "hh:mm" : "hh:mm:ss";
        var order = day.FormatCode == "yyyy-mm-dd" ? "yyyy-mm-dd" : day.FormatCode;
        return new RecognizedInput(CellValue.Number(day.Value.AsNumber() + time.Value.AsNumber()), $"{order} {clock}");
    }

    private bool MonthFirst(CultureInfo culture)
    {
        var pattern = culture.DateTimeFormat.ShortDatePattern;
        var month = pattern.IndexOf('M', StringComparison.Ordinal);
        var day = pattern.IndexOf('d', StringComparison.Ordinal);
        return month >= 0 && day >= 0 && month < day;
    }
}
