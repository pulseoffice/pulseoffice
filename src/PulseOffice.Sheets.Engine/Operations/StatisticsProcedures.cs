using PulseOffice.Sheets.Engine.Evaluation.Functions.Numerics;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Operations;

public sealed record StatisticsRow(string LabelKey, IReadOnlyList<CellValue> Values)
{
    public static StatisticsRow Of(string labelKey, params double[] numbers) =>
        new(labelKey, [.. numbers.Select(Reported)]);

    public static CellValue Reported(double number) =>
        double.IsNaN(number) ? CellValue.FromError(CellError.NotAvailable)
        : double.IsInfinity(number) ? CellValue.FromError(CellError.DivideByZero)
        : CellValue.Number(number);
}

public sealed record StatisticsTable(IReadOnlyList<StatisticsRow> Rows)
{
    public static readonly StatisticsTable Empty = new([]);
}

public enum SamplingMode { Random, Periodic }

public interface IStatisticsProcedures
{
    StatisticsTable Descriptive(IReadOnlyList<IReadOnlyList<double>> columns);

    StatisticsTable Correlation(IReadOnlyList<IReadOnlyList<double>> columns);

    StatisticsTable Covariance(IReadOnlyList<IReadOnlyList<double>> columns);

    StatisticsTable AnovaSingleFactor(IReadOnlyList<IReadOnlyList<double>> groups, double alpha = 0.05);

    StatisticsTable TTestPaired(IReadOnlyList<double> first, IReadOnlyList<double> second, double alpha = 0.05);

    StatisticsTable TTestIndependent(
        IReadOnlyList<double> first, IReadOnlyList<double> second, bool equalVariances, double alpha = 0.05);

    StatisticsTable FTest(IReadOnlyList<double> first, IReadOnlyList<double> second);

    StatisticsTable ZTest(
        IReadOnlyList<double> first, IReadOnlyList<double> second, double firstVariance, double secondVariance);

    StatisticsTable ChiSquareTest(IReadOnlyList<IReadOnlyList<double>> observed);

    StatisticsTable MovingAverage(IReadOnlyList<double> values, int window);

    StatisticsTable ExponentialSmoothing(IReadOnlyList<double> values, double alpha);

    StatisticsTable LinearRegression(IReadOnlyList<double> x, IReadOnlyList<double> y);

    StatisticsTable Fourier(IReadOnlyList<double> values, bool inverse = false);

    IReadOnlyList<double> Sample(IReadOnlyList<double> values, int count, SamplingMode mode, int? seed = null);
}

public sealed class StatisticsProcedures : IStatisticsProcedures
{
    private readonly SampleMoments _moments = new();
    private readonly TailProbabilities _tails = new();
    private readonly StatisticsLayout _layout = new();

    public StatisticsTable Descriptive(IReadOnlyList<IReadOnlyList<double>> columns)
    {
        ArgumentNullException.ThrowIfNull(columns);

        var rows = new List<StatisticsRow>();

        void Add(string key, Func<IReadOnlyList<double>, double> compute) =>
            rows.Add(new StatisticsRow(key,
                [.. columns.Select(column => _layout.Report(_layout.Safe(column, compute)))]));

        Add("statistics.mean", SpreadsheetMath.Mean);
        Add("statistics.standardError", column => SpreadsheetMath.StandardDeviation(column, false)
            / Math.Sqrt(column.Count));
        Add("statistics.median", column => SpreadsheetMath.Median(column));
        Add("statistics.mode", _moments.Mode);
        Add("statistics.standardDeviation", column => SpreadsheetMath.StandardDeviation(column, false));
        Add("statistics.variance", column => SpreadsheetMath.Variance(column, false));
        Add("statistics.kurtosis", _moments.Kurtosis);
        Add("statistics.skewness", _moments.Skewness);
        Add("statistics.range", column => column.Max() - column.Min());
        Add("statistics.minimum", column => column.Min());
        Add("statistics.maximum", column => column.Max());
        Add("statistics.sum", column => column.Sum());
        Add("statistics.count", column => column.Count);

        return new StatisticsTable(rows);
    }

    public StatisticsTable Correlation(IReadOnlyList<IReadOnlyList<double>> columns) =>
        _layout.Matrix(columns, _moments.Pearson);

    public StatisticsTable Covariance(IReadOnlyList<IReadOnlyList<double>> columns) =>
        _layout.Matrix(columns, (first, second) => _moments.CovarianceOf(first, second, population: true));

