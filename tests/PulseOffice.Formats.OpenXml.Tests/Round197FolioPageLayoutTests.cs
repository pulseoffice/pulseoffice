using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round197FolioPageLayoutTests
{
    private const string Namespace = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    [Fact]
    public void A_page_number_format_beyond_letters_and_romans_crosses_docx()
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        document.Styles.Page.Find(document.InitialPageStyle)!.NumberFormat = NumberFormat.Hebrew;

        using var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, stream).Succeeded);
        stream.Position = 0;
        using (var package = WordprocessingDocument.Open(stream, false))
            Assert.Equal(W.NumberFormatValues.Hebrew1, package.MainDocumentPart!.Document!.Descendants<W.PageNumberType>().Single().Format!.Value);

        stream.Position = 0;
        var read = _composer.Reader(DocxFlavour.Document).Read(stream).Value!;
        Assert.Contains(read.Styles.Page.All, style => style.NumberFormat == NumberFormat.Hebrew);
    }

    [Fact]
    public void A_word_gutter_widens_the_left_margin_instead_of_vanishing()
    {
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            main.Document = new W.Document($"<w:document {Namespace}><w:body><w:p><w:r><w:t>x</w:t></w:r></w:p><w:sectPr><w:pgSz w:w=\"11906\" w:h=\"16838\"/><w:pgMar w:top=\"1440\" w:right=\"1440\" w:bottom=\"1440\" w:left=\"1440\" w:header=\"720\" w:footer=\"720\" w:gutter=\"720\"/></w:sectPr></w:body></w:document>");
        }
        stream.Position = 0;

        var read = _composer.Reader(DocxFlavour.Document).Read(stream).Value!;

        Assert.Equal(2160, read.Styles.Page.Find(read.InitialPageStyle)!.Margins.Left.Value);
    }

    [Theory]
    [InlineData("<w:cols w:num=\"2\" w:space=\"720\" w:equalWidth=\"0\"><w:col w:w=\"2000\" w:space=\"720\"/><w:col w:w=\"6000\"/></w:cols>", true)]
    [InlineData("<w:cols w:num=\"2\" w:space=\"720\"/>", false)]
    public void Word_columns_of_different_widths_are_declared(string columns, bool declared)
    {
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            main.Document = new W.Document($"<w:document {Namespace}><w:body><w:p><w:r><w:t>x</w:t></w:r></w:p><w:sectPr>{columns}</w:sectPr></w:body></w:document>");
        }
        stream.Position = 0;

        var read = _composer.Reader(DocxFlavour.Document).Read(stream);

        Assert.Equal(declared, read.Diagnostics.Any(diagnostic => diagnostic.Code == "DOCX245"));
    }

    [Fact]
    public void Mirrored_margins_cross_docx_through_the_document_setting()
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        document.Styles.Page.Find(document.InitialPageStyle)!.Usage = PageUsage.Mirrored;

        Assert.DoesNotContain("DOCX037", _composer.Writer(DocxFlavour.Document).CheckFidelity(document).Losses.Select(loss => loss.Code));
        using var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, stream).Succeeded);
        stream.Position = 0;
        var read = _composer.Reader(DocxFlavour.Document).Read(stream).Value!;

        Assert.Equal(PageUsage.Mirrored, read.Styles.Page.Find(read.InitialPageStyle)!.Usage);
    }
}
