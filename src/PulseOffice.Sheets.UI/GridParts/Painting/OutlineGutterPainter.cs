using Avalonia;
using Avalonia.Media;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.UI.Outline;

namespace PulseOffice.Sheets.UI.GridParts.Painting;

internal sealed class OutlineGutterPainter(GridViewport viewport)
{
    private const double Box = 10;

    public void Paint(DrawingContext context, IBrush background, IBrush foreground, IBrush lineBrush)
    {
        if (viewport.Sheet is not { } sheet) return;
        var geometry = viewport.Geometry;
        var surface = viewport.Surface;
        var pen = new Pen(lineBrush);
        var mark = new Pen(foreground, 1.2);
        var gutter = viewport.Gutter;

        if (geometry.RowGutterWidth > 0)
        {
            using (context.PushClip(new Rect(0, geometry.ColumnHeaderHeight, geometry.RowGutterWidth, Math.Max(surface.Height - geometry.ColumnHeaderHeight, 0))))
                foreach (var toggle in gutter.Toggles(sheet, StructureAxis.Rows))
                {
                    var across = gutter.SlotOffset(toggle.Level) + gutter.SlotSize / 2;
                    if (!toggle.Collapsed && RowSpan(geometry, toggle) is var (top, bottom) && bottom > top)
                        context.DrawLine(pen, new Point(across, top), new Point(across, bottom));
                    if (geometry.RowHeight(toggle.Index) <= 0) continue;
                    var along = RowStart(geometry, toggle.Index) + geometry.RowHeight(toggle.Index) / 2;
                    DrawToggle(context, background, pen, mark, new Point(across, along), toggle.Collapsed);
                }

            foreach (var level in gutter.Levels(sheet, StructureAxis.Rows))
                DrawLevel(context, background, pen, foreground, new Point(
                    gutter.SlotOffset(level.Level) + gutter.SlotSize / 2,
                    geometry.ColumnGutterHeight + (geometry.ColumnHeaderHeight - geometry.ColumnGutterHeight) / 2), level.Level);
        }

        if (geometry.ColumnGutterHeight > 0)
        {
            using (context.PushClip(new Rect(geometry.RowHeaderWidth, 0, Math.Max(surface.Width - geometry.RowHeaderWidth, 0), geometry.ColumnGutterHeight)))
                foreach (var toggle in gutter.Toggles(sheet, StructureAxis.Columns))
                {
                    var across = gutter.SlotOffset(toggle.Level) + gutter.SlotSize / 2;
                    if (!toggle.Collapsed && ColumnSpan(geometry, toggle) is var (left, right) && right > left)
                        context.DrawLine(pen, new Point(left, across), new Point(right, across));
                    if (geometry.ColumnWidth(toggle.Index) <= 0) continue;
                    var along = ColumnStart(geometry, toggle.Index) + geometry.ColumnWidth(toggle.Index) / 2;
                    DrawToggle(context, background, pen, mark, new Point(along, across), toggle.Collapsed);
                }

            foreach (var level in gutter.Levels(sheet, StructureAxis.Columns))
                DrawLevel(context, background, pen, foreground, new Point(
                    geometry.RowGutterWidth + (geometry.RowHeaderWidth - geometry.RowGutterWidth) / 2,
                    gutter.SlotOffset(level.Level) + gutter.SlotSize / 2), level.Level);
        }
    }

    private double RowStart(GridGeometry geometry, int row) =>
        geometry.ColumnHeaderHeight + geometry.RowOffset(row) - (row < geometry.FrozenRows ? viewport.PaneScroll.Y : viewport.Scroll.Y);

    private double ColumnStart(GridGeometry geometry, int column) =>
        geometry.RowHeaderWidth + geometry.ColumnOffset(column) - (column < geometry.FrozenColumns ? viewport.PaneScroll.X : viewport.Scroll.X);

    private (double Start, double End) RowSpan(GridGeometry geometry, OutlineButton toggle) =>
        (RowStart(geometry, Math.Min(toggle.SpanFirst, toggle.Index + 1)),
         RowStart(geometry, Math.Max(toggle.SpanLast + 1, toggle.Index)));

    private (double Start, double End) ColumnSpan(GridGeometry geometry, OutlineButton toggle) =>
        (ColumnStart(geometry, Math.Min(toggle.SpanFirst, toggle.Index + 1)),
         ColumnStart(geometry, Math.Max(toggle.SpanLast + 1, toggle.Index)));

    private void DrawToggle(DrawingContext context, IBrush background, Pen frame, Pen mark, Point centre, bool collapsed)
    {
        var box = new Rect(centre.X - Box / 2, centre.Y - Box / 2, Box, Box);
        context.DrawRectangle(background, frame, box, 2, 2);
        context.DrawLine(mark, new Point(box.Left + 2.5, centre.Y), new Point(box.Right - 2.5, centre.Y));
        if (collapsed) context.DrawLine(mark, new Point(centre.X, box.Top + 2.5), new Point(centre.X, box.Bottom - 2.5));
    }

    private void DrawLevel(DrawingContext context, IBrush background, Pen frame, IBrush foreground, Point centre, int level)
    {
        var box = new Rect(centre.X - Box / 2 - 1, centre.Y - Box / 2 - 1, Box + 2, Box + 2);
        context.DrawRectangle(background, frame, box, 2, 2);
        var text = new FormattedText(
            level.ToString(System.Globalization.CultureInfo.CurrentCulture), System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 9, foreground);
        context.DrawText(text, new Point(centre.X - text.Width / 2, centre.Y - text.Height / 2));
    }
}
