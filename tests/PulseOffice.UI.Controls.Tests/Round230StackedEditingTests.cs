using Avalonia;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls.Text.Editing;
using PulseOffice.UI.Controls.Text.Rich;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class Round230StackedEditingTests
{
    private readonly TextCaretGeometry _caret = new();
    private readonly CaretArrows _arrows = new();
    private readonly CaretRowNavigation _navigation = new();
    private readonly TextFlowFrame _flow = new();
    private readonly TextFrameArea _area = new(new Rect(0, 0, 100, 100), 1);

    private TypesetLine Empty(TextAlign align = TextAlign.Left) => new([], null, 0, 0, 0, 0, new TextParagraph(string.Empty) { Align = align });

    private int Index(TextPosition position) => position.Paragraph * 100 + position.Offset;

    [Fact]
    public void Arrow_keys_follow_the_turned_frame()
    {
        var turn = _flow.Turn(new Rect(0, 0, 100, 60), 90);

        Assert.Equal(CaretArrow.Right, _arrows.InFrame(turn, CaretArrow.Down));
        Assert.Equal(CaretArrow.Down, _arrows.InFrame(turn, CaretArrow.Left));
        Assert.Equal(CaretArrow.Up, _arrows.InFrame(turn, CaretArrow.Right));
        Assert.Equal(CaretArrow.Left, _arrows.InFrame(Matrix.Identity, CaretArrow.Left));
    }

    [Fact]
    public void Stacked_text_moves_along_the_stack_vertically_and_across_stacks_sideways()
    {
        Assert.Equal(CaretMove.Previous, _arrows.Move(CaretArrow.Up, stacked: true));
        Assert.Equal(CaretMove.Next, _arrows.Move(CaretArrow.Down, stacked: true));
        Assert.Equal(CaretMove.Up, _arrows.Move(CaretArrow.Left, stacked: true));
        Assert.Equal(CaretMove.Down, _arrows.Move(CaretArrow.Right, stacked: true));
        Assert.Equal(CaretMove.Next, _arrows.Move(CaretArrow.Right, stacked: false));
    }

    [Fact]
    public void Previous_and_next_step_through_a_row_and_over_its_edges()
    {
        CaretRow[] rows = [new([new(0, 0), new(1, 10)]), new([new(2, 0), new(3, 10)])];

        Assert.Equal(1, _navigation.Move(rows, 0, null, CaretMove.Next, 2).Index);
        Assert.Equal(2, _navigation.Move(rows, 1, null, CaretMove.Next, 2).Index);
        Assert.Equal(1, _navigation.Move(rows, 2, null, CaretMove.Previous, 2).Index);
        Assert.Equal(0, _navigation.Move(rows, 0, null, CaretMove.Previous, 2).Index);
    }

    [Fact]
    public void Stack_rows_run_from_the_leftmost_stack()
    {
        var frame = new ComposedTextFrame(_area, [Empty(), Empty()], [0, 0]) { Shifts = [50, 0], Stacked = true };

        var rows = _caret.Rows(frame, Index);

        Assert.Equal(2, rows.Count);
        Assert.Equal(100, rows[0].First);
        Assert.Equal(0, rows[1].First);
    }

    [Fact]
    public void A_stacked_caret_is_a_bar_across_the_stack()
    {
        var frame = new ComposedTextFrame(_area, [Empty()], [10]) { Shifts = [20], Stacked = true };

        var caret = _caret.Caret(frame, new TextPosition(0, 0));
        var bar = _caret.Bar(caret);

        Assert.Equal(0, caret.Height);
        Assert.True(caret.Width > 0);
        Assert.Equal(new Point(20, 10), caret.TopLeft);
        Assert.Equal(1.5, bar.Height, 6);
        Assert.Equal(caret.Width, bar.Width, 6);
        Assert.Equal(1.5, _caret.Bar(new Rect(5, 5, 0, 12)).Width, 6);
    }

    [Fact]
    public void Mirrored_lines_are_navigated_in_their_drawn_order()
    {
        var frame = new ComposedTextFrame(_area, [Empty(), Empty()], [50, 0]);

        var rows = _caret.Rows(frame, Index);

        Assert.Equal(100, rows[0].First);
        Assert.Equal(0, rows[1].First);
    }

    [Fact]
    public void Centred_stacks_share_the_free_width()
    {
        var frame = new TextStacks().Arrange(_area, [Empty(TextAlign.Center), Empty(TextAlign.Center)], TextAnchor.Top);

        Assert.True(frame.Stacked);
        Assert.Equal(10, frame.Shift(0), 6);
        Assert.Equal(90, frame.Shift(1), 6);
    }
}
