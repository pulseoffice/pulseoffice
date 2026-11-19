using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Layout.Queries;
using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Documents.Marks;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class Round260NoticePagesTests
{
    private static readonly Lazy<DocumentLayout> Continued = new(Build);

    [Fact]
    public void Notices_on_a_page_range_are_those_of_its_pages_only()
    {
        var notices = new LayoutReadOnlyText();
        var all = notices.OnPages(Continued.Value, 0, Continued.Value.Pages.Count - 1);

        Assert.True(all.Count >= 2);
        Assert.All(notices.OnPages(Continued.Value, 1, 1), text => Assert.Equal(1, text.Page));
        Assert.Empty(notices.OnPages(Continued.Value, 0, -1));
        Assert.Equal(all.Count, notices.OnPages(Continued.Value, -3, 99).Count);
    }

    private static DocumentLayout Build()
    {
        var f = new LayoutFixture(width: 200, height: 200);
        var block = f.Add("body");
        block.Marks = [new NoteMark(block.Length, "n1")];
        f.Document.Notes.Add(new Note("n1", NoteKind.Footnote, new BlockContainer(Enumerable.Range(0, 30).Select(i => new ParagraphBlock("np" + i, "line " + i)))));
        f.Document.Notes.FootnoteSettings = f.Document.Notes.FootnoteSettings with { ContinuationForward = "more" };
        return f.Layout();
    }
}
