using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round315SectionColumnTests
{
    private const string Namespace = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    [Fact]
    public void A_next_column_section_starting_with_a_table_keeps_its_break_type()
    {
        const string columns = "<w:cols w:num=\"2\" w:space=\"720\"/>";
        var read = Read($"<w:p><w:pPr><w:sectPr><w:type w:val=\"continuous\"/>{columns}</w:sectPr></w:pPr><w:r><w:t>one</w:t></w:r></w:p>"
            + "<w:tbl><w:tblGrid><w:gridCol w:w=\"2000\"/></w:tblGrid><w:tr><w:tc><w:p><w:r><w:t>cell</w:t></w:r></w:p></w:tc></w:tr></w:tbl><w:p/>"
            + $"<w:sectPr><w:type w:val=\"nextColumn\"/>{columns}</w:sectPr>");

        var section = read.Body.Blocks.OfType<SectionBlock>().Single();
        Assert.True(section.StartsInNextColumn);
        Assert.IsType<TableBlock>(section.Content.Blocks[0]);

        var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(read, stream).Succeeded);
        stream.Position = 0;
        using var package = WordprocessingDocument.Open(stream, false);
        Assert.Contains(package.MainDocumentPart!.Document!.Descendants<W.SectionType>(), type => type.Val?.Value == W.SectionMarkValues.NextColumn);
    }

    [Fact]
    public void Column_widths_count_when_equal_width_is_omitted()
    {
        var read = Read("<w:p><w:r><w:t>one</w:t></w:r></w:p><w:sectPr><w:cols w:num=\"2\" w:space=\"720\"><w:col w:w=\"2000\" w:space=\"500\"/><w:col w:w=\"6000\"/></w:cols></w:sectPr>");

        var page = read.Styles.Page.Find(read.InitialPageStyle)!;
        Assert.True(page.Columns.Uneven);
        Assert.Equal(new Twip(2000), page.Columns.Spans[0].Width);
    }

    [Fact]
    public void A_styled_column_separator_is_declared_lost_in_docx()
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        document.Body.Add(new SectionBlock("s1", "Section1", new BlockContainer([new ParagraphBlock("sp", "inside", DefaultStyleNames.Standard)]))
        {
            Columns = new SectionColumns(2, Twip.FromCentimeters(1)) { Separator = true, SeparatorLine = new ColumnSeparatorLine(BorderLineStyle.Dotted, 1, 0xFFFF0000) },
        });

        Assert.Contains(_composer.Writer(DocxFlavour.Document).CheckFidelity(document).Losses, loss => loss.Code == "DOCX274");
    }

    private TextDocument Read(string body)
    {
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
            package.AddMainDocumentPart().Document = new W.Document($"<w:document {Namespace}><w:body>{body}</w:body></w:document>");
        stream.Position = 0;
        return _composer.Reader(DocxFlavour.Document).Read(stream).Value!;
    }
}
