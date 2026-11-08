using PulseOffice.Drawing.Scenes;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Drawing.Tests;

public sealed class DimensionLineGeometryTests
{
    private readonly DimensionLineGeometry _geometry = new();

    private DimensionLine Line => new() { Start = new ShapePoint(0, 1), End = new ShapePoint(1, 1), LineDistance = Twip.FromPoints(10), GuideOverhang = Twip.FromPoints(2), GuideDistance = Twip.Zero };

    [Fact]
    public void La_quota_sotto_la_misura_mette_la_linea_dall_altra_parte()
    {
        var above = _geometry.Layout(new ShapePoint(0, 0), new ShapePoint(100, 0), Line);
        var below = _geometry.Layout(new ShapePoint(0, 0), new ShapePoint(100, 0), Line with { Placing = DimensionPlacing.Below });

        Assert.Equal(-10, above.LineStart.Y, 6);
        Assert.Equal(10, below.LineStart.Y, 6);
        Assert.Equal(-12, above.FirstGuideTo.Y, 6);
        Assert.Equal(0, above.FirstGuideFrom.Y, 6);
    }

    [Fact]
    public void Le_guide_seguono_lunghezze_proprie_e_la_sporgenza_non_cambia_verso()
    {
        var line = Line with { StartGuide = Twip.FromPoints(4), EndGuide = Twip.FromPoints(-3), GuideDistance = Twip.FromPoints(1), LineDistance = Twip.FromPoints(-10) };
        var layout = _geometry.Layout(new ShapePoint(0, 0), new ShapePoint(100, 0), line);

        Assert.Equal(3, layout.FirstGuideFrom.Y, 6);
        Assert.Equal(-4, layout.SecondGuideFrom.Y, 6);
        Assert.Equal(10, layout.LineStart.Y, 6);
        Assert.Equal(8, layout.FirstGuideTo.Y, 6);
        Assert.Equal(8, layout.SecondGuideTo.Y, 6);
    }

    [Fact]
    public void L_etichetta_sta_dal_lato_esterno_e_l_angolo_resta_leggibile()
    {
        var centre = _geometry.LabelCentre(Line, 100, 12, 6);

        Assert.Equal(50, centre.X, 6);
        Assert.Equal(12 - 10 - 3, centre.Y, 6);
        Assert.Equal(0, _geometry.ReadableAngle(new ShapePoint(100, 0), new ShapePoint(0, 0)), 6);
        Assert.Equal(-45, _geometry.ReadableAngle(new ShapePoint(10, 10), new ShapePoint(0, 0)) - 90, 6);
        Assert.Equal(-60, _geometry.ReadableAngle(new ShapePoint(0, 0), new ShapePoint(10, -17.320508)), 4);
        Assert.Equal(60, _geometry.ReadableAngle(new ShapePoint(10, -17.320508), new ShapePoint(0, 0)) + 120, 4);
    }

    [Fact]
    public void La_cornice_contiene_linea_guide_e_punti_misurati()
    {
        var frame = _geometry.Frame(new ShapePoint(50, 50), new ShapePoint(50, 150), Line);

        Assert.Equal(100, frame.Height, 6);
        Assert.Equal(12, frame.Width, 6);
        Assert.Equal(-90, _geometry.Layout(Line with { Start = frame.Start, End = frame.End }, frame.Width, frame.Height).ReadableAngleDegrees, 6);
    }

    [Fact]
    public void La_geometria_della_forma_usa_la_quota_e_una_misura_degenere_non_rompe()
    {
        var shape = new ShapeObject { Id = "quota", Shape = ShapeKind.Line, Dimension = Line };
        var outline = new ShapeGeometry().Outline(shape, 100, 12);

        Assert.Equal(3, outline.Figures.Count);
        Assert.All(outline.Figures, figure => Assert.False(figure.Filled));
        var point = _geometry.Layout(new ShapePoint(5, 5), new ShapePoint(5, 5), Line);
        Assert.Equal(0, point.MeasuredLength);
        Assert.True(double.IsFinite(point.LineStart.X) && double.IsFinite(point.LineStart.Y));
        Assert.Equal("0.00", _geometry.Label(Line, double.NaN, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Due_scene_con_lo_stesso_contenuto_sono_uguali()
    {
        var first = new Scene3DObject { Id = "s", Markup = "<a/>", Styles = ["<b/>"] };
        var second = new Scene3DObject { Id = "s", Markup = "<a/>", Styles = ["<b/>"] };

        Assert.Equal(first, second);
        Assert.NotEqual(first, second with { Styles = ["<c/>"] });
    }

    [Fact]
    public void La_quota_inserita_dal_modello_sta_tutta_nella_sua_cornice_con_le_frecce()
    {
        var template = new DimensionLineTemplate();
        var shape = template.Create("q1");
        var width = template.Size.Width.ToPoints();
        var height = template.Size.Height.ToPoints();

        var layout = _geometry.Layout(shape.Dimension!, width, height);

        Assert.All(layout.Points, point =>
        {
            Assert.InRange(point.X, -1e-6, width + 1e-6);
            Assert.InRange(point.Y, -1e-6, height + 1e-6);
        });
        Assert.Equal(0, layout.FirstGuideTo.Y, 3);
        Assert.Equal(shape.Dimension!.GuideOverhang.ToPoints(), layout.LineStart.Y, 3);
        Assert.Equal(ArrowEnd.Arrow, shape.Style.Line.StartArrow);
        Assert.Equal(ArrowEnd.Arrow, shape.Style.Line.EndArrow);
        Assert.Equal("6.00 cm", _geometry.Label(shape.Dimension!, layout.MeasuredLength, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData(6)]
    [InlineData(24)]
    [InlineData(48)]
    public void Ridimensionando_in_altezza_la_quota_le_distanze_seguono_la_cornice(double height)
    {
        var layout = _geometry.Layout(Line, 100, height);
        var top = layout.Points.Min(point => point.Y);
        var bottom = layout.Points.Max(point => point.Y);

        Assert.Equal(0, top, 1);
        Assert.Equal(height, bottom, 1);
        Assert.Equal(height * 10 / 12, height - layout.LineStart.Y, 1);
    }

    [Fact]
    public void La_cornice_naturale_non_cambia_le_distanze_e_una_obliqua_resta_dentro()
    {
        Assert.Equal(Line, _geometry.Fitted(Line, 100, 12));
        var oblique = Line with { Start = new ShapePoint(0, 1), End = new ShapePoint(1, 0) };
        foreach (var (width, height) in new[] { (40.0, 40.0), (40.0, 10.0), (200.0, 30.0) })
        {
            var points = _geometry.Layout(oblique, width, height).Points;
            Assert.All(points, point => Assert.InRange(point.X, -0.1, width + 0.1));
            Assert.All(points, point => Assert.InRange(point.Y, -0.1, height + 0.1));
        }
    }
}
