namespace PulseOffice.Charts;

public sealed record ChartErrorSpan(double Centre, double Low, double High);

public interface IChartTrendFit
{
    IReadOnlyList<(double X, double Y)> Curve(ChartTrendline trendline, IReadOnlyList<(double X, double Y)> points, int samples = 48);
}

public sealed class ChartTrendFit : IChartTrendFit
{
    public IReadOnlyList<(double X, double Y)> Curve(ChartTrendline trendline, IReadOnlyList<(double X, double Y)> points, int samples = 48)
    {
        ArgumentNullException.ThrowIfNull(trendline);
        ArgumentNullException.ThrowIfNull(points);
        var usable = points.Where(point => double.IsFinite(point.X) && double.IsFinite(point.Y)).OrderBy(point => point.X).ToList();
        if (usable.Count < 2) return [];
        if (trendline.Kind == TrendlineKind.MovingAverage) return MovingAverage(usable, trendline.EffectivePeriod);

        var model = Model(trendline, usable);
        if (model is null) return [];
        var first = usable[0].X - Math.Max(0, trendline.Backward);
        var last = usable[^1].X + Math.Max(0, trendline.Forward);
        if (trendline.Kind is TrendlineKind.Logarithmic or TrendlineKind.Power) first = Math.Max(first, usable.Where(point => point.X > 0).Select(point => point.X).DefaultIfEmpty(1).Min());
        if (!(last > first)) return [];
        var count = Math.Clamp(samples, 2, 512);
        var curve = new List<(double X, double Y)>(count);
        for (var step = 0; step < count; step++)
        {
            var x = first + (last - first) * step / (count - 1);
            var y = model(x);
            if (double.IsFinite(y)) curve.Add((x, y));
        }
        return curve;
    }

    private List<(double X, double Y)> MovingAverage(List<(double X, double Y)> points, int period)
    {
        var curve = new List<(double X, double Y)>();
        for (var end = period - 1; end < points.Count; end++)
            curve.Add((points[end].X, points.Skip(end - period + 1).Take(period).Average(point => point.Y)));
        return curve;
    }

    private Func<double, double>? Model(ChartTrendline trendline, List<(double X, double Y)> points)
    {
        switch (trendline.Kind)
        {
            case TrendlineKind.Linear:
                return Line(points, trendline.Intercept) is { } line ? x => line.Intercept + line.Slope * x : null;
            case TrendlineKind.Exponential:
            {
                var positive = points.Where(point => point.Y > 0).Select(point => (point.X, Math.Log(point.Y))).ToList();
                var fit = Line(positive, trendline.Intercept is > 0 and var a ? Math.Log(a) : null);
                return fit is { } exponential ? x => Math.Exp(exponential.Intercept + exponential.Slope * x) : null;
            }
            case TrendlineKind.Logarithmic:
            {
                var fit = Line([.. points.Where(point => point.X > 0).Select(point => (Math.Log(point.X), point.Y))], null);
                return fit is { } logarithmic ? x => x > 0 ? logarithmic.Intercept + logarithmic.Slope * Math.Log(x) : double.NaN : null;
            }
            case TrendlineKind.Power:
            {
                var fit = Line([.. points.Where(point => point.X > 0 && point.Y > 0).Select(point => (Math.Log(point.X), Math.Log(point.Y)))], null);
                return fit is { } power ? x => x > 0 ? Math.Exp(power.Intercept) * Math.Pow(x, power.Slope) : double.NaN : null;
            }
            case TrendlineKind.Polynomial:
                return Polynomial(points, trendline.EffectiveOrder) is { } coefficients ? x => coefficients.Select((c, power) => c * Math.Pow(x, power)).Sum() : null;
            default:
                return null;
        }
    }

    private (double Intercept, double Slope)? Line(IReadOnlyList<(double X, double Y)> points, double? intercept)
    {
        if (points.Count < 2) return null;
        if (intercept is { } fixedIntercept)
        {
            var squares = points.Sum(point => point.X * point.X);
            return squares > 0 ? (fixedIntercept, points.Sum(point => point.X * (point.Y - fixedIntercept)) / squares) : null;
        }
        var meanX = points.Average(point => point.X);
        var meanY = points.Average(point => point.Y);
        var spread = points.Sum(point => (point.X - meanX) * (point.X - meanX));
        if (!(spread > 0)) return null;
        var slope = points.Sum(point => (point.X - meanX) * (point.Y - meanY)) / spread;
        return (meanY - slope * meanX, slope);
    }

    private double[]? Polynomial(List<(double X, double Y)> points, int order)
    {
        var size = Math.Min(order, points.Count - 1) + 1;
        var matrix = new double[size, size + 1];
        for (var row = 0; row < size; row++)
        {
            for (var column = 0; column < size; column++) matrix[row, column] = points.Sum(point => Math.Pow(point.X, row + column));
            matrix[row, size] = points.Sum(point => point.Y * Math.Pow(point.X, row));
        }
        for (var pivot = 0; pivot < size; pivot++)
        {
            var best = Enumerable.Range(pivot, size - pivot).MaxBy(row => Math.Abs(matrix[row, pivot]));
            if (Math.Abs(matrix[best, pivot]) < 1e-12) return null;
            for (var column = 0; column <= size; column++) (matrix[pivot, column], matrix[best, column]) = (matrix[best, column], matrix[pivot, column]);
            for (var row = 0; row < size; row++)
            {
                if (row == pivot) continue;
                var factor = matrix[row, pivot] / matrix[pivot, pivot];
                for (var column = pivot; column <= size; column++) matrix[row, column] -= factor * matrix[pivot, column];
            }
        }
        return [.. Enumerable.Range(0, size).Select(row => matrix[row, size] / matrix[row, row])];
    }
}

public interface IChartErrorSpans
{
    IReadOnlyList<ChartErrorSpan?> Spans(ChartErrorBars bars, IReadOnlyList<double> values);
}

public sealed class ChartErrorSpans : IChartErrorSpans
{
    public IReadOnlyList<ChartErrorSpan?> Spans(ChartErrorBars bars, IReadOnlyList<double> values)
    {
        ArgumentNullException.ThrowIfNull(bars);
        ArgumentNullException.ThrowIfNull(values);
        var finite = values.Where(double.IsFinite).ToList();
        var mean = finite.Count == 0 ? 0 : finite.Average();
        var deviation = finite.Count < 2 ? 0 : Math.Sqrt(finite.Sum(value => (value - mean) * (value - mean)) / (finite.Count - 1));
        var standardError = finite.Count == 0 ? 0 : Math.Sqrt(finite.Sum(value => value * value) / Math.Max(1, finite.Count * (finite.Count - 1)));
        var amount = double.IsFinite(bars.Value) ? Math.Abs(bars.Value) : 1;
        return [.. values.Select(value =>
        {
            if (!double.IsFinite(value)) return (ChartErrorSpan?)null;
            var (centre, size) = bars.Kind switch
            {
                ErrorBarKind.Percentage => (value, Math.Abs(value) * amount / 100),
                ErrorBarKind.StandardDeviation => (mean, deviation * amount),
                ErrorBarKind.StandardError => (value, standardError),
                _ => (value, amount),
            };
            return new ChartErrorSpan(centre,
                bars.Direction == ErrorBarDirection.Plus ? centre : centre - size,
                bars.Direction == ErrorBarDirection.Minus ? centre : centre + size);
        })];
    }
}
