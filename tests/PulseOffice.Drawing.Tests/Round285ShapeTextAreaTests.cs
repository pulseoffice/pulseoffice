using PulseOffice.Drawing.Shapes;
using PulseOffice.Drawing.Shapes.Geometry;
using Xunit;

namespace PulseOffice.Drawing.Tests;

public sealed class Round285ShapeTextAreaTests
{
    private static readonly PresetGeometryEngine Engine = new();

    private static readonly ShapeTextArea Area = new(Engine, new ShapePresets(), new EnhancedPathInterpreter());

    [Theory]
    [InlineData(ShapeKind.ArrowRight, "rightArrow")]
    [InlineData(ShapeKind.Octagon, "octagon")]
    [InlineData(ShapeKind.Hexagon, "hexagon")]
    [InlineData(ShapeKind.FlowDocument, "flowChartDocument")]
    [InlineData(ShapeKind.FlowPredefinedProcess, "flowChartPredefinedProcess")]
    [InlineData(ShapeKind.Cylinder, "can")]
    public void Arrows_flowchart_shapes_and_polygons_use_the_preset_text_rectangle(ShapeKind kind, string preset)
    {
        var margins = Area.Margins(new ShapeObject { Id = "s", Shape = kind }, 150, 90);
        var bounds = Engine.TextBounds(new PresetGeometry(preset), 150, 90)!.Value;

        Assert.Equal(bounds.Left, margins.Left, 6);
        Assert.Equal(bounds.Top, margins.Top, 6);
        Assert.Equal(150 - bounds.Right, margins.Right, 6);
        Assert.Equal(90 - bounds.Bottom, margins.Bottom, 6);
    }

    [Fact]
    public void Enhanced_geometry_text_areas_give_the_shape_its_inset()
    {
        var geometry = new FormulaGeometry("M 0 0 L 21600 0 21600 21600 0 21600 Z")
        {
            Equations = [new EnhancedEquation("f0", "$0 / 2"), new EnhancedEquation("f1", "21600 - ?f0")],
            Modifiers = [5400],
            TextAreas = "?f0 2160 ?f1 19440",
        };
        var margins = Area.Margins(new ShapeObject { Id = "f", Shape = ShapeKind.Rectangle, Computed = geometry }, 200, 100);

        Assert.Equal(200 * 2700.0 / 21600, margins.Left, 6);
        Assert.Equal(200 * 2700.0 / 21600, margins.Right, 6);
        Assert.Equal(10, margins.Top, 6);
        Assert.Equal(10, margins.Bottom, 6);
    }

    [Fact]
    public void Enhanced_geometry_without_text_areas_keeps_the_whole_box()
    {
        var geometry = new FormulaGeometry("M 0 0 L 21600 0 21600 21600 Z");

        Assert.Equal(ShapeTextMargins.None, Area.Margins(new ShapeObject { Id = "f", Shape = ShapeKind.Rectangle, Computed = geometry }, 200, 100));
    }
}
