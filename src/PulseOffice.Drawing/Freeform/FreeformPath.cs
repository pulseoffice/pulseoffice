using PulseOffice.Drawing;
using PulseOffice.Document.Embedding;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Modules;

namespace PulseOffice.Drawing.Freeform;

public enum PathNodeKind
{
    Corner,
    Smooth,
    Symmetric,
}

public sealed record PathNode(ShapePoint Point)
{
    public ShapePoint? Before { get; init; }

    public ShapePoint? After { get; init; }

    public PathNodeKind Kind { get; init; } = PathNodeKind.Corner;

    public bool IsCurved => Before is not null || After is not null;
}

public sealed record PathFigure(IReadOnlyList<PathNode> Nodes)
{
    public bool Closed { get; init; }
}

public sealed record FreeformPath(IReadOnlyList<PathFigure> Figures)
{
    public static readonly FreeformPath Empty = new([]);

    public bool Filled { get; init; }

    public ShapeOutline ToOutline()
    {
        var figures = new List<ShapeFigure>(Figures.Count);

        foreach (var figure in Figures)
        {
            if (figure.Nodes.Count == 0) continue;

            var segments = new List<ShapeSegment> { new MoveSegment(figure.Nodes[0].Point) };

            for (var i = 1; i < figure.Nodes.Count; i++)
                segments.Add(Join(figure.Nodes[i - 1], figure.Nodes[i]));

            if (figure.Closed)
            {
                segments.Add(Join(figure.Nodes[^1], figure.Nodes[0]));
                segments.Add(CloseSegment.Instance);
            }

            figures.Add(new ShapeFigure(segments));
        }

        return new ShapeOutline(figures);
    }

    private static ShapeSegment Join(PathNode from, PathNode to) =>
        from.After is null && to.Before is null
            ? new LineSegment(to.Point)
            : new CubicSegment(from.After ?? from.Point, to.Before ?? to.Point, to.Point);

    public static FreeformPath FromOutline(ShapeOutline outline)
    {
        var sink = new NodeSink();
        new PathReplay().Replay(new ArcFlattener().Flatten(outline), sink);
        return new FreeformPath(sink.Figures);
    }

    private sealed class NodeSink : PathSink
    {
        private List<PathNode> _nodes = [];
        private bool _closed;

        public List<PathFigure> Figures { get; } = [];

        public override void BeginFigure()
        {
            _nodes = [];
            _closed = false;
        }

        public override void MoveTo(ShapePoint to) => _nodes.Add(new PathNode(to));

        public override void LineTo(ShapePoint to) => _nodes.Add(new PathNode(to));

        public override void CubicTo(ShapePoint first, ShapePoint second, ShapePoint to)
        {
            if (_nodes.Count > 0) _nodes[^1] = _nodes[^1] with { After = first, Kind = PathNodeKind.Smooth };
            _nodes.Add(new PathNode(to) { Before = second, Kind = PathNodeKind.Smooth });
        }

        public override void ArcTo(ShapePoint from, ArcSegment arc)
        {
        }

        public override void Close() => _closed = true;

        public override void EndFigure()
        {
            if (_nodes.Count > 0) Figures.Add(new PathFigure(_nodes) { Closed = _closed });
        }
    }
}

public sealed record FreeformObject : EmbeddedObject, IDrawingObject
{
    public TResult Accept<TResult>(IDrawingObjectVisitor<TResult> visitor) => visitor.VisitFreeform(this);

    public override EmbeddedObjectKind Kind => EmbeddedObjectKind.Shape;

    public required FreeformPath Path { get; init; }

    public ShapeStyle Style { get; init; } = ShapeStyle.Default;

    public string? StyleName { get; init; }

    public double RotationDegrees { get; init; }

    public override IReadOnlyCollection<ModuleKind> EditableIn =>
    [ModuleKind.Sheets, ModuleKind.Documents, ModuleKind.Presentations];
}
