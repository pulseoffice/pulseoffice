using PulseOffice.Drawing.Shapes.Geometry;

namespace PulseOffice.Drawing.Shapes;

public readonly record struct ShapeTextMargins(double Left, double Top, double Right, double Bottom)
{
    public static ShapeTextMargins None { get; } = new(0, 0, 0, 0);

    public double Width(double width) => Math.Max(1, width - Left - Right);

    public double Height(double height) => Math.Max(1, height - Top - Bottom);

    public double Across => Left + Right;

    public double Down => Top + Bottom;
}

public interface IShapeTextArea
{
    ShapeTextMargins Margins(ShapeObject shape, double width, double height);
}

public sealed class ShapeTextArea(IPresetGeometryEngine presets, IShapePresets names, IEnhancedPathInterpreter formulas) : IShapeTextArea
{
    private const double RoundedCornerInset = 0.2928932;

    public ShapeTextArea() : this(new PresetGeometryEngine(), new ShapePresets(), new EnhancedPathInterpreter())
    {
    }

    public ShapeTextArea(IPresetGeometryEngine presets, IShapePresets names) : this(presets, names, new EnhancedPathInterpreter())
    {
    }

    public ShapeTextMargins Margins(ShapeObject shape, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(shape);
        if (shape.Dimension is not null || width <= 0 || height <= 0) return ShapeTextMargins.None;
        if (shape.Computed is PresetGeometry preset) return Preset(preset, width, height);
        if (shape.Computed is FormulaGeometry formula) return Formula(formula, shape.Style.Text.Layout.IsVertical, width, height);
        if (shape.Computed is not null) return ShapeTextMargins.None;
        if (shape.Shape == ShapeKind.RoundedRectangle)
            return Rounded(width, height, Math.Clamp(double.IsFinite(shape.CornerRatio) ? shape.CornerRatio : 0.18, 0, 0.5));
        return Preset(new PresetGeometry(names.OoxmlName(shape.Shape)), width, height);
    }

    private ShapeTextMargins Formula(FormulaGeometry geometry, bool vertical, double width, double height) =>
        formulas.TextArea(geometry, vertical) is { } area
            ? new ShapeTextMargins(width * area.Left, height * area.Top, width * (1 - area.Right), height * (1 - area.Bottom))
            : ShapeTextMargins.None;

    private ShapeTextMargins Preset(PresetGeometry geometry, double width, double height) =>
        presets.TextBounds(geometry, width, height) is { } bounds
            ? new ShapeTextMargins(bounds.Left, bounds.Top, width - bounds.Right, height - bounds.Bottom)
            : ShapeTextMargins.None;

    private ShapeTextMargins Rounded(double width, double height, double corner)
    {
        var inset = corner * RoundedCornerInset * Math.Min(width, height);
        return new ShapeTextMargins(inset, inset, inset, inset);
    }
}
