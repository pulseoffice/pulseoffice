using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Documents.UI.Rendering;
using PulseOffice.Drawing.Assets;
using PulseOffice.Printing;
using PulseOffice.UI.Controls.Objects;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioRenderingTests
{
    private static byte[] RedPng()
    {
        using var bitmap = new RenderTargetBitmap(new PixelSize(40, 30), new Vector(96, 96));
        using (var context = bitmap.CreateDrawingContext()) context.FillRectangle(Brushes.Red, new Rect(0, 0, 40, 30));
        using var stream = new MemoryStream();
        bitmap.Save(stream, PngBitmapEncoderOptions.Default);
        return stream.ToArray();
    }

    private static FolioScene WithImage()
    {
        var scene = new FolioScene();
        scene.Type("Testo");
        Assert.True(scene.Services.GetRequiredService<IFolioImages>().Insert(scene.Editor, RedPng(), "image/png", "rosso.png"));
        return scene;
    }

    private static PulseOffice.Documents.Layout.Tree.ObjectBox Box(FolioScene scene) =>
        scene.Services.GetRequiredService<IFolioObjectHits>().Find(scene.Editor.Layout, scene.Document.Objects.All[0].Id, out _)!;

    [Fact]
    public Task LImmagineSiDisegnaDentroIlSuoRiquadro() => FolioScene.Session.Dispatch(() =>
    {
        var scene = WithImage();
        var page = scene.Editor.Layout.Pages[0];
        var box = Box(scene);
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)page.Width, (int)page.Height), new Vector(96, 96));
        using (var context = bitmap.CreateDrawingContext())
        {
            context.FillRectangle(Brushes.White, new Rect(0, 0, page.Width, page.Height));
            scene.Services.GetRequiredService<IPageRenderer>().Paint(scene.Services.GetRequiredService<IPageSurfaceFactory>().Screen(context), page,
                id => scene.Document.Objects.Find(id)?.Content is EmbeddedContent embedded ? embedded.Value : null);
        }
        var pixels = scene.Services.GetRequiredService<IBitmapPixels>().Read(bitmap);
        var x = (int)(box.Bounds.X + box.Bounds.Width / 2);
        var y = (int)(box.Bounds.Y + box.Bounds.Height / 2);
        var at = (y * pixels.Width + x) * 4;
        Assert.True(pixels.Pixels[at + 2] > 200 && pixels.Pixels[at + 1] < 60 && pixels.Pixels[at] < 60);
    }, CancellationToken.None);

    [Fact]
    public Task InStampaLImmagineDiventaUnImmagineDelCanvas() => FolioScene.Session.Dispatch(() =>
    {
        var scene = WithImage();
        var printable = scene.Services.GetRequiredService<IFolioPrintables>().From(scene.Editor.Layout, scene.Document)!;
        var canvas = new ImageCanvas();

        printable.PaintPage(0, canvas);

        var image = Assert.Single(canvas.Images);
        var middle = (image.Height / 2 * image.Width + image.Width / 2) * 3;
        Assert.True(image.Rgb[middle] > 200 && image.Rgb[middle + 1] < 60);
    }, CancellationToken.None);

    [Fact]
    public Task CopiareUnImmagineLaMetteNegliAppuntiEIncollarlaLaInserisce() => FolioScene.Session.Dispatch(async () =>
    {
        var scene = WithImage();
        scene.Editor.SelectObject(scene.Document.Objects.All[0].Id);

        await scene.View.CopyAsync();
        Assert.NotNull(scene.Clipboard.Image);

        scene.Editor.SelectObject(null);
        await scene.View.PasteAsync();
        Assert.Equal(2, scene.Document.Objects.All.Count);
    }, CancellationToken.None);

    private static byte[] HalfTransparentPng()
    {
        using var bitmap = new RenderTargetBitmap(new PixelSize(40, 30), new Vector(96, 96));
        using (var context = bitmap.CreateDrawingContext()) context.FillRectangle(new SolidColorBrush(Color.FromArgb(128, 0, 0, 255)), new Rect(0, 0, 40, 30));
        using var stream = new MemoryStream();
        bitmap.Save(stream, PngBitmapEncoderOptions.Default);
        return stream.ToArray();
    }

    [Fact]
    public Task InStampaLaTrasparenzaSiConservaEIlTestoSottoResta() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("Testo sotto");
        Assert.True(scene.Services.GetRequiredService<IFolioImages>().Insert(scene.Editor, HalfTransparentPng(), "image/png", "velo.png"));
        var item = scene.Document.Objects.All[0];
        item.Anchor = new ParagraphAnchor(scene.Document.Body.Blocks.OfType<ParagraphBlock>().First().Id);
        item.Wrap = WrapMode.Through;
        scene.Document.Raise();
        var printable = scene.Services.GetRequiredService<IFolioPrintables>().From(scene.Editor.Layout, scene.Document)!;
        var canvas = new ImageCanvas();

        printable.PaintPage(0, canvas);

        var image = Assert.Single(canvas.Images);
        Assert.NotNull(image.Alpha);
        Assert.InRange(image.Alpha![image.Alpha.Length / 2], 100, 160);
        Assert.Contains(canvas.Texts, text => text is "T" or "s");
        if (Environment.GetEnvironmentVariable("FOLIO_PDF_OUT") is { Length: > 0 } output)
        {
            using var file = File.Create(output);
            new PulseOffice.Formats.Pdf.PdfExporter(new PulseOffice.Formats.Pdf.Fonts.StandardPdfFonts(new PulseOffice.Formats.Pdf.PdfFontMetrics())).Write(printable, file);
            File.WriteAllLines(output + ".expected", canvas.Glyphs.Select(glyph => FormattableString.Invariant($"{glyph.Text}\t{glyph.X:0.00}")));
        }
    }, CancellationToken.None);

    [Fact]
    public Task InStampaLeFormeRestanoVettoriali() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("Testo");
        var shape = new PulseOffice.Drawing.Shapes.ShapeObject { Id = "s1", Shape = PulseOffice.Drawing.Shapes.ShapeKind.Ellipse };
        var size = new PulseOffice.Foundation.Units.DocumentSize(PulseOffice.Foundation.Units.Twip.FromCentimeters(3), PulseOffice.Foundation.Units.Twip.FromCentimeters(2));
        scene.Editor.Insert((documents, at) => documents.InsertObject(scene.Document, scene.History,
            new AnchoredObject("o1", new AsCharacterAnchor(at.ParagraphId), new EmbeddedContent(shape), size), at), "test");
        var canvas = new ImageCanvas();

        scene.Services.GetRequiredService<IFolioPrintables>().From(scene.Editor.Layout, scene.Document)!.PaintPage(0, canvas);

        Assert.Empty(canvas.Images);
        Assert.NotEmpty(canvas.Paths);
    }, CancellationToken.None);

    private sealed class ImageCanvas : IPageCanvas
    {
        public List<PageImage> Images { get; } = [];

        public List<PrintPath> Paths { get; } = [];

        public List<string> Texts { get; } = [];

        public List<(string Text, double X)> Glyphs { get; } = [];

        public PulseOffice.Foundation.Units.DocumentSize PageSize => new(PulseOffice.Foundation.Units.Twip.FromCentimeters(21), PulseOffice.Foundation.Units.Twip.FromCentimeters(29.7));

        public PulseOffice.Foundation.Units.Twip MeasureText(string text, PrintFont font) => PulseOffice.Foundation.Units.Twip.FromPoints(text.Length * font.SizePoints / 2);

        public void DrawImage(PageImage image, PulseOffice.Foundation.Units.DocumentRect bounds) => Images.Add(image);

        public void FillRectangle(PulseOffice.Foundation.Units.DocumentRect rect, PrintColor color)
        {
        }

        public void DrawLine(PulseOffice.Foundation.Units.DocumentPoint from, PulseOffice.Foundation.Units.DocumentPoint to, PrintColor color, PulseOffice.Foundation.Units.Twip thickness)
        {
        }

        public void DrawText(string text, PulseOffice.Foundation.Units.DocumentRect bounds, PrintFont font, PrintColor color, PulseOffice.Printing.TextAlignment alignment, PulseOffice.Printing.TextBaseline baseline)
        {
            Texts.Add(text);
            Glyphs.Add((text, bounds.Left.ToPoints()));
        }

        public void DrawPath(PrintPath path) => Paths.Add(path);
    }
}
