using System.Globalization;

namespace PulseOffice.Drawing.Shapes.Geometry;

public sealed record AdjustHandle(int Index, ShapePoint Position);

public sealed record AdjustAxis(int Value, double Minimum, double Maximum);

public interface IShapeAdjustHandles
{
    IReadOnlyList<AdjustHandle> Handles(ShapeObject shape, double width, double height);

    ShapeObject Drag(ShapeObject shape, int handle, ShapePoint target, double width, double height);
}

public abstract class AdjustableGeometry
{
    public abstract IReadOnlyList<double> Values { get; }

    public abstract int Count { get; }

    public abstract double Granularity { get; }

    public abstract ShapePoint Position(int handle, IReadOnlyList<double> values);

    public abstract IReadOnlyList<AdjustAxis> Axes(int handle, IReadOnlyList<double> values);

    public abstract ComputedGeometry With(IReadOnlyList<double> values);
}

public sealed class PresetAdjustableGeometry(
    PresetGeometry geometry, PresetShapeDefinition definition, double width, double height, IDrawingBuiltIns builtIns, IGuideOperators operators)
    : AdjustableGeometry
{
    private readonly IReadOnlyList<double> _values = [.. definition.Adjustments.Select(adjustment =>
        (geometry.Adjustments.FirstOrDefault(value => value.Name == adjustment.Name)
            ?? (definition.Adjustments.Count == 1 ? geometry.Adjustments.FirstOrDefault() : null))?.Value
        ?? new DrawingGuideScope(1, 1, builtIns, operators).Evaluate(adjustment))];

    public override IReadOnlyList<double> Values => _values;

    public override int Count => definition.Handles.Count;

    public override double Granularity => 1;

    public override ShapePoint Position(int handle, IReadOnlyList<double> values)
    {
        var scope = Scope(values);
        var position = definition.Handles[handle].Position;
        return new ShapePoint(scope.Value(position.X) / Width, scope.Value(position.Y) / Height);
    }

    public override IReadOnlyList<AdjustAxis> Axes(int handle, IReadOnlyList<double> values)
    {
        var scope = Scope(values);
        var found = definition.Handles[handle];
        var axes = new List<AdjustAxis>();
        foreach (var axis in new[] { found.First, found.Second })
        {
            if (axis is null) continue;
            var index = IndexOf(axis.Reference);
            if (index < 0) continue;
            axes.Add(new AdjustAxis(index, scope.Value(axis.Minimum), scope.Value(axis.Maximum)));
        }
        return axes;
    }

    public override ComputedGeometry With(IReadOnlyList<double> values) => geometry with
    {
        Adjustments = [.. definition.Adjustments.Select((adjustment, index) => new GeometryAdjustment(adjustment.Name, values[index]))],
    };

    private double Width => width > 0 ? width : 1;

    private double Height => height > 0 ? height : 1;

    private int IndexOf(string name)
    {
        for (var index = 0; index < definition.Adjustments.Count; index++)
            if (definition.Adjustments[index].Name == name) return index;
        return -1;
    }

    private DrawingGuideScope Scope(IReadOnlyList<double> values)
    {
        var scope = new DrawingGuideScope(Width, Height, builtIns, operators);
        for (var index = 0; index < definition.Adjustments.Count; index++) scope.Set(definition.Adjustments[index].Name, values[index]);
        foreach (var guide in definition.Guides) scope.Define(guide);
        return scope;
    }
}

public sealed class FormulaAdjustableGeometry : AdjustableGeometry
{
    private readonly FormulaGeometry _geometry;
    private readonly IReadOnlyDictionary<string, IEnhancedExpression> _equations;
    private readonly IReadOnlyList<EnhancedAdjustHandle> _handles;
    private readonly double _logicalWidth;
    private readonly double _logicalHeight;

    public FormulaAdjustableGeometry(FormulaGeometry geometry, IEnhancedFormulaParser parser, double width, double height)
    {
        _geometry = geometry;
        var equations = new Dictionary<string, IEnhancedExpression>(StringComparer.Ordinal);
        foreach (var equation in geometry.Equations) equations[equation.Name] = parser.Parse(equation.Formula);
        _equations = equations;
        var view = new EnhancedView(geometry.ViewLeft, geometry.ViewTop, geometry.ViewWidth, geometry.ViewHeight);
        var kinds = new EnhancedHandleFactory(parser, view);
        _handles = [.. geometry.Handles
            .Select(handle => kinds.Create(handle.Attributes.GroupBy(pair => pair.Key).ToDictionary(group => group.Key, group => group.First().Value, StringComparer.Ordinal)))
            .OfType<EnhancedAdjustHandle>()];
        var area = geometry.ReferenceWidth * geometry.ReferenceHeight;
        var requested = width * height;
        var scale = area > 0 && requested > 0 ? Math.Sqrt(area / requested) : 1;
        _logicalWidth = width * scale;
        _logicalHeight = height * scale;
    }

    public override IReadOnlyList<double> Values => _geometry.Modifiers;

    public override int Count => _handles.Count;

    public override double Granularity => 0;

