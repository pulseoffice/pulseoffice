using PulseOffice.Drawing.Shapes;

namespace PulseOffice.Drawing.Freeform;

public readonly record struct PathBounds(double X, double Y, double Width, double Height);

public sealed record NormalizedPath(FreeformPath Path, PathBounds Bounds);

public interface IPathNormalizer
{
    PathBounds Measure(FreeformPath path);

    NormalizedPath ToUnitBox(FreeformPath path);
}

public sealed class PathNormalizer : IPathNormalizer
{
    public PathBounds Measure(FreeformPath path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var minX = double.MaxValue;
        var minY = double.MaxValue;
        var maxX = double.MinValue;
        var maxY = double.MinValue;

        foreach (var point in Points(path))
        {
            minX = Math.Min(minX, point.X);
            minY = Math.Min(minY, point.Y);
            maxX = Math.Max(maxX, point.X);
            maxY = Math.Max(maxY, point.Y);
        }

        if (minX > maxX) return new PathBounds(0, 0, 0, 0);

        return new PathBounds(minX, minY, maxX - minX, maxY - minY);
    }

    public NormalizedPath ToUnitBox(FreeformPath path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var bounds = Measure(path);

        var width = bounds.Width;
        var height = bounds.Height;

        if (width <= 0 && height <= 0) return new NormalizedPath(path, bounds);

        var scaleX = width > 0 ? 1 / width : 1;
        var scaleY = height > 0 ? 1 / height : 1;

        ShapePoint Map(ShapePoint point) => new(
            Math.Round((point.X - bounds.X) * scaleX, 6),
            Math.Round((point.Y - bounds.Y) * scaleY, 6));

        return new NormalizedPath(path with
        {
            Figures = [.. path.Figures.Select(figure => figure with
            {
                Nodes = [.. figure.Nodes.Select(node => node with
                {
                    Point = Map(node.Point),
                    Before = node.Before is { } before ? Map(before) : null,
                    After = node.After is { } after ? Map(after) : null,
                })],
            })],
        }, bounds);
    }

    private IEnumerable<ShapePoint> Points(FreeformPath path)
    {
        foreach (var figure in path.Figures)
        {
            if (figure.Nodes.Count == 0) continue;

            yield return figure.Nodes[0].Point;

            for (var i = 1; i < figure.Nodes.Count; i++)
                foreach (var point in JoinPoints(figure.Nodes[i - 1], figure.Nodes[i]))
                    yield return point;

            if (figure.Closed && figure.Nodes.Count > 1)
                foreach (var point in JoinPoints(figure.Nodes[^1], figure.Nodes[0]))
                    yield return point;
        }
    }

    private IEnumerable<ShapePoint> JoinPoints(PathNode from, PathNode to)
    {
        yield return to.Point;

        if (from.After is null && to.Before is null) yield break;

        var p0 = from.Point;
        var p1 = from.After ?? from.Point;
        var p2 = to.Before ?? to.Point;
        var p3 = to.Point;

        foreach (var t in CriticalTimes(p0.X, p1.X, p2.X, p3.X).Concat(CriticalTimes(p0.Y, p1.Y, p2.Y, p3.Y)))
            yield return new ShapePoint(Cubic(p0.X, p1.X, p2.X, p3.X, t), Cubic(p0.Y, p1.Y, p2.Y, p3.Y, t));
    }

    private IEnumerable<double> CriticalTimes(double p0, double p1, double p2, double p3)
    {
        var a = -p0 + 3 * p1 - 3 * p2 + p3;
        var b = 2 * (p0 - 2 * p1 + p2);
        var c = p1 - p0;

        if (Math.Abs(a) < 1e-12)
        {
            if (Math.Abs(b) > 1e-12) return Inside(-c / b);
            return [];
        }

        var discriminant = b * b - 4 * a * c;
        if (discriminant < 0) return [];

        var root = Math.Sqrt(discriminant);
        return Inside((-b + root) / (2 * a)).Concat(Inside((-b - root) / (2 * a)));
    }

    private IEnumerable<double> Inside(double t) => t > 0 && t < 1 ? [t] : [];

    private double Cubic(double p0, double p1, double p2, double p3, double t)
    {
        var u = 1 - t;
        return u * u * u * p0 + 3 * u * u * t * p1 + 3 * u * t * t * p2 + t * t * t * p3;
    }
}
