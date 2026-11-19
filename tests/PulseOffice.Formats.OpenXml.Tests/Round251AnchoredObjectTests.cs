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

public sealed class Round251AnchoredObjectTests
{
    private const string Namespaces = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\" xmlns:wp=\"http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" xmlns:wps=\"http://schemas.microsoft.com/office/word/2010/wordprocessingShape\"";

    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private static string Shape(int id, int height, string horizontal = "page", string vertical = "page") =>
        $"<w:r><w:drawing><wp:anchor distT=\"0\" distB=\"0\" distL=\"0\" distR=\"0\" simplePos=\"0\" relativeHeight=\"{height}\" behindDoc=\"0\" locked=\"0\" layoutInCell=\"1\" allowOverlap=\"1\"><wp:simplePos x=\"0\" y=\"0\"/>"
        + $"<wp:positionH relativeFrom=\"{horizontal}\"><wp:posOffset>0</wp:posOffset></wp:positionH><wp:positionV relativeFrom=\"{vertical}\"><wp:posOffset>0</wp:posOffset></wp:positionV>"
        + $"<wp:extent cx=\"914400\" cy=\"914400\"/><wp:wrapNone/><wp:docPr id=\"{id}\" name=\"s{id}\"/><a:graphic><a:graphicData uri=\"http://schemas.microsoft.com/office/word/2010/wordprocessingShape\"><wps:wsp><wps:cNvSpPr/><wps:spPr><a:prstGeom prst=\"rect\"/></wps:spPr><wps:bodyPr/></wps:wsp></a:graphicData></a:graphic></wp:anchor></w:drawing></w:r>";

    private TextDocument Read(string runs)
    {
        var source = new MemoryStream();
        using (var package = WordprocessingDocument.Create(source, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            using var xml = new MemoryStream(Encoding.UTF8.GetBytes($"<w:document {Namespaces}><w:body><w:p>{runs}</w:p></w:body></w:document>"));
            package.AddMainDocumentPart().FeedData(xml);
        }
        source.Position = 0;
        return _composer.Reader(DocxFlavour.Document).Read(source).Value!;
    }

    [Fact]
    public void Small_relative_heights_keep_their_stacking_order()
    {
        var document = Read(Shape(1, 3) + Shape(2, 1) + Shape(3, 2));

        var byName = document.Objects.All.ToDictionary(item => item.Name!, item => item.ZOrder);
        Assert.True(byName["s2"] < byName["s3"]);
        Assert.True(byName["s3"] < byName["s1"]);
    }

    [Fact]
    public void Right_and_bottom_margin_relations_are_read()
    {
        var item = Assert.Single(Read(Shape(1, 1024, "rightMargin", "bottomMargin")).Objects.All);

        Assert.Equal(HorizontalRelation.EndMargin, item.Placement.Horizontal);
        Assert.Equal(VerticalRelation.BottomMargin, item.Placement.Vertical);
    }

    [Fact]
    public void Right_and_bottom_margin_relations_round_trip()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Styles.Page.Replace(new PageStyle(DefaultStyleNames.Standard));
        document.Body.Add(new ParagraphBlock("p1", "text", DefaultStyleNames.Standard));
        document.Objects.Add(new AnchoredObject("o1", new ParagraphAnchor("p1"), new EmbeddedContent(new ShapeObject { Id = "s", Shape = ShapeKind.Rectangle }),
            new DocumentSize(Twip.FromPoints(20), Twip.FromPoints(20)))
        {
            Placement = new ObjectPlacement(Twip.FromPoints(4), Twip.FromPoints(6)) { Horizontal = HorizontalRelation.EndMargin, Vertical = VerticalRelation.BottomMargin },
        });
        using var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, stream).Succeeded);
        stream.Position = 0;

        var item = Assert.Single(_composer.Reader(DocxFlavour.Document).Read(stream).Value!.Objects.All);

        Assert.Equal(document.Objects.All[0].Placement, item.Placement);
    }
}
