namespace PulseOffice.Drawing.Shapes;

public interface IShapeGeometry
{
    ShapeOutline Outline(ShapeObject shape);

    ShapeOutline Outline(ShapeObject shape, double width, double height);

    ShapeOutline Outline(ShapeKind kind, double cornerRatio = 0.18, double innerRatio = 0.45);
}

public sealed class ShapeGeometry : IShapeGeometry
{
    private readonly IExtendedShapes _extended;
    private readonly Geometry.IComputedGeometryEngine _computed;
    private readonly Geometry.IImplicitPresetGeometry _implicit;
    private readonly IDimensionLineGeometry _dimensions;

    public ShapeGeometry(IExtendedShapes? extended = null, Geometry.IComputedGeometryEngine? computed = null, Geometry.IImplicitPresetGeometry? implicitPresets = null, IDimensionLineGeometry? dimensions = null)
    {
        _dimensions = dimensions ?? new DimensionLineGeometry();
        _extended = extended ?? new ExtendedShapes();
        var catalog = new Geometry.PresetShapeCatalog();
        _computed = computed ?? new Geometry.ComputedGeometryEngine(
            new Geometry.PresetGeometryEngine(catalog, new Geometry.DrawingBuiltIns(), new Geometry.GuideOperators()), new Geometry.EnhancedPathInterpreter());
        _implicit = implicitPresets ?? new Geometry.ImplicitPresetGeometry(catalog, new ShapePresets());
    }

    private readonly EllipseArcs _arcs = new();

    public ShapeOutline Outline(ShapeObject shape, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(shape);
        if (shape.Dimension is { } dimension) return _dimensions.Outline(dimension, width, height);
        return (shape.Computed ?? _implicit.Shaded(shape, width, height)) is { } computed && _computed.Outline(computed, width, height) is { Figures.Count: > 0 } outline
            ? outline
            : Outline(shape);
    }

    public ShapeOutline Outline(ShapeObject shape)
    {
        ArgumentNullException.ThrowIfNull(shape);
        if (shape.Dimension is { } dimension) return _dimensions.Outline(dimension, shape.NaturalSize.Width.ToPoints(), shape.NaturalSize.Height.ToPoints());
        if (shape.Computed is { } computed && _computed.Outline(computed) is { Figures.Count: > 0 } outline) return outline;

        return shape.Shape is ShapeKind.Arc or ShapeKind.Pie or ShapeKind.Chord
            ? _arcs.Outline(shape.Shape, shape.ArcStartDegrees, shape.ArcEndDegrees)
            : Outline(shape.Shape, shape.CornerRatio, shape.InnerRatio);
    }

    public ShapeOutline Outline(ShapeKind kind, double cornerRatio = 0.18, double innerRatio = 0.45)
    {
        var corner = Math.Clamp(double.IsFinite(cornerRatio) ? cornerRatio : 0.18, 0, 0.5);
        var inner = Math.Clamp(double.IsFinite(innerRatio) ? innerRatio : 0.45, 0.05, 0.95);

        return kind switch
        {
            ShapeKind.Rectangle => Polygon([(0, 0), (1, 0), (1, 1), (0, 1)]),
            ShapeKind.RoundedRectangle => RoundedRectangle(corner),
            ShapeKind.Ellipse => Ellipse(),
            ShapeKind.Triangle => Polygon([(0.5, 0), (1, 1), (0, 1)]),
            ShapeKind.RightTriangle => Polygon([(0, 0), (0, 1), (1, 1)]),
            ShapeKind.Diamond => Polygon([(0.5, 0), (1, 0.5), (0.5, 1), (0, 0.5)]),
            ShapeKind.Parallelogram => Polygon([(0.25, 0), (1, 0), (0.75, 1), (0, 1)]),
            ShapeKind.Trapezoid => Polygon([(0.25, 0), (0.75, 0), (1, 1), (0, 1)]),
            ShapeKind.Pentagon => Regular(5),
            ShapeKind.Hexagon => Regular(6),
            ShapeKind.Octagon => Regular(8),
            ShapeKind.Star4 => Star(4, inner),
            ShapeKind.Star5 => Star(5, inner),
            ShapeKind.Star6 => Star(6, inner),
            ShapeKind.Star8 => Star(8, inner),
            ShapeKind.ArrowRight => Polygon(
                [(0, 0.3), (0.6, 0.3), (0.6, 0), (1, 0.5), (0.6, 1), (0.6, 0.7), (0, 0.7)]),
            ShapeKind.ArrowLeft => Mirrored(
                [(0, 0.3), (0.6, 0.3), (0.6, 0), (1, 0.5), (0.6, 1), (0.6, 0.7), (0, 0.7)]),
            ShapeKind.ArrowUp => Rotated90(
                [(0, 0.3), (0.6, 0.3), (0.6, 0), (1, 0.5), (0.6, 1), (0.6, 0.7), (0, 0.7)], clockwise: false),
            ShapeKind.ArrowDown => Rotated90(
                [(0, 0.3), (0.6, 0.3), (0.6, 0), (1, 0.5), (0.6, 1), (0.6, 0.7), (0, 0.7)], clockwise: true),
            ShapeKind.DoubleArrow => Polygon(
            [
                (0, 0.5), (0.25, 0), (0.25, 0.3), (0.75, 0.3), (0.75, 0),
                (1, 0.5), (0.75, 1), (0.75, 0.7), (0.25, 0.7), (0.25, 1),
            ]),
            ShapeKind.Chevron => Polygon([(0, 0), (0.7, 0), (1, 0.5), (0.7, 1), (0, 1), (0.3, 0.5)]),
            ShapeKind.Cross => Polygon(
            [
                (0.35, 0), (0.65, 0), (0.65, 0.35), (1, 0.35), (1, 0.65),
                (0.65, 0.65), (0.65, 1), (0.35, 1), (0.35, 0.65), (0, 0.65), (0, 0.35), (0.35, 0.35),
            ]),
            ShapeKind.Heart => Heart(),
            ShapeKind.Cylinder => Cylinder(),
            ShapeKind.Callout => Callout(corner),
            ShapeKind.Line => new ShapeOutline([new ShapeFigure(
                [new MoveSegment(new ShapePoint(0, 0)), new LineSegment(new ShapePoint(1, 1))])]),
            ShapeKind.Arc or ShapeKind.Pie or ShapeKind.Chord => _arcs.Outline(kind, 90, 0),
            _ => _extended.Outline(kind),
        };
    }

