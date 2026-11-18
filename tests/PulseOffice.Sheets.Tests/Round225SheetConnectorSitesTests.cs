using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;

namespace PulseOffice.Sheets.Tests;

public sealed class Round225SheetConnectorSitesTests
{
    private readonly ISheetConnectors _connectors = new SheetConnectors();

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

    [Fact]
    public void Resiting_keeps_the_objects_and_reroutes_from_the_chosen_sides()
    {
        var sheet = SheetWithTwo();
        var placed = _connectors.Connect(sheet, sheet.Objects[0], sheet.Objects[1], "c1", ConnectorKind.Straight)!;
        sheet.Objects.Add(placed);

        var resited = _connectors.Resite(sheet, placed, ConnectorSite.Top, ConnectorSite.Bottom)!;

        var connector = (ConnectorObject)resited.Content;
        Assert.Equal(new ConnectorEndpoint("a", ConnectorSite.Top), connector.Start);
        Assert.Equal(new ConnectorEndpoint("b", ConnectorSite.Bottom), connector.End);
        Assert.NotEqual(((ConnectorObject)placed.Content).Path, connector.Path);
    }

    [Fact]
    public void Only_connectors_can_be_resited()
    {
        var sheet = SheetWithTwo();
        Assert.Null(_connectors.Resite(sheet, sheet.Objects[0], ConnectorSite.Left, ConnectorSite.Right));
    }
}
