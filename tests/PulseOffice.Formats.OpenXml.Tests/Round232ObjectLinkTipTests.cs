using PulseOffice.Documents;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round232ObjectLinkTipTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private TextDocument Folio()
    {
        var document = new TextDocument();
        var paragraph = new PulseOffice.Documents.Blocks.ParagraphBlock("p1", "Text");
        document.Body.Replace([paragraph]);
        var shape = new PulseOffice.Drawing.Shapes.ShapeObject { Id = "s1", Shape = PulseOffice.Drawing.Shapes.ShapeKind.Rectangle };
        document.Objects.Add(new AnchoredObject("obj1", new ParagraphAnchor(paragraph.Id), new EmbeddedContent(shape), new DocumentSize(new Twip(1440), new Twip(1440)))
        {
            Hyperlink = "https://example.org/",
            HyperlinkTip = "Shape tip",
        });
        return document;
    }

    private TextDocument Through(TextDocument document, ITextDocumentWriter writer, ITextDocumentReader reader)
    {
        using var stream = new MemoryStream();
        Assert.True(writer.Write(document, stream).Succeeded);
        stream.Position = 0;
        return reader.Read(stream).Value!;
    }

    [Fact]
    public void A_folio_object_link_tip_crosses_docx_and_odt()
    {
        Assert.Equal("Shape tip", Through(Folio(), _composer.Writer(DocxFlavour.Document), _composer.Reader(DocxFlavour.Document)).Objects.All.Single().HyperlinkTip);
        Assert.Equal("Shape tip", Through(Folio(), new OdtWriter(), new OdtReader()).Objects.All.Single().HyperlinkTip);
    }

    private Workbook Book()
    {
        var workbook = new Workbook();
        workbook.AddSheet("Data").Objects.Add(new PlacedObject
        {
            Id = "shape",
            Anchor = new CellAddress(1, 1),
            Size = new DocumentSize(Twip.FromCentimeters(3), Twip.FromCentimeters(2)),
            Content = new PulseOffice.Drawing.Shapes.ShapeObject { Id = "shape", Shape = PulseOffice.Drawing.Shapes.ShapeKind.Ellipse },
            Hyperlink = "https://example.org/",
            HyperlinkTip = "Object tip",
        });
        return workbook;
    }

    [Fact]
    public void A_ledger_object_link_tip_crosses_xlsx_and_ods()
    {
        using var xlsx = new MemoryStream();
        new XlsxWriter().Write(Book(), xlsx);
        xlsx.Position = 0;
        Assert.Equal("Object tip", new XlsxReader().Read(xlsx).Value!.Sheets[0].Objects.Single().HyperlinkTip);

        using var ods = new MemoryStream();
        new OdsWriter().Write(Book(), ods);
        ods.Position = 0;
        Assert.Equal("Object tip", new OdsReader().Read(ods).Value!.Sheets[0].Objects.Single().HyperlinkTip);
    }
}