    public override ShapePoint Position(int handle, IReadOnlyList<double> values) => _handles[handle].Position(Scope(values));

    public override IReadOnlyList<AdjustAxis> Axes(int handle, IReadOnlyList<double> values) =>
        [.. _handles[handle].Axes(Scope(values)).Where(axis => axis.Value < values.Count)];

    public override ComputedGeometry With(IReadOnlyList<double> values) => _geometry with { Modifiers = [.. values] };

    private EnhancedScope Scope(IReadOnlyList<double> values) =>
        new(_equations, values, _geometry.ViewLeft, _geometry.ViewTop, _geometry.ViewWidth, _geometry.ViewHeight, _logicalWidth, _logicalHeight);
}

public sealed record EnhancedView(double Left, double Top, double Width, double Height)
{
    public ShapePoint Normalize(double x, double y) => new((x - Left) / Width, (y - Top) / Height);
}

public sealed class EnhancedHandleFactory(IEnhancedFormulaParser parser, EnhancedView view)
{
    public EnhancedAdjustHandle? Create(IReadOnlyDictionary<string, string> attributes)
    {
        EnhancedAdjustHandle handle = attributes.ContainsKey("handle-polar")
            ? new PolarEnhancedHandle(attributes, parser, view)
            : new CartesianEnhancedHandle(attributes, parser, view);
        return handle.Movable ? handle : null;
    }
}

public abstract class EnhancedAdjustHandle(IReadOnlyDictionary<string, string> attributes, IEnhancedFormulaParser parser, EnhancedView view)
{
    protected EnhancedView View => view;

    protected string[] Tokens { get; } = attributes.TryGetValue("handle-position", out var position) ? position.Split(' ', StringSplitOptions.RemoveEmptyEntries) : [];

    public bool Movable => Tokens.Length == 2 && Tokens.Any(token => Modifier(token) >= 0);

    public abstract ShapePoint Position(EnhancedScope scope);

    public abstract IEnumerable<AdjustAxis> Axes(EnhancedScope scope);

    protected double Evaluate(string expression, EnhancedScope scope) => parser.Parse(expression).Evaluate(scope);

    protected double Evaluate(string key, double fallback, EnhancedScope scope) =>
        attributes.TryGetValue(key, out var expression) ? Evaluate(expression, scope) : fallback;

    protected bool Has(string key) => attributes.ContainsKey(key);

    protected string Attribute(string key) => attributes[key];

    protected int Modifier(string token) =>
        token.Length > 1 && token[0] == '$' && int.TryParse(token[1..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) ? index : -1;
}

public sealed class CartesianEnhancedHandle(IReadOnlyDictionary<string, string> attributes, IEnhancedFormulaParser parser, EnhancedView view)
    : EnhancedAdjustHandle(attributes, parser, view)
{
    public override ShapePoint Position(EnhancedScope scope) => View.Normalize(Evaluate(Tokens[0], scope), Evaluate(Tokens[1], scope));

    public override IEnumerable<AdjustAxis> Axes(EnhancedScope scope)
    {
        if (Modifier(Tokens[0]) is var x and >= 0)
            yield return new AdjustAxis(x, Evaluate("handle-range-x-minimum", View.Left, scope), Evaluate("handle-range-x-maximum", View.Left + View.Width, scope));
        if (Modifier(Tokens[1]) is var y and >= 0)
            yield return new AdjustAxis(y, Evaluate("handle-range-y-minimum", View.Top, scope), Evaluate("handle-range-y-maximum", View.Top + View.Height, scope));
    }
}

public sealed class PolarEnhancedHandle(IReadOnlyDictionary<string, string> attributes, IEnhancedFormulaParser parser, EnhancedView view)
    : EnhancedAdjustHandle(attributes, parser, view)
{
    private const double HalfTurn = 180;

    public override ShapePoint Position(EnhancedScope scope)
    {
        var (x, y) = Centre(scope);
        var radius = Evaluate(Tokens[0], scope);
        var angle = Evaluate(Tokens[1], scope) * Math.PI / HalfTurn;
        return View.Normalize(x + radius * Math.Cos(angle), y + radius * Math.Sin(angle));
    }

    public override IEnumerable<AdjustAxis> Axes(EnhancedScope scope)
    {
        if (Modifier(Tokens[0]) is var radius and >= 0)
            yield return new AdjustAxis(radius, Evaluate("handle-radius-range-minimum", 0, scope), Evaluate("handle-radius-range-maximum", Math.Max(View.Width, View.Height), scope));
        if (Modifier(Tokens[1]) is var angle and >= 0)
            yield return new AdjustAxis(angle, -HalfTurn, HalfTurn);
    }

    private (double X, double Y) Centre(EnhancedScope scope)
    {
        var centre = Has("handle-polar") ? Attribute("handle-polar").Split(' ', StringSplitOptions.RemoveEmptyEntries) : [];
        return centre.Length == 2
            ? (Evaluate(centre[0], scope), Evaluate(centre[1], scope))
            : (View.Left + View.Width / 2, View.Top + View.Height / 2);
    }
}

public interface IAdjustHandleSolver
{
    IReadOnlyList<double> Solve(AdjustableGeometry geometry, int handle, ShapePoint target, double width, double height);
}

public sealed class AdjustHandleSolver : IAdjustHandleSolver
{
    private const int Samples = 96;
    private const int Refinements = 48;
    private const int Rounds = 3;
    private const double MinimumSpan = 100000;
    private const double WholeRange = 50000000;
    private const double GoldenRatio = 0.6180339887498949;

