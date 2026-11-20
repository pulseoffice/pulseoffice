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

    public override PlotScale? ValueAxis(ChartFrame frame) => PrimaryScale(frame, Prepared(frame), Splits(frame));

    public override PlotScale? SecondaryValueAxis(ChartFrame frame) => SecondaryScale(frame, Prepared(frame), Splits(frame));

    public override PlotScale? SeriesScale(ChartFrame frame, int series) =>
        ScaleOf(frame, series, PrimaryScale(frame, Prepared(frame), Splits(frame)), SecondaryValueAxis(frame));

    private bool Splits(ChartFrame frame) =>
        frame.Definition.HasSecondaryAxis && frame.Definition.Type is ChartType.Line or ChartType.SmoothLine or ChartType.SteppedLine or ChartType.Area
            or ChartType.StackedLine or ChartType.PercentStackedLine or ChartType.StackedArea or ChartType.PercentStackedArea;

    public override bool DrawsReversedCategories => true;

    private readonly ChartSeriesStroke _strokes = new();

    private ChartData Prepared(ChartFrame frame)
    {
        var type = frame.Definition.Type;
        var stacked = type is ChartType.StackedLine or ChartType.PercentStackedLine
            or ChartType.StackedArea or ChartType.PercentStackedArea;

        var split = Splits(frame);
        var sides = Enumerable.Range(0, frame.Data.Series.Count)
            .Select(series => split && frame.Definition.SeriesFormat(series).SecondaryAxis)
            .ToList();
        return Prepare(frame.Data, stacked, type is ChartType.PercentStackedLine or ChartType.PercentStackedArea, sides);
    }

    public override void Draw(ChartFrame frame)
    {
        var type = frame.Definition.Type;

        var filled = type is ChartType.Area or ChartType.StackedArea or ChartType.PercentStackedArea;
        var data = Prepared(frame);

        var scale = PrimaryScale(frame, data, Splits(frame));
        var secondary = SecondaryScale(frame, data, Splits(frame));
        var categories = Categories(frame, data);

        for (var series = data.Series.Count - 1; series >= 0; series--)
        {
            var own = ScaleOf(frame, series, scale, secondary);
            foreach (var points in Runs(data.Series[series], categories, own, type is ChartType.SteppedLine))
                DrawRun(frame, points, series, data, filled ? AreaFloor(frame, data, series, categories, own, points) : null, type);
        }

        if (frame.Definition.Type is ChartType.ControlChart) DrawControlLimits(frame, data, scale);
    }

    public override IEnumerable<ChartDataLabel> DataLabels(ChartFrame frame)
    {
        if (frame.Definition.Type is ChartType.ControlChart or ChartType.RunChart) yield break;
        var data = Prepared(frame);
        var scale = PrimaryScale(frame, data, Splits(frame));
        var secondary = SecondaryScale(frame, data, Splits(frame));
        var categories = Categories(frame, data);
        for (var series = 0; series < data.Series.Count; series++)
            for (var index = 0; index < data.Series[series].Values.Count; index++)
                if (IsPlotted(frame.Data.Series[series], index) && IsPlotted(data.Series[series], index))
                    yield return new ChartDataLabel(
                        new Point(categories.Centre(index), ScaleOf(frame, series, scale, secondary).Position(data.Series[series].Values[index])),
                        frame.Data.Series[series].Values[index])
                    { Series = series, Index = index, Across = index + 1 };
    }

    public IReadOnlyList<Point> AreaOutline(ChartFrame frame, int series)
    {
        var data = Prepared(frame);
        var scale = PrimaryScale(frame, data, Splits(frame));
        var own = ScaleOf(frame, series, scale, SecondaryScale(frame, data, Splits(frame)));
        var categories = Categories(frame, data);
        var points = Runs(data.Series[series], categories, own, false).FirstOrDefault() ?? [];
        return points.Count == 0 ? [] : [.. points, .. AreaFloor(frame, data, series, categories, own, points)];
    }

    private List<Point> AreaFloor(ChartFrame frame, ChartData data, int series, CategoryScale categories, PlotScale scale, List<Point> points)
    {
        var below = Stacked(frame.Definition.Type) ? Beneath(frame, series) : -1;
        if (below >= 0 && data.Series[below].Values.Count >= data.Series[series].Values.Count)
        {
            var lower = Runs(data.Series[below], categories, scale, false).FirstOrDefault();
            if (lower is not null && lower.Count == points.Count)
            {
                lower.Reverse();
                return lower;
            }
        }

        var baseline = scale.Position(Math.Clamp(0, Math.Min(scale.Minimum, scale.Maximum), Math.Max(scale.Minimum, scale.Maximum)));
        return [new Point(points[^1].X, baseline), new Point(points[0].X, baseline)];
    }

    private int Beneath(ChartFrame frame, int series)
    {
        var split = Splits(frame);
        var side = split && frame.Definition.SeriesFormat(series).SecondaryAxis;
        for (var candidate = series - 1; candidate >= 0; candidate--)
            if ((split && frame.Definition.SeriesFormat(candidate).SecondaryAxis) == side) return candidate;
        return -1;
    }

    private bool Stacked(ChartType type) => type is ChartType.StackedArea or ChartType.PercentStackedArea;

    private void DrawRun(ChartFrame frame, List<Point> points, int series, ChartData data, List<Point>? floor, ChartType type)
    {
        if (floor is not null) Fill(frame, points, floor, series, data);

        var geometry = new StreamGeometry();

        using (var sink = geometry.Open())
        {
            sink.BeginFigure(points[0], false);

            if (type is ChartType.SmoothLine) Smooth(sink, points);
            else foreach (var point in points.Skip(1)) sink.LineTo(point);

            sink.EndFigure(false);
        }

        var format = frame.Definition.SeriesFormat(series);
        frame.Context.DrawGeometry(null, _strokes.Pen(Brush(data, series), format, 2), geometry);

        foreach (var point in points)
            _strokes.Marker(frame.Context, Brush(data, series), format, point, 2.5);
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

    private void Fill(ChartFrame frame, IReadOnlyList<Point> points, IReadOnlyList<Point> floor, int series, ChartData data)
    {
        var geometry = new StreamGeometry();

        using (var sink = geometry.Open())
        {
            sink.BeginFigure(points[0], true);
            foreach (var point in points.Skip(1)) sink.LineTo(point);
            foreach (var point in floor) sink.LineTo(point);
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

    private ChartData Prepare(ChartData data, bool stacked, bool percent, IReadOnlyList<bool> secondary)
    {
        if (!stacked) return data;

        var count = data.Series.Count == 0 ? 0 : data.Series.Max(series => series.Values.Count);

        double[] Totals(bool side) =>
        [
            .. Enumerable.Range(0, count).Select(index =>
                data.Series.Where((_, series) => secondary[series] == side).Sum(series => Math.Abs(Value(series, index)))),
        ];

        var totals = new[] { Totals(false), Totals(true) };
        var running = new[] { new double[count], new double[count] };
        var result = new List<ChartSeries>();

        for (var series = 0; series < data.Series.Count; series++)
        {
            var side = secondary[series] ? 1 : 0;
            var values = new List<double>(count);

            for (var index = 0; index < count; index++)
            {
                var value = Value(data.Series[series], index);
                if (percent) value = totals[side][index] <= 0 ? 0 : value / totals[side][index] * 100;

                running[side][index] += value;
                values.Add(running[side][index]);
            }

            result.Add(data.Series[series] with { Values = values });
        }

        return new ChartData(data.Categories, result);
    }
}
