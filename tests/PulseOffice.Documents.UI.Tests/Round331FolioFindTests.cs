using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.UI.Editing;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class Round331FolioFindTests
{
    [Fact]
    public Task A_section_break_code_in_the_replacement_starts_a_new_page_style_section() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("uno due");
        var commands = new FolioFindCommandsFactory().Create(scene.Services);

        Assert.Equal(1, commands.ReplaceAll(scene.Editor, new SearchQuery(" "), "^b"));

        Assert.Equal(["uno", "due"], scene.Paragraphs);
        Assert.Equal(scene.Document.InitialPageStyle, ((ParagraphBlock)scene.Document.Body.Blocks[1]).Content.PageStyleName);
    }, CancellationToken.None);

    [Fact]
    public Task Matches_inside_comments_are_kept_for_the_comment_balloon() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("testo");
        scene.Document.Comments.Add(new Comment("c1", "Anna", DateTimeOffset.UnixEpoch, new BlockContainer([new ParagraphBlock("c", "una nota nota")])));
        var highlights = scene.Services.GetRequiredService<IFolioFindHighlights>();

        highlights.Show(scene.Editor, new SearchQuery("nota"));

        Assert.Equal([new CommentFind("c", 4, 8), new CommentFind("c", 9, 13)], highlights.InComments(scene.Editor)["c1"]);
        var match = new SearchMatch(DocumentRange.In("c", 4, 8), "nota") { CommentId = "c1" };
        highlights.Focus(scene.Editor, match);
        Assert.Same(match, highlights.Focused(scene.Editor));
        highlights.Focus(scene.Editor, null);
        Assert.Null(highlights.Focused(scene.Editor));
    }, CancellationToken.None);
}
