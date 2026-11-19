using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Objects;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class Round272ShapeResizeLayoutTests
{
    private double Height(TextAutofit fit)
    {
        var f = new LayoutFixture(width: 400, height: 400);
        var block = f.Add("testo");
        var story = new BlockContainer([new ParagraphBlock("sp", new TextParagraph("ciao") { Runs = [new TextRun("ciao")] })]);
        var shape = new ShapeObject { Id = "s", Shape = ShapeKind.Rectangle, Style = new ShapeStyle { Text = new ShapeTextStyle { Autofit = fit } } };
        f.Document.Objects.Add(new AnchoredObject("o", new ParagraphAnchor(block.Id), new EmbeddedContent(shape, story), new DocumentSize(Twip.FromPoints(120), Twip.FromPoints(100)))
        {
            Placement = new ObjectPlacement(Twip.FromPoints(100), Twip.FromPoints(50)) { Horizontal = HorizontalRelation.Page, Vertical = VerticalRelation.Page },
            Wrap = WrapMode.Parallel,
        });
        return f.Layout().Pages[0].Objects.Single().Bounds.Height;
    }

    [Fact]
    public void A_shape_that_resizes_to_its_text_takes_the_height_of_its_story()
    {
        Assert.Equal(100, Height(TextAutofit.None), 3);
        Assert.True(Height(TextAutofit.ResizeShape) < 50);
    }
}
