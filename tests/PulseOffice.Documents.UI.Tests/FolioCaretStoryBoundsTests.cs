using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Layout.Flow;
using PulseOffice.Documents.Styles;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Foundation.Units;
using TextParagraph = PulseOffice.Text.Rich.TextParagraph;
using TextRun = PulseOffice.Text.Rich.TextRun;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioCaretStoryBoundsTests
{
    private ParagraphBlock Paragraph(FolioScene scene, string text, bool pageBreak = false) =>
        new(scene.Document.NextId("p"), new TextParagraph(text) { Runs = [new TextRun(text)], PageBreakBefore = pageBreak ? true : null });

    [Fact]
    public Task DownFromTheLastBodyLineOfAPageDoesNotEnterATableInTheNextPagesHeader() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        foreach (var style in scene.Document.Styles.Page.All)
        {
            style.Header.Enabled = true;
            style.Header.Default.Replace([new TableBlock(scene.Document.NextId("t"), [new TableRow([new TableCell(new BlockContainer([Paragraph(scene, "Header cell")]))])], [Twip.FromCentimeters(5)])]);
        }
        var one = Paragraph(scene, "one");
        var two = Paragraph(scene, "two", pageBreak: true);
        scene.Document.Body.Replace([one, two]);
        var layout = scene.Services.GetRequiredService<IDocumentLayoutEngine>().Layout(scene.Document);
        var navigator = scene.Services.GetRequiredService<ICaretNavigator>();

        var down = navigator.Move(scene.Document, layout, new DocumentPosition(one.Id, 1), CaretMotion.Down, null, 10).Position;
        var up = navigator.Move(scene.Document, layout, new DocumentPosition(two.Id, 1), CaretMotion.Up, null, 10).Position;

        Assert.Equal(2, layout.Pages.Count);
        Assert.Equal(two.Id, down.ParagraphId);
        Assert.Equal(one.Id, up.ParagraphId);
    }, CancellationToken.None);

    [Fact]
    public Task DownFromTheLastHeaderLineStaysInThatHeader() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        var top = Paragraph(scene, "Top");
        var bottom = Paragraph(scene, "Bottom");
        foreach (var style in scene.Document.Styles.Page.All)
        {
            style.Header.Enabled = true;
            style.Header.Default.Replace([top, bottom]);
        }
        scene.Document.Body.Replace([Paragraph(scene, "one"), Paragraph(scene, "two", pageBreak: true)]);
        var layout = scene.Services.GetRequiredService<IDocumentLayoutEngine>().Layout(scene.Document);
        var navigator = scene.Services.GetRequiredService<ICaretNavigator>();

        var down = navigator.Move(scene.Document, layout, new DocumentPosition(bottom.Id, 1), CaretMotion.Down, null, 10).Position;

        Assert.Equal(2, layout.Pages.Count);
        Assert.Equal(new DocumentPosition(bottom.Id, bottom.Length), down);
    }, CancellationToken.None);

    [Fact]
    public Task DownFromATableCellInAHeaderStaysInThatHeader() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        var cell = Paragraph(scene, "Cell");
        foreach (var style in scene.Document.Styles.Page.All)
        {
            style.Header.Enabled = true;
            style.Header.Default.Replace([new TableBlock(scene.Document.NextId("t"), [new TableRow([new TableCell(new BlockContainer([cell]))])], [Twip.FromCentimeters(5)])]);
        }
        scene.Document.Body.Replace([Paragraph(scene, "one"), Paragraph(scene, "two", pageBreak: true)]);
        var layout = scene.Services.GetRequiredService<IDocumentLayoutEngine>().Layout(scene.Document);
        var navigator = scene.Services.GetRequiredService<ICaretNavigator>();

        var down = navigator.Move(scene.Document, layout, new DocumentPosition(cell.Id, 1), CaretMotion.Down, null, 10);

        Assert.Equal(new DocumentPosition(cell.Id, cell.Length), down.Position);
    }, CancellationToken.None);
}
