using PulseOffice.Documents;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round232DocxSectionNotesTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    [Fact]
    public void Endnote_collection_of_a_section_crosses_docx()
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        document.Body.Replace([new SectionBlock("s1", "One", new BlockContainer([new ParagraphBlock("p1", "one", DefaultStyleNames.Standard)])) { CollectsEndnotes = true }]);

        using var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, stream).Succeeded);
        stream.Position = 0;
        var read = _composer.Reader(DocxFlavour.Document).Read(stream).Value!;

        Assert.True(read.Body.Blocks.OfType<SectionBlock>().Single().CollectsEndnotes);
    }

    [Fact]
    public void A_page_style_separator_is_declared_for_docx()
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        document.Body.Replace([new ParagraphBlock("p1", "one", DefaultStyleNames.Standard)]);
        document.Styles.Page.Find(document.InitialPageStyle)!.FootnoteSeparator = new NoteSeparator { RelativeLength = 1 };

        Assert.Contains(_composer.Writer(DocxFlavour.Document).CheckFidelity(document).Losses, loss => loss.Code == "DOCX273");
    }
}
