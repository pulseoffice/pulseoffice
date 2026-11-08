using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public class XlsxGroupRoundTripTests
{
    private static Workbook WithGroup()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");

        sheet.Objects.Add(new PlacedObject
        {
            Id = "gruppo",
            Content = new ShapeGroupObject
            {
                Id = "gruppo",
                Children =
                [
                    new GroupChild(
                        new ShapeObject { Id = "a", Shape = ShapeKind.Ellipse }, 0, 0, 0.5, 0.5),
                    new GroupChild(
                        new ShapeObject { Id = "b", Shape = ShapeKind.Star5 }, 0.5, 0.5, 0.5, 0.5),
                ],
            },
            Anchor = new CellAddress(2, 1),
            Size = new DocumentSize(Twip.FromCentimeters(6), Twip.FromCentimeters(4)),
        });

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

    [Fact]
    public void IlGruppoTornaComeGruppo()
    {
        var placed = Assert.Single(RoundTrip(WithGroup()).Sheets[0].Objects);
        var group = Assert.IsType<ShapeGroupObject>(placed.Content);

        Assert.Equal(2, group.Children.Count);
    }

    [Fact]
    public void ITipiDeiFigliSopravvivono()
    {
        var placed = Assert.Single(RoundTrip(WithGroup()).Sheets[0].Objects);
        var group = Assert.IsType<ShapeGroupObject>(placed.Content);

        Assert.Equal(ShapeKind.Ellipse, Assert.IsType<ShapeObject>(group.Children[0].Content).Shape);
        Assert.Equal(ShapeKind.Star5, Assert.IsType<ShapeObject>(group.Children[1].Content).Shape);
    }

    [Fact]
    public void LeProporzioniDeiFigliSopravvivono()
    {
        var placed = Assert.Single(RoundTrip(WithGroup()).Sheets[0].Objects);
        var group = Assert.IsType<ShapeGroupObject>(placed.Content);

        Assert.Equal(0, group.Children[0].X, 2);
        Assert.Equal(0.5, group.Children[1].X, 2);
        Assert.Equal(0.5, group.Children[0].Width, 2);
    }

    [Fact]
    public void LaMisuraComplessivaResta()
    {
        var placed = Assert.Single(RoundTrip(WithGroup()).Sheets[0].Objects);

        Assert.Equal(6, placed.Size.Width.ToCentimeters(), 1);
        Assert.Equal(4, placed.Size.Height.ToCentimeters(), 1);
        Assert.Equal(new CellAddress(2, 1), placed.Anchor);
    }

    [Fact]
    public void LaRotazioneDelFiglioSopravvive()
    {
        var source = WithGroup();
        var group = (ShapeGroupObject)source.Sheets[0].Objects[0].Content;
        source.Sheets[0].Objects[0] = source.Sheets[0].Objects[0] with
        {
            Content = group with { Children = [group.Children[0] with { RotationDegrees = 30 }, group.Children[1]] },
        };

        var placed = Assert.Single(RoundTrip(source).Sheets[0].Objects);
        var read = Assert.IsType<ShapeGroupObject>(placed.Content);

        var child = read.Children[0];
        Assert.Equal(30, (child.RotationDegrees + Assert.IsType<ShapeObject>(child.Content).RotationDegrees + 360) % 360, 1);
    }
}
