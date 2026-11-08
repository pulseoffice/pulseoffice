using PulseOffice.Sheets.Engine.Evaluation.Functions.Numerics;
using PulseOffice.Sheets.Values;
using M = System.Math;
using F = PulseOffice.Sheets.Engine.Evaluation.Functions.FunctionBuilder;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Providers;

public sealed class RegressionFunctions(IRangeMaterializer ranges) : IFunctionProvider
{
    public string Category => "Statistica";

    public IEnumerable<FunctionDefinition> GetFunctions()
    {
        var builder = new FunctionBuilder(Category);

        (string Name, int MinimumPoints, Func<RegressionResult, double> Select)[] fromFit =
        [
            ("SLOPE", 2, f => f.Slope),
            ("INTERCEPT", 2, f => f.Intercept),
            ("RSQ", 2, f => f.RSquared),
            ("STEYX", 3, f => f.StandardError),
            ("CORREL", 2, f => f.Correlation),
            ("PEARSON", 2, f => f.Correlation),
        ];

        foreach (var (name, minimum, select) in fromFit)
            builder.Raw(name, 2, 2, r =>
            {
                var (y, x) = Pair(r, 0, 1);
                if (r.HasError) return r.Error;
                if (y.Count < minimum) return F.DivideByZero();
                return LinearRegression.Fit(y, x) is { } fit && select(fit) is var value && !double.IsNaN(value) ? F.Number(value) : F.DivideByZero();
            });

        foreach (var (name, population) in ((string, bool)[])
                 [("COVAR", true), ("COVARIANCE.P", true), ("COVARIANCE.S", false)])
            builder.Raw(name, 2, 2, r =>
            {
                var (a, b) = Pair(r, 0, 1);
                if (r.HasError) return r.Error;
                return LinearRegression.Covariance(a, b, population) is { } value
                    ? F.Number(value) : F.DivideByZero();
            });

        foreach (var name in (string[])["FORECAST", "FORECAST.LINEAR"])
            builder.Raw(name, 3, 3, r =>
            {
                var target = r.Number(0);
                var (y, x) = Pair(r, 1, 2);
                if (r.HasError) return r.Error;
                return LinearRegression.Fit(y, x) is { } fit
                    ? F.Number(fit.Predict(target)) : F.DivideByZero();
            });

        builder
            .Aggregate("SKEW", numbers => LinearRegression.Skewness(numbers) is { } value
                ? F.Number(value) : F.DivideByZero())
            .Aggregate("KURT", numbers => LinearRegression.Kurtosis(numbers) is { } value
                ? F.Number(value) : F.DivideByZero())
            .Aggregate("MODE", Mode)
            .Aggregate("MODE.SNGL", Mode);

        builder.Raw("TRIMMEAN", 2, 2, r =>
        {
            var values = Series(r, 0);
            var fraction = r.Number(1);
            if (r.HasError) return r.Error;
            if (fraction is < 0 or >= 1 || values.Count == 0) return F.OutOfDomain();

            values.Sort();

            var trimmed = (int)M.Floor(values.Count * fraction / 2);
            var kept = values.Skip(trimmed).Take(values.Count - 2 * trimmed).ToList();

            return kept.Count == 0 ? F.OutOfDomain() : F.Number(kept.Average());
        });

        foreach (var name in (string[])["RANK", "RANK.EQ"])
            builder.Raw(name, 2, 3, r => Rank(r, average: false));
        builder.Raw("RANK.AVG", 2, 3, r => Rank(r, average: true));

        builder.Raw("PERCENTRANK", 2, 3, r => PercentRank(r, Series(r, 0), exclusive: false));

        (string Name, Func<double, double, double> Combine)[] pairwise =
        [
            ("SUMX2MY2", (a, b) => a * a - b * b),
            ("SUMX2PY2", (a, b) => a * a + b * b),
            ("SUMXMY2", (a, b) => (a - b) * (a - b)),
        ];

        foreach (var (name, combine) in pairwise)
            builder.Raw(name, 2, 2, r =>
            {
                var (a, b) = Pair(r, 0, 1);
                if (r.HasError) return r.Error;
                if (a.Count != b.Count) return CellValue.FromError(CellError.NotAvailable);

                var total = 0.0;
                for (var i = 0; i < a.Count; i++) total += combine(a[i], b[i]);
                return F.Number(total);
            });

        builder.Raw("SUMPRODUCT", 1, int.MaxValue, r =>
        {
            var grids = new List<ValueGrid>();
            for (var i = 0; i < r.Count; i++) grids.Add(ranges.Materialize(r.Raw(i), r.Context));
            if (r.HasError) return r.Error;

            var size = grids[0].RowCount * grids[0].ColumnCount;
            if (grids.Any(g => g.RowCount != grids[0].RowCount || g.ColumnCount != grids[0].ColumnCount)) return F.Invalid();

            var columns = grids.Select(g => g.InReadingOrder().ToList()).ToList();
            var total = 0.0;
            var booleans = r.Context.TextRules.Settings.BooleansAreNumbersInArrays;

            for (var i = 0; i < size; i++)
            {
                var product = 1.0;
                foreach (var column in columns)
                {
                    if (column[i].IsError) return column[i];

                    product *= column[i].Kind == ValueKind.Number ? column[i].AsNumber()
                        : booleans && column[i].Kind == ValueKind.Boolean ? (column[i].AsBoolean() ? 1 : 0) : 0;
                }
                total += product;
            }
            return F.Number(total);
        });

        return builder.Build();
    }

