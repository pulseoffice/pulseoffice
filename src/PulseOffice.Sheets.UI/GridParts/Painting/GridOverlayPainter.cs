using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.UI.GridParts.Gestures;
using PulseOffice.UI.Theming;

namespace PulseOffice.Sheets.UI.GridParts.Painting;

internal sealed class GridOverlayPainter(
    GridViewport viewport, SheetSelection selection, GridDisplayOptions options, IGridTheme theme)
{
    private static readonly IPen PrecedentPen = new Pen(new SolidColorBrush(Color.FromRgb(37, 99, 235)), 1.4);
    private static readonly IPen DependentPen = new Pen(new SolidColorBrush(Color.FromRgb(22, 163, 74)), 1.4);
    private static readonly IPen ErrorPen = new Pen(new SolidColorBrush(Color.FromRgb(220, 38, 38)), 1.4);

    public Rect ChoiceButtonBounds()
    {
        var cell = viewport.CellRect(viewport.AnchorOf(selection.Active));
        var size = Math.Min(Math.Max(cell.Height, 12), 22);
        return new Rect(cell.Right + 1, cell.Y + (cell.Height - size) / 2, size, size);
    }

    public Rect FillHandleRect()
    {
        var corner = viewport.CellRect(viewport.Sheet is { } sheet ? sheet.Merges.Expand(selection.Range).End : selection.Range.End);
        const double size = 6;

        return new Rect(corner.Right - size / 2, corner.Bottom - size / 2, size, size);
    }

    public void PaintChoiceButton(DrawingContext context)
    {
        if (viewport.Sheet is null || !selection.IsSingleCell || !options.ChoicesAvailable(viewport.AnchorOf(selection.Active))) return;

        var box = ChoiceButtonBounds();
        if (!box.Intersects(viewport.GridArea())) return;
        context.FillRectangle(theme.Brush(Palette.Keys.SurfaceRaised, Brushes.White), box, 2);
        context.DrawRectangle(new Pen(theme.Brush(Palette.Keys.Border, Brushes.LightGray)), box, 2);

        var arrow = new StreamGeometry();
        using (var draw = arrow.Open())
        {
            draw.BeginFigure(new Point(box.X + box.Width * 0.3, box.Y + box.Height * 0.42), true);
            draw.LineTo(new Point(box.X + box.Width * 0.7, box.Y + box.Height * 0.42));
            draw.LineTo(new Point(box.Center.X, box.Y + box.Height * 0.66));
            draw.EndFigure(true);
        }
        context.DrawGeometry(theme.Brush(Palette.Keys.TextPrimary, Brushes.Black), null, arrow);
    }

    public void PaintSelection(DrawingContext context, VisibleRange visible, IBrush accent, GridGesture? gesture)
    {
        var range = selection.Range;

        var fill = new SolidColorBrush(
            (accent as SolidColorBrush)?.Color ?? Colors.SteelBlue, 0.12);

        if (!range.IsSingleCell)
        {
            using (context.PushClip(viewport.GridArea()))
                context.FillRectangle(fill, viewport.SpanRect(range));
        }

        if (!visible.Contains(selection.Active)) return;

        using (context.PushClip(viewport.GridArea()))
        {
            context.DrawRectangle(new Pen(accent, 2), viewport.MergedRect(selection.Active));

            if ((gesture as FillGesture)?.Preview is { } preview)
                context.DrawRectangle(
                    new Pen(accent, 1) { DashStyle = DashStyle.Dash },
                    viewport.SpanRect(preview));

            if (options.PickingReference)
                context.DrawRectangle(
                    new Pen(accent, 2) { DashStyle = DashStyle.Dash },
                    viewport.SpanRect(selection.Range));
            else
                context.FillRectangle(accent, FillHandleRect());
        }
    }

    public void PaintViewAids(DrawingContext context, VisibleRange visible, IBrush lineBrush, IBrush foreground)
    {
        if (viewport.Sheet is not { } sheet) return;

        if (options.HighlightRowAndColumn) PaintCross(context, CrossTint(foreground));
        if (options.ShowPageBreaks) PaintPageBreaks(context, sheet, visible);
        if (options.ShowHiddenMarkers) PaintHiddenMarkers(context, sheet, visible, lineBrush);
    }

    public IBrush CrossTint(IBrush foreground) =>
        new ImmutableSolidColorBrush((foreground as ISolidColorBrush)?.Color ?? Colors.Black, 0.06);

    private void PaintCross(DrawingContext context, IBrush tint)
    {
        var area = viewport.GridArea();
        var active = selection.Active;

        var row = viewport.CellRect(new CellAddress(active.Row, 0));
        var column = viewport.CellRect(new CellAddress(0, active.Column));

        context.FillRectangle(tint, new Rect(area.X, row.Y, area.Width, row.Height));
        context.FillRectangle(tint, new Rect(column.X, area.Y, column.Width, area.Height));
    }

    private void PaintPageBreaks(DrawingContext context, Worksheet sheet, VisibleRange visible)
    {
        var pen = new Pen(Brushes.MediumBlue, 1.5) { DashStyle = DashStyle.Dash };
        var area = viewport.GridArea();

        foreach (var row in sheet.RowBreaks)
        {
            if (row < visible.FirstRow || row > visible.LastRow + 1) continue;

            var y = viewport.CellRect(new CellAddress(row, 0)).Y;
            context.DrawLine(pen, new Point(area.X, y), new Point(area.Right, y));
        }

        foreach (var column in sheet.ColumnBreaks)
        {
            if (column < visible.FirstColumn || column > visible.LastColumn + 1) continue;

            var x = viewport.CellRect(new CellAddress(0, column)).X;
            context.DrawLine(pen, new Point(x, area.Y), new Point(x, area.Bottom));
        }
    }

    private void PaintHiddenMarkers(DrawingContext context, Worksheet sheet, VisibleRange visible, IBrush lineBrush)
    {
        var pen = new Pen(lineBrush, 2.5);
        var area = viewport.GridArea();

        for (var row = visible.FirstRow; row <= visible.LastRow; row++)
        {
            if (!sheet.Layout.IsRowHidden(row + 1)) continue;

            var y = viewport.CellRect(new CellAddress(row + 1, 0)).Y;
            context.DrawLine(pen, new Point(area.X, y), new Point(area.X + 14, y));
        }

        for (var column = visible.FirstColumn; column <= visible.LastColumn; column++)
        {
            if (!sheet.Layout.IsColumnHidden(column + 1)) continue;

            var x = viewport.CellRect(new CellAddress(0, column + 1)).X;
            context.DrawLine(pen, new Point(x, area.Y), new Point(x, area.Y + 14));
        }
    }

    public void PaintTraceArrows(DrawingContext context)
    {
        if (viewport.Sheet is not { } sheet || sheet.TraceArrows.Count == 0) return;

        foreach (var arrow in sheet.TraceArrows)
        {
            var from = viewport.CellRect(arrow.From);
            var to = viewport.CellRect(arrow.To);

            var start = new Point(from.X + from.Width / 2, from.Y + from.Height / 2);
            var end = new Point(to.X + to.Width / 2, to.Y + to.Height / 2);

            var pen = arrow.Kind switch
            {
                TraceKind.Dependent => DependentPen,
                TraceKind.Error => ErrorPen,
                _ => PrecedentPen,
            };

            context.DrawLine(pen, start, end);
            PaintArrowHead(context, pen, start, end);

            context.DrawEllipse(pen.Brush, null, start, 3, 3);
        }
    }

    private void PaintArrowHead(DrawingContext context, IPen pen, Point start, Point end)
    {
        var direction = end - start;
        var length = Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y);

        if (length < 1) return;

        var unit = new Point(direction.X / length, direction.Y / length);
        var normal = new Point(-unit.Y, unit.X);

        const double size = 7;

        var tip = new Point(end.X - unit.X * 2, end.Y - unit.Y * 2);

        var left = new Point(
            tip.X - unit.X * size + normal.X * size / 2.5,
            tip.Y - unit.Y * size + normal.Y * size / 2.5);

        var right = new Point(
            tip.X - unit.X * size - normal.X * size / 2.5,
            tip.Y - unit.Y * size - normal.Y * size / 2.5);

        context.DrawLine(pen, tip, left);
        context.DrawLine(pen, tip, right);
    }
}