    public StatisticsTable AnovaSingleFactor(IReadOnlyList<IReadOnlyList<double>> groups, double alpha = 0.05)
    {
        ArgumentNullException.ThrowIfNull(groups);

        var valid = groups.Where(group => group.Count > 0).ToList();
        if (valid.Count < 2) return StatisticsTable.Empty;

        var all = valid.SelectMany(group => group).ToList();
        var grandMean = all.Average();

        var betweenSquares = valid.Sum(group => group.Count * Math.Pow(group.Average() - grandMean, 2));
        var withinSquares = valid.Sum(group =>
        {
            var mean = group.Average();
            return group.Sum(value => Math.Pow(value - mean, 2));
        });

        var betweenDegrees = valid.Count - 1;
        var withinDegrees = all.Count - valid.Count;

        var betweenMean = betweenSquares / betweenDegrees;
        var withinMean = withinDegrees > 0 ? withinSquares / withinDegrees : double.NaN;

        var f = withinMean > 0 ? betweenMean / withinMean : double.NaN;
        var p = double.IsNaN(f) ? double.NaN : _tails.FDistributionRight(f, betweenDegrees, withinDegrees);

        return new StatisticsTable(
        [
            StatisticsRow.Of("statistics.anova.between", betweenSquares, betweenDegrees, betweenMean, f, p),
            StatisticsRow.Of("statistics.anova.within", withinSquares, withinDegrees, withinMean),
            StatisticsRow.Of("statistics.anova.total", betweenSquares + withinSquares, all.Count - 1),
            StatisticsRow.Of("statistics.alpha", alpha),
        ]);
    }

    public StatisticsTable TTestPaired(
        IReadOnlyList<double> first, IReadOnlyList<double> second, double alpha = 0.05)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        var count = Math.Min(first.Count, second.Count);
        if (count < 2) return StatisticsTable.Empty;

        var differences = Enumerable.Range(0, count).Select(index => first[index] - second[index]).ToList();

        var mean = differences.Average();
        var deviation = SpreadsheetMath.StandardDeviation(differences, population: false);

        var t = deviation > 0 ? mean / (deviation / Math.Sqrt(count)) : double.NaN;
        var degrees = count - 1;

