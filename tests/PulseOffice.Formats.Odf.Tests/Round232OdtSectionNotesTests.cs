using PulseOffice.Documents;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class Round232OdtSectionNotesTests
{
    private TextDocument Through(TextDocument document)
    {
        var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        stream.Position = 0;
        return new OdtReader().Read(stream).Value!;
    }

    [Fact]
    public void Endnote_collection_crosses_odt_per_section()
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        document.Body.Add(new SectionBlock("s1", "One", new BlockContainer([new ParagraphBlock("p1", "one", DefaultStyleNames.Standard)])) { CollectsEndnotes = true });
        document.Body.Add(new SectionBlock("s2", "Two", new BlockContainer([new ParagraphBlock("p2", "two", DefaultStyleNames.Standard)])));

        var read = Through(document);
        var sections = read.Body.Blocks.OfType<SectionBlock>().ToList();

        Assert.Equal(NotePlacement.DocumentEnd, read.Notes.EndnoteSettings.Placement);
        Assert.True(sections[0].CollectsEndnotes);
        Assert.Null(sections[1].CollectsEndnotes);
    }

    [Fact]
    public void A_page_style_keeps_its_own_footnote_separator()
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        var page = (PageStyle)document.Styles.Page.All[0].CloneAs("Wide");
        page.FootnoteSeparator = new NoteSeparator { RelativeLength = 1 };
        document.Styles.Page.Add(page);

        var read = Through(document);

        Assert.Equal(1, read.Styles.Page.Find("Wide")!.FootnoteSeparator!.RelativeLength, 3);
        Assert.Null(read.Styles.Page.Find(document.Styles.Page.All[0].Name)!.FootnoteSeparator);
    }
}
