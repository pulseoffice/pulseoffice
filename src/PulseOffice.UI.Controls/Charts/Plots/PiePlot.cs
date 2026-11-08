using Avalonia;
using Avalonia.Media;
using PulseOffice.Charts;

namespace PulseOffice.UI.Controls.Charts.Plots;

public sealed class PiePlot : ChartPlotBase
{
    public override bool Handles(ChartType type) => type is
        ChartType.Pie or ChartType.Doughnut or ChartType.ExplodedPie
        or ChartType.PieOfPie or ChartType.BarOfPie;

    public override bool DrawsAxes => false;

    public override IReadOnlyList<ChartLegendItem> Legend(ChartDefinition definition, ChartData data)
    {
        var series = data.Series.FirstOrDefault();
        if (series is null) return [];
        return [.. Enumerable.Range(0, series.Values.Count)
            .Select(index => new ChartLegendItem(index < data.Categories.Count ? data.Categories[index] : (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), Slice(index)))];
    }

    public override IEnumerable<ChartDataLabel> DataLabels(ChartFrame frame)
    {
        var series = frame.Data.Series.FirstOrDefault();
        if (series is null) yield break;
        var total = series.Values.Where(double.IsFinite).Sum(Math.Abs);
        if (total <= 0) yield break;

        var radius = Math.Min(frame.Plot.Width, frame.Plot.Height) / 2 * (frame.Definition.Type is ChartType.Doughnut ? 0.75 : 0.62);
        var start = -Math.PI / 2;
        for (var index = 0; index < series.Values.Count; index++)
        {
            if (!double.IsFinite(series.Values[index])) continue;
            var sweep = Math.Abs(series.Values[index]) / total * Math.PI * 2;
            if (sweep > 0) yield return new ChartDataLabel(Polar(frame.Plot.Center, radius, start + sweep / 2), series.Values[index]);
            start += sweep;
        }
    }

    private IBrush SliceBrush(int index) => new SolidColorBrush(Slice(index));

    private Color Slice(int index) => ChartColours.Resolve(null, index);

    public override void Draw(ChartFrame frame)
    {
        var series = frame.Data.Series.FirstOrDefault();
        if (series is null) return;

        var total = series.Values.Where(double.IsFinite).Sum(Math.Abs);
        if (total <= 0) return;

        var exploded = frame.Definition.Type is ChartType.ExplodedPie;
        var radius = Math.Min(frame.Plot.Width, frame.Plot.Height) / 2 - (exploded ? 10 : 4);
        var centre = frame.Plot.Center;

        var start = -Math.PI / 2;

        for (var index = 0; index < series.Values.Count; index++)
        {
            if (!double.IsFinite(series.Values[index])) continue;

            var sweep = Math.Abs(series.Values[index]) / total * Math.PI * 2;
            if (sweep <= 0) continue;

            var offset = exploded ? Polar(new Point(0, 0), 8, start + sweep / 2) : new Point(0, 0);
            var middle = new Point(centre.X + offset.X, centre.Y + offset.Y);

            if (sweep >= Math.PI * 2 - 1e-9)
            {
                frame.Context.DrawEllipse(SliceBrush(index), new Pen(Brushes.White, 1), middle, radius, radius);
                start += sweep;
                continue;
            }

            var geometry = new StreamGeometry();

            using (var sink = geometry.Open())
            {
                sink.BeginFigure(middle, true);
                sink.LineTo(Polar(middle, radius, start));

                sink.ArcTo(Polar(middle, radius, start + sweep),
                    new Size(radius, radius), 0, sweep > Math.PI, SweepDirection.Clockwise);

                sink.LineTo(middle);
                sink.EndFigure(true);
            }

            frame.Context.DrawGeometry(
                SliceBrush(index), new Pen(Brushes.White, 1), geometry);

            start += sweep;
        }

        if (frame.Definition.Type is ChartType.Doughnut)
            frame.Context.DrawEllipse(Brushes.White, null, centre, radius * 0.5, radius * 0.5);
    }

}
