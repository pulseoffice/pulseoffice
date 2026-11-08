using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets.Addressing;
using Xunit;

namespace PulseOffice.Sheets.Tests;

public class ObjectAnchoringRound112Tests
{
    private static readonly Twip Row = Twip.FromPoints(15);

    private Worksheet Sheet(ObjectAnchoring anchoring)
    {
        var sheet = new Worksheet("A");
        sheet.Objects.Add(new PlacedObject
        {
            Id = "s1",
            Content = new ShapeObject { Id = "s1", Shape = ShapeKind.Rectangle },
            Anchor = new CellAddress(2, 1),
            Size = new DocumentSize(Twip.FromPoints(60), Row * 3),
            Anchoring = anchoring,
        });
        return sheet;
    }

    [Fact]
    public void Move_and_size_objects_grow_with_a_row_inside_them()
    {
        var sheet = Sheet(ObjectAnchoring.MoveAndSize);

        sheet.Layout.SetRowHeight(3, Row * 3);

        Assert.Equal((Row * 5).Value, sheet.Objects[0].Size.Height.Value);
        Assert.Equal(new CellAddress(2, 1), sheet.Objects[0].Anchor);
    }

    [Fact]
    public void Move_and_size_objects_follow_a_row_resized_above_them()
    {
        var sheet = Sheet(ObjectAnchoring.MoveAndSize);

        sheet.Layout.SetRowHeight(0, Row * 4);

        Assert.Equal((Row * 3).Value, sheet.Objects[0].Size.Height.Value);
        Assert.Equal(new CellAddress(2, 1), sheet.Objects[0].Anchor);
    }

    [Fact]
    public void Move_objects_keep_their_size()
    {
        var sheet = Sheet(ObjectAnchoring.Move);

        sheet.Layout.SetRowHeight(3, Row * 3);

        Assert.Equal((Row * 3).Value, sheet.Objects[0].Size.Height.Value);
    }

    [Fact]
    public void Fixed_objects_keep_their_place_on_the_sheet()
    {
        var sheet = Sheet(ObjectAnchoring.Fixed);
        var top = sheet.Layout.RowOffset(2).Value;

        sheet.Layout.SetRowHeight(0, Row * 3);

        var placed = sheet.Objects[0];
        Assert.Equal(top, sheet.Layout.RowOffset(placed.Anchor.Row).Value + placed.OffsetY.Value);
        Assert.Equal((Row * 3).Value, placed.Size.Height.Value);
        Assert.Equal(0, placed.Anchor.Row);
    }

    [Fact]
    public void Hiding_the_rows_of_a_move_and_size_object_hides_it_and_showing_them_brings_it_back()
    {
        var sheet = Sheet(ObjectAnchoring.MoveAndSize);

        sheet.Layout.HideRows(2, 4);
        Assert.Equal(0, sheet.Objects[0].Size.Height.Value);

        sheet.Layout.ShowRows(2, 4);
        Assert.Equal((Row * 3).Value, sheet.Objects[0].Size.Height.Value);
    }

    [Fact]
    public void Hiding_one_row_of_a_move_and_size_object_shrinks_it()
    {
        var sheet = Sheet(ObjectAnchoring.MoveAndSize);

        sheet.Layout.HideRows(3, 3);

        Assert.Equal((Row * 2).Value, sheet.Objects[0].Size.Height.Value);
    }

    [Fact]
    public void Suspended_notifications_leave_objects_alone()
    {
        var sheet = Sheet(ObjectAnchoring.MoveAndSize);

        using (sheet.Layout.SuspendGeometryNotifications()) sheet.Layout.SetRowHeight(3, Row * 3);

        Assert.Equal((Row * 3).Value, sheet.Objects[0].Size.Height.Value);
    }

    [Fact]
    public void Column_widths_resize_move_and_size_objects()
    {
        var sheet = Sheet(ObjectAnchoring.MoveAndSize);
        var width = sheet.Objects[0].Size.Width.Value;

        sheet.Layout.SetColumnWidth(1, sheet.Layout.ColumnWidth(1) + Twip.FromPoints(30));

        Assert.Equal(width + Twip.FromPoints(30).Value, sheet.Objects[0].Size.Width.Value);
    }
}
