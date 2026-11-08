namespace PulseOffice.Drawing.Assets.Filters;

public sealed record BgraImage(byte[] Pixels, int Width, int Height);

public interface IPixelFilter
{
    void Apply(BgraImage image);
}

public sealed class IdentityFilter : IPixelFilter
{
    public void Apply(BgraImage image) { }
}

public abstract class PerPixelFilter : IPixelFilter
{
    public void Apply(BgraImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        var pixels = image.Pixels;
        for (var i = 0; i + 3 < pixels.Length; i += 4)
        {
            var alpha = pixels[i + 3];
            if (alpha == 0) continue;

            var (blue, green, red) = Transform(Straight(pixels[i], alpha), Straight(pixels[i + 1], alpha), Straight(pixels[i + 2], alpha));
            pixels[i] = Premultiplied(blue, alpha);
            pixels[i + 1] = Premultiplied(green, alpha);
            pixels[i + 2] = Premultiplied(red, alpha);
        }
    }

    protected abstract (byte Blue, byte Green, byte Red) Transform(byte blue, byte green, byte red);

    private byte Straight(byte value, byte alpha) => alpha == 255 ? value : (byte)Math.Min(255, (value * 255 + alpha / 2) / alpha);

    private byte Premultiplied(byte value, byte alpha) => alpha == 255 ? value : (byte)((value * alpha + 127) / 255);

    protected byte Channel(double value) => (byte)Math.Clamp(value, 0, 255);
}

public sealed class GrayscaleFilter : PerPixelFilter
{
    protected override (byte Blue, byte Green, byte Red) Transform(byte blue, byte green, byte red)
    {
        var luminance = Channel(0.114 * blue + 0.587 * green + 0.299 * red);
        return (luminance, luminance, luminance);
    }
}

public sealed class DuotoneFilter : PerPixelFilter
{
    private readonly (double Red, double Green, double Blue) _dark;
    private readonly (double Red, double Green, double Blue) _light;

    public DuotoneFilter(ImageDuotone duotone)
    {
        ArgumentNullException.ThrowIfNull(duotone);
        _dark = Rgb(duotone.DarkHex);
        _light = Rgb(duotone.LightHex);
    }

    protected override (byte Blue, byte Green, byte Red) Transform(byte blue, byte green, byte red)
    {
        var amount = (0.114 * blue + 0.587 * green + 0.299 * red) / 255;
        return (Channel(Mix(_dark.Blue, _light.Blue, amount)), Channel(Mix(_dark.Green, _light.Green, amount)), Channel(Mix(_dark.Red, _light.Red, amount)));
    }

    private double Mix(double from, double to, double amount) => from + (to - from) * amount;

    private (double Red, double Green, double Blue) Rgb(string hex)
    {
        var digits = hex.TrimStart('#');
        var value = digits.Length >= 6 && uint.TryParse(digits[^6..], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : 0u;
        return ((value >> 16) & 0xFF, (value >> 8) & 0xFF, value & 0xFF);
    }
}

public sealed class ThresholdFilter(byte threshold = 160) : PerPixelFilter
{
    protected override (byte Blue, byte Green, byte Red) Transform(byte blue, byte green, byte red)
    {
        var value = 0.114 * blue + 0.587 * green + 0.299 * red >= threshold ? (byte)255 : (byte)0;
        return (value, value, value);
    }
}

public sealed class SepiaFilter : PerPixelFilter
{
    protected override (byte Blue, byte Green, byte Red) Transform(byte blue, byte green, byte red) =>
        (Channel(0.272 * red + 0.534 * green + 0.131 * blue),
         Channel(0.349 * red + 0.686 * green + 0.168 * blue),
         Channel(0.393 * red + 0.769 * green + 0.189 * blue));
}

public sealed class InvertFilter : PerPixelFilter
{
    protected override (byte Blue, byte Green, byte Red) Transform(byte blue, byte green, byte red) =>
        ((byte)(255 - blue), (byte)(255 - green), (byte)(255 - red));
}

public sealed class MosaicFilter : IPixelFilter
{
    public void Apply(BgraImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image.Width <= 0 || image.Height <= 0) return;

        var block = Math.Max(4, Math.Min(image.Width, image.Height) / 24);
        for (var top = 0; top < image.Height; top += block)
            for (var left = 0; left < image.Width; left += block)
                new Tile(left, top, Math.Min(left + block, image.Width), Math.Min(top + block, image.Height)).Flatten(image);
    }

