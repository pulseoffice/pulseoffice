using Avalonia;
using Avalonia.Input;

namespace PulseOffice.Sheets.UI.GridParts.Gestures;

internal sealed class SelectionGesture(GridViewport viewport, SheetSelection selection) : GridGesture
{
    public override GestureStage Stage => GestureStage.AfterCursor;

    public override void Move(Point point, KeyModifiers modifiers)
    {
        if (viewport.CellAt(point) is { } address) selection.ExtendTo(address);
    }
}
