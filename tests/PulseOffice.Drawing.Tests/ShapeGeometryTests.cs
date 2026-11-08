using PulseOffice.Drawing.Shapes;
using Xunit;

namespace PulseOffice.Drawing.Tests;

public class ShapeGeometryTests
{
    private static readonly IShapeGeometry Geometry = new ShapeGeometry();

    private static IEnumerable<ShapePoint> PointsOf(ShapeOutline outline) =>
        outline.Figures.SelectMany(figure => figure.Segments).SelectMany(segment => segment switch
        {
            MoveSegment move => [move.To],
            LineSegment line => [line.To],
            CubicSegment cubic => new[] { cubic.First, cubic.Second, cubic.To },
            ArcSegment arc => [arc.To],
            _ => [],
        });

    [Theory]
    [InlineData(ShapeKind.Rectangle)]
    [InlineData(ShapeKind.RoundedRectangle)]
    [InlineData(ShapeKind.Ellipse)]
    [InlineData(ShapeKind.Triangle)]
    [InlineData(ShapeKind.Diamond)]
    [InlineData(ShapeKind.Pentagon)]
    [InlineData(ShapeKind.Hexagon)]
    [InlineData(ShapeKind.Octagon)]
    [InlineData(ShapeKind.Star5)]
    [InlineData(ShapeKind.ArrowRight)]
    [InlineData(ShapeKind.ArrowLeft)]
    [InlineData(ShapeKind.ArrowUp)]
    [InlineData(ShapeKind.ArrowDown)]
    [InlineData(ShapeKind.DoubleArrow)]
    [InlineData(ShapeKind.Chevron)]
    [InlineData(ShapeKind.Cross)]
    [InlineData(ShapeKind.Heart)]
    [InlineData(ShapeKind.Cylinder)]
    [InlineData(ShapeKind.Callout)]
    public void OgniFormaStaDentroIlProprioRiquadro(ShapeKind kind)
    {
        var outline = Geometry.Outline(kind);

        Assert.NotEmpty(outline.Figures);

        foreach (var point in PointsOf(outline))
        {
            Assert.InRange(point.X, -0.001, 1.001);
            Assert.InRange(point.Y, -0.001, 1.001);
        }
    }

    [Theory]
    [InlineData(ShapeKind.Rectangle)]
    [InlineData(ShapeKind.Ellipse)]
    [InlineData(ShapeKind.Star5)]
    [InlineData(ShapeKind.Heart)]
    public void LeFormePieneSiChiudono(ShapeKind kind)
    {
        Assert.True(Geometry.Outline(kind).Figures[0].IsClosed);
    }

    [Fact]
    public void LaLineaNonSiChiude()
    {
        Assert.False(Geometry.Outline(ShapeKind.Line).Figures[0].IsClosed);
    }

    [Fact]
    public void UnaStellaAcinquePuntePassaPerDieciVertici()
    {
        var outline = Geometry.Outline(ShapeKind.Star5);
        var segments = outline.Figures[0].Segments;

        Assert.Equal(10, segments.Count(segment => segment is MoveSegment or LineSegment));
    }

    [Fact]
    public void LaProporzioneInternaCambiaLaStella()
    {
        var thin = PointsOf(Geometry.Outline(ShapeKind.Star5, innerRatio: 0.2)).ToList();
        var fat = PointsOf(Geometry.Outline(ShapeKind.Star5, innerRatio: 0.8)).ToList();

        Assert.NotEqual(thin[1], fat[1]);
    }

    [Fact]
    public void LaScalaMoltiplicaLeCoordinate()
    {
        var scaled = Geometry.Outline(ShapeKind.Rectangle).Scaled(200, 100);
        var points = PointsOf(scaled).ToList();

        Assert.Contains(points, point => Math.Abs(point.X - 200) < 0.001 && Math.Abs(point.Y - 100) < 0.001);
    }

    [Fact]
    public void ILatiDiUnEsagonoSonoSei()
    {
        var segments = Geometry.Outline(ShapeKind.Hexagon).Figures[0].Segments;

        Assert.Equal(6, segments.Count(segment => segment is MoveSegment or LineSegment));
    }

    [Fact]
    public void IlCilindroHaDueFigure()
    {
        Assert.Equal(2, Geometry.Outline(ShapeKind.Cylinder).Figures.Count);
    }

