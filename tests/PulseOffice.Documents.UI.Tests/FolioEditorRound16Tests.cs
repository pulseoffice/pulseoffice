using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioEditorRound16Tests
{
    private static FolioScene Three() => new(document => document.Body.Replace(
    [
        new ParagraphBlock("uno", "uno"),
        new ParagraphBlock("due", "duetto"),
        new ParagraphBlock("tre", "tre"),
    ]));

    [Fact]
    public Task UndoingTypingKeepsTheCaretWhereTheTypingWas() => FolioScene.Session.Dispatch(() =>
    {
        var scene = Three();
        scene.Editor.PlaceCaret(new DocumentPosition("tre", 3), false);
        scene.Editor.Type("x");

        scene.History.Undo();

        Assert.Equal(new DocumentPosition("tre", 3), scene.Editor.Caret);
    }, CancellationToken.None);

    [Fact]
    public Task UndoingAParagraphSplitPutsTheCaretBackAtTheSplit() => FolioScene.Session.Dispatch(() =>
    {
        var scene = Three();
        scene.Editor.PlaceCaret(new DocumentPosition("due", 3), false);
        scene.Editor.SplitParagraph();

        scene.History.Undo();

        Assert.Equal(["uno", "duetto", "tre"], scene.Paragraphs);
        Assert.Equal(new DocumentPosition("due", 3), scene.Editor.Caret);
    }, CancellationToken.None);

    [Fact]
    public Task UndoingADeletionAcrossParagraphsDoesNotSendTheCaretToTheTop() => FolioScene.Session.Dispatch(() =>
    {
        var scene = Three();
        scene.Editor.Select(new DocumentPosition("due", 2), new DocumentPosition("tre", 1));
        scene.Editor.Backspace();

        scene.History.Undo();

        Assert.Equal(["uno", "duetto", "tre"], scene.Paragraphs);
        Assert.NotEqual("uno", scene.Editor.Caret.ParagraphId);
    }, CancellationToken.None);

    [Fact]
    public Task CommittedTextEndsTheComposition() => FolioScene.Session.Dispatch(() =>
    {
        var scene = Three();
        scene.Editor.PlaceCaret(new DocumentPosition("uno", 3), false);
        scene.Editor.SetPreedit("ka", null);

        scene.Editor.Type("か");

        Assert.Null(scene.Editor.Preedit);
        Assert.Equal("unoか", scene.Paragraphs[0]);
    }, CancellationToken.None);
}
