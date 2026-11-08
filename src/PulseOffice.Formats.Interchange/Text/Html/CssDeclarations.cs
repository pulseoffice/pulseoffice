using System.Globalization;

namespace PulseOffice.Formats.Interchange.Text.Html;

public interface ICssDeclarations
{
    IReadOnlyDictionary<string, string> Parse(string? style);

    double? Points(string? value, double emBase = 12);

    uint? Color(string? value);

    uint? Background(string? value);

    string? FontFamily(string? value);

    double? FontSize(string? value, double emBase = 12);
}

public sealed class CssDeclarations : ICssDeclarations
{
    private readonly Dictionary<string, uint> _named = new(StringComparer.OrdinalIgnoreCase)
    {
        ["black"] = 0xFF000000, ["white"] = 0xFFFFFFFF, ["red"] = 0xFFFF0000, ["green"] = 0xFF008000, ["blue"] = 0xFF0000FF,
        ["yellow"] = 0xFFFFFF00, ["gray"] = 0xFF808080, ["grey"] = 0xFF808080, ["silver"] = 0xFFC0C0C0, ["maroon"] = 0xFF800000,
        ["purple"] = 0xFF800080, ["fuchsia"] = 0xFFFF00FF, ["magenta"] = 0xFFFF00FF, ["lime"] = 0xFF00FF00, ["olive"] = 0xFF808000,
        ["navy"] = 0xFF000080, ["teal"] = 0xFF008080, ["aqua"] = 0xFF00FFFF, ["cyan"] = 0xFF00FFFF, ["orange"] = 0xFFFFA500,
    };

    private const double SmallestFontSize = 1;
    private const double LargestFontSize = 400;
    private const uint PaperThreshold = 0xF5;
    private const double LargestLength = 14400;

    private readonly HashSet<string> _genericFamilies = new(StringComparer.OrdinalIgnoreCase)
    {
        "serif", "sans-serif", "monospace", "cursive", "fantasy", "math", "emoji", "fangsong",
        "system-ui", "ui-serif", "ui-sans-serif", "ui-monospace", "ui-rounded", "-apple-system", "blinkmacsystemfont",
        "inherit", "initial", "unset", "revert", "revert-layer",
    };

    public uint? Background(string? value)
    {
        if (Color(value) is not { } color) return null;
        var paper = ((color >> 16) & 0xFF) >= PaperThreshold && ((color >> 8) & 0xFF) >= PaperThreshold && (color & 0xFF) >= PaperThreshold;
        return paper ? null : color;
    }

    public string? FontFamily(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        foreach (var candidate in value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var name = candidate.Trim('\'', '"').Trim();
            if (name.Length == 0 || name.Contains('(') || _genericFamilies.Contains(name)) continue;
            return name;
        }
        return null;
    }

    public double? FontSize(string? value, double emBase = 12) =>
        Points(value, emBase) is { } size && size >= SmallestFontSize && size <= LargestFontSize ? size : null;

    public IReadOnlyDictionary<string, string> Parse(string? style)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(style)) return result;
        foreach (var declaration in style.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(64))
        {
            var colon = declaration.IndexOf(':');
            if (colon <= 0) continue;
            var value = declaration[(colon + 1)..].Replace("!important", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
            result[declaration[..colon].Trim()] = value;
        }
        return result;
    }

    public double? Points(string? value, double emBase = 12)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim().ToLowerInvariant();
        (string Unit, double Factor)[] units = [("pt", 1), ("px", 0.75), ("cm", 72 / 2.54), ("mm", 72 / 25.4), ("in", 72), ("pc", 12), ("rem", 12), ("em", emBase), ("%", emBase / 100)];
        foreach (var (unit, factor) in units)
        {
            if (!value.EndsWith(unit, StringComparison.Ordinal)) continue;
            return double.TryParse(value[..^unit.Length], NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? Bounded(Math.Round(number * factor, 2)) : null;
        }
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var plain) ? Bounded(plain * 0.75) : null;
    }

    private double? Bounded(double points) => double.IsFinite(points) && Math.Abs(points) <= LargestLength ? points : null;

    public uint? Color(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();
        if (_named.TryGetValue(value, out var named)) return named;
        if (value.StartsWith('#'))
        {
            var hex = value[1..];
            if (hex.Length == 3) hex = string.Concat(hex.Select(c => new string(c, 2)));
            return hex.Length == 6 && uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb) ? 0xFF000000 | rgb : null;
        }
        if (value.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
        {
            var open = value.IndexOf('(');
            var close = value.IndexOf(')');
            if (open < 0 || close < open) return null;
            var parts = value[(open + 1)..close].Split([',', ' ', '/'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3) return null;
            if (parts.Length > 3 && Alpha(parts[3]) <= 0) return null;
            uint channel(string part) => part.EndsWith('%')
                ? (uint)Math.Clamp(Math.Round(double.TryParse(part[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var p) ? p * 2.55 : 0), 0, 255)
                : (uint)Math.Clamp(double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : 0, 0, 255);
            return 0xFF000000 | (channel(parts[0]) << 16) | (channel(parts[1]) << 8) | channel(parts[2]);
        }
        return null;
    }

    private static double Alpha(string part) =>
        part.EndsWith('%')
            ? double.TryParse(part[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var percent) ? percent / 100 : 1
            : double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var alpha) ? alpha : 1;
}
