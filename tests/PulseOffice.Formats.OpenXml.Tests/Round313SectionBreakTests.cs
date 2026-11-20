using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round313SectionBreakTests
{
    private const string Namespace = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    [Fact]
    public void A_next_column_section_break_starts_a_column_not_a_page()
    {
        const string columns = "<w:cols w:num=\"2\" w:space=\"720\"/>";
        var body = $"<w:document {Namespace}><w:body>"
            + $"<w:p><w:pPr><w:sectPr><w:type w:val=\"continuous\"/>{columns}</w:sectPr></w:pPr><w:r><w:t>one</w:t></w:r></w:p>"
            + "<w:p><w:r><w:t>two</w:t></w:r></w:p>"
            + $"<w:sectPr><w:type w:val=\"nextColumn\"/>{columns}</w:sectPr></w:body></w:document>";
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
            package.AddMainDocumentPart().Document = new W.Document(body);
        stream.Position = 0;

        var read = _composer.Reader(DocxFlavour.Document).Read(stream).Value!;

        var section = read.Body.Blocks.OfType<SectionBlock>().Single();
        Assert.True(section.StartsInNextColumn);
        Assert.Equal("two", Assert.IsType<ParagraphBlock>(section.Content.Blocks.Single()).Content.Text);
        Assert.Null(((ParagraphBlock)section.Content.Blocks.Single()).Content.ColumnBreakBefore);
    }
}
