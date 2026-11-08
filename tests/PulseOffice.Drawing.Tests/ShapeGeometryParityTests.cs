using PulseOffice.Drawing.Printing;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Drawing.Shapes.Geometry;
using PulseOffice.Foundation.Units;
using PulseOffice.Printing;
using Xunit;

namespace PulseOffice.Drawing.Tests;

public sealed class ShapeGeometryParityTests
{
    private readonly ShapeGeometry _geometry = new();
    private readonly ShapeAdjustHandles _handles = new();
    private readonly NativeShapeModifiers _native = new();
    private readonly ObjectPathBuilder _paths = new();

    [Theory]
    [InlineData(ShapeKind.Cylinder, ShapeShade.Lighten)]
    [InlineData(ShapeKind.Bevel, ShapeShade.Darken)]
    [InlineData(ShapeKind.FoldedCorner, ShapeShade.DarkenLess)]
    public void Shapes_with_their_own_kind_are_shaded_before_and_after_the_handle_moves(ShapeKind kind, ShapeShade shade)
    {
        var shape = new ShapeObject { Id = "s", Shape = kind };
        Assert.Contains(_geometry.Outline(shape, 300, 200).Figures, figure => figure.Shade == shade);

        var adjusted = _handles.Drag(shape, 0, new ShapePoint(0.1, 0.1), 300, 200);
        Assert.IsType<PresetGeometry>(adjusted.Computed);
        Assert.Contains(_geometry.Outline(adjusted, 300, 200).Figures, figure => figure.Shade == shade);
    }

    [Fact]
    public void Plain_shapes_keep_their_own_outline()
    {
        var shape = new ShapeObject { Id = "s", Shape = ShapeKind.RoundedRectangle, CornerRatio = 0.3 };
        Assert.Equal(_geometry.Outline(shape).Figures.Count, _geometry.Outline(shape, 300, 200).Figures.Count);
        Assert.All(_geometry.Outline(shape, 300, 200).Figures, figure => Assert.Equal(ShapeShade.Normal, figure.Shade));
    }

    [Fact]
    public void Odf_polar_handles_follow_the_pointer_in_radius_and_angle()
    {
        var formula = new FormulaGeometry("M 0 0 L 21600 0 21600 21600 0 21600 Z N")
        {
            Modifiers = [5000, 0],
            Handles = [new EnhancedHandle([
                new("handle-position", "$0 $1"), new("handle-polar", "10800 10800"),
                new("handle-radius-range-minimum", "0"), new("handle-radius-range-maximum", "10800")])],
            ReferenceWidth = 200,
            ReferenceHeight = 200,
        };
        var shape = new ShapeObject { Id = "p", Shape = ShapeKind.Rectangle, Computed = formula };
        var start = Assert.Single(_handles.Handles(shape, 200, 200)).Position;
        Assert.Equal((10800 + 5000) / 21600.0, start.X, 6);
        Assert.Equal(0.5, start.Y, 6);

        var moved = _handles.Drag(shape, 0, new ShapePoint(0.5, 0.25), 200, 200);
        var modifiers = ((FormulaGeometry)moved.Computed!).Modifiers;
        Assert.Equal(5400, modifiers[0], 0);
        Assert.Equal(-90, modifiers[1], 1);
        var at = Assert.Single(_handles.Handles(moved, 200, 200)).Position;
        Assert.Equal(0.5, at.X, 3);
        Assert.Equal(0.25, at.Y, 3);

        var clamped = (FormulaGeometry)_handles.Drag(shape, 0, new ShapePoint(1.4, 0.5), 200, 200).Computed!;
        Assert.Equal(10800, clamped.Modifiers[0], 0);
    }

    [Fact]
    public void Native_odf_modifiers_become_ooxml_adjustments()
    {
        Assert.Equal(25000, _native.Preset("round-rectangle", [5400], 300, 200)!.Adjustments.Single().Value, 0);
        Assert.Equal(12500, _native.Preset("paper", [18900], 300, 200)!.Adjustments.Single().Value, 0);
        Assert.Equal(50000, _native.Preset("parallelogram", [5400], 200, 100)!.Adjustments.Single().Value, 0);
        Assert.Equal(50000, _native.Preset("isosceles-triangle", [10800], 200, 100)!.Adjustments.Single().Value, 0);
        Assert.Equal(16667, _native.Preset("round-rectangle", [], 300, 200)!.Adjustments.Single().Value, 0);
        Assert.Equal(20000, _native.Preset("can", [4320], 400, 600)!.Adjustments.Single().Value, 0);
        Assert.Null(_native.Preset("mso-spt100", [5400], 300, 200));
        Assert.Null(_native.Preset("ooxml-roundRect", [5400], 300, 200));
    }

    [Fact]
    public void Print_paths_fill_stroke_and_shade_each_figure_on_its_own()
    {
        var shape = new ShapeObject
        {
            Id = "c",
            Shape = ShapeKind.Rectangle,
            Computed = new PresetGeometry("cube"),
            Style = ShapeStyle.Default with { Fill = ShapeStyle.Default.Fill with { Kind = ShapeFillKind.Solid, ColorHex = "#3366CC" } },
        };
        var bounds = new DocumentRect(new Twip(0), new Twip(0), new Twip(3000), new Twip(2000));
        var paths = _paths.Build(shape, bounds).ToList();

        Assert.Contains(paths, path => path.Fill == PrintColor.Black && Math.Abs(path.FillOpacity - 0.2) < 1e-9 && path.Stroke is null);
        Assert.Contains(paths, path => path.Fill == PrintColor.White && Math.Abs(path.FillOpacity - 0.2) < 1e-9 && path.Stroke is null);
        var outline = Assert.Single(paths, path => path.Stroke is not null);
        Assert.Null(outline.Fill);
        Assert.Equal(3, paths.Count(path => path.Fill == PrintColor.FromHex("#3366CC")));
    }

    [Fact]
    public void Print_paths_leave_unfilled_and_unstroked_figures_alone()
    {
        var shape = new ShapeObject
        {
            Id = "f",
            Shape = ShapeKind.Rectangle,
            Computed = new FormulaGeometry("M 0 0 L 21600 0 21600 21600 0 21600 Z N F M 0 0 L 21600 21600 N S M 0 0 L 10800 0 10800 10800 Z N") { ReferenceWidth = 100, ReferenceHeight = 100 },
            Style = ShapeStyle.Default with { Fill = ShapeStyle.Default.Fill with { Kind = ShapeFillKind.Solid, ColorHex = "#3366CC" } },
        };
        var paths = _paths.Build(shape, new DocumentRect(new Twip(0), new Twip(0), new Twip(1000), new Twip(1000))).ToList();
        Assert.Equal(2, Assert.Single(paths, path => path.Fill is not null).Outline.Figures.Count);
        Assert.Equal(2, Assert.Single(paths, path => path.Stroke is not null).Outline.Figures.Count);
    }
}
