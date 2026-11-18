using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Fields;
using PulseOffice.Documents.Indexes;
using PulseOffice.Documents.Styles;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class Round191ContentsLinkTests
{
    private readonly Fixture _f = new();

    [Fact]
    public void Equal_headings_each_get_their_own_link()
    {
        _f.Document.Body.Replace(
        [
            new ParagraphBlock("p0", string.Empty, DefaultStyleNames.TextBody),
            new ParagraphBlock("h1", "Summary", DefaultStyleNames.HeadingLevel(1)),
            new ParagraphBlock("h2", "Summary", DefaultStyleNames.HeadingLevel(1)),
        ]);

        _f.IndexEditing.Insert(_f.Document, _f.History, new DocumentPosition("p0", 0), new IndexDefinition(IndexKind.Contents), new NoFieldLayoutFacts());

        var links = _f.Tree.Paragraphs(_f.IndexEditing.Indexes(_f.Document).Single().Content).Select(paragraph => paragraph.Content.Runs[0].Hyperlink).ToList();
        Assert.Equal(2, links.Distinct().Count());
        Assert.Equal(["h1", "h2"], links.Select(link => _f.Bookmarks.Find(_f.Document, link![1..])!.ParagraphId));
        _f.History.Undo();
        Assert.Empty(_f.Bookmarks.Bookmarks(_f.Document));
    }
}
