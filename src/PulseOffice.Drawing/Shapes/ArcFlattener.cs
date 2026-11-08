namespace PulseOffice.Drawing.Shapes;

public interface IArcFlattener
{
    ShapeOutline Flatten(ShapeOutline outline);
}

public sealed class ArcFlattener : IArcFlattener
{
    private readonly PathReplay _replay = new();

    public ShapeOutline Flatten(ShapeOutline outline)
    {
        var sink = new FlatteningSink(this);
        _replay.Replay(outline, sink);
        return new ShapeOutline([.. sink.Figures.Zip(outline.Figures, (flat, source) =>
            flat with { Filled = source.Filled, Stroked = source.Stroked, Shade = source.Shade })]);
    }

    private IEnumerable<ShapeSegment> Cubics(ShapePoint from, ArcSegment arc)
    {
        var rx = Math.Abs(arc.RadiusX);
        var ry = Math.Abs(arc.RadiusY);
        if (rx < 1e-12 || ry < 1e-12 || (from.X == arc.To.X && from.Y == arc.To.Y))
        {
            yield return new LineSegment(arc.To);
            yield break;
        }

        var phi = arc.Rotation * Math.PI / 180;
        var (cos, sin) = (Math.Cos(phi), Math.Sin(phi));
        var dx = (from.X - arc.To.X) / 2;
        var dy = (from.Y - arc.To.Y) / 2;
        var x1 = cos * dx + sin * dy;
        var y1 = -sin * dx + cos * dy;

        var lambda = x1 * x1 / (rx * rx) + y1 * y1 / (ry * ry);
        if (lambda > 1)
        {
            rx *= Math.Sqrt(lambda);
            ry *= Math.Sqrt(lambda);
        }

        var numerator = rx * rx * ry * ry - rx * rx * y1 * y1 - ry * ry * x1 * x1;
        var denominator = rx * rx * y1 * y1 + ry * ry * x1 * x1;
        var factor = Math.Sqrt(Math.Max(0, numerator / denominator)) * (arc.LargeArc == arc.Clockwise ? -1 : 1);
        var cx1 = factor * rx * y1 / ry;
        var cy1 = -factor * ry * x1 / rx;
        var cx = cos * cx1 - sin * cy1 + (from.X + arc.To.X) / 2;
        var cy = sin * cx1 + cos * cy1 + (from.Y + arc.To.Y) / 2;

        var start = Math.Atan2((y1 - cy1) / ry, (x1 - cx1) / rx);
        var end = Math.Atan2((-y1 - cy1) / ry, (-x1 - cx1) / rx);
        var sweep = end - start;
        if (arc.Clockwise && sweep < 0) sweep += 2 * Math.PI;
        if (!arc.Clockwise && sweep > 0) sweep -= 2 * Math.PI;

        var pieces = Math.Max(1, (int)Math.Ceiling(Math.Abs(sweep) / (Math.PI / 2) - 1e-9));
        var step = sweep / pieces;
        var handle = 4.0 / 3 * Math.Tan(step / 4);

        ShapePoint Point(double angle) => new(
            cx + rx * Math.Cos(angle) * cos - ry * Math.Sin(angle) * sin,
            cy + rx * Math.Cos(angle) * sin + ry * Math.Sin(angle) * cos);

        ShapePoint Derivative(double angle) => new(
            -rx * Math.Sin(angle) * cos - ry * Math.Cos(angle) * sin,
            -rx * Math.Sin(angle) * sin + ry * Math.Cos(angle) * cos);

        for (var piece = 0; piece < pieces; piece++)
        {
            var a = start + piece * step;
            var b = a + step;
            var p0 = Point(a);
            var p3 = piece == pieces - 1 ? arc.To : Point(b);
            var d0 = Derivative(a);
            var d3 = Derivative(b);
            yield return new CubicSegment(
                new ShapePoint(p0.X + handle * d0.X, p0.Y + handle * d0.Y),
                new ShapePoint(p3.X - handle * d3.X, p3.Y - handle * d3.Y),
                p3);
        }
    }

    private sealed class FlatteningSink(ArcFlattener flattener) : PathSink
    {
        private List<ShapeSegment> _segments = [];

        public List<ShapeFigure> Figures { get; } = [];

        public override void BeginFigure() => _segments = [];

        public override void MoveTo(ShapePoint to) => _segments.Add(new MoveSegment(to));

        public override void LineTo(ShapePoint to) => _segments.Add(new LineSegment(to));

        public override void CubicTo(ShapePoint first, ShapePoint second, ShapePoint to) =>
            _segments.Add(new CubicSegment(first, second, to));

        public override void ArcTo(ShapePoint from, ArcSegment arc) => _segments.AddRange(flattener.Cubics(from, arc));

        public override void Close() => _segments.Add(CloseSegment.Instance);

        public override void EndFigure() => Figures.Add(new ShapeFigure(_segments));
    }
}