    [Fact]
    public void LaFrecciaASinistraEIlRiflessoDiQuellaADestra()
    {
        var right = PointsOf(Geometry.Outline(ShapeKind.ArrowRight)).ToList();
        var left = PointsOf(Geometry.Outline(ShapeKind.ArrowLeft)).ToList();

        Assert.Equal(right.Count, left.Count);

        for (var index = 0; index < right.Count; index++)
            Assert.Equal(1 - right[index].X, left[index].X, 6);
    }
}

public class SvgPathParserTests
{
    private static readonly ISvgPathParser Parser = new SvgPathParser();

    [Fact]
    public void UnaCoordinataNonNumericaNonFaFallireLaLettura()
    {
        var outline = Parser.Parse("M0 NaN L21600 NaN");

        Assert.NotNull(outline);
    }

    [Fact]
    public void IFlagCompattiDellArcoSiLeggonoUnoPerUno()
    {
        var arc = Assert.IsType<ArcSegment>(Parser.Parse("M0 0a5 5 0 105 5").Figures[0].Segments[1]);

        Assert.True(arc.LargeArc);
        Assert.False(arc.Clockwise);
        Assert.Equal(new ShapePoint(5, 5), arc.To);
    }

    [Fact]
    public void UnSemicerchioAppiattitoNonDiventaUnaRetta()
    {
        var flat = new ArcFlattener().Flatten(Parser.Parse("M0 0 A5 5 0 0 1 10 0"));
        var cubics = flat.Figures[0].Segments.OfType<CubicSegment>().ToList();

        Assert.Equal(2, cubics.Count);
        Assert.Equal(5, Math.Abs(cubics[0].To.Y), 6);
    }

    [Fact]
    public void DopoLaChiusuraUnSegmentoRipartedalPuntoIniziale()
    {
        var outline = Parser.Parse("M1 1 L5 1 Z L5 5");

        Assert.IsType<MoveSegment>(outline.Figures[^1].Segments[0]);
    }

    [Fact]
    public void UnTracciatoVuotoNonProduceFigure()
    {
        Assert.Empty(Parser.Parse("").Figures);
    }

    [Fact]
    public void ISegmentiRelativiSiSommanoAlPuntoCorrente()
    {
        var figure = Parser.Parse("M 10 10 l 5 0 l 0 5 z").Figures[0];

        Assert.Equal(new ShapePoint(15, 10), Assert.IsType<LineSegment>(figure.Segments[1]).To);
        Assert.Equal(new ShapePoint(15, 15), Assert.IsType<LineSegment>(figure.Segments[2]).To);
        Assert.True(figure.IsClosed);
    }

    [Fact]
    public void LeCoordinateRipetuteDopoUnComandoLoRiusano()
    {
        var figure = Parser.Parse("M0 0 L1 1 2 2 3 3").Figures[0];

        Assert.Equal(4, figure.Segments.Count);
    }

    [Fact]
    public void DopoUnMoveLeCoordinateSuccessiveSonoLinee()
    {
        var figure = Parser.Parse("M0 0 10 0 10 10").Figures[0];

        Assert.IsType<MoveSegment>(figure.Segments[0]);
        Assert.IsType<LineSegment>(figure.Segments[1]);
        Assert.IsType<LineSegment>(figure.Segments[2]);
    }

    [Fact]
    public void LaQuadraticaDiventaUnaCubica()
    {
        var figure = Parser.Parse("M0 0 Q 5 10 10 0").Figures[0];

        Assert.IsType<CubicSegment>(figure.Segments[1]);
    }

    [Fact]
    public void PiuSottotracciatiProduconoPiuFigure()
    {
        Assert.Equal(2, Parser.Parse("M0 0 L1 0 Z M2 2 L3 2 Z").Figures.Count);
    }

    [Fact]
    public void ILettereMaiuscoleSonoAssolute()
    {
        var figure = Parser.Parse("M 10 10 L 5 5").Figures[0];

        Assert.Equal(new ShapePoint(5, 5), Assert.IsType<LineSegment>(figure.Segments[1]).To);
    }

    [Fact]
    public void LaNotazioneScientificaSiLegge()
    {
        var figure = Parser.Parse("M0 0 L 1e2 5").Figures[0];

        Assert.Equal(100, Assert.IsType<LineSegment>(figure.Segments[1]).To.X);
    }
}

