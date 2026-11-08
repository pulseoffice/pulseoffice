using PulseOffice.Drawing.Freeform;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class FreeformRoundTripTests
{
    private static Workbook WithPath(FreeformPath path)
    {
        var workbook = Workbook.CreateDefault();

        workbook.Sheets[0].Objects.Add(new PlacedObject
        {
            Id = "p1",
            Anchor = new CellAddress(2, 1),
            Size = new DocumentSize(Twip.FromCentimeters(5), Twip.FromCentimeters(3)),
            Content = new FreeformObject { Id = "p1", Path = path },
        });

        return workbook;
    }

    private static FreeformObject RoundTrip(FreeformPath path)
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(WithPath(path), stream);

        stream.Position = 0;
        var result = new OdsReader().Read(stream);

        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));

        return result.Value!.Sheets[0].Objects
            .Select(placed => placed.Content)
            .OfType<FreeformObject>()
            .Single();
    }

    [Fact]
    public void UnPoligonoTornaConISuoiNodi()
    {
        var path = new FreeformPath([
            new PathFigure([
                new PathNode(new ShapePoint(0, 0)),
                new PathNode(new ShapePoint(1, 0)),
                new PathNode(new ShapePoint(0.5, 1)),
            ])
            { Closed = true },
        ]);

        var read = RoundTrip(path);

        Assert.Equal(3, read.Path.Figures[0].Nodes.Count);
        Assert.True(read.Path.Figures[0].Closed);
    }

    [Fact]
    public void LeCoordinateTornanoFraZeroEUno()
    {
        var path = new FreeformPath([
            new PathFigure([
                new PathNode(new ShapePoint(0, 0)),
                new PathNode(new ShapePoint(1, 1)),
            ]),
        ]);

        var read = RoundTrip(path);

        Assert.Equal(0, read.Path.Figures[0].Nodes[0].Point.X, 4);
        Assert.Equal(1, read.Path.Figures[0].Nodes[1].Point.X, 4);
    }

    [Fact]
    public void UnaCurvaConservaLeSueManiglie()
    {
        var path = new FreeformPath([
            new PathFigure([
                new PathNode(new ShapePoint(0, 0)) { After = new ShapePoint(0.2, 0.4) },
                new PathNode(new ShapePoint(1, 1)) { Before = new ShapePoint(0.8, 0.6) },
            ]),
        ]);

        var read = RoundTrip(path);

        Assert.Equal(0.2, read.Path.Figures[0].Nodes[0].After!.Value.X, 3);
        Assert.Equal(0.6, read.Path.Figures[0].Nodes[1].Before!.Value.Y, 3);
    }

    [Fact]
    public void LAncoraggioELaMisuraSopravvivono()
    {
        var path = new FreeformPath([
            new PathFigure([
                new PathNode(new ShapePoint(0, 0)),
                new PathNode(new ShapePoint(1, 1)),
            ]),
        ]);

        using var stream = new MemoryStream();
        new OdsWriter().Write(WithPath(path), stream);

        stream.Position = 0;
        var read = new OdsReader().Read(stream).Value!;

        var placed = read.Sheets[0].Objects.Single(item => item.Content is FreeformObject);

        Assert.Equal(new CellAddress(2, 1), placed.Anchor);
        Assert.Equal(5, placed.Size.Width.ToCentimeters(), 2);
    }
}