    public IReadOnlyList<double> Solve(AdjustableGeometry geometry, int handle, ShapePoint target, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        var values = geometry.Values.ToArray();
        for (var round = 0; round < Rounds; round++)
            foreach (var axis in geometry.Axes(handle, values))
            {
                var (low, high) = Bounds(values[axis.Value], axis);
                values[axis.Value] = Best(geometry, handle, target, width, height, values, axis.Value, low, high);
            }
        if (geometry.Granularity > 0)
            foreach (var axis in geometry.Axes(handle, values)) values[axis.Value] = Math.Round(values[axis.Value] / geometry.Granularity) * geometry.Granularity;
        return values;
    }

    private (double Low, double High) Bounds(double current, AdjustAxis axis)
    {
        var minimum = Math.Min(axis.Minimum, axis.Maximum);
        var maximum = Math.Max(axis.Minimum, axis.Maximum);
        if (maximum - minimum <= WholeRange) return (minimum, maximum);
        var span = 4 * Math.Max(Math.Abs(current), MinimumSpan);
        return (Math.Max(minimum, current - span), Math.Min(maximum, current + span));
    }

    private double Best(AdjustableGeometry geometry, int handle, ShapePoint target, double width, double height, double[] values, int index, double low, double high)
    {
        if (high <= low) return low;
        double Cost(double candidate)
        {
            values[index] = candidate;
            var at = geometry.Position(handle, values);
            var dx = (at.X - target.X) * width;
            var dy = (at.Y - target.Y) * height;
            return dx * dx + dy * dy;
        }
        var original = values[index];
        var step = (high - low) / Samples;
        var bestIndex = 0;
        var bestCost = double.MaxValue;
        for (var sample = 0; sample <= Samples; sample++)
        {
            var cost = Cost(low + sample * step);
            if (cost < bestCost - 1e-12)
            {
                bestCost = cost;
                bestIndex = sample;
            }
        }
        var left = low + Math.Max(0, bestIndex - 1) * step;
        var right = low + Math.Min(Samples, bestIndex + 1) * step;
        for (var iteration = 0; iteration < Refinements && right - left > 1e-6; iteration++)
        {
            var first = right - GoldenRatio * (right - left);
            var second = left + GoldenRatio * (right - left);
            if (Cost(first) <= Cost(second)) right = second;
            else left = first;
        }
        var refined = (left + right) / 2;
        var result = Cost(refined) <= Cost(low + bestIndex * step) ? refined : low + bestIndex * step;
        values[index] = original;
        return result;
    }
}

public sealed class ShapeAdjustHandles(
    IPresetShapeCatalog catalog, IImplicitPresetGeometry implicitPresets, IDrawingBuiltIns builtIns, IGuideOperators operators,
    IEnhancedFormulaParser parser, IAdjustHandleSolver solver) : IShapeAdjustHandles
{
    public ShapeAdjustHandles() : this(new PresetShapeCatalog())
    {
    }

    private ShapeAdjustHandles(IPresetShapeCatalog catalog)
        : this(catalog, new ImplicitPresetGeometry(catalog, new ShapePresets()), new DrawingBuiltIns(), new GuideOperators(), new EnhancedFormulaParser(), new AdjustHandleSolver())
    {
    }

    public IReadOnlyList<AdjustHandle> Handles(ShapeObject shape, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(shape);
        if (Adjustable(shape, width, height) is not { } geometry) return [];
        try
        {
            return [.. Enumerable.Range(0, geometry.Count)
                .Select(index => new AdjustHandle(index, geometry.Position(index, geometry.Values)))
                .Where(handle => double.IsFinite(handle.Position.X) && double.IsFinite(handle.Position.Y))];
        }
        catch (FormatException)
        {
            return [];
        }
    }

    public ShapeObject Drag(ShapeObject shape, int handle, ShapePoint target, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(shape);
        if (Adjustable(shape, width, height) is not { } geometry || handle < 0 || handle >= geometry.Count) return shape;
        try
        {
            return shape with { Computed = geometry.With(solver.Solve(geometry, handle, target, Math.Max(1, width), Math.Max(1, height))) };
        }
        catch (FormatException)
        {
            return shape;
        }
    }

    private AdjustableGeometry? Adjustable(ShapeObject shape, double width, double height) => (shape.Computed ?? implicitPresets.Adjustable(shape, width, height)) switch
    {
        PresetGeometry preset when catalog.Find(preset.Name) is { Handles.Count: > 0 } definition =>
            new PresetAdjustableGeometry(preset, definition, width, height, builtIns, operators),
        FormulaGeometry formula when formula.Handles.Count > 0 => new FormulaAdjustableGeometry(formula, parser, width, height),
        _ => null,
    };
}
