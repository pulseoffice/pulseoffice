using PulseOffice.Sheets.Values;
using M = System.Math;
using F = PulseOffice.Sheets.Engine.Evaluation.Functions.FunctionBuilder;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Providers;

public sealed class ModernLookupFunctions(IRangeMaterializer ranges, NumericText numbers) : IFunctionProvider
{
    public string Category => "Ricerca e riferimento";

    private enum MatchMode { Exact = 0, ExactOrNextSmaller = -1, ExactOrNextLarger = 1, Wildcard = 2 }

    public IEnumerable<FunctionDefinition> GetFunctions() => new FunctionBuilder(Category)
        .ArrayResult("XLOOKUP", 3, 6, r =>
        {
            var needle = r.Value(0);
            var haystack = ranges.Materialize(r.Raw(1), r.Context);
            var results = ranges.Materialize(r.Raw(2), r.Context);
            var mode = (MatchMode)r.Integer(4, 0);
            var searchOrder = r.Integer(5, 1);
            if (r.HasError) return EvalValue.Scalar(r.Error);
            if (!Valid(mode, searchOrder) || IsTwoDimensional(haystack)) return EvalValue.Error(CellError.Value);

            var vertical = haystack.ColumnCount == 1;
            var keys = (vertical ? haystack.Column(0) : haystack.Row(0)).ToList();
            if ((vertical ? results.RowCount : results.ColumnCount) != keys.Count) return EvalValue.Error(CellError.Value);

            var index = Find(keys, needle, mode, searchOrder, r.Context.TextRules.ForQueries);

            if (index < 0)

                return r.Has(3)
                    ? EvalValue.Scalar(r.Value(3))
                    : EvalValue.Scalar(CellValue.FromError(CellError.NotAvailable));

            if (vertical && results.ColumnCount > 1)
                return new ArrayValue([[.. results.Row(index)]]);

            if (!vertical && results.RowCount > 1)
                return new ArrayValue([.. results.Column(index).Select(v => (IReadOnlyList<CellValue>)[v])]);

            var values = (vertical ? results.Column(0) : results.Row(0)).ToList();
            return EvalValue.Scalar(index < values.Count
                ? values[index]
                : CellValue.FromError(CellError.Reference));
        })

        .Raw("XMATCH", 2, 4, r =>
        {
            var needle = r.Value(0);
            var haystack = ranges.Materialize(r.Raw(1), r.Context);
            var mode = (MatchMode)r.Integer(2, 0);
            var searchOrder = r.Integer(3, 1);
            if (r.HasError) return r.Error;
            if (!Valid(mode, searchOrder) || IsTwoDimensional(haystack)) return CellValue.FromError(CellError.Value);

            var keys = haystack.InReadingOrder().ToList();
            var index = Find(keys, needle, mode, searchOrder, r.Context.TextRules.ForQueries);

            return index < 0 ? CellValue.FromError(CellError.NotAvailable) : CellValue.Number(index + 1);
        })

        .Build();

    private static bool Valid(MatchMode mode, int searchOrder) =>
        Enum.IsDefined(mode) && searchOrder is 1 or -1 or 2 or -2
        && !(mode == MatchMode.Wildcard && searchOrder is 2 or -2);

    private static bool IsTwoDimensional(ValueGrid grid) => grid.RowCount > 1 && grid.ColumnCount > 1;

    private int Find(IReadOnlyList<CellValue> keys, CellValue needle, MatchMode mode, int searchOrder, TextRules rules) =>
        searchOrder is 2 or -2
            ? BinarySearch(keys, needle, mode, ascending: searchOrder > 0, rules)
            : LinearSearch(keys, needle, mode, reverse: searchOrder < 0, rules);

    private static int BinarySearch(IReadOnlyList<CellValue> keys, CellValue needle, MatchMode mode, bool ascending, TextRules rules)
    {
        var direction = ascending ? 1 : -1;
        var (low, high) = (0, keys.Count);
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (direction * TotalCompare(keys[middle], needle, rules) >= 0) high = middle;
            else low = middle + 1;
        }

        if (low < keys.Count && TotalCompare(keys[low], needle, rules) == 0) return low;

        var candidate = mode switch
        {
            MatchMode.ExactOrNextLarger => ascending ? low : low - 1,
            MatchMode.ExactOrNextSmaller => ascending ? low - 1 : low,
            _ => -1,
        };
        return candidate >= 0 && candidate < keys.Count && TypeRank(keys[candidate]) == TypeRank(needle) ? candidate : -1;
    }

    private static int TotalCompare(CellValue value, CellValue needle, TextRules rules)
    {
        var (valueRank, needleRank) = (TypeRank(value), TypeRank(needle));
        if (valueRank != needleRank) return valueRank.CompareTo(needleRank);
        return valueRank switch
        {
            2 => value.AsBoolean().CompareTo(needle.AsBoolean()),
            _ => Compare(value, needle, rules) ?? 0,
        };
    }

    private static int TypeRank(CellValue value) => value.Kind switch
    {
        ValueKind.Number => 0,
        ValueKind.Text => 1,
        ValueKind.Boolean => 2,
        _ => 3,
    };

    private int LinearSearch(IReadOnlyList<CellValue> keys, CellValue needle, MatchMode mode, bool reverse, TextRules rules)
    {
        var order = reverse
            ? Enumerable.Range(0, keys.Count).Reverse()
            : Enumerable.Range(0, keys.Count);

        if (mode == MatchMode.Wildcard)
        {
            var matcher = CriteriaMatcher.Parse(needle, numbers, rules);
            foreach (var i in order)
                if (matcher.Matches(keys[i])) return i;
            return -1;
        }

        var best = -1;

        foreach (var i in order)
        {
            var comparison = Compare(keys[i], needle, rules);
            if (comparison is null) continue;

            if (comparison == 0) return i;
            if (mode == MatchMode.Exact) continue;

            var acceptable = mode == MatchMode.ExactOrNextSmaller ? comparison < 0 : comparison > 0;
            if (!acceptable) continue;

            if (best < 0 || Compare(keys[i], keys[best], rules) is { } closer
                && (mode == MatchMode.ExactOrNextSmaller ? closer > 0 : closer < 0))
                best = i;
        }
        return best;
    }

    private static int? Compare(CellValue value, CellValue needle, TextRules rules)
    {
        if (value.IsNumeric && needle.IsNumeric)
        {
            var difference = value.AsNumber() - needle.AsNumber();
            return difference == 0 ? 0 : difference > 0 ? 1 : -1;
        }

        if (value.Kind == ValueKind.Text && needle.Kind == ValueKind.Text)
            return M.Sign(string.Compare(value.AsText(), needle.AsText(), rules.Comparison));

        return value.IsEmpty && needle.IsEmpty ? 0 : null;
    }
}
