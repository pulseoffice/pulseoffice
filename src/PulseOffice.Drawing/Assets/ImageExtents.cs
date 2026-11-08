using PulseOffice.Foundation.Units;

namespace PulseOffice.Drawing.Assets;

public sealed record ImageBounds(DocumentSize Largest, Twip SmallestWidth);

public interface IImageExtents
{
    DocumentSize Natural(int pixelWidth, int pixelHeight, ImageBounds bounds);
}

public sealed class ImageExtents : IImageExtents
{
    private const double PixelsPerInch = 96;

    public DocumentSize Natural(int pixelWidth, int pixelHeight, ImageBounds bounds)
    {
        ArgumentNullException.ThrowIfNull(bounds);
        var width = Math.Max(1, pixelWidth) * Twip.PerInch / PixelsPerInch;
        var height = Math.Max(1, pixelHeight) * Twip.PerInch / PixelsPerInch;
        if (width < bounds.SmallestWidth.Value)
        {
            height *= bounds.SmallestWidth.Value / width;
            width = bounds.SmallestWidth.Value;
        }
        var fit = Math.Min(1.0, Math.Min(Math.Max(1, bounds.Largest.Width.Value) / width, Math.Max(1, bounds.Largest.Height.Value) / height));
        return new DocumentSize(new Twip(Twips(width * fit)), new Twip(Twips(height * fit)));
    }

    private int Twips(double value) => (int)Math.Clamp(Math.Round(value), 1, int.MaxValue);
}
