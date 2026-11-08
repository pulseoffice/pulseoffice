using PulseOffice.Drawing.Shapes;
using Avalonia;
using Avalonia.Input;
using PulseOffice.UI.Controls.Shapes;

namespace PulseOffice.Sheets.UI.GridParts.Gestures;

internal sealed class ObjectDragGesture(
    GridViewport viewport,
    IGridSurface surface,
    GridNotifications notifications,
    GridObjectSelection objects,
    IObjectManipulator manipulator,
    IObjectSnapping snapping,
    ObjectHandle handle,
    Point origin,
    ObjectBox start,
    double rotation) : GridGesture
{
    private readonly ObjectTransformSession _session = manipulator.Begin(handle, start, rotation);

    private const double CellSnapTolerance = 8;

    private bool _released;

    public ObjectBox? Preview => _released ? null : _session.Preview;

    public double? RotationPreview => _released ? null : _session.RotationPreview;

    public override bool EndsWithReferencePick => false;

    public override void Move(Point point, KeyModifiers modifiers)
    {
        _session.Track(point.X - origin.X, point.Y - origin.Y, point.X, point.Y, modifiers.HasFlag(KeyModifiers.Shift));
        if (modifiers.HasFlag(KeyModifiers.Alt)) _session.Settle(snapping.Snap(_session, viewport.CellEdges(), CellSnapTolerance));
        surface.Invalidate();
    }

    public override void Release()
    {
        if (objects.Last is { } placed) Commit(placed);

        _released = true;

        surface.Invalidate();
    }

    private void Commit(PlacedObject placed)
    {
        var box = _session.Preview;
        var turned = _session.Rotated(0.001) ? _session.RotationPreview : null;
        var moved = _session.Moved(0.001);

        if (!moved && turned is null) return;

        var changes = new List<SheetGridView.ObjectChange> { viewport.ChangeOf(placed, box, turned) };

        if (_session.Moving)
        {
            var deltaX = box.X - start.X;
            var deltaY = box.Y - start.Y;

            foreach (var other in objects.Items)
            {
                if (ReferenceEquals(other, placed)) continue;

                var bounds = viewport.BoundsOf(other);

                changes.Add(viewport.ChangeOf(
                    other,
                    new ObjectBox(bounds.X + deltaX, bounds.Y + deltaY, bounds.Width, bounds.Height),
                    null));
            }
        }

        notifications.ObjectChanges(changes);
    }
}
