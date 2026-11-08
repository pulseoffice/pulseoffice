using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DocxShapeStoryTests
{
    private readonly DirectFormatting _formatting = new();
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private TextDocument RoundTrip(TextDocument document)
    {
        using var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, stream).Succeeded);
        stream.Position = 0;
        return _composer.Reader(DocxFlavour.Document).Read(stream).Value!;
    }

    [Fact]
    public void IlTestoRiccoDiUnaFormaAttraversaIlDocx()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Body.Replace([new ParagraphBlock("p1", "Corpo") { Marks = [new ObjectMark(0, "o1")] }]);
        var bold = _formatting.Set(new TextRun("Grassetto"), new CharacterProperties { Bold = true });
        var story = new BlockContainer(
        [
            new ParagraphBlock("s1", new TextParagraph(string.Empty).WithRuns([bold, new TextRun(" normale")])),
            new ParagraphBlock("s2", "Seconda riga"),
        ]);
        document.Objects.Add(new AnchoredObject("o1", new CharacterAnchor("p1"),
            new EmbeddedContent(new ShapeObject { Id = "e1", Shape = ShapeKind.Ellipse }, story),
            new DocumentSize(Twip.FromCentimeters(6), Twip.FromCentimeters(4))));

        var read = RoundTrip(document);

        var content = Assert.IsType<EmbeddedContent>(Assert.Single(read.Objects.All).Content);
        var shape = Assert.IsType<ShapeObject>(content.Value);
        Assert.Equal(ShapeKind.Ellipse, shape.Shape);
        Assert.Null(shape.Text);
        var paragraphs = Assert.IsType<BlockContainer>(content.Story).Blocks.OfType<ParagraphBlock>().ToList();
        Assert.Equal(["Grassetto normale", "Seconda riga"], paragraphs.Select(paragraph => paragraph.Text));
        Assert.True(_formatting.Run(paragraphs[0].Content.Runs[0]).Bold);
        Assert.NotEqual(true, _formatting.Run(paragraphs[0].Content.Runs[^1]).Bold);
    }
}
