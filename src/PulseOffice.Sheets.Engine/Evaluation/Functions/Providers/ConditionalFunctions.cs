using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using F = PulseOffice.Sheets.Engine.Evaluation.Functions.FunctionBuilder;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Providers;

public sealed class ConditionalFunctions(IRangeMaterializer ranges, NumericText numbers) : IFunctionProvider
{
    public string Category => "Statistica";

    public IEnumerable<FunctionDefinition> GetFunctions() => new FunctionBuilder(Category)
        .Lifted("COUNTIF", 2, 2, index => index == 1, r => Count(r, SingleCondition(r, rangeIndex: 0, criteriaIndex: 1)))
        .Lifted("SUMIF", 2, 3, index => index == 1, r => Sum(r, SingleCondition(r, 0, 1), sumRangeIndex: 2, expands: true))
        .Lifted("AVERAGEIF", 2, 3, index => index == 1, r => Average(r, SingleCondition(r, 0, 1), 2, expands: true))

        .Lifted("COUNTIFS", 2, int.MaxValue, index => index % 2 == 1, r => Count(r, PairedConditions(r, from: 0)))
        .Lifted("SUMIFS", 3, int.MaxValue, index => index > 0 && index % 2 == 0, r => Sum(r, PairedConditions(r, from: 1), sumRangeIndex: 0))
        .Lifted("AVERAGEIFS", 3, int.MaxValue, index => index > 0 && index % 2 == 0, r => Average(r, PairedConditions(r, from: 1), 0))

        .Lifted("MAXIFS", 3, int.MaxValue, index => index > 0 && index % 2 == 0, r => Extreme(r, PairedConditions(r, from: 1), largest: true))
        .Lifted("MINIFS", 3, int.MaxValue, index => index > 0 && index % 2 == 0, r => Extreme(r, PairedConditions(r, from: 1), largest: false))

        .Build();

    private sealed record Condition(ValueGrid Range, CriteriaMatcher Matcher);

    private List<Condition> SingleCondition(ArgumentReader r, int rangeIndex, int criteriaIndex) =>
        [new(ranges.Materialize(r.Raw(rangeIndex), r.Context),
             CriteriaMatcher.Parse(r.Value(criteriaIndex), numbers, r.Context.TextRules))];

    private List<Condition> PairedConditions(ArgumentReader r, int from)
    {
        var conditions = new List<Condition>();
        for (var i = from; i + 1 < r.Count; i += 2)
            conditions.Add(new Condition(
                ranges.Materialize(r.Raw(i), r.Context),
                CriteriaMatcher.Parse(r.Value(i + 1), numbers, r.Context.TextRules)));
        return conditions;
    }

    private sealed record Matches(List<int> Positions, long Size, int TailStart, bool TailMatches)
    {
        public long Count => Positions.Count + (TailMatches ? Size - TailStart : 0);

        public IEnumerable<int> UpTo(int end)
        {
            foreach (var position in Positions) yield return position;
            if (!TailMatches) yield break;
            for (var position = TailStart; position < Math.Min(end, Size); position++) yield return position;
        }
    }

    private static Matches? MatchingPositions(IReadOnlyList<Condition> conditions)
    {
        if (conditions.Count == 0) return null;

        var first = conditions[0].Range;
        var size = (long)first.RowCount * first.ColumnCount;
        var columns = first.ColumnCount;

        foreach (var condition in conditions)
            if (condition.Range.RowCount != first.RowCount || condition.Range.ColumnCount != columns)
                return null;

        var tailStart = conditions.Max(condition => condition.Range.StoredRowCount) * columns;
        var positions = new List<int>();
        for (var i = 0; i < Math.Min(tailStart, size); i++)
        {
            var all = true;
            for (var c = 0; c < conditions.Count && all; c++)
                all = conditions[c].Matcher.Matches(conditions[c].Range[i / columns, i % columns]);
            if (all) positions.Add(i);
        }
        var tailMatches = tailStart < size && conditions.All(condition => condition.Matcher.Matches(CellValue.Empty));
        return new Matches(positions, size, tailStart, tailMatches);
    }

    private static EvalValue SumRange(EvalValue raw, ValueGrid criteria, bool expands)
    {
        if (!expands || raw is not RangeValue range) return raw;

        var start = range.Range.Start;
        var end = new CellAddress(start.Row + criteria.RowCount - 1, start.Column + criteria.ColumnCount - 1);
        return range with { Range = new CellRange(start, end) };
    }

    private CellValue Count(ArgumentReader r, IReadOnlyList<Condition> conditions)
    {
        if (r.HasError) return r.Error;
        var matches = MatchingPositions(conditions);
        return matches is null ? F.Invalid() : CellValue.Number(matches.Count);
    }

    private CellValue Sum(ArgumentReader r, IReadOnlyList<Condition> conditions,
                          int sumRangeIndex, bool expands = false)
    {
        var values = SelectedNumbers(r, conditions, sumRangeIndex, expands);
        if (r.HasError) return r.Error;
        return values is null ? F.Invalid() : F.Number(values.Sum());
    }

    private CellValue Average(ArgumentReader r, IReadOnlyList<Condition> conditions,
                              int sumRangeIndex, bool expands = false)
    {
        var values = SelectedNumbers(r, conditions, sumRangeIndex, expands);
        if (r.HasError) return r.Error;
        if (values is null) return F.Invalid();
        return values.Count == 0 ? F.DivideByZero() : F.Number(values.Average());
    }

    private CellValue Extreme(ArgumentReader r, IReadOnlyList<Condition> conditions, bool largest)
    {
        var values = SelectedNumbers(r, conditions, sumRangeIndex: 0);
        if (r.HasError) return r.Error;
        if (values is null) return F.Invalid();

        return CellValue.Number(values.Count == 0 ? 0 : largest ? values.Max() : values.Min());
    }

    private List<double>? SelectedNumbers(
    ArgumentReader r, IReadOnlyList<Condition> conditions, int sumRangeIndex,
    bool expands = false)
    {
        if (r.HasError) return null;

        var matches = MatchingPositions(conditions);
        if (matches is null) return null;

        var source = r.Has(sumRangeIndex)
            ? ranges.Materialize(SumRange(r.Raw(sumRangeIndex), conditions[0].Range, expands), r.Context)
            : conditions[0].Range;
        if (!expands && (source.RowCount != conditions[0].Range.RowCount || source.ColumnCount != conditions[0].Range.ColumnCount))
            return null;

        var selected = new List<double>();
        var columns = conditions[0].Range.ColumnCount;
        foreach (var position in matches.UpTo(source.StoredRowCount * columns))
        {
            var (row, column) = (position / columns, position % columns);
            if (row >= source.RowCount || column >= source.ColumnCount) continue;
            var value = source[row, column];
            if (value.IsError) { r.Fail(value.Error); return null; }
            if (value.Kind == ValueKind.Number) selected.Add(value.AsNumber());
        }
        return selected;
    }
}
