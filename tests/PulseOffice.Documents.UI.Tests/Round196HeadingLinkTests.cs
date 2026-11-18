using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Fields;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class Round196HeadingLinkTests
{
    private static FolioScene Scene() => new(document => document.Body.Replace(
    [
        new ParagraphBlock("h1", new TextParagraph("Same") { OutlineLevel = 1 }),
        new ParagraphBlock("h2", new TextParagraph("Same") { OutlineLevel = 1 }),
        new ParagraphBlock("p1", "body"),
    ]), focus: false);

    [Fact]
    public Task Heading_places_link_to_each_heading_bookmark_created_in_the_link_step() => FolioScene.Session.Dispatch(() =>
    {
        var scene = Scene();
        var links = scene.Services.GetRequiredService<IFolioLinks>();
        var catalog = scene.Services.GetRequiredService<IBookmarkCatalog>();
        var headings = links.Places(scene.Document)[0].Children;
        Assert.NotEqual(headings[0].Address, headings[1].Address);
        Assert.DoesNotContain("|outline", headings[1].Address, StringComparison.Ordinal);

        string settled;
        using (scene.Editor.History.BeginTransaction("hyperlink")) settled = links.Settle(scene.Document, scene.Editor.History, headings[1].Address);

        Assert.Equal(headings[1].Address, settled);
        Assert.Equal("h2", catalog.Find(scene.Document, settled[1..])!.ParagraphId);
        scene.Editor.History.Undo();
        Assert.Null(catalog.Find(scene.Document, settled[1..]));
    }, CancellationToken.None);

    [Fact]
    public Task Other_addresses_pass_through_unchanged() => FolioScene.Session.Dispatch(() =>
    {
        var scene = Scene();
        var links = scene.Services.GetRequiredService<IFolioLinks>();

        Assert.Equal("#Same|outline", links.Settle(scene.Document, scene.Editor.History, "#Same|outline"));
        Assert.Equal("https://example.org", links.Settle(scene.Document, scene.Editor.History, "https://example.org"));
        Assert.False(scene.Editor.History.CanUndo);
    }, CancellationToken.None);
}
