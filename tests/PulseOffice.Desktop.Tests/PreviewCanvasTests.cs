using PulseOffice.Testing;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using PulseOffice.Drawing.Assets.Filters;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Printing;
using PulseOffice.UI.Controls.Objects;
using PulseOffice.UI.Controls.Printing;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class PreviewCanvasTests(HeadlessDesktopSession headless)
{
    private UiThreadSession Session => headless.Session;

    private readonly ShapeOutline _square = new([new ShapeFigure([
        new MoveSegment(new ShapePoint(0, 0)), new LineSegment(new ShapePoint(1, 0)),
        new LineSegment(new ShapePoint(1, 1)), new LineSegment(new ShapePoint(0, 1)), CloseSegment.Instance,
    ])]);

    private readonly RecordingFonts _fonts = new();

    private BgraImage Paint(PrintPath path)
    {
        using var bitmap = new RenderTargetBitmap(new PixelSize(100, 100), new Vector(96, 96));
        using (var context = bitmap.CreateDrawingContext())
        {
            context.FillRectangle(Avalonia.Media.Brushes.White, new Rect(0, 0, 100, 100));
            new AvaloniaPageCanvasFactory(_fonts).Create(context, new DocumentSize(Twip.FromPoints(100), Twip.FromPoints(100)), 1).DrawPath(path);
        }
        return new BitmapPixels().Read(bitmap);
    }

    private (byte R, byte G, byte B) At(BgraImage image, int x, int y)
    {
        var index = (y * image.Width + x) * 4;
        return (image.Pixels[index + 2], image.Pixels[index + 1], image.Pixels[index]);
    }

    [Fact]
    public Task LAnteprimaDipingeLeSfumature() => Session.Dispatch(() =>
    {
        var image = Paint(new PrintPath(_square, new DocumentRect(Twip.FromPoints(10), Twip.FromPoints(10), Twip.FromPoints(80), Twip.FromPoints(80)))
        {
            Gradient = new PrintGradient(new PrintColor(255, 0, 0), new PrintColor(0, 0, 255), 0, false),
        });

        Assert.True(At(image, 15, 50) is { R: > 200, G: < 100 });
        Assert.True(At(image, 85, 50) is { B: > 200, G: < 100 });
    }, CancellationToken.None);

    [Fact]
    public Task LAnteprimaRuotaLeForme() => Session.Dispatch(() =>
    {
        var image = Paint(new PrintPath(_square, new DocumentRect(Twip.FromPoints(10), Twip.FromPoints(40), Twip.FromPoints(80), Twip.FromPoints(20)))
        {
            Fill = new PrintColor(0, 0, 0),
            RotationDegrees = 90,
        });

        Assert.Equal((byte)0, At(image, 50, 15).R);
        Assert.Equal((byte)255, At(image, 15, 50).R);
    }, CancellationToken.None);

    [Fact]
    public Task Un_immagine_in_scala_di_grigi_si_filtra_e_prende_la_bicromia() => Session.Dispatch(() =>
    {
        var gray = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAIAAAABCAAAAADRSSBWAAAAC0lEQVR4nGNwOAAAAUMBAQURd3wAAAAASUVORK5CYII=");
        var pixels = new BitmapPixels();
        using var bitmap = pixels.Decode(gray)!;
        BgraImage? seen = null;

        using var edited = pixels.Edit(bitmap, image =>
        {
            new PixelFilterCatalog().Duotone(new PulseOffice.Drawing.Assets.ImageDuotone("#000000", "#FF0000")).Apply(image);
            seen = image;
        });

        Assert.Equal(8, seen!.Pixels.Length);
        Assert.Equal(0, seen.Pixels[1]);
        Assert.True(seen.Pixels[6] > seen.Pixels[2]);
    }, CancellationToken.None);

    [Fact]
    public Task La_tela_dell_anteprima_risolve_i_caratteri_come_la_vista() => Session.Dispatch(() =>
    {
        using var bitmap = new RenderTargetBitmap(new PixelSize(400, 100), new Vector(96, 96));
        Twip measured;
        using (var context = bitmap.CreateDrawingContext())
        {
            var canvas = new AvaloniaPageCanvasFactory(_fonts).Create(context, new DocumentSize(Twip.FromPoints(400), Twip.FromPoints(100)), 1);
            var font = new PrintFont("Liberation Serif", 12);
            measured = canvas.MeasureText("Anteprima di stampa", font);
            canvas.DrawText("Anteprima di stampa", new DocumentRect(Twip.Zero, Twip.Zero, Twip.FromPoints(400), Twip.FromPoints(100)), font, new PrintColor(0, 0, 0));
        }

        Assert.Equal(["Liberation Serif", "Liberation Serif"], _fonts.Asked);
        var expected = new Avalonia.Media.FormattedText("Anteprima di stampa", System.Globalization.CultureInfo.InvariantCulture,
            Avalonia.Media.FlowDirection.LeftToRight, new Avalonia.Media.Typeface(_fonts.Resolve(null)), 12, Avalonia.Media.Brushes.Black).Width;
        Assert.InRange(measured.ToPoints(), expected - 0.1, expected + 0.1);
    }, CancellationToken.None);

    private sealed class RecordingFonts : PulseOffice.UI.Controls.Fonts.IDisplayFonts
    {
        public List<string> Asked { get; } = [];

        public Avalonia.Media.FontFamily Resolve(string? family)
        {
            if (family is not null) Asked.Add(family);
            return Avalonia.Media.FontFamily.Default;
        }

        public string? Effective(string? family) => family;
    }
}
