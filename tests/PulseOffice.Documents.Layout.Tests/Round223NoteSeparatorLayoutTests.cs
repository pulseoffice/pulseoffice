using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Documents.Marks;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class Round223NoteSeparatorLayoutTests
{
    private const string Notice = "note-continuation-notice";

    [Fact]
    public void The_footnote_separator_follows_the_document_settings()
    {
        var f = new LayoutFixture(width: 200, height: 400);
        var block = f.Add("body");
        block.Marks = [new NoteMark(block.Length, "n1")];
        f.Document.Notes.Add(new Note("n1", NoteKind.Footnote, new BlockContainer([new ParagraphBlock("np", "note")])));
        f.Document.Notes.FootnoteSeparator = new NoteSeparator { RelativeLength = 0.5, Thickness = 2, SpaceBefore = 4, SpaceAfter = 6 };

        var page = f.Layout().Pages[0];
        var rule = Assert.Single(page.Footnotes!.Content.OfType<DecorationBox>(), box => box.Kind == DecorationKind.FootnoteSeparator);

        Assert.Equal(100, rule.Bounds.Width, 3);
        Assert.Equal(2, rule.Bounds.Height, 3);
        Assert.Equal(page.Footnotes.Bounds.Y + 4, rule.Bounds.Y, 3);
    }

    [Fact]
    public void A_footnote_continued_on_the_next_page_shows_both_continuation_notices()
    {
        var f = new LayoutFixture(width: 200, height: 200);
        var block = f.Add("body");
        block.Marks = [new NoteMark(block.Length, "n1")];
        f.Document.Notes.Add(new Note("n1", NoteKind.Footnote, new BlockContainer(Enumerable.Range(0, 30).Select(i => new ParagraphBlock("np" + i, "line " + i)))));
        f.Document.Notes.FootnoteSettings = f.Document.Notes.FootnoteSettings with { ContinuationForward = "more", ContinuationBackward = "continued" };

        var pages = f.Layout().Pages;

        Assert.True(pages.Count >= 2);
        Assert.Contains(pages[0].Footnotes!.Lines, line => line.ParagraphId == Notice);
        Assert.Contains(pages[1].Footnotes!.Lines, line => line.ParagraphId == Notice);
        Assert.Equal(30, pages.SelectMany(page => page.Footnotes?.Lines ?? []).Count(line => line.ParagraphId.StartsWith("np", StringComparison.Ordinal)));
    }
}
