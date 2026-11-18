using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Styles;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class Round232SectionNotesLayoutTests
{
    private List<string> Order(LayoutFixture f) =>
        [.. f.Layout().Pages.SelectMany(page => page.Lines).Select(line => line.ParagraphId)];

    [Fact]
    public void Footnotes_at_the_section_end_close_each_page_style_run()
    {
        var f = new LayoutFixture(width: 200, height: 400);
        f.Document.Notes.Add(new Note("n1", NoteKind.Footnote, new BlockContainer([new ParagraphBlock("np", "note")])));
        f.Document.Notes.FootnoteSettings = f.Document.Notes.FootnoteSettings with { Placement = NotePlacement.SectionEnd };
        var first = f.Add("first");
        first.Marks = [new NoteMark(first.Length, "n1")];
        var second = f.Add("second", content => content with { PageStyleName = f.Document.Styles.Page.All[0].Name });

        var order = Order(f);

        Assert.True(order.IndexOf("np") > order.IndexOf(first.Id));
        Assert.True(order.IndexOf("np") < order.IndexOf(second.Id));
    }

    [Fact]
    public void A_section_can_keep_its_endnotes_for_the_document_end()
    {
        var f = new LayoutFixture(width: 200, height: 400);
        f.Document.Notes.Add(new Note("n1", NoteKind.Endnote, new BlockContainer([new ParagraphBlock("np", "note")])));
        f.Document.Notes.EndnoteSettings = f.Document.Notes.EndnoteSettings with { Placement = NotePlacement.SectionEnd };
        f.Document.Notes.ContinuousEndnotes = true;
        var inner = new ParagraphBlock("sp", "inside");
        inner.Marks = [new NoteMark(inner.Length, "n1")];
        f.Document.Body.Add(new SectionBlock("s1", "Section1", new BlockContainer([inner])) { CollectsEndnotes = false });
        var after = f.Add("after");

        var order = Order(f);

        Assert.True(order.IndexOf("np") > order.IndexOf(after.Id));
    }
}
