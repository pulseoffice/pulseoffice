using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public class XlsxConnectorRoundTripTests
{
    private static Workbook WithConnector(ConnectorKind kind)
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");

        foreach (var (id, row, column) in new[] { ("a", 0, 0), ("b", 10, 6) })
            sheet.Objects.Add(new PlacedObject
            {
                Id = id,
                Content = new ShapeObject { Id = id, Shape = ShapeKind.Rectangle },
                Anchor = new CellAddress(row, column),
                Size = new DocumentSize(Twip.FromCentimeters(3), Twip.FromCentimeters(2)),
            });

        var connectors = new SheetConnectors();

        sheet.Objects.Add(connectors.Connect(sheet, sheet.Objects[0], sheet.Objects[1], "c1", kind)!);

        return workbook;
    }

    private static Workbook RoundTrip(Workbook source)
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(source, stream);

        stream.Position = 0;
        var result = new XlsxReader().Read(stream);

        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        return result.Value!;
    }

    private static ConnectorObject Read(Workbook workbook) =>
        Assert.IsType<ConnectorObject>(
            workbook.Sheets[0].Objects.Single(placed => placed.Content is ConnectorObject).Content);

    [Theory]
    [InlineData(ConnectorKind.Elbow)]
    [InlineData(ConnectorKind.Straight)]
    [InlineData(ConnectorKind.Curved)]
    public void IlTipoDelConnettoreSopravvive(ConnectorKind kind)
    {
        Assert.Equal(kind, Read(RoundTrip(WithConnector(kind))).Shape);
    }

    [Fact]
    public void GliEstremiRestanoAgganciatiAgliOggetti()
    {
        var connector = Read(RoundTrip(WithConnector(ConnectorKind.Elbow)));

        Assert.Equal("a", connector.Start.ObjectId);
        Assert.Equal("b", connector.End.ObjectId);
    }

    [Fact]
    public void DopoLaLetturaIlConnettoreSiRicalcola()
    {
        var result = RoundTrip(WithConnector(ConnectorKind.Elbow));
        var sheet = result.Sheets[0];

        new SheetConnectors().Refresh(sheet);

        var connector = Read(result);

        Assert.True(connector.Path.Figures[0].Segments.Count >= 3);
    }

    [Fact]
    public void FormeEConnettoriConvivono()
    {
        var sheet = RoundTrip(WithConnector(ConnectorKind.Straight)).Sheets[0];

        Assert.Equal(2, sheet.Objects.Count(placed => placed.Content is ShapeObject));
        Assert.Single(sheet.Objects, placed => placed.Content is ConnectorObject);
    }
}
