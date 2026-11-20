using Avalonia;
using Avalonia.Media;
using PulseOffice.Charts;

namespace PulseOffice.UI.Controls.Charts.Plots;

public sealed class PiePlot(IPieSplitter splitter) : ChartPlotBase
{
    public PiePlot() : this(new PieSplitter()) { }

    public override bool Handles(ChartType type) => type is
        ChartType.Pie or ChartType.Doughnut or ChartType.ExplodedPie
        or ChartType.PieOfPie or ChartType.BarOfPie;

    public override bool DrawsAxes => false;

    public override IReadOnlyList<ChartLegendItem> Legend(ChartDefinition definition, ChartData data)
    {
        var series = data.Series.FirstOrDefault();
        if (series is null) return [];
        return [.. Enumerable.Range(0, series.Values.Count)
            .Select(index => new ChartLegendItem(index < data.Categories.Count ? data.Categories[index] : (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), Slice(definition, index)))];
    }

    public override IEnumerable<ChartDataLabel> DataLabels(ChartFrame frame)
    {
        foreach (var slice in Slices(frame))
        {
            var middleAngle = slice.Start + slice.Sweep / 2;
            if (slice.Box is { } box)
            {
                yield return new ChartDataLabel(box.Center, slice.Value)
                {
                    Series = 0, Index = slice.Index, Placement = ChartLabelPlacement.Centre,
                    Base = new Point(box.Left, box.Center.Y), Tip = new Point(box.Right, box.Center.Y),
                };
                continue;
            }
            var inner = frame.Definition.Type is ChartType.Doughnut ? slice.Radius * 0.5 : 0;
            var label = frame.Definition.Type is ChartType.Doughnut ? slice.Radius * 0.75 : slice.Radius * 0.62;
            yield return new ChartDataLabel(Polar(slice.Centre, label, middleAngle), slice.Value)
            {
                Series = 0, Index = slice.Index, Placement = ChartLabelPlacement.Centre,
                Base = Polar(slice.Centre, inner, middleAngle), Tip = Polar(slice.Centre, slice.Radius, middleAngle),
            };
        }
    }

    private IBrush SliceBrush(ChartDefinition definition, int index) => new SolidColorBrush(Slice(definition, index));

    private Color Slice(ChartDefinition definition, int index) => ChartColours.Resolve(definition.PointFormat(0, index).ColorHex, index);

    private double Explosion(ChartDefinition definition, int index) =>
        definition.PointFormat(0, index).ExplosionPercent / 100.0;

    public IReadOnlyList<PieSlice> Slices(ChartFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        var series = frame.Data.Series.FirstOrDefault();
        if (series is null) return [];
        var plotted = Enumerable.Range(0, series.Values.Count).Where(index => double.IsFinite(series.Values[index]) && series.Values[index] != 0).ToList();
        var total = plotted.Sum(index => Math.Abs(series.Values[index]));
        if (total <= 0) return [];
        var second = frame.Definition.Type is ChartType.PieOfPie or ChartType.BarOfPie
            ? splitter.SecondPlot(frame.Definition.PieSplit, plotted, series.Values)
            : [];
        return second.Count > 0
            ? Composite(frame, series, plotted, second, total)
            : Single(frame, series, plotted, total);
    }

    private List<PieSlice> Single(ChartFrame frame, ChartSeries series, List<int> plotted, double total)
    {
        var exploded = frame.Definition.Type is ChartType.ExplodedPie;
        var furthest = plotted.Select(index => Explosion(frame.Definition, index)).DefaultIfEmpty(0).Max();
        var radius = (Math.Min(frame.Plot.Width, frame.Plot.Height) / 2 - (exploded ? 10 : 4)) / (1 + furthest);
        var centre = frame.Plot.Center;
        var start = -Math.PI / 2;
        var slices = new List<PieSlice>();
        foreach (var index in plotted)
        {
            var sweep = Math.Abs(series.Values[index]) / total * Math.PI * 2;
            var distance = (exploded ? 8 : 0) + radius * Explosion(frame.Definition, index);
            var offset = distance > 0 ? Polar(new Point(0, 0), distance, start + sweep / 2) : new Point(0, 0);
            slices.Add(new PieSlice(index, series.Values[index], new Point(centre.X + offset.X, centre.Y + offset.Y), radius, start, sweep));
            start += sweep;
        }
        return slices;
    }

