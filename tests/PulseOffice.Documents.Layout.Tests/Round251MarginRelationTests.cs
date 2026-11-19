using PulseOffice.Documents.Layout.Flow;
using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Documents.Objects;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public class Round251MarginRelationTests
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
    public void Right_margin_offsets_start_at_the_text_area_end()
    {
        var item = Placed(new(Twip.FromPoints(4), Twip.Zero) { Horizontal = HorizontalRelation.EndMargin });
        var centred = Placed(new(Twip.Zero, Twip.Zero) { Horizontal = HorizontalRelation.EndMargin, HorizontalAlign = ObjectAlignment.Center });

        Assert.Equal(532, _placement.Place(item, _frame).X, 3);
        Assert.Equal(554, _placement.Place(centred, _frame).X, 3);
    }

    [Fact]
    public void Bottom_margin_offsets_start_at_the_text_area_bottom()
    {
        var item = Placed(new(Twip.Zero, Twip.FromPoints(6)) { Vertical = VerticalRelation.BottomMargin });
        var bottom = Placed(new(Twip.Zero, Twip.Zero) { Vertical = VerticalRelation.BottomMargin, VerticalAlign = ObjectAlignment.End });

        Assert.Equal(734, _placement.Place(item, _frame).Y, 3);
        Assert.Equal(790, _placement.Place(bottom, _frame).Y, 3);
    }
}
