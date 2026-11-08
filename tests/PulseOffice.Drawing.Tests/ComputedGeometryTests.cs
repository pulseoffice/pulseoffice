using PulseOffice.Drawing.Shapes;
using PulseOffice.Drawing.Shapes.Geometry;
using Xunit;

namespace PulseOffice.Drawing.Tests;

public class ComputedGeometryTests
{
    private readonly PresetShapeCatalog _catalog = new();
    private readonly PresetGeometryEngine _engine = new();
    private readonly EnhancedPathInterpreter _interpreter = new();
    private readonly PresetFormulaTranslator _translator = new();
    private readonly EnhancedFormulaParser _parser = new();

    private IReadOnlyList<ShapePoint> Ends(ShapeOutline outline) =>
        [.. outline.Figures.SelectMany(figure => figure.Segments).Select(segment => segment switch
        {
            MoveSegment move => move.To,
            LineSegment line => line.To,
            CubicSegment cubic => cubic.To,
            _ => new ShapePoint(double.NaN, double.NaN),
        }).Where(point => !double.IsNaN(point.X))];

    [Fact]
    public void The_catalog_holds_every_preset_of_the_standard()
    {
        Assert.Equal(187, _catalog.Names.Count);
        Assert.NotNull(_catalog.Find("snip1Rect"));
        Assert.NotNull(_catalog.Find("round2DiagRect"));
        Assert.Null(_catalog.Find("notAShape"));
    }

    [Theory]
    [InlineData("*/ 10 6 4", 15)]
    [InlineData("+- 10 6 4", 12)]
    [InlineData("+/ 10 6 4", 4)]
    [InlineData("?: -1 6 4", 4)]
    [InlineData("pin 0 70000 50000", 50000)]
    [InlineData("mod 3 4 0", 5)]
    [InlineData("sin 10 5400000", 10)]
    [InlineData("at2 0 1", 5400000)]
    [InlineData("cat2 10 0 1", 0)]
    public void Drawing_guides_evaluate_like_the_standard(string formula, double expected)
    {
        var parts = formula.Split(' ');
        var scope = new DrawingGuideScope(100, 50, new DrawingBuiltIns(), new GuideOperators());
        Assert.Equal(expected, scope.Evaluate(new DrawingGuide("g", parts[0], parts[1..])), 6);
    }

    [Theory]
    [InlineData("ss", 50)]
    [InlineData("wd4", 25)]
    [InlineData("ssd8", 6.25)]
    [InlineData("3cd4", 16200000)]
    public void Built_in_guides_follow_the_shape_size(string name, double expected)
    {
        var scope = new DrawingGuideScope(100, 50, new DrawingBuiltIns(), new GuideOperators());
        Assert.Equal(expected, scope.Value(name), 6);
    }

    [Theory]
    [InlineData("1+2*3", 7)]
    [InlineData("-(2-5)*2", 6)]
    [InlineData("if(?a-1,10,20)", 10)]
    [InlineData("max($0,3)+min(1,2)", 8)]
    [InlineData("logwidth/2+abs(-1)", 51)]
    [InlineData("atan2(1,0)*2/pi", 1)]
    public void Odf_equations_evaluate(string formula, double expected)
    {
        var equations = new Dictionary<string, IEnhancedExpression>(StringComparer.Ordinal) { ["a"] = _parser.Parse("5") };
        var scope = new EnhancedScope(equations, [7], 0, 0, 21600, 21600, 100, 40);
        Assert.Equal(expected, _parser.Parse(formula).Evaluate(scope), 6);
    }

    [Fact]
    public void A_snipped_rectangle_cuts_its_top_right_corner()
    {
        var outline = _engine.Outline(new PresetGeometry("snip1Rect"), 200, 100);
        var points = Ends(outline);
        Assert.Equal(5, points.Count);
        Assert.Equal(new ShapePoint(0, 0), points[0]);
        Assert.Equal(1 - 100 * 0.16667 / 200, points[1].X, 4);
        Assert.Equal(0.16667, points[2].Y, 4);
        Assert.Equal(new ShapePoint(1, 1), points[3]);
        Assert.True(outline.Figures[0].IsClosed);
    }

