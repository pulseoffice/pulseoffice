using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using PulseOffice.Document.Commands;
using PulseOffice.Drawing.Scenes;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Formats.Pdf;
using PulseOffice.Formats.Pdf.Fonts;
using PulseOffice.Foundation.Pages;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations.UI.Rendering;
using PulseOffice.Printing;
using PulseOffice.Testing;
using PulseOffice.UI.Controls.Shapes;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class DeckHeadlessApp : Application
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<DeckHeadlessApp>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

public sealed class DeckSceneAndDimensionTests
{
    private const string SceneMarkup =
        "<dr3d:scene xmlns:dr3d=\"urn:oasis:names:tc:opendocument:xmlns:dr3d:1.0\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" "
        + "draw:style-name=\"gr7\" dr3d:projection=\"perspective\"><dr3d:light dr3d:enabled=\"true\"/><dr3d:sphere draw:style-name=\"gr7\"/></dr3d:scene>";

    private const string SceneStyle =
        "<style:style xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" "
        + "style:name=\"gr7\" style:family=\"graphic\"><style:graphic-properties draw:fill-color=\"#729fcf\"/></style:style>";

    private static readonly UiThreadSession Session = UiThreadSession.StartNew(typeof(DeckHeadlessApp));

    private static readonly DocumentRect Place = new(Twip.FromCentimeters(4), Twip.FromCentimeters(4), Twip.FromCentimeters(8), Twip.FromCentimeters(6));

    private static PulseOffice.Document.Embedding.EmbeddedObject Content(string kind) => kind == "scene"
        ? new Scene3DObject { Id = "scena", Markup = SceneMarkup, Styles = [SceneStyle] }
        : new ShapeObject
        {
            Id = "quota",
            Shape = ShapeKind.Line,
            Style = ShapeStyle.Default with { Fill = ShapeFill.None, Line = ShapeLine.Default with { ColorHex = "#FF0000", StartArrow = ArrowEnd.Arrow, EndArrow = ArrowEnd.Arrow } },
            Dimension = new DimensionLine { Start = new ShapePoint(0, 1), End = new ShapePoint(1, 1), Unit = "mm", ShowUnit = true },
        };

    private static (Presentation Deck, Slide Slide, DeckActions Actions, ObjectElement Element) Scene(string kind)
    {
        var deck = new Presentation();
        deck.Master = new MasterFactory().CreateStandard(deck.Theme);
        var slide = new SlideFactory().Create(deck.NextId(), deck.Master, SlideLayout.Blank);
        deck.AddExisting(slide);
        var actions = new DeckActions(deck, new UndoStack(), new SlideFactory(), new FakeTextCatalog());
        var element = new ObjectElement { Id = deck.NextId(), Bounds = Place, ZOrder = slide.Elements.Count, Content = Content(kind) };
        actions.InsertObject(slide, element);
        return (deck, slide, actions, element);
    }

    private static SlideRenderer Renderer() => new(new DrawingObjectRenderer(), new FakeTextCatalog());

