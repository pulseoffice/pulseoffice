namespace PulseOffice.Charts;

public readonly record struct HistogramBin(double Lower, double Upper, int Count)
{
    public double Centre => (Lower + Upper) / 2;
}

public readonly record struct FiveNumbers(
    double Minimum, double LowerQuartile, double Median, double UpperQuartile, double Maximum)
{
    public double Range => Maximum - Minimum;

    public double InterQuartile => UpperQuartile - LowerQuartile;
}

public interface IChartStatistics
{
    IReadOnlyList<HistogramBin> Histogram(IReadOnlyList<double> values, int bins = 0);

    FiveNumbers Summary(IReadOnlyList<double> values);

    IReadOnlyList<double> Outliers(IReadOnlyList<double> values);

    IReadOnlyList<(double X, double Y)> Density(IReadOnlyList<double> values, int samples = 64);

    IReadOnlyList<(double X, double Y)> Cumulative(IReadOnlyList<double> values);

    IReadOnlyList<(double Theoretical, double Observed)> QuantilePairs(IReadOnlyList<double> values);

    IReadOnlyList<double> RunningTotals(IReadOnlyList<double> values);
}

public sealed class ChartStatistics : IChartStatistics
{
    private const double NominalBinWidth = 1;

    public IReadOnlyList<HistogramBin> Histogram(IReadOnlyList<double> values, int bins = 0)
    {
        ArgumentNullException.ThrowIfNull(values);

        var sorted = Clean(values);
        if (sorted.Count == 0) return [];

        var minimum = sorted[0];
        var maximum = sorted[^1];

        if (bins <= 0) bins = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(sorted.Count)));

        if (Math.Abs(maximum - minimum) < 1e-12)
            return [new HistogramBin(minimum - (NominalBinWidth / 2), minimum + (NominalBinWidth / 2), sorted.Count)];

        var halfWidth = (maximum / 2 - minimum / 2) / bins;
        var counts = new int[bins];

        foreach (var value in sorted)
        {
            var index = (int)((value / 2 - minimum / 2) / halfWidth);
            counts[Math.Clamp(index, 0, bins - 1)]++;
        }

        double Edge(int index) => index == bins ? maximum : minimum * ((double)(bins - index) / bins) + maximum * ((double)index / bins);

        return [.. counts.Select((count, index) => new HistogramBin(Edge(index), Edge(index + 1), count))];
    }

    public FiveNumbers Summary(IReadOnlyList<double> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var sorted = Clean(values);
        if (sorted.Count == 0) return default;

        return new FiveNumbers(
            sorted[0],
            Quantile(sorted, 0.25),
            Quantile(sorted, 0.5),
            Quantile(sorted, 0.75),
            sorted[^1]);
    }

    public IReadOnlyList<double> Outliers(IReadOnlyList<double> values)
    {
        var summary = Summary(values);
        if (summary.InterQuartile <= 0) return [];

        var low = summary.LowerQuartile - 1.5 * summary.InterQuartile;
        var high = summary.UpperQuartile + 1.5 * summary.InterQuartile;

        return [.. Clean(values).Where(value => value < low || value > high)];
    }

    public IReadOnlyList<(double X, double Y)> Density(IReadOnlyList<double> values, int samples = 64)
    {
        var sorted = Clean(values);
        if (sorted.Count < 2) return [];

        var mean = sorted.Average();
        var deviation = Math.Sqrt(sorted.Sum(value => Math.Pow(value - mean, 2)) / (sorted.Count - 1));

        if (deviation <= 0) return [];

        var interQuartile = Quantile(sorted, 0.75) - Quantile(sorted, 0.25);
        var spread = interQuartile > 0 ? Math.Min(deviation, interQuartile / 1.34) : deviation;

        var bandwidth = 0.9 * spread * Math.Pow(sorted.Count, -0.2);
        if (!double.IsFinite(bandwidth) || bandwidth <= 0) return [];

        var from = sorted[0] - 3 * bandwidth;
        var to = sorted[^1] + 3 * bandwidth;
        var step = (to - from) / Math.Max(1, samples - 1);
        if (!double.IsFinite(from) || !double.IsFinite(to) || !double.IsFinite(step)) return [];

        var points = new List<(double, double)>(Math.Max(0, samples));

        for (var index = 0; index < samples; index++)
        {
            var x = from + index * step;

            var density = sorted.Sum(value => Kernel((x - value) / bandwidth))
                / (sorted.Count * bandwidth);

            points.Add((x, density));
        }

        return points;
    }

    public IReadOnlyList<(double X, double Y)> Cumulative(IReadOnlyList<double> values)
    {
        var sorted = Clean(values);
        if (sorted.Count == 0) return [];

        return [.. sorted.Select((value, index) => (value, (index + 1) / (double)sorted.Count))];
    }

    public IReadOnlyList<(double Theoretical, double Observed)> QuantilePairs(
        IReadOnlyList<double> values)
    {
        var sorted = Clean(values);
        if (sorted.Count < 2) return [];

        return
        [
            .. sorted.Select((value, index) =>
                (NormalQuantile((index + 0.5) / sorted.Count), value)),
        ];
    }

    public IReadOnlyList<double> RunningTotals(IReadOnlyList<double> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var totals = new List<double>(values.Count);
        var running = 0.0;

        foreach (var value in values)
        {
            running += value;
            totals.Add(running);
        }

        return totals;
    }

    private static double Kernel(double u) =>
        Math.Exp(-0.5 * u * u) / Math.Sqrt(2 * Math.PI);

    private static List<double> Clean(IReadOnlyList<double> values) =>
        [.. values.Where(value => !double.IsNaN(value) && !double.IsInfinity(value)).Order()];

    private static double Quantile(IReadOnlyList<double> sorted, double fraction)
    {
        if (sorted.Count == 1) return sorted[0];

        var position = fraction * (sorted.Count - 1);
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);

        return lower == upper
            ? sorted[lower]
            : sorted[lower] + (position - lower) * (sorted[upper] - sorted[lower]);
    }

    private static double NormalQuantile(double probability)
    {
        if (probability <= 0) return -4;
        if (probability >= 1) return 4;

        const double a1 = -39.69683028665376, a2 = 220.9460984245205, a3 = -275.9285104469687;
        const double a4 = 138.3577518672690, a5 = -30.66479806614716, a6 = 2.506628277459239;
        const double b1 = -54.47609879822406, b2 = 161.5858368580409, b3 = -155.6989798598866;
        const double b4 = 66.80131188771972, b5 = -13.28068155288572;
        const double c1 = -0.007784894002430293, c2 = -0.3223964580411365, c3 = -2.400758277161838;
        const double c4 = -2.549732539343734, c5 = 4.374664141464968, c6 = 2.938163982698783;
        const double d1 = 0.007784695709041462, d2 = 0.3224671290700398;
        const double d3 = 2.445134137142996, d4 = 3.754408661907416;

        const double low = 0.02425;
        const double high = 1 - low;

        if (probability < low)
        {
            var q = Math.Sqrt(-2 * Math.Log(probability));

            return (((((c1 * q + c2) * q + c3) * q + c4) * q + c5) * q + c6)
                / ((((d1 * q + d2) * q + d3) * q + d4) * q + 1);
        }

        if (probability > high)
        {
            var q = Math.Sqrt(-2 * Math.Log(1 - probability));

            return -(((((c1 * q + c2) * q + c3) * q + c4) * q + c5) * q + c6)
                / ((((d1 * q + d2) * q + d3) * q + d4) * q + 1);
        }

        var p = probability - 0.5;
        var r = p * p;

        return (((((a1 * r + a2) * r + a3) * r + a4) * r + a5) * r + a6) * p
            / (((((b1 * r + b2) * r + b3) * r + b4) * r + b5) * r + 1);
    }
}
