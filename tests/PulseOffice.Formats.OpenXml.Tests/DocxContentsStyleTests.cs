using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DocxContentsStyleTests
{
    private const string Namespace = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    [Fact]
    public void Gli_stili_del_sommario_di_Word_sono_quelli_del_sommario_di_Folio_e_tornano_tali()
    {
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            main.AddNewPart<StyleDefinitionsPart>().Styles = new W.Styles(
                $"<w:styles {Namespace}><w:style w:type=\"paragraph\" w:styleId=\"TOC2\"><w:name w:val=\"toc 2\"/><w:pPr><w:ind w:left=\"720\"/></w:pPr><w:rPr><w:sz w:val=\"36\"/></w:rPr></w:style>"
                + $"<w:style w:type=\"paragraph\" w:styleId=\"TOCHeading\"><w:name w:val=\"TOC Heading\"/><w:rPr><w:b/></w:rPr></w:style></w:styles>");
            main.Document = new W.Document($"<w:document {Namespace}><w:body><w:p><w:pPr><w:pStyle w:val=\"TOC2\"/></w:pPr><w:r><w:t>Voce</w:t></w:r></w:p></w:body></w:document>");
        }

        stream.Position = 0;
        var document = Read(stream);
        var entry = Assert.Single(document.Body.Blocks.OfType<ParagraphBlock>());
        Assert.Equal(DefaultStyleNames.ContentsLevel(2), entry.StyleName);
        Assert.Equal(18, document.Styles.Paragraph.Find(DefaultStyleNames.ContentsLevel(2))!.Character.FontSize);
        Assert.NotNull(document.Styles.Paragraph.Find(DefaultStyleNames.ContentsHeading));

        var written = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, written).Succeeded);
        written.Position = 0;
        using (var package = WordprocessingDocument.Open(written, false))
        {
            var names = package.MainDocumentPart!.StyleDefinitionsPart!.Styles!.Elements<W.Style>().Select(style => style.StyleName?.Val?.Value).ToHashSet();
            Assert.Contains("toc 2", names);
            Assert.Contains("TOC Heading", names);
            Assert.DoesNotContain("Contents 2", names);
        }

        written.Position = 0;
        Assert.Equal(DefaultStyleNames.ContentsLevel(2), Assert.Single(Read(written).Body.Blocks.OfType<ParagraphBlock>()).StyleName);
    }

    private TextDocument Read(Stream stream)
    {
        var read = _composer.Reader(DocxFlavour.Document).Read(stream);
        Assert.True(read.Succeeded);
        return read.Value!;
    }
}
