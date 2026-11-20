using Avalonia;
using Avalonia.Media;
using PulseOffice.Charts;

namespace PulseOffice.UI.Controls.Charts.Plots;

public sealed class StatisticalPlot : ChartPlotBase
{
    private readonly IChartStatistics _statistics;
    private readonly IChartHierarchy _hierarchy;

    public StatisticalPlot(IChartStatistics statistics, IChartHierarchy hierarchy)
    {
        _statistics = statistics;
        _hierarchy = hierarchy;
    }

    public StatisticalPlot() : this(new ChartStatistics(), new ChartHierarchy()) { }

    public override bool Handles(ChartType type) => type is
        ChartType.Histogram or ChartType.Pareto or ChartType.BoxPlot or ChartType.ViolinPlot
        or ChartType.DensityPlot or ChartType.CumulativeDistribution
        or ChartType.QuantileQuantile or ChartType.DotPlot or ChartType.BeeswarmPlot
        or ChartType.Waterfall;

    public override bool HasCategoryAxis(ChartType type) => type is ChartType.Waterfall;

    public override bool KeepsGaps => true;

    public override bool DrawsReversedCategories => true;

    public override PlotScale? ValueAxis(ChartFrame frame)
    {
        var values = Values(frame);
        if (values.Count == 0) return null;

        return frame.Definition.Type switch
        {
            ChartType.Histogram or ChartType.Pareto => PlotScale.Nice(
                0, Bars(frame, frame.Definition.Type is ChartType.Pareto).Select(bar => bar.Value).DefaultIfEmpty().Max(),
                frame.Plot.Bottom, frame.Plot.Y),

            ChartType.DensityPlot => PlotScale.Nice(
                0, _statistics.Density(values, 96).DefaultIfEmpty().Max(point => point.Y),
                frame.Plot.Bottom, frame.Plot.Y),

            ChartType.CumulativeDistribution => new PlotScale(0, 1, frame.Plot.Bottom, frame.Plot.Y),

            ChartType.Waterfall => WaterfallScale(frame),

            _ => PlotScale.Nice(values.Min(), values.Max(), frame.Plot.Bottom, frame.Plot.Y),
        };
    }

    private PlotScale WaterfallScale(ChartFrame frame)
    {
        var series = frame.Data.Series.FirstOrDefault();
        if (series is null) return new PlotScale(0, 1, frame.Plot.Bottom, frame.Plot.Y);

        var steps = _statistics.Waterfall(series.Values, frame.Definition.Statistical.Subtotals);
        var lowest = Math.Min(0, steps.Select(step => Math.Min(step.From, step.To)).DefaultIfEmpty(0).Min());
        var highest = Math.Max(0, steps.Select(step => Math.Max(step.From, step.To)).DefaultIfEmpty(0).Max());

        return PlotScale.Nice(lowest, highest, frame.Plot.Bottom, frame.Plot.Y);
    }

    public override void Draw(ChartFrame frame)
    {
        switch (frame.Definition.Type)
        {
            case ChartType.Histogram or ChartType.Pareto:
                DrawHistogram(frame, pareto: frame.Definition.Type is ChartType.Pareto);
                break;

            case ChartType.BoxPlot or ChartType.ViolinPlot:
                DrawBoxes(frame);
                break;

            case ChartType.DensityPlot:
                DrawCurve(frame, _statistics.Density(Values(frame), 96), fill: true);
                break;

            case ChartType.CumulativeDistribution:
                DrawCurve(frame, _statistics.Cumulative(Values(frame)), fill: false);
                break;

            case ChartType.QuantileQuantile:
                DrawQuantiles(frame);
                break;

            case ChartType.Waterfall:
                DrawWaterfall(frame);
                break;

            default:
                DrawPoints(frame, spread: frame.Definition.Type is ChartType.BeeswarmPlot);
                break;
        }
    }

    private IReadOnlyList<double> Values(ChartFrame frame) =>
        [.. frame.Data.Series.SelectMany(series => Finite(series.Values))];

    private IEnumerable<double> Finite(IEnumerable<double> values) => values.Where(double.IsFinite);

