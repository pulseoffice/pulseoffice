using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DocxTenthAuditListTests
{
    private const string Namespace = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    [Theory]
    [InlineData("<w:lvlPicBulletId w:val=\"0\"/>", true)]
    [InlineData("", false)]
    public void Un_punto_elenco_a_immagine_si_dichiara(string picture, bool declared)
    {
        var numbering = $"<w:numbering {Namespace}><w:abstractNum w:abstractNumId=\"0\"><w:lvl w:ilvl=\"0\"><w:numFmt w:val=\"bullet\"/><w:lvlText w:val=\"o\"/>{picture}</w:lvl></w:abstractNum><w:num w:numId=\"1\"><w:abstractNumId w:val=\"0\"/></w:num></w:numbering>";
        var read = _composer.Reader(DocxFlavour.Document).Read(Docx(numbering));
        Assert.NotNull(read.Value);
        Assert.Equal(declared, read.Diagnostics.Any(diagnostic => diagnostic.Code == "DOCX173"));
    }

    private static MemoryStream Docx(string numbering)
    {
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            main.Document = new W.Document($"<w:document {Namespace}><w:body><w:p><w:pPr><w:numPr><w:ilvl w:val=\"0\"/><w:numId w:val=\"1\"/></w:numPr></w:pPr><w:r><w:t>voce</w:t></w:r></w:p></w:body></w:document>");
            main.AddNewPart<NumberingDefinitionsPart>().Numbering = new W.Numbering(numbering);
        }

        stream.Position = 0;
        return stream;
    }
}
