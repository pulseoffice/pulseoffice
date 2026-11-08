using PulseOffice.Document.Embedding;
using PulseOffice.Drawing.Assets;
using PulseOffice.Printing;

namespace PulseOffice.Drawing.Printing;

public interface IOriginalImageSources
{
    PageImageSource? Original(EmbeddedObject content, double rotationDegrees, bool flipped);
}

public sealed class OriginalImageSources : IOriginalImageSources
{
    private readonly HashSet<string> _passThrough = new(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/jpg", "image/pjpeg" };

    public PageImageSource? Original(EmbeddedObject content, double rotationDegrees, bool flipped)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content is not RasterImageObject image || image.Data.IsEmpty || !_passThrough.Contains(image.MediaType)) return null;
        if (flipped || !double.IsFinite(rotationDegrees) || Math.Abs(rotationDegrees % 360) > 0.001) return null;
        if (image.Crop != default || image.Filter != ImageFilter.None || image.Duotone is not null) return null;
        return new PageImageSource(image.Data, "image/jpeg");
    }
}
