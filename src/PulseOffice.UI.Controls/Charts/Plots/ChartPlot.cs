using Avalonia;
using Avalonia.Media;
using PulseOffice.Charts;

namespace PulseOffice.UI.Controls.Charts.Plots;

public sealed record ChartFrame(
    DrawingContext Context, ChartDefinition Definition, ChartData Data, Rect Plot, Rect Bounds,
    System.Globalization.CultureInfo? Culture = null, IBrush? Ink = null, ChartInk? Inks = null)
{
    public ChartInk Marks => Inks ?? ChartInk.Print;

    public IBrush LabelInk => Ink ?? Brushes.Gray;

    public System.Globalization.CultureInfo Formatting => Culture ?? System.Globalization.CultureInfo.InvariantCulture;
}

public sealed record ChartLegendItem(string Name, Color Colour);

public sealed record ChartDataLabel(Point Anchor, double Value)
{
    public int Series { get; init; } = -1;

    public int Index { get; init; } = -1;

    public double Across { get; init; } = double.NaN;

    public ChartLabelPlacement Placement { get; init; } = ChartLabelPlacement.Above;

    public Point? Middle { get; init; }

    public Point? Base { get; init; }

    public Point? Tip { get; init; }
}

public enum ChartLabelPlacement
{
    Above,
    Below,
    After,
    Before,
    Centre,
}

public sealed class ChartLabelLayout
{
    private const double Gap = 2;

    public Point Origin(ChartDataLabel label, Size size, DataLabelPlacement requested = DataLabelPlacement.Automatic)
    {
        ArgumentNullException.ThrowIfNull(label);
        var tip = label.Tip ?? label.Anchor;
        return requested switch
        {
            DataLabelPlacement.Center => Centred(label.Middle ?? (label.Base is { } start ? new Point((start.X + tip.X) / 2, (start.Y + tip.Y) / 2) : label.Anchor), size),
            DataLabelPlacement.Above => Origin(label with { Placement = ChartLabelPlacement.Above }, size),
            DataLabelPlacement.Below => Origin(label with { Placement = ChartLabelPlacement.Below }, size),
            DataLabelPlacement.Left => Origin(label with { Placement = ChartLabelPlacement.Before }, size),
            DataLabelPlacement.Right => Origin(label with { Placement = ChartLabelPlacement.After }, size),
            DataLabelPlacement.OutsideEnd when label.Base is { } start => Along(tip, start, tip, size, 1),
            DataLabelPlacement.InsideEnd when label.Base is { } start => Along(tip, start, tip, size, -1),
            DataLabelPlacement.InsideBase when label.Base is { } start => Along(start, start, tip, size, 1),
            _ => Origin(label, size),
        };
    }

    public Point BestFit(ChartDataLabel label, Size size, IReadOnlyList<Rect> placed)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(placed);
        DataLabelPlacement[] candidates = label.Base is null
            ? [DataLabelPlacement.Automatic, DataLabelPlacement.Above, DataLabelPlacement.Right, DataLabelPlacement.Below, DataLabelPlacement.Left]
            : [DataLabelPlacement.Automatic, DataLabelPlacement.OutsideEnd, DataLabelPlacement.InsideEnd, DataLabelPlacement.Center, DataLabelPlacement.InsideBase];
        var first = Origin(label, size, candidates[0]);
        foreach (var candidate in candidates)
        {
            var origin = Origin(label, size, candidate);
            var area = new Rect(origin, size);
            if (!placed.Any(other => other.Intersects(area))) return origin;
        }
        return first;
    }

    public Point Origin(ChartDataLabel label, Size size) => label.Placement switch
    {
        ChartLabelPlacement.Below => new Point(label.Anchor.X - size.Width / 2, label.Anchor.Y + Gap),
        ChartLabelPlacement.After => new Point(label.Anchor.X + Gap, label.Anchor.Y - size.Height / 2),
        ChartLabelPlacement.Before => new Point(label.Anchor.X - size.Width - Gap, label.Anchor.Y - size.Height / 2),
        ChartLabelPlacement.Centre => Centred(label.Middle ?? label.Anchor, size),
        _ => new Point(label.Anchor.X - size.Width / 2, label.Anchor.Y - size.Height - Gap),
    };

    private static Point Centred(Point centre, Size size) => new(centre.X - size.Width / 2, centre.Y - size.Height / 2);

    private static Point Along(Point from, Point start, Point tip, Size size, int sign)
    {
        var dx = tip.X - start.X;
        var dy = tip.Y - start.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length < 1e-9) return Centred(from, size);
        dx /= length;
        dy /= length;
        var extent = Math.Abs(dx) * size.Width / 2 + Math.Abs(dy) * size.Height / 2 + Gap;
        return Centred(new Point(from.X + sign * dx * extent, from.Y + sign * dy * extent), size);
    }
}

