using Avalonia;
using Avalonia.Media;
using PulseOffice.Drawing.Shapes;
using DrawingArcSegment = PulseOffice.Drawing.Shapes.ArcSegment;

namespace PulseOffice.UI.Controls.Shapes;

public sealed class OutlineGeometry
{
    private readonly PathReplay _replay = new();

    public StreamGeometry Build(ShapeOutline outline) => Build(outline, point => new Point(point.X, point.Y), filled: true);

    public StreamGeometry Build(ShapeOutline outline, Func<ShapePoint, Point> map, bool filled)
    {
        ArgumentNullException.ThrowIfNull(outline);

        var geometry = new StreamGeometry();
        using (var context = geometry.Open()) _replay.Replay(outline, new GeometrySink(context, map, filled));
        return geometry;
    }

    private sealed class GeometrySink(StreamGeometryContext context, Func<ShapePoint, Point> map, bool filled) : PathSink
    {
        private bool _open;

        public override void MoveTo(ShapePoint to)
        {
            if (_open) context.EndFigure(false);
            context.BeginFigure(map(to), filled);
            _open = true;
        }

        public override void LineTo(ShapePoint to)
        {
            if (_open) context.LineTo(map(to));
        }

        public override void CubicTo(ShapePoint first, ShapePoint second, ShapePoint to)
        {
            if (_open) context.CubicBezierTo(map(first), map(second), map(to));
        }

        public override void ArcTo(ShapePoint from, DrawingArcSegment arc)
        {
            if (!_open) return;

            var end = map(arc.To);
            var origin = map(new ShapePoint(0, 0));
            var unit = map(new ShapePoint(1, 1));
            context.ArcTo(end, new Size(arc.RadiusX * Math.Abs(unit.X - origin.X), arc.RadiusY * Math.Abs(unit.Y - origin.Y)),
                arc.Rotation, arc.LargeArc, arc.Clockwise ? SweepDirection.Clockwise : SweepDirection.CounterClockwise);
        }

        public override void Close()
        {
            if (!_open) return;
            context.EndFigure(true);
            _open = false;
        }

        public override void EndFigure()
        {
            if (_open) context.EndFigure(false);
            _open = false;
        }
    }
}
