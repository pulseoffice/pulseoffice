using Avalonia;
using Avalonia.Media;
using PulseOffice.Charts;

namespace PulseOffice.UI.Controls.Charts.Plots;

public sealed class MeterPlot : ChartPlotBase
{
    public override bool Handles(ChartType type) => type is
        ChartType.Gauge or ChartType.Bullet or ChartType.Gantt or ChartType.Heatmap
        or ChartType.Contour or ChartType.Surface or ChartType.Surface3D;

    public override bool DrawsAxes => false;

    public override void Draw(ChartFrame frame)
    {
        switch (frame.Definition.Type)
        {
            case ChartType.Gauge:
                DrawGauge(frame);
                break;

            case ChartType.Bullet:
                DrawBullet(frame);
                break;

            case ChartType.Gantt:
                DrawGantt(frame);
                break;

            default:
                DrawHeatmap(frame);
                break;
        }
    }

    private void DrawGauge(ChartFrame frame)
    {
        var series = frame.Data.Series.FirstOrDefault();
        if (series is null || series.Values.Count == 0) return;

        var value = Value(series, 0);
        var maximum = frame.Data.Series.Count > 1 && frame.Data.Series[1].Values.Count > 0
            ? Value(frame.Data.Series[1], 0)
            : Math.Max(value, 1);

        var centre = new Point(frame.Plot.Center.X, frame.Plot.Bottom - frame.Plot.Height * 0.15);
        var radius = Math.Min(frame.Plot.Width / 2, frame.Plot.Height) * 0.8;

        var background = new Pen(new SolidColorBrush(Colors.Gray, 0.25), radius * 0.28)
        {
            LineCap = PenLineCap.Round,
        };

        frame.Context.DrawGeometry(null, background, Arc(centre, radius * 0.8, Math.PI, Math.PI));

        var fraction = maximum <= 0 ? 0 : Math.Clamp(value / maximum, 0, 1);

        var foreground = new Pen(Brush(frame.Data, 0), radius * 0.28) { LineCap = PenLineCap.Round };

        if (fraction > 0)
            frame.Context.DrawGeometry(
                null, foreground, Arc(centre, radius * 0.8, Math.PI, Math.PI * fraction));

        Label(frame, value.ToString("0.##", frame.Formatting),
            new Point(centre.X - 14, centre.Y - radius * 0.3), 14);
    }

    private void DrawBullet(ChartFrame frame)
    {
        var series = frame.Data.Series;
        if (series.Count == 0 || series[0].Values.Count == 0) return;

        var value = Math.Max(0, Value(series[0], 0));
        var target = Math.Max(0, series.Count > 1 && series[1].Values.Count > 0 ? Value(series[1], 0) : value * 1.2);
        var maximum = Math.Max(value, target) * 1.15;

        var scale = new PlotScale(0, maximum <= 0 ? 1 : maximum, frame.Plot.X, frame.Plot.Right);
        var middle = frame.Plot.Center.Y;
        var height = Math.Min(frame.Plot.Height * 0.35, 30);

        frame.Context.FillRectangle(
            new SolidColorBrush(Colors.Gray, 0.2),
            new Rect(frame.Plot.X, middle - height / 2, frame.Plot.Width, height));

        frame.Context.FillRectangle(
            Brush(frame.Data, 0),
            new Rect(frame.Plot.X, middle - height / 4, scale.Position(value) - frame.Plot.X, height / 2));

        frame.Context.DrawLine(
            new Pen(Brushes.Black, 2),
            new Point(scale.Position(target), middle - height / 2),
            new Point(scale.Position(target), middle + height / 2));
    }

    private void DrawGantt(ChartFrame frame)
    {
        var series = frame.Data.Series;
        if (series.Count < 2) return;

        var starts = series[0].Values;
        var lengths = series[1].Values;

        var finish = starts.Zip(lengths, (start, length) => start + length).Where(double.IsFinite).DefaultIfEmpty(1).Max();
        var scale = PlotScale.Nice(starts.Where(double.IsFinite).DefaultIfEmpty(0).Min(), finish, frame.Plot.X, frame.Plot.Right);

        var rows = new CategoryScale(Math.Max(1, starts.Count), frame.Plot.Y, frame.Plot.Bottom);

        for (var index = 0; index < starts.Count && index < lengths.Count; index++)
        {
            var first = scale.Position(starts[index]);
            var second = scale.Position(starts[index] + lengths[index]);
            var from = Math.Min(first, second);
            var to = Math.Max(first, second);

            frame.Context.FillRectangle(
                Brush(frame.Data, index, 0.85),
                new Rect(from, rows.Start(index) + rows.Width * 0.2,
                    Math.Max(2, to - from), rows.Width * 0.6));
        }
    }

    private void DrawHeatmap(ChartFrame frame)
    {
        var series = frame.Data.Series;
        if (series.Count == 0) return;

        var values = series.SelectMany(row => row.Values).Where(double.IsFinite).ToList();
        if (values.Count == 0) return;

        var minimum = values.Min();
        var maximum = values.Max();
        var range = maximum - minimum;

        var columns = series.Max(row => row.Values.Count);

        var cellWidth = frame.Plot.Width / Math.Max(1, columns);
        var cellHeight = frame.Plot.Height / series.Count;

        for (var row = 0; row < series.Count; row++)
            for (var column = 0; column < series[row].Values.Count; column++)
            {
                var cell = series[row].Values[column];
                if (!double.IsFinite(cell)) continue;

                var fraction = range <= 0 ? 0.5 : (cell - minimum) / range;

                frame.Context.FillRectangle(
                    new SolidColorBrush(Gradient(fraction)),
                    new Rect(
                        frame.Plot.X + cellWidth * column,
                        frame.Plot.Y + cellHeight * row,
                        cellWidth,
                        cellHeight));
            }
    }

    private Color Gradient(double fraction)
    {
        var cold = Color.Parse("#1D4ED8");
        var warm = Color.Parse("#DC2626");

        byte Mix(byte from, byte to) => (byte)(from + (to - from) * Math.Clamp(fraction, 0, 1));

        return Color.FromRgb(Mix(cold.R, warm.R), Mix(cold.G, warm.G), Mix(cold.B, warm.B));
    }

    private StreamGeometry Arc(Point centre, double radius, double start, double sweep)
    {
        var geometry = new StreamGeometry();

        using var sink = geometry.Open();

        var from = new Point(centre.X + radius * Math.Cos(start), centre.Y + radius * Math.Sin(start));
        var to = new Point(
            centre.X + radius * Math.Cos(start + sweep), centre.Y + radius * Math.Sin(start + sweep));

        sink.BeginFigure(from, false);
        sink.ArcTo(to, new Size(radius, radius), 0, sweep > Math.PI, SweepDirection.Clockwise);
        sink.EndFigure(false);

        return geometry;
    }
}
