namespace PulseOffice.Drawing.Shapes;

public readonly record struct ShapePoint(double X, double Y)
{
    public ShapePoint Scaled(double width, double height) => new(X * width, Y * height);
}

public abstract record ShapeSegment
{
    public abstract T Accept<T>(IShapeSegmentVisitor<T> visitor);
}

public sealed record MoveSegment(ShapePoint To) : ShapeSegment
{
    public override T Accept<T>(IShapeSegmentVisitor<T> visitor) => visitor.VisitMove(this);
}

public sealed record LineSegment(ShapePoint To) : ShapeSegment
{
    public override T Accept<T>(IShapeSegmentVisitor<T> visitor) => visitor.VisitLine(this);
}

public sealed record CubicSegment(ShapePoint First, ShapePoint Second, ShapePoint To) : ShapeSegment
{
    public override T Accept<T>(IShapeSegmentVisitor<T> visitor) => visitor.VisitCubic(this);
}

public sealed record ArcSegment(ShapePoint To, double RadiusX, double RadiusY, bool Clockwise) : ShapeSegment
{
    public bool LargeArc { get; init; }

    public double Rotation { get; init; }

    public override T Accept<T>(IShapeSegmentVisitor<T> visitor) => visitor.VisitArc(this);
}

public sealed record CloseSegment : ShapeSegment
{
    public static readonly CloseSegment Instance = new();

    public override T Accept<T>(IShapeSegmentVisitor<T> visitor) => visitor.VisitClose(this);
}

public enum ShapeShade
{
    Normal,
    Darken,
    DarkenLess,
    Lighten,
    LightenLess,
}

public sealed record ShapeFigure(IReadOnlyList<ShapeSegment> Segments)
{
    public ShapeShade Shade { get; init; }

    public bool IsClosed => Segments.Count > 0 && Segments[^1] is CloseSegment;

    public bool Filled { get; init; } = true;

    public bool Stroked { get; init; } = true;

    public bool Equals(ShapeFigure? other) =>
        other is not null && Filled == other.Filled && Stroked == other.Stroked && Shade == other.Shade && Segments.SequenceEqual(other.Segments);

    public override int GetHashCode() => HashCode.Combine(Segments.Count, Filled, Stroked);
}

public sealed record ShapeOutline(IReadOnlyList<ShapeFigure> Figures)
{
    public static readonly ShapeOutline Empty = new([]);

    public ShapeOutline Scaled(double width, double height) =>
        new([.. Figures.Select(figure => figure with
        {
            Segments = [.. figure.Segments.Select(new SegmentMapper(point => point.Scaled(width, height), width, height).Map)],
        })]);
}