        return new StatisticsTable(
        [
            StatisticsRow.Of("statistics.mean", first.Take(count).Average(), second.Take(count).Average()),
            StatisticsRow.Of("statistics.variance",
                SpreadsheetMath.Variance(first.Take(count).ToList(), false),
                SpreadsheetMath.Variance(second.Take(count).ToList(), false)),
            StatisticsRow.Of("statistics.observations", count, count),
            StatisticsRow.Of("statistics.degreesOfFreedom", degrees),
            StatisticsRow.Of("statistics.tStatistic", t),
            StatisticsRow.Of("statistics.pOneTail", _tails.StudentRight(Math.Abs(t), degrees)),
            StatisticsRow.Of("statistics.pTwoTail", 2 * _tails.StudentRight(Math.Abs(t), degrees)),
            StatisticsRow.Of("statistics.alpha", alpha),
        ]);
    }

    public StatisticsTable TTestIndependent(
        IReadOnlyList<double> first, IReadOnlyList<double> second, bool equalVariances, double alpha = 0.05)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        if (first.Count < 2 || second.Count < 2) return StatisticsTable.Empty;

        var firstVariance = SpreadsheetMath.Variance(first, population: false);
        var secondVariance = SpreadsheetMath.Variance(second, population: false);

        double t;
        double degrees;

        if (equalVariances)
        {
            var pooled = ((first.Count - 1) * firstVariance + (second.Count - 1) * secondVariance)
                / (first.Count + second.Count - 2);

            t = (first.Average() - second.Average())
                / Math.Sqrt(pooled * (1.0 / first.Count + 1.0 / second.Count));

            degrees = first.Count + second.Count - 2;
        }
        else
        {
            var firstTerm = firstVariance / first.Count;
            var secondTerm = secondVariance / second.Count;

            t = (first.Average() - second.Average()) / Math.Sqrt(firstTerm + secondTerm);

            degrees = Math.Pow(firstTerm + secondTerm, 2)
                / (Math.Pow(firstTerm, 2) / (first.Count - 1) + Math.Pow(secondTerm, 2) / (second.Count - 1));
        }

        return new StatisticsTable(
        [
            StatisticsRow.Of("statistics.mean", first.Average(), second.Average()),
            StatisticsRow.Of("statistics.variance", firstVariance, secondVariance),
            StatisticsRow.Of("statistics.observations", first.Count, second.Count),
            StatisticsRow.Of("statistics.degreesOfFreedom", degrees),
            StatisticsRow.Of("statistics.tStatistic", t),
            StatisticsRow.Of("statistics.pOneTail", _tails.StudentRight(Math.Abs(t), degrees)),
            StatisticsRow.Of("statistics.pTwoTail", 2 * _tails.StudentRight(Math.Abs(t), degrees)),
            StatisticsRow.Of("statistics.alpha", alpha),
        ]);
    }

    public StatisticsTable FTest(IReadOnlyList<double> first, IReadOnlyList<double> second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        if (first.Count < 2 || second.Count < 2) return StatisticsTable.Empty;

        var firstVariance = SpreadsheetMath.Variance(first, population: false);
        var secondVariance = SpreadsheetMath.Variance(second, population: false);

        var f = secondVariance > 0 ? firstVariance / secondVariance : double.NaN;

        return new StatisticsTable(
        [
            StatisticsRow.Of("statistics.mean", first.Average(), second.Average()),
            StatisticsRow.Of("statistics.variance", firstVariance, secondVariance),
            StatisticsRow.Of("statistics.observations", first.Count, second.Count),
            StatisticsRow.Of("statistics.degreesOfFreedom", first.Count - 1, second.Count - 1),
            StatisticsRow.Of("statistics.fStatistic", f),
            StatisticsRow.Of("statistics.pOneTail", _tails.FOneTail(f, first.Count - 1, second.Count - 1)),
        ]);
    }

    public StatisticsTable ZTest(
        IReadOnlyList<double> first, IReadOnlyList<double> second, double firstVariance, double secondVariance)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        if (first.Count == 0 || second.Count == 0) return StatisticsTable.Empty;

        var z = (first.Average() - second.Average())
            / Math.Sqrt(firstVariance / first.Count + secondVariance / second.Count);

        var oneTail = 1 - SpreadsheetMath.StandardNormalCdf(Math.Abs(z));

        return new StatisticsTable(
        [
            StatisticsRow.Of("statistics.mean", first.Average(), second.Average()),
            StatisticsRow.Of("statistics.variance", firstVariance, secondVariance),
            StatisticsRow.Of("statistics.observations", first.Count, second.Count),
            StatisticsRow.Of("statistics.zStatistic", z),
            StatisticsRow.Of("statistics.pOneTail", oneTail),
            StatisticsRow.Of("statistics.pTwoTail", 2 * oneTail),
        ]);
    }

    public StatisticsTable ChiSquareTest(IReadOnlyList<IReadOnlyList<double>> observed)
    {
        ArgumentNullException.ThrowIfNull(observed);

        if (observed.Count < 2 || observed[0].Count < 2) return StatisticsTable.Empty;

        var rowTotals = observed.Select(row => row.Sum()).ToList();
        var columnTotals = Enumerable.Range(0, observed[0].Count)
            .Select(column => observed.Sum(row => row[column]))
            .ToList();

        var total = rowTotals.Sum();
        if (total <= 0) return StatisticsTable.Empty;

        var statistic = 0.0;

        for (var row = 0; row < observed.Count; row++)
            for (var column = 0; column < observed[row].Count; column++)
            {
                var expected = rowTotals[row] * columnTotals[column] / total;
                if (expected <= 0) continue;

                statistic += Math.Pow(observed[row][column] - expected, 2) / expected;
            }

        var degrees = (observed.Count - 1) * (observed[0].Count - 1);

        return new StatisticsTable(
        [
            StatisticsRow.Of("statistics.chiSquare", statistic),
            StatisticsRow.Of("statistics.degreesOfFreedom", degrees),
            StatisticsRow.Of("statistics.pValue", 1 - SpreadsheetMath.RegularizedIncompleteGamma(degrees / 2.0, statistic / 2.0)),
        ]);
    }

    public StatisticsTable MovingAverage(IReadOnlyList<double> values, int window)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentOutOfRangeException.ThrowIfLessThan(window, 1);

        var rows = new List<StatisticsRow>();

        for (var index = 0; index < values.Count; index++)
        {
            if (index < window - 1)
            {
                rows.Add(new StatisticsRow("statistics.movingAverage",
                    [CellValue.FromError(CellError.NotAvailable)]));

                continue;
            }

            var slice = values.Skip(index - window + 1).Take(window);
            rows.Add(StatisticsRow.Of("statistics.movingAverage", slice.Average()));
        }

        return new StatisticsTable(rows);
    }

    public StatisticsTable ExponentialSmoothing(IReadOnlyList<double> values, double alpha)
    {
        ArgumentNullException.ThrowIfNull(values);

        var rows = new List<StatisticsRow>();
        if (values.Count == 0) return StatisticsTable.Empty;

        rows.Add(new StatisticsRow("statistics.smoothed", [CellValue.FromError(CellError.NotAvailable)]));
        var previous = values[0];

        for (var index = 1; index < values.Count; index++)
        {
            rows.Add(StatisticsRow.Of("statistics.smoothed", previous));
            previous = alpha * values[index] + (1 - alpha) * previous;
        }

        return new StatisticsTable(rows);
    }

    public StatisticsTable LinearRegression(IReadOnlyList<double> x, IReadOnlyList<double> y)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);

        var count = Math.Min(x.Count, y.Count);
        if (count < 2) return StatisticsTable.Empty;

        var meanX = x.Take(count).Average();
        var meanY = y.Take(count).Average();

        var covariance = 0.0;
        var varianceX = 0.0;

        for (var index = 0; index < count; index++)
        {
            covariance += (x[index] - meanX) * (y[index] - meanY);
            varianceX += Math.Pow(x[index] - meanX, 2);
        }

        var observations = StatisticsRow.Of("statistics.observations", count);

        if (varianceX == 0)
        {
            var undefined = CellValue.FromError(CellError.DivideByZero);

            return new StatisticsTable(
            [
                new StatisticsRow("statistics.slope", [undefined]),
                new StatisticsRow("statistics.intercept", [undefined]),
                new StatisticsRow("statistics.determination", [undefined]),
                new StatisticsRow("statistics.correlation", [undefined]),
                new StatisticsRow("statistics.standardError", [undefined]),
                observations,
            ]);
        }

        var slope = covariance / varianceX;
        var intercept = meanY - slope * meanX;

        var totalSquares = y.Take(count).Sum(value => Math.Pow(value - meanY, 2));
        var residualSquares = Enumerable.Range(0, count)
            .Sum(index => Math.Pow(y[index] - (intercept + slope * x[index]), 2));

        var determination = totalSquares > 0 ? 1 - residualSquares / totalSquares : double.NaN;
        var standardError = count > 2 ? Math.Sqrt(residualSquares / (count - 2)) : double.NaN;
        var correlation = double.IsNaN(determination)
            ? double.NaN
            : Math.Sign(slope) * Math.Sqrt(Math.Abs(determination));

        return new StatisticsTable(
        [
            StatisticsRow.Of("statistics.slope", slope),
            StatisticsRow.Of("statistics.intercept", intercept),
            StatisticsRow.Of("statistics.determination", determination),
            StatisticsRow.Of("statistics.correlation", correlation),
            StatisticsRow.Of("statistics.standardError", standardError),
            observations,
        ]);
    }

    public StatisticsTable Fourier(IReadOnlyList<double> values, bool inverse = false)
    {
        ArgumentNullException.ThrowIfNull(values);

        var count = values.Count;
        if (count == 0) return StatisticsTable.Empty;

        var samples = values.Select(value => new System.Numerics.Complex(value, 0)).ToList();
        var transform = DiscreteFourier.TransformUnscaled(samples, inverse);
        var scale = inverse ? 1.0 / count : 1.0;
        var rows = new List<StatisticsRow>(count);

        foreach (var point in transform)
        {
            var real = point.Real * scale;
            var imaginary = point.Imaginary * scale;

            rows.Add(StatisticsRow.Of("statistics.fourier", real, imaginary,
                Math.Sqrt(real * real + imaginary * imaginary)));
        }

        return new StatisticsTable(rows);
    }

    public IReadOnlyList<double> Sample(
        IReadOnlyList<double> values, int count, SamplingMode mode, int? seed = null)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (count == 0 || values.Count == 0) return [];

        if (mode == SamplingMode.Periodic)
        {
            var step = Math.Max(values.Count / count, 1);

            return [.. Enumerable.Range(0, count)
                .Select(index => index * step)
                .Where(index => index < values.Count)
                .Select(index => values[index])];
        }

        var random = seed is null ? new Random() : new Random(seed.Value);
        var pool = values.ToList();
        var sample = new List<double>(Math.Min(count, pool.Count));

        while (sample.Count < count && pool.Count > 0)
        {
            var index = random.Next(pool.Count);

            sample.Add(pool[index]);
            pool.RemoveAt(index);
        }

        return sample;
    }
}

