namespace PulseOffice.Sheets;

[Flags]
public enum NoteBoxEdges
{
    None = 0,
    Left = 1,
    Top = 2,
    Right = 4,
    Bottom = 8,
}

public interface ICommentBoxDrag
{
    NoteBoxEdges EdgesAt(double width, double height, double x, double y, double grip);

    CommentBox Drag(CommentBox start, NoteBoxEdges edges, double deltaX, double deltaY);
}

public sealed class CommentBoxDrag : ICommentBoxDrag
{
    private const double MinimumSize = 12;

    public NoteBoxEdges EdgesAt(double width, double height, double x, double y, double grip)
    {
        var edges = NoteBoxEdges.None;
        if (x <= grip) edges |= NoteBoxEdges.Left;
        else if (x >= width - grip) edges |= NoteBoxEdges.Right;
        if (y <= grip) edges |= NoteBoxEdges.Top;
        else if (y >= height - grip) edges |= NoteBoxEdges.Bottom;
        return edges;
    }

    public CommentBox Drag(CommentBox start, NoteBoxEdges edges, double deltaX, double deltaY)
    {
        ArgumentNullException.ThrowIfNull(start);
        if (edges == NoteBoxEdges.None) return start with { OffsetX = start.OffsetX + deltaX, OffsetY = start.OffsetY + deltaY };

        var (left, width) = Axis(start.OffsetX, start.Width, deltaX, edges.HasFlag(NoteBoxEdges.Left), edges.HasFlag(NoteBoxEdges.Right));
        var (top, height) = Axis(start.OffsetY, start.Height, deltaY, edges.HasFlag(NoteBoxEdges.Top), edges.HasFlag(NoteBoxEdges.Bottom));
        return new CommentBox(left, top, width, height);
    }

    private (double Start, double Size) Axis(double start, double size, double delta, bool near, bool far)
    {
        if (near)
        {
            var moved = Math.Min(delta, size - MinimumSize);
            return (start + moved, size - moved);
        }

        return far ? (start, Math.Max(size + delta, MinimumSize)) : (start, size);
    }
}
