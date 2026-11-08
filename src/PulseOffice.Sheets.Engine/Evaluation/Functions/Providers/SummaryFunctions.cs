using PulseOffice.Sheets.Engine.Evaluation.Functions.Numerics;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using M = System.Math;
using F = PulseOffice.Sheets.Engine.Evaluation.Functions.FunctionBuilder;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Providers;

public sealed class SummaryFunctions(IRangeMaterializer ranges) : IFunctionProvider
{
    public string Category => "Statistica";

    private static readonly Dictionary<int, Func<Sample, CellValue>> Operations = new()
    {
        [1] = s => s.Numbers.Count == 0 ? F.DivideByZero() : F.Number(s.Numbers.Average()),
        [2] = s => CellValue.Number(s.Numbers.Count),
        [3] = s => CellValue.Number(s.NonEmpty),
        [4] = s => CellValue.Number(s.Numbers.Count == 0 ? 0 : s.Numbers.Max()),
        [5] = s => CellValue.Number(s.Numbers.Count == 0 ? 0 : s.Numbers.Min()),
        [6] = s => F.Number(s.Numbers.Count == 0 ? 0 : s.Numbers.Aggregate(1.0, (a, b) => a * b)),
        [7] = s => s.Numbers.Count < 2 ? F.DivideByZero()
            : F.Number(SpreadsheetMath.StandardDeviation(s.Numbers, population: false)),
        [8] = s => s.Numbers.Count < 1 ? F.DivideByZero()
            : F.Number(SpreadsheetMath.StandardDeviation(s.Numbers, population: true)),
        [9] = s => F.Number(s.Numbers.Sum()),
        [10] = s => s.Numbers.Count < 2 ? F.DivideByZero()
            : F.Number(SpreadsheetMath.Variance(s.Numbers, population: false)),
        [11] = s => s.Numbers.Count < 1 ? F.DivideByZero()
            : F.Number(SpreadsheetMath.Variance(s.Numbers, population: true)),
        [12] = s => s.Numbers.Count == 0 ? F.OutOfDomain() : F.Number(SpreadsheetMath.Median(s.Numbers)),
        [13] = s => Mode(s.Numbers),
    };

    private static readonly Dictionary<int, Func<List<double>, double, CellValue>> RankedOperations = new()
    {
        [14] = (values, k) => Ordered(values, k, largest: true),
        [15] = (values, k) => Ordered(values, k, largest: false),
        [16] = (values, k) => k is < 0 or > 1 ? F.OutOfDomain() : F.Number(SpreadsheetMath.Quantile(values, k)),
        [17] = (values, k) => Quartile(values, k, exclusive: false),
        [18] = (values, k) => F.Number(SpreadsheetMath.ExclusiveQuantile(values, k)),
        [19] = (values, k) => Quartile(values, k, exclusive: true),
    };

    private sealed record Sample(List<double> Numbers, int NonEmpty);


    public IEnumerable<FunctionDefinition> GetFunctions() => new FunctionBuilder(Category)
        .Raw("SUBTOTAL", 2, int.MaxValue, r =>
        {
            var code = r.Integer(0);
            if (r.HasError) return r.Error;

            var operation = code > 100 ? code - 100 : code;
            if (operation > 11 || !Operations.TryGetValue(operation, out var reduce)) return F.Invalid();

            var excluded = code > 100 ? FilteredOrHidden : FilteredOnly;
            var sample = Collect(r, from: 1, to: r.Count, skipNested: true, excluded, out var error);
            return error is { } e ? CellValue.FromError(e) : reduce(sample);
        })

