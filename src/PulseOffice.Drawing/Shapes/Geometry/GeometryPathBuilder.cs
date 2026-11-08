namespace PulseOffice.Drawing.Shapes.Geometry;

public sealed class GeometryPathBuilder
{
    private const double QuarterTurn = Math.PI / 2;
    private const double FullTurn = 2 * Math.PI;

    private readonly List<ShapeFigure> _figures = [];
    private List<ShapeSegment>? _current;
    private bool _filled = true;
    private bool _stroked = true;
    private ShapeShade _shade;

    public ShapePoint Current { get; private set; }

    public void Style(bool filled, bool stroked, ShapeShade shade = ShapeShade.Normal)
    {
        _filled = filled;
        _stroked = stroked;
        _shade = shade;
    }

    public void Shade(ShapeShade shade) => _shade = shade;

    public void Fill(bool filled) => _filled = filled;

    public void Stroke(bool stroked) => _stroked = stroked;

    public void Move(double x, double y)
    {
        Flush();
        _current = [new MoveSegment(new ShapePoint(x, y))];
        Current = new ShapePoint(x, y);
    }

    public void Line(double x, double y)
    {
        if (_current is null)
        {
            Move(x, y);
            return;
        }
        _current.Add(new LineSegment(new ShapePoint(x, y)));
        Current = new ShapePoint(x, y);
    }

    public void Quadratic(double x1, double y1, double x, double y)
    {
        var start = Current;
        Cubic(start.X + 2.0 / 3 * (x1 - start.X), start.Y + 2.0 / 3 * (y1 - start.Y),
            x + 2.0 / 3 * (x1 - x), y + 2.0 / 3 * (y1 - y), x, y);
    }

    public void Cubic(double x1, double y1, double x2, double y2, double x, double y)
    {
        if (_current is null) Move(Current.X, Current.Y);
        _current!.Add(new CubicSegment(new ShapePoint(x1, y1), new ShapePoint(x2, y2), new ShapePoint(x, y)));
        Current = new ShapePoint(x, y);
    }

    public void Ellipse(double cx, double cy, double rx, double ry, double start, double sweep, bool connect)
    {
        var first = new ShapePoint(cx + rx * Math.Cos(start), cy + ry * Math.Sin(start));
        if (!connect || _current is null) Move(first.X, first.Y);
        else if (Math.Abs(first.X - Current.X) + Math.Abs(first.Y - Current.Y) > 1e-9) Line(first.X, first.Y);
        if (!double.IsFinite(sweep) || Math.Abs(sweep) < 1e-12) return;
        sweep = Math.Clamp(sweep, -FullTurn, FullTurn);
        var pieces = Math.Max(1, (int)Math.Ceiling(Math.Abs(sweep) / QuarterTurn - 1e-9));
        var step = sweep / pieces;
        var handle = 4.0 / 3 * Math.Tan(step / 4);
        var angle = start;
        for (var piece = 0; piece < pieces; piece++)
        {
            var next = angle + step;
            var (cos0, sin0) = (Math.Cos(angle), Math.Sin(angle));
            var (cos1, sin1) = (Math.Cos(next), Math.Sin(next));
            Cubic(cx + rx * (cos0 - handle * sin0), cy + ry * (sin0 + handle * cos0),
                cx + rx * (cos1 + handle * sin1), cy + ry * (sin1 - handle * cos1),
                cx + rx * cos1, cy + ry * sin1);
            angle = next;
        }
    }

    public void Close()
    {
        if (_current is null) return;
        _current.Add(CloseSegment.Instance);
        var start = _current[0] is MoveSegment move ? move.To : Current;
        Flush();
        Current = start;
    }

    public void End() => Flush();

    public ShapeOutline Build(double left, double top, double width, double height)
    {
        Flush();
        var scaleX = width > 0 ? 1 / width : 0;
        var scaleY = height > 0 ? 1 / height : 0;
        ShapePoint Map(ShapePoint point) => new((point.X - left) * scaleX, (point.Y - top) * scaleY);
        return new ShapeOutline([.. _figures.Select(figure => figure with
        {
            Segments = [.. figure.Segments.Select(segment => segment switch
            {
                MoveSegment move => new MoveSegment(Map(move.To)),
                LineSegment line => new LineSegment(Map(line.To)),
                CubicSegment cubic => (ShapeSegment)new CubicSegment(Map(cubic.First), Map(cubic.Second), Map(cubic.To)),
                _ => segment,
            })],
        })]);
    }

    private void Flush()
    {
        if (_current is { Count: > 1 }) _figures.Add(new ShapeFigure(_current) { Filled = _filled, Stroked = _stroked, Shade = _shade });
        _current = null;
    }
}
