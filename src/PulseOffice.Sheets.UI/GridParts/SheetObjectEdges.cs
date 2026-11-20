using PulseOffice.Drawing.Shapes;

namespace PulseOffice.Sheets.UI.GridParts;

internal sealed class SheetObjectEdges
{
    private const double SmallestSide = 1;

    public ObjectBox Clamp(ObjectBox box, double left, double top, bool resizing)
    {
        if (!resizing) return box with { X = Math.Max(box.X, left), Y = Math.Max(box.Y, top) };
        var right = box.X + box.Width;
        var bottom = box.Y + box.Height;
        var x = Math.Max(box.X, left);
        var y = Math.Max(box.Y, top);
        return new ObjectBox(x, y, Math.Max(right - x, SmallestSide), Math.Max(bottom - y, SmallestSide));
    }
}
