using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round212DocxDefaultStyleTests
{
    private const string Namespace = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    [Fact]
    public void A_localized_default_paragraph_style_becomes_the_standard_style_and_is_written_as_the_default()
    {
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            main.AddNewPart<StyleDefinitionsPart>().Styles = new W.Styles(
                $"<w:styles {Namespace}><w:style w:type=\"paragraph\" w:default=\"1\" w:styleId=\"Standard\"><w:name w:val=\"Normal\"/><w:rPr><w:sz w:val=\"28\"/></w:rPr></w:style></w:styles>");
            main.Document = new W.Document($"<w:document {Namespace}><w:body><w:p><w:r><w:t>Text</w:t></w:r></w:p></w:body></w:document>");
        }

        stream.Position = 0;
        var document = Read(stream);
        Assert.Equal(DefaultStyleNames.Standard, Assert.Single(document.Body.Blocks.OfType<ParagraphBlock>()).StyleName);
        Assert.Equal(14, document.Styles.Paragraph.Find(DefaultStyleNames.Standard)!.Character.FontSize);
        Assert.Null(document.Styles.Paragraph.Find("Normal"));

        var written = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, written).Succeeded);
        written.Position = 0;
        using var reopened = WordprocessingDocument.Open(written, false);
        var normal = reopened.MainDocumentPart!.StyleDefinitionsPart!.Styles!.Elements<W.Style>().Single(style => style.StyleId?.Value == "Normal");
        Assert.True(normal.Default?.Value);
    }

    [Fact]
    public void A_user_style_named_Normal_does_not_take_the_id_of_the_default_style()
    {
        var map = new DocxStyleMap();
        Assert.NotEqual("Normal", map.Bind(StyleFamily.Paragraph, "Normal"));
        Assert.Equal("Normal", map.Bind(StyleFamily.Paragraph, DefaultStyleNames.Standard));
    }

    [Fact]
    public void A_style_whose_semi_hidden_flag_is_off_stays_visible()
    {
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            main.AddNewPart<StyleDefinitionsPart>().Styles = new W.Styles(
                $"<w:styles {Namespace}><w:style w:type=\"paragraph\" w:styleId=\"Shown\"><w:name w:val=\"Shown\"/><w:semiHidden w:val=\"0\"/></w:style>"
                + $"<w:style w:type=\"paragraph\" w:styleId=\"Kept\"><w:name w:val=\"Kept\"/><w:semiHidden/></w:style></w:styles>");
            main.Document = new W.Document($"<w:document {Namespace}><w:body><w:p/></w:body></w:document>");
        }

        stream.Position = 0;
        var document = Read(stream);
        Assert.False(document.Styles.Paragraph.Find("Shown")!.Hidden);
        Assert.True(document.Styles.Paragraph.Find("Kept")!.Hidden);
    }

    private TextDocument Read(Stream stream)
    {
        var read = _composer.Reader(DocxFlavour.Document).Read(stream);
        Assert.True(read.Succeeded);
        return read.Value!;
    }
}
