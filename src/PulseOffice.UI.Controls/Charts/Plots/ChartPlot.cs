using Avalonia;
using Avalonia.Media;
using PulseOffice.Charts;

namespace PulseOffice.UI.Controls.Charts.Plots;

public sealed record ChartFrame(
    DrawingContext Context, ChartDefinition Definition, ChartData Data, Rect Plot, Rect Bounds,
    System.Globalization.CultureInfo? Culture = null)
{
    public System.Globalization.CultureInfo Formatting => Culture ?? System.Globalization.CultureInfo.InvariantCulture;
}

public sealed record ChartLegendItem(string Name, Color Colour);

public sealed record ChartDataLabel(Point Anchor, double Value);

public interface IChartPlot
{
    IReadOnlyList<ChartLegendItem> Legend(ChartDefinition definition, ChartData data);

    bool HasCategoryAxis(ChartType type);

    PlotScale? CategoryValueAxis(ChartFrame frame);

    IEnumerable<ChartDataLabel> DataLabels(ChartFrame frame);

    bool Handles(ChartType type);

    bool DrawsAxes { get; }

    PlotScale? ValueAxis(ChartFrame frame);

    bool HorizontalAxis(ChartType type);

    bool KeepsGaps { get; }

    void Draw(ChartFrame frame);
}

public abstract class ChartPlotBase : IChartPlot
{
    public abstract bool Handles(ChartType type);

    public virtual bool DrawsAxes => true;

    public virtual PlotScale? ValueAxis(ChartFrame frame) => null;

    public virtual bool HorizontalAxis(ChartType type) => false;

    public virtual bool KeepsGaps => false;

    public virtual bool HasCategoryAxis(ChartType type) => DrawsAxes;

    public virtual PlotScale? CategoryValueAxis(ChartFrame frame) => null;

    public virtual IEnumerable<ChartDataLabel> DataLabels(ChartFrame frame) => [];

    public virtual IReadOnlyList<ChartLegendItem> Legend(ChartDefinition definition, ChartData data) =>
        [.. data.Series.Select((series, index) => new ChartLegendItem(series.Name, Colour(data, index)))];

    public abstract void Draw(ChartFrame frame);

    protected bool IsPlotted(ChartSeries series, int index) =>
        index >= 0 && index < series.Values.Count && double.IsFinite(series.Values[index]);

    protected Point Polar(Point centre, double radius, double angle) =>
        new(centre.X + radius * Math.Cos(angle), centre.Y + radius * Math.Sin(angle));

    protected IBrush Brush(ChartData data, int index, double opacity = 1) =>
        new SolidColorBrush(Colour(data, index), opacity);

    protected Color Colour(ChartData data, int index) =>
        ChartColours.Resolve(index < data.Series.Count ? data.Series[index].ColorHex : null, index);

    protected PlotScale ValueScale(ChartData data, Rect plot, bool horizontal = false)
    {
        var values = data.Series.SelectMany(series => series.Values).Where(double.IsFinite).ToList();

        var minimum = values.Count == 0 ? 0 : Math.Min(0, values.Min());
        var maximum = values.Count == 0 ? 1 : Math.Max(0, values.Max());

        return horizontal
            ? PlotScale.Nice(minimum, maximum, plot.X, plot.Right)
            : PlotScale.Nice(minimum, maximum, plot.Bottom, plot.Y);
    }

    protected CategoryScale Categories(ChartData data, Rect plot, bool horizontal = false)
    {
        var count = Math.Max(
            data.Categories.Count,
            data.Series.Count == 0 ? 0 : data.Series.Max(series => series.Values.Count));

        return horizontal
            ? new CategoryScale(Math.Max(count, 1), plot.Y, plot.Bottom, Reversed: true)
            : new CategoryScale(Math.Max(count, 1), plot.X, plot.Right);
    }

    protected void Label(ChartFrame frame, string text, Point origin, double size = 9)
    {
        var formatted = new FormattedText(
            text, frame.Formatting, FlowDirection.LeftToRight,
            Typeface.Default, size, Brushes.Gray);

        frame.Context.DrawText(formatted, origin);
    }

    protected double Value(ChartSeries series, int index) =>
        index >= 0 && index < series.Values.Count && double.IsFinite(series.Values[index]) ? series.Values[index] : 0;
}

public static class ChartColours
{
    public static Color Resolve(string? hex, int index) =>
        hex is not null && Color.TryParse(hex, out var colour) ? colour : Color.Parse(ChartPalette.For(index));
}
