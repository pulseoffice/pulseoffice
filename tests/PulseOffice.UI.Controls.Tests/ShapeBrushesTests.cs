using PulseOffice.Testing;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Media;
using PulseOffice.Drawing.Shapes;
using PulseOffice.UI.Controls.Shapes;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class ShapeBrushesTests
{
    private static UiThreadSession Session => MacroEventsViewTests.Session;

    private readonly byte[] _png =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52, 0, 0, 0, 1, 0, 0, 0, 1, 8, 6, 0, 0, 0, 0x1F, 0x15, 0xC4, 0x89,
        0, 0, 0, 0x0D, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x63, 0xF8, 0xCF, 0xC0, 0xF0, 0x1F, 0x00, 0x05, 0x00, 0x01, 0xFF, 0x89, 0x99, 0x3D, 0x1D,
        0, 0, 0, 0, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82,
    ];

    [Fact]
    public Task Il_riempimento_a_bitmap_diventa_un_pennello_a_immagine_affiancato_o_stirato() => Session.Dispatch(() =>
    {
        var brushes = new ShapeBrushes();
        var stretched = Assert.IsType<ImageBrush>(brushes.Fill(ShapeFill.Default with { Kind = ShapeFillKind.Bitmap, Bitmap = new ShapeBitmap { Data = _png } }, new Rect(0, 0, 100, 50)));
        Assert.Equal(TileMode.None, stretched.TileMode);
        var tiled = Assert.IsType<ImageBrush>(brushes.Fill(ShapeFill.Default with { Kind = ShapeFillKind.Bitmap, Bitmap = new ShapeBitmap { Data = _png, Tiled = true } }, new Rect(0, 0, 100, 50)));
        Assert.Equal(TileMode.Tile, tiled.TileMode);
    }, CancellationToken.None);

    [Fact]
    public Task Il_tratteggio_personalizzato_passa_alla_penna() => Session.Dispatch(() =>
    {
        var line = ShapeLine.Default with { Dash = ShapeStrokeStyle.DashDot, CustomDash = new ShapeDashPattern { Dots1 = 2, Dots1Length = 3, Dots2 = 1, Dots2Length = 0, Distance = 1 } };
        var pen = new ShapeBrushes().Stroke(line)!;
        Assert.Equal([3d, 1, 3, 1, 1, 1], pen.DashStyle!.Dashes!);
    }, CancellationToken.None);
}