    private void DrawHistogram(ChartFrame frame, bool pareto)
    {
        var ordered = Bars(frame, pareto);
        if (ordered.Count == 0) return;

        var maximum = ordered.Max(bar => bar.Value);
        var scale = PlotScale.Nice(0, maximum, frame.Plot.Bottom, frame.Plot.Y);
        var categories = Across(frame, ordered.Count);

        for (var index = 0; index < ordered.Count; index++)
        {
            var top = scale.Position(ordered[index].Value);

            frame.Context.FillRectangle(
                Brush(frame.Data, 0),
                new Rect(
                    categories.Start(index) + categories.Width * 0.1,
                    top,
                    categories.Width * 0.8,
                    Math.Max(1, frame.Plot.Bottom - top)));

            Label(frame, ordered[index].Label,
                new Point(categories.Start(index), frame.Plot.Bottom + 2), 8);
        }

        if (!pareto) return;

        var totals = _statistics.RunningTotals([.. ordered.Select(bar => bar.Value)]);
        var overall = totals[^1];

        var points = totals
            .Select((total, index) => new Point(
                categories.Centre(index),
                frame.Plot.Bottom - frame.Plot.Height * (overall <= 0 ? 0 : total / overall)))
            .ToList();

        DrawPolyline(frame, points, frame.Marks.Mark(Colors.Crimson));
    }

    private IReadOnlyList<(string Label, double Value)> Bars(ChartFrame frame, bool pareto)
    {
        var categories = frame.Data.Categories;
        if (pareto && !frame.Data.OrdinalCategories && categories.Count > 0 && frame.Data.Series.FirstOrDefault() is { } series)
            return [.. categories.Select((label, index) => (Label: label, Value: index < series.Values.Count && double.IsFinite(series.Values[index]) ? Math.Max(0, series.Values[index]) : 0))
                .OrderByDescending(bar => bar.Value)];

        var bins = _statistics.Histogram(Values(frame), frame.Definition.Statistical);
        var bars = bins.Select(bin => (Label: bin.Lower.ToString("0.#", frame.Formatting), Value: (double)bin.Count));
        return pareto ? [.. bars.OrderByDescending(bar => bar.Value)] : [.. bars];
    }

    private void DrawBoxes(ChartFrame frame)
    {
        var everything = Values(frame);
        if (everything.Count == 0) return;

        var layout = frame.Definition.Statistical;
        var buckets = _hierarchy.Buckets(frame.Data);
        var seriesCount = Math.Max(1, frame.Data.Series.Count);
        var categories = Across(frame, Math.Max(1, buckets.Count));
        var scale = PlotScale.Nice(everything.Min(), everything.Max(), frame.Plot.Bottom, frame.Plot.Y);
        var pen = new Pen(frame.Marks.Stroke, 1.2);
        var slot = categories.Width * 0.8 / seriesCount;

        for (var bucket = 0; bucket < buckets.Count; bucket++)
            if (buckets[bucket].Label.Length > 0)
                Label(frame, buckets[bucket].Label, new Point(categories.Start(bucket), frame.Plot.Bottom + 2), 8);

        for (var index = 0; index < frame.Data.Series.Count; index++)
        {
            var series = frame.Data.Series[index];
            var means = new List<Point>();

            for (var bucket = 0; bucket < buckets.Count; bucket++)
            {
                var values = buckets[bucket].Points.Where(point => point < series.Values.Count).Select(point => series.Values[point]).Where(double.IsFinite).ToList();
                if (values.Count == 0) continue;

                var centre = categories.Start(bucket) + categories.Width * 0.1 + slot * (index + 0.5);
                var half = slot * 0.35;
                var summary = _statistics.Summary(values, layout.QuartileMethod);
                IReadOnlyList<double> outliers = layout.Outliers ? _statistics.Outliers(values, layout.QuartileMethod) : [];
                var inside = values.Where(value => !outliers.Contains(value)).DefaultIfEmpty(summary.Median).ToList();

                var top = scale.Position(summary.UpperQuartile);
                var bottom = scale.Position(summary.LowerQuartile);

                if (frame.Definition.Type is ChartType.ViolinPlot)
                    DrawViolin(frame, values, centre, half * 2, scale);

                frame.Context.DrawRectangle(
                    Brush(frame.Data, index, 0.55), pen,
                    new Rect(centre - half, Math.Min(top, bottom), half * 2, Math.Abs(bottom - top)));

                var median = scale.Position(summary.Median);
                frame.Context.DrawLine(new Pen(frame.Marks.Strong, 1.5), new Point(centre - half, median), new Point(centre + half, median));
                frame.Context.DrawLine(pen, new Point(centre, scale.Position(inside.Min())), new Point(centre, bottom));
                frame.Context.DrawLine(pen, new Point(centre, top), new Point(centre, scale.Position(inside.Max())));

                foreach (var outlier in outliers)
                    frame.Context.DrawEllipse(null, pen, new Point(centre, scale.Position(outlier)), 2.5, 2.5);

                if (layout.InnerPoints)
                    foreach (var value in inside)
                        frame.Context.DrawEllipse(Brush(frame.Data, index, 0.8), null, new Point(centre, scale.Position(value)), 1.8, 1.8);

                var mean = new Point(centre, scale.Position(values.Average()));
                means.Add(mean);
                if (layout.MeanMarker) DrawCross(frame, mean, half * 0.4, pen);
            }

            if (layout.MeanLine) DrawPolyline(frame, means, Colour(frame.Data, index));
        }
    }

