namespace PulseOffice.Drawing.Shapes;

public abstract class PathSink
{
    public virtual void BeginFigure()
    {
    }

    public abstract void MoveTo(ShapePoint to);

    public abstract void LineTo(ShapePoint to);

    public abstract void CubicTo(ShapePoint first, ShapePoint second, ShapePoint to);

    public abstract void ArcTo(ShapePoint from, ArcSegment arc);

    public abstract void Close();

    public virtual void EndFigure()
    {
    }
}

public sealed class PathReplay
{
    public void Replay(ShapeOutline outline, PathSink sink)
    {
        ArgumentNullException.ThrowIfNull(outline);
        ArgumentNullException.ThrowIfNull(sink);

        foreach (var figure in outline.Figures)
        {
            sink.BeginFigure();

            var walker = new Walker(sink);
            foreach (var segment in figure.Segments) segment.Accept(walker);

            sink.EndFigure();
        }
    }

    private sealed class Walker(PathSink sink) : IShapeSegmentVisitor<ShapePoint>
    {
        private ShapePoint _current;
        private ShapePoint _start;

        public ShapePoint VisitMove(MoveSegment segment)
        {
            sink.MoveTo(segment.To);
            return _start = _current = segment.To;
        }

        public ShapePoint VisitLine(LineSegment segment)
        {
            sink.LineTo(segment.To);
            return _current = segment.To;
        }

        public ShapePoint VisitCubic(CubicSegment segment)
        {
            sink.CubicTo(segment.First, segment.Second, segment.To);
            return _current = segment.To;
        }

        public ShapePoint VisitArc(ArcSegment segment)
        {
            sink.ArcTo(_current, segment);
            return _current = segment.To;
        }

        public ShapePoint VisitClose(CloseSegment segment)
        {
            sink.Close();
            return _current = _start;
        }
    }
}
