using PulseOffice.Drawing.Shapes;
using PulseOffice.Document.Forms;
using Avalonia;
using PulseOffice.Drawing.Freeform;
using PulseOffice.Sheets.Addressing;
using PulseOffice.UI.Controls.Forms;
using PulseOffice.UI.Controls.Shapes;

namespace PulseOffice.Sheets.UI.GridParts.Gestures;

internal sealed class GridGestureFactory(
    GridViewport viewport,
    SheetSelection selection,
    IGridSurface surface,
    GridNotifications notifications,
    GridObjectSelection objects,
    IObjectManipulator manipulator,
    IAdjustHandleChrome adjusting,
    IPathEditing pathEditing,
    IFormControlInteraction formInteraction,
    IObjectSnapping snapping)
{
    public GridGesture Selection() => new SelectionGesture(viewport, selection);

    public GridGesture ColumnResize(int column, double origin) =>
        new ColumnResizeGesture(viewport.Geometry, surface, notifications, column, origin);

    public GridGesture RowResize(int row, double origin) =>
        new RowResizeGesture(viewport.Geometry, surface, notifications, row, origin);

    public GridGesture Fill(CellRange source) => new FillGesture(viewport, surface, notifications, source);

    public GridGesture ObjectDrag(ObjectHandle handle, Point origin, ObjectBox start, double rotation) =>
        new ObjectDragGesture(viewport, surface, notifications, objects, manipulator, snapping, handle, origin, start, rotation);

    public GridGesture ShapeAdjust(PlacedObject placed, AdjustHandleGrip grip) =>
        new ShapeAdjustGesture(viewport, surface, notifications, adjusting, placed, grip);

    public GridGesture PathEdit(PlacedObject placed, PathHandle handle) =>
        new PathEditGesture(viewport, surface, notifications, pathEditing, placed, handle);

    public GridGesture FormControlScroll(PlacedObject placed) =>
        new FormControlScrollGesture(viewport, surface, notifications, formInteraction, placed);
}
