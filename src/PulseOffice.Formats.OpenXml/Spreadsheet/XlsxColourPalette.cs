using System.Globalization;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using A = DocumentFormat.OpenXml.Drawing;

namespace PulseOffice.Formats.OpenXml;

internal sealed class XlsxColourPalette(IReadOnlyList<string> theme, IReadOnlyList<string> indexed)
{
    public IReadOnlyList<string> Theme => theme;

    public IReadOnlyList<string> Indexed => indexed;

    public string? Resolve(string? rgb, uint? themeIndex, uint? indexedIndex, double tint)
    {
        var baseHex = rgb is { Length: 8 } && rgb.All(char.IsAsciiHexDigit) ? rgb[2..].ToUpperInvariant()
            : rgb is { Length: 6 } && rgb.All(char.IsAsciiHexDigit) ? rgb.ToUpperInvariant()
            : themeIndex is { } t && t < theme.Count ? theme[(int)t]
            : indexedIndex is { } i && i < indexed.Count ? indexed[(int)i]
            : indexedIndex is 64 ? "000000"
            : indexedIndex is 65 ? "FFFFFF"
            : null;
        return baseHex is null ? null : "#" + Tinted(baseHex, tint);
    }

    private static string Tinted(string hex, double tint)
    {
        if (tint == 0 || !double.IsFinite(tint)) return hex;
        var value = int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var (h, l, s) = ToHls(((value >> 16) & 0xFF) / 255.0, ((value >> 8) & 0xFF) / 255.0, (value & 0xFF) / 255.0);
        var clamped = Math.Clamp(tint, -1, 1);
        l = clamped < 0 ? l * (1 + clamped) : l * (1 - clamped) + clamped;
        var (r, g, b) = FromHls(h, l, s);
        return $"{Channel(r):X2}{Channel(g):X2}{Channel(b):X2}";
    }

    private static int Channel(double value) => (int)Math.Round(Math.Clamp(value, 0, 1) * 255);

    private static (double H, double L, double S) ToHls(double r, double g, double b)
    {
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var l = (max + min) / 2;
        if (max == min) return (0, l, 0);
        var d = max - min;
        var s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
        var h = max == r ? (g - b) / d + (g < b ? 6 : 0) : max == g ? (b - r) / d + 2 : (r - g) / d + 4;
        return (h / 6, l, s);
    }

    private static (double R, double G, double B) FromHls(double h, double l, double s)
    {
        if (s == 0) return (l, l, l);
        var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
        var p = 2 * l - q;
        return (Hue(p, q, h + 1.0 / 3), Hue(p, q, h), Hue(p, q, h - 1.0 / 3));
    }

    private static double Hue(double p, double q, double t)
    {
        if (t < 0) t += 1;
        if (t > 1) t -= 1;
        return t < 1.0 / 6 ? p + (q - p) * 6 * t
            : t < 0.5 ? q
            : t < 2.0 / 3 ? p + (q - p) * (2.0 / 3 - t) * 6
            : p;
    }
}

internal sealed class XlsxColourPalettes
{
    private readonly string[] _theme =
        ["FFFFFF", "000000", "E7E6E6", "44546A", "4472C4", "ED7D31", "A5A5A5", "FFC000", "5B9BD5", "70AD47", "0563C1", "954F72"];

    private readonly string[] _indexed =
    [
        "000000", "FFFFFF", "FF0000", "00FF00", "0000FF", "FFFF00", "FF00FF", "00FFFF",
        "000000", "FFFFFF", "FF0000", "00FF00", "0000FF", "FFFF00", "FF00FF", "00FFFF",
        "800000", "008000", "000080", "808000", "800080", "008080", "C0C0C0", "808080",
        "9999FF", "993366", "FFFFCC", "CCFFFF", "660066", "FF8080", "0066CC", "CCCCFF",
        "000080", "FF00FF", "FFFF00", "00FFFF", "800080", "800000", "008080", "0000FF",
        "00CCFF", "CCFFFF", "CCFFCC", "FFFF99", "99CCFF", "FF99CC", "CC99FF", "FFCC99",
        "3366FF", "33CCCC", "99CC00", "FFCC00", "FF9900", "FF6600", "666699", "969696",
        "003366", "339966", "003300", "333300", "993300", "993366", "333399", "333333",
    ];

    public XlsxColourPalette Default => new(_theme, _indexed);

    public XlsxColourPalette From(Stylesheet? stylesheet)
    {
        var indexed = stylesheet?.Colors?.IndexedColors?.Elements<RgbColor>()
            .Select(colour => colour.Rgb?.Value is { Length: 8 } argb ? argb[2..].ToUpperInvariant() : "000000").ToArray();
        var workbook = stylesheet?.WorkbookStylesPart?.GetParentParts().OfType<WorkbookPart>().FirstOrDefault();
        var scheme = workbook?.ThemePart?.Theme?.ThemeElements?.ColorScheme;
        return new XlsxColourPalette(scheme is null ? _theme : Theme(scheme), indexed is { Length: > 0 } ? indexed : _indexed);
    }

    private string[] Theme(A.ColorScheme scheme) =>
    [
        Hex(scheme.Light1Color, _theme[0]), Hex(scheme.Dark1Color, _theme[1]), Hex(scheme.Light2Color, _theme[2]), Hex(scheme.Dark2Color, _theme[3]),
        Hex(scheme.Accent1Color, _theme[4]), Hex(scheme.Accent2Color, _theme[5]), Hex(scheme.Accent3Color, _theme[6]),
        Hex(scheme.Accent4Color, _theme[7]), Hex(scheme.Accent5Color, _theme[8]), Hex(scheme.Accent6Color, _theme[9]),
        Hex(scheme.Hyperlink, _theme[10]), Hex(scheme.FollowedHyperlinkColor, _theme[11]),
    ];

    private string Hex(A.Color2Type? colour, string fallback) =>
        colour?.RgbColorModelHex?.Val?.Value is { Length: 6 } hex ? hex.ToUpperInvariant()
        : colour?.SystemColor?.LastColor?.Value is { Length: 6 } last ? last.ToUpperInvariant()
        : fallback;

    public string? Read(XlsxColourPalette palette, ColorType? colour) =>
        colour is null ? null
        : palette.Resolve(colour.Rgb?.Value, colour.Theme?.Value, colour.Indexed?.Value, colour.Tint?.Value ?? 0);

    public string? Read(XlsxColourPalette palette, XElement? colour) =>
        colour is null ? null
        : palette.Resolve((string?)colour.Attribute("rgb"), Unsigned(colour.Attribute("theme")), Unsigned(colour.Attribute("indexed")),
            double.TryParse((string?)colour.Attribute("tint"), NumberStyles.Float, CultureInfo.InvariantCulture, out var tint) ? tint : 0);

    private uint? Unsigned(XAttribute? attribute) =>
        uint.TryParse((string?)attribute, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
}