internal sealed class StatisticsLayout
{
    public StatisticsTable Matrix(
        IReadOnlyList<IReadOnlyList<double>> columns, Func<IReadOnlyList<double>, IReadOnlyList<double>, double> pair)
    {
        ArgumentNullException.ThrowIfNull(columns);

        var rows = new List<StatisticsRow>();

        for (var row = 0; row < columns.Count; row++)
        {
            var values = new List<CellValue>();

            for (var column = 0; column < columns.Count; column++)
                values.Add(column > row
                    ? CellValue.Empty
                    : StatisticsRow.Reported(pair(columns[row], columns[column])));

            rows.Add(new StatisticsRow("statistics.column", values));
        }

        return new StatisticsTable(rows);
    }

    public CellValue Report(double value) => StatisticsRow.Reported(value);

    public double Safe(IReadOnlyList<double> column, Func<IReadOnlyList<double>, double> compute) =>
        column.Count == 0 ? double.NaN : compute(column);
}

internal sealed class SampleMoments
{
    public double Mode(IReadOnlyList<double> values)
    {
        var groups = values.GroupBy(value => value).OrderByDescending(group => group.Count()).ThenBy(group => group.Key).ToList();

        return groups.Count == 0 || groups[0].Count() < 2 ? double.NaN : groups[0].Key;
    }

