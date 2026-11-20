using PulseOffice.Drawing.Shapes;
using Xunit;

namespace PulseOffice.Drawing.Tests;

public sealed class Round292RoundedCornerTests
{
    private static readonly ShapeObject Rounded = new() { Id = "r", Shape = ShapeKind.RoundedRectangle, CornerRatio = 0.2 };

    [Fact]
    public void A_model_rounded_rectangle_draws_circular_corners_on_the_short_side()
    {
        var outline = new ShapeGeometry().Outline(Rounded, 200, 100);

        var arc = outline.Figures[0].Segments.OfType<ArcSegment>().First();
        var start = Assert.IsType<MoveSegment>(outline.Figures[0].Segments[0]);
        Assert.Equal(20, arc.RadiusX * 200, 6);
        Assert.Equal(20, arc.RadiusY * 100, 6);
        Assert.Equal(0.1, start.To.X, 6);
    }

    [Fact]
    public void Its_text_area_is_inset_alike_on_every_side()
    {
        var margins = new ShapeTextArea().Margins(Rounded, 200, 100);

        Assert.Equal(margins.Top, margins.Left, 6);
        Assert.Equal(0.2 * 0.2928932 * 100, margins.Left, 5);
    }
}
