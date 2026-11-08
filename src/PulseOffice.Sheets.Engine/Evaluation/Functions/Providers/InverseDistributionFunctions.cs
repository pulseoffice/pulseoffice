using PulseOffice.Sheets.Engine.Evaluation.Functions.Numerics;
using PulseOffice.Sheets.Values;
using M = System.Math;
using F = PulseOffice.Sheets.Engine.Evaluation.Functions.FunctionBuilder;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Providers;

public sealed class InverseDistributionFunctions(IRangeMaterializer ranges) : IFunctionProvider
{
    public string Category => "Statistica";

    public IEnumerable<FunctionDefinition> GetFunctions()
    {
        var builder = new FunctionBuilder(Category);

        RegisterInverses(builder);
        RegisterTests(builder);

        return builder.Build();
    }

    private static void RegisterInverses(FunctionBuilder builder)
    {
        foreach (var name in (string[])["CHIINV", "CHISQ.INV.RT"])
            builder.Numeric2(name, (probability, degrees) =>
                DistributionInverse.ChiSquare(1 - probability, M.Floor(degrees)) is { } value
                    ? F.Number(value) : F.OutOfDomain());

        foreach (var name in (string[])["CHISQ.INV", "CHISQINV"])
            builder.Numeric2(name, (probability, degrees) =>
                DistributionInverse.ChiSquare(probability, M.Floor(degrees)) is { } value
                    ? F.Number(value) : F.OutOfDomain());

        foreach (var name in (string[])["TINV", "T.INV.2T"])
            builder.Numeric2(name, (probability, degrees) =>
                DistributionInverse.StudentTwoTailed(probability, M.Floor(degrees)) is { } value
                    ? F.Number(value) : F.OutOfDomain());

        builder.Numeric2("T.INV", (probability, degrees) =>
            DistributionInverse.StudentOneTailed(probability, M.Floor(degrees)) is { } value
                ? F.Number(value) : F.OutOfDomain());

        foreach (var name in (string[])["FINV", "F.INV.RT"])
            builder.Raw(name, 3, 3, r =>
            {
                var probability = r.Number(0);
                var degrees1 = M.Floor(r.Number(1));
                var degrees2 = M.Floor(r.Number(2));
                if (r.HasError) return r.Error;
                return DistributionInverse.Fisher(1 - probability, degrees1, degrees2) is { } value
                    ? F.Number(value) : F.OutOfDomain();
            });

        builder.Raw("F.INV", 3, 3, r =>
        {
            var probability = r.Number(0);
            var degrees1 = M.Floor(r.Number(1));
            var degrees2 = M.Floor(r.Number(2));
            if (r.HasError) return r.Error;
            return DistributionInverse.Fisher(probability, degrees1, degrees2) is { } value
                ? F.Number(value) : F.OutOfDomain();
        });

        foreach (var name in (string[])["BETAINV", "BETA.INV"])
            builder.Raw(name, 3, 5, r =>
            {
                var probability = r.Number(0);
                var alpha = r.Number(1);
                var beta = r.Number(2);
                var lower = r.Number(3, 0);
                var upper = r.Number(4, 1);
                if (r.HasError) return r.Error;
                if (upper <= lower) return F.OutOfDomain();

                return DistributionInverse.Beta(probability, alpha, beta) is { } value
                    ? F.Number(lower + value * (upper - lower)) : F.OutOfDomain();
            });

        foreach (var name in (string[])["GAMMAINV", "GAMMA.INV"])
            builder.Raw(name, 3, 3, r =>
            {
                var probability = r.Number(0);
                var shape = r.Number(1);
                var scale = r.Number(2);
                if (r.HasError) return r.Error;
                return DistributionInverse.Gamma(probability, shape, scale) is { } value
                    ? F.Number(value) : F.OutOfDomain();
            });

        builder.Raw("CONFIDENCE.T", 3, 3, r =>
        {
            var alpha = r.Number(0);
            var deviation = r.Number(1);
            var size = r.Number(2);
            if (r.HasError) return r.Error;
            if (alpha is <= 0 or >= 1 || deviation <= 0 || size < 2) return F.OutOfDomain();

            return DistributionInverse.StudentTwoTailed(alpha, size - 1) is { } t
                ? F.Number(t * deviation / M.Sqrt(size)) : F.OutOfDomain();
        });
    }

