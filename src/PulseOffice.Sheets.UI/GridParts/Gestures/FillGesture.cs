using Avalonia;
using Avalonia.Input;
using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets.UI.GridParts.Gestures;

internal sealed class FillGesture(
    GridViewport viewport, IGridSurface surface, GridNotifications notifications, CellRange source) : GridGesture
{
    private readonly CellRange _source = source;

    public CellRange Preview { get; private set; } = source;

    public override void Move(Point point, KeyModifiers modifiers)
    {
        if (viewport.CellAt(point) is not { } over) return;

        Preview = PreviewFor(_source, over);
        surface.Invalidate();
    }

    public override void Release() => notifications.Fill(new SheetGridView.FillRequest(_source, Preview));

    private CellRange PreviewFor(CellRange origin, CellAddress over)
    {
        var below = over.Row - origin.End.Row;
        var above = origin.Start.Row - over.Row;
        var right = over.Column - origin.End.Column;
        var left = origin.Start.Column - over.Column;

        var vertical = Math.Max(below, above);
        var horizontal = Math.Max(right, left);

        if (vertical <= 0 && horizontal <= 0) return origin;

        if (vertical >= horizontal)
            return below >= above
                ? new CellRange(origin.Start, new CellAddress(over.Row, origin.End.Column))
                : new CellRange(new CellAddress(over.Row, origin.Start.Column), origin.End);

        return right >= left
            ? new CellRange(origin.Start, new CellAddress(origin.End.Row, over.Column))
            : new CellRange(new CellAddress(origin.Start.Row, over.Column), origin.End);
    }
}
