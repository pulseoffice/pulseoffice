using Avalonia;
using Avalonia.Media;
using PulseOffice.Charts;

namespace PulseOffice.UI.Controls.Charts.Plots;

public sealed class HierarchyPlot : ChartPlotBase
{
    public override bool Handles(ChartType type) => type is
        ChartType.Treemap or ChartType.Funnel or ChartType.Sunburst or ChartType.Sankey;

    public override bool DrawsAxes => false;

    public override void Draw(ChartFrame frame)
    {
        var values = Slice(frame);
        if (values.Count == 0) return;

        switch (frame.Definition.Type)
        {
            case ChartType.Funnel:
                DrawFunnel(frame, values);
                break;

            case ChartType.Sunburst:
                DrawSunburst(frame, values);
                break;

            default:
                DrawTreemap(frame, values, frame.Plot);
                break;
        }
    }

    private List<(double Value, int Index)> Slice(ChartFrame frame) =>
        [.. (frame.Data.Series.FirstOrDefault()?.Values ?? [])
            .Select((value, index) => (Value: double.IsFinite(value) ? Math.Abs(value) : 0, Index: index))
            .Where(entry => entry.Value > 0)];

    private void DrawTreemap(ChartFrame frame, IReadOnlyList<(double Value, int Index)> values, Rect area)
    {
        var total = values.Sum(entry => entry.Value);
        if (total <= 0) return;

        var remaining = area;
        var left = total;

        for (var index = 0; index < values.Count; index++)
        {
            var fraction = values[index].Value / left;

            var horizontal = remaining.Width >= remaining.Height;

            var slice = horizontal
                ? new Rect(remaining.X, remaining.Y, remaining.Width * fraction, remaining.Height)
                : new Rect(remaining.X, remaining.Y, remaining.Width, remaining.Height * fraction);

            if (index == values.Count - 1) slice = remaining;

            frame.Context.DrawRectangle(
                Brush(frame.Data, values[index].Index, 0.85), new Pen(Brushes.White, 1), slice.Deflate(1));

            var source = values[index].Index;
            if (source < frame.Data.Categories.Count && slice.Width > 40 && slice.Height > 18)
                Label(frame, frame.Data.Categories[source],
                    new Point(slice.X + 4, slice.Y + 4), 10);

            remaining = horizontal
                ? new Rect(slice.Right, remaining.Y, remaining.Width - slice.Width, remaining.Height)
                : new Rect(remaining.X, slice.Bottom, remaining.Width, remaining.Height - slice.Height);

            left -= values[index].Value;
        }
    }

    private void DrawFunnel(ChartFrame frame, IReadOnlyList<(double Value, int Index)> values)
    {
        var largest = values.Max(entry => entry.Value);
        if (largest <= 0) return;

        var height = frame.Plot.Height / values.Count;

        for (var index = 0; index < values.Count; index++)
        {
            var width = frame.Plot.Width * (values[index].Value / largest);

            frame.Context.FillRectangle(
                Brush(frame.Data, values[index].Index, 0.85),
                new Rect(
                    frame.Plot.Center.X - width / 2,
                    frame.Plot.Y + height * index + 2,
                    width,
                    Math.Max(1, height - 4)));
        }
    }

    private void DrawSunburst(ChartFrame frame, IReadOnlyList<(double Value, int Index)> values)
    {
        var total = values.Sum(entry => entry.Value);
        if (total <= 0) return;

        var centre = frame.Plot.Center;
        var radius = Math.Min(frame.Plot.Width, frame.Plot.Height) / 2 * 0.9;
        var inner = radius * 0.4;

        var start = -Math.PI / 2;

        for (var index = 0; index < values.Count; index++)
        {
            var sweep = 2 * Math.PI * values[index].Value / total;
            var brush = Brush(frame.Data, values[index].Index, 0.9);

            if (sweep >= 2 * Math.PI - 1e-9)
            {
                var ring = new EllipseGeometry(new Rect(centre.X - radius, centre.Y - radius, radius * 2, radius * 2));
                var hole = new EllipseGeometry(new Rect(centre.X - inner, centre.Y - inner, inner * 2, inner * 2));
                frame.Context.DrawGeometry(brush, new Pen(Brushes.White, 1),
                    new CombinedGeometry(GeometryCombineMode.Exclude, ring, hole));
                start += sweep;
                continue;
            }

            var geometry = new StreamGeometry();

            using (var sink = geometry.Open())
            {
                sink.BeginFigure(Polar(centre, inner, start), true);
                sink.LineTo(Polar(centre, radius, start));

                sink.ArcTo(Polar(centre, radius, start + sweep),
                    new Size(radius, radius), 0, sweep > Math.PI, SweepDirection.Clockwise);

                sink.LineTo(Polar(centre, inner, start + sweep));

                sink.ArcTo(Polar(centre, inner, start),
                    new Size(inner, inner), 0, sweep > Math.PI, SweepDirection.CounterClockwise);

                sink.EndFigure(true);
            }

            frame.Context.DrawGeometry(brush, new Pen(Brushes.White, 1), geometry);

            start += sweep;
        }
    }

}
