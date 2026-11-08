namespace PulseOffice.UI.Controls.Spectrum;

public readonly record struct RgbColor(byte Red, byte Green, byte Blue)
{
    public string Hex => $"#{Red:X2}{Green:X2}{Blue:X2}";
}

public readonly record struct SpectrumColor(double Hue, double Saturation, double Value)
{
    public static SpectrumColor From(RgbColor colour)
    {
        var r = colour.Red / 255.0;
        var g = colour.Green / 255.0;
        var b = colour.Blue / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        var hue = delta == 0 ? 0
            : max == r ? 60 * ((g - b) / delta % 6)
            : max == g ? 60 * ((b - r) / delta + 2)
            : 60 * ((r - g) / delta + 4);

        if (hue < 0) hue += 360;

        return new SpectrumColor(hue, max == 0 ? 0 : delta / max, max);
    }

    public RgbColor ToRgb()
    {
        var hue = (Finite(Hue) % 360 + 360) % 360;
        var saturation = Unit(Saturation);
        var value = Unit(Value);

        var chroma = value * saturation;
        var x = chroma * (1 - Math.Abs(hue / 60 % 2 - 1));
        var m = value - chroma;

        var (r, g, b) = (int)(hue / 60) switch
        {
            0 => (chroma, x, 0.0),
            1 => (x, chroma, 0.0),
            2 => (0.0, chroma, x),
            3 => (0.0, x, chroma),
            4 => (x, 0.0, chroma),
            _ => (chroma, 0.0, x),
        };

        return new RgbColor(Channel(r + m), Channel(g + m), Channel(b + m));
    }

    public SpectrumColor WithHue(double hue) => this with { Hue = Math.Clamp(Finite(hue), 0, 360) };

    public SpectrumColor WithShade(double saturation, double value) =>
        this with { Saturation = Unit(saturation), Value = Unit(value) };

    private static double Finite(double value) => double.IsFinite(value) ? value : 0;

    private static double Unit(double value) => Math.Clamp(Finite(value), 0, 1);

    private static byte Channel(double value) => (byte)Math.Round(Unit(value) * 255);
}
