using Avalonia;
using Avalonia.Media;
using PulseOffice.Document.Embedding;
using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Documents.UI.Rendering;
using PulseOffice.Drawing.Assets;
using PulseOffice.UI.Controls.Objects;
using PulseOffice.UI.Controls.Shapes;
using PulseOffice.UI.Embedding;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class Round282TransparentImagePdfTests
{
    private sealed class HalfWhitePainter : IContentPainter
    {
        public bool CanPaint(EmbeddedObject content) => true;

        public void Paint(DrawingContext context, EmbeddedObject content, Rect bounds, double pixelsPerPoint = 1, ShapeSurroundings backdrop = default)
        {
            using (context.PushOpacity(0.5)) context.FillRectangle(Brushes.White, bounds);
        }
    }

    [Fact]
    public Task A_semi_transparent_image_keeps_its_colour_beside_the_soft_mask() => FolioScene.RunAsync(() =>
    {
        var rasterizer = new EmbeddedRasterizer(new HalfWhitePainter(), new BitmapPixels(), new PulseOffice.Drawing.Printing.OriginalImageSources());
        var content = new RasterImageObject { Id = "r", Data = new byte[] { 1 }, MediaType = "image/png", Tone = new ImageTone(Transparency: 50) };
        var image = rasterizer.Rasterize(content, new LayoutRect(0, 0, 4, 4), new ObjectTransform(0), out _);
        Assert.NotNull(image);
        Assert.NotNull(image.Alpha);
        Assert.InRange(image.Alpha[0], 120, 135);
        Assert.InRange(image.Rgb[0], 250, 255);
        return Task.CompletedTask;
    });
}
