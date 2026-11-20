using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class Round326FolioFindReplaceTests
{
    [Fact]
    public Task Paragraph_marks_are_replaced_and_inserted_in_one_undo_step() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("uno, due");
        var commands = new FolioFindCommandsFactory().Create(scene.Services);

        Assert.Equal(1, commands.ReplaceAll(scene.Editor, new SearchQuery(", "), "^p"));
        Assert.Equal(["uno", "due"], scene.Paragraphs);
        Assert.Equal(1, commands.ReplaceAll(scene.Editor, new SearchQuery("o^pd"), "o d"));
        Assert.Equal(["uno due"], scene.Paragraphs);
        scene.History.Undo();
        Assert.Equal(["uno", "due"], scene.Paragraphs);
    }, CancellationToken.None);

    [Fact]
    public Task A_found_page_break_is_removed_and_a_replacement_page_break_is_set() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("alfa");
        var body = scene.Document.Body;
        body.Replace([.. body.Blocks, new ParagraphBlock(scene.Document.NextId("p"), new TextParagraph("beta") { PageBreakBefore = true })]);
        var commands = new FolioFindCommandsFactory().Create(scene.Services);

        Assert.Equal(1, commands.ReplaceAll(scene.Editor, new SearchQuery("^m"), string.Empty));
        Assert.NotEqual(true, ((ParagraphBlock)body.Blocks[1]).Content.PageBreakBefore);
        Assert.Equal(1, commands.ReplaceAll(scene.Editor, new SearchQuery("beta"), "^mbeta"));
        Assert.True(((ParagraphBlock)body.Blocks[1]).Content.PageBreakBefore);
        Assert.Equal(["alfa", "beta"], scene.Paragraphs);
    }, CancellationToken.None);

    [Fact]
    public Task Replace_with_format_and_no_text_formats_the_found_text() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("alfa beta");
        var commands = new FolioFindCommandsFactory().Create(scene.Services);

        Assert.Equal(1, commands.ReplaceAll(scene.Editor, new SearchQuery("beta"), string.Empty, new SearchFormat(Bold: true)));

        Assert.Equal(["alfa beta"], scene.Paragraphs);
        var bold = new SearchQuery(string.Empty) { Format = new SearchFormat(Bold: true) };
        Assert.Equal(1, commands.Count(scene.Editor, bold));
    }, CancellationToken.None);

    [Fact]
    public Task Highlights_cover_every_match_and_follow_edits() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("ab ab");
        var highlights = scene.Services.GetRequiredService<IFolioFindHighlights>();
        var paragraph = scene.Editor.Caret.ParagraphId;

        highlights.Show(scene.Editor, new SearchQuery("ab"));
        Assert.Equal(new[] { (0, 2), (3, 5) }, highlights.Of(scene.Editor)[paragraph]);
        scene.Type(" ab");
        Assert.Equal(3, highlights.Of(scene.Editor)[paragraph].Count);
        highlights.Clear(scene.Editor);
        Assert.Empty(highlights.Of(scene.Editor));
    }, CancellationToken.None);
}