    private readonly record struct Tile(int Left, int Top, int Right, int Bottom)
    {
        private IEnumerable<int> Offsets(int width)
        {
            for (var y = Top; y < Bottom; y++)
                for (var x = Left; x < Right; x++)
                    yield return (y * width + x) * 4;
        }

        public void Flatten(BgraImage image)
        {
            var offsets = Offsets(image.Width).ToList();
            if (offsets.Count == 0) return;

            var average = new byte[4];
            for (var channel = 0; channel < 4; channel++)
                average[channel] = (byte)(offsets.Sum(offset => (long)image.Pixels[offset + channel]) / offsets.Count);

            foreach (var offset in offsets)
                Array.Copy(average, 0, image.Pixels, offset, 4);
        }
    }
}

public sealed class PosterizeFilter : PerPixelFilter
{
    private const int Levels = 4;

    protected override (byte Blue, byte Green, byte Red) Transform(byte blue, byte green, byte red) =>
        (Quantize(blue), Quantize(green), Quantize(red));

    private byte Quantize(byte value)
    {
        var step = 255.0 / (Levels - 1);
        return Channel(Math.Round(value / step) * step);
    }
}

public sealed class SolarizeFilter : PerPixelFilter
{
    private const byte Threshold = 128;

    protected override (byte Blue, byte Green, byte Red) Transform(byte blue, byte green, byte red) =>
        (Flip(blue), Flip(green), Flip(red));

    private byte Flip(byte value) => value >= Threshold ? (byte)(255 - value) : value;
}

public sealed class PopArtFilter : PerPixelFilter
{
    private static readonly (byte Blue, byte Green, byte Red)[] Palette =
    [
        (0x5A, 0x1E, 0x14), (0x8C, 0x28, 0xE6), (0x00, 0xC8, 0xFF), (0xE6, 0xB4, 0x2C), (0xFF, 0xFF, 0xFF),
    ];

    protected override (byte Blue, byte Green, byte Red) Transform(byte blue, byte green, byte red)
    {
        var luminance = 0.114 * blue + 0.587 * green + 0.299 * red;
        var index = Math.Min(Palette.Length - 1, (int)(luminance / 256 * Palette.Length));
        return Palette[index];
    }
}

public abstract class NeighbourhoodFilter : IPixelFilter
{
    public void Apply(BgraImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image.Width < 3 || image.Height < 3 || (long)image.Width * image.Height * 4 > image.Pixels.Length) return;

        var source = (byte[])image.Pixels.Clone();
        for (var y = 0; y < image.Height; y++)
            for (var x = 0; x < image.Width; x++)
            {
                var offset = (y * image.Width + x) * 4;
                var alpha = source[offset + 3];
                for (var channel = 0; channel < 3; channel++)
                    image.Pixels[offset + channel] = Math.Min(alpha, Channel(Compute(new Neighbourhood(source, image.Width, image.Height, x, y, channel))));
            }
    }

    protected abstract double Compute(Neighbourhood around);

    private byte Channel(double value) => (byte)Math.Clamp(Math.Round(value), 0, 255);
}

public readonly struct Neighbourhood(byte[] pixels, int width, int height, int x, int y, int channel)
{
    public double At(int dx, int dy)
    {
        var px = Math.Clamp(x + dx, 0, width - 1);
        var py = Math.Clamp(y + dy, 0, height - 1);
        return pixels[(py * width + px) * 4 + channel];
    }

    public double Luminance(int dx, int dy)
    {
        var px = Math.Clamp(x + dx, 0, width - 1);
        var py = Math.Clamp(y + dy, 0, height - 1);
        var offset = (py * width + px) * 4;
        return 0.114 * pixels[offset] + 0.587 * pixels[offset + 1] + 0.299 * pixels[offset + 2];
    }

    public void Window(Span<double> window)
    {
        var index = 0;
        for (var dy = -1; dy <= 1; dy++)
            for (var dx = -1; dx <= 1; dx++)
                window[index++] = At(dx, dy);
    }
}

public abstract class KernelFilter : NeighbourhoodFilter
{
    protected abstract double[,] Kernel { get; }

    protected virtual double Bias => 0;

    protected override double Compute(Neighbourhood around)
    {
        var sum = 0.0;
        for (var dy = -1; dy <= 1; dy++)
            for (var dx = -1; dx <= 1; dx++)
                sum += Kernel[dy + 1, dx + 1] * around.At(dx, dy);
        return sum + Bias;
    }
}

public sealed class SmoothFilter : KernelFilter
{
    protected override double[,] Kernel { get; } =
        { { 1 / 16.0, 2 / 16.0, 1 / 16.0 }, { 2 / 16.0, 4 / 16.0, 2 / 16.0 }, { 1 / 16.0, 2 / 16.0, 1 / 16.0 } };
}

