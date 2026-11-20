using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using A = DocumentFormat.OpenXml.Drawing;

namespace PulseOffice.Formats.OpenXml;

internal sealed class ChartColourScheme(IReadOnlyDictionary<string, string> colours)
{
    public string? Named(string name) => colours.TryGetValue(name, out var hex) ? hex : null;

    public string Accent(int index) => colours.TryGetValue($"accent{((index % 6) + 6) % 6 + 1}", out var hex) ? hex : "4472C4";

    public string Dark => Named("dk1") ?? "000000";

    public string Light => Named("lt1") ?? "FFFFFF";
}

internal sealed class ChartThemeColours
{
    private readonly IReadOnlyDictionary<string, string> _office = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["dk1"] = "000000", ["lt1"] = "FFFFFF", ["dk2"] = "44546A", ["lt2"] = "E7E6E6",
        ["accent1"] = "4472C4", ["accent2"] = "ED7D31", ["accent3"] = "A5A5A5",
        ["accent4"] = "FFC000", ["accent5"] = "5B9BD5", ["accent6"] = "70AD47",
        ["hlink"] = "0563C1", ["folHlink"] = "954F72",
    };

    public ChartColourScheme For(OpenXmlElement? element)
    {
        var root = element as OpenXmlPartRootElement ?? element?.Ancestors<OpenXmlPartRootElement>().FirstOrDefault();
        var part = root?.OpenXmlPart;
        var scheme = part is null ? null : Override(part) ?? Theme(part);
        if (scheme is null) return new ChartColourScheme(_office);

        var colours = new Dictionary<string, string>(_office, StringComparer.Ordinal);
        foreach (var child in scheme.ChildElements.OfType<A.Color2Type>())
            if (Hex(child) is { } hex) colours[child.LocalName] = hex;
        return new ChartColourScheme(colours);
    }

    private A.ColorScheme? Override(OpenXmlPart part) =>
        part.GetPartsOfType<ThemeOverridePart>().FirstOrDefault()?.ThemeOverride?.GetFirstChild<A.ColorScheme>();

    private A.ColorScheme? Theme(OpenXmlPart part)
    {
        var seen = new HashSet<OpenXmlPart>();
        var pending = new Queue<OpenXmlPart>();
        pending.Enqueue(part);
        while (pending.TryDequeue(out var current))
        {
            if (!seen.Add(current) || seen.Count > 16) continue;
            if (current.GetPartsOfType<ThemePart>().FirstOrDefault()?.Theme?.ThemeElements?.ColorScheme is { } scheme) return scheme;
            foreach (var parent in current.GetParentParts()) pending.Enqueue(parent);
        }
        return null;
    }

    private string? Hex(A.Color2Type colour) =>
        colour.RgbColorModelHex?.Val?.Value is { Length: 6 } hex ? hex.ToUpperInvariant()
        : colour.SystemColor?.LastColor?.Value is { Length: 6 } last ? last.ToUpperInvariant()
        : null;
}

