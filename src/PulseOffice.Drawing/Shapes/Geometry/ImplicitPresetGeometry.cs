namespace PulseOffice.Drawing.Shapes.Geometry;

public interface IImplicitPresetGeometry
{
    PresetGeometry? Adjustable(ShapeObject shape, double width, double height);

    PresetGeometry? Shaded(ShapeObject shape, double width, double height);
}

public sealed class ImplicitPresetGeometry(IPresetShapeCatalog catalog, IShapePresets presets) : IImplicitPresetGeometry
{
    private readonly IReadOnlySet<ShapeKind> _ownAdjustments = new HashSet<ShapeKind> { ShapeKind.Arc, ShapeKind.Pie, ShapeKind.Chord, ShapeKind.Line };

    public ImplicitPresetGeometry() : this(new PresetShapeCatalog(), new ShapePresets())
    {
    }

    public PresetGeometry? Adjustable(ShapeObject shape, double width, double height) =>
        Implicit(shape, width, height, definition => definition.Handles.Count > 0);

    public PresetGeometry? Shaded(ShapeObject shape, double width, double height) =>
        Implicit(shape, width, height, definition => definition.Paths.Any(path => path.Shade is not ShapeShade.Normal));

    private PresetGeometry? Implicit(ShapeObject shape, double width, double height, Func<PresetShapeDefinition, bool> wanted)
    {
        ArgumentNullException.ThrowIfNull(shape);
        if (shape.Computed is not null || _ownAdjustments.Contains(shape.Shape)) return null;
        var name = presets.OoxmlName(shape.Shape);
        return catalog.Find(name) is { } definition && wanted(definition)
            ? new PresetGeometry(name) { ReferenceWidth = Math.Max(1, width), ReferenceHeight = Math.Max(1, height) }
            : null;
    }
}
