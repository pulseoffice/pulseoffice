namespace PulseOffice.Drawing.Shapes.Geometry;

public interface IPresetGeometryEngine
{
    bool Knows(string? name);

    IReadOnlyList<GeometryAdjustment> Defaults(string name);

    ShapeOutline Outline(PresetGeometry geometry, double width, double height);

    bool Adjusted(PresetGeometry geometry);

    PresetTextBounds? TextBounds(PresetGeometry geometry, double width, double height);
}

public readonly record struct PresetTextBounds(double Left, double Top, double Right, double Bottom);

public sealed class PresetGeometryEngine(IPresetShapeCatalog catalog, IDrawingBuiltIns builtIns, IGuideOperators operators) : IPresetGeometryEngine
{
    private const double AngleUnit = Math.PI / 10800000;

    public PresetGeometryEngine() : this(new PresetShapeCatalog(), new DrawingBuiltIns(), new GuideOperators())
    {
    }

    public bool Knows(string? name) => catalog.Find(name) is not null;

    public IReadOnlyList<GeometryAdjustment> Defaults(string name)
    {
        if (catalog.Find(name) is not { } definition) return [];
        var scope = new DrawingGuideScope(1, 1, builtIns, operators);
        return [.. definition.Adjustments.Select(guide => new GeometryAdjustment(guide.Name, scope.Evaluate(guide)))];
    }

    public bool Adjusted(PresetGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        var defaults = Defaults(geometry.Name);
        return geometry.Adjustments.Any(adjustment =>
            (defaults.FirstOrDefault(value => value.Name == adjustment.Name) ?? (defaults.Count == 1 ? defaults[0] : null)) is not { } known
            || Math.Abs(known.Value - adjustment.Value) > 0.5);
    }

    public ShapeOutline Outline(PresetGeometry geometry, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        if (catalog.Find(geometry.Name) is not { } definition) return ShapeOutline.Empty;
        var w = width > 0 ? width : 1;
        var h = height > 0 ? height : 1;
        var scope = Scope(definition, geometry, w, h);

        var builder = new GeometryPathBuilder();
        foreach (var path in definition.Paths)
        {
            builder.Style(path.Filled, path.Stroked, path.Shade);
            var walker = new Walker(scope, builder, path.Width is > 0 ? w / path.Width.Value : 1, path.Height is > 0 ? h / path.Height.Value : 1);
            foreach (var command in path.Commands) command.Accept(walker);
            builder.End();
        }
        return builder.Build(0, 0, w, h);
    }

    public PresetTextBounds? TextBounds(PresetGeometry geometry, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        if (catalog.Find(geometry.Name) is not { TextRect: { } rect } definition || width <= 0 || height <= 0) return null;
        var scope = Scope(definition, geometry, width, height);
        var left = Math.Clamp(scope.Value(rect.Left), 0, width);
        var top = Math.Clamp(scope.Value(rect.Top), 0, height);
        var right = Math.Clamp(scope.Value(rect.Right), left, width);
        var bottom = Math.Clamp(scope.Value(rect.Bottom), top, height);
        return new PresetTextBounds(left, top, right, bottom);
    }

    private DrawingGuideScope Scope(PresetShapeDefinition definition, PresetGeometry geometry, double width, double height)
    {
        var scope = new DrawingGuideScope(width, height, builtIns, operators);
        foreach (var adjustment in definition.Adjustments)
        {
            var given = geometry.Adjustments.FirstOrDefault(value => value.Name == adjustment.Name)
                ?? (definition.Adjustments.Count == 1 ? geometry.Adjustments.FirstOrDefault() : null);
            scope.Set(adjustment.Name, given?.Value ?? scope.Evaluate(adjustment));
        }
        foreach (var guide in definition.Guides) scope.Define(guide);
        return scope;
    }

    private sealed class Walker(DrawingGuideScope scope, GeometryPathBuilder builder, double scaleX, double scaleY) : IPresetPathVisitor
    {
        private double X(string token) => scope.Value(token) * scaleX;

        private double Y(string token) => scope.Value(token) * scaleY;

        public void VisitMove(PresetMove command) => builder.Move(X(command.To.X), Y(command.To.Y));

        public void VisitLine(PresetLine command) => builder.Line(X(command.To.X), Y(command.To.Y));

        public void VisitQuadratic(PresetQuadratic command) =>
            builder.Quadratic(X(command.Control.X), Y(command.Control.Y), X(command.To.X), Y(command.To.Y));

        public void VisitCubic(PresetCubic command) =>
            builder.Cubic(X(command.First.X), Y(command.First.Y), X(command.Second.X), Y(command.Second.Y), X(command.To.X), Y(command.To.Y));

        public void VisitClose(PresetClose command) => builder.Close();

        public void VisitArc(PresetArc command)
        {
            var pathRadiusX = scope.Value(command.RadiusX);
            var pathRadiusY = scope.Value(command.RadiusY);
            var start = scope.Value(command.Start) * AngleUnit;
            var sweep = scope.Value(command.Sweep) * AngleUnit;
            new EllipticArc().Append(builder, pathRadiusX * scaleX, pathRadiusY * scaleY, start, sweep, pathRadiusX, pathRadiusY);
        }
    }
}

public sealed class EllipticArc
{
    private const double FullTurn = 2 * Math.PI;

    public void Append(GeometryPathBuilder builder, double radiusX, double radiusY, double start, double sweep) =>
        Append(builder, radiusX, radiusY, start, sweep, radiusX, radiusY);

    public void Append(GeometryPathBuilder builder, double radiusX, double radiusY, double start, double sweep, double angleRadiusX, double angleRadiusY)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var from = builder.Current;
        var first = Parametric(angleRadiusX, angleRadiusY, start);
        var last = Parametric(angleRadiusX, angleRadiusY, start + sweep);
        var parametricSweep = last - first;
        if (Math.Abs(sweep) >= FullTurn - 1e-9) parametricSweep = Math.Sign(sweep) * FullTurn;
        else if (sweep > 0 && parametricSweep < 0) parametricSweep += FullTurn;
        else if (sweep < 0 && parametricSweep > 0) parametricSweep -= FullTurn;
        var centerX = from.X - radiusX * Math.Cos(first);
        var centerY = from.Y - radiusY * Math.Sin(first);
        builder.Ellipse(centerX, centerY, radiusX, radiusY, first, parametricSweep, connect: true);
    }

    private double Parametric(double radiusX, double radiusY, double angle) =>
        Math.Atan2(radiusX * Math.Sin(angle), radiusY * Math.Cos(angle));
}
