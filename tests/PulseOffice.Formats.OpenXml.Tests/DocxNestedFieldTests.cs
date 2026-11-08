using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Text.Rich;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DocxNestedFieldTests
{
    private const string Namespace = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";

    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private static string Begin => "<w:r><w:fldChar w:fldCharType=\"begin\"/></w:r>";

    private static string Separate => "<w:r><w:fldChar w:fldCharType=\"separate\"/></w:r>";

    private static string End => "<w:r><w:fldChar w:fldCharType=\"end\"/></w:r>";

    private static string Code(string text) => $"<w:r><w:instrText xml:space=\"preserve\">{text}</w:instrText></w:r>";

    private static string Text(string text) => $"<w:r><w:t xml:space=\"preserve\">{text}</w:t></w:r>";

    private static MemoryStream Package(string paragraph)
    {
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
            package.AddMainDocumentPart().Document = new W.Document($"<w:document {Namespace}><w:body><w:p>{paragraph}</w:p></w:body></w:document>");
        stream.Position = 0;
        return stream;
    }

    private (TextDocument Document, IReadOnlyList<string> Codes) Read(Stream stream)
    {
        var read = _composer.Reader(DocxFlavour.Document).Read(stream);
        Assert.True(read.Succeeded);
        return (read.Value!, [.. read.Diagnostics.Select(diagnostic => diagnostic.Code)]);
    }

    private static TextRun Field(TextDocument document) =>
        document.Body.Blocks.OfType<ParagraphBlock>().SelectMany(paragraph => paragraph.Content.Runs).Single(run => run.Field == TextFieldKind.ConditionalText);

    private static void AssertMerge(TextDocument document)
    {
        var field = Field(document);
        Assert.Equal("Citta == \"Roma\"", field.FieldArgument);
        Assert.Equal("sì", field.FieldDetail?.WhenTrue);
        Assert.Equal("no", field.FieldDetail?.WhenFalse);
        Assert.Equal("sì", field.Text);
    }

    [Fact]
    public void UnIfConUnCampoUnioneAnnidatoTieneRisultatoECondizione()
    {
        using var stream = Package(Begin + Code(" IF \"") + Begin + Code(" MERGEFIELD Citta ") + Separate + Text("Roma") + End + Code("\" = \"Roma\" \"sì\" \"no\" ") + Separate + Text("sì") + End);
        var (document, codes) = Read(stream);
        AssertMerge(document);
        Assert.DoesNotContain("DOCX160", codes);

        using var docx = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, docx).Succeeded);
        docx.Position = 0;
        using (var package = WordprocessingDocument.Open(docx, false))
        {
            var codesWritten = string.Concat(package.MainDocumentPart!.Document!.Descendants<W.FieldCode>().Select(code => code.Text));
            Assert.Contains("MERGEFIELD Citta", codesWritten);
            Assert.Equal(2, package.MainDocumentPart.Document!.Descendants<W.FieldChar>().Count(item => item.FieldCharType?.Value == W.FieldCharValues.Begin));
        }
        docx.Position = 0;
        AssertMerge(Read(docx).Document);

        using var odt = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, odt).Succeeded);
        odt.Position = 0;
        AssertMerge(new OdtReader().Read(odt).Value!);
    }

    [Fact]
    public void UnaVariabileDelDocumentoNellaCondizioneSiScriveComeDocvariableAnnidato()
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        document.Variables["Tipo"] = "A";
        document.Body.Replace([new ParagraphBlock("p1", new TextParagraph("uno")
        {
            Runs = [new TextRun("uno") { Field = TextFieldKind.ConditionalText, FieldArgument = "Tipo == \"A\"", FieldDetail = new FieldDetail { WhenTrue = "uno", WhenFalse = "due" } }],
        })]);

        using var docx = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, docx).Succeeded);
        docx.Position = 0;
        using (var package = WordprocessingDocument.Open(docx, false))
            Assert.Contains("DOCVARIABLE Tipo", string.Concat(package.MainDocumentPart!.Document!.Descendants<W.FieldCode>().Select(code => code.Text)));
        docx.Position = 0;
        var field = Field(Read(docx).Document);
        Assert.Equal("Tipo == \"A\"", field.FieldArgument);
        Assert.Equal("uno", field.Text);
    }

    [Fact]
    public void UnCampoAnnidatoCheNonEUnNomeSiLeggeComeValoreESiDichiara()
    {
        using var stream = Package(Begin + Code(" IF ") + Begin + Code(" PAGE ") + Separate + Text("3") + End + Code(" > 1 \"molte\" \"una\" ") + Separate + Text("molte") + End);
        var (document, codes) = Read(stream);
        var field = Field(document);
        Assert.Equal("\"3\" > 1", field.FieldArgument);
        Assert.Equal("molte", field.Text);
        Assert.Contains("DOCX160", codes);
    }

    [Fact]
    public void UnCampoAnnidatoNelRisultatoRestaUnCampoNelRisultatoDiUnIntervallo()
    {
        using var stream = Package(Begin + Code(" IF 1 = 1 \"sì\" \"no\" ") + Separate + Begin + Code(" MERGEFIELD Nome ") + Separate + Text("Ada") + End + End);
        var (document, codes) = Read(stream);
        var field = document.Body.Blocks.OfType<ParagraphBlock>().SelectMany(paragraph => paragraph.Content.Runs).Single(run => run.Field != TextFieldKind.None);
        Assert.Equal("Ada", field.Text);
        Assert.DoesNotContain("DOCX161", codes);
        Assert.DoesNotContain("DOCX160", codes);
    }
}
