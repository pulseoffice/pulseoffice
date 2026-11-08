using System.Globalization;
using PulseOffice.Sheets.Formatting;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions;

public sealed class NumericText(ISpreadsheetLocale locale)
{
    private const NumberStyles Styles =
        NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite |
        NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent;

    private static readonly string[] IsoDateFormats =
        ["yyyy-MM-dd", "yyyy-M-d", "yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ss"];

    private static readonly string[] TimeFormats = [@"h\:mm", @"h\:mm\:ss", @"hh\:mm", @"hh\:mm\:ss", @"h\:mm\:ss\.FFF"];

    private static readonly DateTime StandardEpoch = new(1899, 12, 30);

    private readonly ISpreadsheetLocale _locale = locale ?? throw new ArgumentNullException(nameof(locale));

    public CultureInfo Culture => _locale.Culture;

    public bool TryRead(string text, out double number)
    {
        ArgumentNullException.ThrowIfNull(text);
        var trimmed = text.Trim();
        var percent = trimmed.EndsWith('%');
        if (percent) trimmed = trimmed[..^1].TrimEnd();

        if (trimmed.Length == 0 || !TryReadPlain(trimmed, out number) || !double.IsFinite(number))
        {
            number = 0;
            return false;
        }

        if (percent) number /= 100;
        return true;
    }

    public bool TryReadGrouped(string text, out double number)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (TryRead(text, out number)) return true;
        var culture = Culture;
        if (culture.Equals(CultureInfo.InvariantCulture)) return false;
        var trimmed = text.Trim();
        var percent = trimmed.EndsWith('%');
        if (percent) trimmed = trimmed[..^1].TrimEnd();
        if (WithoutThousands(trimmed, culture.NumberFormat) is not { } ungrouped
            || !double.TryParse(ungrouped, Styles, culture, out number) || !double.IsFinite(number))
        {
            number = 0;
            return false;
        }

        if (percent) number /= 100;
        return true;
    }

    public bool TryReadValue(string text, out double number)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (TryRead(text, out number)) return true;
        var trimmed = text.Trim();
        if (TimeSpan.TryParseExact(trimmed, TimeFormats, CultureInfo.InvariantCulture, out var time) && time >= TimeSpan.Zero)
        {
            number = time.TotalDays;
            return true;
        }
        if (LooksLikeDate(trimmed) && TryReadDate(trimmed, out var moment))
        {
            number = (moment - StandardEpoch).TotalDays;
            return true;
        }
        number = 0;
        return false;
    }

    public bool TryReadDate(string text, out DateTime value)
    {
        ArgumentNullException.ThrowIfNull(text);
        var trimmed = text.Trim();
        return DateTime.TryParseExact(trimmed, IsoDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out value)
            || DateTime.TryParse(trimmed, Culture, DateTimeStyles.None, out value)
            || DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
    }

    private bool LooksLikeDate(string text) =>
        text.Count(character => character is '/' or '-' or '.') >= 2 || text.Any(char.IsLetter);

    private bool TryReadPlain(string text, out double number)
    {
        var culture = Culture;
        if (double.TryParse(text, Styles, culture, out number)) return true;
        return !AmbiguousWithInvariant(culture.NumberFormat)
            && double.TryParse(text, Styles, CultureInfo.InvariantCulture, out number);
    }

    private static string? WithoutThousands(string text, NumberFormatInfo format)
    {
        var separator = format.NumberGroupSeparator;
        if (separator.Length != 1) return null;
        char[] separators = char.IsWhiteSpace(separator[0]) ? [' ', '\u00A0', '\u202F'] : [separator[0]];

        var decimalAt = text.IndexOf(format.NumberDecimalSeparator, StringComparison.Ordinal);
        var integral = decimalAt < 0 ? text : text[..decimalAt];
        if (integral.IndexOfAny(['e', 'E']) >= 0) return null;
        var signLength = integral.Length > 0 && (integral[0] == '-' || integral[0] == '+') ? 1 : 0;
        var digits = integral[signLength..];
        if (digits.IndexOfAny(separators) < 0) return null;

        var groups = digits.Split(separators);
        if (groups[0].Length is < 1 or > 3 || !groups[0].All(char.IsAsciiDigit)) return null;
        if (groups.Skip(1).Any(group => group.Length != 3 || !group.All(char.IsAsciiDigit))) return null;
        return integral[..signLength] + string.Concat(groups) + (decimalAt < 0 ? string.Empty : text[decimalAt..]);
    }

    private bool AmbiguousWithInvariant(NumberFormatInfo format) =>
        format.NumberGroupSeparator == CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator;
}
