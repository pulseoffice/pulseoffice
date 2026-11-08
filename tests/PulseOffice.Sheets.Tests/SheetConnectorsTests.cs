using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;

namespace PulseOffice.Sheets.Tests;

public class SheetConnectorsTests
{
    private static readonly ISheetConnectors Connectors = new SheetConnectors();

    private static PlacedObject Shape(string id, int row, int column) => new()
    {
        Id = id,
        Content = new ShapeObject { Id = id, Shape = ShapeKind.Rectangle },
        Anchor = new CellAddress(row, column),
        Size = new DocumentSize(Twip.FromCentimeters(3), Twip.FromCentimeters(2)),
    };

    private static Worksheet SheetWithTwo()
    {
        var sheet = new Worksheet("Foglio1");

        sheet.Objects.Add(Shape("a", 0, 0));
        sheet.Objects.Add(Shape("b", 10, 6));

        return sheet;
    }

    [Fact]
    public void UnOggettoNonSiCollegaASeStesso()
    {
        var sheet = SheetWithTwo();

        Assert.Null(Connectors.Connect(
            sheet, sheet.Objects[0], sheet.Objects[0], "c1", ConnectorKind.Elbow));
    }

    [Fact]
    public void IlConnettoreNasceConUnTracciato()
    {
        var sheet = SheetWithTwo();

        var placed = Connectors.Connect(
            sheet, sheet.Objects[0], sheet.Objects[1], "c1", ConnectorKind.Elbow)!;

        var connector = Assert.IsType<ConnectorObject>(placed.Content);

        Assert.NotEmpty(connector.Path.Figures);
        Assert.Equal("a", connector.Start.ObjectId);
        Assert.Equal("b", connector.End.ObjectId);
    }

    [Fact]
    public void IlTracciatoStaDentroIlProprioRiquadro()
    {
        var sheet = SheetWithTwo();

        var placed = Connectors.Connect(
            sheet, sheet.Objects[0], sheet.Objects[1], "c1", ConnectorKind.Straight)!;

        var connector = Assert.IsType<ConnectorObject>(placed.Content);

        foreach (var figure in connector.Path.Figures)
            foreach (var segment in figure.Segments)
                if (segment is LineSegment line)
                {
                    Assert.InRange(line.To.X, -0.001, 1.001);
                    Assert.InRange(line.To.Y, -0.001, 1.001);
                }
    }

    [Fact]
    public void SpostandoUnOggettoIlConnettoreLoSegue()
    {
        var sheet = SheetWithTwo();

        var placed = Connectors.Connect(
            sheet, sheet.Objects[0], sheet.Objects[1], "c1", ConnectorKind.Elbow)!;

        sheet.Objects.Add(placed);

        var before = sheet.Objects[^1].Size.Width.Value;

        sheet.Objects[1] = sheet.Objects[1] with { Anchor = new CellAddress(30, 20) };

        Assert.True(Connectors.Refresh(sheet));
        Assert.NotEqual(before, sheet.Objects[^1].Size.Width.Value);
    }

    [Fact]
    public void SenzaSpostamentiIlRicalcoloNonCambiaNulla()
    {
        var sheet = SheetWithTwo();

        sheet.Objects.Add(Connectors.Connect(
            sheet, sheet.Objects[0], sheet.Objects[1], "c1", ConnectorKind.Elbow)!);

        Connectors.Refresh(sheet);

        Assert.False(Connectors.Refresh(sheet));
    }

    [Fact]
    public void UnConnettoreSenzaEstremiNonSiRicalcola()
    {
        var sheet = SheetWithTwo();

        sheet.Objects.Add(new PlacedObject
        {
            Id = "c1",
            Content = new ConnectorObject
            {
                Id = "c1",
                Start = new ConnectorEndpoint("assente"),
                End = new ConnectorEndpoint("b"),
            },
            Anchor = CellAddress.First,
        });

        Assert.False(Connectors.Refresh(sheet));
    }
}