    [Theory]
    [InlineData("dimension")]
    [InlineData("scene")]
    public Task An_object_is_selected_moved_resized_deleted_and_each_step_undoes(string kind) => Session.Dispatch(() =>
    {
        var (deck, slide, actions, element) = Scene(kind);
        var view = new SlideView(Renderer()) { Deck = deck, Size = deck.Size, Editable = true };
        view.Show(slide);
        var window = new Window { Width = 900, Height = 600, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame();
        Assert.Equal(element.Id, view.ElementAt(view.BoundsOf(element).Center)?.Id);
        view.SetSelection([element], element);
        Assert.Equal(element.Id, view.SelectedElement?.Id);

        var moved = Place with { Left = Place.Left + Twip.FromCentimeters(3), Top = Place.Top + Twip.FromCentimeters(1) };
        actions.TransformElement(slide, element.Id, moved, 0);
        Assert.Equal(moved, slide.Find(element.Id)!.Bounds);
        Assert.Equal(element.Content, ((ObjectElement)slide.Find(element.Id)!).Content);

        var resized = moved with { Height = Twip.FromCentimeters(9) };
        actions.TransformElement(slide, element.Id, resized, 0);
        Assert.Equal(resized, slide.Find(element.Id)!.Bounds);

        actions.DeleteObject(slide, slide.Find(element.Id)!);
        Assert.Null(slide.Find(element.Id));

        actions.History.Undo();
        Assert.Equal(resized, slide.Find(element.Id)!.Bounds);
        actions.History.Undo();
        Assert.Equal(moved, slide.Find(element.Id)!.Bounds);
        actions.History.Undo();
        Assert.Equal(Place, slide.Find(element.Id)!.Bounds);
        actions.History.Redo();
        actions.History.Redo();
        Assert.Equal(resized, slide.Find(element.Id)!.Bounds);
        actions.History.Redo();
        Assert.Null(slide.Find(element.Id));
        actions.History.Undo();
        Assert.Equal(element.Content, ((ObjectElement)slide.Find(element.Id)!).Content);
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task An_animated_scene_survives_slide_duplication_with_unique_ids() => Session.Dispatch(() =>
    {
        var (deck, slide, actions, element) = Scene("scene");
        actions.AddAnimation(slide, new AnimationEffect { Id = "anim1", TargetId = element.Id });

        var copy = actions.DuplicateSlide(slide)!;

        var copied = Assert.Single(copy.Elements.OfType<ObjectElement>(), candidate => candidate.Content is Scene3DObject);
        Assert.Equal(element.Content, copied.Content);
        var animation = Assert.Single(copy.Animations);
        Assert.Equal(copied.Id, animation.TargetId);
        var elementIds = deck.Slides.SelectMany(each => each.Elements).Select(each => each.Id).ToList();
        Assert.Equal(elementIds.Count, elementIds.Distinct(StringComparer.Ordinal).Count());
        var animationIds = deck.Slides.SelectMany(each => each.Animations).Select(each => each.Id).ToList();
        Assert.Equal(animationIds.Count, animationIds.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(element.Id, Assert.Single(slide.Animations).TargetId);

        actions.History.Undo();
        Assert.Single(deck.Slides);
    }, CancellationToken.None);

    [Fact]
    public Task The_scene_is_painted_in_pdf_image_and_video_frames() => Session.Dispatch(() =>
    {
        var (deck, slide, _, element) = Scene("scene");
        var images = new SlideImageRenderer(Renderer());

        var picture = images.RenderPixels(deck, slide, 640);
        var scale = 640.0 / deck.Size.Width.Value;
        Assert.True(Inked(picture.Rgb, picture.Width, 3, element.Bounds, scale));

        var frames = Pumped(new DeckVideoFrameSourceFactory().CreateAsync([slide, slide], _ => 1000, new DispatchedSlideFrameRenderer(images, deck, 640), CancellationToken.None));
        var second = Pumped(frames.RenderAsync(1, CancellationToken.None).AsTask());
        Assert.True(Inked(second.Bgra, frames.Width, 4, element.Bounds, scale));

        var painter = new SlidePagePainter(Renderer());
        var setup = new PageSetup
        {
            Paper = new PaperSize("slide", new DocumentSize(deck.Size.Width, deck.Size.Height)),
            Margins = new Margins(Twip.Zero, Twip.Zero, Twip.Zero, Twip.Zero),
            HeaderHeight = Twip.Zero,
            FooterHeight = Twip.Zero,
        };
        var printable = new DeckPrintable(setup, [painter.For(deck, slide)], [string.Empty], DeckExportLayout.Slides);
        var canvas = new ImageCanvas(new DocumentSize(deck.Size.Width, deck.Size.Height));
        printable.PaintPage(0, canvas);
        Assert.Contains(canvas.Images, image => image.Rgb.Where((_, index) => index % 3 == 2).Any(blue => blue > 150) && image.Rgb.Any(channel => channel < 200));

        using var stream = new MemoryStream();
        Assert.True(new PdfExporter(new StandardPdfFonts(new PdfFontMetrics())).Write(printable, stream, new PdfExportOptions("Deck", Compress: false)).Succeeded);
        Assert.Contains("/Subtype /Image", System.Text.Encoding.Latin1.GetString(stream.ToArray()), StringComparison.Ordinal);
    }, CancellationToken.None);

    private static T Pumped<T>(Task<T> task)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (!task.IsCompleted && DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(1);
        }
        return task.GetAwaiter().GetResult();
    }

    private static bool Inked(byte[] pixels, int width, int stride, DocumentRect bounds, double scale)
    {
        var height = pixels.Length / stride / width;
        var left = (int)(bounds.Left.Value * scale);
        var top = (int)(bounds.Top.Value * scale);
        var right = Math.Min(width, (int)((bounds.Left.Value + bounds.Width.Value) * scale));
        var bottom = Math.Min(height, (int)((bounds.Top.Value + bounds.Height.Value) * scale));
        var corner = pixels.AsSpan(0, stride).ToArray();
        for (var y = top; y < bottom; y++)
            for (var x = left; x < right; x++)
            {
                var at = (y * width + x) * stride;
                for (var channel = 0; channel < 3; channel++)
                    if (Math.Abs(pixels[at + channel] - corner[channel]) > 40) return true;
            }
        return false;
    }

    private sealed class ImageCanvas(DocumentSize size) : IPageCanvas
    {
        public List<PageImage> Images { get; } = [];

        public DocumentSize PageSize => size;

        public Twip MeasureText(string text, PrintFont font) => Twip.FromPoints(text.Length * font.SizePoints / 2);

        public void DrawImage(PageImage image, DocumentRect bounds) => Images.Add(image);

        public void FillRectangle(DocumentRect rect, PrintColor color)
        {
        }

        public void DrawLine(DocumentPoint from, DocumentPoint to, PrintColor color, Twip thickness)
        {
        }

        public void DrawText(string text, DocumentRect bounds, PrintFont font, PrintColor color, PulseOffice.Printing.TextAlignment alignment, PulseOffice.Printing.TextBaseline baseline)
        {
        }

        public void DrawPath(PrintPath path)
        {
        }
    }
}
