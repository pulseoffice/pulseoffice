using M = System.Math;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Numerics;

public readonly record struct RegressionResult(
    double Slope, double Intercept, double Correlation, double StandardError, int Count)
{
    public double RSquared => Correlation * Correlation;

    public double Predict(double x) => Slope * x + Intercept;
}

public static class LinearRegression
{
    public static RegressionResult? Fit(IReadOnlyList<double> y, IReadOnlyList<double> x)
    {
        if (y.Count != x.Count || y.Count < 2) return null;

        var n = y.Count;
        var meanX = SpreadsheetMath.Mean(x);
        var meanY = SpreadsheetMath.Mean(y);

        double sumXy = 0, sumXx = 0, sumYy = 0;
        for (var i = 0; i < n; i++)
        {
            var dx = x[i] - meanX;
            var dy = y[i] - meanY;
            sumXy += dx * dy;
            sumXx += dx * dx;
            sumYy += dy * dy;
        }

        if (sumXx == 0) return null;

        var slope = sumXy / sumXx;
        var intercept = meanY - slope * meanX;
        var correlation = sumYy == 0 ? double.NaN : sumXy / M.Sqrt(sumXx) / M.Sqrt(sumYy);

        var standardError = n > 2
            ? M.Sqrt(M.Max(sumYy - slope * sumXy, 0) / (n - 2))
            : 0;

        return new RegressionResult(slope, intercept, correlation, standardError, n);
    }

    public static double? Covariance(IReadOnlyList<double> a, IReadOnlyList<double> b, bool population)
    {
        if (a.Count != b.Count || a.Count == 0) return null;
        if (!population && a.Count < 2) return null;

        var meanA = SpreadsheetMath.Mean(a);
        var meanB = SpreadsheetMath.Mean(b);

        var total = 0.0;
        for (var i = 0; i < a.Count; i++) total += (a[i] - meanA) * (b[i] - meanB);

        return total / (population ? a.Count : a.Count - 1);
    }

    public static double? Skewness(IReadOnlyList<double> values)
    {
        var n = values.Count;
        if (n < 3) return null;

        var mean = SpreadsheetMath.Mean(values);
        var deviation = SpreadsheetMath.StandardDeviation(values, population: false);
        if (deviation == 0) return null;

        var total = values.Sum(v => M.Pow((v - mean) / deviation, 3));
        return total * n / ((n - 1.0) * (n - 2.0));
    }

    public static double? Kurtosis(IReadOnlyList<double> values)
    {
        var n = values.Count;
        if (n < 4) return null;

        var mean = SpreadsheetMath.Mean(values);
        var deviation = SpreadsheetMath.StandardDeviation(values, population: false);
        if (deviation == 0) return null;

        var total = values.Sum(v => M.Pow((v - mean) / deviation, 4));
        var scale = n * (n + 1.0) / ((n - 1.0) * (n - 2.0) * (n - 3.0));
        var correction = 3.0 * (n - 1) * (n - 1) / ((n - 2.0) * (n - 3.0));
        return scale * total - correction;
    }
}
