using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round215DocxLinkedStyleTests
{
    private const string Namespace = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    [Fact]
    public void Linked_paragraph_and_character_styles_cross_docx()
    {
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            main.AddNewPart<StyleDefinitionsPart>().Styles = new W.Styles(
                $"<w:styles {Namespace}><w:style w:type=\"paragraph\" w:styleId=\"Quote\"><w:name w:val=\"Quote\"/><w:link w:val=\"QuoteChar\"/></w:style>"
                + "<w:style w:type=\"character\" w:styleId=\"QuoteChar\"><w:name w:val=\"Quote Char\"/><w:link w:val=\"Quote\"/></w:style></w:styles>");
            main.Document = new W.Document($"<w:document {Namespace}><w:body><w:p/></w:body></w:document>");
        }

        stream.Position = 0;
        var read = _composer.Reader(DocxFlavour.Document).Read(stream);
        Assert.True(read.Succeeded);
        var document = read.Value!;
        Assert.Equal("Quote Char", document.Styles.Paragraph.Find("Quote")!.LinkedStyleName);
        Assert.Equal("Quote", document.Styles.Character.Find("Quote Char")!.LinkedStyleName);

        var written = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, written).Succeeded);
        written.Position = 0;
        using var reopened = WordprocessingDocument.Open(written, false);
        var styles = reopened.MainDocumentPart!.StyleDefinitionsPart!.Styles!.Elements<W.Style>().ToList();
        var paragraph = styles.Single(style => style.StyleName?.Val?.Value == "Quote");
        var character = styles.Single(style => style.StyleName?.Val?.Value == "Quote Char");
        Assert.Equal(character.StyleId!.Value, paragraph.LinkedStyle!.Val!.Value);
        Assert.Equal(paragraph.StyleId!.Value, character.LinkedStyle!.Val!.Value);
    }
}
