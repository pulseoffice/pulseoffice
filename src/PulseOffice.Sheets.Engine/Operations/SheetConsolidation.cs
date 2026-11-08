using PulseOffice.Sheets;
using PulseOffice.Sheets.Engine.Evaluation.Functions.Numerics;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Operations;

public sealed record ConsolidationSource(int SheetIndex, CellRange Range);

public sealed record ConsolidationRequest(IReadOnlyList<ConsolidationSource> Sources, CellAddress Destination)
{
    public SubtotalFunction Function { get; init; } = SubtotalFunction.Sum;

    public bool LabelsInFirstRow { get; init; }

    public bool LabelsInFirstColumn { get; init; }
}

public sealed record ConsolidationPlan(IReadOnlyDictionary<CellAddress, CellValue> Values);

public interface ISheetConsolidation
{
    ConsolidationPlan Plan(Workbook workbook, ConsolidationRequest request);
}

public sealed class SheetConsolidation : ISheetConsolidation
{
    private readonly ConsolidationCombiners _combiners = new();
    private readonly ISpreadsheetLocale _locale;

    public SheetConsolidation() : this(new SystemSpreadsheetLocale())
    {
    }

    public SheetConsolidation(ISpreadsheetLocale locale)
    {
        _locale = locale ?? throw new ArgumentNullException(nameof(locale));
    }

    public ConsolidationPlan Plan(Workbook workbook, ConsolidationRequest request)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(request);

        var values = new Dictionary<CellAddress, CellValue>();
        if (request.Sources.Count == 0) return new ConsolidationPlan(values);

        return request.LabelsInFirstRow || request.LabelsInFirstColumn
            ? new ConsolidationPlan(ByLabel(workbook, request))
            : new ConsolidationPlan(ByPosition(workbook, request));
    }

    private Dictionary<CellAddress, CellValue> ByPosition(
        Workbook workbook, ConsolidationRequest request)
    {
        var buckets = new Dictionary<(int Row, int Column), List<CellValue>>();

        foreach (var source in request.Sources)
        {
            var sheet = workbook.Sheets[source.SheetIndex];
            if (Used(sheet, source.Range) is not { } used) continue;

            for (var row = used.Start.Row; row <= used.End.Row; row++)
                for (var column = used.Start.Column; column <= used.End.Column; column++)
                {
                    var value = sheet.GetValue(new CellAddress(row, column));
                    if (value.IsEmpty) continue;

                    var key = (row - source.Range.Start.Row, column - source.Range.Start.Column);

                    if (!buckets.TryGetValue(key, out var list)) buckets[key] = list = [];
                    list.Add(value);
                }
        }

        return buckets.ToDictionary(
            pair => new CellAddress(
                request.Destination.Row + pair.Key.Row, request.Destination.Column + pair.Key.Column),
            pair => _combiners.Combine(pair.Value, request.Function));
    }

    private Dictionary<CellAddress, CellValue> ByLabel(Workbook workbook, ConsolidationRequest request)
    {
        var rowLabels = new List<string>();
        var columnLabels = new List<string>();
        var buckets = new Dictionary<(string Row, string Column), List<CellValue>>();

        foreach (var source in request.Sources)
        {
            var sheet = workbook.Sheets[source.SheetIndex];
            var range = source.Range;

            var firstRow = request.LabelsInFirstRow ? range.Start.Row + 1 : range.Start.Row;
            var firstColumn = request.LabelsInFirstColumn ? range.Start.Column + 1 : range.Start.Column;
            if (Used(sheet, range) is not { } used) continue;

            for (var row = firstRow; row <= used.End.Row; row++)
                for (var column = firstColumn; column <= used.End.Column; column++)
                {
                    var value = sheet.GetValue(new CellAddress(row, column));
                    if (value.IsEmpty) continue;

                    var rowLabel = request.LabelsInFirstColumn
                        ? Label(sheet, row, range.Start.Column)
                        : (row - firstRow).ToString(System.Globalization.CultureInfo.InvariantCulture);

                    var columnLabel = request.LabelsInFirstRow
                        ? Label(sheet, range.Start.Row, column)
                        : (column - firstColumn).ToString(System.Globalization.CultureInfo.InvariantCulture);

                    rowLabel = Canonical(rowLabels, rowLabel);
                    columnLabel = Canonical(columnLabels, columnLabel);

                    var key = (rowLabel, columnLabel);
                    if (!buckets.TryGetValue(key, out var list)) buckets[key] = list = [];

                    list.Add(value);
                }
        }

        var values = new Dictionary<CellAddress, CellValue>();
        var origin = request.Destination;
        var rowOffset = request.LabelsInFirstRow ? 1 : 0;
        var columnOffset = request.LabelsInFirstColumn ? 1 : 0;

        if (request.LabelsInFirstRow)
            for (var index = 0; index < columnLabels.Count; index++)
                values[new CellAddress(origin.Row, origin.Column + index + columnOffset)] =
                    CellValue.Text(columnLabels[index]);

        if (request.LabelsInFirstColumn)
            for (var index = 0; index < rowLabels.Count; index++)
                values[new CellAddress(origin.Row + index + rowOffset, origin.Column)] =
                    CellValue.Text(rowLabels[index]);

        foreach (var ((rowLabel, columnLabel), cells) in buckets)
        {
            var row = origin.Row + rowLabels.IndexOf(rowLabel) + rowOffset;
            var column = origin.Column + columnLabels.IndexOf(columnLabel) + columnOffset;

            values[new CellAddress(row, column)] = _combiners.Combine(cells, request.Function);
        }

        return values;
    }

    private string Canonical(List<string> labels, string label)
    {
        var known = labels.Find(candidate => _locale.Culture.CompareInfo.Compare(candidate, label, System.Globalization.CompareOptions.IgnoreCase) == 0);
        if (known is not null) return known;

        labels.Add(label);
        return label;
    }

    private CellRange? Used(Worksheet sheet, CellRange range)
    {
        var bottom = Math.Min(range.End.Row, sheet.LastUsedRow);
        var right = Math.Min(range.End.Column, sheet.LastUsedColumn);
        return bottom < range.Start.Row || right < range.Start.Column ? null : new CellRange(range.Start, new CellAddress(bottom, right));
    }

    private string Label(Worksheet sheet, int row, int column)
    {
        var value = sheet.GetValue(new CellAddress(row, column));
        return value.Kind == ValueKind.Text ? value.AsText() : value.ToString();
    }
}

