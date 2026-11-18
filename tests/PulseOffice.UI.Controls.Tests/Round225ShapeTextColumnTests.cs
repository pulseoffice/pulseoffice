using Avalonia;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls.Shapes;
using PulseOffice.UI.Controls.Text.Rich;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class Round225ShapeTextColumnTests
{
    private readonly ShapeTextColumns _columns = new();

    [Fact]
    public void Columns_break_at_the_composed_lines_of_mixed_sizes()
    {
        ShapeTextLine[] lines = [new(0, 12), new(12, 52), new(52, 64), new(64, 76)];

        var placed = _columns.Place(new Rect(0, 0, 210, 60), 2, 10, lines);

        Assert.Equal(2, placed.Count);
        Assert.Equal(52, placed[0].Clip.Height);
        Assert.Equal(new Point(110, -52), placed[1].Origin);
    }

    [Fact]
    public void A_line_taller_than_the_column_still_gets_a_column_of_its_own()
    {
        ShapeTextLine[] lines = [new(0, 80), new(80, 90)];

        var placed = _columns.Place(new Rect(0, 0, 210, 60), 3, 10, lines);

        Assert.Equal(2, placed.Count);
        Assert.Equal(80, placed[0].Clip.Height);
        Assert.Equal(-80, placed[1].Origin.Y);
    }

    [Fact]
    public void Right_to_left_stacks_start_from_the_right_edge()
    {
        var area = new TextFrameArea(new Rect(0, 0, 100, 100), 1);
        TypesetLine Empty(int offset) => new([], null, 0, 0, 0, offset, new TextParagraph(string.Empty));

        var frame = new TextStacks().Arrange(area, [Empty(0), Empty(0)], TextAnchor.Top, rightToLeft: true);

        Assert.True(frame.Shift(0) > frame.Shift(1));
        Assert.Equal(area.Inner.Width, frame.Shift(0), 3);
    }

    [Fact]
    public void Only_Mongolian_vertical_text_orders_lines_left_to_right()
    {
        Assert.True(new TextLayout { Flow = TextFlow.MongolianVertical }.LinesLeftToRight);
        Assert.False(new TextLayout { Flow = TextFlow.EastAsianVertical }.LinesLeftToRight);
        Assert.Equal(90, new TextLayout { Flow = TextFlow.EastAsianVertical }.FlowDegrees);
        Assert.True(new TextLayout { Flow = TextFlow.StackedRightToLeft }.IsStacked);
    }
}