    private void RegisterTests(FunctionBuilder builder)
    {
        foreach (var name in (string[])["CHITEST", "CHISQ.TEST"])
            builder.Raw(name, 2, 2, r =>
            {
                var observed = Series(r, 0);
                var expected = Series(r, 1);
                if (r.HasError) return r.Error;
                if (observed.Count != expected.Count || observed.Count < 2) return CellValue.FromError(CellError.NotAvailable);

                var statistic = 0.0;
                for (var i = 0; i < observed.Count; i++)
                {
                    if (expected[i] == 0) return F.DivideByZero();
                    var difference = observed[i] - expected[i];
                    statistic += difference * difference / expected[i];
                }

                var shape = ranges.Materialize(r.Raw(0), r.Context);
                var degrees = shape.RowCount > 1 && shape.ColumnCount > 1
                    ? (shape.RowCount - 1) * (shape.ColumnCount - 1)
                    : observed.Count - 1;
                return F.Number(1 - SpreadsheetMath.RegularizedIncompleteGamma(degrees / 2.0, statistic / 2));
            });

        foreach (var name in (string[])["ZTEST", "Z.TEST"])
            builder.Raw(name, 2, 3, r =>
            {
                var values = Series(r, 0);
                var hypothesis = r.Number(1);
                if (r.HasError) return r.Error;
                if (values.Count < 2) return CellValue.FromError(CellError.NotAvailable);

                var deviation = r.Has(2)
                    ? r.Number(2)
                    : SpreadsheetMath.StandardDeviation(values, population: false);
                if (deviation <= 0) return F.DivideByZero();

                var z = (SpreadsheetMath.Mean(values) - hypothesis) / (deviation / M.Sqrt(values.Count));
                return F.Number(1 - SpreadsheetMath.StandardNormalCdf(z));
            });

        foreach (var name in (string[])["FTEST", "F.TEST"])
            builder.Raw(name, 2, 2, r =>
            {
                var first = Series(r, 0);
                var second = Series(r, 1);
                if (r.HasError) return r.Error;
                if (first.Count < 2 || second.Count < 2) return CellValue.FromError(CellError.NotAvailable);

                var varianceFirst = SpreadsheetMath.Variance(first, population: false);
                var varianceSecond = SpreadsheetMath.Variance(second, population: false);
                if (varianceFirst == 0 || varianceSecond == 0) return F.DivideByZero();

                var ratio = varianceFirst / varianceSecond;
                var probability = SpreadsheetMath.RegularizedIncompleteBeta(
                    (first.Count - 1) / 2.0, (second.Count - 1) / 2.0,
                    (first.Count - 1) * ratio / ((first.Count - 1) * ratio + second.Count - 1));

                return F.Number(2 * M.Min(probability, 1 - probability));
            });

        foreach (var name in (string[])["TTEST", "T.TEST"])
            builder.Raw(name, 4, 4, r =>
            {
                var first = Series(r, 0);
                var second = Series(r, 1);
                var tails = r.Integer(2);
                var type = r.Integer(3);
                if (r.HasError) return r.Error;
                if (tails is < 1 or > 2 || type is < 1 or > 3) return F.OutOfDomain();
                if (first.Count < 2 || second.Count < 2) return CellValue.FromError(CellError.NotAvailable);

                var (statistic, degrees) = type switch
                {
                    1 => PairedStatistic(first, second),
                    2 => PooledStatistic(first, second),
                    _ => WelchStatistic(first, second),
                };

                if (double.IsNaN(statistic)) return F.DivideByZero();

                var tail = 0.5 * SpreadsheetMath.RegularizedIncompleteBeta(
                    degrees / 2, 0.5, degrees / (degrees + statistic * statistic));
                return F.Number(tails == 1 ? tail : 2 * tail);
            });
    }

    private static (double Statistic, double Degrees) PairedStatistic(
    IReadOnlyList<double> first, IReadOnlyList<double> second)
    {
        if (first.Count != second.Count) return (double.NaN, 0);

        var differences = new List<double>(first.Count);
        for (var i = 0; i < first.Count; i++) differences.Add(first[i] - second[i]);

        var deviation = SpreadsheetMath.StandardDeviation(differences, population: false);
        if (deviation == 0) return (double.NaN, 0);

        var statistic = SpreadsheetMath.Mean(differences) / (deviation / M.Sqrt(differences.Count));
        return (statistic, differences.Count - 1);
    }

    private static (double Statistic, double Degrees) PooledStatistic(
    IReadOnlyList<double> first, IReadOnlyList<double> second)
    {
        var n1 = first.Count;
        var n2 = second.Count;
        var pooled = ((n1 - 1) * SpreadsheetMath.Variance(first, false)
                    + (n2 - 1) * SpreadsheetMath.Variance(second, false)) / (n1 + n2 - 2);

        if (pooled == 0) return (double.NaN, 0);

        var statistic = (SpreadsheetMath.Mean(first) - SpreadsheetMath.Mean(second))
                        / M.Sqrt(pooled * (1.0 / n1 + 1.0 / n2));
        return (statistic, n1 + n2 - 2);
    }

    private static (double Statistic, double Degrees) WelchStatistic(
    IReadOnlyList<double> first, IReadOnlyList<double> second)
    {
        var v1 = SpreadsheetMath.Variance(first, false) / first.Count;
        var v2 = SpreadsheetMath.Variance(second, false) / second.Count;
        if (v1 + v2 == 0) return (double.NaN, 0);

        var statistic = (SpreadsheetMath.Mean(first) - SpreadsheetMath.Mean(second)) / M.Sqrt(v1 + v2);
        var degrees = M.Pow(v1 + v2, 2)
                      / (v1 * v1 / (first.Count - 1) + v2 * v2 / (second.Count - 1));
        return (statistic, degrees);
    }

    private List<double> Series(ArgumentReader r, int index)
    {
        var grid = ranges.Materialize(r.Raw(index), r.Context);
        var values = new List<double>();

        foreach (var value in grid.InReadingOrder())
        {
            if (value.IsError) { r.Fail(value.Error); return values; }
            if (value.Kind == ValueKind.Number) values.Add(value.AsNumber());
        }
        return values;
    }
}
