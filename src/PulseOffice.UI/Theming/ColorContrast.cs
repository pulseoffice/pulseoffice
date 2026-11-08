using Avalonia.Media;

namespace PulseOffice.UI.Theming;

public interface IColorContrast
{
    double Ratio(Color foreground, Color background);

    Color Readable(Color color, IReadOnlyCollection<Color> backgrounds, double target);
}

public sealed class WcagColorContrast : IColorContrast
{
    private const int Steps = 50;

    public double Ratio(Color foreground, Color background)
    {
        var first = Luminance(foreground);
        var second = Luminance(background);
        return (Math.Max(first, second) + 0.05) / (Math.Min(first, second) + 0.05);
    }

    public Color Readable(Color color, IReadOnlyCollection<Color> backgrounds, double target)
    {
        ArgumentNullException.ThrowIfNull(backgrounds);
        if (backgrounds.Count == 0) return color;

        var towardsWhite = backgrounds.Average(Luminance) < 0.18;
        var candidate = color;
        for (var step = 1; Weakest(candidate, backgrounds) < target && step <= Steps; step++)
            candidate = Mix(color, towardsWhite ? Colors.White : Colors.Black, (double)step / Steps);
        return candidate;
    }

    private double Weakest(Color color, IReadOnlyCollection<Color> backgrounds) =>
        backgrounds.Min(background => Ratio(color, background));

    private static Color Mix(Color color, Color target, double amount)
    {
        byte Channel(byte from, byte to) => (byte)Math.Round(from + (to - from) * amount);
        return Color.FromArgb(color.A, Channel(color.R, target.R), Channel(color.G, target.G), Channel(color.B, target.B));
    }

    private static double Luminance(Color color)
    {
        static double Linear(byte channel)
        {
            var value = channel / 255.0;
            return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);
    }
}

public readonly record struct AccentShades(Color Fill, Color Text);

public interface IAccentPalette
{
    AccentShades For(Color brand, bool dark);
}

public sealed class ReadableAccentPalette(IColorContrast contrast) : IAccentPalette
{
    public const double TextContrast = 4.5;

    public AccentShades For(Color brand, bool dark)
    {
        var backgrounds = Palette.Colors(dark);
        var surfaces = Palette.Backgrounds.Select(key => backgrounds[key]).ToList();
        return new AccentShades(
            contrast.Readable(brand, [Colors.White], TextContrast),
            contrast.Readable(brand, surfaces, TextContrast));
    }
}