    public double Skewness(IReadOnlyList<double> values)
    {
        var count = values.Count;
        if (count < 3) return double.NaN;

        var mean = values.Average();
        var deviation = SpreadsheetMath.StandardDeviation(values, population: false);

        if (deviation == 0) return double.NaN;

        var sum = values.Sum(value => Math.Pow((value - mean) / deviation, 3));

        return count / ((double)(count - 1) * (count - 2)) * sum;
    }

    public double Kurtosis(IReadOnlyList<double> values)
    {
        var count = values.Count;
        if (count < 4) return double.NaN;

        var mean = values.Average();
        var deviation = SpreadsheetMath.StandardDeviation(values, population: false);

        if (deviation == 0) return double.NaN;

        var sum = values.Sum(value => Math.Pow((value - mean) / deviation, 4));

        var first = (double)count * (count + 1) / ((count - 1.0) * (count - 2) * (count - 3));
        var second = 3.0 * Math.Pow(count - 1, 2) / ((count - 2.0) * (count - 3));

        return first * sum - second;
    }

    public double Pearson(IReadOnlyList<double> first, IReadOnlyList<double> second)
    {
        var count = Math.Min(first.Count, second.Count);
        if (count == 0) return double.NaN;

        first = [.. first.Take(count)];
        second = [.. second.Take(count)];

        var covariance = CovarianceOf(first, second, population: true);

        var deviationFirst = SpreadsheetMath.StandardDeviation(first, population: true);
        var deviationSecond = SpreadsheetMath.StandardDeviation(second, population: true);

        return deviationFirst > 0 && deviationSecond > 0
            ? covariance / (deviationFirst * deviationSecond)
            : double.NaN;
    }

    public double CovarianceOf(
        IReadOnlyList<double> first, IReadOnlyList<double> second, bool population)
    {
        var count = Math.Min(first.Count, second.Count);
        if (count == 0) return double.NaN;

        var meanFirst = first.Take(count).Average();
        var meanSecond = second.Take(count).Average();

        var sum = 0.0;

        for (var index = 0; index < count; index++)
            sum += (first[index] - meanFirst) * (second[index] - meanSecond);

        return population ? sum / count : sum / (count - 1);
    }
}

internal sealed class TailProbabilities
{
    public double StudentRight(double t, double degrees)
    {
        if (double.IsNaN(t) || degrees <= 0) return double.NaN;

        var x = degrees / (degrees + t * t);

        return 0.5 * SpreadsheetMath.RegularizedIncompleteBeta(degrees / 2.0, 0.5, x);
    }

    public double FDistributionRight(double f, double first, double second)
    {
        if (double.IsNaN(f) || f < 0 || first <= 0 || second <= 0) return double.NaN;
        if (f == 0) return 1;

        var x = second / (second + first * f);

        return SpreadsheetMath.RegularizedIncompleteBeta(second / 2.0, first / 2.0, x);
    }

    public double FOneTail(double f, double first, double second)
    {
        var right = FDistributionRight(f, first, second);

        return !double.IsNaN(right) && f < 1 ? 1 - right : right;
    }
}
