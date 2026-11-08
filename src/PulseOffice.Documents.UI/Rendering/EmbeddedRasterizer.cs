using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PulseOffice.Document.Embedding;
using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Printing;
using PulseOffice.UI.Controls.Objects;
using PulseOffice.UI.Embedding;

namespace PulseOffice.Documents.UI.Rendering;

public sealed record ObjectTransform(double RotationDegrees, bool FlipHorizontal = false, bool FlipVertical = false)
{
    public bool Flipped => FlipHorizontal || FlipVertical;

    public Matrix Around(Rect rect) =>
        Matrix.CreateTranslation(-rect.Center.X, -rect.Center.Y)
        * Matrix.CreateScale(FlipHorizontal ? -1 : 1, FlipVertical ? -1 : 1)
        * Matrix.CreateRotation(RotationDegrees * Math.PI / 180)
        * Matrix.CreateTranslation(rect.Center.X, rect.Center.Y);
}

public interface IEmbeddedRasterizer
{
    PageImage? Rasterize(EmbeddedObject content, LayoutRect bounds, ObjectTransform transform, out LayoutRect area, string? language = null);
}

public sealed class EmbeddedRasterizer(IContentPainter painter, IBitmapPixels pixels, PulseOffice.Drawing.Printing.IOriginalImageSources originals) : IEmbeddedRasterizer
{
    private const double PixelsPerPoint = 3;
    private const int MaximumSide = 4000;

    public PageImage? Rasterize(EmbeddedObject content, LayoutRect bounds, ObjectTransform transform, out LayoutRect area, string? language = null)
    {
        area = RotatedRect.Around(bounds, transform.RotationDegrees);
        if (!painter.CanPaint(content) || area.Width <= 0 || area.Height <= 0) return null;
        var scale = Math.Min(PixelsPerPoint, MaximumSide / Math.Max(area.Width, area.Height));
        var width = Math.Max(1, (int)Math.Ceiling(area.Width * scale));
        var height = Math.Max(1, (int)Math.Ceiling(area.Height * scale));
        using var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        var local = new Rect(bounds.X - area.X, bounds.Y - area.Y, bounds.Width, bounds.Height);
        using (var context = bitmap.CreateDrawingContext())
        {
            using var zoom = context.PushTransform(Matrix.CreateScale(scale, scale));
            using var turn = context.PushTransform(transform.Around(local));
            painter.Paint(context, content, local, 1, new PulseOffice.UI.Controls.Shapes.ShapeSurroundings() { Language = language });
        }
        var image = pixels.Read(bitmap);
        var count = image.Width * image.Height;
        var rgb = new byte[count * 3];
        var alpha = new byte[count];
        var opaque = true;
        for (var index = 0; index < count && index * 4 + 3 < image.Pixels.Length; index++)
        {
            var source = index * 4;
            rgb[index * 3] = image.Pixels[source + 2];
            rgb[index * 3 + 1] = image.Pixels[source + 1];
            rgb[index * 3 + 2] = image.Pixels[source];
            alpha[index] = image.Pixels[source + 3];
            opaque &= alpha[index] == 255;
        }
        return new PageImage(image.Width, image.Height, rgb)
        {
            Alpha = opaque ? null : alpha,
            Source = opaque ? originals.Original(content, transform.RotationDegrees, transform.Flipped) : null,
        };
    }
}
