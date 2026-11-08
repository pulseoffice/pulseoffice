using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Operations;

public enum SubtotalFunction
{
    Average = 1, CountNumbers = 2, Count = 3, Max = 4, Min = 5,
    Product = 6, StandardDeviation = 7, StandardDeviationPopulation = 8,
    Sum = 9, Variance = 10, VariancePopulation = 11,
}

public sealed record SubtotalRequest(CellRange Range, int GroupColumn, IReadOnlyList<int> ValueColumns)
{
    public SubtotalFunction Function { get; init; } = SubtotalFunction.Sum;

    public IReadOnlyDictionary<int, SubtotalFunction> ColumnFunctions { get; init; } = new Dictionary<int, SubtotalFunction>();

    public bool HasHeader { get; init; } = true;

    public bool GrandTotal { get; init; } = true;

    public bool CaseSensitive { get; init; }

    public bool ReplaceCurrent { get; init; }

    public bool PageBreaks { get; init; }

    public bool SummaryBelow { get; init; } = true;

    public bool Outline { get; init; }

    public string GroupLabelFormat { get; init; } = "{0} - {1}";

    public string FunctionLabel { get; init; } = "";

    public string GrandTotalLabel { get; init; } = "";

    public SubtotalFunction FunctionOf(int column) => ColumnFunctions.TryGetValue(column, out var function) ? function : Function;
}

public sealed record SubtotalRow(int InsertAt, string Label, int First, int Last, IReadOnlyDictionary<int, string> Formulas)
{
    public bool IsGrandTotal { get; init; }
}

public sealed record SubtotalPlan(IReadOnlyList<SubtotalRow> Rows)
{
    public int LabelColumn { get; init; }

    public bool SummaryBelow { get; init; } = true;

    public bool PageBreaks { get; init; }

    public bool Outline { get; init; }

    public IReadOnlyList<int> ValueColumns { get; init; } = [];

    public IReadOnlyDictionary<int, SubtotalFunction> Functions { get; init; } = new Dictionary<int, SubtotalFunction>();
}

public sealed record SubtotalOutcome(int Removed, int Inserted);

public interface ISheetSubtotals
{
    SubtotalPlan Plan(Worksheet sheet, SubtotalRequest request);

    SubtotalOutcome Apply(CalculationEngine engine, int sheetIndex, SubtotalRequest request);
}

public sealed class SheetSubtotals : ISheetSubtotals
{
    private readonly ISheetStructure _structure;
    private readonly ISpreadsheetLocale _locale;
    private readonly ISheetOutline _outline;

    public SheetSubtotals() : this(new SheetStructure(), new SystemSpreadsheetLocale(), new SheetOutline())
    {
    }

    public SheetSubtotals(ISheetStructure structure, ISpreadsheetLocale locale) : this(structure, locale, new SheetOutline())
    {
    }

    public SheetSubtotals(ISheetStructure structure, ISpreadsheetLocale locale, ISheetOutline outline)
    {
        _structure = structure ?? throw new ArgumentNullException(nameof(structure));
        _locale = locale ?? throw new ArgumentNullException(nameof(locale));
        _outline = outline ?? throw new ArgumentNullException(nameof(outline));
    }

