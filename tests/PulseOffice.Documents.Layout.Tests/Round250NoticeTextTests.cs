using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Layout.Queries;
using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Documents.Marks;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class Round250NoticeTextTests
{
    private static readonly Lazy<(LayoutFixture Fixture, DocumentLayout Layout)> Built = new(Build);

    private static DocumentLayout Continued => Built.Value.Layout;

    [Fact]
    public void A_continuation_notice_is_read_only_text_with_its_words()
    {
        var page = Continued.Pages[0];
        var notice = page.Footnotes!.Lines.Last(line => line.ParagraphId == LayoutParagraphs.ContinuationNotice);
        var text = new LayoutReadOnlyText().At(Continued, 0, notice.Left + 1, notice.Top + notice.Line.Height / 2);

        Assert.NotNull(text);
        Assert.Equal("more", text.Text);
        Assert.Equal(0, text.Page);
    }

    [Fact]
    public void A_click_on_a_continuation_notice_never_places_the_caret_in_it()
    {
        var queries = Built.Value.Fixture.Queries;
        foreach (var page in Continued.Pages.Take(2))
        {
            var notice = page.Footnotes!.Lines.First(line => line.ParagraphId == LayoutParagraphs.ContinuationNotice);
            var (x, y) = (notice.Left + 1, notice.Top + notice.Line.Height / 2);

            Assert.NotEqual(LayoutParagraphs.ContinuationNotice, queries.HitTest(Continued, page.Index, x, y)?.ParagraphId);
            Assert.Null(queries.CharacterAt(Continued, page.Index, x, y));
        }
        Assert.Null(queries.Caret(Continued, new PulseOffice.Documents.Editing.DocumentPosition(LayoutParagraphs.ContinuationNotice, 0)));
    }

    [Fact]
    public void Note_text_is_not_read_only()
    {
        var page = Continued.Pages[0];
        var line = page.Footnotes!.Lines.First(candidate => candidate.ParagraphId.StartsWith("np", StringComparison.Ordinal));

        Assert.Null(new LayoutReadOnlyText().At(Continued, 0, line.Left + 1, line.Top + line.Line.Height / 2));
    }

    private static (LayoutFixture, DocumentLayout) Build()
    {
        var f = new LayoutFixture(width: 200, height: 200);
        var block = f.Add("body");
        block.Marks = [new NoteMark(block.Length, "n1")];
        f.Document.Notes.Add(new Note("n1", NoteKind.Footnote, new BlockContainer(Enumerable.Range(0, 30).Select(i => new ParagraphBlock("np" + i, "line " + i)))));
        f.Document.Notes.FootnoteSettings = f.Document.Notes.FootnoteSettings with { ContinuationForward = "more", ContinuationBackward = "continued" };
        return (f, f.Layout());
    }
}
