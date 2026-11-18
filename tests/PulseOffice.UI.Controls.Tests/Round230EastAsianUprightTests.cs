using Avalonia;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls.Shapes;
using PulseOffice.UI.Controls.Text.Rich;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class Round230EastAsianUprightTests
{
    private readonly EastAsianUprightText _eastAsian = new();
    private readonly UprightGlyphs _glyphs = new();
    private readonly TextFlowFrame _flow = new();

    [Fact]
    public void Only_East_Asian_clusters_stand_upright()
    {
        Assert.Equal(new[] { new TextCluster(2, 3), new TextCluster(3, 4) }, _eastAsian.Clusters("ab漢字c"));
        Assert.Equal(new[] { new TextCluster(1, 3) }, _eastAsian.Clusters("x\U0002000B"));
        Assert.Equal(new[] { new TextCluster(0, 1) }, _eastAsian.Clusters("한"));
        Assert.Empty(_eastAsian.Clusters("abc 123"));
        Assert.Empty(_eastAsian.Clusters("ᠮᠣᠩ"));
    }

    [Fact]
    public void An_upright_glyph_cancels_the_flow_turn_around_its_own_cell()
    {
        var cell = new Rect(20, 40, 10, 12);
        var stand = _glyphs.Stand(cell);

        Assert.Equal(cell.Center, stand.Transform(cell.Center));
        var right = stand.Transform(cell.Center + new Vector(5, 0));
        Assert.Equal(cell.Center.X, right.X, 6);
        Assert.Equal(cell.Center.Y - 5, right.Y, 6);

        var net = stand * _flow.Turn(new Rect(0, 0, 200, 100), 90);
        Assert.Equal(1, net.M11, 6);
        Assert.Equal(0, net.M12, 6);
        Assert.Equal(0, net.M21, 6);
        Assert.Equal(1, net.M22, 6);
    }

    [Fact]
    public void An_upright_glyph_is_centred_in_its_cell()
    {
        var origin = _glyphs.Origin(new Rect(0, 0, 20, 30), new Size(10, 14));

        Assert.Equal(new Point(5, 8), origin);
    }

    [Fact]
    public void East_Asian_Mongolian_and_stacked_shape_text_goes_through_the_composed_frame()
    {
        var text = new ShapeListText();
        ShapeObject With(TextLayout layout) => new()
        {
            Id = "s",
            Shape = ShapeKind.Rectangle,
            Text = "縦書き",
            Style = ShapeStyle.Default with { Text = ShapeStyle.Default.Text with { Layout = layout } },
        };

        Assert.True(text.Draws(With(new TextLayout { Flow = TextFlow.EastAsianVertical })));
        Assert.True(text.Draws(With(new TextLayout { Flow = TextFlow.MongolianVertical })));
        Assert.True(text.Draws(With(new TextLayout { Flow = TextFlow.Stacked })));
        Assert.True(text.Draws(With(new TextLayout { Flow = TextFlow.StackedRightToLeft })));
        Assert.True(text.Draws(With(new TextLayout { Flow = TextFlow.Vertical })));
        Assert.False(text.Draws(With(TextLayout.Default)));
    }

    [Fact]
    public void The_composed_shape_frame_carries_the_flow_and_the_shape_rotation()
    {
        var text = new ShapeListText();
        var shape = new ShapeObject
        {
            Id = "s",
            Shape = ShapeKind.Rectangle,
            Text = "縦",
            Style = ShapeStyle.Default with { Text = ShapeStyle.Default.Text with { Layout = new TextLayout { Flow = TextFlow.EastAsianVertical }, RotationDegrees = 10 } },
        };

        var framed = text.Framed(shape, 30);
        Assert.Equal(TextFlow.EastAsianVertical, framed.Frame.Layout.Flow);
        Assert.Equal(40, framed.Frame.RotationDegrees, 6);

        var upright = text.Framed(shape with { Style = shape.Style with { Text = shape.Style.Text with { Layout = new TextLayout { Flow = TextFlow.Vertical, Upright = true } } } }, 30);
        Assert.Equal(10, upright.Frame.RotationDegrees, 6);
    }
}
