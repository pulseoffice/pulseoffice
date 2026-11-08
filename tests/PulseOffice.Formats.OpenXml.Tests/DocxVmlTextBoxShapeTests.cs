using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DocxVmlTextBoxShapeTests
{
    private const string Namespaces = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\" xmlns:v=\"urn:schemas-microsoft-com:vml\"";

    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private static MemoryStream Package(string pict)
    {
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
            package.AddMainDocumentPart().Document = new W.Document($"<w:document {Namespaces}><w:body><w:p><w:r><w:pict>{pict}</w:pict></w:r></w:p></w:body></w:document>");
        stream.Position = 0;
        return stream;
    }

    private TextDocument Read(Stream stream)
    {
        var read = _composer.Reader(DocxFlavour.Document).Read(stream);
        Assert.True(read.Succeeded);
        return read.Value!;
    }

    private static void AssertShape(TextDocument document, ShapeKind kind)
    {
        var item = Assert.Single(document.Objects.All);
        var content = Assert.IsType<EmbeddedContent>(item.Content);
        var shape = Assert.IsType<ShapeObject>(content.Value);
        Assert.Equal(kind, shape.Shape);
        Assert.Equal("#FFCC00", shape.Style.Fill.ColorHex, StringComparer.OrdinalIgnoreCase);
        Assert.True(shape.Style.Line.IsVisible);
        Assert.Equal("#FF0000", shape.Style.Line.ColorHex, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("Ciao forma", Assert.IsType<ParagraphBlock>(content.Story![0]).Text);
    }

    [Theory]
    [InlineData("rect", ShapeKind.Rectangle)]
    [InlineData("oval", ShapeKind.Ellipse)]
    public void UnaFormaVmlConCasellaFuoriDaUnGruppoSiLeggeConTestoColoriEBordo(string element, ShapeKind kind)
    {
        using var stream = Package($"<v:{element} style=\"position:absolute;margin-left:10pt;margin-top:5pt;width:120pt;height:60pt\" fillcolor=\"#ffcc00\" strokecolor=\"red\" strokeweight=\"2pt\">"
            + "<v:textbox><w:txbxContent><w:p><w:r><w:t>Ciao forma</w:t></w:r></w:p></w:txbxContent></v:textbox></v:" + element + ">");
        var document = Read(stream);
        AssertShape(document, kind);

        using var docx = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, docx).Succeeded);
        docx.Position = 0;
        AssertShape(Read(docx), kind);

        using var odt = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, odt).Succeeded);
        odt.Position = 0;
        var back = new OdtReader().Read(odt);
        Assert.True(back.Succeeded);
        AssertShape(back.Value!, kind);
    }

    [Fact]
    public void UnaCasellaVmlSenzaRiempimentoNeBordoRestaUnaCasellaDiTesto()
    {
        using var stream = Package("<v:shape style=\"width:120pt;height:60pt\" filled=\"f\" stroked=\"f\"><v:textbox><w:txbxContent><w:p><w:r><w:t>Solo testo</w:t></w:r></w:p></w:txbxContent></v:textbox></v:shape>");
        var item = Assert.Single(Read(stream).Objects.All);
        Assert.IsType<TextBoxContent>(item.Content);
    }
}
