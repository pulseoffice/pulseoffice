using System.Globalization;
using DocumentFormat.OpenXml;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Drawing.Shapes.Geometry;
using A = DocumentFormat.OpenXml.Drawing;

namespace PulseOffice.Formats.OpenXml;

public sealed class DrawingPresetGeometry(IShapePresets presets, IPresetGeometryEngine engine, IShapeGeometry geometry)
{
    private const string First = "adj1";
    private const string Second = "adj2";
    private const double EmuPerHundredthMillimetre = 360;
    private readonly ArcAdjustments _arcs = new();
    private readonly OoxmlGeometry _custom = new();

    public DrawingPresetGeometry(IShapePresets presets) : this(presets, new PresetGeometryEngine(), new ShapeGeometry())
    {
    }

    public DrawingPresetGeometry() : this(new ShapePresets())
    {
    }

    public OpenXmlElement Build(ShapeObject shape)
    {
        ArgumentNullException.ThrowIfNull(shape);
        if (shape.Computed is PresetGeometry preset)
            return new A.PresetGeometry(new A.AdjustValueList(preset.Adjustments.Select(adjustment => Guide(adjustment.Name, (long)Math.Round(adjustment.Value)))))
            {
                Preset = new A.ShapeTypeValues(preset.Name),
            };
        if (shape.Computed is FormulaGeometry) return _custom.CustomGeometry(geometry.Outline(shape));
        var adjustments = new A.AdjustValueList();
        if (_arcs.Applies(shape.Shape))
        {
            var (first, second) = _arcs.ToOoxml(shape);
            adjustments.Append(Guide(First, first), Guide(Second, second));
        }
        return new A.PresetGeometry(adjustments) { Preset = new A.ShapeTypeValues(presets.OoxmlName(shape.Shape)) };
    }

    public ShapeObject Shaped(ShapeObject shape, string? preset, A.PresetGeometry? element)
    {
        ArgumentNullException.ThrowIfNull(shape);
        var adjustments = element?.AdjustValueList;
        if (!presets.Knows(preset) && engine.Knows(preset))
            return shape with { Shape = ShapeKind.Rectangle, Computed = Computed(preset!, element, adjustments) };
        var kind = presets.KindOf(preset);
        if (!_arcs.Applies(kind))
            return engine.Knows(preset) && Computed(preset!, element, adjustments) is { Adjustments.Count: > 0 } adjusted && engine.Adjusted(adjusted)
                ? shape with { Shape = kind, Computed = adjusted }
                : shape with { Shape = kind };
        var (start, end) = _arcs.FromOoxml(kind, Value(adjustments, First), Value(adjustments, Second));
        return shape with { Shape = kind, ArcStartDegrees = start, ArcEndDegrees = end };
    }

    private PresetGeometry Computed(string preset, A.PresetGeometry? element, A.AdjustValueList? adjustments)
    {
        var extents = element?.Parent?.GetFirstChild<A.Transform2D>()?.Extents;
        return new PresetGeometry(preset)
        {
            Adjustments = [.. (adjustments?.Elements<A.ShapeGuide>() ?? []).Select(guide => (Name: guide.Name?.Value, Value: Value(guide)))
                .Where(guide => guide.Name is not null && guide.Value is not null)
                .Select(guide => new GeometryAdjustment(guide.Name!, guide.Value!.Value))],
            ReferenceWidth = extents?.Cx?.Value is > 0 and var width ? width / EmuPerHundredthMillimetre : 1,
            ReferenceHeight = extents?.Cy?.Value is > 0 and var height ? height / EmuPerHundredthMillimetre : 1,
        };
    }

    private A.ShapeGuide Guide(string name, long value) =>
        new() { Name = name, Formula = "val " + value.ToString(CultureInfo.InvariantCulture) };

    private long? Value(A.AdjustValueList? adjustments, string name) =>
        adjustments?.Elements<A.ShapeGuide>().FirstOrDefault(guide => guide.Name?.Value == name) is { } guide ? Value(guide) : null;

    private long? Value(A.ShapeGuide guide) =>
        guide.Formula?.Value is { } formula
        && formula.StartsWith("val ", StringComparison.Ordinal)
        && long.TryParse(formula[4..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
}
