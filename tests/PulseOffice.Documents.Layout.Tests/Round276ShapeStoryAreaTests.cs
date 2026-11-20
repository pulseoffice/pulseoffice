using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Objects;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class Round276ShapeStoryAreaTests
{
    private PulseOffice.Documents.Layout.Tree.ObjectBox Box(ShapeKind kind, TextAutofit autofit, string text, double height)
    {
        var f = new LayoutFixture(width: 400, height: 400);
        var block = f.Add("testo");
        var story = new BlockContainer([new ParagraphBlock("sp", new TextParagraph(text) { Runs = [new TextRun(text) { FontSize = 12 }] })]);
        var shape = new ShapeObject { Id = "s", Shape = kind, Style = new ShapeStyle { Text = new ShapeTextStyle { Autofit = autofit, Vertical = TextVerticalPlacement.Top } } };
        f.Document.Objects.Add(new AnchoredObject("o", new ParagraphAnchor(block.Id), new EmbeddedContent(shape, story), new DocumentSize(Twip.FromPoints(120), Twip.FromPoints(height)))
        {
            Placement = new ObjectPlacement(Twip.FromPoints(100), Twip.FromPoints(50)) { Horizontal = HorizontalRelation.Page, Vertical = VerticalRelation.Page },
            Wrap = WrapMode.Parallel,
        });
        return f.Layout().Pages[0].Objects.Single();
    }

    [Fact]
    public void An_ellipse_story_starts_inside_the_inscribed_rectangle()
    {
        var ellipse = Box(ShapeKind.Ellipse, TextAutofit.None, "ciao", 100);
        var rectangle = Box(ShapeKind.Rectangle, TextAutofit.None, "ciao", 100);

        Assert.True(ellipse.Lines.First().Left - ellipse.Bounds.X >= 120 * 0.14);
        Assert.True(ellipse.Lines.First().Top > rectangle.Lines.First().Top + 10);
    }

    [Fact]
    public void A_shrinking_shape_story_scales_its_text_down_to_fit()
    {
        const string Long = "uno due tre quattro cinque sei sette otto nove dieci undici dodici tredici quattordici quindici sedici";
        var shrunk = Box(ShapeKind.Rectangle, TextAutofit.ShrinkText, Long, 30);
        var plain = Box(ShapeKind.Rectangle, TextAutofit.None, Long, 30);

        double Size(PulseOffice.Documents.Layout.Tree.ObjectBox box) => box.Lines.SelectMany(line => line.Line.Runs).First(run => run.Kind == PulseOffice.Documents.Layout.Tree.GlyphRunKind.Text).Style!.FontSize ?? 0;

        Assert.True(Size(shrunk) < Size(plain));
        Assert.True(shrunk.Lines.Count() < plain.Lines.Count());
    }

    [Fact]
    public void A_resizing_ellipse_is_taller_than_a_resizing_rectangle_with_the_same_story()
    {
        var ellipse = Box(ShapeKind.Ellipse, TextAutofit.ResizeShape, "uno due tre quattro cinque sei sette otto", 20);
        var rectangle = Box(ShapeKind.Rectangle, TextAutofit.ResizeShape, "uno due tre quattro cinque sei sette otto", 20);

        Assert.True(ellipse.Bounds.Height > rectangle.Bounds.Height);
    }

    [Fact]
    public void The_stored_size_of_a_resizing_shape_follows_its_story_once()
    {
        var f = new LayoutFixture(width: 400, height: 400);
        var block = f.Add("testo");
        var story = new BlockContainer([new ParagraphBlock("sp", new TextParagraph("uno due tre quattro cinque sei sette otto") { Runs = [new TextRun("uno due tre quattro cinque sei sette otto")] })]);
        var shape = new ShapeObject { Id = "s", Shape = ShapeKind.Rectangle, Style = new ShapeStyle { Text = new ShapeTextStyle { Autofit = TextAutofit.ResizeShape } } };
        var item = new AnchoredObject("o", new ParagraphAnchor(block.Id), new EmbeddedContent(shape, story), new DocumentSize(Twip.FromPoints(120), Twip.FromPoints(10)));
        f.Document.Objects.Add(item);
        var layout = f.Layout();
        var sizes = new PulseOffice.Documents.Layout.Flow.ShapeStorySizes();

        Assert.True(sizes.Settle(f.Document, layout));
        Assert.Equal(Twip.FromPoints(layout.Pages[0].Objects.Single().Bounds.Height), item.Size.Height);
        Assert.Equal(Twip.FromPoints(120), item.Size.Width);
        Assert.False(sizes.Settle(f.Document, layout));
    }

    [Fact]
    public void The_stored_size_of_an_inline_resizing_shape_follows_its_story()
    {
        var f = new LayoutFixture(width: 400, height: 400);
        var block = f.Add("ab");
        block.Marks = [new ObjectMark(1, "o")];
        var story = new BlockContainer([new ParagraphBlock("sp", new TextParagraph("uno due tre quattro cinque sei sette otto") { Runs = [new TextRun("uno due tre quattro cinque sei sette otto")] })]);
        var shape = new ShapeObject { Id = "s", Shape = ShapeKind.Rectangle, Style = new ShapeStyle { Text = new ShapeTextStyle { Autofit = TextAutofit.ResizeShape } } };
        var item = new AnchoredObject("o", new AsCharacterAnchor(block.Id), new EmbeddedContent(shape, story), new DocumentSize(Twip.FromPoints(60), Twip.FromPoints(10)));
        f.Document.Objects.Add(item);
        var sizes = new PulseOffice.Documents.Layout.Flow.ShapeStorySizes();

        Assert.True(sizes.Settle(f.Document, f.Layout()));
        Assert.True(item.Size.Height.Value > Twip.FromPoints(10).Value);
        Assert.Equal(Twip.FromPoints(60), item.Size.Width);
        Assert.False(sizes.Settle(f.Document, f.Layout()));
    }
}