    private static ShapeOutline Polygon(IReadOnlyList<(double X, double Y)> points)
    {
        var segments = new List<ShapeSegment> { new MoveSegment(new ShapePoint(points[0].X, points[0].Y)) };

        for (var index = 1; index < points.Count; index++)
            segments.Add(new LineSegment(new ShapePoint(points[index].X, points[index].Y)));

        segments.Add(CloseSegment.Instance);

        return new ShapeOutline([new ShapeFigure(segments)]);
    }

    private static ShapeOutline Mirrored(IReadOnlyList<(double X, double Y)> points) =>
        Polygon([.. points.Select(point => (1 - point.X, point.Y))]);

    private static ShapeOutline Rotated90(IReadOnlyList<(double X, double Y)> points, bool clockwise) =>
        Polygon([.. points.Select(point => clockwise
            ? (1 - point.Y, point.X)
            : (point.Y, 1 - point.X))]);

    private static ShapeOutline Regular(int sides)
    {
        var points = new List<(double, double)>(sides);

        for (var index = 0; index < sides; index++)
        {
            var angle = -Math.PI / 2 + 2 * Math.PI * index / sides;

            points.Add((0.5 + 0.5 * Math.Cos(angle), 0.5 + 0.5 * Math.Sin(angle)));
        }

        return Polygon(points);
    }

    private static ShapeOutline Star(int points, double inner)
    {
        var vertices = new List<(double, double)>(points * 2);

        for (var index = 0; index < points * 2; index++)
        {
            var radius = index % 2 == 0 ? 0.5 : 0.5 * inner;
            var angle = -Math.PI / 2 + Math.PI * index / points;

            vertices.Add((0.5 + radius * Math.Cos(angle), 0.5 + radius * Math.Sin(angle)));
        }

        return Polygon(vertices);
    }

    private static ShapeOutline RoundedRectangle(double corner)
    {
        var r = corner;

        var segments = new List<ShapeSegment>
        {
            new MoveSegment(new ShapePoint(r, 0)),
            new LineSegment(new ShapePoint(1 - r, 0)),
            new ArcSegment(new ShapePoint(1, r), r, r, Clockwise: true),
            new LineSegment(new ShapePoint(1, 1 - r)),
            new ArcSegment(new ShapePoint(1 - r, 1), r, r, Clockwise: true),
            new LineSegment(new ShapePoint(r, 1)),
            new ArcSegment(new ShapePoint(0, 1 - r), r, r, Clockwise: true),
            new LineSegment(new ShapePoint(0, r)),
            new ArcSegment(new ShapePoint(r, 0), r, r, Clockwise: true),
            CloseSegment.Instance,
        };

        return new ShapeOutline([new ShapeFigure(segments)]);
    }

    private static ShapeOutline Ellipse()
    {
        const double k = 0.5522847498307936 / 2;

        var segments = new List<ShapeSegment>
        {
            new MoveSegment(new ShapePoint(0.5, 0)),
            new CubicSegment(new ShapePoint(0.5 + k, 0), new ShapePoint(1, 0.5 - k), new ShapePoint(1, 0.5)),
            new CubicSegment(new ShapePoint(1, 0.5 + k), new ShapePoint(0.5 + k, 1), new ShapePoint(0.5, 1)),
            new CubicSegment(new ShapePoint(0.5 - k, 1), new ShapePoint(0, 0.5 + k), new ShapePoint(0, 0.5)),
            new CubicSegment(new ShapePoint(0, 0.5 - k), new ShapePoint(0.5 - k, 0), new ShapePoint(0.5, 0)),
            CloseSegment.Instance,
        };

        return new ShapeOutline([new ShapeFigure(segments)]);
    }

