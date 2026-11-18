using Avalonia;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls.Shapes;
using PulseOffice.UI.Controls.Text.Rich;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class Round218ShapeTextDrawingTests
{
    private readonly TextFlowFrame _flow = new();
    private readonly ShapeTextColumns _columns = new();
    private readonly TextCaretGeometry _caret = new();

    private TypesetLine Empty(int offset) => new([], null, 0, 0, 0, offset, new TextParagraph(string.Empty));

    [Fact]
    public void Upright_text_ignores_the_container_and_flow_rotation_and_stacks()
    {
        var layout = new TextLayout { Flow = TextFlow.Vertical, Upright = true };
        Assert.Equal(15, _flow.Degrees(layout, 15, 30));
        Assert.True(_flow.Stacks(layout));
        Assert.Equal(new Rect(0, 0, 40, 20), _flow.Box(new Rect(0, 0, 40, 20), layout));
    }

    [Fact]
    public void Turning_text_follows_the_container_and_the_flow()
    {
        var layout = new TextLayout { Flow = TextFlow.Vertical };
        Assert.Equal(135, _flow.Degrees(layout, 15, 30));
        Assert.False(_flow.Stacks(layout));
        Assert.True(_flow.Stacks(new TextLayout { Flow = TextFlow.Stacked }));
        Assert.Equal(Matrix.Identity, _flow.Turn(new Rect(0, 0, 10, 10), 360));
    }

    [Fact]
    public void Shape_text_columns_split_the_text_at_whole_lines()
    {
        var inner = new Rect(0, 0, 210, 100);
        Assert.Equal(100, _columns.Width(inner, 2, 10));

        var placed = _columns.Place(inner, 2, 10, 30, 150);

        Assert.Equal(2, placed.Count);
        Assert.Equal(new Rect(0, 0, 100, 90), placed[0].Clip);
        Assert.Equal(new Point(0, 0), placed[0].Origin);
        Assert.Equal(110, placed[1].Clip.X);
        Assert.Equal(new Point(110, -90), placed[1].Origin);
    }

    [Fact]
    public void Short_shape_text_uses_one_column()
    {
        Assert.Single(_columns.Place(new Rect(0, 0, 210, 100), 3, 10, 20, 40));
    }

    [Fact]
    public void Hit_testing_picks_the_line_of_the_column_under_the_point()
    {
        var area = new TextFrameArea(new Rect(0, 0, 100, 100), 1);
        var frame = new ComposedTextFrame(area, [Empty(0), Empty(0), Empty(0)], [0, 50, 0]) { Shifts = [0, 0, 110] };

        Assert.Equal(1, _caret.Hit(frame, new Point(20, 60)).Paragraph);
        Assert.Equal(2, _caret.Hit(frame, new Point(130, 60)).Paragraph);
        Assert.Equal(0, _caret.Hit(frame, new Point(20, 10)).Paragraph);
    }

    [Fact]
    public void Hit_testing_maps_the_point_back_through_the_rotation()
    {
        var area = new TextFrameArea(new Rect(0, 0, 100, 100), 1);
        var turn = _flow.Turn(new Rect(0, 0, 100, 100), 90);
        var frame = new ComposedTextFrame(area, [Empty(0), Empty(0)], [0, 60]) { Placement = turn };

        var onScreen = turn.Transform(new Point(20, 70));

        Assert.Equal(1, _caret.Hit(frame, onScreen).Paragraph);
        Assert.Equal(0, _caret.Hit(frame, turn.Transform(new Point(20, 10))).Paragraph);
    }
}
