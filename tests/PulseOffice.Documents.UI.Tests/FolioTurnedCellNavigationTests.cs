using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Layout.Flow;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Documents.UI.Rendering;
using PulseOffice.Foundation.Units;
using PulseOffice.Printing;
using TextParagraph = PulseOffice.Text.Rich.TextParagraph;
using TextRun = PulseOffice.Text.Rich.TextRun;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioTurnedCellNavigationTests
{
    private (TableBlock Table, string First, string Second) Turned(FolioScene scene, CellTextDirection direction, TextRun? link = null)
    {
        var first = new ParagraphBlock(scene.Document.NextId("p"), new TextParagraph("Vertical one") { Runs = link is null ? [new TextRun("Vertical one")] : [new TextRun("Vertical "), link] });
        var second = new ParagraphBlock(scene.Document.NextId("p"), new TextParagraph("Second line") { Runs = [new TextRun("Second line")] });
        var turned = new TableCell(new BlockContainer([first, second])) { TextDirection = direction };
        var plain = new TableCell(new BlockContainer([new ParagraphBlock(scene.Document.NextId("p"), new TextParagraph("Plain") { Runs = [new TextRun("Plain")] })]));
        return (new TableBlock(scene.Document.NextId("t"), [new TableRow([turned, plain])], [Twip.FromPoints(60), Twip.FromPoints(120)]), first.Id, second.Id);
    }

    [Theory]
    [InlineData(CellTextDirection.TopToBottom, CaretMotion.Down, CaretMotion.Left)]
    [InlineData(CellTextDirection.BottomToTop, CaretMotion.Up, CaretMotion.Right)]
    public Task ArrowKeysInAVerticalCellFollowTheReadingDirection(CellTextDirection direction, CaretMotion along, CaretMotion across) => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        var (table, first, second) = Turned(scene, direction);
        scene.Document.Body.Add(table);
        var layout = scene.Services.GetRequiredService<IDocumentLayoutEngine>().Layout(scene.Document);
        var navigator = scene.Services.GetRequiredService<ICaretNavigator>();
        var caret = new DocumentPosition(first, 2);

        var reading = navigator.Move(scene.Document, layout, caret, along, null, 10).Position;
        var nextLine = navigator.Move(scene.Document, layout, caret, across, null, 10).Position;

        Assert.Equal(new DocumentPosition(first, 3), reading);
        Assert.Equal(second, nextLine.ParagraphId);
    }, CancellationToken.None);

    [Theory]
    [InlineData(CellTextDirection.TopToBottom, CaretMotion.Left, CaretMotion.Right)]
    [InlineData(CellTextDirection.BottomToTop, CaretMotion.Right, CaretMotion.Left)]
    public Task LineMovesAtTheEdgeOfAVerticalCellLeaveTheCellInReadingOrder(CellTextDirection direction, CaretMotion onward, CaretMotion back) => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        var before = new ParagraphBlock(scene.Document.NextId("p"), new TextParagraph("Before") { Runs = [new TextRun("Before")] });
        scene.Document.Body.Add(before);
        var (table, first, second) = Turned(scene, direction);
        scene.Document.Body.Add(table);
        var plain = table.Rows[0].Cells[1].Content.Blocks.OfType<ParagraphBlock>().Single().Id;
        var layout = scene.Services.GetRequiredService<IDocumentLayoutEngine>().Layout(scene.Document);
        var navigator = scene.Services.GetRequiredService<ICaretNavigator>();

        var leaving = navigator.Move(scene.Document, layout, new DocumentPosition(second, 3), onward, null, 10);
        var leavingBack = navigator.Move(scene.Document, layout, new DocumentPosition(first, 3), back, null, 10);

        Assert.Equal(new DocumentPosition(plain, 0), leaving.Position);
        Assert.Null(leaving.PreferredX);
        Assert.Equal(new DocumentPosition(before.Id, before.Length), leavingBack.Position);
    }, CancellationToken.None);

    [Fact]
    public Task ALinkInAVerticalCellIsMarkedAtItsTurnedPlace() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        var (table, _, _) = Turned(scene, CellTextDirection.TopToBottom, new TextRun("one") { Hyperlink = "https://example.org" });
        scene.Document.Body.Add(table);
        var page = scene.Services.GetRequiredService<IDocumentLayoutEngine>().Layout(scene.Document).Pages[0];
        var cell = page.Tables.Single().Cells.Single(item => item.Column == 0);
        var marks = scene.Services.GetRequiredService<IFolioPrintMarks>();
        var canvas = new LinkRecorder();

        marks.Mark(canvas, page, marks.Index(scene.Document));

        var area = Assert.Single(canvas.Links);
        Assert.True(area.Height.ToPoints() > area.Width.ToPoints(), $"link {area.Width.ToPoints()}x{area.Height.ToPoints()} is not standing");
        Assert.True(area.Left.ToPoints() >= cell.Bounds.X - 0.5 && area.Left.ToPoints() + area.Width.ToPoints() <= cell.Bounds.Right + 0.5);
    }, CancellationToken.None);

    [Fact]
    public Task AnInlineObjectInsideATurnIsPrintedTurnedAroundItsTurnedCentre() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        var canvas = new LinkRecorder();
        var surface = scene.Services.GetRequiredService<IPageSurfaceFactory>().Print(canvas);

        surface.BeginObject(new PulseOffice.Documents.Layout.Tree.LayoutRect(100, 100, 100, 20), 90);
        surface.Embedded(new PulseOffice.Drawing.Shapes.ShapeObject { Id = "s", Shape = PulseOffice.Drawing.Shapes.ShapeKind.Rectangle }, new PulseOffice.Documents.Layout.Tree.LayoutRect(100, 100, 20, 10), new ObjectTransform(0));
        surface.EndObject();

        var path = Assert.Single(canvas.Paths);
        Assert.Equal(90, path.RotationDegrees, 3);
    }, CancellationToken.None);

    private sealed class LinkRecorder : IPageCanvas
    {
        public List<PrintPath> Paths { get; } = [];

        public List<DocumentRect> Links { get; } = [];

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

        public void DrawPath(PrintPath path) => Paths.Add(path);

        public void DrawImage(PageImage image, DocumentRect bounds)
        {
        }

        public void MarkLink(DocumentRect area, string target) => Links.Add(area);
    }
}