public interface IChartPlot
{
    IReadOnlyList<ChartLegendItem> Legend(ChartDefinition definition, ChartData data);

    bool HasCategoryAxis(ChartType type);

    PlotScale? CategoryValueAxis(ChartFrame frame);

    IEnumerable<ChartDataLabel> DataLabels(ChartFrame frame);

    bool Handles(ChartType type);

    bool DrawsAxes { get; }

    bool ScalesValues { get; }

    PlotScale? ValueAxis(ChartFrame frame);

    PlotScale? SecondaryValueAxis(ChartFrame frame);

    PlotScale? SeriesScale(ChartFrame frame, int series);

    bool HorizontalAxis(ChartType type);

    bool DrawsReversedCategories { get; }

    bool KeepsGaps { get; }

    void Draw(ChartFrame frame);
}

public abstract class ChartPlotBase : IChartPlot
{
    public abstract bool Handles(ChartType type);

    public virtual bool DrawsAxes => true;

    public virtual bool ScalesValues => DrawsAxes;

    public virtual PlotScale? ValueAxis(ChartFrame frame) => null;

    public virtual PlotScale? SecondaryValueAxis(ChartFrame frame) => null;

    public virtual PlotScale? SeriesScale(ChartFrame frame, int series) => null;

    private readonly ChartAxisScaling _axisScaling = new();

    protected PlotScale Scaled(PlotScale automatic, ChartAxisScale axis) => _axisScaling.Apply(automatic, axis);

    protected ChartData Side(ChartFrame frame, ChartData data, bool secondary, bool split) =>
        split && frame.Definition.HasSecondaryAxis
            ? data with { Series = [.. data.Series.Select((series, index) => frame.Definition.SeriesFormat(index).SecondaryAxis == secondary ? series : series with { Values = [] })] }
            : data;

    protected PlotScale PrimaryScale(ChartFrame frame, ChartData data, bool split, bool horizontal = false) =>
        Scaled(ValueScale(Side(frame, data, false, split), frame.Plot, horizontal), frame.Definition.ValueAxis);

    protected PlotScale? SecondaryScale(ChartFrame frame, ChartData data, bool split, bool horizontal = false) =>
        split && frame.Definition.HasSecondaryAxis ? Scaled(ValueScale(Side(frame, data, true, split), frame.Plot, horizontal), frame.Definition.SecondaryValueAxis) : null;

    protected PlotScale ScaleOf(ChartFrame frame, int series, PlotScale primary, PlotScale? secondary) =>
        secondary is not null && frame.Definition.SeriesFormat(series).SecondaryAxis ? secondary : primary;

    public virtual bool HorizontalAxis(ChartType type) => false;

    public virtual bool KeepsGaps => false;

    public virtual bool DrawsReversedCategories => false;

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

    protected IBrush PointBrush(ChartDefinition definition, ChartData data, int series, int point, double opacity = 1) =>
        definition.PointFormat(series, point).ColorHex is { } hex && Color.TryParse(hex, out var colour)
            ? new SolidColorBrush(colour, opacity)
            : Brush(data, series, opacity);

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

    protected CategoryScale Categories(ChartFrame frame, ChartData data, bool horizontal = false)
    {
        var plot = frame.Plot;
        var reversed = DrawsReversedCategories && frame.Definition.CategoryAxis.Reversed;
        var count = Math.Max(
            data.Categories.Count,
            data.Series.Count == 0 ? 0 : data.Series.Max(series => series.Values.Count));

        return horizontal
            ? new CategoryScale(Math.Max(count, 1), plot.Y, plot.Bottom, Reversed: !reversed)
            : new CategoryScale(Math.Max(count, 1), plot.X, plot.Right, Reversed: reversed);
    }

    protected CategoryScale Across(ChartFrame frame, int count) =>
        new(Math.Max(1, count), frame.Plot.X, frame.Plot.Right, Reversed: frame.Definition.CategoryAxis.Reversed);

    protected void Label(ChartFrame frame, string text, Point origin, double size = 9)
    {
        var formatted = new FormattedText(
            text, frame.Formatting, FlowDirection.LeftToRight,
            Typeface.Default, size, frame.LabelInk);

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
