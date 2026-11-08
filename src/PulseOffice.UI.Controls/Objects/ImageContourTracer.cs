using PulseOffice.Drawing.Assets.Filters;

namespace PulseOffice.UI.Controls.Objects;

public readonly record struct TracedPoint(double X, double Y);

public interface IImageContourTracer
{
    IReadOnlyList<TracedPoint>? Trace(byte[] data);
}

public sealed class ImageContourTracer(IBitmapPixels pixels) : IImageContourTracer
{
    private const int Bands = 32;
    private const byte Opaque = 16;

    public IReadOnlyList<TracedPoint>? Trace(byte[] data)
    {
        using var bitmap = pixels.Decode(data);
        if (bitmap is null) return null;
        var image = pixels.Read(bitmap);
        if (image.Width <= 0 || image.Height <= 0) return null;
        var left = new List<TracedPoint>();
        var right = new List<TracedPoint>();
        var transparent = false;
        for (var band = 0; band <= Bands; band++)
        {
            var row = Math.Min(image.Height - 1, band * image.Height / Bands);
            var (first, last) = Extent(image, row);
            if (first < 0)
            {
                transparent = true;
                continue;
            }
            if (first > 0 || last < image.Width - 1) transparent = true;
            var y = band / (double)Bands;
            left.Add(new TracedPoint(first / (double)image.Width, y));
            right.Add(new TracedPoint((last + 1) / (double)image.Width, y));
        }
        if (!transparent || left.Count < 2) return null;
        right.Reverse();
        return [.. left, .. right];
    }

    private (int First, int Last) Extent(BgraImage image, int row)
    {
        var first = -1;
        var last = -1;
        for (var x = 0; x < image.Width; x++)
        {
            var index = (row * image.Width + x) * 4 + 3;
            if (index >= image.Pixels.Length || image.Pixels[index] < Opaque) continue;
            if (first < 0) first = x;
            last = x;
        }
        return (first, last);
    }
}
