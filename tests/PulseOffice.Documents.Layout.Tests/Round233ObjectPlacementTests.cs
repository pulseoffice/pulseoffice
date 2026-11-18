using PulseOffice.Documents.Layout.Flow;
using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Documents.Objects;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public class Round233ObjectPlacementTests
{
    private readonly ObjectFrame _frame = new(new PageGeometry("Standard", 600, 800, new LayoutRect(72, 72, 456, 656), "Standard"), 72, 456, 100);

    private static AnchoredObject Placed(HorizontalRelation relation) =>
        new("o", new ParagraphAnchor("p"), new TextBoxContent(new PulseOffice.Documents.Blocks.BlockContainer([])), new DocumentSize(Twip.FromPoints(20), Twip.FromPoints(20)))
        {
            Placement = new PulseOffice.Documents.Objects.ObjectPlacement(Twip.FromPoints(10), Twip.Zero) { Horizontal = relation },
        };

    [Fact]
    public void A_position_relative_to_the_left_margin_is_measured_from_the_page_edge()
    {
        var placement = new PulseOffice.Documents.Layout.Flow.ObjectPlacement();

        Assert.Equal(10, placement.Place(Placed(HorizontalRelation.Margin), _frame).X, 3);
        Assert.Equal(82, placement.Place(Placed(HorizontalRelation.PageArea), _frame).X, 3);
    }

    [Fact]
    public void A_centred_object_is_centred_in_its_relation_area()
    {
        var item = Placed(HorizontalRelation.PageArea);
        item.Placement = item.Placement with { HorizontalAlign = ObjectAlignment.Center, Vertical = VerticalRelation.Page, VerticalAlign = ObjectAlignment.End };

        var rect = new PulseOffice.Documents.Layout.Flow.ObjectPlacement().Place(item, _frame);

        Assert.Equal(72 + (456 - 20) / 2.0, rect.X, 3);
        Assert.Equal(780, rect.Y, 3);
    }
}
