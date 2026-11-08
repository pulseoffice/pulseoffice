using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public class XlsxShapeStyleRoundTripTests
{
    private static Workbook With(ShapeStyle style, string? text = null)
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");

        sheet.Objects.Add(new PlacedObject
        {
            Id = "f1",
            Content = new ShapeObject
            {
                Id = "f1",
                Shape = ShapeKind.Rectangle,
                Style = style,
                Text = text,
            },
            Anchor = new CellAddress(1, 1),
            Size = new DocumentSize(Twip.FromCentimeters(4), Twip.FromCentimeters(3)),
        });

        return workbook;
    }

    private static ShapeStyle RoundTrip(ShapeStyle style, string? text = null)
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(With(style, text), stream);

        stream.Position = 0;
        var result = new XlsxReader().Read(stream);

        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));

        var placed = Assert.Single(result.Value!.Sheets[0].Objects);
        return Assert.IsType<ShapeObject>(placed.Content).Style;
    }

    [Fact]
    public void LAngoloDiUnaSfumaturaLineareSopravvive()
    {
        var style = RoundTrip(ShapeStyle.Default with
        {
            Fill = ShapeFill.Default with
            {
                Kind = ShapeFillKind.Gradient,
                Gradient = ShapeGradient.Default with { Kind = GradientKind.Linear, AngleDegrees = 45 },
            },
        });

        Assert.Equal(45, style.Fill.Gradient.AngleDegrees, 1);
    }

    [Fact]
    public void LaTrasparenzaDelRiempimentoSopravvive()
    {
        var style = RoundTrip(ShapeStyle.Default with
        {
            Fill = ShapeFill.Default with { ColorHex = "#DC2626", Opacity = 0.4 },
        });

        Assert.Equal("#dc2626", style.Fill.ColorHex, ignoreCase: true);
        Assert.Equal(0.4, style.Fill.Opacity, 2);
    }

    [Fact]
    public void LaSfumaturaSopravvive()
    {
        var style = RoundTrip(ShapeStyle.Default with
        {
            Fill = ShapeFill.Default with
            {
                Kind = ShapeFillKind.Gradient,
                Gradient = ShapeGradient.Default with
                {
                    Kind = GradientKind.Radial,
                    AngleDegrees = 45,
                    Stops = [new("#FF0000", 0), new("#0000FF", 1)],
                },
            },
        });

        Assert.Equal(ShapeFillKind.Gradient, style.Fill.Kind);
        Assert.Equal(GradientKind.Radial, style.Fill.Gradient.Kind);
        Assert.Equal("#ff0000", style.Fill.Gradient.StartHex, ignoreCase: true);
        Assert.Equal("#0000ff", style.Fill.Gradient.EndHex, ignoreCase: true);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IlRiempimentoABitmapSopravvive(bool tiled)
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52, 0, 0, 0, 1, 0, 0, 0, 1, 8, 6, 0, 0, 0, 0x1F, 0x15, 0xC4, 0x89, 0, 0, 0, 0x0D, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x63, 0xF8, 0xCF, 0xC0, 0xF0, 0x1F, 0x00, 0x05, 0x00, 0x01, 0xFF, 0x89, 0x99, 0x3D, 0x1D, 0, 0, 0, 0, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82];
        var style = RoundTrip(ShapeStyle.Default with
        {
            Fill = ShapeFill.Default with { Kind = ShapeFillKind.Bitmap, Bitmap = new ShapeBitmap { Data = png, Tiled = tiled } },
        });

        Assert.Equal(ShapeFillKind.Bitmap, style.Fill.Kind);
        Assert.Equal(png, style.Fill.Bitmap!.Data);
        Assert.Equal("image/png", style.Fill.Bitmap.MediaType);
        Assert.Equal(tiled, style.Fill.Bitmap.Tiled);
    }

    [Fact]
    public void IlTratteggioSopravvive()
    {
        var style = RoundTrip(ShapeStyle.Default with
        {
            Fill = ShapeFill.Default with
            {
                Kind = ShapeFillKind.Hatch,
                Hatch = ShapeHatch.Default with
                {
                    Kind = HatchKind.Double,
                    ColorHex = "#16A34A",
                    AngleDegrees = 30,
                },
            },
        });

        Assert.Equal(ShapeFillKind.Hatch, style.Fill.Kind);
        Assert.Equal(HatchKind.Double, style.Fill.Hatch.Kind);
        Assert.Equal("#16a34a", style.Fill.Hatch.ColorHex, ignoreCase: true);
    }

    [Fact]
    public void LOmbraSopravvive()
    {
        var style = RoundTrip(ShapeStyle.Default with
        {
            Shadow = ShapeShadow.Default with
            {
                Enabled = true,
                ColorHex = "#111827",
                OffsetX = Twip.FromPoints(6),
                OffsetY = Twip.FromPoints(4),
                Opacity = 0.5,
            },
        });

        Assert.True(style.Shadow.Enabled);
        Assert.Equal("#111827", style.Shadow.ColorHex, ignoreCase: true);
        Assert.Equal(6, style.Shadow.OffsetX.ToPoints(), 1);
        Assert.Equal(0.5, style.Shadow.Opacity, 2);
    }

    [Fact]
    public void IlBagliore_ELaSfumaturaDeiBordiSopravvivono()
    {
        var style = RoundTrip(ShapeStyle.Default with
        {
            Glow = ShapeGlow.Default with { Enabled = true, ColorHex = "#38BDF8" },
            SoftEdge = Twip.FromPoints(5),
        });

        Assert.True(style.Glow.Enabled);
        Assert.Equal("#38bdf8", style.Glow.ColorHex, ignoreCase: true);
        Assert.Equal(5, style.SoftEdge.ToPoints(), 1);
    }

    [Fact]
    public void IlRilievoSopravvive()
    {
        var style = RoundTrip(ShapeStyle.Default with
        {
            Bevel = new ShapeBevel { Style = BevelStyle.Round, Depth = Twip.FromPoints(8) },
        });

        Assert.True(style.Bevel.Enabled);
        Assert.Equal(8, style.Bevel.Depth.ToPoints(), 1);
    }

    [Fact]
    public void LoStileDelTestoSopravvive()
    {
        var style = RoundTrip(ShapeStyle.Default with
        {
            Text = ShapeTextStyle.Default with
            {
                Family = "Times New Roman",
                Size = 18,
                Bold = true,
                Italic = true,
                Underline = true,
                ColorHex = "#FACC15",
                Horizontal = TextHorizontalPlacement.Left,
                Vertical = TextVerticalPlacement.Top,
            },
        }, "Ciao");

        Assert.Equal("Times New Roman", style.Text.Family);
        Assert.Equal(18, style.Text.Size, 1);
        Assert.True(style.Text.Bold);
        Assert.True(style.Text.Italic);
        Assert.True(style.Text.Underline);
        Assert.Equal("#facc15", style.Text.ColorHex, ignoreCase: true);
        Assert.Equal(TextHorizontalPlacement.Left, style.Text.Horizontal);
        Assert.Equal(TextVerticalPlacement.Top, style.Text.Vertical);
    }

    [Fact]
    public void LeEstremitaELeGiunzioniDellaLineaSopravvivono()
    {
        var style = RoundTrip(ShapeStyle.Default with
        {
            Line = ShapeLine.Default with
            {
                Cap = LineCapStyle.Round,
                Join = LineJoinStyle.Miter,
                Opacity = 0.6,
                StartArrow = ArrowEnd.Circle,
                EndArrow = ArrowEnd.Arrow,
            },
        });

        Assert.Equal(LineCapStyle.Round, style.Line.Cap);
        Assert.Equal(LineJoinStyle.Miter, style.Line.Join);
        Assert.Equal(0.6, style.Line.Opacity, 2);
        Assert.Equal(ArrowEnd.Circle, style.Line.StartArrow);
        Assert.Equal(ArrowEnd.Arrow, style.Line.EndArrow);
    }
}
