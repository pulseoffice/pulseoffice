using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Objects;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class ShapeStoryLayoutTests
{
    private double Offset(TextVerticalPlacement vertical, string anchorKind)
    {
        var f = new LayoutFixture(width: 400, height: 400);
        var block = f.Add("testo");
        var story = new BlockContainer([new ParagraphBlock("sp", new TextParagraph("ciao") { Runs = [new TextRun("ciao")] })]);
        var shape = new ShapeObject { Id = "s", Shape = ShapeKind.Rectangle, Style = new ShapeStyle { Text = new ShapeTextStyle { Vertical = vertical } } };
        ObjectAnchor anchor = anchorKind switch
        {
            "page" => new PageAnchor(1),
            "inline" => new AsCharacterAnchor(block.Id),
            _ => new ParagraphAnchor(block.Id),
        };
        if (anchorKind == "inline") block.Marks = [new PulseOffice.Documents.Marks.ObjectMark(0, "o")];
        f.Document.Objects.Add(new AnchoredObject("o", anchor, new EmbeddedContent(shape, story), new DocumentSize(Twip.FromPoints(120), Twip.FromPoints(100)))
        {
            Placement = new ObjectPlacement(Twip.FromPoints(100), Twip.FromPoints(50)) { Horizontal = HorizontalRelation.Page, Vertical = VerticalRelation.Page },
            Wrap = WrapMode.Parallel,
        });
        var box = f.Layout().Pages[0].Objects.Single();
        var line = Assert.Single(box.Lines);
        return line.Top + line.Line.Height / 2 - (box.Bounds.Y + box.Bounds.Height / 2);
    }

    [Theory]
    [InlineData("paragraph")]
    [InlineData("page")]
    [InlineData("inline")]
    public void IlTestoDellaFormaStaAlCentroInVerticale(string anchor)
    {
        Assert.InRange(Offset(TextVerticalPlacement.Middle, anchor), -1, 1);
        Assert.True(Offset(TextVerticalPlacement.Top, anchor) < -20);
        Assert.True(Offset(TextVerticalPlacement.Bottom, anchor) > 20);
    }

    private uint? Ink(string fillHex, uint? own)
    {
        var f = new LayoutFixture(width: 400, height: 400);
        var block = f.Add("testo");
        var story = new BlockContainer([new ParagraphBlock("sp", new TextParagraph("ciao") { Runs = [new TextRun("ciao") { Color = own }] })]);
        var shape = new ShapeObject { Id = "s", Shape = ShapeKind.Rectangle, Style = new ShapeStyle { Fill = new ShapeFill { ColorHex = fillHex } } };
        f.Document.Objects.Add(new AnchoredObject("o", new ParagraphAnchor(block.Id), new EmbeddedContent(shape, story), new DocumentSize(Twip.FromPoints(120), Twip.FromPoints(100)))
        {
            Placement = new ObjectPlacement(Twip.FromPoints(100), Twip.FromPoints(50)) { Horizontal = HorizontalRelation.Page, Vertical = VerticalRelation.Page },
            Wrap = WrapMode.Parallel,
        });
        var line = Assert.Single(f.Layout().Pages[0].Objects.Single().Lines);
        return line.Line.Runs.First(item => item.Kind == PulseOffice.Documents.Layout.Tree.GlyphRunKind.Text).Style!.Color;
    }

    [Fact]
    public void IlTestoAutomaticoDiUnaFormaScuraDiventaChiaroEQuelloConColoreProprioResta()
    {
        Assert.Equal(0xFFFFFFFFu, Ink("#1F2937", null));
        Assert.Null(Ink("#FFF3B0", null));
        Assert.Equal(0xFFFF0000u, Ink("#1F2937", 0xFFFF0000u));
    }
}