public sealed class SharpenFilter : KernelFilter
{
    protected override double[,] Kernel { get; } = { { 0, -1, 0 }, { -1, 5, -1 }, { 0, -1, 0 } };
}

public sealed class ReliefFilter : NeighbourhoodFilter
{
    protected override double Compute(Neighbourhood around) =>
        128 + around.Luminance(1, 1) - around.Luminance(-1, -1);
}

public sealed class RemoveNoiseFilter : NeighbourhoodFilter
{
    private const int WindowSize = 9;

    protected override double Compute(Neighbourhood around)
    {
        Span<double> window = stackalloc double[WindowSize];
        around.Window(window);
        window.Sort();
        return window[WindowSize / 2];
    }
}

public sealed class CharcoalFilter : NeighbourhoodFilter
{
    protected override double Compute(Neighbourhood around)
    {
        var gx = around.Luminance(1, -1) + 2 * around.Luminance(1, 0) + around.Luminance(1, 1)
            - around.Luminance(-1, -1) - 2 * around.Luminance(-1, 0) - around.Luminance(-1, 1);
        var gy = around.Luminance(-1, 1) + 2 * around.Luminance(0, 1) + around.Luminance(1, 1)
            - around.Luminance(-1, -1) - 2 * around.Luminance(0, -1) - around.Luminance(1, -1);
        return 255 - Math.Sqrt(gx * gx + gy * gy);
    }
}

public interface IPixelFilterCatalog
{
    IPixelFilter For(ImageFilter filter);

    IPixelFilter Duotone(ImageDuotone duotone);
}

public sealed class PixelFilterCatalog : IPixelFilterCatalog
{
    public IPixelFilter Duotone(ImageDuotone duotone) => new DuotoneFilter(duotone);

    private readonly IReadOnlyDictionary<ImageFilter, IPixelFilter> _filters = new Dictionary<ImageFilter, IPixelFilter>
    {
        [ImageFilter.Grayscale] = new GrayscaleFilter(),
        [ImageFilter.Sepia] = new SepiaFilter(),
        [ImageFilter.Invert] = new InvertFilter(),
        [ImageFilter.Mosaic] = new MosaicFilter(),
        [ImageFilter.Charcoal] = new CharcoalFilter(),
        [ImageFilter.PopArt] = new PopArtFilter(),
        [ImageFilter.Posterize] = new PosterizeFilter(),
        [ImageFilter.Relief] = new ReliefFilter(),
        [ImageFilter.RemoveNoise] = new RemoveNoiseFilter(),
        [ImageFilter.Sharpen] = new SharpenFilter(),
        [ImageFilter.Smooth] = new SmoothFilter(),
        [ImageFilter.Solarize] = new SolarizeFilter(),
    };

    private readonly IPixelFilter _identity;

    public PixelFilterCatalog(IPixelFilter? identity = null) => _identity = identity ?? new IdentityFilter();

    public IPixelFilter For(ImageFilter filter) => _filters.GetValueOrDefault(filter, _identity);
}

public sealed record ColorAdjustment(
    double Brightness = 0, double Contrast = 0, double Red = 0, double Green = 0, double Blue = 0, double Gamma = 1)
{
    public bool IsIdentity => Brightness == 0 && Contrast == 0 && Red == 0 && Green == 0 && Blue == 0 && Math.Abs(Gamma - 1) < 1e-9;
}

public sealed class ColorAdjustmentFilter(ColorAdjustment adjustment) : PerPixelFilter
{
    private readonly double _contrast = (100 + Math.Clamp(Finite(adjustment.Contrast, 0), -100, 100)) / 100.0;
    private readonly double _gamma = 1 / Math.Clamp(Finite(adjustment.Gamma, 1), 0.1, 10);
    private readonly double _brightness = Finite(adjustment.Brightness, 0);

    protected override (byte Blue, byte Green, byte Red) Transform(byte blue, byte green, byte red) =>
        (Adjust(blue, Finite(adjustment.Blue, 0)), Adjust(green, Finite(adjustment.Green, 0)), Adjust(red, Finite(adjustment.Red, 0)));

    private static double Finite(double value, double neutral) => double.IsFinite(value) ? value : neutral;

    private byte Adjust(byte value, double channel)
    {
        var scaled = value / 255.0;
        scaled = (scaled - 0.5) * _contrast + 0.5;
        scaled += (_brightness + channel) / 100.0;
        scaled = Math.Pow(Math.Clamp(scaled, 0, 1), _gamma);
        return Channel(Math.Round(scaled * 255));
    }
}
