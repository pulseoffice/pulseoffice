using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PulseOffice.Charts;
using PulseOffice.Document.Embedding;
using PulseOffice.Drawing.Assets;
using PulseOffice.Foundation.Units;
using PulseOffice.UI.Controls.Charts;
using PulseOffice.UI.Controls.Shapes;

namespace PulseOffice.UI.Controls.Services;

public interface IEmbeddedObjectPicture
{
    byte[]? Png(EmbeddedObject content, DocumentSize size, CultureInfo culture);
}

public interface IObjectPicturePainter
{
    bool Paints(EmbeddedObject content);

    void Paint(DrawingContext context, EmbeddedObject content, Rect bounds, CultureInfo culture);
}

public sealed class DrawingPicturePainter(IDrawingObjectRenderer drawings) : IObjectPicturePainter
{
    public bool Paints(EmbeddedObject content) => drawings.CanRender(content);

    public void Paint(DrawingContext context, EmbeddedObject content, Rect bounds, CultureInfo culture) => drawings.Render(context, content, bounds);
}

public sealed class ChartPicturePainter(ChartRenderer charts) : IObjectPicturePainter
{
    public bool Paints(EmbeddedObject content) => content is ChartDefinition { InlineData: not null };

    public void Paint(DrawingContext context, EmbeddedObject content, Rect bounds, CultureInfo culture)
    {
        var chart = (ChartDefinition)content;
        context.FillRectangle(Brushes.White, bounds);
        charts.Render(context, chart, chart.InlineData!, bounds, culture);
    }
}

public sealed class FormulaPicturePainter(Formulas.IFormulaObjectRenderer formulas) : IObjectPicturePainter
{
    public bool Paints(EmbeddedObject content) => content is FormulaObject;

    public void Paint(DrawingContext context, EmbeddedObject content, Rect bounds, CultureInfo culture)
    {
        context.FillRectangle(Brushes.White, bounds);
        formulas.Render(context, (FormulaObject)content, bounds);
    }
}

public sealed class EmbeddedObjectPicture(IEnumerable<IObjectPicturePainter> painters) : IEmbeddedObjectPicture
{
    private const double PixelsPerPoint = 2;
    private const double MaximumPixels = 4096;

    private readonly IReadOnlyList<IObjectPicturePainter> _painters = [.. painters];

    public byte[]? Png(EmbeddedObject content, DocumentSize size, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(culture);
        if (content is RasterImageObject { MediaType: "image/png" } raster) return raster.Data.ToArray();
        if (_painters.FirstOrDefault(painter => painter.Paints(content)) is not { } chosen) return null;

        var extent = size.Width.Value > 0 && size.Height.Value > 0 ? size : content.NaturalSize;
        var width = extent.Width.ToPoints() * PixelsPerPoint;
        var height = extent.Height.ToPoints() * PixelsPerPoint;
        if (width < 1 || height < 1) return null;
        var scale = Math.Min(1, MaximumPixels / Math.Max(width, height));
        var pixels = new PixelSize((int)Math.Ceiling(width * scale), (int)Math.Ceiling(height * scale));

        using var bitmap = new RenderTargetBitmap(pixels, new Vector(96, 96));
        using (var context = bitmap.CreateDrawingContext())
            chosen.Paint(context, content, new Rect(0, 0, pixels.Width, pixels.Height), culture);
        using var stream = new MemoryStream();
        bitmap.Save(stream, PngBitmapEncoderOptions.Default);
        return stream.ToArray();
    }
}
