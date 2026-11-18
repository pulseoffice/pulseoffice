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

    public void Paint(DrawingContext context, Plots.IChartPlot painter, Plots.ChartFrame frame, IReadOnlyList<Plots.ChartDataLabel> anchors) =>
        Paint(context, painter, frame, anchors, false);

    public void Paint(DrawingContext context, Plots.IChartPlot painter, Plots.ChartFrame frame, IReadOnlyList<Plots.ChartDataLabel> anchors, bool horizontal)
    {
        for (var series = 0; series < frame.Data.Series.Count; series++)
        {
            var format = frame.Definition.SeriesFormat(series);
            if (format.Trendline is null && format.ErrorBars is null && format.XErrorBars is null) continue;
            var points = Ordered(anchors, series);
            if (points.Count == 0 || painter.SeriesScale(frame, series) is not { } scale) continue;
            var colour = Plots.ChartColours.Resolve(frame.Data.Series[series].ColorHex, series);
            if (format.Trendline is { } trendline) Trend(context, trendline, points, scale, colour, frame.Formatting, horizontal);
            if (format.ErrorBars is { } bars) Whiskers(context, bars, ValueErrors(bars, frame.Data.Series[series], points, scale, horizontal), colour);
            if (format.XErrorBars is { } across) Whiskers(context, across, CategoryErrors(across, frame.Data.Series[series], points, horizontal), colour);
        }
    }

    public IReadOnlyList<Point> TrendPoints(ChartTrendline trendline, IReadOnlyList<Plots.ChartDataLabel> anchors, int series, PlotScale scale, bool horizontal)
    {
        var points = Ordered(anchors, series);
        if (points.Count < 2 || !(points[^1].Across > points[0].Across)) return [];
        var pixelsPerUnit = PixelsPerUnit(points, horizontal);
        var curve = _trends.Curve(trendline, [.. points.Select(point => (point.Across, point.Value))]);
        return curve.Count < 2 ? [] : [.. curve.Select(point => At(Along(points[0], horizontal) + (point.X - points[0].Across) * pixelsPerUnit, scale.Position(point.Y), horizontal))];
    }

    public IReadOnlyList<(Point From, Point To)> ValueErrors(ChartErrorBars bars, ChartSeries series, IReadOnlyList<Plots.ChartDataLabel> points, PlotScale scale, bool horizontal)
    {
        var spans = _errors.Spans(bars, series.Values);
        var lines = new List<(Point, Point)>();
        foreach (var point in points)
        {
            if (point.Index < 0 || point.Index >= spans.Count || spans[point.Index] is not { } span) continue;
            var category = Along(point, horizontal);
            lines.Add((At(category, scale.Position(span.Low), horizontal), At(category, scale.Position(span.High), horizontal)));
        }
        return lines;
    }

    public IReadOnlyList<(Point From, Point To)> CategoryErrors(ChartErrorBars bars, ChartSeries series, IReadOnlyList<Plots.ChartDataLabel> points, bool horizontal)
    {
        if (points.Count < 2 || !(points[^1].Across > points[0].Across)) return [];
        var first = points[0];
        var pixelsPerUnit = PixelsPerUnit(points, horizontal);
        var across = Enumerable.Repeat(double.NaN, series.Values.Count).ToArray();
        foreach (var point in points.Where(point => point.Index >= 0 && point.Index < across.Length)) across[point.Index] = point.Across;
        var spans = _errors.Spans(bars, across);
        var lines = new List<(Point, Point)>();
        foreach (var point in points)
        {
            if (point.Index < 0 || point.Index >= spans.Count || spans[point.Index] is not { } span) continue;
            var value = horizontal ? point.Anchor.X : point.Anchor.Y;
            var low = Along(first, horizontal) + (span.Low - first.Across) * pixelsPerUnit;
            var high = Along(first, horizontal) + (span.High - first.Across) * pixelsPerUnit;
            lines.Add((At(low, value, horizontal), At(high, value, horizontal)));
        }
        return lines;
    }

    private List<Plots.ChartDataLabel> Ordered(IReadOnlyList<Plots.ChartDataLabel> anchors, int series) =>
        [.. anchors.Where(anchor => anchor.Series == series && double.IsFinite(anchor.Across)).OrderBy(anchor => anchor.Across)];

    private double Along(Plots.ChartDataLabel point, bool horizontal) => horizontal ? point.Anchor.Y : point.Anchor.X;

    private Point At(double category, double value, bool horizontal) => horizontal ? new Point(value, category) : new Point(category, value);

    private double PixelsPerUnit(IReadOnlyList<Plots.ChartDataLabel> points, bool horizontal) =>
        (Along(points[^1], horizontal) - Along(points[0], horizontal)) / (points[^1].Across - points[0].Across);

    private void Trend(DrawingContext context, ChartTrendline trendline, List<Plots.ChartDataLabel> points, PlotScale scale, Color colour, System.Globalization.CultureInfo culture, bool horizontal)
    {
        var curve = TrendPoints(trendline, points, points[0].Series, scale, horizontal);
        if (curve.Count < 2) return;
        var geometry = new StreamGeometry();
        using (var sink = geometry.Open())
        {
            sink.BeginFigure(curve[0], false);
            foreach (var point in curve.Skip(1)) sink.LineTo(point);
            sink.EndFigure(false);
        }
        context.DrawGeometry(null, new Pen(new SolidColorBrush(colour, 0.85), 1.5) { DashStyle = new DashStyle([5, 3], 0) }, geometry);
        if (!trendline.DisplayEquation && !trendline.DisplayRSquared) return;
        if (_trends.Summary(trendline, [.. points.Select(point => (point.Across, point.Value))], culture) is not { } summary) return;
        var lines = new List<string>();
        if (trendline.DisplayEquation) lines.Add(summary.Equation);
        if (trendline.DisplayRSquared) lines.Add("R² = " + summary.RSquared.ToString("0.####", culture));
        var text = new FormattedText(string.Join(Environment.NewLine, lines), culture, FlowDirection.LeftToRight, Typeface.Default, 9,
            new SolidColorBrush(Color.FromRgb((byte)(colour.R / 2), (byte)(colour.G / 2), (byte)(colour.B / 2))));
        var anchor = curve[^1];
        context.DrawText(text, new Point(anchor.X - text.Width - 4, anchor.Y - text.Height - 4));
    }

    private Pen ErrorPen(Color colour) => new(new SolidColorBrush(Color.FromRgb((byte)(colour.R / 2), (byte)(colour.G / 2), (byte)(colour.B / 2))), 1);

    private void Whiskers(DrawingContext context, ChartErrorBars bars, IReadOnlyList<(Point From, Point To)> lines, Color colour)
    {
        var pen = ErrorPen(colour);
        foreach (var (from, to) in lines)
        {
            context.DrawLine(pen, from, to);
            if (!bars.EndCaps) continue;
            var length = Math.Max(Math.Sqrt(Math.Pow(to.X - from.X, 2) + Math.Pow(to.Y - from.Y, 2)), 1e-9);
            var normal = new Vector(-(to.Y - from.Y) / length * 3, (to.X - from.X) / length * 3);
            context.DrawLine(pen, from - normal, from + normal);
            context.DrawLine(pen, to - normal, to + normal);
        }
    }
}
