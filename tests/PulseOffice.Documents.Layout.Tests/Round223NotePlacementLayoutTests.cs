using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class Round223NotePlacementLayoutTests
{
    private LayoutFixture Fixture(NoteKind kind, NotePlacement placement)
    {
        var f = new LayoutFixture(width: 200, height: 400);
        f.Document.Notes.Add(new Note("n1", kind, new BlockContainer([new ParagraphBlock("np", "note")])));
        if (kind == NoteKind.Footnote) f.Document.Notes.FootnoteSettings = f.Document.Notes.FootnoteSettings with { Placement = placement };
        else f.Document.Notes.EndnoteSettings = f.Document.Notes.EndnoteSettings with { Placement = placement };
        f.Document.Notes.ContinuousEndnotes = true;
        return f;
    }

    [Fact]
    public void Footnotes_collected_at_the_document_end_follow_the_text_instead_of_the_page_bottom()
    {
        var f = Fixture(NoteKind.Footnote, NotePlacement.DocumentEnd);
        var block = f.Add("body");
        block.Marks = [new NoteMark(block.Length, "n1")];
        f.Add("last");

        var page = f.Layout().Pages[0];

        Assert.Null(page.Footnotes);
        Assert.Equal(new[] { "body", "last", "note" }, page.Lines.Select(line => line.ParagraphId == "np" ? "note" : line.ParagraphId == block.Id ? "body" : "last"));
    }

    [Fact]
    public void Endnotes_collected_at_the_section_end_come_before_the_text_after_the_section()
    {
        var f = Fixture(NoteKind.Endnote, NotePlacement.SectionEnd);
        var inner = new ParagraphBlock("sp", "inside");
        inner.Marks = [new NoteMark(inner.Length, "n1")];
        f.Document.Body.Add(new SectionBlock("s1", "Section1", new BlockContainer([inner])));
        f.Add("after");

        var order = f.Layout().Pages.SelectMany(page => page.Lines).Select(line => line.ParagraphId).ToList();

        Assert.True(order.IndexOf("np") > order.IndexOf("sp"));
        Assert.True(order.IndexOf("np") < order.FindIndex(id => id != "np" && id != "sp"));
    }

    [Fact]
    public void Footnotes_below_the_text_sit_right_under_the_last_line()
    {
        var f = Fixture(NoteKind.Footnote, NotePlacement.BelowText);
        var block = f.Add("body");
        block.Marks = [new NoteMark(block.Length, "n1")];

        var page = f.Layout().Pages[0];

        Assert.NotNull(page.Footnotes);
        Assert.True(page.Footnotes.Bounds.Y < 100);
    }

    [Fact]
    public void Footnotes_at_the_page_bottom_stay_at_the_bottom()
    {
        var f = Fixture(NoteKind.Footnote, NotePlacement.PageBottom);
        var block = f.Add("body");
        block.Marks = [new NoteMark(block.Length, "n1")];

        var page = f.Layout().Pages[0];

        Assert.NotNull(page.Footnotes);
        Assert.True(page.Footnotes.Bounds.Y > 300);
    }
}
