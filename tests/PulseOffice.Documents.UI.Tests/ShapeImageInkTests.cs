using PulseOffice.Drawing.Shapes;
using PulseOffice.UI.Controls.Shapes;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class ShapeImageInkTests
{
    [Fact]
    public Task La_media_di_un_riempimento_a_immagine_guida_il_colore_automatico_del_testo() => FolioScene.Session.Dispatch(() =>
    {
        var pixels = new PulseOffice.UI.Controls.Objects.BitmapPixels();
        byte[] Png(byte value)
        {
            var data = new byte[4 * 4];
            for (var index = 0; index < data.Length; index += 4) (data[index], data[index + 1], data[index + 2], data[index + 3]) = (value, value, value, 255);
            using var bitmap = pixels.Create(new PulseOffice.Drawing.Assets.Filters.BgraImage(data, 2, 2));
            return pixels.EncodePng(bitmap);
        }

        var brushes = new ShapeBrushes(pixels);
        var dark = brushes.Average(new ShapeBitmap { Data = Png(20) })!.Value;
        Assert.InRange(dark.R, 15, 25);
        var light = brushes.Average(new ShapeBitmap { Data = Png(240) })!.Value;
        Assert.InRange(light.G, 235, 245);
    }, CancellationToken.None);

    [Fact]
    public Task Shared_backdrop_of_a_picture_fill_drives_the_automatic_ink_of_revealed_units() => FolioScene.Session.Dispatch(() =>
    {
        var pixels = new PulseOffice.UI.Controls.Objects.BitmapPixels();
        var data = new byte[4 * 4];
        for (var index = 0; index < data.Length; index += 4) (data[index], data[index + 1], data[index + 2], data[index + 3]) = (10, 10, 10, 255);
        using var bitmap = pixels.Create(new PulseOffice.Drawing.Assets.Filters.BgraImage(data, 2, 2));
        var fill = new ShapeFill { Kind = ShapeFillKind.Bitmap, Bitmap = new ShapeBitmap { Data = pixels.EncodePng(bitmap) } };
        var behind = new ShapeBrushes(pixels).Behind(fill, new ShapeSurroundings("#FFFFFF"));
        var style = new ShapeStyle { Fill = fill, Text = new ShapeTextStyle { AutomaticColor = true } };
        Assert.Equal("#FFFFFF", new ShapeTextInk().Resolve(style, behind.Hex));
    }, CancellationToken.None);
}
