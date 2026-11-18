using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Text.Rich;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round215DocxListSuppressionTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    [Fact]
    public void A_paragraph_that_switches_off_its_style_list_is_written_with_numbering_id_zero_and_read_back()
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        document.Styles.Paragraph.Add(new ParagraphStyle("Listed") { ParentName = DefaultStyleNames.Standard, ListStyleName = DefaultStyleNames.List1 });
        document.Body.Add(new ParagraphBlock(document.NextId("p"), new TextParagraph("off") { StyleName = "Listed", ListSuppressed = true }));
        document.Body.Add(new ParagraphBlock(document.NextId("p"), new TextParagraph("on") { StyleName = "Listed" }));

        var written = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, written).Succeeded);
        written.Position = 0;
        using (var package = WordprocessingDocument.Open(written, false))
        {
            var listed = package.MainDocumentPart!.Document!.Body!.Descendants<W.Paragraph>()
                .Where(paragraph => paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value == "Listed").ToList();
            Assert.Equal(2, listed.Count);
            Assert.Equal(0, listed[0].ParagraphProperties!.NumberingProperties!.NumberingId!.Val!.Value);
            Assert.Null(listed[1].ParagraphProperties!.NumberingProperties);
        }

        written.Position = 0;
        var read = _composer.Reader(DocxFlavour.Document).Read(written);
        Assert.True(read.Succeeded);
        var blocks = read.Value!.Body.Blocks.OfType<ParagraphBlock>().Where(block => block.Content.StyleName == "Listed").ToList();
        Assert.Equal(2, blocks.Count);
        Assert.True(blocks[0].Content.ListSuppressed);
        Assert.False(blocks[1].Content.ListSuppressed);
    }
}
