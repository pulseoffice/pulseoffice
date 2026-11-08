using Avalonia;
using Avalonia.Input;
using PulseOffice.UI.Controls.Shapes;

namespace PulseOffice.Sheets.UI.GridParts.Gestures;

internal sealed class ShapeAdjustGesture(
    GridViewport viewport,
    IGridSurface surface,
    GridNotifications notifications,
    IAdjustHandleChrome chrome,
    PlacedObject placed,
    AdjustHandleGrip grip) : GridGesture
{
    public PlacedObject Placed => placed;

    public AdjustHandleGrip Grip { get; private set; } = grip;

    public override bool EndsWithReferencePick => false;

    public override void Move(Point point, KeyModifiers modifiers)
    {
        Grip = chrome.Drag(Grip, new AdjustFrame(viewport.BoundsOf(placed), viewport.RotationOf(placed)), point);
        surface.Invalidate();
    }

    public override void Release()
    {
        if (Grip.Changed) notifications.Adjusted(new ShapeAdjustedEventArgs(placed, Grip.Preview));
        surface.Invalidate();
    }
}
