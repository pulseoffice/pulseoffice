using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.UI.Commands;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Localization;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls.Services;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class Round325FolioFindAcrossStoriesTests
{
    private static FolioFindCommands Commands(FolioScene scene) =>
        new(scene.Services.GetRequiredService<IActiveFolioContext>(), scene.Services.GetRequiredService<IDocumentSearch>(),
            scene.Services.GetRequiredService<IDocumentStatisticsCounter>(), scene.Services.GetRequiredService<IStatusBarService>(), null!,
            scene.Services.GetRequiredService<ITextCatalog>());

    private static ParagraphBlock Header(FolioScene scene)
    {
        var header = new ParagraphBlock(scene.Document.NextId("p"), new TextParagraph("alfa in testa") { Runs = [new TextRun("alfa in testa")] });
        var style = scene.Document.Styles.Page.Find(scene.Document.InitialPageStyle)!;
        style.Header.Enabled = true;
        style.Header.Default.Replace([header]);
        return header;
    }

    [Fact]
    public Task Replace_all_reaches_headers_and_undoes_as_one_step() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("alfa beta alfa");
        var header = Header(scene);

        var replaced = Commands(scene).ReplaceAll(scene.Editor, new SearchQuery("alfa"), "gamma");

        Assert.Equal(3, replaced);
        Assert.Equal(["gamma beta gamma"], scene.Paragraphs);
        Assert.Equal("gamma in testa", header.Text);
        scene.History.Undo();
        Assert.Equal(["alfa beta alfa"], scene.Paragraphs);
        Assert.Equal("alfa in testa", header.Text);
    }, CancellationToken.None);

    [Fact]
    public Task Replace_changes_the_selected_match_and_moves_to_the_next() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("uno due uno");
        var commands = Commands(scene);
        var query = new SearchQuery("uno");
        var paragraph = scene.Editor.Caret.ParagraphId;
        scene.Editor.Select(new DocumentPosition(paragraph, 0), new DocumentPosition(paragraph, 3));

        Assert.True(commands.Replace(scene.Editor, query, "tre"));

        Assert.Equal(["tre due uno"], scene.Paragraphs);
        Assert.Equal(new DocumentRange(new DocumentPosition(paragraph, 8), new DocumentPosition(paragraph, 11)), scene.Editor.Selection);
    }, CancellationToken.None);
}
