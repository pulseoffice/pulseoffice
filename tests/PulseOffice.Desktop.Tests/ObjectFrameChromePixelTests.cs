using PulseOffice.Testing;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PulseOffice.Drawing.Assets.Filters;
using PulseOffice.Drawing.Shapes;
using PulseOffice.UI.Controls.Objects;
using PulseOffice.UI.Controls.Shapes;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class ObjectFrameChromePixelTests(HeadlessDesktopSession headless)
{
    private UiThreadSession Session => headless.Session;

    private readonly ObjectManipulator _manipulator = new();
    private readonly FrameStyles _styles = new();
    private readonly ObjectBox _box = new(60, 60, 120, 80);
    private readonly IBrush _accent = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A));

    [Fact]
    public Task La_variante_piena_riempie_le_maniglie_con_l_accento_e_la_tratteggiata_le_lascia_bianche() => Session.Dispatch(() =>
    {
        var filled = Paint(_styles.Filled, 0);
        var dashed = Paint(_styles.Dashed, 0);

        Assert.True(Near(filled, 180, 140, 0x16, 0xA3, 0x4A));
        Assert.True(Near(dashed, 180, 140, 0xFF, 0xFF, 0xFF));
        Assert.True(Near(filled, 120, 60 - _manipulator.RotationDistance, 0x16, 0xA3, 0x4A));
        Assert.True(Near(dashed, 120, 60 - _manipulator.RotationDistance, 0xFF, 0xFF, 0xFF));
        Assert.True(Stem(filled, 60 - _manipulator.RotationDistance / 2));
        Assert.True(Stem(dashed, 60 - _manipulator.RotationDistance / 2));
        Assert.NotEqual(filled.Pixels, dashed.Pixels);
        return Task.CompletedTask;
    }, CancellationToken.None);

    [Fact]
    public Task La_cornice_ruota_attorno_al_centro() => Session.Dispatch(() =>
    {
        var turned = Paint(_styles.Filled, 90);
        Assert.True(Near(turned, 120 + 40 + _manipulator.RotationDistance, 100, 0x16, 0xA3, 0x4A));
        Assert.True(Near(turned, 120, 100 - 60, 0x16, 0xA3, 0x4A));
        Assert.True(Near(turned, 120, 20, 0xFF, 0xFF, 0xFF));
        return Task.CompletedTask;
    }, CancellationToken.None);

    private BgraImage Paint(FrameStyle style, double rotation)
    {
        using var bitmap = new RenderTargetBitmap(new PixelSize(240, 220), new Vector(96, 96));
        using (var context = bitmap.CreateDrawingContext())
        {
            context.FillRectangle(Brushes.White, new Rect(0, 0, 240, 220));
            new ObjectFrameChrome(_manipulator, style).Draw(context, new FrameScene(_box, rotation, _accent));
        }
        return new BitmapPixels().Read(bitmap);
    }

    private bool Stem(BgraImage image, double y) =>
        Enumerable.Range(118, 4).Any(x => image.Pixels[(((int)y * image.Width) + x) * 4 + 2] < 0xC0);

    private bool Near(BgraImage image, double x, double y, byte r, byte g, byte b)
    {
        var index = ((int)Math.Round(y) * image.Width + (int)Math.Round(x)) * 4;
        return Math.Abs(image.Pixels[index + 2] - r) < 40 && Math.Abs(image.Pixels[index + 1] - g) < 40 && Math.Abs(image.Pixels[index] - b) < 40;
    }
}
