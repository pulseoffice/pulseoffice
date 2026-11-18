using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;

namespace PulseOffice.Sheets.Tests;

public sealed class Round218SheetConnectorRoutingTests
{
    private readonly ISheetConnectors _connectors = new SheetConnectors();
    private readonly ISheetPlacement _placement = new SheetPlacement();

    private Worksheet SheetWithTwo()
    {
        var sheet = new Worksheet("Data");
        foreach (var (id, row, column) in new[] { ("a", 0, 0), ("b", 10, 6) })
            sheet.Objects.Add(new PlacedObject
            {
                Id = id,
                Content = new ShapeObject { Id = id, Shape = ShapeKind.Rectangle },
                Anchor = new CellAddress(row, column),
                Size = new DocumentSize(Twip.FromCentimeters(3), Twip.FromCentimeters(2)),
            });
        return sheet;
    }

    private (double X, double Y) Start(Worksheet sheet, PlacedObject placed)
    {
        var box = _placement.Box(sheet.Layout, new CellAddress(0, 0), placed);
        var point = ((MoveSegment)((ConnectorObject)placed.Content).Path.Figures[0].Segments[0]).To;
        return (box.Left.Value + point.X * box.Width.Value, box.Top.Value + point.Y * box.Height.Value);
    }

    private void Near((double X, double Y) expected, (double X, double Y) actual)
    {
        Assert.InRange(actual.X, expected.X - 2, expected.X + 2);
        Assert.InRange(actual.Y, expected.Y - 2, expected.Y + 2);
    }

    [Fact]
    public void An_automatic_start_leaves_from_the_side_facing_the_target()
    {
        var sheet = SheetWithTwo();
        var placed = _connectors.Connect(sheet, sheet.Objects[0], sheet.Objects[1], "c1", ConnectorKind.Elbow)!;
        var size = sheet.Objects[0].Size;
        Near((size.Width.Value, size.Height.Value / 2.0), Start(sheet, placed));
    }

    [Fact]
    public void A_chosen_start_site_is_where_the_connector_leaves()
    {
        var sheet = SheetWithTwo();
        var placed = _connectors.Connect(sheet, sheet.Objects[0], sheet.Objects[1], "c1", ConnectorKind.Elbow, ConnectorSite.Top)!;
        Assert.Equal(ConnectorSite.Top, ((ConnectorObject)placed.Content).Start.Site);
        Near((sheet.Objects[0].Size.Width.Value / 2.0, 0), Start(sheet, placed));
    }

    [Fact]
    public void Refresh_follows_a_site_read_from_a_file()
    {
        var sheet = SheetWithTwo();
        var placed = _connectors.Connect(sheet, sheet.Objects[0], sheet.Objects[1], "c1", ConnectorKind.Straight)!;
        var connector = (ConnectorObject)placed.Content;
        sheet.Objects.Add(placed with { Content = connector with { Start = connector.Start with { Site = ConnectorSite.Left } } });

        Assert.True(_connectors.Refresh(sheet));

        Near((0, sheet.Objects[0].Size.Height.Value / 2.0), Start(sheet, sheet.Objects[2]));
    }
}
