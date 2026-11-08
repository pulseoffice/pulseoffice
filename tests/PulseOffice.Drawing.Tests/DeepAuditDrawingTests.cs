using PulseOffice.Drawing.Assets;
using PulseOffice.Drawing.Assets.Filters;
using PulseOffice.Drawing.Assets.Gif;
using PulseOffice.Drawing.ConditionalIcons;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Drawing.Shapes.Geometry;
using Xunit;

namespace PulseOffice.Drawing.Tests;

public sealed class DeepAuditDrawingTests
{
    [Fact]
    public void UnFotogrammaGifPiuGrandeDelTettoNonVieneDecompresso()
    {
        byte[] data =
        [
            (byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'9', (byte)'a',
            10, 0, 10, 0, 0x80, 0, 0,
            0, 0, 0, 255, 255, 255,
            0x2C, 0, 0, 0, 0, 0x20, 0x4E, 0x20, 0x4E, 0,
            2, 0,
            0x3B,
        ];

        Assert.Null(new GifCodec().Decode(data));
    }

    [Fact]
    public void UnCodiceLzwOltreIlProssimoChiudeIlFlusso()
    {
        byte[] data =
        [
            (byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'9', (byte)'a',
            2, 0, 1, 0, 0x80, 0, 0,
            0, 0, 0, 255, 255, 255,
            0x2C, 0, 0, 0, 0, 2, 0, 1, 0, 0,
            2, 2, 0xCC, 0x0B, 0,
            0x3B,
        ];

        var animation = new GifCodec().Decode(data);

        Assert.NotNull(animation);
        Assert.Equal(255, animation.Frames[0].Image.Pixels[0]);
        Assert.NotEqual(255, animation.Frames[0].Image.Pixels[4]);
    }

    [Fact]
    public void UnSvgConNumeriInfinitiNonProduceCoordinateInfinite()
    {
        const string svg = "<svg xmlns='http://www.w3.org/2000/svg'><rect x='0' y='0' width='1e999' height='5'/><path d='M 0 0 L 1e999 4 L 2 2 Z'/><polygon points='0,0 1e999,1 2,2'/></svg>";

        var drawing = new VectorOutlineReader().Read(svg);

        var points = drawing.Paths.SelectMany(path => path.Outline.Figures).SelectMany(figure => figure.Segments)
            .OfType<LineSegment>().Select(line => line.To).ToList();
        Assert.All(points, point => Assert.True(double.IsFinite(point.X) && double.IsFinite(point.Y)));
    }

    [Fact]
    public void UnaFormulaAnnidataTroppoNonFaCadereIlProcesso()
    {
        var formula = new string('(', 200_000) + "1" + new string(')', 200_000);

        Assert.Throws<FormatException>(() => new EnhancedFormulaParser().Parse(formula));
    }

    [Fact]
    public void UnaSommaLunghissimaSiValutaSenzaTraboccareLoStack()
    {
        var formula = string.Join("+", Enumerable.Repeat("1", 300_000));
        var parser = new EnhancedFormulaParser();
        var expression = parser.Parse(formula);
        var scope = new EnhancedScope(new Dictionary<string, IEnhancedExpression>(), [], 0, 0, 1, 1, 1, 1);

        Assert.Throws<FormatException>(() => expression.Evaluate(scope));
    }

    [Fact]
    public void UnaCatenaDiEquazioniLunghissimaDaUnaFiguraVuotaInveceDiUnCrollo()
    {
        const int count = 200_000;
        var equations = Enumerable.Range(0, count)
            .Select(index => new EnhancedEquation($"f{index}", index == count - 1 ? "1" : $"?f{index + 1}+1"))
            .ToList();
        var geometry = new FormulaGeometry("M ?f0 0 L 10 10 N") { Equations = equations, ViewWidth = 10, ViewHeight = 10 };

        Assert.Empty(new ComputedGeometryEngine().Outline(geometry, 10, 10).Figures);
    }

    [Fact]
    public void UnArcoDiAngoloEnormeNonGeneraMilioniDiSegmenti()
    {
        var geometry = new FormulaGeometry("U 10800 10800 5400 5400 0 36000000 N");

        var outline = new EnhancedPathInterpreter().Outline(geometry, 21600, 21600);

        Assert.Single(outline.Figures);
        Assert.InRange(outline.Figures[0].Segments.Count, 2, 6);
    }

    [Fact]
    public void UnPezzoPngConLunghezzaEnormeNonFaSaltareLaRisoluzione()
    {
        byte[] data =
        [
            0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A,
            0x7F, 0xFF, 0xFF, 0xF8, (byte)'t', (byte)'E', (byte)'X', (byte)'t',
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        ];

        Assert.Equal(ImageHeader.ScreenDotsPerInch, new ImageHeader().DotsPerInch(data));
    }

    [Fact]
    public void UnTratteggioConMilioniDiPuntiRestaLimitato()
    {
        var pattern = new ShapeDashPattern { Dots1 = 10_000_000, Dots2 = 10_000_000 };

        Assert.Equal(4 * ShapeDashPattern.MaximumDots, pattern.InLineWidths(20).Count);
    }

    [Fact]
    public void IlColoreDiUnaSfumaturaInUnPuntoNonNumericoNonFaCrollare()
    {
        var editing = new GradientStopEditing();

        Assert.Equal(ShapeGradient.Default.StartHex, editing.ColorAt(ShapeGradient.Default, double.NaN));
    }

    [Fact]
    public void UnaGrigliaTroppoFittaNonEsaurisceLaMemoria()
    {
        var lines = new ObjectSnapping().Lattice(1e17, 0, 1e17 + 1000, 1000, 1);

        Assert.True(lines.Verticals.Count <= 10000);
        Assert.Equal(1001, new ObjectSnapping().Lattice(0, 0, 1000, 1000, 1).Verticals.Count);
    }

    [Fact]
    public void IlFiltroAntirumoreTieneLaMediana()
    {
        var pixels = new byte[3 * 3 * 4];
        for (var index = 0; index < 9; index++)
        {
            pixels[index * 4] = (byte)(index * 20);
            pixels[index * 4 + 1] = (byte)(index * 20);
            pixels[index * 4 + 2] = (byte)(index * 20);
            pixels[index * 4 + 3] = 255;
        }
        var image = new BgraImage(pixels, 3, 3);

        new PixelFilterCatalog().For(ImageFilter.RemoveNoise).Apply(image);

        Assert.Equal(80, image.Pixels[4 * 4]);
    }

    [Fact]
    public void UnaRegolazioneDelColoreNonNumericaLasciaLImmagineIntatta()
    {
        var image = new BgraImage([100, 150, 200, 255], 1, 1);

        new ColorAdjustmentFilter(new ColorAdjustment(Brightness: double.NaN, Contrast: double.NaN, Gamma: double.NaN)).Apply(image);

        Assert.Equal([100, 150, 200, 255], image.Pixels);
    }

    [Fact]
    public void UnFiltroDiVicinatoSuPixelPiuCortiDelleDimensioniNonCrolla()
    {
        var image = new BgraImage(new byte[4 * 4 * 4], 5, 5);

        new PixelFilterCatalog().For(ImageFilter.Smooth).Apply(image);

        Assert.Equal(64, image.Pixels.Length);
    }

    [Fact]
    public void UnaGuidaDivisaPerZeroRestaFinita()
    {
        var scope = new DrawingGuideScope(100, 50, new DrawingBuiltIns(), new GuideOperators());

        Assert.Equal(0, scope.Value("wd0"));
    }

    [Fact]
    public void UnaGuidaNonNumericaSiSalvaComeZero()
    {
        var scope = new DrawingGuideScope(100, 50, new DrawingBuiltIns(), new GuideOperators());
        scope.Set("inf", double.PositiveInfinity);

        scope.Define(new DrawingGuide("g", "+-", ["inf", "0", "inf"]));

        Assert.Equal(0, scope.Value("g"));
    }

    [Fact]
    public void UnArcoConAngoliNonNumericiDisegnaLEllisseIntera()
    {
        var outline = new EllipseArcs().Outline(ShapeKind.Pie, double.NaN, double.PositiveInfinity);

        Assert.All(Points(outline), point => Assert.True(double.IsFinite(point.X) && double.IsFinite(point.Y)));
    }

    [Fact]
    public void UnRaggioDAngoloNonNumericoNonCancellaLaForma()
    {
        var outline = new ShapeGeometry().Outline(ShapeKind.RoundedRectangle, double.NaN, double.NaN);

        Assert.All(Points(outline), point => Assert.True(double.IsFinite(point.X) && double.IsFinite(point.Y)));
    }

    [Fact]
    public void UnaConversioneInPoligonoConPassiEnormiRestaLimitata()
    {
        var polygon = new PulseOffice.Drawing.Freeform.ShapeConversion().ToPolygon(new ShapeGeometry().Outline(ShapeKind.Ellipse), int.MaxValue);

        Assert.True(polygon.Figures.Sum(figure => figure.Nodes.Count) <= 4 * 256 + 1);
    }

    [Fact]
    public void UnTratteggioConDistanzeNonNumericheRestaFinito()
    {
        var widths = new ShapeDashPattern { Distance = double.NaN, Dots1Length = double.PositiveInfinity }.InLineWidths(20);

        Assert.All(widths, width => Assert.True(double.IsFinite(width) && width > 0));
    }

    [Fact]
    public void UnaFermataDiSfumaturaNonNumericaNonEntra()
    {
        var editing = new GradientStopEditing();

        var added = editing.Add(ShapeGradient.Default, double.NaN);
        var moved = editing.Move(ShapeGradient.Default, 0, double.NaN);

        Assert.All(added.Stops.Concat(moved.Stops), stop => Assert.False(double.IsNaN(stop.Position)));
    }

    [Fact]
    public void TrasformareUnContornoConservaRiempimentoTrattoEOmbreggiatura()
    {
        var figure = new ShapeFigure([new MoveSegment(new ShapePoint(0, 0)), new LineSegment(new ShapePoint(1, 1))])
        {
            Filled = false,
            Stroked = false,
            Shade = ShapeShade.Darken,
        };

        var mapped = new SegmentMapper(point => point.Scaled(2, 2)).Map(new ShapeOutline([figure])).Figures[0];

        Assert.False(mapped.Filled);
        Assert.False(mapped.Stroked);
        Assert.Equal(ShapeShade.Darken, mapped.Shade);
    }

    [Fact]
    public void IlCatalogoDelleIconeReggeDisegniDaPiuThread()
    {
        for (var round = 0; round < 20; round++)
        {
            var catalog = new ConditionalIconCatalog();
            var names = catalog.Names;

            Parallel.For(0, 2000, index =>
            {
                var set = names[index % names.Count];
                Assert.NotNull(catalog.Icon(set, index % 5));
            });
        }
    }

    [Fact]
    public void ICaratteriDeiTrattiDiUnaFormaSonoElencati()
    {
        var shape = new ShapeObject
        {
            Id = "forma",
            Shape = ShapeKind.Rectangle,
            Runs = [new ShapeTextRun("a"), new ShapeTextRun("b") { Family = "Georgia" }],
        };

        Assert.Contains("Georgia", new PulseOffice.Drawing.Text.ObjectFontNames().Of(shape));
    }

    [Fact]
    public void InStampaLaPuntaDelConnettoreSegueLaScalaDellaFreccia()
    {
        var bounds = new PulseOffice.Foundation.Units.DocumentRect(
            new PulseOffice.Foundation.Units.Twip(0), new PulseOffice.Foundation.Units.Twip(0),
            new PulseOffice.Foundation.Units.Twip(10000), new PulseOffice.Foundation.Units.Twip(10000));
        var path = new OutlineBuilder().Polyline((0, 0.5), (1, 0.5)).Build();
        var normal = new ConnectorObject { Id = "c", Path = path };
        var large = normal with { Style = normal.Style with { Line = normal.Style.Line with { ArrowScale = 2 } } };
        var builder = new PulseOffice.Drawing.Printing.ObjectPathBuilder();

        double HeadLength(ConnectorObject connector) =>
            new SegmentPoints().Of(builder.Build(connector, bounds).Last().Outline).Min(point => point.X);

        Assert.True(HeadLength(large) < HeadLength(normal));
    }

    private static IEnumerable<ShapePoint> Points(ShapeOutline outline) =>
        outline.Figures.SelectMany(figure => figure.Segments).SelectMany(segment => segment switch
        {
            MoveSegment move => [move.To],
            LineSegment line => [line.To],
            CubicSegment cubic => [cubic.First, cubic.Second, cubic.To],
            ArcSegment arc => [arc.To, new ShapePoint(arc.RadiusX, arc.RadiusY)],
            _ => Array.Empty<ShapePoint>(),
        });
}