    private void DrawCross(ChartFrame frame, Point centre, double size, Pen pen)
    {
        var arm = Math.Clamp(size, 2, 5);
        frame.Context.DrawLine(pen, new Point(centre.X - arm, centre.Y - arm), new Point(centre.X + arm, centre.Y + arm));
        frame.Context.DrawLine(pen, new Point(centre.X - arm, centre.Y + arm), new Point(centre.X + arm, centre.Y - arm));
    }

    private void DrawViolin(
        ChartFrame frame, IReadOnlyList<double> values, double centre, double width, PlotScale scale)
    {
        var density = _statistics.Density(values, 48);
        if (density.Count == 0) return;

        var peak = density.Max(point => point.Y);
        if (peak <= 0) return;

        var geometry = new StreamGeometry();

        using (var sink = geometry.Open())
        {
            sink.BeginFigure(new Point(centre, scale.Position(density[0].X)), true);

            foreach (var (x, y) in density)
                sink.LineTo(new Point(centre + width * (y / peak) / 2, scale.Position(x)));

            for (var index = density.Count - 1; index >= 0; index--)
                sink.LineTo(new Point(
                    centre - width * (density[index].Y / peak) / 2, scale.Position(density[index].X)));

            sink.EndFigure(true);
        }

        frame.Context.DrawGeometry(Brush(frame.Data, 0, 0.25), null, geometry);
    }

    private void DrawCurve(
        ChartFrame frame, IReadOnlyList<(double X, double Y)> points, bool fill)
    {
        if (points.Count < 2) return;

        var horizontal = PlotScale.Nice(
            points.Min(point => point.X), points.Max(point => point.X), frame.Plot.X, frame.Plot.Right);

        var vertical = PlotScale.Nice(
            0, points.Max(point => point.Y), frame.Plot.Bottom, frame.Plot.Y);

        var mapped = points
            .Select(point => new Point(horizontal.Position(point.X), vertical.Position(point.Y)))
            .ToList();

        if (fill)
        {
            var geometry = new StreamGeometry();

            using (var sink = geometry.Open())
            {
                sink.BeginFigure(new Point(mapped[0].X, frame.Plot.Bottom), true);
                foreach (var point in mapped) sink.LineTo(point);
                sink.LineTo(new Point(mapped[^1].X, frame.Plot.Bottom));
                sink.EndFigure(true);
            }

            frame.Context.DrawGeometry(Brush(frame.Data, 0, 0.3), null, geometry);
        }

        DrawPolyline(frame, mapped, Colour(frame.Data, 0));
    }

