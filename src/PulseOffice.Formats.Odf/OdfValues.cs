using PulseOffice.Foundation.Colors;
using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.Odf;

internal sealed class OdfValues
{
    private const double LongestLength = 10_000;

    private const double LargestLineHeightPercent = 1_000;

    private const double LongestDurationSeconds = 3_600;

    public Twip Centimeters(XAttribute? attribute) => Measure(attribute?.Value) ?? Twip.Zero;

    public Twip Twips(string? value, double fallbackCentimeters) =>
        Measure(value) ?? Twip.FromCentimeters(fallbackCentimeters);

    public Twip? Measure(string? value) => new OdfLength().Parse(value);

    public DocumentRect Bounds(XElement element) => new(
        Centimeters(element.Attribute(OdfPackage.Namespaces.Svg + "x")),
        Centimeters(element.Attribute(OdfPackage.Namespaces.Svg + "y")),
        Centimeters(element.Attribute(OdfPackage.Namespaces.Svg + "width")),
        Centimeters(element.Attribute(OdfPackage.Namespaces.Svg + "height")));

    public double Points(string? value, double fallback = 18)
    {
        if (value is null) return fallback;
        var trimmed = value.Replace("pt", string.Empty, StringComparison.OrdinalIgnoreCase);
        return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var pt) && double.IsFinite(pt) ? pt : fallback;
    }

    public double Length(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;
        var (unit, factor) = value.EndsWith("cm", StringComparison.OrdinalIgnoreCase) ? ("cm", 72 / 2.54)
            : value.EndsWith("mm", StringComparison.OrdinalIgnoreCase) ? ("mm", 72 / 25.4)
            : value.EndsWith("in", StringComparison.OrdinalIgnoreCase) ? ("in", 72.0)
            : value.EndsWith("pt", StringComparison.OrdinalIgnoreCase) ? ("pt", 1.0)
            : (string.Empty, 1.0);
        return double.TryParse(value[..^unit.Length], NumberStyles.Float, CultureInfo.InvariantCulture, out var amount) && double.IsFinite(amount)
            ? Math.Round(Math.Clamp(amount * factor, -LongestLength, LongestLength), 2)
            : 0;
    }

    public uint? Color(string? value) =>
        ArgbColor.FromHashedRgbHex(value)?.Value;

    public double LineHeight(string? value) =>
        value is not null && value.EndsWith('%')
            && double.TryParse(value[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var percent) && percent > 0 && double.IsFinite(percent)
            ? Math.Min(percent, LargestLineHeightPercent) / 100.0
            : 1.0;

    public TextAlign Align(string? value, TextAlign fallback = TextAlign.Left) => value switch
    {
        "center" => TextAlign.Center,
        "end" or "right" => TextAlign.Right,
        "justify" => TextAlign.Justify,
        "start" or "left" => TextAlign.Left,
        _ => fallback,
    };

    public int Seconds(string? value, int fallback) =>
        value is not null
            && double.TryParse(value.TrimEnd('s', 'S'), NumberStyles.Float, CultureInfo.InvariantCulture, out var s)
            ? Milliseconds(s) ?? fallback
            : fallback;

    public int XmlDuration(string? value)
    {
        if (value is null || !value.StartsWith("PT", StringComparison.OrdinalIgnoreCase)) return 0;
        if (double.TryParse(value[2..].TrimEnd('s', 'S'), NumberStyles.Float, CultureInfo.InvariantCulture, out var s)) return Milliseconds(s) ?? 0;
        try
        {
            return Milliseconds(XmlConvert.ToTimeSpan(value).TotalSeconds) ?? 0;
        }
        catch (FormatException)
        {
            return 0;
        }
        catch (OverflowException)
        {
            return 0;
        }
    }

    private int? Milliseconds(double seconds) =>
        double.IsFinite(seconds) && seconds >= 0 ? (int)Math.Round(Math.Min(seconds, LongestDurationSeconds) * 1000) : null;

    public string MediaType(string href) => href[(href.LastIndexOf('.') + 1)..].ToLowerInvariant() switch
    {
        "jpg" or "jpeg" => "image/jpeg",
        "gif" => "image/gif",
        "bmp" => "image/bmp",
        "webp" => "image/webp",
        _ => "image/png",
    };
}
