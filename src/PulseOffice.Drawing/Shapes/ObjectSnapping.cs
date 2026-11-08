namespace PulseOffice.Drawing.Shapes;

public sealed record SnapLines(IReadOnlyList<double> Verticals, IReadOnlyList<double> Horizontals)
{
    public bool IsEmpty => Verticals.Count == 0 && Horizontals.Count == 0;
}

public interface IObjectSnapping
{
    double? Nearest(double value, IReadOnlyList<double> lines, double tolerance);

    (double Dx, double Dy) SnapMove(double left, double top, double width, double height,
        IReadOnlyList<double> verticals, IReadOnlyList<double> horizontals, double tolerance);

    ObjectBox Snap(ObjectTransformSession session, SnapLines lines, double tolerance);

    SnapLines Lattice(double left, double top, double right, double bottom, double step);
}

public sealed class ObjectSnapping : IObjectSnapping
{
    private const double Unmoved = 0.5;
    private const int MaximumLatticeLines = 10000;

    public double? Nearest(double value, IReadOnlyList<double> lines, double tolerance)
    {
        ArgumentNullException.ThrowIfNull(lines);

        double best = tolerance;
        double? result = null;

        foreach (var line in lines)
        {
            var distance = Math.Abs(line - value);
            if (distance <= best)
            {
                best = distance;
                result = line;
            }
        }

        return result;
    }

    public (double Dx, double Dy) SnapMove(
        double left, double top, double width, double height,
        IReadOnlyList<double> verticals, IReadOnlyList<double> horizontals, double tolerance)
    {
        var dx = BestOffset([left, left + width / 2, left + width], verticals, tolerance);
        var dy = BestOffset([top, top + height / 2, top + height], horizontals, tolerance);
        return (dx, dy);
    }

    public ObjectBox Snap(ObjectTransformSession session, SnapLines lines, double tolerance)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(lines);

        var box = session.Preview;
        if (session.Rotating || lines.IsEmpty) return box;

        if (session.Moving)
        {
            var (dx, dy) = SnapMove(box.X, box.Y, box.Width, box.Height, lines.Verticals, lines.Horizontals, tolerance);
            return box with { X = box.X + dx, Y = box.Y + dy };
        }

        var start = session.Start;
        double x = box.X, y = box.Y, right = box.Right, bottom = box.Bottom;

        if (Math.Abs(box.X - start.X) > Unmoved && Nearest(box.X, lines.Verticals, tolerance) is { } nx) x = nx;
        if (Math.Abs(box.Right - start.Right) > Unmoved && Nearest(box.Right, lines.Verticals, tolerance) is { } nr) right = nr;
        if (Math.Abs(box.Y - start.Y) > Unmoved && Nearest(box.Y, lines.Horizontals, tolerance) is { } ny) y = ny;
        if (Math.Abs(box.Bottom - start.Bottom) > Unmoved && Nearest(box.Bottom, lines.Horizontals, tolerance) is { } nb) bottom = nb;

        return new ObjectBox(x, y, Math.Max(1, right - x), Math.Max(1, bottom - y));
    }

    public SnapLines Lattice(double left, double top, double right, double bottom, double step)
    {
        if (step <= 0 || right < left || bottom < top) return new SnapLines([], []);
        return new SnapLines(Steps(left, right, step), Steps(top, bottom, step));
    }

    private List<double> Steps(double from, double to, double step)
    {
        var count = Math.Floor((to - from) / step + 1e-9) + 1;
        if (!double.IsFinite(count) || count > MaximumLatticeLines) return [];
        var lines = new List<double>((int)count);
        for (var index = 0; index < (int)count; index++) lines.Add(from + index * step);
        return lines;
    }

    private double BestOffset(
        IReadOnlyList<double> candidates, IReadOnlyList<double> lines, double tolerance)
    {
        double best = tolerance;
        double offset = 0;

        foreach (var candidate in candidates)
            if (Nearest(candidate, lines, best) is { } line)
            {
                best = Math.Abs(line - candidate);
                offset = line - candidate;
            }

        return offset;
    }
}
