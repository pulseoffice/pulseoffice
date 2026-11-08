using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Documents.UI.Rendering;
using PulseOffice.Drawing.Scenes;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Formats.Pdf;
using PulseOffice.Formats.Pdf.Fonts;
using PulseOffice.Foundation.Units;
using PulseOffice.Printing;
using PulseOffice.UI.Controls.Objects;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioSceneAndDimensionTests
{
    private const string SceneMarkup =
        "<dr3d:scene xmlns:dr3d=\"urn:oasis:names:tc:opendocument:xmlns:dr3d:1.0\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" "
        + "draw:style-name=\"gr7\" dr3d:projection=\"perspective\"><dr3d:light dr3d:enabled=\"true\"/><dr3d:sphere draw:style-name=\"gr7\"/></dr3d:scene>";

    private const string SceneStyle =
        "<style:style xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" "
        + "style:name=\"gr7\" style:family=\"graphic\"><style:graphic-properties draw:fill-color=\"#729fcf\"/></style:style>";

    private static readonly DocumentSize SceneSize = new(Twip.FromCentimeters(4), Twip.FromCentimeters(3));

    private static readonly DocumentSize DimensionSize = new(Twip.FromCentimeters(5), Twip.FromCentimeters(1));

    private static ShapeObject Dimension() => new()
    {
        Id = "quota",
        Shape = ShapeKind.Line,
        Style = ShapeStyle.Default with { Fill = ShapeFill.None, Line = ShapeLine.Default with { ColorHex = "#FF0000", StartArrow = ArrowEnd.Arrow, EndArrow = ArrowEnd.Arrow } },
        Dimension = new DimensionLine { Start = new ShapePoint(0, 1), End = new ShapePoint(1, 1), Unit = "mm", ShowUnit = true },
    };

    private static FolioScene Scene()
    {
        var scene = new FolioScene();
        scene.Type("Testo");
        var first = scene.Document.Body.Blocks.OfType<ParagraphBlock>().First();
        var scene3D = new AnchoredObject("o3d", new PageAnchor(1), new EmbeddedContent(new Scene3DObject { Id = "scena", Markup = SceneMarkup, Styles = [SceneStyle] }), SceneSize);
        var dimension = new AnchoredObject("oquota", new PageAnchor(1), new EmbeddedContent(Dimension()), DimensionSize)
        {
            Placement = new ObjectPlacement(Twip.FromCentimeters(1), Twip.FromCentimeters(8)),
        };
        scene.Editor.Insert((documents, _) => documents.InsertObject(scene.Document, scene.History, scene3D, new DocumentPosition(first.Id, 0)), "test");
        scene.Editor.Insert((documents, _) => documents.InsertObject(scene.Document, scene.History, dimension, new DocumentPosition(first.Id, 0)), "test");
        return scene;
    }

    [Theory]
    [InlineData("o3d")]
    [InlineData("oquota")]
    public Task An_object_moves_deletes_and_comes_back_with_undo(string id) => FolioScene.Session.Dispatch(() =>
    {
        var scene = Scene();
        var original = scene.Document.Objects.Find(id)!;
        var (originalPlacement, originalContent, originalSize) = (original.Placement, original.Content, original.Size);
        var mover = scene.Services.GetRequiredService<IFolioObjectMover>();

        Assert.True(mover.Move(scene.Editor, id, new ObjectDrop(0, 150, 320, 160, 330)));
        var moved = scene.Document.Objects.Find(id)!;
        Assert.Equal(150, moved.Placement.X.ToPoints(), 0);
        Assert.Equal(320, moved.Placement.Y.ToPoints(), 0);
        var movedPlacement = moved.Placement;
        Assert.Equal(originalContent, moved.Content);
        Assert.Equal(originalSize, moved.Size);

        scene.Editor.SelectObject(id);
        Assert.True(scene.Editor.DeleteSelectedObject());
        Assert.Null(scene.Document.Objects.Find(id));

        scene.History.Undo();
        Assert.Equal(movedPlacement, scene.Document.Objects.Find(id)!.Placement);
        Assert.Equal(originalContent, scene.Document.Objects.Find(id)!.Content);
        scene.History.Undo();
        Assert.Equal(originalPlacement, scene.Document.Objects.Find(id)!.Placement);
        scene.History.Redo();
        scene.History.Redo();
        Assert.Null(scene.Document.Objects.Find(id));
    }, CancellationToken.None);

    [Fact]
    public Task Print_and_pdf_paint_the_scene_and_the_dimension_line() => FolioScene.Session.Dispatch(() =>
    {
        var scene = Scene();
        var printable = scene.Services.GetRequiredService<IFolioPrintables>().From(scene.Editor.Layout, scene.Document)!;
        var canvas = new RecordingCanvas();

        printable.PaintPage(0, canvas);

        Assert.Equal(2, canvas.Images.Count);
        Assert.All(canvas.Images, image => Assert.True(Inked(image, (r, g, b) => r < 200 || g < 200 || b < 200)));
        Assert.Contains(canvas.Images, image => Inked(image, (r, g, b) => r > 200 && g < 80 && b < 80));

        using var stream = new MemoryStream();
        Assert.True(new PdfExporter(new StandardPdfFonts(new PdfFontMetrics())).Write(printable, stream, new PdfExportOptions("Prova", Compress: false)).Succeeded);
        Assert.True(stream.Length > 0);
    }, CancellationToken.None);

    [Fact]
    public Task The_raster_page_shows_the_scene_and_the_dimension_line() => FolioScene.Session.Dispatch(() =>
    {
        var scene = Scene();
        var page = scene.Editor.Layout.Pages[0];
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)page.Width, (int)page.Height), new Vector(96, 96));
        using (var context = bitmap.CreateDrawingContext())
        {
            context.FillRectangle(Brushes.White, new Rect(0, 0, page.Width, page.Height));
            scene.Services.GetRequiredService<IPageRenderer>().Paint(scene.Services.GetRequiredService<IPageSurfaceFactory>().Screen(context), page,
                id => scene.Document.Objects.Find(id)?.Content is EmbeddedContent embedded ? embedded.Value : null);
        }
        var pixels = scene.Services.GetRequiredService<IBitmapPixels>().Read(bitmap);
        var hits = scene.Services.GetRequiredService<IFolioObjectHits>();

        Assert.True(Inked(pixels, hits.Find(scene.Editor.Layout, "o3d", out _)!.Bounds));
        Assert.True(Inked(pixels, hits.Find(scene.Editor.Layout, "oquota", out _)!.Bounds));
    }, CancellationToken.None);

    private static bool Inked(PulseOffice.Drawing.Assets.Filters.BgraImage pixels, PulseOffice.Documents.Layout.Tree.LayoutRect bounds)
    {
        for (var y = (int)bounds.Y; y < (int)(bounds.Y + bounds.Height) && y < pixels.Height; y++)
            for (var x = (int)bounds.X; x < (int)(bounds.X + bounds.Width) && x < pixels.Width; x++)
            {
                var at = (y * pixels.Width + x) * 4;
                if (pixels.Pixels[at] < 200 || pixels.Pixels[at + 1] < 200 || pixels.Pixels[at + 2] < 200) return true;
            }
        return false;
    }

    private static bool Inked(PageImage image, Func<byte, byte, byte, bool> ink)
    {
        for (var pixel = 0; pixel * 3 + 2 < image.Rgb.Length; pixel++)
            if ((image.Alpha is null || image.Alpha[pixel] > 0) && ink(image.Rgb[pixel * 3], image.Rgb[pixel * 3 + 1], image.Rgb[pixel * 3 + 2])) return true;
        return false;
    }

    private sealed class RecordingCanvas : IPageCanvas
    {
        public List<PageImage> Images { get; } = [];

        public List<PrintPath> Paths { get; } = [];

        public List<string> Texts { get; } = [];

        public DocumentSize PageSize => new(Twip.FromCentimeters(21), Twip.FromCentimeters(29.7));

        public Twip MeasureText(string text, PrintFont font) => Twip.FromPoints(text.Length * font.SizePoints / 2);

        public void DrawImage(PageImage image, DocumentRect bounds) => Images.Add(image);

        public void FillRectangle(DocumentRect rect, PrintColor color)
        {
        }

        public void DrawLine(DocumentPoint from, DocumentPoint to, PrintColor color, Twip thickness)
        {
        }

        public void DrawText(string text, DocumentRect bounds, PrintFont font, PrintColor color, PulseOffice.Printing.TextAlignment alignment, PulseOffice.Printing.TextBaseline baseline) => Texts.Add(text);

        public void DrawPath(PrintPath path) => Paths.Add(path);

        public void DrawGlyphs(PrintGlyphRun run) => Texts.Add(run.Text);
    }
}
