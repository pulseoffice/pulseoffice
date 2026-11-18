using Avalonia;
using Avalonia.Input;
using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets.UI.GridParts.Gestures;

internal sealed class NoteBoxGesture(
    GridDisplayOptions options,
    IGridSurface surface,
    GridNotifications notifications,
    ICommentBoxDrag drag,
    CellAddress address,
    CommentBox? stored,
    CommentBox start,
    NoteBoxEdges edges,
    Point origin) : GridGesture
{
    public override GestureStage Stage => GestureStage.BeforeDrawing;

    public override bool EndsWithReferencePick => false;

    public override void Move(Point point, KeyModifiers modifiers)
    {
        options.NotePreview = new NotePreview(address, drag.Drag(start, edges, Points(point.X - origin.X), Points(point.Y - origin.Y)));
        surface.Invalidate();
    }

    public override void Release()
    {
        var after = options.NotePreview is { } preview && preview.Address == address ? preview.Box : start;
        options.NotePreview = null;
        if (after != start) notifications.NoteBox(new SheetGridView.NoteBoxChange(address, stored, after));
        surface.Invalidate();
    }

    private double Points(double pixels) => GridGeometry.ToTwips(pixels).ToPoints();
}
