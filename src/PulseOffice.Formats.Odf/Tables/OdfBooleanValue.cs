using System.Globalization;

namespace PulseOffice.Formats.Odf;

internal static class OdfBooleanValue
{
    public static bool Parse(string? text)
    {
        if (text is null) return false;
        var trimmed = text.Trim();
        if (trimmed.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
        if (trimmed.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
        return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && number != 0 && !double.IsNaN(number);
    }
}
