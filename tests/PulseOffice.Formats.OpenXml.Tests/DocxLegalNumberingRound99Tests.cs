using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DocxLegalNumberingRound99Tests
{
    private const string Namespace = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";

    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    [Fact]
    public void Legal_numbering_crosses_the_docx_round_trip()
    {
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            main.Document = new W.Document($"<w:document {Namespace}><w:body><w:p><w:pPr><w:numPr><w:ilvl w:val=\"1\"/><w:numId w:val=\"1\"/></w:numPr></w:pPr><w:r><w:t>x</w:t></w:r></w:p></w:body></w:document>");
            main.AddNewPart<NumberingDefinitionsPart>().Numbering = new W.Numbering(
                $"<w:numbering {Namespace}><w:abstractNum w:abstractNumId=\"0\"><w:lvl w:ilvl=\"0\"><w:numFmt w:val=\"upperRoman\"/><w:lvlText w:val=\"%1.\"/></w:lvl><w:lvl w:ilvl=\"1\"><w:start w:val=\"1\"/><w:numFmt w:val=\"lowerLetter\"/><w:isLgl/><w:lvlText w:val=\"%1.%2.\"/></w:lvl></w:abstractNum><w:num w:numId=\"1\"><w:abstractNumId w:val=\"0\"/></w:num></w:numbering>");
        }
        stream.Position = 0;
        var read = _composer.Reader(DocxFlavour.Document).Read(stream).Value!;
        Assert.True(Level(read).Legal);

        var written = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(read, written).Succeeded);
        written.Position = 0;
        using (var package = WordprocessingDocument.Open(written, false))
            Assert.NotEmpty(package.MainDocumentPart!.NumberingDefinitionsPart!.Numbering!.Descendants<W.IsLegalNumberingStyle>());
        written.Position = 0;
        Assert.True(Level(_composer.Reader(DocxFlavour.Document).Read(written).Value!).Legal);
    }

    private static ListLevel Level(PulseOffice.Documents.TextDocument document)
    {
        var name = document.Body.Blocks.OfType<ParagraphBlock>().First().Content.ListStyleName!;
        return document.Styles.List.Find(name)!.Level(1);
    }
}
