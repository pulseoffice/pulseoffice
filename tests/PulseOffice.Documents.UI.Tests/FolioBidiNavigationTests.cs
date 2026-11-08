using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Layout.Flow;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Text.Rich.Bidi;
using TextParagraph = PulseOffice.Text.Rich.TextParagraph;
using TextRun = PulseOffice.Text.Rich.TextRun;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioBidiNavigationTests
{
    private (FolioScene Scene, string Id) Scene(string text, TextDirection direction)
    {
        var scene = new FolioScene();
        var block = new ParagraphBlock(scene.Document.NextId("p"), new TextParagraph(text) { Runs = [new TextRun(text)], Direction = direction });
        scene.Document.Body.Add(block);
        return (scene, block.Id);
    }

    private DocumentPosition Move(FolioScene scene, DocumentPosition caret, CaretMotion motion)
    {
        var layout = scene.Services.GetRequiredService<IDocumentLayoutEngine>().Layout(scene.Document);
        return scene.Services.GetRequiredService<ICaretNavigator>().Move(scene.Document, layout, caret, motion, null, 10).Position;
    }

    [Fact]
    public Task LeftArrowInARightToLeftParagraphMovesForwardInTheText() => FolioScene.Session.Dispatch(() =>
    {
        var (scene, id) = Scene("אבג", TextDirection.RightToLeft);

        Assert.Equal(new DocumentPosition(id, 1), Move(scene, new DocumentPosition(id, 0), CaretMotion.Left));
        Assert.Equal(new DocumentPosition(id, 1), Move(scene, new DocumentPosition(id, 2), CaretMotion.Right));
    }, CancellationToken.None);

    [Fact]
    public Task ArrowsCrossAHebrewWordInsideLatinTextVisually() => FolioScene.Session.Dispatch(() =>
    {
        var (scene, id) = Scene("ab אבג cd", TextDirection.LeftToRight);

        Assert.Equal(new DocumentPosition(id, 5), Move(scene, new DocumentPosition(id, 6), CaretMotion.Right));
        Assert.Equal(new DocumentPosition(id, 6), Move(scene, new DocumentPosition(id, 5), CaretMotion.Left));
    }, CancellationToken.None);

    [Fact]
    public Task LatinParagraphsStillStepLogically() => FolioScene.Session.Dispatch(() =>
    {
        var (scene, id) = Scene("abc", TextDirection.LeftToRight);

        Assert.Equal(new DocumentPosition(id, 2), Move(scene, new DocumentPosition(id, 1), CaretMotion.Right));
        Assert.Equal(new DocumentPosition(id, 0), Move(scene, new DocumentPosition(id, 1), CaretMotion.Left));
    }, CancellationToken.None);
}
