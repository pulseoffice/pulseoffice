using Avalonia;
using PulseOffice.Charts;
using PulseOffice.UI.Controls.Charts.Plots;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class DeepAuditRoundNineTests
{
    private static ChartFrame Frame(ChartType type, params double[] values) =>
        new(null!, new ChartDefinition { Id = "c", Type = type, SourceSheet = "S", SourceRange = "A1" },
            new ChartData(["a"], [new ChartSeries("s", values)]), new Rect(0, 0, 300, 200), new Rect(0, 0, 300, 200));

    [Fact]
    public void LeCelleVuoteNonDiventanoZeroNeiGraficiStatistici()
    {
        Assert.True(new StatisticalPlot().KeepsGaps);
    }

    [Fact]
    public void UnaCellaVuotaNonAppiattisceLaScalaDelDiagrammaAScatola()
    {
        var scale = new StatisticalPlot().ValueAxis(Frame(ChartType.BoxPlot, 10, 20, double.NaN, 30));

        Assert.NotNull(scale);
        Assert.True(scale.Minimum > 1, $"minimum {scale.Minimum}");
        Assert.True(scale.Maximum >= 30);
    }

    [Fact]
    public Task IlQuadranteDegliAngoliNonAccettaUnValoreNonNumerico() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var dial = new PulseOffice.UI.Controls.Fields.AngleDial(new PulseOffice.UI.Controls.Fields.AngleDialGeometry(), -90, 90, double.NaN, "a");
        var raised = new List<double>();
        dial.ValueChanged += (_, value) => raised.Add(value);

        dial.Value = double.NaN;
        dial.Step(double.NaN);
        dial.Step(5);

        Assert.True(double.IsFinite(dial.Value));
        Assert.All(raised, value => Assert.True(double.IsFinite(value)));
        Assert.Equal(5, dial.Value);
    }, CancellationToken.None);

    [Fact]
    public Task IlQuadranteDegliAngoliRestaEntroUnGiroAncheConLimitiSmisurati() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var dial = new PulseOffice.UI.Controls.Fields.AngleDial(new PulseOffice.UI.Controls.Fields.AngleDialGeometry(), -1e15, 1e15, 0, "a");

        dial.Value = 1e12;

        Assert.InRange(dial.Value, -360, 360);
    }, CancellationToken.None);

    [Theory]
    [InlineData(0.0, null)]
    [InlineData(-4.0, null)]
    [InlineData(double.NaN, null)]
    [InlineData(double.PositiveInfinity, null)]
    [InlineData(11.0, double.PositiveInfinity)]
    [InlineData(11.0, double.NaN)]
    public Task UnaFormaConUnCorpoDelTestoNonValidoSiDisegnaSenzaEccezioni(double size, double? runSize) => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var shape = new PulseOffice.Drawing.Shapes.ShapeObject
        {
            Id = "s",
            Shape = PulseOffice.Drawing.Shapes.ShapeKind.Rectangle,
            Text = "abc",
            Runs = runSize is null ? null : [new PulseOffice.Drawing.Shapes.ShapeTextRun("abc") { Size = runSize }],
            Style = new PulseOffice.Drawing.Shapes.ShapeStyle { Text = new PulseOffice.Drawing.Shapes.ShapeTextStyle { Size = size } },
        };
        using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(80, 60));
        using (var context = bitmap.CreateDrawingContext())
            new PulseOffice.UI.Controls.Shapes.ShapeRenderer().Render(context, shape, new Rect(0, 0, 80, 60));
    }, CancellationToken.None);

    private sealed class CountingVectorReader : PulseOffice.Drawing.Shapes.IVectorOutlineReader
    {
        private readonly PulseOffice.Drawing.Shapes.VectorOutlineReader _inner = new();

        public int Reads { get; private set; }

        public PulseOffice.Drawing.Shapes.VectorDrawing Read(string svg)
        {
            Reads++;
            return _inner.Read(svg);
        }
    }

    [Fact]
    public Task UnaGraficaVettorialeNonVieneRiletteAOgniDisegno() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var reader = new CountingVectorReader();
        var painter = new PulseOffice.UI.Controls.Shapes.Painting.VectorPainter(reader);
        var vector = new PulseOffice.Drawing.Assets.VectorGraphicObject
        {
            Id = "v",
            Asset = new PulseOffice.Drawing.Assets.VectorAsset
            {
                Id = "a",
                Name = "a",
                SourceKind = PulseOffice.Drawing.Assets.VectorSourceKind.InlineSvg,
                SvgContent = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 10 10\"><path d=\"M0 0 L10 0 L10 10 Z\"/></svg>",
            },
        };
        using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(40, 40));
        for (var frame = 0; frame < 5; frame++)
            using (var context = bitmap.CreateDrawingContext())
                painter.Paint(context, vector, new Rect(0, 0, 40, 40));

        Assert.Equal(1, reader.Reads);
    }, CancellationToken.None);

    [Theory]
    [InlineData(PulseOffice.Drawing.Fontwork.FontworkWarp.Plain)]
    [InlineData(PulseOffice.Drawing.Fontwork.FontworkWarp.ArchUp)]
    public Task UnTestoArtisticoMoltoGrandeSiDisegnaSenzaEccezioni(PulseOffice.Drawing.Fontwork.FontworkWarp warp) => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var fontwork = new PulseOffice.Drawing.Fontwork.FontworkObject { Id = "f", Text = "W", Warp = warp };
        using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(40, 40));
        using (var context = bitmap.CreateDrawingContext())
            new PulseOffice.UI.Controls.Shapes.FontworkRenderer().Render(context, fontwork, new Rect(0, 0, 200000, 120000));
    }, CancellationToken.None);

    private sealed class PlainTheme : PulseOffice.Text.Rich.ITextTheme
    {
        public uint Resolve(PulseOffice.Text.Rich.ColorRole role) => 0xFF000000;

        public string MajorFont => "Inter";

        public string MinorFont => "Inter";
    }

    [Theory]
    [InlineData(double.NaN, null)]
    [InlineData(1e6, null)]
    [InlineData(18.0, double.PositiveInfinity)]
    [InlineData(18.0, 1e7)]
    public Task UnTestoRiccoConCorpoNonValidoSiMisuraSenzaEccezioni(double paragraphSize, double? runSize) => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var paragraph = new PulseOffice.Text.Rich.TextParagraph("abc")
        {
            FontSize = paragraphSize,
            Runs = runSize is null ? [] : [new PulseOffice.Text.Rich.TextRun("abc") { FontSize = runSize }],
        };
        var text = new PulseOffice.Text.Rich.RichText(PulseOffice.Text.Rich.PlaceholderRole.Body, [paragraph]);

        var height = new PulseOffice.UI.Controls.Text.Rich.TextFrameMeasure(new PulseOffice.Text.Rich.ListLabels()).Height(text, 400, new PlainTheme(), new PulseOffice.Text.Rich.TextFields());

        Assert.True(double.IsFinite(height));
    }, CancellationToken.None);

    [Theory]
    [InlineData(60000.0)]
    [InlineData(double.NaN)]
    public Task UnaFormulaMoltoAltaSiDisegnaSenzaEccezioni(double height) => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var formula = new PulseOffice.Document.Embedding.FormulaObject { Id = "f", Expression = "a+b" };
        var renderer = new PulseOffice.UI.Controls.Formulas.FormulaObjectRenderer(new PulseOffice.UI.Controls.Formulas.FormulaRenderer(), new PulseOffice.Formulas.FormulaParser());
        using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(40, 40));
        using (var context = bitmap.CreateDrawingContext())
            renderer.Render(context, formula, new Rect(0, 0, 40, height));
    }, CancellationToken.None);

    [Fact]
    public void UnaCellaVuotaNonAzzeraLaScalaDellaCascata()
    {
        var scale = new StatisticalPlot().ValueAxis(Frame(ChartType.Waterfall, 10, double.NaN, 20));

        Assert.NotNull(scale);
        Assert.True(scale.Maximum >= 30, $"maximum {scale.Maximum}");
    }
}
