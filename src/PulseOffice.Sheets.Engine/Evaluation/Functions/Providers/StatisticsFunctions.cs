using PulseOffice.Sheets.Engine.Evaluation.Functions.Numerics;
using PulseOffice.Sheets.Values;
using M = System.Math;
using F = PulseOffice.Sheets.Engine.Evaluation.Functions.FunctionBuilder;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Providers;

public sealed class StatisticsFunctions(NumericText numbers) : IFunctionProvider
{
    public string Category => "Statistica";

    public IEnumerable<FunctionDefinition> GetFunctions() => new FunctionBuilder(Category)
        .Aggregate("AVERAGE", n => n.Count == 0 ? F.DivideByZero() : F.Number(SpreadsheetMath.Mean(n)))
        .Aggregate("MAX", n => CellValue.Number(n.Count == 0 ? 0 : n.Max()))
        .Aggregate("MIN", n => CellValue.Number(n.Count == 0 ? 0 : n.Min()))
        .Aggregate("MEDIAN", n => n.Count == 0 ? F.OutOfDomain() : F.Number(SpreadsheetMath.Median(n)))

        .Aggregate("STDEV", n => n.Count < 2 ? F.DivideByZero() : F.Number(SpreadsheetMath.StandardDeviation(n, population: false)))
        .Aggregate("STDEVP", n => n.Count < 1 ? F.DivideByZero() : F.Number(SpreadsheetMath.StandardDeviation(n, population: true)))
        .Aggregate("STDEV.S", n => n.Count < 2 ? F.DivideByZero() : F.Number(SpreadsheetMath.StandardDeviation(n, population: false)))
        .Aggregate("STDEV.P", n => n.Count < 1 ? F.DivideByZero() : F.Number(SpreadsheetMath.StandardDeviation(n, population: true)))
        .Aggregate("VAR", n => n.Count < 2 ? F.DivideByZero() : F.Number(SpreadsheetMath.Variance(n, population: false)))
        .Aggregate("VARP", n => n.Count < 1 ? F.DivideByZero() : F.Number(SpreadsheetMath.Variance(n, population: true)))
        .Aggregate("VAR.S", n => n.Count < 2 ? F.DivideByZero() : F.Number(SpreadsheetMath.Variance(n, population: false)))
        .Aggregate("VAR.P", n => n.Count < 1 ? F.DivideByZero() : F.Number(SpreadsheetMath.Variance(n, population: true)))
        .Aggregate("DEVSQ", n => F.Number(SpreadsheetMath.SumOfSquaredDeviations(n)))

        .Aggregate("AVEDEV", n =>
        {
            if (n.Count == 0) return F.DivideByZero();
            var mean = SpreadsheetMath.Mean(n);
            return F.Number(n.Sum(x => M.Abs(x - mean)) / n.Count);
        })

        .Aggregate("GEOMEAN", n =>
            n.Count == 0 || n.Any(x => x <= 0) ? F.OutOfDomain()
                : F.Number(M.Exp(n.Sum(M.Log) / n.Count)))

        .Aggregate("HARMEAN", n =>
            n.Count == 0 || n.Any(x => x <= 0) ? F.OutOfDomain()
                : F.Number(n.Count / n.Sum(x => 1 / x)))

        .Raw("COUNT", 1, int.MaxValue, r =>
            CellValue.Number(Enumerable.Range(0, r.Count).Sum(i => r.Raw(i) is ScalarValue { Value: var direct }
                ? CountsAsDirectNumber(direct) ? 1 : 0
                : r.Values(i).Count(v => v.Kind == ValueKind.Number))))
        .Raw("COUNTA", 1, int.MaxValue, r =>
            CellValue.Number(Enumerable.Range(0, r.Count).Sum(i => r.Has(i) ? r.Values(i).Count(v => !v.IsEmpty) : 1)))
        .Raw("COUNTBLANK", 1, 1, r =>
            CellValue.Number(r.AllValues().Count(v => v.IsEmpty || v.Kind == ValueKind.Text && v.AsText().Length == 0)))

        .Lifted("LARGE", 2, 2, index => index == 1, SortedSample, (r, sample) => Rank(r, sample, fromLargest: true))
        .Lifted("SMALL", 2, 2, index => index == 1, SortedSample, (r, sample) => Rank(r, sample, fromLargest: false))

        .Raw("QUARTILE", 2, 2, r =>
        {
            if (!r.TryNumbersOf(0, out var numbers, out var error))
                return CellValue.FromError(error);
            var quart = r.Integer(1);
            if (r.HasError) return r.Error;
            return numbers.Count == 0 || quart is < 0 or > 4
                ? F.OutOfDomain()
                : F.Number(SpreadsheetMath.Quantile(numbers, quart / 4.0));
        })

        .Raw("PERCENTILE", 2, 2, r =>
        {
            if (!r.TryNumbersOf(0, out var numbers, out var error))
                return CellValue.FromError(error);
            var fraction = r.Number(1);
            if (r.HasError) return r.Error;
            return numbers.Count == 0 || fraction is < 0 or > 1
                ? F.OutOfDomain()
                : F.Number(SpreadsheetMath.Quantile(numbers, fraction));
        })

        .Build();

    private sealed record Sample(List<double> Sorted, CellError Error);

    private static Sample SortedSample(ArgumentReader r)
    {
        if (!r.TryNumbersOf(0, out var numbers, out var error)) return new Sample([], error);
        numbers.Sort();
        return new Sample(numbers, CellError.None);
    }

    private static CellValue Rank(ArgumentReader r, Sample sample, bool fromLargest)
    {
        if (sample.Error != CellError.None) return CellValue.FromError(sample.Error);

        var requested = r.Number(1);
        if (r.HasError) return r.Error;
        var rank = fromLargest ? SpreadsheetMath.ApproxCeiling(requested) : SpreadsheetMath.ApproxFloor(requested);
        if (rank < 1 || rank > sample.Sorted.Count) return F.OutOfDomain();

        var k = (int)rank;
        return CellValue.Number(fromLargest ? sample.Sorted[^k] : sample.Sorted[k - 1]);
    }

    private bool CountsAsDirectNumber(CellValue value) => value.Kind switch
    {
        ValueKind.Number or ValueKind.Boolean => true,
        ValueKind.Text => numbers.TryRead(value.AsText(), out _),
        _ => false,
    };
}
