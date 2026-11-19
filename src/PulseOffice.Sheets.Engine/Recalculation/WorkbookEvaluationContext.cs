using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Evaluation;
using PulseOffice.Sheets.Engine.Evaluation.Formats;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.Sheets.Engine.Parsing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Recalculation;

internal sealed class WorkbookEvaluationContext(CalculationEngine engine, SheetCell current) : IEvaluationContext
{
    public SheetCell CurrentCell => current;

    public Evaluation.Functions.NumberToText NumberText => engine.NumberText;

    public Evaluation.Functions.NumericText TextNumbers => engine.TextNumbers;

    public bool ImplicitIntersection { get; } = engine.SheetAt(current.SheetIndex)?[current.Address] is { ImplicitIntersection: true, IsPartOfArrayFormula: false };

    public bool UsedArrays { get; private set; }

    public Evaluation.Functions.TextRules TextRules => engine.TextRules;

    public void NoteArrayUse()
    {
        if (!ImplicitIntersection) UsedArrays = true;
    }

    public CellValue GetCellValue(string? sheetName, CellAddress address)
    {
        var index = ResolveSheetIndex(sheetName);
        if (index < 0) return CellValue.FromError(CellError.Reference);
        return engine.GetValue(new SheetCell(index, address));
    }

    public CellRange? ContentWithin(string? sheetName, CellRange range)
    {
        var index = ResolveSheetIndex(sheetName);
        if (index < 0 || engine.SheetAt(index) is not { } sheet) return range;
        var lastRow = Math.Min(range.End.Row, Math.Max(sheet.LastUsedRow, engine.SpillBottom(index)));
        var lastColumn = Math.Min(range.End.Column, Math.Max(sheet.LastUsedColumn, engine.SpillRight(index)));
        if (lastRow < range.Start.Row || lastColumn < range.Start.Column) return null;
        return new CellRange(range.Start, new CellAddress(lastRow, lastColumn));
    }

    public IEnumerable<CellValue> GetRangeValues(string? sheetName, CellRange range)
    {
        var index = ResolveSheetIndex(sheetName);
        if (index < 0)
        {
            yield return CellValue.FromError(CellError.Reference);
            yield break;
        }

        var sheet = engine.SheetAt(index);
        if (sheet is null) yield break;

        var lastRow = Math.Max(sheet.LastUsedRow, engine.SpillBottom(index));
        var lastColumn = Math.Max(sheet.LastUsedColumn, engine.SpillRight(index));
        for (var row = range.Start.Row; row <= range.End.Row; row++)
        {
            if (row > lastRow)
            {
                for (long rest = (long)(range.End.Row - row + 1) * range.ColumnCount; rest > 0; rest--) yield return CellValue.Empty;
                yield break;
            }
            for (var column = range.Start.Column; column <= range.End.Column; column++)
                yield return column > lastColumn
                    ? CellValue.Empty
                    : engine.GetValue(new SheetCell(index, new CellAddress(row, column)));
        }
    }

    public string? ResolveName(string name) =>
        engine.Workbook.ResolveName(name, engine.SheetAt(current.SheetIndex)?.Name)?.Expression
        ?? DatabaseRangeExpression(name)
        ?? LabelExpression(name);

    private string? LabelExpression(string name)
    {
        if (engine.SheetAt(current.SheetIndex) is not { } sheet
            || engine.Labels.Find(sheet, name, engine.Workbook.Calculation.AutomaticFindLabels) is not { } range) return null;
        return $"{range.Start.ToA1(true, true)}:{range.End.ToA1(true, true)}";
    }

    private string? DatabaseRangeExpression(string name)
    {
        if (engine.Workbook.DatabaseRanges.Find(name) is not { } database || engine.SheetAt(database.SheetIndex) is not { } sheet) return null;
        var range = database.Range;
        return $"'{sheet.Name.Replace("'", "''", StringComparison.Ordinal)}'.{range.Start.ToA1(true, true)}:{range.End.ToA1(true, true)}";
    }

