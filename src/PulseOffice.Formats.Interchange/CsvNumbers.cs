using System.Globalization;

namespace PulseOffice.Formats.Interchange;

public interface ICsvNumberParser
{
    bool TryParse(string text, CultureInfo culture, out double number);
}

public sealed class CsvNumberParser : ICsvNumberParser
{
    public bool TryParse(string text, CultureInfo culture, out double number)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(culture);

        number = 0;

        if (!text.Any(char.IsAsciiDigit)) return false;
        if (!GroupsAreValid(text, culture.NumberFormat)) return false;

        if (!double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, culture, out var parsed))
            return false;

        if (!double.IsFinite(parsed)) return false;

        number = parsed;
        return true;
    }

    private static bool GroupsAreValid(string text, NumberFormatInfo format)
    {
        var group = format.NumberGroupSeparator;
        if (group.Length == 0 || !text.Contains(group, StringComparison.Ordinal)) return true;

        var decimalAt = text.IndexOf(format.NumberDecimalSeparator, StringComparison.Ordinal);
        var integer = decimalAt >= 0 ? text[..decimalAt] : text;

        if (decimalAt >= 0 && text[decimalAt..].Contains(group, StringComparison.Ordinal)) return false;

        var exponent = integer.IndexOfAny(['e', 'E']);
        if (exponent >= 0) integer = integer[..exponent];

        if (!integer.Contains(group, StringComparison.Ordinal)) return true;

        var parts = integer.TrimStart('+', '-', ' ').Split(group);

        if (parts[0].Length is 0 or > 3) return false;

        return parts.Skip(1).All(part => part.Length == 3 && part.All(char.IsAsciiDigit));
    }
}
