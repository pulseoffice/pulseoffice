using Avalonia;
using Avalonia.Media;
using PulseOffice.Charts;

namespace PulseOffice.UI.Controls.Charts;

public sealed class ChartSeriesStroke
{
    private const double PixelsPerPoint = 96.0 / 72.0;

    public Pen Pen(IBrush brush, ChartSeriesFormat format, double width)
    {
        var thickness = format.LineWidthPoints is > 0 and var points && double.IsFinite(points) ? points * PixelsPerPoint : width;
        return new Pen(brush, thickness)
        {
            LineJoin = PenLineJoin.Round,
            LineCap = format.Dash == LineDash.Solid ? PenLineCap.Round : PenLineCap.Flat,
            DashStyle = format.Dash switch
            {
                LineDash.Dash => new DashStyle([4, 2], 0),
                LineDash.Dot => new DashStyle([1, 1.5], 0),
                LineDash.DashDot => new DashStyle([4, 2, 1, 2], 0),
                _ => null,
            },
        };
    }

    public void Marker(DrawingContext context, IBrush brush, ChartSeriesFormat format, Point centre, double radius)
    {
        if (format.Marker == MarkerSymbol.None) return;
        var size = format.MarkerSize is { } points ? Math.Clamp(points, 2, 72) * PixelsPerPoint / 2 : radius;
        var outline = new Pen(brush, 1.2);
        switch (format.Marker)
        {
            case MarkerSymbol.Square:
                context.FillRectangle(brush, new Rect(centre.X - size, centre.Y - size, size * 2, size * 2));
                break;
            case MarkerSymbol.Diamond:
                context.DrawGeometry(brush, null, Polygon(centre, size, 4, -Math.PI / 2));
                break;
            case MarkerSymbol.Triangle:
                context.DrawGeometry(brush, null, Polygon(centre, size * 1.15, 3, -Math.PI / 2));
                break;
            case MarkerSymbol.Star:
                context.DrawGeometry(brush, null, Star(centre, size * 1.2));
                break;
            case MarkerSymbol.Cross:
                context.DrawLine(outline, new Point(centre.X - size, centre.Y - size), new Point(centre.X + size, centre.Y + size));
                context.DrawLine(outline, new Point(centre.X - size, centre.Y + size), new Point(centre.X + size, centre.Y - size));
                break;
            case MarkerSymbol.Plus:
                context.DrawLine(outline, new Point(centre.X - size, centre.Y), new Point(centre.X + size, centre.Y));
                context.DrawLine(outline, new Point(centre.X, centre.Y - size), new Point(centre.X, centre.Y + size));
                break;
            case MarkerSymbol.Dash:
                context.DrawLine(new Pen(brush, 2), new Point(centre.X - size, centre.Y), new Point(centre.X + size, centre.Y));
                break;
            case MarkerSymbol.Dot:
                context.DrawEllipse(brush, null, centre, Math.Max(1, size / 2), Math.Max(1, size / 2));
                break;
            default:
                context.DrawEllipse(brush, null, centre, size, size);
                break;
        }
    }

    private StreamGeometry Polygon(Point centre, double radius, int corners, double start)
    {
        var geometry = new StreamGeometry();
        using var sink = geometry.Open();
        for (var corner = 0; corner < corners; corner++)
        {
            var angle = start + Math.PI * 2 * corner / corners;
            var point = new Point(centre.X + radius * Math.Cos(angle), centre.Y + radius * Math.Sin(angle));
            if (corner == 0) sink.BeginFigure(point, true);
            else sink.LineTo(point);
        }
        sink.EndFigure(true);
        return geometry;
    }

    private StreamGeometry Star(Point centre, double radius)
    {
        var geometry = new StreamGeometry();
        using var sink = geometry.Open();
        for (var corner = 0; corner < 10; corner++)
        {
            var angle = -Math.PI / 2 + Math.PI * corner / 5;
            var reach = corner % 2 == 0 ? radius : radius * 0.45;
            var point = new Point(centre.X + reach * Math.Cos(angle), centre.Y + reach * Math.Sin(angle));
            if (corner == 0) sink.BeginFigure(point, true);
            else sink.LineTo(point);
        }
        sink.EndFigure(true);
        return geometry;
    }
}

public sealed class ChartAnalysisPainter
{
    private readonly ChartTrendFit _trends = new();
    private readonly ChartErrorSpans _errors = new();

    public void Paint(DrawingContext context, Plots.IChartPlot painter, Plots.ChartFrame frame, IReadOnlyList<Plots.ChartDataLabel> anchors)
    {
        for (var series = 0; series < frame.Data.Series.Count; series++)
        {
            var format = frame.Definition.SeriesFormat(series);
            if (format.Trendline is null && format.ErrorBars is null) continue;
            var points = anchors.Where(anchor => anchor.Series == series && double.IsFinite(anchor.Across)).OrderBy(anchor => anchor.Across).ToList();
            if (points.Count == 0 || painter.SeriesScale(frame, series) is not { } scale) continue;
            var colour = Plots.ChartColours.Resolve(frame.Data.Series[series].ColorHex, series);
            if (format.Trendline is { } trendline) Trend(context, trendline, points, scale, colour);
            if (format.ErrorBars is { } bars) Errors(context, bars, frame.Data.Series[series], points, scale, colour);
        }
    }

    private void Trend(DrawingContext context, ChartTrendline trendline, List<Plots.ChartDataLabel> points, PlotScale scale, Color colour)
    {
        var first = points[0];
        var last = points[^1];
        if (!(last.Across > first.Across)) return;
        var pixelsPerUnit = (last.Anchor.X - first.Anchor.X) / (last.Across - first.Across);
        var curve = _trends.Curve(trendline, [.. points.Select(point => (point.Across, point.Value))]);
        if (curve.Count < 2) return;
        var geometry = new StreamGeometry();
        using (var sink = geometry.Open())
        {
            for (var index = 0; index < curve.Count; index++)
            {
                var point = new Point(first.Anchor.X + (curve[index].X - first.Across) * pixelsPerUnit, scale.Position(curve[index].Y));
                if (index == 0) sink.BeginFigure(point, false);
                else sink.LineTo(point);
            }
            sink.EndFigure(false);
        }
        context.DrawGeometry(null, new Pen(new SolidColorBrush(colour, 0.85), 1.5) { DashStyle = new DashStyle([5, 3], 0) }, geometry);
    }

    private void Errors(DrawingContext context, ChartErrorBars bars, ChartSeries series, List<Plots.ChartDataLabel> points, PlotScale scale, Color colour)
    {
        var spans = _errors.Spans(bars, series.Values);
        var pen = new Pen(new SolidColorBrush(Color.FromRgb((byte)(colour.R / 2), (byte)(colour.G / 2), (byte)(colour.B / 2))), 1);
        foreach (var point in points)
        {
            if (point.Index < 0 || point.Index >= spans.Count || spans[point.Index] is not { } span) continue;
            var x = point.Anchor.X;
            var low = scale.Position(span.Low);
            var high = scale.Position(span.High);
            context.DrawLine(pen, new Point(x, low), new Point(x, high));
            if (!bars.EndCaps) continue;
            context.DrawLine(pen, new Point(x - 3, low), new Point(x + 3, low));
            context.DrawLine(pen, new Point(x - 3, high), new Point(x + 3, high));
        }
    }
}
