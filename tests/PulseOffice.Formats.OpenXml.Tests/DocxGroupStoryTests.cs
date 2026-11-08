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

public sealed class DocxGroupStoryTests
{
    private readonly DirectFormatting _formatting = new();
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private TextDocument RoundTrip(TextDocument document)
    {
        using var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, stream).Succeeded);
        stream.Position = 0;
        var read = _composer.Reader(DocxFlavour.Document).Read(stream);
        Assert.True(read.Succeeded);
        return read.Value!;
    }

    internal static TextDocument Grouped(DirectFormatting formatting)
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Body.Replace([new ParagraphBlock("p1", "Corpo") { Marks = [new ObjectMark(0, "g1")] }]);
        var bold = formatting.Set(new TextRun("Grassetto"), new CharacterProperties { Bold = true });
        var story = new BlockContainer([new ParagraphBlock("s1", new TextParagraph(string.Empty).WithRuns([bold, new TextRun(" e tondo")])), new ParagraphBlock("s2", "Seconda")]);
        var filled = new ShapeObject { Id = "a", Shape = ShapeKind.Rectangle, Style = new ShapeStyle { Fill = new ShapeFill { ColorHex = "#FFFF00" } }, Text = "Grassetto e tondo\nSeconda" };
        var plain = new ShapeObject { Id = "b", Shape = ShapeKind.Ellipse };
        var group = new ShapeGroupObject { Id = "grp", Children = [new GroupChild(filled, 0, 0, 0.5, 1), new GroupChild(plain, 0.5, 0, 0.5, 1)] };
        document.Objects.Add(new AnchoredObject("g1", new CharacterAnchor("p1"),
            new EmbeddedContent(group, null, new Dictionary<string, BlockContainer> { ["a"] = story }),
            new DocumentSize(Twip.FromCentimeters(8), Twip.FromCentimeters(3))));
        return document;
    }

    internal static void AssertRich(TextDocument read, DirectFormatting formatting)
    {
        var content = Assert.IsType<EmbeddedContent>(Assert.Single(read.Objects.All).Content);
        var group = Assert.IsType<ShapeGroupObject>(content.Value);
        Assert.Equal(2, group.Children.Count);
        var filled = group.Children.Select(child => child.Content).OfType<ShapeObject>().Single(shape => shape.Shape == ShapeKind.Rectangle);
        Assert.Equal("#FFFF00", filled.Style.Fill.ColorHex, ignoreCase: true);
        var story = content.MemberStories[filled.Id];
        var paragraphs = story.Blocks.OfType<ParagraphBlock>().ToList();
        Assert.Equal(["Grassetto e tondo", "Seconda"], paragraphs.Select(paragraph => paragraph.Text));
        Assert.True(formatting.Run(paragraphs[0].Content.Runs[0]).Bold);
        Assert.Contains("Grassetto", filled.Text);
    }

    [Fact]
    public void IlTestoRiccoDelleFormeNeiGruppiAttraversaIlDocx() => AssertRich(RoundTrip(Grouped(_formatting)), _formatting);

    [Fact]
    public void SeparandoIlGruppoLaFormaRiprendeLaSuaStoria()
    {
        var document = Grouped(_formatting);
        var item = document.Objects.All.Single();
        item.Anchor = new PageAnchor(1);
        var ids = new ObjectGrouping(new PortableObjects(), new GroupStories()).Ungroup(document, new PulseOffice.Document.Commands.UndoStack(), item.Id, new PlacedBox(item.Id, 0, 0, 200, 80), 0);

        Assert.Equal(2, ids.Count);
        var released = document.Objects.All.Single(other => other.Content is EmbeddedContent { Value: ShapeObject { Shape: ShapeKind.Rectangle } });
        Assert.Null(((ShapeObject)((EmbeddedContent)released.Content).Value).Text);
        Assert.Equal("Seconda", ((ParagraphBlock)released.Content.Story![1]).Text);
    }

    [Theory]
    [InlineData(TextVerticalPlacement.Middle)]
    [InlineData(TextVerticalPlacement.Bottom)]
    public void LaFormaConStoriaTieneColoreEAncoraggioVerticale(TextVerticalPlacement vertical)
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Body.Replace([new ParagraphBlock("p1", "Corpo") { Marks = [new ObjectMark(0, "o1")] }]);
        var shape = new ShapeObject { Id = "e1", Shape = ShapeKind.Rectangle, Style = new ShapeStyle { Text = new ShapeTextStyle { Vertical = vertical } } };
        document.Objects.Add(new AnchoredObject("o1", new CharacterAnchor("p1"), new EmbeddedContent(shape, new BlockContainer([new ParagraphBlock("s1", "Ciao")])),
            new DocumentSize(Twip.FromCentimeters(4), Twip.FromCentimeters(3))));

        var read = Assert.IsType<ShapeObject>(Assert.IsType<EmbeddedContent>(Assert.Single(RoundTrip(document).Objects.All).Content).Value);

        Assert.Equal(vertical, read.Style.Text.Vertical);
        Assert.True(read.Style.Fill.IsVisible);
        Assert.Equal("#2563EB", read.Style.Fill.ColorHex, ignoreCase: true);
        Assert.True(read.Style.Line.IsVisible);
    }
}
