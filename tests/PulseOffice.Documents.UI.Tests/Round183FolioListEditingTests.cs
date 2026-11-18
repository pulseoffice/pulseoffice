using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class Round183FolioListEditingTests
{
    private static FolioScene List(string secondText, int secondLevel) => new(document => document.Body.Replace(
    [
        new ParagraphBlock("one", new TextParagraph("one") { ListStyleName = DefaultStyleNames.Numbering123, Numbered = true, ListId = "list1" }),
        new ParagraphBlock("two", new TextParagraph(secondText) { ListStyleName = DefaultStyleNames.Numbering123, Numbered = true, ListId = "list1", BulletLevel = secondLevel }),
    ]));

    private static TextParagraph Second(FolioScene scene) => scene.Document.Body.Blocks.OfType<ParagraphBlock>().Single(block => block.Id == "two").Content;

    [Fact]
    public Task EnterOnAnEmptyNestedItemOutdentsIt() => FolioScene.Session.Dispatch(() =>
    {
        var scene = List("", 1);
        scene.Editor.PlaceCaret(new DocumentPosition("two", 0), false);

        scene.Editor.SplitParagraph();

        Assert.Equal(0, Second(scene).BulletLevel);
        Assert.NotNull(Second(scene).ListStyleName);
        scene.History.Undo();
        Assert.Equal(1, Second(scene).BulletLevel);
    }, CancellationToken.None);

    [Fact]
    public Task BackspaceAtTheStartOfAnItemRemovesTheNumberBeforeMerging() => FolioScene.Session.Dispatch(() =>
    {
        var scene = List("two", 0);
        scene.Editor.PlaceCaret(new DocumentPosition("two", 0), false);

        scene.Editor.Backspace();

        Assert.Equal(["one", "two"], scene.Paragraphs);
        Assert.True(Second(scene).ListHeader);
        scene.Editor.Backspace();
        Assert.Equal(["onetwo"], scene.Paragraphs);
        scene.History.Undo();
        scene.History.Undo();
        Assert.False(Second(scene).ListHeader);
    }, CancellationToken.None);
}
