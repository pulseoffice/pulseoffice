namespace PulseOffice.Drawing.Assets.Filters;

public sealed record ColorReplacementRule(uint Source, double Tolerance, uint? Target)
{
    public bool Matches(byte blue, byte green, byte red)
    {
        var limit = Math.Clamp(Tolerance, 0, 1) * 255;
        return Math.Abs(red - (int)((Source >> 16) & 0xFF)) <= limit
            && Math.Abs(green - (int)((Source >> 8) & 0xFF)) <= limit
            && Math.Abs(blue - (int)(Source & 0xFF)) <= limit;
    }
}

public sealed class ColorReplacementFilter(IReadOnlyList<ColorReplacementRule> rules) : IPixelFilter
{
    public int Replaced { get; private set; }

    public void Apply(BgraImage image)
    {
        ArgumentNullException.ThrowIfNull(image);

        var pixels = image.Pixels;
        Replaced = 0;
        for (var i = 0; i + 3 < pixels.Length; i += 4)
        {
            if (pixels[i + 3] == 0) continue;
            var opacity = pixels[i + 3] / 255.0;
            var (blue, green, red) = (Straight(pixels[i], opacity), Straight(pixels[i + 1], opacity), Straight(pixels[i + 2], opacity));
            if (rules.FirstOrDefault(rule => rule.Matches(blue, green, red)) is not { } rule) continue;

            Replaced++;
            if (rule.Target is not { } target)
            {
                pixels[i] = pixels[i + 1] = pixels[i + 2] = pixels[i + 3] = 0;
                continue;
            }

            pixels[i] = (byte)Math.Round((target & 0xFF) * opacity);
            pixels[i + 1] = (byte)Math.Round(((target >> 8) & 0xFF) * opacity);
            pixels[i + 2] = (byte)Math.Round(((target >> 16) & 0xFF) * opacity);
        }
    }

    private byte Straight(byte premultiplied, double opacity) =>
        (byte)Math.Clamp(Math.Round(premultiplied / opacity), 0, 255);
}
