using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PulseOffice.Charts;
using PulseOffice.Document.Embedding;
using PulseOffice.Drawing.Printing;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets.Printing;
using PulseOffice.UI.Controls.Charts;
using PulseOffice.UI.Controls.Printing;
using PulseOffice.UI.Controls.Shapes;

namespace PulseOffice.Sheets.UI;

public sealed class SheetObjectRasterizer(
    Workbook workbook,
    ChartRenderer renderer,
    Engine.Operations.IChartDataExtractor data,
    IPageImageCapture capture,
    IDrawingObjectRenderer drawings,
    IObjectPathBuilder paths,
    System.Globalization.CultureInfo culture,
    IOriginalImageSources originals) : ISheetObjectImages
{
    private const double PixelsPerPoint = 3;
    private const int MaximumSide = 3000;

    public SheetObjectImage? Image(EmbeddedObject content, DocumentRect bounds)
    {
        if (content is ChartDefinition chart)
            return Rasterized(bounds, bounds, (context, area) =>
            {
                context.FillRectangle(Brushes.White, area);
                renderer.Render(context, chart, data.Extract(workbook, chart, visibleOnly: true), area, culture);
            });

        if (paths.IsExact(content) || !drawings.CanRender(content)) return null;
        var rotation = RotationOf(content);
        var image = Rasterized(Turned(bounds, rotation), bounds, (context, area) => drawings.Render(context, content, area, new ShapeSurroundings() { Language = workbook.TextLanguage }));
        return image is { Image.Alpha: null } && originals.Original(content, rotation, flipped: false) is { } original
            ? image with { Image = image.Image with { Source = original } }
            : image;
    }

    private SheetObjectImage? Rasterized(DocumentRect area, DocumentRect bounds, Action<DrawingContext, Rect> paint)
    {
        var width = area.Width.ToPoints();
        var height = area.Height.ToPoints();
        if (width <= 0 || height <= 0 || bounds.Width.Value <= 0 || bounds.Height.Value <= 0) return null;

        var scale = Math.Min(PixelsPerPoint, MaximumSide / Math.Max(width, height));
        using var bitmap = new RenderTargetBitmap(
            new PixelSize(Math.Max(1, (int)Math.Ceiling(width * scale)), Math.Max(1, (int)Math.Ceiling(height * scale))),
            new Vector(96, 96));
        using (var context = bitmap.CreateDrawingContext())
        {
            using var zoom = context.PushTransform(Matrix.CreateScale(scale, scale));
            paint(context, new Rect(
                (bounds.Left - area.Left).ToPoints(), (bounds.Top - area.Top).ToPoints(),
                bounds.Width.ToPoints(), bounds.Height.ToPoints()));
        }

        return new SheetObjectImage(capture.FromBitmap(bitmap), area);
    }

    private double RotationOf(EmbeddedObject content) => content switch
    {
        Drawing.Shapes.ShapeObject shape => shape.RotationDegrees,
        Drawing.Shapes.ShapeGroupObject group => group.RotationDegrees,
        Drawing.Freeform.FreeformObject freeform => freeform.RotationDegrees,
        Drawing.Assets.VectorGraphicObject vector => vector.RotationDegrees,
        _ => 0,
    };

    private DocumentRect Turned(DocumentRect bounds, double degrees)
    {
        if (!double.IsFinite(degrees) || degrees % 360 == 0) return bounds;
        var radians = degrees * Math.PI / 180;
        var (cos, sin) = (Math.Abs(Math.Cos(radians)), Math.Abs(Math.Sin(radians)));
        var width = bounds.Width.Value * cos + bounds.Height.Value * sin;
        var height = bounds.Width.Value * sin + bounds.Height.Value * cos;
        return new DocumentRect(
            new Twip((int)Math.Floor(bounds.Left.Value + (bounds.Width.Value - width) / 2)),
            new Twip((int)Math.Floor(bounds.Top.Value + (bounds.Height.Value - height) / 2)),
            new Twip((int)Math.Ceiling(width)), new Twip((int)Math.Ceiling(height)));
    }
}
