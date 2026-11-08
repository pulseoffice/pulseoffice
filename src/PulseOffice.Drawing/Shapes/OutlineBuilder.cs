namespace PulseOffice.Drawing.Shapes;

public sealed class OutlineBuilder
{
    private const double Kappa = 0.5522847498307936;

    private readonly List<ShapeFigure> _figures = [];
    private List<ShapeSegment>? _current;

    public OutlineBuilder Move(double x, double y)
    {
        Flush();
        _current = [new MoveSegment(new ShapePoint(x, y))];
        return this;
    }

    public OutlineBuilder Line(double x, double y)
    {
        _current?.Add(new LineSegment(new ShapePoint(x, y)));
        return this;
    }

    public OutlineBuilder Cubic(double x1, double y1, double x2, double y2, double x, double y)
    {
        _current?.Add(new CubicSegment(new ShapePoint(x1, y1), new ShapePoint(x2, y2), new ShapePoint(x, y)));
        return this;
    }

    public OutlineBuilder Polygon(params (double X, double Y)[] points)
    {
        Move(points[0].X, points[0].Y);
        foreach (var point in points.Skip(1)) Line(point.X, point.Y);
        return Close();
    }

    public OutlineBuilder Polyline(params (double X, double Y)[] points)
    {
        Move(points[0].X, points[0].Y);
        foreach (var point in points.Skip(1)) Line(point.X, point.Y);
        Flush();
        return this;
    }

    public OutlineBuilder Ellipse(double cx, double cy, double rx, double ry)
    {
        var kx = rx * Kappa;
        var ky = ry * Kappa;
        Move(cx, cy - ry);
        Cubic(cx + kx, cy - ry, cx + rx, cy - ky, cx + rx, cy);
        Cubic(cx + rx, cy + ky, cx + kx, cy + ry, cx, cy + ry);
        Cubic(cx - kx, cy + ry, cx - rx, cy + ky, cx - rx, cy);
        Cubic(cx - rx, cy - ky, cx - kx, cy - ry, cx, cy - ry);
        return Close();
    }

    public OutlineBuilder Rectangle(double x, double y, double width, double height) =>
        Polygon((x, y), (x + width, y), (x + width, y + height), (x, y + height));

    public OutlineBuilder Close()
    {
        _current?.Add(CloseSegment.Instance);
        Flush();
        return this;
    }

    public ShapeOutline Build()
    {
        Flush();
        return new ShapeOutline([.. _figures]);
    }

    private void Flush()
    {
        if (_current is { Count: > 0 }) _figures.Add(new ShapeFigure(_current));
        _current = null;
    }
}
