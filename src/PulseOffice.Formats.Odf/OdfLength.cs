using System.Globalization;
using PulseOffice.Foundation.Units;

namespace PulseOffice.Formats.Odf;

internal sealed class OdfLength
{
    private const double LargestCentimetres = 100_000;

    public Twip? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var text = value.Trim();
        var unitStart = text.Length;
        while (unitStart > 0 && char.IsLetter(text[unitStart - 1])) unitStart--;
        if (!double.TryParse(text[..unitStart], NumberStyles.Float, CultureInfo.InvariantCulture, out var number)) return null;

        if (!double.IsFinite(number)) return null;

        var points = LargestCentimetres / 2.54 * 72;
        return text[unitStart..].ToLowerInvariant() switch
        {
            "cm" or "" => Twip.FromCentimeters(Math.Clamp(number, -LargestCentimetres, LargestCentimetres)),
            "mm" => Twip.FromMillimeters(Math.Clamp(number, -LargestCentimetres * 10, LargestCentimetres * 10)),
            "in" => Twip.FromInches(Math.Clamp(number, -LargestCentimetres / 2.54, LargestCentimetres / 2.54)),
            "pt" => Twip.FromPoints(Math.Clamp(number, -points, points)),
            "pc" => Twip.FromPoints(Math.Clamp(number * 12, -points, points)),
            "px" => Twip.FromPoints(Math.Clamp(number * 0.75, -points, points)),
            _ => null,
        };
    }

    public Twip Parse(string? value, Twip fallback) => Parse(value) ?? fallback;
}