        .Raw("AGGREGATE", 3, int.MaxValue, r =>
        {
            var code = r.Integer(0);
            var options = r.Integer(1);
            if (r.HasError) return r.Error;
            if (options is < 0 or > 7) return F.Invalid();

            var ignoresErrors = options is 2 or 3 or 6 or 7;
            var excluded = options % 2 == 1 ? FilteredOrHidden : NoRows;

            if (RankedOperations.TryGetValue(code, out var ranked))
            {
                if (r.Count != 4) return F.Invalid();
                var k = r.Number(3);
                if (r.HasError) return r.Error;

                var data = Collect(r, from: 2, to: 3, skipNested: options <= 3, excluded, out var rankedError);
                if (rankedError is { } re && !ignoresErrors) return CellValue.FromError(re);
                return data.Numbers.Count == 0 ? F.OutOfDomain() : ranked(data.Numbers, k);
            }

            if (!Operations.TryGetValue(code, out var reduce)) return F.Invalid();

            var sample = Collect(r, from: 2, to: r.Count, skipNested: options <= 3, excluded, out var error);
            if (error is { } e && !ignoresErrors) return CellValue.FromError(e);
            return reduce(sample);
        })

        .Build();

    private static readonly IReadOnlySet<RowVisibility> NoRows = new HashSet<RowVisibility>();

    private static readonly IReadOnlySet<RowVisibility> FilteredOnly = new HashSet<RowVisibility> { RowVisibility.Filtered };

    private static readonly IReadOnlySet<RowVisibility> FilteredOrHidden = new HashSet<RowVisibility> { RowVisibility.Filtered, RowVisibility.Hidden };

    private Sample Collect(ArgumentReader r, int from, int to, bool skipNested, IReadOnlySet<RowVisibility> excluded, out CellError? error)
    {
        error = null;
        var values = new List<double>();
        var nonEmpty = 0;

        for (var i = from; i < to; i++)
        {
            var raw = r.Raw(i);
            var grid = ranges.Materialize(raw, r.Context);
            var reference = skipNested ? raw as RangeValue : null;
            var source = raw as RangeValue;
            for (var row = 0; row < grid.StoredRowCount && row < grid.RowCount; row++)
            {
                if (source is not null && excluded.Count > 0
                    && excluded.Contains(r.Context.RowVisibilityAt(source.SheetName, source.Range.Start.Row + row))) continue;
                for (var column = 0; column < grid.ColumnCount; column++)
                {
                    var value = grid[row, column];
                    if (value.IsEmpty) continue;
                    if (reference is not null && IsNestedSummary(r, reference, row, column)) continue;
                    if (value.IsError) { error ??= value.Error; continue; }
                    nonEmpty++;
                    if (value.Kind == ValueKind.Number) values.Add(value.AsNumber());
                }
            }
        }

        return new Sample(values, nonEmpty);
    }

    private static bool IsNestedSummary(ArgumentReader r, RangeValue reference, int row, int column)
    {
        var start = reference.Range.Start;
        var formula = r.Context.GetFormulaText(reference.SheetName, new CellAddress(start.Row + row, start.Column + column));
        return formula is not null && NestedSummary.IsMatch(formula);
    }

    private static readonly System.Text.RegularExpressions.Regex NestedSummary = new(
        @"\b(SUBTOTAL|AGGREGATE)\s*\(",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    private static CellValue Mode(List<double> values)
    {
        var groups = values.GroupBy(v => v)
                           .Select(g => (Value: g.Key, Count: g.Count()))
                           .OrderByDescending(g => g.Count)
                           .ToList();

        return groups.Count == 0 || groups[0].Count < 2
            ? CellValue.FromError(CellError.NotAvailable)
            : CellValue.Number(groups[0].Value);
    }

    private static CellValue Ordered(List<double> values, double k, bool largest)
    {
        var rank = (int)M.Ceiling(k);
        if (rank < 1 || rank > values.Count) return F.OutOfDomain();

        var sorted = values.Order().ToList();
        return CellValue.Number(largest ? sorted[^rank] : sorted[rank - 1]);
    }

    private static CellValue Quartile(List<double> values, double k, bool exclusive)
    {
        var quarter = M.Truncate(k);
        if (exclusive ? quarter is < 1 or > 3 : quarter is < 0 or > 4) return F.OutOfDomain();
        return F.Number(exclusive
            ? SpreadsheetMath.ExclusiveQuantile(values, quarter / 4)
            : SpreadsheetMath.Quantile(values, quarter / 4));
    }
}
