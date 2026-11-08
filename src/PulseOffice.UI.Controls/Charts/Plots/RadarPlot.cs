using Avalonia;
using Avalonia.Media;
using PulseOffice.Charts;

namespace PulseOffice.UI.Controls.Charts.Plots;

public sealed class RadarPlot : ChartPlotBase
{
    public override bool Handles(ChartType type) => type is
        ChartType.Radar or ChartType.FilledRadar or ChartType.Polar
        or ChartType.ParallelCoordinates;

    public override bool DrawsAxes => false;

    public override void Draw(ChartFrame frame)
    {
        if (frame.Definition.Type is ChartType.ParallelCoordinates)
        {
            DrawParallel(frame);
            return;
        }

        var count = Math.Max(
            frame.Data.Categories.Count,
            frame.Data.Series.Count == 0 ? 0 : frame.Data.Series.Max(series => series.Values.Count));

        if (count < 3) return;

        var centre = frame.Plot.Center;
        var radius = Math.Min(frame.Plot.Width, frame.Plot.Height) / 2 * 0.85;

        var values = frame.Data.Series.SelectMany(series => series.Values).Where(double.IsFinite).ToList();
        if (values.Count == 0) return;

        var scale = PlotScale.Nice(Math.Min(0, values.Min()), Math.Max(0, values.Max()), 0, radius);

        var web = new Pen(new SolidColorBrush(Colors.Gray, 0.35));

        for (var ring = 1; ring <= 4; ring++)
        {
            var geometry = Polygon(centre, radius * ring / 4, count);
            frame.Context.DrawGeometry(null, web, geometry);
        }

        for (var index = 0; index < count; index++)
            frame.Context.DrawLine(web, centre, Corner(centre, radius, index, count));

        foreach (var (series, order) in frame.Data.Series.Select((series, order) => (series, order)))
        {
            var geometry = new StreamGeometry();

            using (var sink = geometry.Open())
            {
                for (var index = 0; index < count; index++)
                {
                    var point = Corner(centre, scale.Position(Value(series, index)), index, count);

                    if (index == 0) sink.BeginFigure(point, frame.Definition.Type is ChartType.FilledRadar);
                    else sink.LineTo(point);
                }

                sink.EndFigure(true);
            }

            frame.Context.DrawGeometry(
                frame.Definition.Type is ChartType.FilledRadar ? Brush(frame.Data, order, 0.3) : null,
                new Pen(Brush(frame.Data, order), 2),
                geometry);
        }
    }

    private void DrawParallel(ChartFrame frame)
    {
        var axes = frame.Data.Series.Count;
        if (axes < 2) return;

        var points = frame.Data.Series.Max(series => series.Values.Count);
        var categories = new CategoryScale(axes, frame.Plot.X, frame.Plot.Right);

        var scales = frame.Data.Series
            .Select(series => PlotScale.Nice(
                series.Values.Where(double.IsFinite).DefaultIfEmpty(0).Min(),
                series.Values.Where(double.IsFinite).DefaultIfEmpty(1).Max(),
                frame.Plot.Bottom, frame.Plot.Y))
            .ToList();

        var web = new Pen(new SolidColorBrush(Colors.Gray, 0.4));

        for (var axis = 0; axis < axes; axis++)
            frame.Context.DrawLine(web,
                new Point(categories.Centre(axis), frame.Plot.Y),
                new Point(categories.Centre(axis), frame.Plot.Bottom));

        for (var index = 0; index < points; index++)
        {
            var geometry = new StreamGeometry();

            using (var sink = geometry.Open())
            {
                sink.BeginFigure(new Point(
                    categories.Centre(0), scales[0].Position(Value(frame.Data.Series[0], index))), false);

                for (var axis = 1; axis < axes; axis++)
                    sink.LineTo(new Point(
                        categories.Centre(axis),
                        scales[axis].Position(Value(frame.Data.Series[axis], index))));

                sink.EndFigure(false);
            }

            frame.Context.DrawGeometry(null, new Pen(Brush(frame.Data, index, 0.8), 1.4), geometry);
        }
    }

    private StreamGeometry Polygon(Point centre, double radius, int count)
    {
        var geometry = new StreamGeometry();

        using var sink = geometry.Open();

        sink.BeginFigure(Corner(centre, radius, 0, count), false);

        for (var index = 1; index < count; index++) sink.LineTo(Corner(centre, radius, index, count));

        sink.EndFigure(true);
        return geometry;
    }

    private Point Corner(Point centre, double radius, int index, int count)
    {
        var angle = -Math.PI / 2 + 2 * Math.PI * index / count;

        return new Point(centre.X + radius * Math.Cos(angle), centre.Y + radius * Math.Sin(angle));
    }
}
