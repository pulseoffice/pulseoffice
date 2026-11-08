using Avalonia;
using Avalonia.Media;
using PulseOffice.Charts;

namespace PulseOffice.UI.Controls.Charts.Plots;

public sealed class FinancialPlot : ChartPlotBase
{
    private static readonly Color Rising = Color.Parse("#16A34A");
    private static readonly Color Falling = Color.Parse("#DC2626");

    public override bool Handles(ChartType type) => type is
        ChartType.StockHighLowClose or ChartType.StockOpenHighLowClose or ChartType.Candlestick;

    public override PlotScale? ValueAxis(ChartFrame frame)
    {
        var withOpen = frame.Definition.Type is not ChartType.StockHighLowClose;
        if (frame.Data.Series.Count < (withOpen ? 4 : 3)) return null;

        var series = frame.Data.Series;
        var everything = series[withOpen ? 1 : 0].Values.Concat(series[withOpen ? 2 : 1].Values)
            .Concat(series[withOpen ? 3 : 2].Values).ToList();

        return everything.Count == 0
            ? null
            : PlotScale.Nice(everything.Min(), everything.Max(), frame.Plot.Bottom, frame.Plot.Y);
    }

    public override void Draw(ChartFrame frame)
    {
        var withOpen = frame.Definition.Type is not ChartType.StockHighLowClose;
        var needed = withOpen ? 4 : 3;

        if (frame.Data.Series.Count < needed) return;

        var series = frame.Data.Series;

        var open = withOpen ? series[0].Values : [];
        var high = series[withOpen ? 1 : 0].Values;
        var low = series[withOpen ? 2 : 1].Values;
        var close = series[withOpen ? 3 : 2].Values;

        if (ValueAxis(frame) is not { } scale) return;

        var count = new[] { high.Count, low.Count, close.Count }.Min();
        var categories = new CategoryScale(Math.Max(1, count), frame.Plot.X, frame.Plot.Right);

        for (var index = 0; index < count; index++)
        {
            var centre = categories.Centre(index);
            var width = categories.Width * 0.5;

            var first = withOpen && index < open.Count ? open[index] : close[index];
            var rising = close[index] >= first;

            var colour = new SolidColorBrush(rising ? Rising : Falling);
            var pen = new Pen(colour, 1.4);

            frame.Context.DrawLine(pen,
                new Point(centre, scale.Position(high[index])),
                new Point(centre, scale.Position(low[index])));

            if (frame.Definition.Type is ChartType.Candlestick && withOpen)
            {
                var top = Math.Min(scale.Position(first), scale.Position(close[index]));
                var height = Math.Max(1, Math.Abs(scale.Position(close[index]) - scale.Position(first)));

                var body = new Rect(centre - width / 2, top, width, height);

                frame.Context.DrawRectangle(rising ? null : colour, pen, body);
                if (rising) frame.Context.DrawRectangle(new SolidColorBrush(Rising, 0.25), pen, body);

                continue;
            }

            if (withOpen && index < open.Count)
                frame.Context.DrawLine(pen,
                    new Point(centre - width / 2, scale.Position(open[index])),
                    new Point(centre, scale.Position(open[index])));

            frame.Context.DrawLine(pen,
                new Point(centre, scale.Position(close[index])),
                new Point(centre + width / 2, scale.Position(close[index])));
        }
    }
}
