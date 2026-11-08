using Avalonia;
using Avalonia.Input;
using PulseOffice.Foundation.Units;

namespace PulseOffice.Sheets.UI.GridParts.Gestures;

internal sealed class RowResizeGesture : GridGesture
{
    private readonly GridGeometry _geometry;
    private readonly IGridSurface _surface;
    private readonly GridNotifications _notifications;
    private readonly int _row;
    private readonly double _origin;
    private readonly Twip? _before;
    private readonly double _start;

    public RowResizeGesture(
        GridGeometry geometry, IGridSurface surface, GridNotifications notifications, int row, double origin)
    {
        _geometry = geometry;
        _surface = surface;
        _notifications = notifications;
        _row = row;
        _origin = origin;
        _before = geometry.Layout.RowHeights.TryGetValue(row, out var height) ? height : null;
        _start = geometry.RowHeight(row);
    }

    public override void Move(Point point, KeyModifiers modifiers)
    {
        _geometry.SetRowHeight(_row, Math.Max(_start + point.Y - _origin, 6));
        _surface.Invalidate();
    }

    public override void Release()
    {
        var after = _geometry.Layout.RowHeight(_row);

        if (_before is { } before) _geometry.Layout.SetRowHeight(_row, before);
        else _geometry.Layout.ResetRowHeight(_row);

        if (after != (_before ?? _geometry.Layout.DefaultRowHeight))
            _notifications.Resize(new SheetGridView.ResizeRequest(false, _row, _before, after));

        _surface.Invalidate();
    }
}