    [Fact]
    public void Adjustments_move_the_geometry()
    {
        var outline = _engine.Outline(new PresetGeometry("snip1Rect") { Adjustments = [new GeometryAdjustment("adj", 50000)] }, 100, 100);
        Assert.Equal(0.5, Ends(outline)[1].X, 6);
    }

    [Fact]
    public void A_diagonally_rounded_rectangle_draws_two_arcs()
    {
        var outline = _engine.Outline(new PresetGeometry("round2DiagRect"), 100, 100);
        Assert.Contains(outline.Figures[0].Segments, segment => segment is CubicSegment);
    }

    [Fact]
    public void Every_preset_draws_something()
    {
        foreach (var name in _catalog.Names)
            Assert.True(_engine.Outline(new PresetGeometry(name), 300, 200).Figures.Count > 0, name);
    }

    [Fact]
    public void Every_preset_translated_to_odf_equations_draws_the_same_outline()
    {
        foreach (var name in _catalog.Names)
        {
            var preset = new PresetGeometry(name);
            var expected = Ends(_engine.Outline(preset, 3000, 2000));
            var translated = _translator.Translate(preset, 3000, 2000)!;
            var actual = Ends(_interpreter.Outline(translated, 3000, 2000));
            Assert.True(expected.Count == actual.Count, name);
            for (var index = 0; index < expected.Count; index++)
            {
                Assert.True(Math.Abs(expected[index].X - actual[index].X) < 1e-3, $"{name} x {index}");
                Assert.True(Math.Abs(expected[index].Y - actual[index].Y) < 1e-3, $"{name} y {index}");
            }
        }
    }

    [Fact]
    public void The_translation_keeps_type_and_modifiers()
    {
        var translated = _translator.Translate(new PresetGeometry("snip1Rect") { Adjustments = [new GeometryAdjustment("adj", 25000)] }, 100, 50)!;
        Assert.Equal("ooxml-snip1Rect", translated.Type);
        Assert.Equal([25000.0], translated.Modifiers);
        var back = _translator.Preset(translated.Type, translated.Modifiers, 100, 50)!;
        Assert.Equal("snip1Rect", back.Name);
        Assert.Equal(new GeometryAdjustment("adj", 25000), back.Adjustments.Single());
    }

    [Fact]
    public void An_odf_formula_path_with_modifiers_and_arcs_evaluates()
    {
        var geometry = new FormulaGeometry("M ?f0 0 L 21600 0 21600 21600 0 21600 Z N U 10800 10800 5400 5400 0 360 Z N")
        {
            Modifiers = [5400],
            Equations = [new EnhancedEquation("f0", "$0*2")],
        };
        var outline = _interpreter.Outline(geometry, 100, 100);
        Assert.Equal(2, outline.Figures.Count);
        Assert.Equal(0.5, ((MoveSegment)outline.Figures[0].Segments[0]).To.X, 6);
        var circle = Ends(new ShapeOutline([outline.Figures[1]]));
        Assert.All(circle, point => Assert.Equal(0.25, Math.Sqrt(Math.Pow(point.X - 0.5, 2) + Math.Pow(point.Y - 0.5, 2)), 4));
    }

    [Fact]
    public void Odf_no_fill_and_no_stroke_reach_the_figures()
    {
        var outline = _interpreter.Outline(new FormulaGeometry("F M 0 0 L 10 10 N S M 0 10 L 10 0 Z N") { ViewWidth = 10, ViewHeight = 10 }, 10, 10);
        Assert.False(outline.Figures[0].Filled);
        Assert.True(outline.Figures[0].Stroked);
        Assert.False(outline.Figures[1].Stroked);
    }

    [Fact]
    public void Shapes_with_a_computed_geometry_are_drawn_by_the_engine()
    {
        var shape = new ShapeObject { Id = "s", Shape = ShapeKind.Rectangle, Computed = new PresetGeometry("snip1Rect") { ReferenceWidth = 200, ReferenceHeight = 100 } };
        Assert.Equal(5, Ends(new ShapeGeometry().Outline(shape)).Count);
    }
}
