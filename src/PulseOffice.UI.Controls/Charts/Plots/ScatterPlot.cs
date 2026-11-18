using Avalonia;
using Avalonia.Media;
using PulseOffice.Charts;

namespace PulseOffice.UI.Controls.Charts.Plots;

public sealed class ScatterPlot : ChartPlotBase
{
    private readonly ChartSeriesStroke _strokes = new();

    public override bool Handles(ChartType type) => type is
        ChartType.Scatter or ChartType.ScatterWithLines or ChartType.Bubble;

    public override bool KeepsGaps => true;

    public override PlotScale? ValueAxis(ChartFrame frame) => Layout(frame)?.Vertical;

    public override PlotScale? SeriesScale(ChartFrame frame, int series) => Layout(frame)?.Vertical;

    public override bool HasCategoryAxis(ChartType type) => false;

    public override PlotScale? CategoryValueAxis(ChartFrame frame) => Layout(frame)?.Horizontal;

    public override IReadOnlyList<ChartLegendItem> Legend(ChartDefinition definition, ChartData data) =>
        [.. data.Series.Skip(1).Take(definition.Type is ChartType.Bubble ? 1 : int.MaxValue)
            .Select((series, index) => new ChartLegendItem(series.Name, Colour(data, index + 1)))];

    public override IEnumerable<ChartDataLabel> DataLabels(ChartFrame frame)
    {
        if (Layout(frame) is not { } layout) yield break;
        var horizontalValues = frame.Data.Series[0].Values;
        for (var plotted = 0; plotted < layout.Plotted.Count; plotted++)
        {
            var series = layout.Plotted[plotted];
            for (var point = 0; point < series.Values.Count && point < horizontalValues.Count; point++)
                if (double.IsFinite(horizontalValues[point]) && double.IsFinite(series.Values[point]))
                    yield return new ChartDataLabel(new Point(layout.Horizontal.Position(horizontalValues[point]), layout.Vertical.Position(series.Values[point])), series.Values[point])
                    { Series = plotted + 1, Index = point, Across = horizontalValues[point] };
        }
    }

    public override void Draw(ChartFrame frame)
    {
        if (Layout(frame) is not { } layout) return;

        var horizontalValues = frame.Data.Series[0].Values;
        var bubble = frame.Definition.Type is ChartType.Bubble;
        var sizes = bubble && layout.Rest.Count > 1 ? layout.Rest[1].Values : [];
        var largest = sizes.Where(double.IsFinite).Select(Math.Abs).DefaultIfEmpty(0).Max();

        for (var index = 0; index < layout.Plotted.Count; index++)
        {
            var series = layout.Plotted[index];
            var format = frame.Definition.SeriesFormat(index + 1);
            var points = new List<(Point Point, int Index)>();

            for (var point = 0; point < series.Values.Count && point < horizontalValues.Count; point++)
            {
                if (!double.IsFinite(horizontalValues[point]) || !double.IsFinite(series.Values[point])) continue;

                points.Add((new Point(
                    layout.Horizontal.Position(horizontalValues[point]),
                    layout.Vertical.Position(series.Values[point])), point));
            }

            if (frame.Definition.Type is ChartType.ScatterWithLines && points.Count > 1)
            {
                var geometry = new StreamGeometry();

                using (var sink = geometry.Open())
                {
                    sink.BeginFigure(points[0].Point, false);
                    foreach (var next in points.Skip(1)) sink.LineTo(next.Point);
                    sink.EndFigure(false);
                }

                frame.Context.DrawGeometry(null, _strokes.Pen(Brush(frame.Data, index + 1), format, 1.6), geometry);
            }

            foreach (var (centre, source) in points)
            {
                var radius = 3.5;

                if (largest > 0 && source < sizes.Count && double.IsFinite(sizes[source]))
                    radius = 4 + 14 * Math.Sqrt(Math.Abs(sizes[source]) / largest);

                if (bubble || format.Marker == MarkerSymbol.Automatic) frame.Context.DrawEllipse(Brush(frame.Data, index + 1, 0.75), null, centre, radius, radius);
                else _strokes.Marker(frame.Context, Brush(frame.Data, index + 1, 0.75), format, centre, radius);
            }
        }
    }

    private ScatterLayout? Layout(ChartFrame frame)
    {
        if (frame.Data.Series.Count < 2) return null;

        var horizontalValues = frame.Data.Series[0].Values.Where(double.IsFinite).ToList();
        if (horizontalValues.Count == 0) return null;

        var rest = frame.Data.Series.Skip(1).ToList();
        var plotted = rest.Take(frame.Definition.Type is ChartType.Bubble ? 1 : rest.Count).ToList();
        var values = plotted.SelectMany(series => series.Values).Where(double.IsFinite).ToList();
        if (values.Count == 0) return null;

        return new ScatterLayout(
            Scaled(PlotScale.Nice(horizontalValues.Min(), horizontalValues.Max(), frame.Plot.X, frame.Plot.Right), frame.Definition.CategoryAxis),
            Scaled(PlotScale.Nice(values.Min(), values.Max(), frame.Plot.Bottom, frame.Plot.Y), frame.Definition.ValueAxis),
            rest,
            plotted);
    }

    private sealed record ScatterLayout(
        PlotScale Horizontal, PlotScale Vertical, IReadOnlyList<ChartSeries> Rest, IReadOnlyList<ChartSeries> Plotted);
}
