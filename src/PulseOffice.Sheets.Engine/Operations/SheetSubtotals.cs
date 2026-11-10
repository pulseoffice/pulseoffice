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
    public IReadOnlyList<int> ThenGroupColumns { get; init; } = [];

    public IReadOnlyList<int> GroupColumns => [GroupColumn, .. ThenGroupColumns.Where(column => column != GroupColumn).Distinct()];

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

    public int Level { get; init; } = 1;

    public int LabelColumn { get; init; }

    public int Line { get; init; }

    public int ContentStart { get; init; }

    public int ContentEnd { get; init; }
}

public sealed record SubtotalPlan(IReadOnlyList<SubtotalRow> Rows)
{
    public int LabelColumn { get; init; }

    public int FirstRow { get; init; }

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
        var ordered = plan.Rows.OrderBy(row => row.Line).ToList();
        foreach (var row in ordered)
            _structure.InsertRows(engine, sheetIndex, plan.FirstRow + row.Line, 1);
        foreach (var row in ordered)
        {
            var at = plan.FirstRow + row.Line;
            engine.SetValue(new SheetCell(sheetIndex, new CellAddress(at, row.LabelColumn)), CellValue.Text(row.Label));
            foreach (var column in plan.ValueColumns)
                engine.SetFormula(new SheetCell(sheetIndex, new CellAddress(at, column)),
                    Formula(plan.Functions[column], column, plan.FirstRow + row.ContentStart, plan.FirstRow + row.ContentEnd));
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
        if (plan.Outline)
        {
            sheet.OutlineSummary = sheet.OutlineSummary with { Below = plan.SummaryBelow };
            foreach (var row in plan.Rows.OrderBy(row => row.Level).ThenBy(row => row.Line))
                _outline.Group(sheet, StructureAxis.Rows, plan.FirstRow + row.ContentStart, plan.FirstRow + row.ContentEnd);
        }
        if (plan.PageBreaks)
            foreach (var group in groups.Where(row => row.Level == 1).OrderBy(row => row.Line).Skip(1))
                sheet.RowBreaks.Add(plan.FirstRow + Math.Min(group.Line, group.ContentStart));
    }

    public SubtotalPlan Plan(Worksheet sheet, SubtotalRequest request)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(request);

        var rows = new List<SubtotalRow>();
        var first = request.HasHeader ? request.Range.Start.Row + 1 : request.Range.Start.Row;
        var last = request.Range.End.Row;
        var levels = request.GroupColumns;
        var shape = new SubtotalPlan(rows)
        {
            LabelColumn = LabelColumn(request, levels[0]),
            FirstRow = first,
            SummaryBelow = request.SummaryBelow,
            PageBreaks = request.PageBreaks,
            Outline = request.Outline,
            ValueColumns = [.. request.ValueColumns.Distinct()],
            Functions = request.ValueColumns.Distinct().ToDictionary(column => column, request.FunctionOf),
        };

        if (last < first) return shape;

        var lines = new SubtotalLines();
        if (request.GrandTotal && !request.SummaryBelow) lines.Reserve();
        var grandStart = lines.Count;
        Emit(sheet, request, shape, lines, 0, first, last);
        if (lines.Summaries == 0) return shape;
        if (request.GrandTotal)
        {
            var line = request.SummaryBelow ? lines.Count : 0;
            if (request.SummaryBelow) lines.Reserve();
            rows.Add(new SubtotalRow(request.SummaryBelow ? last + 1 : first, request.GrandTotalLabel, first, last, Formulas(shape, first, last))
            {
                IsGrandTotal = true,
                Level = 0,
                LabelColumn = shape.LabelColumn,
                Line = line,
                ContentStart = grandStart,
                ContentEnd = request.SummaryBelow ? line - 1 : lines.Count - 1,
            });
        }
        rows.InsertRange(0, lines.Rows);
        rows.Sort((left, right) => left.Line.CompareTo(right.Line));

        return shape;
    }

    private void Emit(Worksheet sheet, SubtotalRequest request, SubtotalPlan shape, SubtotalLines lines, int depth, int first, int last)
    {
        var levels = request.GroupColumns;
        var column = levels[depth];
        var runStart = first;
        var runKey = KeyOf(sheet, first, column);

        for (var row = first + 1; row <= last + 1; row++)
        {
            var key = row <= last ? KeyOf(sheet, row, column) : null;
            if (key is not null && Same(key, runKey, request.CaseSensitive)) continue;

            var line = request.SummaryBelow ? -1 : lines.Reserve();
            var contentStart = lines.Count;
            if (depth + 1 < levels.Count) Emit(sheet, request, shape, lines, depth + 1, runStart, row - 1);
            else lines.Data(row - runStart);
            var contentEnd = lines.Count - 1;
            if (request.SummaryBelow) line = lines.Reserve();

            lines.Add(new SubtotalRow(
                request.SummaryBelow ? row : runStart,
                string.Format(_locale.Culture, request.GroupLabelFormat, runKey, request.FunctionLabel),
                runStart, row - 1,
                Formulas(shape, runStart, row - 1))
            {
                Level = depth + 1,
                LabelColumn = LabelColumn(request, column),
                Line = line,
                ContentStart = contentStart,
                ContentEnd = contentEnd,
            });

            runStart = row;
            runKey = key;
        }
    }

    private int LabelColumn(SubtotalRequest request, int groupColumn) =>
        request.ValueColumns.Contains(groupColumn) ? request.Range.Start.Column : groupColumn;

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

internal sealed class SubtotalLines
{
    private readonly List<SubtotalRow> _rows = [];

    public int Count { get; private set; }

    public int Summaries => _rows.Count;

    public IReadOnlyList<SubtotalRow> Rows => _rows;

    public int Reserve() => Count++;

    public void Data(int count) => Count += count;

    public void Add(SubtotalRow row) => _rows.Add(row);
}
