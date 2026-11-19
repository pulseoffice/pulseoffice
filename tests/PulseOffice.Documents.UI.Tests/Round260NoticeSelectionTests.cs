using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Marks;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class Round260NoticeSelectionTests
{
    private static void Prepare(TextDocument document)
    {
        var body = new ParagraphBlock("b1", "body") { Marks = [new NoteMark(4, "n1")] };
        document.Body.Replace([body, new ParagraphBlock("b2", "tail")]);
        document.Notes.Add(new Note("n1", NoteKind.Footnote, new BlockContainer(Enumerable.Range(0, 160).Select(i => new ParagraphBlock("np" + i, "line " + i)))));
        document.Notes.FootnoteSettings = document.Notes.FootnoteSettings with { ContinuationForward = "more" };
    }

    [Fact]
    public Task Select_all_includes_the_continuation_notices_and_a_caret_does_not() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene(Prepare);
        Assert.Empty(scene.Editor.LayoutTexts);

        scene.Editor.SelectAll();

        Assert.NotEmpty(scene.Editor.LayoutTexts);
        Assert.All(scene.Editor.LayoutTexts, text => Assert.Equal("more", text.Text));
    }, CancellationToken.None);

    [Fact]
    public Task A_selection_within_one_page_leaves_the_notice_out_and_a_shift_extension_onto_it_takes_it_in() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene(Prepare);
        scene.Editor.Select(new DocumentPosition("b1", 0), new DocumentPosition("b1", 4));
        Assert.Empty(scene.Editor.LayoutTexts);

        scene.Editor.SelectAll();
        var notice = scene.Editor.LayoutTexts[0];
        scene.Editor.Select(new DocumentPosition("b1", 0), new DocumentPosition("b1", 4), notice);

        Assert.Single(scene.Editor.LayoutTexts);
        Assert.True(scene.Editor.HasSelection);
    }, CancellationToken.None);
}
