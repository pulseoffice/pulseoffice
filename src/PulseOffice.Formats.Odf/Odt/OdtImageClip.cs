using PulseOffice.Foundation.Units;
using PulseOffice.Drawing.Assets;

namespace PulseOffice.Formats.Odf.Odt;

internal sealed class OdtImageClip(OdtMeasure measure)
{
    private const double PointsPerPixel = 72 / ImageHeader.ScreenDotsPerInch;

    private readonly ImageHeader _header = new();

    public string Write(RasterImageObject image, DocumentSize frame)
    {
        var crop = image.Crop;
        var (width, height) = Pixels(image) is { } pixels
            ? (pixels.Width * PointsPerPixel, pixels.Height * PointsPerPixel)
            : (Unscaled(frame.Width.ToPoints(), crop.Left + crop.Right), Unscaled(frame.Height.ToPoints(), crop.Top + crop.Bottom));
        return $"rect({measure.Points(crop.Top * height)}, {measure.Points(crop.Right * width)}, {measure.Points(crop.Bottom * height)}, {measure.Points(crop.Left * width)})";
    }

    public (double Left, double Top, double Right, double Bottom) Read(string? clip, RasterImageObject image, DocumentSize frame)
    {
        if (clip is null) return default;
        var open = clip.IndexOf('(');
        var close = clip.IndexOf(')');
        if (open < 0 || close <= open) return default;
        var parts = clip[(open + 1)..close].Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4) return default;
        double Part(int index) => Math.Max(0, measure.Points(parts[index]) ?? 0);
        double top = Part(0), right = Part(1), bottom = Part(2), left = Part(3);
        var (width, height) = Pixels(image) is { } pixels
            ? (pixels.Width * PointsPerPixel, pixels.Height * PointsPerPixel)
            : (frame.Width.ToPoints() + left + right, frame.Height.ToPoints() + top + bottom);
        return (Fraction(left, width), Fraction(top, height), Fraction(right, width), Fraction(bottom, height));
    }

    private (int Width, int Height)? Pixels(RasterImageObject image) =>
        image is { PixelWidth: > 0, PixelHeight: > 0 } ? (image.PixelWidth, image.PixelHeight)
        : _header.Size(image.Data.Span) is { Width: > 0, Height: > 0 } read ? read
        : null;

    private double Unscaled(double shown, double cropped) => cropped < 0.99 ? shown / (1 - cropped) : shown;

    private double Fraction(double length, double whole) => whole > 0 ? Math.Clamp(length / whole, 0, 1) : 0;
}