    public SubtotalOutcome Apply(CalculationEngine engine, int sheetIndex, SubtotalRequest request)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(request);
        var sheet = engine.Workbook.Sheets[sheetIndex];
        var removed = request.ReplaceCurrent ? RemoveCurrent(engine, sheetIndex, request) : 0;
        var range = new CellRange(request.Range.Start, new CellAddress(request.Range.End.Row - removed, request.Range.End.Column));
        if (range.End.Row < range.Start.Row) return new SubtotalOutcome(removed, 0);
        var plan = Plan(sheet, request with { Range = range });
        var ordered = plan.SummaryBelow
            ? plan.Rows.OrderByDescending(row => row.InsertAt).ThenByDescending(row => row.IsGrandTotal)
            : plan.Rows.OrderByDescending(row => row.InsertAt).ThenBy(row => row.IsGrandTotal);
        foreach (var row in ordered)
        {
            _structure.InsertRows(engine, sheetIndex, row.InsertAt, 1);
            engine.SetValue(new SheetCell(sheetIndex, new CellAddress(row.InsertAt, plan.LabelColumn)), CellValue.Text(row.Label));
            var first = row.First >= row.InsertAt ? row.First + 1 : row.First;
            var spanned = row.IsGrandTotal && !plan.SummaryBelow ? row.Last + plan.Rows.Count - 1 : row.Last;
            var last = spanned >= row.InsertAt ? spanned + 1 : spanned;
            foreach (var column in plan.ValueColumns)
                engine.SetFormula(new SheetCell(sheetIndex, new CellAddress(row.InsertAt, column)), Formula(plan.Functions[column], column, first, last));
        }
        Arrange(sheet, plan);
        return new SubtotalOutcome(removed, plan.Rows.Count);
    }

    private int RemoveCurrent(CalculationEngine engine, int sheetIndex, SubtotalRequest request)
    {
        var sheet = engine.Workbook.Sheets[sheetIndex];
        var range = request.Range;
        var first = request.HasHeader ? range.Start.Row + 1 : range.Start.Row;
        var rows = Enumerable.Range(first, Math.Max(0, range.End.Row - first + 1))
            .Where(row => IsSubtotalRow(sheet, range, row))
            .ToList();
        _outline.Ungroup(sheet, StructureAxis.Rows, first, range.End.Row);
        sheet.RowBreaks.RemoveWhere(row => row > first && row <= range.End.Row);
        for (var index = rows.Count - 1; index >= 0; index--)
            _structure.DeleteRows(engine, sheetIndex, rows[index], 1);
        return rows.Count;
    }

    private bool IsSubtotalRow(Worksheet sheet, CellRange range, int row)
    {
        for (var column = range.Start.Column; column <= range.End.Column; column++)
            if (sheet[new CellAddress(row, column)].Formula is { Length: > 0 } formula
                && formula.Contains("SUBTOTAL(", StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    private void Arrange(Worksheet sheet, SubtotalPlan plan)
    {
        var groups = plan.Rows.Where(row => !row.IsGrandTotal).ToList();
        if (groups.Count == 0) return;
        var grand = plan.Rows.Any(row => row.IsGrandTotal) ? 1 : 0;
        var above = plan.SummaryBelow ? 0 : 1;
        var lead = plan.SummaryBelow ? 0 : grand;
        var placed = groups
            .Select((group, index) => (Subtotal: plan.SummaryBelow ? group.Last + index + 1 : group.First + lead + index,
                First: group.First + lead + index + above, Last: group.Last + lead + index + above))
            .ToList();
        if (plan.Outline)
        {
            if (grand == 1)
                _outline.Group(sheet, StructureAxis.Rows,
                    Math.Min(placed[0].First, placed[0].Subtotal), Math.Max(placed[^1].Last, placed[^1].Subtotal));
            foreach (var group in placed) _outline.Group(sheet, StructureAxis.Rows, group.First, group.Last);
        }
        if (plan.PageBreaks)
            foreach (var group in placed.Skip(1))
                sheet.RowBreaks.Add(Math.Min(group.First, group.Subtotal));
    }

    public SubtotalPlan Plan(Worksheet sheet, SubtotalRequest request)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(request);

        var rows = new List<SubtotalRow>();
        var first = request.HasHeader ? request.Range.Start.Row + 1 : request.Range.Start.Row;
        var last = request.Range.End.Row;
        var shape = new SubtotalPlan(rows)
        {
            LabelColumn = LabelColumn(request),
            SummaryBelow = request.SummaryBelow,
            PageBreaks = request.PageBreaks,
            Outline = request.Outline,
            ValueColumns = [.. request.ValueColumns.Distinct()],
            Functions = request.ValueColumns.Distinct().ToDictionary(column => column, request.FunctionOf),
        };

        if (last < first) return shape;

        var groupStart = first;
        var groupKey = KeyOf(sheet, first, request.GroupColumn);

        for (var row = first + 1; row <= last + 1; row++)
        {
            var key = row <= last ? KeyOf(sheet, row, request.GroupColumn) : null;

            if (key is not null && Same(key, groupKey, request.CaseSensitive)) continue;

            rows.Add(new SubtotalRow(
                request.SummaryBelow ? row : groupStart,
                string.Format(
                    _locale.Culture,
                    request.GroupLabelFormat, groupKey, request.FunctionLabel),
                groupStart, row - 1,
                Formulas(shape, groupStart, row - 1)));

            groupStart = row;
            groupKey = key;
        }

        if (request.GrandTotal && rows.Count > 0)
            rows.Add(new SubtotalRow(request.SummaryBelow ? last + 1 : first, request.GrandTotalLabel, first, last, Formulas(shape, first, last)) { IsGrandTotal = true });

        return shape;
    }

    private int LabelColumn(SubtotalRequest request) =>
        request.ValueColumns.Contains(request.GroupColumn) ? request.Range.Start.Column : request.GroupColumn;

    private bool Same(string key, string? groupKey, bool caseSensitive) =>
        caseSensitive
            ? string.Equals(key, groupKey, StringComparison.Ordinal)
            : groupKey is not null && _locale.Culture.CompareInfo.Compare(key, groupKey, System.Globalization.CompareOptions.IgnoreCase) == 0;

    private IReadOnlyDictionary<int, string> Formulas(SubtotalPlan plan, int first, int last) =>
        plan.ValueColumns.ToDictionary(column => column, column => Formula(plan.Functions[column], column, first, last));

    private string Formula(SubtotalFunction function, int column, int first, int last) =>
        $"=SUBTOTAL({(int)function};{new CellAddress(first, column).ToA1()}:{new CellAddress(last, column).ToA1()})";

    private string KeyOf(Worksheet sheet, int row, int column)
    {
        var value = sheet.GetValue(new CellAddress(row, column));

        return value.Kind == ValueKind.Text
            ? value.AsText()
            : value.ToString();
    }
}
