using PulseOffice.Drawing.Shapes;

namespace PulseOffice.Drawing.Freeform;

public interface IPathAuthoring
{
    ShapePoint Constrain(ShapePoint from, ShapePoint to);

    FreeformPath Polygon(IReadOnlyList<ShapePoint> points, bool closed, bool filled);

    FreeformPath Freehand(IReadOnlyList<ShapePoint> points, double tolerance, bool closed, bool filled);

    FreeformPath Curve(IReadOnlyList<ShapePoint> points, bool closed, bool filled);
}

public sealed class PathAuthoring : IPathAuthoring
{
    private const double SmoothingRatio = 0.28;

    public ShapePoint Constrain(ShapePoint from, ShapePoint to)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;

        if (dx == 0 && dy == 0) return to;

        var angle = Math.Atan2(dy, dx);
        var step = Math.PI / 4;
        var snapped = Math.Round(angle / step) * step;

        var length = Math.Sqrt((dx * dx) + (dy * dy));

        var x = from.X + (length * Math.Cos(snapped));
        var y = from.Y + (length * Math.Sin(snapped));

        return new ShapePoint(Round(x), Round(y));
    }

    public FreeformPath Polygon(IReadOnlyList<ShapePoint> points, bool closed, bool filled)
    {
        ArgumentNullException.ThrowIfNull(points);

        if (points.Count < 2) return FreeformPath.Empty;

        return new FreeformPath([new PathFigure([.. points.Select(point => new PathNode(point))])
        {
            Closed = closed,
        }])
        {
            Filled = filled,
        };
    }

    public FreeformPath Freehand(IReadOnlyList<ShapePoint> points, double tolerance, bool closed, bool filled)
    {
        ArgumentNullException.ThrowIfNull(points);

        if (points.Count < 2) return FreeformPath.Empty;

        var simplified = Simplify(points, Math.Max(tolerance, 0));

        return Curve(simplified, closed, filled);
    }

    public FreeformPath Curve(IReadOnlyList<ShapePoint> points, bool closed, bool filled)
    {
        ArgumentNullException.ThrowIfNull(points);

        if (points.Count < 2) return FreeformPath.Empty;
        if (points.Count == 2) return Polygon(points, closed, filled);

        var nodes = new List<PathNode>(points.Count);

        for (var i = 0; i < points.Count; i++)
        {
            var previous = points[i > 0 ? i - 1 : closed ? points.Count - 1 : 0];
            var next = points[i < points.Count - 1 ? i + 1 : closed ? 0 : points.Count - 1];

            var tangentX = (next.X - previous.X) * SmoothingRatio;
            var tangentY = (next.Y - previous.Y) * SmoothingRatio;

            nodes.Add(new PathNode(points[i])
            {
                Before = new ShapePoint(Round(points[i].X - tangentX), Round(points[i].Y - tangentY)),
                After = new ShapePoint(Round(points[i].X + tangentX), Round(points[i].Y + tangentY)),
                Kind = PathNodeKind.Symmetric,
            });
        }

        return new FreeformPath([new PathFigure(nodes) { Closed = closed }]) { Filled = filled };
    }

    private static IReadOnlyList<ShapePoint> Simplify(IReadOnlyList<ShapePoint> points, double tolerance)
    {
        if (tolerance <= 0 || points.Count < 3) return points;

        var keep = new bool[points.Count];
        keep[0] = true;
        keep[^1] = true;

        Reduce(points, 0, points.Count - 1, tolerance, keep);

        return [.. points.Where((_, index) => keep[index])];
    }

    private static void Reduce(
        IReadOnlyList<ShapePoint> points, int start, int end, double tolerance, bool[] keep)
    {
        var pending = new Stack<(int First, int Last)>();
        pending.Push((start, end));

        while (pending.Count > 0)
        {
            var (first, last) = pending.Pop();
            if (last <= first + 1) continue;

            var farthest = -1;
            var distance = tolerance;

            for (var i = first + 1; i < last; i++)
            {
                var candidate = DistanceToSegment(points[i], points[first], points[last]);
                if (candidate <= distance) continue;

                farthest = i;
                distance = candidate;
            }

            if (farthest < 0) continue;

            keep[farthest] = true;
            pending.Push((first, farthest));
            pending.Push((farthest, last));
        }
    }

    private static double DistanceToSegment(ShapePoint point, ShapePoint start, ShapePoint end)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;

        if (dx == 0 && dy == 0)
            return Math.Sqrt(Square(point.X - start.X) + Square(point.Y - start.Y));

        var ratio = (((point.X - start.X) * dx) + ((point.Y - start.Y) * dy)) / ((dx * dx) + (dy * dy));
        ratio = Math.Clamp(ratio, 0, 1);

        var projectedX = start.X + (ratio * dx);
        var projectedY = start.Y + (ratio * dy);

        return Math.Sqrt(Square(point.X - projectedX) + Square(point.Y - projectedY));
    }

    private static double Square(double value) => value * value;

    private static double Round(double value) => Math.Round(value, 6);
}
