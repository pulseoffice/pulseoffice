using Avalonia;
using Avalonia.Media.Imaging;
using PulseOffice.Document.Pages;
using PulseOffice.Drawing.Assets.Filters;
using PulseOffice.UI.Controls.Objects;
using PulseOffice.UI.Controls.Preview;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class PagePreviewTests(HeadlessDesktopSession headless)
{
    private readonly BitmapPixels _pixels = new();

    [Fact]
    public Task La_pagina_disegna_carta_margini_bande_e_colonne_dal_modello_neutro() => headless.Session.Dispatch(() =>
    {
        var sketch = new PageSketch(210, 297)
        {
            Margins = new PageSketchMargins(20, 25, 20, 25),
            Header = new PageSketchBand(10, 5),
            Footer = new PageSketchBand(8, 4, Right: "1"),
            Columns = 2,
            ColumnGap = 10,
        };

        var portrait = Paint(sketch, 120, 160);
        Assert.Equal(0xFF, Pixel(portrait, 60, 80).R);
        Assert.True(Pixel(portrait, 2, 2).A == 0);
        Assert.NotEqual(Pixel(portrait, 60, 22), Pixel(portrait, 60, 80));

        var gutter = Paint(sketch with { Columns = 1 }, 120, 160);
        Assert.NotEqual(portrait.Pixels, gutter.Pixels);

        var landscape = Paint(sketch with { Orientation = PageSketchOrientation.Landscape }, 120, 160);
        Assert.True(Pixel(landscape, 60, 20).A == 0);
        Assert.True(Pixel(landscape, 60, 80).A == 0xFF);
        return Task.CompletedTask;
    }, CancellationToken.None);

    private BgraImage Paint(PageSketch sketch, int width, int height)
    {
        var preview = new PagePreview(new PageSketchLayout()) { Sketch = sketch };
        preview.Measure(new Size(width, height));
        preview.Arrange(new Rect(0, 0, width, height));
        using var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        bitmap.Render(preview);
        return _pixels.Read(bitmap);
    }

    private (byte R, byte G, byte B, byte A) Pixel(BgraImage image, int x, int y)
    {
        var index = (y * image.Width + x) * 4;
        return (image.Pixels[index + 2], image.Pixels[index + 1], image.Pixels[index], image.Pixels[index + 3]);
    }
}