public class VectorOutlineReaderTests
{
    private static readonly IVectorOutlineReader Reader = new VectorOutlineReader();

    [Fact]
    public void LeTrasformazioniDiElementiEGruppiSiApplicano()
    {
        var drawing = Reader.Read("<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 10 10'><g transform='translate(5 5)'><rect width='2' height='2' transform='scale(2)'/></g></svg>");

        var segments = drawing.Paths[0].Outline.Figures[0].Segments;
        Assert.Equal(new ShapePoint(0.5, 0.5), Assert.IsType<MoveSegment>(segments[0]).To);
        Assert.Equal(new ShapePoint(0.9, 0.5), Assert.IsType<LineSegment>(segments[1]).To);
    }

    [Fact]
    public void SenzaViewBoxLaScalaVieneDaLarghezzaEAltezza()
    {
        var drawing = Reader.Read("<svg xmlns='http://www.w3.org/2000/svg' width='24' height='24'><circle cx='12' cy='12' r='12'/></svg>");

        Assert.Equal(new ShapePoint(1, 0.5), Assert.IsType<MoveSegment>(drawing.Paths[0].Outline.Figures[0].Segments[0]).To);
    }

    [Fact]
    public void UnTestoNonSvgNonProduceNulla()
    {
        Assert.True(Reader.Read("non è xml").IsEmpty);
    }

    [Fact]
    public void IlViewBoxNormalizzaAUno()
    {
        var drawing = Reader.Read(
            """<svg viewBox="0 0 24 24"><path d="M0 0 L24 24"/></svg>""");

        var segments = Assert.Single(drawing.Paths).Outline.Figures[0].Segments;

        Assert.Equal(new ShapePoint(1, 1), Assert.IsType<LineSegment>(segments[1]).To);
    }

    [Fact]
    public void SenzaAttributiIlTracciatoEPieno()
    {
        var path = Assert.Single(Reader.Read("""<svg viewBox="0 0 10 10"><path d="M0 0 L5 5"/></svg>""").Paths);

        Assert.Equal("#000000", path.Paint.FillHex);
        Assert.Null(path.Paint.StrokeHex);
    }

    [Fact]
    public void UnTracciatoSenzaRiempimentoResta_Contorno()
    {
        var path = Assert.Single(Reader.Read(
            """<svg viewBox="0 0 10 10"><path d="M0 0 L5 5" fill="none" stroke="#FF0000"/></svg>""").Paths);

        Assert.Null(path.Paint.FillHex);
        Assert.Equal("#FF0000", path.Paint.StrokeHex);
    }

    [Fact]
    public void GliAttributiSiEreditanoDallaRadice()
    {
        var path = Assert.Single(Reader.Read(
            """<svg viewBox="0 0 10 10" fill="none" stroke="#00FF00"><path d="M0 0 L5 5"/></svg>""").Paths);

        Assert.Null(path.Paint.FillHex);
        Assert.Equal("#00FF00", path.Paint.StrokeHex);
    }

    [Fact]
    public void LoSpessoreDelContornoSiNormalizzaComeLeCoordinate()
    {
        var path = Assert.Single(Reader.Read(
            """<svg viewBox="0 0 24 24"><path d="M0 0 L5 5" fill="none" stroke="#000" stroke-width="2.4"/></svg>""").Paths);

        Assert.Equal(0.1, path.Paint.StrokeWidth, 6);
    }

    [Fact]
    public void LaRicolorazioneNonAccendeCioCheEraSpento()
    {
        var drawing = Reader.Read(
            """<svg viewBox="0 0 10 10"><path d="M0 0 L5 5" fill="none" stroke="#000"/></svg>""")
            .Recoloured("#123456");

        var path = Assert.Single(drawing.Paths);

        Assert.Null(path.Paint.FillHex);
        Assert.Equal("#123456", path.Paint.StrokeHex);
    }

    [Fact]
    public void PiuTracciatiRestanoDistinti()
    {
        Assert.Equal(2, Reader.Read(
            """<svg viewBox="0 0 10 10"><path d="M0 0 L5 5"/><path d="M5 5 L9 9"/></svg>""").Paths.Count);
    }
}
