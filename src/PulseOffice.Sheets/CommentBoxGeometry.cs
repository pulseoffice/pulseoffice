using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets;

public readonly record struct NoteBounds(double X, double Y, double Width, double Height);

public readonly record struct NoteAnchor(int LeftColumn, int LeftOffset, int TopRow, int TopOffset, int RightColumn, int RightOffset, int BottomRow, int BottomOffset);

public interface ICommentBoxGeometry
{
    NoteBounds Bounds(SheetLayout layout, CellAddress address, CommentBox? box);

    CommentBox Relative(SheetLayout layout, CellAddress address, NoteBounds bounds);

    NoteAnchor Anchor(SheetLayout layout, NoteBounds bounds);

    NoteBounds FromAnchor(SheetLayout layout, NoteAnchor anchor);
}

public sealed class CommentBoxGeometry : ICommentBoxGeometry
{
    private const double PixelsPerPoint = 96.0 / 72.0;

    public NoteBounds Bounds(SheetLayout layout, CellAddress address, CommentBox? box)
    {
        var placed = box ?? CommentBox.Default;
        var right = ColumnStart(layout, address.Column + 1);
        var top = RowStart(layout, address.Row);
        return new NoteBounds(right + placed.OffsetX, Math.Max(top + placed.OffsetY, 0), placed.Width, placed.Height);
    }

    public CommentBox Relative(SheetLayout layout, CellAddress address, NoteBounds bounds) => new(
        bounds.X - ColumnStart(layout, address.Column + 1),
        bounds.Y - RowStart(layout, address.Row),
        Math.Max(bounds.Width, 1),
        Math.Max(bounds.Height, 1));

    public NoteAnchor Anchor(SheetLayout layout, NoteBounds bounds)
    {
        var (left, leftOffset) = ColumnAt(layout, bounds.X);
        var (top, topOffset) = RowAt(layout, bounds.Y);
        var (right, rightOffset) = ColumnAt(layout, bounds.X + bounds.Width);
        var (bottom, bottomOffset) = RowAt(layout, bounds.Y + bounds.Height);
        return new NoteAnchor(left, leftOffset, top, topOffset, right, rightOffset, bottom, bottomOffset);
    }

    public NoteBounds FromAnchor(SheetLayout layout, NoteAnchor anchor)
    {
        var x = ColumnStart(layout, anchor.LeftColumn) + anchor.LeftOffset / PixelsPerPoint;
        var y = RowStart(layout, anchor.TopRow) + anchor.TopOffset / PixelsPerPoint;
        var right = ColumnStart(layout, anchor.RightColumn) + anchor.RightOffset / PixelsPerPoint;
        var bottom = RowStart(layout, anchor.BottomRow) + anchor.BottomOffset / PixelsPerPoint;
        return new NoteBounds(x, y, Math.Max(right - x, 1), Math.Max(bottom - y, 1));
    }

    private double ColumnStart(SheetLayout layout, int column) =>
        layout.ColumnOffset(Math.Clamp(column, 0, CellAddress.MaxColumns)).ToPoints();

    private double RowStart(SheetLayout layout, int row) =>
        layout.RowOffset(Math.Clamp(row, 0, CellAddress.MaxRows)).ToPoints();

    private (int Index, int Pixels) ColumnAt(SheetLayout layout, double points) =>
        IndexAt(points, CellAddress.MaxColumns, index => ColumnStart(layout, index));

    private (int Index, int Pixels) RowAt(SheetLayout layout, double points) =>
        IndexAt(points, CellAddress.MaxRows, index => RowStart(layout, index));

    private (int Index, int Pixels) IndexAt(double points, int count, Func<int, double> start)
    {
        var low = 0;
        var high = count - 1;
        while (low < high)
        {
            var middle = low + ((high - low + 1) / 2);
            if (start(middle) <= points) low = middle;
            else high = middle - 1;
        }
        return (low, (int)Math.Round((points - start(low)) * PixelsPerPoint));
    }
}
