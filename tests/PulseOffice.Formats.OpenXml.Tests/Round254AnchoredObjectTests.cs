using System.Text;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round254AnchoredObjectTests
{
    private const string Namespaces = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\" xmlns:wp=\"http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" xmlns:wps=\"http://schemas.microsoft.com/office/word/2010/wordprocessingShape\"";

    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private TextDocument Read(string horizontal, string vertical, string flags)
    {
        var run = $"<w:r><w:drawing><wp:anchor distT=\"0\" distB=\"0\" distL=\"0\" distR=\"0\" simplePos=\"0\" relativeHeight=\"1024\" behindDoc=\"0\" locked=\"0\" {flags}><wp:simplePos x=\"0\" y=\"0\"/>"
            + $"<wp:positionH relativeFrom=\"{horizontal}\"><wp:posOffset>0</wp:posOffset></wp:positionH><wp:positionV relativeFrom=\"{vertical}\"><wp:posOffset>0</wp:posOffset></wp:positionV>"
            + "<wp:extent cx=\"914400\" cy=\"914400\"/><wp:wrapNone/><wp:docPr id=\"1\" name=\"s1\"/><a:graphic><a:graphicData uri=\"http://schemas.microsoft.com/office/word/2010/wordprocessingShape\"><wps:wsp><wps:cNvSpPr/><wps:spPr><a:prstGeom prst=\"rect\"/></wps:spPr><wps:bodyPr/></wps:wsp></a:graphicData></a:graphic></wp:anchor></w:drawing></w:r>";
        var source = new MemoryStream();
        using (var package = WordprocessingDocument.Create(source, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            using var xml = new MemoryStream(Encoding.UTF8.GetBytes($"<w:document {Namespaces}><w:body><w:p>{run}</w:p></w:body></w:document>"));
            package.AddMainDocumentPart().FeedData(xml);
        }
        source.Position = 0;
        return _composer.Reader(DocxFlavour.Document).Read(source).Value!;
    }

    private TextDocument RoundTrip(AnchoredObject item)
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Styles.Page.Replace(new PageStyle(DefaultStyleNames.Standard));
        document.Body.Add(new ParagraphBlock("p1", "text", DefaultStyleNames.Standard));
        document.Objects.Add(item);
        using var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, stream).Succeeded);
        stream.Position = 0;
        return _composer.Reader(DocxFlavour.Document).Read(stream).Value!;
    }

    private static AnchoredObject Shape() => new("o1", new ParagraphAnchor("p1"), new EmbeddedContent(new ShapeObject { Id = "s", Shape = ShapeKind.Rectangle }),
        new DocumentSize(Twip.FromPoints(20), Twip.FromPoints(20)));

    [Fact]
    public void Layout_in_cell_and_allow_overlap_are_read()
    {
        var item = Assert.Single(Read("page", "page", "layoutInCell=\"0\" allowOverlap=\"0\"").Objects.All);

        Assert.False(item.LayoutInCell);
        Assert.False(item.AllowOverlap);
    }

    [Fact]
    public void Layout_in_cell_and_allow_overlap_round_trip()
    {
        var shape = Shape();
        shape.LayoutInCell = false;
        shape.AllowOverlap = false;

        var item = Assert.Single(RoundTrip(shape).Objects.All);

        Assert.False(item.LayoutInCell);
        Assert.False(item.AllowOverlap);
    }

    [Fact]
    public void Inside_and_outside_margin_relations_are_read_as_their_own_relations()
    {
        var item = Assert.Single(Read("insideMargin", "outsideMargin", "layoutInCell=\"1\" allowOverlap=\"1\"").Objects.All);

        Assert.Equal(HorizontalRelation.InsideMargin, item.Placement.Horizontal);
        Assert.Equal(VerticalRelation.OutsideMargin, item.Placement.Vertical);
    }

    [Fact]
    public void Inside_and_outside_margin_relations_round_trip()
    {
        var shape = Shape();
        shape.Placement = new ObjectPlacement(Twip.FromPoints(4), Twip.FromPoints(6)) { Horizontal = HorizontalRelation.OutsideMargin, Vertical = VerticalRelation.InsideMargin };

        var item = Assert.Single(RoundTrip(shape).Objects.All);

        Assert.Equal(shape.Placement, item.Placement);
    }
}
