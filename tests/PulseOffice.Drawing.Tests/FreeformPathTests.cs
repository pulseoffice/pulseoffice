using PulseOffice.Drawing.Freeform;
using PulseOffice.Drawing.Shapes;
using Xunit;

namespace PulseOffice.Drawing.Tests;

public sealed class PathAuthoringTests
{
    private readonly PathAuthoring _authoring = new();

    [Fact]
    public void UnPoligonoDiventaUnaSpezzata()
    {
        var path = _authoring.Polygon(
            [new ShapePoint(0, 0), new ShapePoint(1, 0), new ShapePoint(1, 1)], closed: true, filled: true);

        var figure = Assert.Single(path.ToOutline().Figures);

        Assert.IsType<MoveSegment>(figure.Segments[0]);
        Assert.IsType<LineSegment>(figure.Segments[1]);
        Assert.True(figure.IsClosed);
    }

    [Fact]
    public void ConMenoDiDuePuntiNonNasceUnTracciato()
    {
        Assert.Empty(_authoring.Polygon([new ShapePoint(0, 0)], false, false).Figures);
    }

    [Fact]
    public void IlVincoloPortaIlPuntoSullaDiagonale()
    {
        var snapped = _authoring.Constrain(new ShapePoint(0, 0), new ShapePoint(10, 9));

        Assert.Equal(snapped.X, snapped.Y, 3);
    }

    [Fact]
    public void IlVincoloTieneLaDistanzaDalPuntoDiPartenza()
    {
        var from = new ShapePoint(2, 2);
        var snapped = _authoring.Constrain(from, new ShapePoint(12, 2.5));

        Assert.Equal(2, snapped.Y, 3);
        Assert.Equal(12.012, snapped.X, 2);
    }

    [Fact]
    public void UnaCurvaDaNodiConManiglieSimmetriche()
    {
        var path = _authoring.Curve(
            [new ShapePoint(0, 0), new ShapePoint(1, 1), new ShapePoint(2, 0)], false, false);

        var node = path.Figures[0].Nodes[1];

        Assert.Equal(PathNodeKind.Symmetric, node.Kind);
        Assert.NotNull(node.Before);
        Assert.NotNull(node.After);
    }

    [Fact]
    public void UnaCurvaProduceSegmentiCubici()
    {
        var path = _authoring.Curve(
            [new ShapePoint(0, 0), new ShapePoint(1, 1), new ShapePoint(2, 0)], false, false);

        Assert.Contains(path.ToOutline().Figures[0].Segments, segment => segment is CubicSegment);
    }

    [Fact]
    public void ILineaAManoLiberaPerdeIPuntiInutili()
    {
        var points = new List<ShapePoint>();
        for (var i = 0; i <= 100; i++) points.Add(new ShapePoint(i, 0));

        var path = _authoring.Freehand(points, tolerance: 0.5, closed: false, filled: false);

        Assert.Equal(2, path.Figures[0].Nodes.Count);
    }

    [Fact]
    public void LaSemplificazioneNonPerdeUnaSvolta()
    {
        IReadOnlyList<ShapePoint> points =
        [
            new(0, 0), new(1, 0), new(2, 0), new(3, 0),
            new(3, 1), new(3, 2), new(3, 3),
        ];

        var path = _authoring.Freehand(points, tolerance: 0.2, closed: false, filled: false);

        Assert.Equal(3, path.Figures[0].Nodes.Count);
    }
}

public sealed class PathEditingTests
{
    private readonly PathEditing _editing = new();

    private static FreeformPath Square() => new([
        new PathFigure([
            new PathNode(new ShapePoint(0, 0)),
            new PathNode(new ShapePoint(1, 0)),
            new PathNode(new ShapePoint(1, 1)),
            new PathNode(new ShapePoint(0, 1)),
        ])
        { Closed = true },
    ]);

    [Fact]
    public void LaManigliaSiTrovaVicinoAlNodo()
    {
        var handle = _editing.HandleAt(Square(), new ShapePoint(0.98, 0.02), 0.05);

        Assert.Equal(1, handle.Node);
        Assert.Equal(PathHandlePart.Node, handle.Part);
    }

    [Fact]
    public void LontanoDaiNodiNonCEManiglia()
    {
        Assert.False(_editing.HandleAt(Square(), new ShapePoint(0.5, 0.5), 0.05).Exists);
    }

    [Fact]
    public void SpostareUnNodoPortaConSeLeSueManiglie()
    {
        var path = new FreeformPath([
            new PathFigure([
                new PathNode(new ShapePoint(0, 0))
                {
                    Before = new ShapePoint(-0.1, 0),
                    After = new ShapePoint(0.1, 0),
                },
                new PathNode(new ShapePoint(1, 1)),
            ]),
        ]);

        var moved = _editing.Move(path, new PathHandle(0, 0, PathHandlePart.Node), new ShapePoint(0.5, 0.5));
        var node = moved.Figures[0].Nodes[0];

        Assert.Equal(0.4, node.Before!.Value.X, 3);
        Assert.Equal(0.6, node.After!.Value.X, 3);
    }