internal sealed class ConsolidationCombiners
{
    private readonly IReadOnlyDictionary<SubtotalFunction, ConsolidationCombiner> _combiners = new ConsolidationCombiner[]
    {
        new CellCountCombiner(),
        new NumberCombiner(SubtotalFunction.CountNumbers, numbers => numbers.Count, allowEmpty: true),
        new NumberCombiner(SubtotalFunction.Sum, numbers => numbers.Sum()),
        new NumberCombiner(SubtotalFunction.Average, numbers => numbers.Average()),
        new NumberCombiner(SubtotalFunction.Max, numbers => numbers.Max()),
        new NumberCombiner(SubtotalFunction.Min, numbers => numbers.Min()),
        new NumberCombiner(SubtotalFunction.Product, numbers => numbers.Aggregate(1.0, (a, b) => a * b)),
        new SpreadCombiner(SubtotalFunction.StandardDeviation, population: false, root: true),
        new SpreadCombiner(SubtotalFunction.StandardDeviationPopulation, population: true, root: true),
        new SpreadCombiner(SubtotalFunction.Variance, population: false, root: false),
        new SpreadCombiner(SubtotalFunction.VariancePopulation, population: true, root: false),
    }.ToDictionary(combiner => combiner.Function);

    public CellValue Combine(IReadOnlyList<CellValue> cells, SubtotalFunction function) =>
        (_combiners.TryGetValue(function, out var combiner) ? combiner : _combiners[SubtotalFunction.Sum]).Combine(cells);
}

internal abstract class ConsolidationCombiner(SubtotalFunction function)
{
    public SubtotalFunction Function => function;

    public abstract CellValue Combine(IReadOnlyList<CellValue> cells);
}

internal sealed class CellCountCombiner() : ConsolidationCombiner(SubtotalFunction.Count)
{
    public override CellValue Combine(IReadOnlyList<CellValue> cells) =>
        CellValue.Number(cells.Count(cell => !cell.IsEmpty));
}

internal class NumberCombiner(SubtotalFunction function, Func<List<double>, double> compute, bool allowEmpty = false)
    : ConsolidationCombiner(function)
{
    public override CellValue Combine(IReadOnlyList<CellValue> cells)
    {
        var numbers = cells
            .Where(cell => cell.Kind == ValueKind.Number)
            .Select(cell => cell.AsNumber())
            .ToList();

        return numbers.Count == 0 && !allowEmpty ? CellValue.Empty : Over(numbers);
    }

    protected virtual CellValue Over(List<double> numbers) => CellValue.Number(compute(numbers));
}

internal sealed class SpreadCombiner(SubtotalFunction function, bool population, bool root)
    : NumberCombiner(function, _ => 0)
{
    protected override CellValue Over(List<double> numbers)
    {
        if (!population && numbers.Count < 2) return CellValue.FromError(CellError.DivideByZero);

        var variance = SpreadsheetMath.Variance(numbers, population);
        return CellValue.Number(root ? Math.Sqrt(variance) : variance);
    }
}
