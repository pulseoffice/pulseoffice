using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DocxFloatingTableTests
{
    private const string Namespace = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private MemoryStream Package()
    {
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            main.Document = new W.Document($"<w:document {Namespace}><w:body>"
                + "<w:p><w:r><w:t>Prima</w:t></w:r></w:p>"
                + "<w:tbl><w:tblPr><w:tblpPr w:leftFromText=\"180\" w:rightFromText=\"180\" w:topFromText=\"0\" w:bottomFromText=\"0\" w:vertAnchor=\"text\" w:horzAnchor=\"margin\" w:tblpX=\"2880\" w:tblpY=\"360\"/><w:tblW w:w=\"0\" w:type=\"auto\"/></w:tblPr>"
                + "<w:tblGrid><w:gridCol w:w=\"1440\"/><w:gridCol w:w=\"1440\"/></w:tblGrid>"
                + "<w:tr><w:tc><w:p><w:r><w:t>a</w:t></w:r></w:p></w:tc><w:tc><w:p><w:r><w:t>b</w:t></w:r></w:p></w:tc></w:tr></w:tbl>"
                + "<w:p><w:r><w:t>Testo che scorre accanto</w:t></w:r></w:p>"
                + "</w:body></w:document>");
        }

        stream.Position = 0;
        return stream;
    }

    private TextDocument Through(TextDocument document, ITextDocumentWriter writer, ITextDocumentReader reader)
    {
        using var stream = new MemoryStream();
        Assert.True(writer.Write(document, stream).Succeeded);
        stream.Position = 0;
        return reader.Read(stream).Value!;
    }

    private void AssertFloating(TextDocument document)
    {
        Assert.Empty(document.Body.Blocks.OfType<TableBlock>());
        var anchor = document.Body.Blocks.OfType<ParagraphBlock>().Single(paragraph => paragraph.Text == "Testo che scorre accanto");
        var item = Assert.Single(document.Objects.All);
        Assert.Equal(new ParagraphAnchor(anchor.Id), item.Anchor);
        Assert.Equal(WrapMode.Parallel, item.Wrap);
        Assert.Equal(new ObjectPlacement(new Foundation.Units.Twip(2880), new Foundation.Units.Twip(360)) { Horizontal = HorizontalRelation.PageArea, Vertical = VerticalRelation.Paragraph }, item.Placement);
        var table = Assert.IsType<TableBlock>(Assert.Single(Assert.IsType<TextBoxContent>(item.Content).Content.Blocks));
        Assert.Equal(2, table.ColumnCount);
        Assert.Equal(2880, item.Size.Width.Value);
    }

    [Fact]
    public void Una_tabella_fluttuante_di_Word_diventa_una_cornice_ancorata_e_torna_tabella_fluttuante()
    {
        var document = _composer.Reader(DocxFlavour.Document).Read(Package()).Value!;
        AssertFloating(document);
        AssertFloating(Through(document, new OdtWriter(), new OdtReader()));

        using var written = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, written).Succeeded);
        written.Position = 0;
        using (var package = WordprocessingDocument.Open(written, false))
        {
            var body = package.MainDocumentPart!.Document!.Body!;
            var table = Assert.Single(body.Elements<W.Table>());
            Assert.Equal(2880, table.GetFirstChild<W.TableProperties>()!.TablePositionProperties!.TablePositionX!.Value);
            Assert.DoesNotContain(body.Descendants(), element => element.LocalName == "txbxContent");
        }
        written.Position = 0;
        AssertFloating(_composer.Reader(DocxFlavour.Document).Read(written).Value!);
    }
}
