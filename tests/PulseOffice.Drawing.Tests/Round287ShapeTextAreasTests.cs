using PulseOffice.Drawing.Shapes;
using PulseOffice.Drawing.Shapes.Geometry;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Drawing.Tests;

public sealed class Round287ShapeTextAreasTests
{
    private static readonly ShapeTextArea Area = new(new PresetGeometryEngine(), new ShapePresets(), new EnhancedPathInterpreter());

    private static readonly FormulaGeometry Geometry = new("M 0 0 L 21600 0 21600 21600 0 21600 Z")
    {
        TextAreas = "2160 2160 19440 19440 0 0 10800 21600",
    };

    private static ShapeObject Shape(TextFlow flow) => new()
    {
        Id = "f",
        Shape = ShapeKind.Rectangle,
        Computed = Geometry,
        Style = new ShapeStyle { Text = new ShapeTextStyle { Layout = new TextLayout { Flow = flow } } },
    };

    [Fact]
    public void Horizontal_text_uses_the_first_text_area()
    {
        var margins = Area.Margins(Shape(TextFlow.Horizontal), 200, 100);

        Assert.Equal(20, margins.Left, 6);
        Assert.Equal(10, margins.Top, 6);
    }

    [Fact]
    public void Vertical_text_uses_the_second_text_area_when_there_is_one()
    {
        var margins = Area.Margins(Shape(TextFlow.Vertical), 200, 100);

        Assert.Equal(0, margins.Left, 6);
        Assert.Equal(100, margins.Right, 6);
        Assert.Equal(0, margins.Top, 6);
    }
}
