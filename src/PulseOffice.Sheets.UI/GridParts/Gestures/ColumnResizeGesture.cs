using Avalonia;
using Avalonia.Input;
using PulseOffice.Foundation.Units;

namespace PulseOffice.Sheets.UI.GridParts.Gestures;

internal sealed class ColumnResizeGesture : GridGesture
{
    private readonly GridGeometry _geometry;
    private readonly IGridSurface _surface;
    private readonly GridNotifications _notifications;
    private readonly int _column;
    private readonly double _origin;
    private readonly Twip? _before;
    private readonly double _start;

    public ColumnResizeGesture(
        GridGeometry geometry, IGridSurface surface, GridNotifications notifications, int column, double origin)
    {
        _geometry = geometry;
        _surface = surface;
        _notifications = notifications;
        _column = column;
        _origin = origin;
        _before = geometry.Layout.ColumnWidths.TryGetValue(column, out var width) ? width : null;
        _start = geometry.ColumnWidth(column);
    }

    public override void Move(Point point, KeyModifiers modifiers)
    {
        _geometry.SetColumnWidth(_column, Math.Max(_start + point.X - _origin, 8));
        _surface.Invalidate();
    }

    public override void Release()
    {
        var after = _geometry.Layout.ColumnWidth(_column);

        if (_before is { } before) _geometry.Layout.SetColumnWidth(_column, before);
        else _geometry.Layout.ResetColumnWidth(_column);

        if (after != (_before ?? _geometry.Layout.DefaultColumnWidth))
            _notifications.Resize(new SheetGridView.ResizeRequest(true, _column, _before, after));

        _surface.Invalidate();
    }
}