    private void DrawQuantiles(ChartFrame frame)
    {
        var pairs = _statistics.QuantilePairs(Values(frame));
        if (pairs.Count < 2) return;

        var horizontal = PlotScale.Nice(
            pairs.Min(pair => pair.Theoretical), pairs.Max(pair => pair.Theoretical),
            frame.Plot.X, frame.Plot.Right);

        var vertical = PlotScale.Nice(
            pairs.Min(pair => pair.Observed), pairs.Max(pair => pair.Observed),
            frame.Plot.Bottom, frame.Plot.Y);

        foreach (var (theoretical, observed) in pairs)
            frame.Context.DrawEllipse(
                Brush(frame.Data, 0), null,
                new Point(horizontal.Position(theoretical), vertical.Position(observed)), 3, 3);

        var pen = new Pen(frame.Marks.Stroke, 1)
        {
            DashStyle = new DashStyle([4, 3], 0),
        };

        frame.Context.DrawLine(pen,
            new Point(frame.Plot.X, frame.Plot.Bottom), new Point(frame.Plot.Right, frame.Plot.Y));
    }

    private void DrawPoints(ChartFrame frame, bool spread)
    {
        var categories = Across(frame, Math.Max(1, frame.Data.Series.Count));

        var everything = Values(frame);
        if (everything.Count == 0) return;

        var scale = PlotScale.Nice(everything.Min(), everything.Max(), frame.Plot.Bottom, frame.Plot.Y);

        for (var index = 0; index < frame.Data.Series.Count; index++)
        {
            var centre = categories.Centre(index);
            var offsets = new Dictionary<int, int>();

            foreach (var value in Finite(frame.Data.Series[index].Values))
            {
                var y = scale.Position(value);
                var key = (int)Math.Round(y / 6);

                offsets.TryGetValue(key, out var seen);
                offsets[key] = seen + 1;

                var shift = spread
                    ? (seen % 2 == 0 ? 1 : -1) * (seen + 1) / 2 * 5
                    : 0;

                frame.Context.DrawEllipse(
                    Brush(frame.Data, index, 0.8), null, new Point(centre + shift, y), 3, 3);
            }
        }
    }

    private void DrawWaterfall(ChartFrame frame)
    {
        var series = frame.Data.Series.FirstOrDefault();
        if (series is null || series.Values.Count == 0) return;

        var steps = _statistics.Waterfall(series.Values, frame.Definition.Statistical.Subtotals);
        var lowest = Math.Min(0, steps.Min(step => Math.Min(step.From, step.To)));
        var highest = Math.Max(0, steps.Max(step => Math.Max(step.From, step.To)));

        var scale = PlotScale.Nice(lowest, highest, frame.Plot.Bottom, frame.Plot.Y);
        var categories = Across(frame, steps.Count);
        var rising = new SolidColorBrush(frame.Marks.Mark(Color.FromRgb(0x16, 0xA3, 0x4A)));
        var falling = new SolidColorBrush(frame.Marks.Mark(Color.FromRgb(0xDC, 0x26, 0x26)));
        var total = Brush(frame.Data, 0);

        for (var index = 0; index < steps.Count; index++)
        {
            var (from, to, isTotal) = steps[index];

            var top = Math.Min(scale.Position(from), scale.Position(to));
            var height = Math.Max(1, Math.Abs(scale.Position(to) - scale.Position(from)));

            var brush = isTotal ? total : to >= from ? rising : falling;

            frame.Context.FillRectangle(brush, new Rect(
                categories.Start(index) + categories.Width * 0.15,
                top,
                categories.Width * 0.7,
                height));
        }
    }

    private void DrawPolyline(ChartFrame frame, IReadOnlyList<Point> points, Color colour)
    {
        if (points.Count < 2) return;

        var geometry = new StreamGeometry();

        using (var sink = geometry.Open())
        {
            sink.BeginFigure(points[0], false);
            foreach (var point in points.Skip(1)) sink.LineTo(point);
            sink.EndFigure(false);
        }

        frame.Context.DrawGeometry(null, new Pen(new SolidColorBrush(colour), 2), geometry);
    }
}
