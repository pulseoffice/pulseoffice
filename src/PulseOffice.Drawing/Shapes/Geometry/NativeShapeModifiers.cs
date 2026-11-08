namespace PulseOffice.Drawing.Shapes.Geometry;

public enum NativeExtent
{
    Shortest,
    Width,
    Height,
}

public sealed record NativeModifierRule(int Modifier, string Adjustment, double Scale, double Offset = 0)
{
    public NativeExtent Native { get; init; } = NativeExtent.Shortest;

    public NativeExtent Preset { get; init; } = NativeExtent.Shortest;
}

public sealed record NativeShapeMapping(string Type, string Preset, IReadOnlyList<NativeModifierRule> Rules);

public sealed class NativeShapeMappingTable
{
    private const double Units = 21600;
    private const double Whole = 100000;
    private const double Proportional = Whole / Units;

    public IReadOnlyList<NativeShapeMapping> All { get; } =
    [
        new("round-rectangle", "roundRect", [new(0, "adj", Proportional)]),
        new("can", "can", [new(0, "adj", Proportional)]),
        new("cube", "cube", [new(0, "adj", Proportional)]),
        new("quad-bevel", "bevel", [new(0, "adj", Proportional)]),
        new("isosceles-triangle", "triangle", [new(0, "adj", Proportional) { Native = NativeExtent.Width, Preset = NativeExtent.Width }]),
        new("parallelogram", "parallelogram", [new(0, "adj", Proportional) { Native = NativeExtent.Width }]),
        new("hexagon", "hexagon", [new(0, "adj", Proportional) { Native = NativeExtent.Width }]),
        new("ring", "donut", [new(0, "adj", Proportional)]),
        new("plaque", "plaque", [new(0, "adj", Proportional)]),
        new("paper", "foldedCorner", [new(0, "adj", -Proportional, Whole)]),
    ];
}

public interface INativeShapeModifiers
{
    bool Maps(string? type);

    PresetGeometry? Preset(string? type, IReadOnlyList<double> modifiers, double width, double height);
}

public sealed class NativeShapeModifiers(IPresetGeometryEngine engine, IReadOnlyList<NativeShapeMapping> mappings) : INativeShapeModifiers
{
    private readonly IReadOnlyDictionary<string, NativeShapeMapping> _mappings = mappings.ToDictionary(mapping => mapping.Type, StringComparer.Ordinal);

    public NativeShapeModifiers() : this(new PresetGeometryEngine(), new NativeShapeMappingTable().All)
    {
    }

    public NativeShapeModifiers(IPresetGeometryEngine engine) : this(engine, new NativeShapeMappingTable().All)
    {
    }

    public bool Maps(string? type) => type is not null && _mappings.ContainsKey(type) && engine.Knows(_mappings[type].Preset);

    public PresetGeometry? Preset(string? type, IReadOnlyList<double> modifiers, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(modifiers);
        if (!Maps(type)) return null;
        var mapping = _mappings[type!];
        var adjustments = engine.Defaults(mapping.Preset).ToList();
        foreach (var rule in mapping.Rules)
        {
            var index = adjustments.FindIndex(adjustment => adjustment.Name == rule.Adjustment);
            if (index < 0 || rule.Modifier >= modifiers.Count) continue;
            var ratio = Extent(rule.Native, width, height) / Extent(rule.Preset, width, height);
            adjustments[index] = adjustments[index] with { Value = Math.Round(rule.Offset + rule.Scale * modifiers[rule.Modifier] * ratio) };
        }
        return new PresetGeometry(mapping.Preset)
        {
            Adjustments = adjustments,
            ReferenceWidth = width > 0 ? width : 1,
            ReferenceHeight = height > 0 ? height : 1,
        };
    }

    private double Extent(NativeExtent extent, double width, double height)
    {
        var w = width > 0 ? width : 1;
        var h = height > 0 ? height : 1;
        return extent switch
        {
            NativeExtent.Width => w,
            NativeExtent.Height => h,
            _ => Math.Min(w, h),
        };
    }
}
