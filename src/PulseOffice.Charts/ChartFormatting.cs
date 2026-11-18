namespace PulseOffice.Charts;

public enum AxisCrossing { Automatic, Minimum, Maximum, Value }

public sealed record ChartAxisScale
{
    public static readonly ChartAxisScale Automatic = new();

    public double? Minimum { get; init; }

    public double? Maximum { get; init; }

    public double? MajorUnit { get; init; }

    public double? MinorUnit { get; init; }

    public bool Logarithmic { get; init; }

    public double LogBase { get; init; } = 10;

    public bool Reversed { get; init; }

    public AxisCrossing Crossing { get; init; }

    public double CrossingValue { get; init; }

    public string? NumberFormat { get; init; }

    public bool IsAutomatic => this == Automatic;

    public double EffectiveLogBase => LogBase > 1 && double.IsFinite(LogBase) ? LogBase : 10;
}

public enum TrendlineKind { Linear, Exponential, Logarithmic, Polynomial, Power, MovingAverage }

public sealed record ChartTrendline(TrendlineKind Kind)
{
    public int Order { get; init; } = 2;

    public int Period { get; init; } = 2;

    public double Forward { get; init; }

    public double Backward { get; init; }

    public double? Intercept { get; init; }

    public bool DisplayEquation { get; init; }

    public bool DisplayRSquared { get; init; }

    public string? Name { get; init; }

    public int EffectiveOrder => Math.Clamp(Order, 2, 6);

    public int EffectivePeriod => Math.Max(2, Period);
}

public enum ErrorBarKind { FixedValue, Percentage, StandardDeviation, StandardError }

public enum ErrorBarDirection { Both, Plus, Minus }

public sealed record ChartErrorBars(ErrorBarKind Kind, double Value)
{
    public ErrorBarDirection Direction { get; init; }

    public bool EndCaps { get; init; } = true;
}

public enum MarkerSymbol { Automatic, None, Circle, Square, Diamond, Triangle, Cross, Star, Plus, Dash, Dot }

public enum LineDash { Solid, Dash, Dot, DashDot }

public sealed record ChartSeriesFormat
{
    public static readonly ChartSeriesFormat Plain = new();

    public double? LineWidthPoints { get; init; }

    public LineDash Dash { get; init; }

    public MarkerSymbol Marker { get; init; }

    public int? MarkerSize { get; init; }

    public bool SecondaryAxis { get; init; }

    public ChartTrendline? Trendline { get; init; }

    public ChartErrorBars? ErrorBars { get; init; }

    public bool IsPlain => this == Plain;
}

public enum DataLabelPlacement { Automatic, Center, InsideEnd, InsideBase, OutsideEnd, Above, Below, Left, Right }

public sealed record ChartDataLabelOptions
{
    public static readonly ChartDataLabelOptions ValuesOnly = new();

    public bool ShowValue { get; init; } = true;

    public bool ShowCategory { get; init; }

    public bool ShowSeriesName { get; init; }

    public bool ShowPercent { get; init; }

    public string? NumberFormat { get; init; }

    public DataLabelPlacement Placement { get; init; }

    public bool IsValuesOnly => this == ValuesOnly;
}

public interface IChartAxisScaling
{
    PlotScale Apply(PlotScale automatic, ChartAxisScale axis);
}

public sealed class ChartAxisScaling : IChartAxisScaling
{
    public PlotScale Apply(PlotScale automatic, ChartAxisScale axis)
    {
        ArgumentNullException.ThrowIfNull(automatic);
        ArgumentNullException.ThrowIfNull(axis);
        if (axis.IsAutomatic) return automatic;

        var from = axis.Reversed ? automatic.To : automatic.From;
        var to = axis.Reversed ? automatic.From : automatic.To;

        if (axis.Logarithmic)
        {
            var logBase = axis.EffectiveLogBase;
            var lowest = axis.Minimum is > 0 and var minimum ? minimum
                : automatic.Minimum > 0 ? Math.Pow(logBase, Math.Floor(Math.Log(automatic.Minimum, logBase))) : 1;
            var highest = axis.Maximum is { } maximum && maximum > lowest ? maximum
                : Math.Pow(logBase, Math.Ceiling(Math.Log(Math.Max(automatic.Maximum, lowest * logBase), logBase)));
            return new PlotScale(lowest, highest, from, to) { Logarithmic = true, LogBase = logBase, MajorStep = axis.MajorUnit };
        }

        var low = axis.Minimum ?? automatic.Minimum;
        var high = axis.Maximum ?? automatic.Maximum;
        if (!(high > low))
        {
            low = automatic.Minimum;
            high = automatic.Maximum;
        }
        return new PlotScale(low, high, from, to) { MajorStep = axis.MajorUnit };
    }
}