    private static ShapeOutline Heart()
    {
        var segments = new List<ShapeSegment>
        {
            new MoveSegment(new ShapePoint(0.5, 1)),
            new CubicSegment(new ShapePoint(0.05, 0.62), new ShapePoint(0, 0.28), new ShapePoint(0.25, 0.12)),
            new CubicSegment(new ShapePoint(0.4, 0.02), new ShapePoint(0.5, 0.12), new ShapePoint(0.5, 0.25)),
            new CubicSegment(new ShapePoint(0.5, 0.12), new ShapePoint(0.6, 0.02), new ShapePoint(0.75, 0.12)),
            new CubicSegment(new ShapePoint(1, 0.28), new ShapePoint(0.95, 0.62), new ShapePoint(0.5, 1)),
            CloseSegment.Instance,
        };

        return new ShapeOutline([new ShapeFigure(segments)]);
    }

    private static ShapeOutline Cylinder()
    {
        const double h = 0.12;
        const double k = 0.5522847498307936 / 2;

        var body = new List<ShapeSegment>
        {
            new MoveSegment(new ShapePoint(0, h)),
            new CubicSegment(new ShapePoint(0, h - 2 * k * h), new ShapePoint(1, h - 2 * k * h),
                new ShapePoint(1, h)),
            new LineSegment(new ShapePoint(1, 1 - h)),
            new CubicSegment(new ShapePoint(1, 1 - h + 2 * k * h), new ShapePoint(0, 1 - h + 2 * k * h),
                new ShapePoint(0, 1 - h)),
            CloseSegment.Instance,
        };

        var lid = new List<ShapeSegment>
        {
            new MoveSegment(new ShapePoint(0, h)),
            new CubicSegment(new ShapePoint(0, h + 2 * k * h), new ShapePoint(1, h + 2 * k * h),
                new ShapePoint(1, h)),
        };

        return new ShapeOutline([new ShapeFigure(body), new ShapeFigure(lid)]);
    }

    private static ShapeOutline Callout(double corner)
    {
        var r = Math.Min(corner, 0.25);

        var segments = new List<ShapeSegment>
        {
            new MoveSegment(new ShapePoint(r, 0)),
            new LineSegment(new ShapePoint(1 - r, 0)),
            new ArcSegment(new ShapePoint(1, r), r, r, Clockwise: true),
            new LineSegment(new ShapePoint(1, 0.7 - r)),
            new ArcSegment(new ShapePoint(1 - r, 0.7), r, r, Clockwise: true),
            new LineSegment(new ShapePoint(0.38, 0.7)),
            new LineSegment(new ShapePoint(0.22, 1)),
            new LineSegment(new ShapePoint(0.24, 0.7)),
            new LineSegment(new ShapePoint(r, 0.7)),
            new ArcSegment(new ShapePoint(0, 0.7 - r), r, r, Clockwise: true),
            new LineSegment(new ShapePoint(0, r)),
            new ArcSegment(new ShapePoint(r, 0), r, r, Clockwise: true),
            CloseSegment.Instance,
        };

        return new ShapeOutline([new ShapeFigure(segments)]);
    }
}

public sealed class EllipseArcs
{
    private const int StepsPerTurn = 96;
    private const double FullTurn = 360;

    public ShapeOutline Outline(ShapeKind kind, double startDegrees, double endDegrees)
    {
        var start = Normal(startDegrees);
        var sweep = Normal(endDegrees) - start;
        if (sweep <= 0) sweep += FullTurn;
        var steps = Math.Max(2, (int)Math.Ceiling(StepsPerTurn * sweep / FullTurn));
        var points = Enumerable.Range(0, steps + 1).Select(step => Point(start + sweep * step / steps)).ToList();

        var builder = new OutlineBuilder();
        if (kind is ShapeKind.Pie)
            builder.Polygon([(0.5, 0.5), .. points]);
        else if (kind is ShapeKind.Chord)
            builder.Polygon([.. points]);
        else
            builder.Polyline([.. points]);
        return builder.Build();
    }

    private (double X, double Y) Point(double degrees)
    {
        var radians = degrees * Math.PI / 180;
        return (0.5 + 0.5 * Math.Cos(radians), 0.5 - 0.5 * Math.Sin(radians));
    }

    private double Normal(double degrees) => double.IsFinite(degrees) ? ((degrees % FullTurn) + FullTurn) % FullTurn : 0;
}
