using PulseOffice.Document.Forms;
using Avalonia;
using Avalonia.Input;
using PulseOffice.UI.Controls.Forms;

namespace PulseOffice.Sheets.UI.GridParts.Gestures;

internal sealed class FormControlScrollGesture(
    GridViewport viewport,
    IGridSurface surface,
    GridNotifications notifications,
    IFormControlInteraction interaction,
    PlacedObject placed) : GridGesture
{
    public PlacedObject Placed => placed;

    public FormControlProperties? Preview { get; private set; }

    public override GestureStage Stage => GestureStage.BeforeDrawing;

    public override bool EndsWithReferencePick => false;

    public override void Move(Point point, KeyModifiers modifiers)
    {
        if (placed.Content is not FormControlObject form) return;

        var bounds = viewport.BoundsOf(placed);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        var horizontal = bounds.Width >= bounds.Height;

        var changed = interaction.Drag(
            form.ControlKind,
            Preview ?? form.Properties,
            (point.X - bounds.X) / bounds.Width,
            (point.Y - bounds.Y) / bounds.Height,
            horizontal);

        if (changed is null) return;

        Preview = changed;
        surface.Invalidate();
    }

    public override void Release()
    {
        if (Preview is { } changed) notifications.FormControl(new FormControlInteractionEventArgs(placed, changed));
        surface.Invalidate();
    }
}
