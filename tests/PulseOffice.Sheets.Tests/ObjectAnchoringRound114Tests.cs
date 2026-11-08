using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets.Addressing;
using Xunit;

namespace PulseOffice.Sheets.Tests;

public class ObjectAnchoringRound114Tests
{
    private static readonly Twip Row = Twip.FromPoints(15);
    private static readonly Twip Inset = Twip.FromPoints(5);

    private Worksheet Sheet(ObjectAnchoring anchoring)
    {
        var sheet = new Worksheet("A");
        sheet.Objects.Add(new PlacedObject
        {
            Id = "s1",
            Content = new ShapeObject { Id = "s1", Shape = ShapeKind.Rectangle },
            Anchor = new CellAddress(2, 1),
            OffsetX = Inset,
            OffsetY = Inset,
            Size = new DocumentSize(Twip.FromPoints(60), Row * 3),
            Anchoring = anchoring,
        });
        return sheet;
    }

    [Theory]
    [InlineData(ObjectAnchoring.MoveAndSize)]
    [InlineData(ObjectAnchoring.Move)]
    [InlineData(ObjectAnchoring.Fixed)]
    public void Hiding_and_showing_the_anchor_row_keeps_the_offset_inside_it(ObjectAnchoring anchoring)
    {
        var sheet = Sheet(anchoring);
        var before = sheet.Objects[0];

        sheet.Layout.HideRows(2, 2);
        sheet.Layout.ShowRows(2, 2);

        var after = sheet.Objects[0];
        Assert.Equal(before.Anchor, after.Anchor);
        Assert.Equal(before.OffsetY.Value, after.OffsetY.Value);
        Assert.Equal(before.Size.Height.Value, after.Size.Height.Value);
    }

    [Fact]
    public void Hiding_and_showing_the_last_row_keeps_the_end_offset()
    {
        var sheet = Sheet(ObjectAnchoring.MoveAndSize);
        var before = sheet.Objects[0];

        sheet.Layout.HideRows(5, 5);
        sheet.Layout.ShowRows(5, 5);

        Assert.Equal(before.Size.Height.Value, sheet.Objects[0].Size.Height.Value);
    }

    [Fact]
    public void Hiding_and_showing_the_anchor_column_keeps_the_offset_inside_it()
    {
        var sheet = Sheet(ObjectAnchoring.MoveAndSize);
        var before = sheet.Objects[0];

        sheet.Layout.HideColumns(1, 1);
        sheet.Layout.ShowColumns(1, 1);

        Assert.Equal(before.OffsetX.Value, sheet.Objects[0].OffsetX.Value);
        Assert.Equal(before.Size.Width.Value, sheet.Objects[0].Size.Width.Value);
    }

    [Fact]
    public void A_filter_hiding_and_showing_the_anchor_row_keeps_the_offset()
    {
        var sheet = Sheet(ObjectAnchoring.MoveAndSize);
        var before = sheet.Objects[0];

        sheet.Layout.ApplyFilteredRows([2]);
        sheet.Layout.ApplyFilteredRows(null);

        Assert.Equal(before, sheet.Objects[0]);
    }
}
