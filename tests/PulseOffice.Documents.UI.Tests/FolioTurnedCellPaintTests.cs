using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Document.Embedding;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Layout.Flow;
using PulseOffice.Documents.UI.Rendering;
using PulseOffice.Foundation.Units;
using PulseOffice.Printing;
using TextParagraph = PulseOffice.Text.Rich.TextParagraph;
using TextRun = PulseOffice.Text.Rich.TextRun;
using Xunit;
using LayoutRect = PulseOffice.Documents.Layout.Tree.LayoutRect;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioTurnedCellPaintTests
{
    private TableBlock Table(FolioScene scene, CellTextDirection direction)
    {
        var turned = new TableCell(new BlockContainer([new ParagraphBlock(scene.Document.NextId("p"), new TextParagraph("Vertical") { Runs = [new TextRun("Vertical")] })])) { TextDirection = direction };
        var plain = new TableCell(new BlockContainer([new ParagraphBlock(scene.Document.NextId("p"), new TextParagraph("Plain") { Runs = [new TextRun("Plain")] })]));
        return new TableBlock(scene.Document.NextId("t"), [new TableRow([turned, plain])], [Twip.FromPoints(40), Twip.FromPoints(120)]);
    }

    [Theory]
    [InlineData(CellTextDirection.TopToBottom, 90)]
    [InlineData(CellTextDirection.BottomToTop, 270)]
    public Task AVerticalCellIsPaintedTurnedOnScreenAndInPrint(CellTextDirection direction, double degrees) => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Document.Body.Add(Table(scene, direction));
        var page = scene.Services.GetRequiredService<IDocumentLayoutEngine>().Layout(scene.Document).Pages[0];
        var renderer = scene.Services.GetRequiredService<IPageRenderer>();
        var screen = new TurnRecorder();
        var canvas = new GlyphRecorder();

        renderer.Paint(screen, page);
        renderer.Paint(scene.Services.GetRequiredService<IPageSurfaceFactory>().Print(canvas), page);

        Assert.Equal([degrees], screen.Turns);
        Assert.Equal(["Vertical"], screen.TurnedTexts);
        Assert.Equal(-degrees, canvas.Runs.Single(run => run.Text == "Vertical").RotationDegrees, 3);
        Assert.Equal(0, canvas.Runs.Single(run => run.Text == "Plain").RotationDegrees, 3);
    }, CancellationToken.None);

    private sealed class TurnRecorder : IPageSurface
    {
        private readonly Stack<double> _open = new();

        public List<double> Turns { get; } = [];

        public List<string> TurnedTexts { get; } = [];

        public void BeginObject(LayoutRect bounds, double rotationDegrees)
        {
            _open.Push(rotationDegrees);
            Turns.Add(rotationDegrees);
        }

        public void EndObject() => _open.Pop();

        public void Fill(LayoutRect rect, uint argb)
        {
        }

        public void Line(double x1, double y1, double x2, double y2, uint argb, double thickness, PageStroke stroke = PageStroke.Solid)
        {
        }

        public void Frame(LayoutRect rect, uint argb, double thickness, PageStroke stroke = PageStroke.Solid)
        {
        }

        public void Glyphs(PulseOffice.Documents.Layout.Tree.GlyphRun run, double x, double baseline, uint argb)
        {
            if (_open.Count > 0) TurnedTexts.Add(run.Text);
        }

        public void RotatedGlyphs(PulseOffice.Documents.Layout.Tree.GlyphRun run, double x, double baseline, uint argb, double degrees)
        {
        }

        public void Embedded(EmbeddedObject content, LayoutRect bounds, ObjectTransform transform)
        {
        }
    }

    private sealed class GlyphRecorder : IPageCanvas
    {
        public List<PrintGlyphRun> Runs { get; } = [];

        public DocumentSize PageSize { get; } = new(Twip.FromPoints(600), Twip.FromPoints(800));

        public Twip MeasureText(string text, PrintFont font) => Twip.FromPoints(text.Length * 6);

        public void FillRectangle(DocumentRect rect, PrintColor color)
        {
        }

        public void DrawLine(DocumentPoint from, DocumentPoint to, PrintColor color, Twip thickness)
        {
        }

        public void DrawText(string text, DocumentRect bounds, PrintFont font, PrintColor color, TextAlignment alignment = TextAlignment.Left, TextBaseline baseline = TextBaseline.Middle)
        {
        }

        public void DrawPath(PrintPath path)
        {
        }

        public void DrawImage(PageImage image, DocumentRect bounds)
        {
        }

        public void DrawGlyphs(PrintGlyphRun run) => Runs.Add(run);
    }
}