internal sealed class ChartFillColour
{
    private readonly IReadOnlyDictionary<string, string> _aliases = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["tx1"] = "dk1", ["bg1"] = "lt1", ["tx2"] = "dk2", ["bg2"] = "lt2",
    };

    public string? Hex(OpenXmlElement? container, ChartColourScheme scheme)
    {
        ArgumentNullException.ThrowIfNull(scheme);
        if (container?.ChildElements.FirstOrDefault(child => child.LocalName is "srgbClr" or "schemeClr" or "sysClr" or "scrgbClr") is not { } colour)
            return null;

        var hex = colour.LocalName switch
        {
            "srgbClr" => Attribute(colour, "val"),
            "sysClr" => Attribute(colour, "lastClr"),
            "schemeClr" => Attribute(colour, "val") is { } name ? scheme.Named(_aliases.GetValueOrDefault(name, name)) : null,
            _ => Linear(colour),
        };
        if (hex is not { Length: 6 } || !hex.All(char.IsAsciiHexDigit)) return null;
        return "#" + Modified(hex.ToUpperInvariant(), colour);
    }

    public string Adjusted(string hex, double luminanceFactor, double luminanceOffset)
    {
        var (red, green, blue) = Components(hex);
        var (hue, saturation, lightness) = ToHsl(red, green, blue);
        lightness = Math.Clamp(lightness * luminanceFactor + luminanceOffset, 0, 1);
        return Text(FromHsl(hue, saturation, lightness));
    }

    private string Modified(string hex, OpenXmlElement colour)
    {
        var (red, green, blue) = Components(hex);
        foreach (var modifier in colour.ChildElements)
        {
            if (Attribute(modifier, "val") is not { } text
                || !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) continue;
            var amount = value / 100_000;
            switch (modifier.LocalName)
            {
                case "lumMod" or "lumOff":
                    var (hue, saturation, lightness) = ToHsl(red, green, blue);
                    lightness = Math.Clamp(modifier.LocalName == "lumMod" ? lightness * amount : lightness + amount, 0, 1);
                    (red, green, blue) = FromHsl(hue, saturation, lightness);
                    break;
                case "shade":
                    (red, green, blue) = (red * amount, green * amount, blue * amount);
                    break;
                case "tint":
                    (red, green, blue) = (1 - (1 - red) * amount, 1 - (1 - green) * amount, 1 - (1 - blue) * amount);
                    break;
            }
        }
        return Text((red, green, blue)).TrimStart('#');
    }

    private string? Linear(OpenXmlElement colour)
    {
        double Channel(string name) =>
            double.TryParse(Attribute(colour, name), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? Math.Clamp(value / 100_000, 0, 1) : 0;
        double Gamma(double linear) => linear <= 0.0031308 ? linear * 12.92 : 1.055 * Math.Pow(linear, 1 / 2.4) - 0.055;
        return Text((Gamma(Channel("r")), Gamma(Channel("g")), Gamma(Channel("b")))).TrimStart('#');
    }

    private string? Attribute(OpenXmlElement element, string name)
    {
        var value = element.GetAttributes().FirstOrDefault(attribute => attribute.LocalName == name).Value;
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private (double Red, double Green, double Blue) Components(string hex)
    {
        var value = int.Parse(hex.TrimStart('#'), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return (((value >> 16) & 0xFF) / 255.0, ((value >> 8) & 0xFF) / 255.0, (value & 0xFF) / 255.0);
    }

    private string Text((double Red, double Green, double Blue) colour)
    {
        int Byte(double channel) => (int)Math.Round(Math.Clamp(channel, 0, 1) * 255, MidpointRounding.AwayFromZero);
        return "#" + string.Create(CultureInfo.InvariantCulture, $"{Byte(colour.Red):X2}{Byte(colour.Green):X2}{Byte(colour.Blue):X2}");
    }

    private (double Hue, double Saturation, double Lightness) ToHsl(double red, double green, double blue)
    {
        var maximum = Math.Max(red, Math.Max(green, blue));
        var minimum = Math.Min(red, Math.Min(green, blue));
        var lightness = (maximum + minimum) / 2;
        var delta = maximum - minimum;
        if (delta < 1e-9) return (0, 0, lightness);
        var saturation = lightness > 0.5 ? delta / (2 - maximum - minimum) : delta / (maximum + minimum);
        var hue = maximum == red ? (green - blue) / delta + (green < blue ? 6 : 0)
            : maximum == green ? (blue - red) / delta + 2
            : (red - green) / delta + 4;
        return (hue / 6, saturation, lightness);
    }

    private (double Red, double Green, double Blue) FromHsl(double hue, double saturation, double lightness)
    {
        if (saturation < 1e-9) return (lightness, lightness, lightness);
        var upper = lightness < 0.5 ? lightness * (1 + saturation) : lightness + saturation - lightness * saturation;
        var lower = 2 * lightness - upper;
        double Channel(double offset)
        {
            var t = hue + offset;
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            return t < 1.0 / 6 ? lower + (upper - lower) * 6 * t
                : t < 0.5 ? upper
                : t < 2.0 / 3 ? lower + (upper - lower) * (2.0 / 3 - t) * 6
                : lower;
        }
        return (Channel(1.0 / 3), Channel(0), Channel(-1.0 / 3));
    }
}

internal sealed class ChartStyleColours(ChartFillColour fills)
{
    private const string Chart2010Namespace = "http://schemas.microsoft.com/office/drawing/2007/8/2/chart";
    private const string ChartNamespace = "http://schemas.openxmlformats.org/drawingml/2006/chart";

    private readonly (double Factor, double Offset)[] _variations =
    [
        (1, 0), (0.6, 0), (0.8, 0.2), (0.8, 0), (0.6, 0.4), (0.5, 0), (0.7, 0.3), (0.7, 0), (0.5, 0.5),
    ];

    public ChartStyleColours() : this(new ChartFillColour())
    {
    }

    public int Style(OpenXmlElement? space)
    {
        if (space is null) return 2;
        var styles = space.ChildElements
            .SelectMany(child => child.LocalName == "AlternateContent" ? child.Descendants() : new OpenXmlElement[] { child })
            .Where(element => element.LocalName == "style" && element.NamespaceUri is Chart2010Namespace or ChartNamespace)
            .Select(element => (Modern: element.NamespaceUri == Chart2010Namespace,
                Value: int.TryParse(element.GetAttributes().FirstOrDefault(attribute => attribute.LocalName == "val").Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0))
            .Where(style => style.Value > 0)
            .OrderByDescending(style => style.Modern)
            .ToList();
        if (styles.Count == 0) return 2;
        var chosen = styles[0].Value > 100 ? styles[0].Value - 100 : styles[0].Value;
        return chosen is >= 1 and <= 48 ? chosen : 2;
    }

    public string Automatic(int index, int count, int style, ChartColourScheme scheme)
    {
        ArgumentNullException.ThrowIfNull(scheme);
        var column = (Math.Clamp(style, 1, 48) - 1) % 8;
        var safeIndex = Math.Max(0, index);
        if (column == 1)
        {
            var (factor, offset) = _variations[(safeIndex / 6) % _variations.Length];
            var accent = "#" + scheme.Accent(safeIndex % 6);
            return factor == 1 && offset == 0 ? accent : fills.Adjusted(accent, factor, offset);
        }

        var spread = count <= 1 ? 0 : -0.5 + (double)Math.Min(safeIndex, count - 1) / (count - 1);
        if (column == 0) return fills.Adjusted("#808080", 1, spread * 0.6);
        var baseColour = "#" + scheme.Accent(column - 2);
        return spread < 0 ? fills.Adjusted(baseColour, 1 + spread, 0) : fills.Adjusted(baseColour, 1 - spread, spread);
    }
}
