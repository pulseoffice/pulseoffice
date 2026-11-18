using Xunit;

namespace PulseOffice.Sheets.Tests;

public sealed class Round228CommentBoxDragTests
{
    private readonly ICommentBoxDrag _drag = new CommentBoxDrag();
    private readonly CommentBox _start = new(10, -7, 100, 60);

    [Fact]
    public void Dragging_the_inside_moves_the_box()
    {
        Assert.Equal(new CommentBox(25, 3, 100, 60), _drag.Drag(_start, NoteBoxEdges.None, 15, 10));
    }

    [Fact]
    public void Dragging_the_far_corner_resizes_and_keeps_a_minimum()
    {
        Assert.Equal(new CommentBox(10, -7, 120, 80), _drag.Drag(_start, NoteBoxEdges.Right | NoteBoxEdges.Bottom, 20, 20));
        Assert.Equal(12, _drag.Drag(_start, NoteBoxEdges.Right, -500, 0).Width);
    }

    [Fact]
    public void Dragging_the_near_edge_moves_the_start_and_shrinks()
    {
        var box = _drag.Drag(_start, NoteBoxEdges.Left, 30, 0);

        Assert.Equal(40, box.OffsetX);
        Assert.Equal(70, box.Width);
    }

    [Fact]
    public void Edges_are_found_within_the_grip()
    {
        Assert.Equal(NoteBoxEdges.Right | NoteBoxEdges.Bottom, _drag.EdgesAt(100, 60, 98, 58, 5));
        Assert.Equal(NoteBoxEdges.None, _drag.EdgesAt(100, 60, 50, 30, 5));
    }
}
