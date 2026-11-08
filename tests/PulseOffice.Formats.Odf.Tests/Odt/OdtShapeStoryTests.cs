using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using System.IO.Compression;
using System.Xml.Linq;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests.Odt;

public sealed class OdtShapeStoryTests
{
    private readonly DirectFormatting _formatting = new();

    [Fact]
    public void IlTestoRiccoDiUnaFormaAttraversaLOdt()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Body.Replace([new ParagraphBlock("p1", "Corpo") { Marks = [new ObjectMark(0, "o1")] }]);
        var italic = _formatting.Set(new TextRun("Corsivo"), new CharacterProperties { Italic = true });
        var story = new BlockContainer(
        [
            new ParagraphBlock("s1", new TextParagraph(string.Empty).WithRuns([italic, new TextRun(" e tondo")])),
            new ParagraphBlock("s2", "Riga due"),
        ]);
        document.Objects.Add(new AnchoredObject("o1", new CharacterAnchor("p1"),
            new EmbeddedContent(new ShapeObject { Id = "e1", Shape = ShapeKind.Diamond }, story),
            new DocumentSize(Twip.FromCentimeters(6), Twip.FromCentimeters(4))));

        var read = OdtRoundTripTests.RoundTrip(document);

        var content = Assert.IsType<EmbeddedContent>(Assert.Single(read.Objects.All).Content);
        Assert.Equal(ShapeKind.Diamond, Assert.IsType<ShapeObject>(content.Value).Shape);
        var paragraphs = Assert.IsType<BlockContainer>(content.Story).Blocks.OfType<ParagraphBlock>().ToList();
        Assert.Equal(["Corsivo e tondo", "Riga due"], paragraphs.Select(paragraph => paragraph.Text));
        Assert.True(_formatting.Run(paragraphs[0].Content.Runs[0]).Italic);
    }

    [Fact]
    public void LaFormaConTestoDerivaDalloStileCorniceCosiLeAltreSuiteNeLeggonoICampi()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Body.Replace([new ParagraphBlock("p1", "Corpo") { Marks = [new ObjectMark(0, "o1")] }]);
        var story = new BlockContainer([new ParagraphBlock("s1", new TextParagraph("1").WithRuns([new TextRun("1") { Field = TextFieldKind.PageNumber }]))]);
        document.Objects.Add(new AnchoredObject("o1", new CharacterAnchor("p1"),
            new EmbeddedContent(new ShapeObject { Id = "e1", Shape = ShapeKind.Rectangle }, story),
            new DocumentSize(Twip.FromCentimeters(4), Twip.FromCentimeters(3))));

        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        stream.Position = 0;
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, true))
        {
            using var reader = new StreamReader(archive.GetEntry("content.xml")!.Open());
            var content = XDocument.Parse(reader.ReadToEnd());
            XNamespace draw = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";
            XNamespace style = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
            var shape = content.Descendants(draw + "custom-shape").Single();
            var graphic = content.Descendants(style + "style").Single(element => (string?)element.Attribute(style + "name") == (string?)shape.Attribute(draw + "style-name"));
            Assert.Equal("Frame", (string?)graphic.Attribute(style + "parent-style-name"));
        }
        stream.Position = 0;
        var read = new OdtReader().Read(stream).Value!;

        var item = Assert.Single(read.Objects.All);
        Assert.Null(item.StyleName);
        Assert.Equal(ShapeKind.Rectangle, Assert.IsType<ShapeObject>(Assert.IsType<EmbeddedContent>(item.Content).Value).Shape);
        Assert.Contains(Assert.IsType<BlockContainer>(item.Content.Story).Blocks.OfType<ParagraphBlock>().Single().Content.Runs, run => run.Field == TextFieldKind.PageNumber);
    }
    [Fact]
    public void LaGeometriaRegolataInOdtHaIlRiquadroInTwipCosiWriterNonLaRimpicciolisce()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Body.Replace([new ParagraphBlock("p1", "Corpo") { Marks = [new ObjectMark(0, "o1")] }]);
        var geometry = new PulseOffice.Drawing.Shapes.Geometry.PresetGeometry("rightArrow")
        {
            Adjustments = [new("adj1", 50000), new("adj2", 89300)],
        };
        document.Objects.Add(new AnchoredObject("o1", new CharacterAnchor("p1"),
            new EmbeddedContent(new ShapeObject { Id = "e1", Shape = ShapeKind.ArrowRight, FlipHorizontal = true, Computed = geometry }, new BlockContainer([])),
            new DocumentSize(new Twip(2268), new Twip(1701))));

        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        stream.Position = 0;
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, true))
        {
            using var reader = new StreamReader(archive.GetEntry("content.xml")!.Open());
            XNamespace draw = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";
            XNamespace svg = "urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0";
            var enhanced = XDocument.Parse(reader.ReadToEnd()).Descendants(draw + "enhanced-geometry").Single();
            Assert.Equal("0 0 2268 1701", (string?)enhanced.Attribute(svg + "viewBox"));
            Assert.Equal("50000 89300", (string?)enhanced.Attribute(draw + "modifiers"));
        }
        stream.Position = 0;
        var read = new OdtReader().Read(stream).Value!;

        var shape = Assert.IsType<ShapeObject>(Assert.IsType<EmbeddedContent>(Assert.Single(read.Objects.All).Content).Value);
        Assert.True(shape.FlipHorizontal);
        var back = Assert.IsType<PulseOffice.Drawing.Shapes.Geometry.PresetGeometry>(shape.Computed);
        Assert.Equal([new("adj1", 50000), new("adj2", 89300)], back.Adjustments);
    }
}
