namespace PulseOffice.Charts;

public interface IChartAxisMarks
{
    double ValueCrossing(PlotScale scale, ChartAxisScale axis);

    double CategoryCrossing(int count, ChartAxisScale categoryAxis);

    double CategoryAxisLine(PlotScale values, ChartAxisScale valueAxis, double edge);

    IReadOnlyList<double> MinorTicks(PlotScale scale, ChartAxisScale axis, int most = 200);
}

public sealed class ChartAxisMarks : IChartAxisMarks
{
    public double ValueCrossing(PlotScale scale, ChartAxisScale axis)
    {
        ArgumentNullException.ThrowIfNull(scale);
        ArgumentNullException.ThrowIfNull(axis);
        var low = Math.Min(scale.Minimum, scale.Maximum);
        var high = Math.Max(scale.Minimum, scale.Maximum);
        return axis.Crossing switch
        {
            AxisCrossing.Minimum => low,
            AxisCrossing.Maximum => high,
            AxisCrossing.Value when double.IsFinite(axis.CrossingValue) => Math.Clamp(axis.CrossingValue, low, high),
            _ => Math.Clamp(0, low, high),
        };
    }

    public double CategoryAxisLine(PlotScale values, ChartAxisScale valueAxis, double edge)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(valueAxis);
        return valueAxis.Crossing == AxisCrossing.Automatic ? edge : values.Position(ValueCrossing(values, valueAxis));
    }

    public double CategoryCrossing(int count, ChartAxisScale categoryAxis)
    {
        ArgumentNullException.ThrowIfNull(categoryAxis);
        var categories = Math.Max(count, 1);
        return categoryAxis.Crossing switch
        {
            AxisCrossing.Maximum => categories,
            AxisCrossing.Value when double.IsFinite(categoryAxis.CrossingValue) => Math.Clamp(categoryAxis.CrossingValue - 1, 0, categories),
            _ => 0,
        };
    }

    public IReadOnlyList<double> MinorTicks(PlotScale scale, ChartAxisScale axis, int most = 200)
    {
        ArgumentNullException.ThrowIfNull(scale);
        ArgumentNullException.ThrowIfNull(axis);
        if (axis.MinorUnit is not { } unit || !(unit > 0) || !double.IsFinite(unit) || !(scale.Maximum > scale.Minimum)) return [];
        if (scale.Logarithmic) return [];
        if ((scale.Maximum - scale.Minimum) / unit > most) return [];

        var majors = scale.Divisions();
        var ticks = new List<double>();
        var first = Math.Ceiling(scale.Minimum / unit - 1e-9) * unit;
        for (var tick = first; tick <= scale.Maximum + unit * 1e-9; tick += unit)
        {
            var value = Math.Abs(tick) < unit * 1e-9 ? 0 : tick;
            if (majors.Any(major => Math.Abs(major - value) < unit * 1e-6)) continue;
            ticks.Add(value);
        }
        return ticks;
    }
}
