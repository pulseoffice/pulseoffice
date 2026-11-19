using PulseOffice.Documents.Layout.Flow;
using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Documents.Objects;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public class Round235ObjectPositionTests
{
    private readonly ObjectFrame _frame = new(new PageGeometry("Standard", 600, 800, new LayoutRect(72, 72, 456, 656), "Standard"), 72, 456, 100)
    {
        ParagraphHeight = 60,
        LineHeight = 20,
    };

    private readonly PulseOffice.Documents.Layout.Flow.ObjectPlacement _placement = new();

    private static AnchoredObject Placed(PulseOffice.Documents.Objects.ObjectPlacement placement) =>
        new("o", new ParagraphAnchor("p"), new TextBoxContent(new PulseOffice.Documents.Blocks.BlockContainer([])), new DocumentSize(Twip.FromPoints(20), Twip.FromPoints(10)))
        {
            Placement = placement,
        };

    [Fact]
    public void The_vertical_margin_relation_is_the_top_margin_area()
    {
        var item = Placed(new(Twip.Zero, Twip.FromPoints(5)) { Vertical = VerticalRelation.Margin });
        var bottom = Placed(new(Twip.Zero, Twip.Zero) { Vertical = VerticalRelation.Margin, VerticalAlign = ObjectAlignment.End });

        Assert.Equal(5, _placement.Place(item, _frame).Y, 3);
        Assert.Equal(62, _placement.Place(bottom, _frame).Y, 3);
    }

    [Fact]
    public void Inside_and_outside_follow_the_page_side()
    {
        var inside = Placed(new(Twip.Zero, Twip.Zero) { Horizontal = HorizontalRelation.Page, HorizontalAlign = ObjectAlignment.Inside });
        var outside = Placed(new(Twip.Zero, Twip.Zero) { Horizontal = HorizontalRelation.Page, HorizontalAlign = ObjectAlignment.Outside });
        var even = _frame with { EvenPage = true };

        Assert.Equal(0, _placement.Place(inside, _frame).X, 3);
        Assert.Equal(580, _placement.Place(inside, even).X, 3);
        Assert.Equal(580, _placement.Place(outside, _frame).X, 3);
        Assert.Equal(0, _placement.Place(outside, even).X, 3);
    }

    [Fact]
    public void Middle_and_bottom_work_relative_to_the_paragraph_and_the_line()
    {
        var paragraph = Placed(new(Twip.Zero, Twip.Zero) { VerticalAlign = ObjectAlignment.End });
        var line = Placed(new(Twip.Zero, Twip.Zero) { Vertical = VerticalRelation.Line, VerticalAlign = ObjectAlignment.Center });

        Assert.Equal(150, _placement.Place(paragraph, _frame).Y, 3);
        Assert.Equal(105, _placement.Place(line, _frame).Y, 3);
    }

    [Fact]
    public void Inside_and_outside_margin_relations_mirror_on_even_pages()
    {
        var inside = Placed(new(Twip.FromPoints(2), Twip.FromPoints(3)) { Horizontal = HorizontalRelation.InsideMargin, Vertical = VerticalRelation.OutsideMargin });
        var even = _frame with { EvenPage = true };

        Assert.Equal(2, _placement.Place(inside, _frame).X, 3);
        Assert.Equal(731, _placement.Place(inside, _frame).Y, 3);
        Assert.Equal(530, _placement.Place(inside, even).X, 3);
        Assert.Equal(3, _placement.Place(inside, even).Y, 3);
    }
}
