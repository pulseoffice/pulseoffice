using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DocxRevisionLossTests
{
    private const string Namespace = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    [Fact]
    public void Un_inserimento_registrato_senza_testo_si_dichiara_scrivendo_DOCX()
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        document.Changes.Add(new Insertion("ins1", "nl", DateTimeOffset.UnixEpoch));
        document.Body.Replace([new ParagraphBlock("p1", "ab") { Marks = [new ChangeMark(1, "ins1", MarkEdge.Start), new ChangeMark(1, "ins1", MarkEdge.End)] }]);
        Assert.Contains(_composer.Writer(DocxFlavour.Document).CheckFidelity(document).Losses, loss => loss.Code == "DOCX116");

        document.Body.Replace([new ParagraphBlock("p1", "ab") { Marks = [new ChangeMark(0, "ins1", MarkEdge.Start), new ChangeMark(1, "ins1", MarkEdge.End)] }]);
        Assert.DoesNotContain(_composer.Writer(DocxFlavour.Document).CheckFidelity(document).Losses, loss => loss.Code == "DOCX116");
    }

    [Fact]
    public void Le_revisioni_dentro_le_istruzioni_di_campo_si_dichiarano_leggendo_DOCX()
    {
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            package.AddMainDocumentPart().Document = new W.Document($"<w:document {Namespace}><w:body><w:p><w:r><w:fldChar w:fldCharType=\"begin\"/></w:r>"
                + "<w:ins w:id=\"0\" w:author=\"a\"><w:r><w:instrText>HYPERLINK \"https://example.org/\"</w:instrText></w:r></w:ins>"
                + "<w:del w:id=\"1\" w:author=\"a\"><w:r><w:delInstrText>HYPERLINK \"https://example.com/\"</w:delInstrText></w:r></w:del>"
                + "<w:r><w:fldChar w:fldCharType=\"separate\"/></w:r><w:r><w:t>sito</w:t></w:r><w:r><w:fldChar w:fldCharType=\"end\"/></w:r></w:p></w:body></w:document>");
        }

        stream.Position = 0;
        var read = _composer.Reader(DocxFlavour.Document).Read(stream);
        Assert.True(read.Succeeded);
        Assert.Contains(read.Diagnostics, diagnostic => diagnostic.Code == "DOCX117");
    }

    [Fact]
    public void Le_revisioni_nel_testo_di_una_forma_raggruppata_si_dichiarano_leggendo_DOCX()
    {
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            package.AddMainDocumentPart().Document = new W.Document($"<w:document {Namespace} xmlns:v=\"urn:schemas-microsoft-com:vml\"><w:body><w:p><w:r><w:pict><v:group><v:oval><v:textbox><w:txbxContent><w:p>"
                + "<w:del w:id=\"1\" w:author=\"a\"><w:r><w:delText>Deleted</w:delText></w:r></w:del><w:ins w:id=\"2\" w:author=\"a\"><w:r><w:t>Inserted</w:t></w:r></w:ins>"
                + "</w:p></w:txbxContent></v:textbox></v:oval></v:group></w:pict></w:r></w:p></w:body></w:document>");
        }

        stream.Position = 0;
        var read = _composer.Reader(DocxFlavour.Document).Read(stream);
        Assert.True(read.Succeeded);
        Assert.Contains(read.Diagnostics, diagnostic => diagnostic.Code == "DOCX120");
    }
}
