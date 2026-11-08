namespace PulseOffice.Drawing.Shapes.Geometry;

public interface IComputedGeometryEngine
{
    ShapeOutline Outline(ComputedGeometry geometry);

    ShapeOutline Outline(ComputedGeometry geometry, double width, double height);
}

public sealed class ComputedGeometryEngine(IPresetGeometryEngine presets, IEnhancedPathInterpreter formulas) : IComputedGeometryEngine
{
    public ComputedGeometryEngine() : this(new PresetGeometryEngine(), new EnhancedPathInterpreter())
    {
    }

    public ShapeOutline Outline(ComputedGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        return Outline(geometry, geometry.ReferenceWidth, geometry.ReferenceHeight);
    }

    public ShapeOutline Outline(ComputedGeometry geometry, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        try
        {
            return geometry.Accept(new Evaluation(presets, formulas, width, height));
        }
        catch (FormatException)
        {
            return ShapeOutline.Empty;
        }
    }

    private sealed class Evaluation(IPresetGeometryEngine presets, IEnhancedPathInterpreter formulas, double width, double height)
        : IComputedGeometryVisitor<ShapeOutline>
    {
        public ShapeOutline VisitPreset(PresetGeometry geometry) => presets.Outline(geometry, width, height);

        public ShapeOutline VisitFormula(FormulaGeometry geometry)
        {
            var area = geometry.ReferenceWidth * geometry.ReferenceHeight;
            var requested = width * height;
            var scale = area > 0 && requested > 0 ? Math.Sqrt(area / requested) : 1;
            return formulas.Outline(geometry, width * scale, height * scale);
        }
    }
}
