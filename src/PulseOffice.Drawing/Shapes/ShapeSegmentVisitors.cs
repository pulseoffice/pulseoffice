namespace PulseOffice.Drawing.Shapes;

public interface IShapeSegmentVisitor<out T>
{
    T VisitMove(MoveSegment segment);

    T VisitLine(LineSegment segment);

    T VisitCubic(CubicSegment segment);

    T VisitArc(ArcSegment segment);

    T VisitClose(CloseSegment segment);
}

public abstract class ShapeSegmentVisitor<T> : IShapeSegmentVisitor<T>
{
    public virtual T VisitMove(MoveSegment segment) => Otherwise(segment);

    public virtual T VisitLine(LineSegment segment) => Otherwise(segment);

    public virtual T VisitCubic(CubicSegment segment) => Otherwise(segment);

    public virtual T VisitArc(ArcSegment segment) => Otherwise(segment);

    public virtual T VisitClose(CloseSegment segment) => Otherwise(segment);

    protected abstract T Otherwise(ShapeSegment segment);
}

public sealed class SegmentPoints : ShapeSegmentVisitor<IEnumerable<ShapePoint>>
{
    public IEnumerable<ShapePoint> Of(ShapeSegment segment) => segment.Accept(this);

    public IEnumerable<ShapePoint> Of(ShapeOutline outline) =>
        outline.Figures.SelectMany(figure => figure.Segments).SelectMany(Of);

    public override IEnumerable<ShapePoint> VisitMove(MoveSegment segment) => [segment.To];

    public override IEnumerable<ShapePoint> VisitLine(LineSegment segment) => [segment.To];

    public override IEnumerable<ShapePoint> VisitCubic(CubicSegment segment) => [segment.First, segment.Second, segment.To];

    public override IEnumerable<ShapePoint> VisitArc(ArcSegment segment) => [segment.To];

    protected override IEnumerable<ShapePoint> Otherwise(ShapeSegment segment) => [];
}

public sealed class SegmentMapper(Func<ShapePoint, ShapePoint> point, double radiusX = 1, double radiusY = 1)
    : ShapeSegmentVisitor<ShapeSegment>
{
    public ShapeSegment Map(ShapeSegment segment) => segment.Accept(this);

    public ShapeOutline Map(ShapeOutline outline) =>
        new([.. outline.Figures.Select(figure => figure with { Segments = [.. figure.Segments.Select(Map)] })]);

    public override ShapeSegment VisitMove(MoveSegment segment) => new MoveSegment(point(segment.To));

    public override ShapeSegment VisitLine(LineSegment segment) => new LineSegment(point(segment.To));

    public override ShapeSegment VisitCubic(CubicSegment segment) =>
        new CubicSegment(point(segment.First), point(segment.Second), point(segment.To));

    public override ShapeSegment VisitArc(ArcSegment segment) =>
        segment with { To = point(segment.To), RadiusX = segment.RadiusX * radiusX, RadiusY = segment.RadiusY * radiusY };

    protected override ShapeSegment Otherwise(ShapeSegment segment) => segment;
}

public sealed class SegmentEnds : ShapeSegmentVisitor<IEnumerable<ShapePoint>>
{
    public IEnumerable<ShapePoint> Of(ShapeOutline outline) =>
        outline.Figures.SelectMany(figure => figure.Segments).SelectMany(segment => segment.Accept(this));

    public override IEnumerable<ShapePoint> VisitMove(MoveSegment segment) => [segment.To];

    public override IEnumerable<ShapePoint> VisitLine(LineSegment segment) => [segment.To];

    public override IEnumerable<ShapePoint> VisitCubic(CubicSegment segment) => [segment.To];

    protected override IEnumerable<ShapePoint> Otherwise(ShapeSegment segment) => [];
}