    private List<PieSlice> Composite(ChartFrame frame, ChartSeries series, List<int> plotted, IReadOnlyList<int> second, double total)
    {
        var first = plotted.Where(index => !second.Contains(index)).ToList();
        var size = Math.Clamp(frame.Definition.PieSplit.SecondPlotSize, ChartPieSplit.MinimumSecondPlotSize, ChartPieSplit.MaximumSecondPlotSize) / 100.0;
        var secondTotal = second.Sum(index => Math.Abs(series.Values[index]));
        var plot = frame.Plot;
        var radius = Math.Max(4, Math.Min(plot.Width * 0.27, plot.Height / 2 - 4));
        var centre = new Point(plot.X + plot.Width * 0.3, plot.Center.Y);
        var otherSweep = secondTotal / total * Math.PI * 2;
        var slices = new List<PieSlice> { new(-1, secondTotal, centre, radius, -otherSweep / 2, otherSweep) };
        var start = otherSweep / 2;
        foreach (var index in first)
        {
            var sweep = Math.Abs(series.Values[index]) / total * Math.PI * 2;
            slices.Add(new PieSlice(index, series.Values[index], centre, radius, start, sweep));
            start += sweep;
        }

        var secondCentre = new Point(plot.X + plot.Width * 0.78, plot.Center.Y);
        var secondRadius = Math.Max(2, Math.Min(radius * size * 0.8, Math.Min(plot.Width * 0.21, plot.Height / 2 - 2)));
        if (frame.Definition.Type is ChartType.BarOfPie)
        {
            var height = Math.Max(4, Math.Min(radius * 2.1 * size, plot.Height - 4));
            var width = Math.Max(4, radius * 0.45);
            var top = secondCentre.Y - height / 2;
            foreach (var index in second)
            {
                var share = Math.Abs(series.Values[index]) / secondTotal * height;
                slices.Add(new PieSlice(index, series.Values[index], secondCentre, 0, 0, 0) { Box = new Rect(secondCentre.X - width / 2, top, width, share) });
                top += share;
            }
            return slices;
        }

        var secondStart = Math.PI - Math.Abs(series.Values[second[0]]) / secondTotal * Math.PI;
        foreach (var index in second)
        {
            var sweep = Math.Abs(series.Values[index]) / secondTotal * Math.PI * 2;
            slices.Add(new PieSlice(index, series.Values[index], secondCentre, secondRadius, secondStart, sweep));
            secondStart += sweep;
        }
        return slices;
    }

    public override void Draw(ChartFrame frame)
    {
        var slices = Slices(frame);
        var pen = new Pen(frame.Marks.Separator, 1);
        foreach (var slice in slices)
        {
            var brush = slice.Index < 0 ? new SolidColorBrush(ChartColours.Resolve(null, frame.Data.Series[0].Values.Count)) : SliceBrush(frame.Definition, slice.Index);
            if (slice.Box is { } box)
            {
                frame.Context.DrawRectangle(brush, pen, box);
                continue;
            }
            if (slice.Sweep >= Math.PI * 2 - 1e-9)
            {
                frame.Context.DrawEllipse(brush, pen, slice.Centre, slice.Radius, slice.Radius);
                continue;
            }
            var geometry = new StreamGeometry();
            using (var sink = geometry.Open())
            {
                sink.BeginFigure(slice.Centre, true);
                sink.LineTo(Polar(slice.Centre, slice.Radius, slice.Start));
                sink.ArcTo(Polar(slice.Centre, slice.Radius, slice.Start + slice.Sweep),
                    new Size(slice.Radius, slice.Radius), 0, slice.Sweep > Math.PI, SweepDirection.Clockwise);
                sink.LineTo(slice.Centre);
                sink.EndFigure(true);
            }
            frame.Context.DrawGeometry(brush, pen, geometry);
        }

        DrawConnectors(frame, slices, pen);

        if (frame.Definition.Type is ChartType.Doughnut && slices.Count > 0)
            frame.Context.DrawEllipse(frame.Marks.Separator, null, frame.Plot.Center, slices[0].Radius * 0.5, slices[0].Radius * 0.5);
    }

    private void DrawConnectors(ChartFrame frame, IReadOnlyList<PieSlice> slices, Pen pen)
    {
        if (slices.FirstOrDefault(slice => slice.Index < 0) is not { } other) return;
        var secondary = slices.Where(slice => slice.Index >= 0 && slice.Centre != other.Centre).ToList();
        if (secondary.Count == 0) return;
        var upper = Polar(other.Centre, other.Radius, other.Start);
        var lower = Polar(other.Centre, other.Radius, other.Start + other.Sweep);
        var (top, bottom) = secondary[0].Box is not null
            ? (secondary[0].Box!.Value.TopLeft, secondary[^1].Box!.Value.BottomLeft)
            : (new Point(secondary[0].Centre.X, secondary[0].Centre.Y - secondary[0].Radius), new Point(secondary[0].Centre.X, secondary[0].Centre.Y + secondary[0].Radius));
        frame.Context.DrawLine(pen, upper, top);
        frame.Context.DrawLine(pen, lower, bottom);
    }
}

public sealed record PieSlice(int Index, double Value, Point Centre, double Radius, double Start, double Sweep)
{
    public Rect? Box { get; init; }
}