    public FormulaNode? ResolveTableReference(TableReferenceNode node) => engine.Tables.Reference(engine.Workbook, node, current);

    public bool SheetExists(string sheetName) => engine.Workbook.FindSheet(sheetName) is not null;

    public string CurrentSheetName => engine.SheetAt(current.SheetIndex)?.Name ?? string.Empty;

    public int SheetCount => engine.Workbook.Sheets.Count;

    public int SheetIndex(string? sheetName)
    {
        if (sheetName is null) return current.SheetIndex + 1;
        var sheet = engine.Workbook.FindSheet(sheetName);
        return sheet is null ? 0 : engine.Workbook.IndexOf(sheet) + 1;
    }

    public IReadOnlyList<string> SheetsBetween(string firstSheet, string lastSheet) => engine.SheetsBetween(firstSheet, lastSheet);

    public CellRange? ArrayArea => engine.SheetAt(current.SheetIndex)?[current.Address].ArrayFormulaRange;

    public string? GetFormulaText(string? sheetName, CellAddress address)
    {
        var index = ResolveSheetIndex(sheetName);
        if (index < 0 || engine.SheetAt(index) is not { } sheet) return null;
        var cell = sheet[address];
        if (cell.ArrayFormulaRange is not { } array) return cell.Formula;
        var anchor = sheet[array.Start].Formula;
        return anchor is null ? null : "{" + (anchor.StartsWith('=') ? anchor : "=" + anchor) + "}";
    }

    public int GetStyle(string? sheetName, CellAddress address)
    {
        var index = ResolveSheetIndex(sheetName);
        return index < 0 ? 0 : engine.SheetAt(index)?.StyleIdAt(address) ?? 0;
    }

    public RowVisibility RowVisibilityAt(string? sheetName, int row)
    {
        var index = ResolveSheetIndex(sheetName);
        if (index < 0 || engine.SheetAt(index) is not { } sheet || !sheet.Layout.IsRowHidden(row)) return RowVisibility.Visible;
        return sheet.Layout.IsRowFiltered(row)
            || sheet.AutoFilter is { Range: { } filtered, Filters.Count: > 0 } && filtered.Contains(new CellAddress(row, filtered.Start.Column))
            ? RowVisibility.Filtered
            : RowVisibility.Hidden;
    }

    public CellRange? GetSpillRange(string? sheetName, CellAddress anchor)
    {
        var index = ResolveSheetIndex(sheetName);
        if (index < 0) return null;

        var cell = new SheetCell(index, anchor);
        engine.GetValue(cell);
        return engine.SpillRanges.TryGetValue(cell, out var range)
            ? range : null;
    }

    public CellValue EvaluateWith(
        SheetCell formula, IReadOnlyList<(SheetCell Cell, CellValue Value)> inputs) =>
        engine.EvaluateSubstituted(formula, inputs);

    public void RequestStyle(string styleName) => engine.RequestStyle(current, styleName);

    public CellValue LookupPivot(
        string? sheetName, CellAddress target, string dataField,
        IReadOnlyList<(string Field, string Item)> filters)
    {
        var index = ResolveSheetIndex(sheetName);
        if (index < 0) return CellValue.FromError(CellError.Reference);

        return engine.Pivots?.Lookup(index, target, dataField, filters)
            ?? CellValue.FromError(CellError.Reference);
    }

    public string? FetchResource(string url) => engine.Web?.Fetch(url);

    public CellValue EvaluateOffset(string? sheetName, CellAddress formula, int rowDelta, int columnDelta)
    {
        var index = ResolveSheetIndex(sheetName);
        return index < 0
            ? CellValue.FromError(CellError.Reference)
            : engine.EvaluateOffset(new SheetCell(index, formula), rowDelta, columnDelta);
    }

    private int ResolveSheetIndex(string? sheetName)
    {
        if (sheetName is null) return current.SheetIndex;
        var sheet = engine.Workbook.FindSheet(sheetName);
        return sheet is null ? -1 : engine.Workbook.IndexOf(sheet);
    }
}
