using System.Globalization;
using PulseOffice.Foundation.Units;

namespace PulseOffice.Formats.OpenXml;

public sealed class VmlStyle
{
    private readonly IReadOnlyList<(string Suffix, double Twips)> _units =
        [("pt", 20), ("in", 1440), ("cm", 1440 / 2.54), ("mm", 144 / 2.54), ("px", 15)];

    public Dictionary<string, string> Parse(string? style)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var declaration in (style ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var colon = declaration.IndexOf(':');
            if (colon > 0) values[declaration[..colon].Trim()] = declaration[(colon + 1)..].Trim();
        }

        return values;
    }

    public Twip? Length(IReadOnlyDictionary<string, string> style, string name)
    {
        if (!style.TryGetValue(name, out var text)) return null;
        foreach (var (suffix, twips) in _units)
            if (text.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
                && double.TryParse(text[..^suffix.Length], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                return new Twip((int)Math.Round(value * twips));
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var emu) ? new Twip((int)Math.Round(emu / 635)) : null;
    }

    public string Points(Twip length) => length.ToPoints().ToString("0.##", CultureInfo.InvariantCulture) + "pt";
}
