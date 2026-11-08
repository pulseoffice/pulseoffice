using PulseOffice.Drawing.Shapes;
using PulseOffice.Drawing.Shapes.Geometry;
using Xunit;

namespace PulseOffice.Drawing.Tests;

public class GeometryShadesAndHandlesTests
{
    private readonly PresetShapeCatalog _catalog = new();
    private readonly PresetGeometryEngine _engine = new();
    private readonly EnhancedPathInterpreter _interpreter = new();
    private readonly PresetFormulaTranslator _translator = new();
    private readonly ShapeAdjustHandles _handles = new();

    [Fact]
    public void Preset_paths_keep_their_darken_and_lighten_shades()
    {
        Assert.Contains(_engine.Outline(new PresetGeometry("cube"), 300, 200).Figures, figure => figure.Shade == ShapeShade.DarkenLess);
        Assert.Contains(_engine.Outline(new PresetGeometry("cube"), 300, 200).Figures, figure => figure.Shade == ShapeShade.LightenLess);
        Assert.Contains(_engine.Outline(new PresetGeometry("can"), 300, 200).Figures, figure => figure.Shade == ShapeShade.Lighten);
        Assert.All(_engine.Outline(new PresetGeometry("rect"), 300, 200).Figures, figure => Assert.Equal(ShapeShade.Normal, figure.Shade));
    }

    [Fact]
    public void Odf_shade_commands_reach_the_figures()
    {
        var outline = _interpreter.Outline(new FormulaGeometry("H M 0 0 L 10 0 10 10 Z N I M 0 0 L 5 5 0 5 Z N J M 0 0 L 1 1 0 1 Z N K M 0 0 L 2 2 0 2 Z N M 0 0 L 3 3 0 3 Z N")
            { ViewWidth = 10, ViewHeight = 10 }, 10, 10);
        Assert.Equal([ShapeShade.Darken, ShapeShade.DarkenLess, ShapeShade.Lighten, ShapeShade.LightenLess, ShapeShade.Normal],
            outline.Figures.Select(figure => figure.Shade));
    }

    [Fact]
    public void Every_preset_translated_to_odf_keeps_its_shades()
    {
        foreach (var name in _catalog.Names)
        {
            var preset = new PresetGeometry(name);
            var expected = _engine.Outline(preset, 3000, 2000).Figures.Select(figure => (figure.Shade, figure.Filled, figure.Stroked)).ToList();
            var actual = _interpreter.Outline(_translator.Translate(preset, 3000, 2000)!, 3000, 2000).Figures.Select(figure => (figure.Shade, figure.Filled, figure.Stroked)).ToList();
            Assert.True(expected.SequenceEqual(actual), name);
        }
    }

    [Fact]
    public void The_translation_writes_the_adjust_handles_for_other_suites()
    {
        var translated = _translator.Translate(new PresetGeometry("roundRect"), 200, 100)!;
        var handle = Assert.Single(translated.Handles).Attributes.ToDictionary(pair => pair.Key, pair => pair.Value);
        Assert.Matches(@"^\?f\d+ \?f\d+$", handle["handle-position"]);
        Assert.Equal("0", handle["handle-range-x-minimum"]);
        Assert.Equal("50000", handle["handle-range-x-maximum"]);

        var polar = _translator.Translate(new PresetGeometry("arc"), 200, 100)!;
        Assert.Equal(2, polar.Handles.Count);
        Assert.All(polar.Handles, item => Assert.Contains(item.Attributes, pair => pair.Key == "handle-position"));
        Assert.Equal(_catalog.Names.Sum(name => _catalog.Find(name)!.Handles.Count),
            _catalog.Names.Sum(name => _translator.Translate(new PresetGeometry(name), 300, 200)!.Handles.Count));
    }

