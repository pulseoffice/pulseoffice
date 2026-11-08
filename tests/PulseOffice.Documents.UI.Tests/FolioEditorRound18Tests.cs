using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Styles;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioEditorRound18Tests
{
    [Fact]
    public Task AStaleProofMarkPastTheEndHasNoSuggestions() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene(document => document.Body.Replace([new ParagraphBlock("uno", "abc")]));
        var proofing = scene.Services.GetRequiredService<IFolioProofing>();

        Assert.Empty(proofing.Suggestions(new ProofMark(10, 3, false, "en"), "abc"));
        Assert.Empty(proofing.Suggestions(new ProofMark(-1, 3, true, "en"), "abc"));
    }, CancellationToken.None);

    [Fact]
    public Task TheCaretInsideASectionReadsThePageStyleBeforeIt() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene(document => document.Body.Replace(
        [
            new ParagraphBlock("primo", "primo"),
            new SectionBlock("s1", "Sezione", new BlockContainer([new ParagraphBlock("dentro", "dentro")])),
            new ParagraphBlock("ultimo", new TextParagraph("ultimo") { PageStyleName = DefaultStyleNames.Landscape }),
        ]));
        var at = new DocumentPosition("dentro", 1);
        scene.Editor.Select(at, at);
        var pages = scene.Services.GetRequiredService<IFolioPageStyles>();

        Assert.NotEqual(DefaultStyleNames.Landscape, pages.Current(scene.Editor).Name);
        Assert.Equal("primo", pages.Anchor(scene.Editor));
    }, CancellationToken.None);

    [Fact]
    public Task ALongChangeIsDescribedClipped() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene(document => document.Body.Replace(
        [
            new ParagraphBlock("a", new string('x', 300)),
            new ParagraphBlock("b", new string('y', 300)),
        ]));
        var text = scene.Services.GetRequiredService<IFolioReviewText>();

        var plain = text.Plain(scene.Document, new DocumentPosition("a", 0), new DocumentPosition("b", 300));

        Assert.Equal(new string('x', 300) + " ¶ " + new string('y', 300), plain);
    }, CancellationToken.None);

    [Fact]
    public Task ArrowsCrossParagraphsInsideAndOutOfASection() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene(document => document.Body.Replace(
        [
            new ParagraphBlock("primo", "ab"),
            new ParagraphBlock("secondo", "cd"),
        ]));
        var at = new DocumentPosition("primo", 2);
        scene.Editor.Select(at, at);

        scene.Editor.Move(CaretMotion.Right, false, 10);
        Assert.Equal(new DocumentPosition("secondo", 0), scene.Editor.Caret);
        scene.Editor.Move(CaretMotion.Right, false, 10);
        Assert.Equal(new DocumentPosition("secondo", 1), scene.Editor.Caret);
        scene.Editor.Move(CaretMotion.Left, false, 10);
        scene.Editor.Move(CaretMotion.Left, false, 10);
        Assert.Equal(new DocumentPosition("primo", 2), scene.Editor.Caret);
    }, CancellationToken.None);
}
