using PulseOffice.Sheets.Engine.Evaluation.Functions.Numerics;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Pivot;

public interface IPivotAggregator
{
    CellValue Aggregate(PivotAggregation aggregation, IReadOnlyList<CellValue> values);
}

public sealed class PivotAggregator : IPivotAggregator
{
    private readonly IReadOnlyDictionary<PivotAggregation, Aggregation> _aggregations = new Aggregation[]
    {
        new CellAggregation(PivotAggregation.Count, cells => CellValue.Number(cells.Count(value => !value.IsEmpty))),
        new CellAggregation(PivotAggregation.DistinctCount, cells => CellValue.Number(cells
            .Where(value => !value.IsEmpty).Select(PivotValues.KeyOf).Distinct(StringComparer.Ordinal).Count())),
        new CellAggregation(PivotAggregation.First, cells => cells.FirstOrDefault(value => !value.IsEmpty, CellValue.Empty)),
        new CellAggregation(PivotAggregation.Last, cells => cells.LastOrDefault(value => !value.IsEmpty, CellValue.Empty)),
        new CountNumbers(),
        new NumericAggregation(PivotAggregation.Sum, numbers => CellValue.Number(numbers.Sum())),
        new NumericAggregation(PivotAggregation.Average, numbers => CellValue.Number(numbers.Average())),
        new NumericAggregation(PivotAggregation.Max, numbers => CellValue.Number(numbers.Max())),
        new NumericAggregation(PivotAggregation.Min, numbers => CellValue.Number(numbers.Min())),
        new NumericAggregation(PivotAggregation.Product, numbers => CellValue.Number(numbers.Aggregate(1.0, (a, b) => a * b))),
        new NumericAggregation(PivotAggregation.Median, numbers => CellValue.Number(SpreadsheetMath.Median(numbers))),
        new Spread(PivotAggregation.Variance, population: false, root: false),
        new Spread(PivotAggregation.VariancePopulation, population: true, root: false),
        new Spread(PivotAggregation.StandardDeviation, population: false, root: true),
        new Spread(PivotAggregation.StandardDeviationPopulation, population: true, root: true),
    }.ToDictionary(aggregation => aggregation.Kind);

    public CellValue Aggregate(PivotAggregation aggregation, IReadOnlyList<CellValue> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        return (_aggregations.TryGetValue(aggregation, out var rule) ? rule : _aggregations[PivotAggregation.Sum]).Apply(values);
    }
}

internal abstract class Aggregation(PivotAggregation kind)
{
    public PivotAggregation Kind => kind;

    public abstract CellValue Apply(IReadOnlyList<CellValue> values);
}

internal sealed class CellAggregation(PivotAggregation kind, Func<IReadOnlyList<CellValue>, CellValue> compute) : Aggregation(kind)
{
    public override CellValue Apply(IReadOnlyList<CellValue> values) => compute(values);
}

internal abstract class NumberAggregation(PivotAggregation kind) : Aggregation(kind)
{
    public override CellValue Apply(IReadOnlyList<CellValue> values) =>
        Over([.. values.Where(value => value.IsNumeric).Select(value => value.AsNumber())]);

    protected abstract CellValue Over(List<double> numbers);
}

internal sealed class CountNumbers() : NumberAggregation(PivotAggregation.CountNumbers)
{
    protected override CellValue Over(List<double> numbers) => CellValue.Number(numbers.Count);
}

internal sealed class NumericAggregation(PivotAggregation kind, Func<List<double>, CellValue> compute) : NumberAggregation(kind)
{
    protected override CellValue Over(List<double> numbers) => numbers.Count == 0 ? CellValue.Empty : compute(numbers);
}

internal sealed class Spread(PivotAggregation kind, bool population, bool root) : NumberAggregation(kind)
{
    protected override CellValue Over(List<double> numbers)
    {
        if (numbers.Count == 0) return CellValue.Empty;
        if (!population && numbers.Count < 2) return CellValue.FromError(CellError.DivideByZero);

        var variance = SpreadsheetMath.Variance(numbers, population);
        return CellValue.Number(root ? Math.Sqrt(variance) : variance);
    }
}
