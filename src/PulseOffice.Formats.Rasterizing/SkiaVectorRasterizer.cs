using PulseOffice.Drawing.Assets;
using SkiaSharp;
using Svg.Skia;

namespace PulseOffice.Formats.Rasterizing;

public sealed class SkiaVectorRasterizer(ISvgSanitizer sanitizer) : IVectorRasterizer
{
    public SkiaVectorRasterizer() : this(new SvgSanitizer())
    {
    }

    public byte[]? Png(byte[] svg, int pixelWidth, int pixelHeight)
    {
        ArgumentNullException.ThrowIfNull(svg);
        if (pixelWidth <= 0 || pixelHeight <= 0) return null;

        if (sanitizer.Sanitize(svg) is not { } safe) return null;

        using var document = new SKSvg();
        using var reader = safe.CreateReader();
        if (document.Load(reader) is not { } picture) return null;

        var bounds = picture.CullRect;
        if (bounds.Width <= 0 || bounds.Height <= 0) return null;

        using var surface = SKSurface.Create(new SKImageInfo(pixelWidth, pixelHeight, SKColorType.Rgba8888, SKAlphaType.Premul));
        if (surface is null) return null;

        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        canvas.Scale(pixelWidth / bounds.Width, pixelHeight / bounds.Height);
        canvas.Translate(-bounds.Left, -bounds.Top);
        canvas.DrawPicture(picture);
        canvas.Flush();

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data?.ToArray();
    }
}
