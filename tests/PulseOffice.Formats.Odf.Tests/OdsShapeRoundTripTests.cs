using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public class OdsShapeRoundTripTests
{
    private static Workbook RoundTrip(Workbook source)
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(source, stream);

        stream.Position = 0;
        var result = new OdsReader().Read(stream);

        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        return result.Value!;
    }

    private static Workbook WithShape(ShapeObject shape)
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");

        sheet.Objects.Add(new PlacedObject
        {
            Id = shape.Id,
            Content = shape,
            Anchor = new CellAddress(3, 2),
            Size = new DocumentSize(Twip.FromCentimeters(5), Twip.FromCentimeters(2)),
        });

        return workbook;
    }

    private static ShapeObject Read(Workbook workbook)
    {
        var placed = Assert.Single(workbook.Sheets[0].Objects);
        return Assert.IsType<ShapeObject>(placed.Content);
    }

    [Fact]
    public void FontworkSurvives()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");
        sheet.Objects.Add(new PlacedObject
        {
            Id = "art1",
            Content = new PulseOffice.Drawing.Fontwork.FontworkObject
            {
                Id = "art1",
                Text = "Benvenuti",
                Warp = PulseOffice.Drawing.Fontwork.FontworkWarp.Wave,
                Style = ShapeStyle.Default with { Fill = ShapeFill.Default with { ColorHex = "#DC2626" } },
            },
            Anchor = new CellAddress(2, 1),
            Size = new DocumentSize(Twip.FromCentimeters(10), Twip.FromCentimeters(3)),
        });

        var placed = Assert.Single(RoundTrip(workbook).Sheets[0].Objects);
        var art = Assert.IsType<PulseOffice.Drawing.Fontwork.FontworkObject>(placed.Content);
        Assert.Equal("Benvenuti", art.Text);
        Assert.Equal(PulseOffice.Drawing.Fontwork.FontworkWarp.Wave, art.Warp);
        Assert.Equal("#DC2626", art.Style.Fill.ColorHex, ignoreCase: true);
    }

    [Theory]
    [InlineData(ShapeKind.Rectangle)]
    [InlineData(ShapeKind.RoundedRectangle)]
    [InlineData(ShapeKind.Ellipse)]
    [InlineData(ShapeKind.Star5)]
    [InlineData(ShapeKind.ArrowRight)]
    [InlineData(ShapeKind.Heart)]
    [InlineData(ShapeKind.Cylinder)]
    public void IlTipoDellaFormaSopravvive(ShapeKind kind)
    {
        var read = Read(RoundTrip(WithShape(new ShapeObject { Id = "f1", Shape = kind })));

        Assert.Equal(kind, read.Shape);
    }

    [Fact]
    public void IlTestoAlternativoSopravvive()
    {
        var read = Read(RoundTrip(WithShape(new ShapeObject
        {
            Id = "f1", Shape = ShapeKind.Rectangle, Title = "Freccia", Description = "Indica il totale",
        })));

        Assert.Equal("Freccia", read.Title);
        Assert.Equal("Indica il totale", read.Description);
    }

    [Fact]
    public void LAncoraggioELaDimensioneSopravvivono()
    {
        var result = RoundTrip(WithShape(new ShapeObject { Id = "f1", Shape = ShapeKind.Rectangle }));
        var placed = Assert.Single(result.Sheets[0].Objects);

        Assert.Equal(new CellAddress(3, 2), placed.Anchor);
        Assert.Equal(5, placed.Size.Width.ToCentimeters(), 2);
        Assert.Equal(2, placed.Size.Height.ToCentimeters(), 2);
    }

    [Fact]
    public void ColoriEContornoSopravvivono()
    {
        var read = Read(RoundTrip(WithShape(new ShapeObject
        {
            Id = "f1",
            Shape = ShapeKind.Rectangle,
            Style = ShapeStyle.Default with
            {
                Fill = ShapeFill.Default with { ColorHex = "#DC2626" },
                Line = ShapeLine.Default with
                {
                    ColorHex = "#111827",
                    Dash = ShapeStrokeStyle.Dashed,
                },
            },
        })));

        Assert.Equal("#dc2626", read.Style.Fill.ColorHex, ignoreCase: true);
        Assert.Equal("#111827", read.Style.Line.ColorHex, ignoreCase: true);
        Assert.Equal(ShapeStrokeStyle.Dashed, read.Style.Line.Dash);
    }

    [Fact]
    public void UnaFormaSenzaRiempimentoRestaSenza()
    {
        var read = Read(RoundTrip(WithShape(new ShapeObject
        {
            Id = "f1",
            Shape = ShapeKind.Line,
            Style = ShapeStyle.Default with { Fill = ShapeFill.None },
        })));

        Assert.Equal(ShapeFillKind.None, read.Style.Fill.Kind);
    }

    [Fact]
    public void RotazioneETestoSopravvivono()
    {
        var read = Read(RoundTrip(WithShape(new ShapeObject
        {
            Id = "f1",
            Shape = ShapeKind.Rectangle,
            RotationDegrees = 45,
            Text = "Ciao",
        })));

        Assert.Equal(45, read.RotationDegrees, 1);
        Assert.Equal("Ciao", read.Text);
    }

    [Fact]
    public void GliSpecchiamentiSopravvivono()
    {
        var read = Read(RoundTrip(WithShape(new ShapeObject
        {
            Id = "f1",
            Shape = ShapeKind.ArrowRight,
            FlipHorizontal = true,
            FlipVertical = true,
        })));

        Assert.True(read.FlipHorizontal);
        Assert.True(read.FlipVertical);
    }

    [Fact]
    public void FormeEGraficiConvivonoNelloStessoFoglio()
    {
        var workbook = WithShape(new ShapeObject { Id = "f1", Shape = ShapeKind.Ellipse });
        var sheet = workbook.Sheets[0];

        sheet[new CellAddress(0, 0)] = new Cell { Value = CellValue.Number(1) };

        sheet.Objects.Add(new PlacedObject
        {
            Id = "g1",
            Content = new Charts.ChartDefinition
            {
                Id = "g1",
                Type = Charts.ChartType.Column,
                SourceSheet = "Dati",
                SourceRange = "A1:A1",
            },
            Anchor = new CellAddress(0, 5),
        });

        var result = RoundTrip(workbook);

        Assert.Single(result.Sheets[0].Objects, o => o.Content is ShapeObject);
        Assert.Single(result.Sheets[0].Objects, o => o.Content is Charts.ChartDefinition);
    }
}
