using PulseOffice.Sheets.Engine.Operations;
using Avalonia;
using Avalonia.Media;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Styling;
using PulseOffice.UI.Theming;

namespace PulseOffice.Sheets.UI.GridParts.Painting;

internal sealed class CellDecorationPainter(
    IGridTheme theme, GridRenderServices services, Engine.Operations.ISparklineData sparklines)
{
    private static readonly IBrush CommentMarkBrush = new SolidColorBrush(Color.FromRgb(220, 38, 38));

    public int? FilterButtonAt(Worksheet sheet, CellAddress address)
    {
        if (sheet.AutoFilter.Range is not { } range) return null;
        if (address.Row != range.Start.Row) return null;
        if (address.Column < range.Start.Column || address.Column > range.End.Column) return null;

        return address.Column - range.Start.Column;
    }

    public Rect FilterButtonBox(Rect bounds)
    {
        var size = Math.Min(14, Math.Min(bounds.Width, bounds.Height) - 2);
        return new Rect(bounds.Right - size - 2, bounds.Y + (bounds.Height - size) / 2, size, size);
    }

    public void DrawFilterButton(DrawingContext context, Rect bounds, bool filtered)
    {
        var size = Math.Min(14, Math.Min(bounds.Width, bounds.Height) - 2);
        if (size <= 4) return;

        var box = FilterButtonBox(bounds);

        context.FillRectangle(
            filtered
                ? theme.Brush(Palette.Keys.Success, Brushes.SeaGreen)
                : theme.Brush(Palette.Keys.SurfaceRaised, Brushes.White),
            box,
            2);

        context.DrawRectangle(new Pen(theme.Brush(Palette.Keys.Border, Brushes.LightGray)), box, 2);

        var arrow = new StreamGeometry();

        using (var draw = arrow.Open())
        {
            draw.BeginFigure(new Point(box.X + size * 0.25, box.Y + size * 0.4), true);
            draw.LineTo(new Point(box.X + size * 0.75, box.Y + size * 0.4));
            draw.LineTo(new Point(box.Center.X, box.Y + size * 0.7));
            draw.EndFigure(true);
        }

        context.DrawGeometry(
            filtered ? Brushes.White : theme.Brush(Palette.Keys.TextPrimary, Brushes.Black),
            null,
            arrow);
    }

    public void DrawCommentMark(DrawingContext context, Rect bounds)
    {
        const double size = 6;

        var geometry = new StreamGeometry();

        using (var sink = geometry.Open())
        {
            sink.BeginFigure(new Point(bounds.Right - size, bounds.Y), isFilled: true);
            sink.LineTo(new Point(bounds.Right, bounds.Y));
            sink.LineTo(new Point(bounds.Right, bounds.Y + size));
            sink.EndFigure(isClosed: true);
        }

        context.DrawGeometry(CommentMarkBrush, null, geometry);
    }

    public void DrawChangeMark(DrawingContext context, Rect bounds)
    {
        var pen = services.Paints.Pen(Color.FromRgb(0xC0, 0x26, 0xD3), 1.2);

        context.DrawRectangle(null, pen, bounds.Deflate(new Thickness(0.5)));
    }

    public void DrawSparkline(
        DrawingContext context, Rect bounds, Worksheet sheet, SparklineGroup group, Sparkline sparkline) =>
        services.SparklinePainter.Paint(context, bounds, sparklines.Read(sheet, group, sparkline), group.Kind, group.Style);

    public void DrawConditionalFill(DrawingContext context, ConditionalAppearance appearance, Rect bounds)
    {
        if (services.Paints.Brush(appearance.ScaleColorHex) is { } scale)
            context.FillRectangle(scale, bounds);

        if (appearance.BarShape is not { } bar || !Color.TryParse(bar.FillHex, out var barColor)) return;

        var left = bounds.X + bounds.Width * Math.Clamp(bar.Start, 0, 1);
        var right = bounds.X + bounds.Width * Math.Clamp(bar.End, 0, 1);
        var area = new Rect(left, bounds.Y + 1, Math.Max(0, right - left), Math.Max(0, bounds.Height - 2));
        if (area.Width > 0)
        {
            IBrush fill = bar.Gradient ? BarGradient(barColor, bar.Axis is { } at && bar.End <= at) : services.Paints.Brush(barColor);
            context.FillRectangle(fill, area);
            if (bar.BorderHex is { } border && Color.TryParse(border, out var borderColor))
                context.DrawRectangle(services.Paints.Pen(borderColor, 1), area.Deflate(0.5));
        }

        if (bar.Axis is { } axis && Color.TryParse(bar.AxisColorHex, out var axisColor))
        {
            var x = Math.Round(bounds.X + bounds.Width * axis) + 0.5;
            context.DrawLine(services.Paints.Pen(axisColor, 1, DashStyle.Dash), new Point(x, bounds.Y), new Point(x, bounds.Bottom));
        }
    }

    private IBrush BarGradient(Color color, bool towardsLeft) => new LinearGradientBrush
    {
        StartPoint = new RelativePoint(towardsLeft ? 1 : 0, 0.5, RelativeUnit.Relative),
        EndPoint = new RelativePoint(towardsLeft ? 0 : 1, 0.5, RelativeUnit.Relative),
        GradientStops = { new Avalonia.Media.GradientStop(color, 0), new Avalonia.Media.GradientStop(Color.FromArgb(255, 255, 255, 255), 1) },
    };

    public void DrawStyleBackground(DrawingContext context, CellStyle style, Rect bounds)
    {
        if (services.Paints.Brush(style.BackgroundHex) is not { } background) return;
        context.FillRectangle(background, bounds);
    }

    public void DrawStyleBorders(DrawingContext context, CellStyle style, Rect bounds)
    {
        if (style.Borders.IsEmpty) return;

        Draw(style.Borders.Left, bounds.TopLeft, bounds.BottomLeft);
        Draw(style.Borders.Top, bounds.TopLeft, bounds.TopRight);
        Draw(style.Borders.Right, bounds.TopRight, bounds.BottomRight);
        Draw(style.Borders.Bottom, bounds.BottomLeft, bounds.BottomRight);
        Draw(style.Borders.DiagonalUp, bounds.BottomLeft, bounds.TopRight);
        Draw(style.Borders.DiagonalDown, bounds.TopLeft, bounds.BottomRight);

        void Draw(BorderEdge edge, Point from, Point to)
        {
            if (!edge.IsVisible) return;
            var color = services.Paints.Brush(edge.ColorHex) is ISolidColorBrush solid ? solid.Color : Colors.Black;

            if (edge.Style == BorderStyle.Double)
            {
                var thin = services.Paints.Pen(color, 1);
                var horizontal = Math.Abs(to.Y - from.Y) < 0.5;
                var vertical = Math.Abs(to.X - from.X) < 0.5;
                var offset = horizontal ? new Vector(0, 1.5) : vertical ? new Vector(1.5, 0) : new Vector(0, 0);

                context.DrawLine(thin, from - offset, to - offset);
                context.DrawLine(thin, from + offset, to + offset);
                return;
            }

            var pen = services.Paints.Pen(color, Math.Max(edge.Width.ToPoints(), 1), edge.Style switch
            {
                BorderStyle.Dashed => DashStyle.Dash,
                BorderStyle.Dotted => DashStyle.Dot,
                _ => null,
            });

            context.DrawLine(pen, from, to);
        }
    }
}
