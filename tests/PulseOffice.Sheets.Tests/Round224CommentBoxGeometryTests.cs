using PulseOffice.Sheets.Addressing;
using Xunit;

namespace PulseOffice.Sheets.Tests;

public sealed class Round224CommentBoxGeometryTests
{
    private readonly CommentBoxGeometry _geometry = new();

    [Fact]
    public void A_note_box_is_placed_from_the_cell_top_right_corner_and_back()
    {
        var layout = new SheetLayout();
        var address = new CellAddress(2, 1);

        var bounds = _geometry.Bounds(layout, address, new CommentBox(12, 3, 100, 50));

        Assert.Equal(new NoteBounds(132, 33, 100, 50), bounds);
        Assert.Equal(new CommentBox(12, 3, 100, 50), _geometry.Relative(layout, address, bounds));
    }

    [Fact]
    public void A_note_anchor_round_trips_through_columns_and_rows()
    {
        var layout = new SheetLayout();
        var bounds = new NoteBounds(150, 27, 144, 90);

        var anchor = _geometry.Anchor(layout, bounds);

        Assert.Equal(new NoteAnchor(2, 40, 1, 16, 4, 72, 7, 16), anchor);
        var back = _geometry.FromAnchor(layout, anchor);
        Assert.Equal(150, back.X, 6);
        Assert.Equal(27, back.Y, 6);
        Assert.Equal(144, back.Width, 6);
        Assert.Equal(90, back.Height, 6);
    }
}