    private static CellValue Mode(List<double> numbers)
    {
        if (numbers.Count == 0) return CellValue.FromError(CellError.NotAvailable);

        var groups = numbers.GroupBy(n => n)
                            .Select(g => (Value: g.Key, Count: g.Count()))
                            .OrderByDescending(g => g.Count)
                            .ToList();

        return groups[0].Count < 2
            ? CellValue.FromError(CellError.NotAvailable)
            : CellValue.Number(groups[0].Value);
    }

    private CellValue Rank(ArgumentReader r, bool average)
    {
        var target = r.Number(0);
        var values = Series(r, 1);
        var ascending = r.Number(2, 0) != 0;
        if (r.HasError) return r.Error;
        if (values.Count == 0) return CellValue.FromError(CellError.NotAvailable);

        var better = ascending
            ? values.Count(v => v < target)
            : values.Count(v => v > target);
        var equal = values.Count(v => v == target);

        if (equal == 0) return CellValue.FromError(CellError.NotAvailable);

        return CellValue.Number(average ? better + 1 + (equal - 1) / 2.0 : better + 1);
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

    private (List<double> First, List<double> Second) Pair(ArgumentReader r, int firstIndex, int secondIndex)
    {
        var first = ranges.Materialize(r.Raw(firstIndex), r.Context).InReadingOrder().ToList();
        var second = ranges.Materialize(r.Raw(secondIndex), r.Context).InReadingOrder().ToList();
        if (first.Count != second.Count)
        {
            r.Fail(CellError.NotAvailable);
            return ([], []);
        }

        var (left, right) = (new List<double>(), new List<double>());
        for (var index = 0; index < first.Count; index++)
        {
            if (first[index].IsError) { r.Fail(first[index].Error); return ([], []); }
            if (second[index].IsError) { r.Fail(second[index].Error); return ([], []); }
            if (first[index].Kind != ValueKind.Number || second[index].Kind != ValueKind.Number) continue;
            left.Add(first[index].AsNumber());
            right.Add(second[index].AsNumber());
        }
        return (left, right);
    }

    internal static CellValue PercentRank(ArgumentReader r, List<double> values, bool exclusive)
    {
        var target = r.Number(1);
        var significance = M.Floor(r.Number(2, 3));
        if (r.HasError) return r.Error;
        if (values.Count == 0 || significance < 1) return F.OutOfDomain();

        values.Sort();
        if (values.Count == 1 && !exclusive)
            return values[0] == target ? F.Number(1) : CellValue.FromError(CellError.NotAvailable);
        if (PercentRanking.Position(values, target) is not { } position) return CellValue.FromError(CellError.NotAvailable);

        var rank = exclusive ? (position + 1) / (values.Count + 1) : position / (values.Count - 1);
        return F.Number(PercentRanking.Truncate(rank, (int)significance));
    }
}
