using Avalonia;
using Avalonia.Media;
using PulseOffice.Charts;

namespace PulseOffice.UI.Controls.Charts.Plots;

public sealed class LinePlot : ChartPlotBase
{
    public override bool Handles(ChartType type) => type is
        ChartType.Line or ChartType.SmoothLine or ChartType.SteppedLine
        or ChartType.StackedLine or ChartType.PercentStackedLine
        or ChartType.Area or ChartType.StackedArea or ChartType.PercentStackedArea
        or ChartType.RunChart or ChartType.ControlChart;

    public override bool KeepsGaps => true;

    public override PlotScale? ValueAxis(ChartFrame frame) => ValueScale(Prepared(frame), frame.Plot);

    private ChartData Prepared(ChartFrame frame)
    {
        var type = frame.Definition.Type;
        var stacked = type is ChartType.StackedLine or ChartType.PercentStackedLine
            or ChartType.StackedArea or ChartType.PercentStackedArea;

        return Prepare(frame.Data, stacked, type is ChartType.PercentStackedLine or ChartType.PercentStackedArea);
    }

    public override void Draw(ChartFrame frame)
    {
        var type = frame.Definition.Type;

        var filled = type is ChartType.Area or ChartType.StackedArea or ChartType.PercentStackedArea;
        var data = Prepared(frame);

        var scale = ValueScale(data, frame.Plot);
        var categories = Categories(data, frame.Plot);

        for (var series = data.Series.Count - 1; series >= 0; series--)
            foreach (var points in Runs(data.Series[series], categories, scale, type is ChartType.SteppedLine))
                DrawRun(frame, points, series, scale, data, filled, type);

        if (frame.Definition.Type is ChartType.ControlChart) DrawControlLimits(frame, data, scale);
    }

    public override IEnumerable<ChartDataLabel> DataLabels(ChartFrame frame)
    {
        if (frame.Definition.Type is ChartType.ControlChart or ChartType.RunChart) yield break;
        var data = Prepared(frame);
        var scale = ValueScale(data, frame.Plot);
        var categories = Categories(data, frame.Plot);
        for (var series = 0; series < data.Series.Count; series++)
            for (var index = 0; index < data.Series[series].Values.Count; index++)
                if (IsPlotted(frame.Data.Series[series], index) && IsPlotted(data.Series[series], index))
                    yield return new ChartDataLabel(
                        new Point(categories.Centre(index), scale.Position(data.Series[series].Values[index])),
                        frame.Data.Series[series].Values[index]);
    }

    private void DrawRun(ChartFrame frame, List<Point> points, int series, PlotScale scale, ChartData data, bool filled, ChartType type)
    {
        if (filled) Fill(frame, points, series, scale, data);

        var geometry = new StreamGeometry();

        using (var sink = geometry.Open())
        {
            sink.BeginFigure(points[0], false);

            if (type is ChartType.SmoothLine) Smooth(sink, points);
            else foreach (var point in points.Skip(1)) sink.LineTo(point);

            sink.EndFigure(false);
        }

        frame.Context.DrawGeometry(null, new Pen(Brush(data, series), 2)
        {
            LineJoin = PenLineJoin.Round,
            LineCap = PenLineCap.Round,
        }, geometry);

        foreach (var point in points)
            frame.Context.DrawEllipse(Brush(data, series), null, point, 2.5, 2.5);
    }

    private void DrawControlLimits(ChartFrame frame, ChartData data, PlotScale scale)
    {
        var values = data.Series.SelectMany(series => series.Values).Where(double.IsFinite).ToList();
        if (values.Count < 2) return;

        var mean = values.Average();
        var deviation = Math.Sqrt(values.Sum(value => Math.Pow(value - mean, 2)) / values.Count);

        var pen = new Pen(new SolidColorBrush(Colors.Crimson, 0.8), 1)
        {
            DashStyle = new DashStyle([4, 3], 0),
        };

        foreach (var limit in new[] { mean, mean + 3 * deviation, mean - 3 * deviation })
        {
            var y = scale.Position(limit);

            frame.Context.DrawLine(pen, new Point(frame.Plot.X, y), new Point(frame.Plot.Right, y));
        }
    }

    private void Fill(
        ChartFrame frame, IReadOnlyList<Point> points, int series, PlotScale scale, ChartData data)
    {
        var geometry = new StreamGeometry();
        var baseline = scale.Position(Math.Max(0, scale.Minimum));

        using (var sink = geometry.Open())
        {
            sink.BeginFigure(new Point(points[0].X, baseline), true);

            foreach (var point in points) sink.LineTo(point);

            sink.LineTo(new Point(points[^1].X, baseline));
            sink.EndFigure(true);
        }

        frame.Context.DrawGeometry(Brush(data, series, 0.35), null, geometry);
    }

    private void Smooth(StreamGeometryContext sink, IReadOnlyList<Point> points)
    {
        for (var index = 1; index < points.Count; index++)
        {
            var previous = points[index - 1];
            var current = points[index];
            var middle = (previous.X + current.X) / 2;

            sink.CubicBezierTo(
                new Point(middle, previous.Y), new Point(middle, current.Y), current);
        }
    }

    private List<List<Point>> Runs(
        ChartSeries series, CategoryScale categories, PlotScale scale, bool stepped)
    {
        var runs = new List<List<Point>>();
        var points = new List<Point>();

        for (var index = 0; index < series.Values.Count; index++)
        {
            if (!double.IsFinite(series.Values[index]))
            {
                if (points.Count > 0) runs.Add(points);
                points = [];
                continue;
            }

            var point = new Point(categories.Centre(index), scale.Position(series.Values[index]));

            if (stepped && points.Count > 0) points.Add(new Point(point.X, points[^1].Y));

            points.Add(point);
        }

        if (points.Count > 0) runs.Add(points);
        return runs;
    }

    private ChartData Prepare(ChartData data, bool stacked, bool percent)
    {
        if (!stacked) return data;

        var count = data.Series.Count == 0 ? 0 : data.Series.Max(series => series.Values.Count);

        var totals = Enumerable.Range(0, count)
            .Select(index => data.Series.Sum(series => Math.Abs(Value(series, index))))
            .ToList();

        var running = new double[count];
        var series = new List<ChartSeries>();

        foreach (var current in data.Series)
        {
            var values = new List<double>(count);

            for (var index = 0; index < count; index++)
            {
                var value = Value(current, index);
                if (percent) value = totals[index] <= 0 ? 0 : value / totals[index] * 100;

                running[index] += value;
                values.Add(running[index]);
            }

            series.Add(current with { Values = values });
        }

        return new ChartData(data.Categories, series);
    }
}
