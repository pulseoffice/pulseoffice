using PulseOffice.Drawing.Shapes;
using PulseOffice.Drawing.Shapes.Geometry;
using Xunit;

namespace PulseOffice.Drawing.Tests;

public sealed class Round281PresetTextAreaTests
{
    private static readonly PresetGeometryEngine Engine = new();

    private static readonly ShapeTextArea Area = new(Engine, new ShapePresets());

    [Fact]
    public void Adjusted_preset_geometry_moves_the_text_area_with_its_adjust_value()
    {
        var narrow = new ShapeObject { Id = "a", Shape = ShapeKind.RoundedRectangle, Computed = new PresetGeometry("roundRect") { Adjustments = [new("adj", 5000)] } };
        var wide = narrow with { Computed = new PresetGeometry("roundRect") { Adjustments = [new("adj", 50000)] } };

        var small = Area.Margins(narrow, 200, 100);
        var large = Area.Margins(wide, 200, 100);

        Assert.True(small.Left > 0);
        Assert.True(large.Left > small.Left * 5);
        Assert.Equal(large.Left, large.Right, 6);
    }

    [Theory]
    [InlineData(ShapeKind.Star5, "star5")]
    [InlineData(ShapeKind.Heart, "heart")]
    [InlineData(ShapeKind.Cloud, "cloud")]
    [InlineData(ShapeKind.Pentagon, "pentagon")]
    public void Star_heart_cloud_and_pentagon_use_the_exact_preset_text_rectangle(ShapeKind kind, string preset)
    {
        var margins = Area.Margins(new ShapeObject { Id = "s", Shape = kind }, 120, 80);
        var bounds = Engine.TextBounds(new PresetGeometry(preset), 120, 80)!.Value;

        Assert.Equal(bounds.Left, margins.Left, 6);
        Assert.Equal(80 - bounds.Bottom, margins.Bottom, 6);
        Assert.True(margins.Across < 120 && margins.Down < 80);
    }
}
