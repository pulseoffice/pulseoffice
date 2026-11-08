using System.Globalization;

namespace PulseOffice.UI.Commands;

public interface IRibbonPaletteBuilder
{
    IReadOnlyList<string> Standard { get; }

    IReadOnlyList<string> OfficeTheme { get; }

    RibbonPalette Build(IReadOnlyList<string> themeColours);

    string Shade(string hex, double amount);
}

public sealed class RibbonPaletteBuilder : IRibbonPaletteBuilder
{
    private static readonly double[] Steps = [0.8, 0.6, 0.4, -0.25, -0.5];

    public IReadOnlyList<string> Standard { get; } =
    [
        "#C00000", "#FF0000", "#FFC000", "#FFFF00", "#92D050",
        "#00B050", "#00B0F0", "#0070C0", "#002060", "#7030A0",
    ];

    public IReadOnlyList<string> OfficeTheme { get; } =
    [
        "#FFFFFF", "#000000", "#E7E6E6", "#44546A", "#4472C4",
        "#ED7D31", "#A5A5A5", "#FFC000", "#5B9BD5", "#70AD47",
    ];

    public RibbonPalette Build(IReadOnlyList<string> themeColours)
    {
        ArgumentNullException.ThrowIfNull(themeColours);

        var columns = themeColours
            .Select(Normalise)
            .OfType<string>()
            .Select(colour => (IReadOnlyList<string>)[colour, .. Steps.Select(step => Shade(colour, step))])
            .ToList();

        return new RibbonPalette(columns, Standard);
    }

    public string Shade(string hex, double amount)
    {
        if (Normalise(hex) is not { } colour) return hex;

        var channels = Enumerable.Range(0, 3)
            .Select(index => int.Parse(colour.AsSpan(1 + index * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture))
            .Select(value => amount >= 0
                ? value + (255 - value) * amount
                : value * (1 + amount))
            .Select(value => (int)Math.Round(Math.Clamp(value, 0, 255)));

        return "#" + string.Concat(channels.Select(value => value.ToString("X2", CultureInfo.InvariantCulture)));
    }

    private string? Normalise(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return null;

        var text = hex.Trim().TrimStart('#');

        if (text.Length == 8) text = text[2..];

        return text.Length == 6 && text.All(Uri.IsHexDigit) ? "#" + text.ToUpperInvariant() : null;
    }
}
