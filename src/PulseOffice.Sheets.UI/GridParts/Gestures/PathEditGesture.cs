using Avalonia;
using Avalonia.Input;
using PulseOffice.Drawing.Freeform;
using PulseOffice.Drawing.Shapes;

namespace PulseOffice.Sheets.UI.GridParts.Gestures;

internal sealed class PathEditGesture(
    GridViewport viewport,
    IGridSurface surface,
    GridNotifications notifications,
    IPathEditing editing,
    PlacedObject placed,
    PathHandle handle) : GridGesture
{
    public PlacedObject Placed => placed;

    public FreeformPath? Preview { get; private set; }

    public override GestureStage Stage => GestureStage.BeforeDrawing;

    public override bool EndsWithReferencePick => false;

    public override void Move(Point point, KeyModifiers modifiers)
    {
        if (!handle.Exists || placed.Content is not FreeformObject freeform) return;

        var bounds = viewport.BoundsOf(placed);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        var local = new ShapePoint(
            (point.X - bounds.X) / bounds.Width, (point.Y - bounds.Y) / bounds.Height);

        Preview = editing.Move(freeform.Path, handle, local);
        surface.Invalidate();
    }

    public override void Release()
    {
        if (Preview is { } path) notifications.FreeformEdit(new FreeformEditedEventArgs(placed, path));
        surface.Invalidate();
    }
}