    [Fact]
    public void UnNodoSimmetricoSpecchiaLaManigliaOpposta()
    {
        var path = new FreeformPath([
            new PathFigure([
                new PathNode(new ShapePoint(1, 1))
                {
                    Before = new ShapePoint(0.9, 1),
                    After = new ShapePoint(1.1, 1),
                    Kind = PathNodeKind.Symmetric,
                },
                new PathNode(new ShapePoint(2, 2)),
            ]),
        ]);

        var moved = _editing.Move(path, new PathHandle(0, 0, PathHandlePart.After), new ShapePoint(1, 1.5));
        var node = moved.Figures[0].Nodes[0];

        Assert.Equal(1, node.Before!.Value.X, 3);
        Assert.Equal(0.5, node.Before.Value.Y, 3);
    }

    [Fact]
    public void UnNodoAngolareMuoveUnaManigliaSola()
    {
        var path = new FreeformPath([
            new PathFigure([
                new PathNode(new ShapePoint(1, 1))
                {
                    Before = new ShapePoint(0.9, 1),
                    After = new ShapePoint(1.1, 1),
                },
                new PathNode(new ShapePoint(2, 2)),
            ]),
        ]);

        var moved = _editing.Move(path, new PathHandle(0, 0, PathHandlePart.After), new ShapePoint(1, 1.5));

        Assert.Equal(new ShapePoint(0.9, 1), moved.Figures[0].Nodes[0].Before);
    }

    [Fact]
    public void UnNodoSiAggiungeDoveSiChiede()
    {
        var path = _editing.InsertNode(Square(), 0, 1, new ShapePoint(1, 0.5));

        Assert.Equal(5, path.Figures[0].Nodes.Count);
        Assert.Equal(new ShapePoint(1, 0.5), path.Figures[0].Nodes[2].Point);
    }

    [Fact]
    public void UnNodoSiTogliePurcheNeRestinoDue()
    {
        var path = _editing.DeleteNode(Square(), new PathHandle(0, 0, PathHandlePart.Node));
        Assert.Equal(3, path.Figures[0].Nodes.Count);

        var minimal = new FreeformPath([
            new PathFigure([new PathNode(new ShapePoint(0, 0)), new PathNode(new ShapePoint(1, 1))]),
        ]);

        Assert.Equal(2, _editing.DeleteNode(minimal, new PathHandle(0, 0, PathHandlePart.Node))
            .Figures[0].Nodes.Count);
    }

    [Fact]
    public void RendereLisciaUnAngoloGliDaDueManiglie()
    {
        var path = _editing.SetKind(Square(), new PathHandle(0, 0, PathHandlePart.Node), PathNodeKind.Symmetric);
        var node = path.Figures[0].Nodes[0];

        Assert.NotNull(node.Before);
        Assert.NotNull(node.After);
    }

    [Fact]
    public void RiportareUnNodoAdAngoloGliToglieLeManiglie()
    {
        var smooth = _editing.SetKind(Square(), new PathHandle(0, 0, PathHandlePart.Node), PathNodeKind.Symmetric);
        var corner = _editing.SetKind(smooth, new PathHandle(0, 0, PathHandlePart.Node), PathNodeKind.Corner);

        Assert.False(corner.Figures[0].Nodes[0].IsCurved);
    }

    [Fact]
    public void LaChiusuraSiAccendeESiSpegne()
    {
        Assert.False(_editing.ToggleClosed(Square(), 0).Figures[0].Closed);
    }
}

public sealed class PathNormalizerTests
{
    private readonly PathNormalizer _normalizer = new();

    [Fact]
    public void IlRiquadroTieneContoDelleManiglie()
    {
        var path = new FreeformPath([
            new PathFigure([
                new PathNode(new ShapePoint(0, 0)) { After = new ShapePoint(-2, 0) },
                new PathNode(new ShapePoint(4, 3)),
            ]),
        ]);

        var bounds = _normalizer.Measure(path);

        Assert.True(bounds.X < 0 && bounds.X > -2);
        Assert.Equal(4 - bounds.X, bounds.Width, 3);
    }

    [Fact]
    public void IlTracciatoSiRiportaFraZeroEUno()
    {
        var path = new FreeformPath([
            new PathFigure([
                new PathNode(new ShapePoint(10, 20)),
                new PathNode(new ShapePoint(30, 60)),
            ]),
        ]);

        var normalized = _normalizer.ToUnitBox(path);

        Assert.Equal(new ShapePoint(0, 0), normalized.Path.Figures[0].Nodes[0].Point);
        Assert.Equal(new ShapePoint(1, 1), normalized.Path.Figures[0].Nodes[1].Point);
        Assert.Equal(20, normalized.Bounds.Width, 3);
        Assert.Equal(40, normalized.Bounds.Height, 3);
    }

    [Fact]
    public void UnaLineaOrizzontaleNonDivideMaiPerZero()
    {
        var path = new FreeformPath([
            new PathFigure([
                new PathNode(new ShapePoint(0, 5)),
                new PathNode(new ShapePoint(10, 5)),
            ]),
        ]);

        var normalized = _normalizer.ToUnitBox(path);

        Assert.Equal(0, normalized.Path.Figures[0].Nodes[0].Point.Y, 3);
        Assert.Equal(1, normalized.Path.Figures[0].Nodes[1].Point.X, 3);
    }
}
