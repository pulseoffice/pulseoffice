using PulseOffice.Drawing.Shapes;
using PulseOffice.Formats.Odf;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round214SheetConnectorSiteTests
{
    private Workbook Sheet()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Data");
        foreach (var (id, row, column) in new[] { ("a", 0, 0), ("b", 10, 6) })
            sheet.Objects.Add(new PlacedObject
            {
                Id = id,
                Content = new ShapeObject { Id = id, Shape = ShapeKind.Rectangle },
                Anchor = new CellAddress(row, column),
                Size = new DocumentSize(Twip.FromCentimeters(3), Twip.FromCentimeters(2)),
            });
        var placed = new SheetConnectors().Connect(sheet, sheet.Objects[0], sheet.Objects[1], "c1", ConnectorKind.Elbow)!;
        var connector = (ConnectorObject)placed.Content;
        sheet.Objects.Add(placed with { Content = connector with { Start = new ConnectorEndpoint("a", ConnectorSite.Right), End = new ConnectorEndpoint("b") } });
        return workbook;
    }

    private ConnectorObject Connector(Workbook workbook) =>
        workbook.Sheets[0].Objects.Select(placed => placed.Content).OfType<ConnectorObject>().Single();

    [Fact]
    public void A_chosen_site_and_an_automatic_end_survive_XLSX()
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(Sheet(), stream);
        stream.Position = 0;
        var connector = Connector(new XlsxReader().Read(stream).Value!);
        Assert.Equal(("a", ConnectorSite.Right), (connector.Start.ObjectId, connector.Start.Site));
        Assert.Equal(("b", ConnectorSite.Automatic), (connector.End.ObjectId, connector.End.Site));
    }

    [Fact]
    public void A_chosen_site_and_an_automatic_end_survive_ODS()
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(Sheet(), stream);
        stream.Position = 0;
        var connector = Connector(new OdsReader().Read(stream).Value!);
        Assert.Equal(("a", ConnectorSite.Right), (connector.Start.ObjectId, connector.Start.Site));
        Assert.Equal(("b", ConnectorSite.Automatic), (connector.End.ObjectId, connector.End.Site));
    }
}