    [Fact]
    public void A_rounded_rectangle_shows_its_handle_and_dragging_it_changes_the_radius()
    {
        var shape = new ShapeObject { Id = "s", Shape = ShapeKind.RoundedRectangle };
        var handle = Assert.Single(_handles.Handles(shape, 200, 100));
        Assert.Equal(16667 / 100000.0 * 100 / 200, handle.Position.X, 3);
        Assert.Equal(0, handle.Position.Y, 6);

        var dragged = _handles.Drag(shape, 0, new ShapePoint(0.2, 0.3), 200, 100);
        var preset = Assert.IsType<PresetGeometry>(dragged.Computed);
        Assert.Equal(ShapeKind.RoundedRectangle, dragged.Shape);
        Assert.Equal("roundRect", preset.Name);
        Assert.Equal(40000, preset.Adjustments.Single(adjustment => adjustment.Name == "adj").Value, 0);

        var clamped = (PresetGeometry)_handles.Drag(shape, 0, new ShapePoint(0.9, 0), 200, 100).Computed!;
        Assert.Equal(50000, clamped.Adjustments.Single().Value, 0);
    }

    [Fact]
    public void Two_axis_and_polar_handles_follow_the_pointer()
    {
        var callout = new ShapeObject { Id = "c", Shape = ShapeKind.Rectangle, Computed = new PresetGeometry("wedgeRectCallout") };
        var moved = _handles.Drag(callout, 0, new ShapePoint(0.1, 1.4), 300, 200);
        var tip = _handles.Handles(moved, 300, 200)[0].Position;
        Assert.Equal(0.1, tip.X, 2);
        Assert.Equal(1.4, tip.Y, 2);

        var pie = new ShapeObject { Id = "p", Shape = ShapeKind.Rectangle, Computed = new PresetGeometry("pie") };
        var turned = _handles.Drag(pie, 0, new ShapePoint(0.5, 0), 200, 200);
        var at = _handles.Handles(turned, 200, 200)[0].Position;
        Assert.Equal(0.5, at.X, 2);
        Assert.Equal(0, at.Y, 2);
    }

    [Fact]
    public void Odf_handles_bound_to_a_modifier_move_it_within_their_range()
    {
        var formula = new FormulaGeometry("M $0 0 L 21600 0 21600 21600 0 21600 Z N")
        {
            Modifiers = [5400],
            Handles = [new EnhancedHandle([new("handle-position", "$0 top"), new("handle-range-x-minimum", "0"), new("handle-range-x-maximum", "10800")])],
            ReferenceWidth = 100,
            ReferenceHeight = 100,
        };
        var shape = new ShapeObject { Id = "f", Shape = ShapeKind.Rectangle, Computed = formula };
        Assert.Equal(0.25, Assert.Single(_handles.Handles(shape, 100, 100)).Position.X, 6);
        Assert.Equal(8640, ((FormulaGeometry)_handles.Drag(shape, 0, new ShapePoint(0.4, 0), 100, 100).Computed!).Modifiers[0], 0);
        Assert.Equal(10800, ((FormulaGeometry)_handles.Drag(shape, 0, new ShapePoint(0.9, 0), 100, 100).Computed!).Modifiers[0], 0);
    }

    [Fact]
    public void Shapes_without_handles_offer_none()
    {
        Assert.Empty(_handles.Handles(new ShapeObject { Id = "r", Shape = ShapeKind.Rectangle }, 100, 100));
        Assert.Empty(_handles.Handles(new ShapeObject { Id = "a", Shape = ShapeKind.Arc }, 100, 100));
    }

    [Fact]
    public void A_preset_knows_when_its_adjustments_differ_from_the_defaults()
    {
        Assert.False(_engine.Adjusted(new PresetGeometry("roundRect") { Adjustments = [new GeometryAdjustment("adj", 16667)] }));
        Assert.True(_engine.Adjusted(new PresetGeometry("roundRect") { Adjustments = [new GeometryAdjustment("adj", 30000)] }));
    }
}
