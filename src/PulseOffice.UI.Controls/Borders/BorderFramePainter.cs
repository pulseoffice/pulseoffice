using Avalonia;
using Avalonia.Media;
using PulseOffice.Document.Borders;

namespace PulseOffice.UI.Controls.Borders;

public sealed record BorderPaintColors(IBrush Guide, IBrush Sample, IBrush Focus);

public interface IBorderFramePainter
{
    void Paint(DrawingContext context, Rect area, BorderFrameScope scope, BorderFrame frame, BorderPaintColors colors, BorderPosition? focus = null, bool showSample = true);

    void PaintStroke(DrawingContext context, Point start, Point end, BorderStroke stroke, double scale = 1);
}

public sealed class BorderFramePainter(IBorderSchemaGeometry geometry) : IBorderFramePainter
{
    private const double PixelsPerPoint = 1.33;
    private const double MaximumWidth = 6;
    private const double CornerTick = 6;

    public void Paint(DrawingContext context, Rect area, BorderFrameScope scope, BorderFrame frame, BorderPaintColors colors, BorderPosition? focus = null, bool showSample = true)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(colors);
        var guide = new Pen(colors.Guide, 1) { DashStyle = new DashStyle([2, 3], 0) };
        var segments = geometry.Segments(area, scope);
        foreach (var segment in segments.Where(segment => !frame.Has(segment.Position)))
            context.DrawLine(guide, segment.Start, segment.End);
        PaintCorners(context, area, new Pen(colors.Guide, 1));
        if (showSample) PaintSample(context, area, scope, colors.Sample);
        foreach (var segment in segments)
        {
            if (frame.Stroke(segment.Position) is { } stroke) PaintStroke(context, segment.Start, segment.End, stroke, area.Width < 40 ? 0.6 : 1);
        }
        if (focus is { } focused && segments.FirstOrDefault(segment => segment.Position == focused) is { } target)
            context.DrawLine(new Pen(colors.Focus, 2) { DashStyle = new DashStyle([1, 2], 0) }, target.Start, target.End);
    }

    public void PaintStroke(DrawingContext context, Point start, Point end, BorderStroke stroke, double scale = 1)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(stroke);
        var brush = new SolidColorBrush(Color.TryParse(stroke.ColorHex, out var colour) ? colour : Colors.Black);
        var width = Math.Clamp(stroke.WidthPoints * PixelsPerPoint * scale, 1, MaximumWidth);
        if (stroke.Dash == BorderDash.Double)
        {
            var dx = end.X - start.X;
            var dy = end.Y - start.Y;
            var length = Math.Max(1, Math.Sqrt(dx * dx + dy * dy));
            var offset = new Point(-dy / length * width, dx / length * width);
            var thin = new Pen(brush, Math.Max(1, width / 2));
            context.DrawLine(thin, start + offset, end + offset);
            context.DrawLine(thin, start - offset, end - offset);
            return;
        }
        var pen = new Pen(brush, width)
        {
            DashStyle = stroke.Dash switch
            {
                BorderDash.Dotted => new DashStyle([1, 1.5], 0),
                BorderDash.Dashed => new DashStyle([3, 2], 0),
                _ => null,
            },
        };
        context.DrawLine(pen, start, end);
    }

    private void PaintCorners(DrawingContext context, Rect area, Pen pen)
    {
        if (area.Width < 40) return;
        foreach (var corner in new[] { area.TopLeft, area.TopRight, area.BottomLeft, area.BottomRight })
        {
            var horizontal = corner.X <= area.X ? -1 : 1;
            var vertical = corner.Y <= area.Y ? -1 : 1;
            context.DrawLine(pen, corner + new Point(horizontal * 2, 0), corner + new Point(horizontal * (2 + CornerTick), 0));
            context.DrawLine(pen, corner + new Point(0, vertical * 2), corner + new Point(0, vertical * (2 + CornerTick)));
        }
    }

    private void PaintSample(DrawingContext context, Rect area, BorderFrameScope scope, IBrush brush)
    {
        var columns = scope.InsideVertical ? 2 : 1;
        var rows = scope.InsideHorizontal ? 2 : 1;
        var cellWidth = area.Width / columns;
        var cellHeight = area.Height / rows;
        if (cellWidth < 24 || cellHeight < 16) return;
        for (var row = 0; row < rows; row++)
            for (var column = 0; column < columns; column++)
            {
                var cell = new Rect(area.X + column * cellWidth, area.Y + row * cellHeight, cellWidth, cellHeight).Deflate(new Thickness(cellWidth * 0.22, cellHeight * 0.3));
                var bar = Math.Max(2, cell.Height / 5);
                context.FillRectangle(brush, new Rect(cell.X, cell.Y, cell.Width, bar), 1);
                context.FillRectangle(brush, new Rect(cell.X, cell.Y + cell.Height / 2 - bar / 2, cell.Width * 0.8, bar), 1);
                context.FillRectangle(brush, new Rect(cell.X, cell.Bottom - bar, cell.Width * 0.6, bar), 1);
            }
    }
}
