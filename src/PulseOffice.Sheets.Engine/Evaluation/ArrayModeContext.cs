using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Evaluation;

public sealed class ArrayModeContext(IEvaluationContext inner) : IEvaluationContext
{
    public SheetCell CurrentCell => inner.CurrentCell;

    public Functions.NumberToText NumberText => inner.NumberText;

    public Functions.NumericText TextNumbers => inner.TextNumbers;

    public bool ImplicitIntersection => false;

    public Functions.TextRules TextRules => inner.TextRules;

    public void NoteArrayUse()
    {
    }

    public CellValue GetCellValue(string? sheetName, CellAddress address) => inner.GetCellValue(sheetName, address);

    public IEnumerable<CellValue> GetRangeValues(string? sheetName, CellRange range) => inner.GetRangeValues(sheetName, range);

    public CellRange? ContentWithin(string? sheetName, CellRange range) => inner.ContentWithin(sheetName, range);

    public string? ResolveName(string name) => inner.ResolveName(name);

    public bool SheetExists(string sheetName) => inner.SheetExists(sheetName);

    public string CurrentSheetName => inner.CurrentSheetName;

    public int SheetCount => inner.SheetCount;

    public int SheetIndex(string? sheetName) => inner.SheetIndex(sheetName);

    public CellRange? ArrayArea => inner.ArrayArea;

    public IReadOnlyList<string> SheetsBetween(string firstSheet, string lastSheet) => inner.SheetsBetween(firstSheet, lastSheet);

    public string? GetFormulaText(string? sheetName, CellAddress address) => inner.GetFormulaText(sheetName, address);

    public int GetStyle(string? sheetName, CellAddress address) => inner.GetStyle(sheetName, address);

    public RowVisibility RowVisibilityAt(string? sheetName, int row) => inner.RowVisibilityAt(sheetName, row);

    public CellRange? GetSpillRange(string? sheetName, CellAddress anchor) => inner.GetSpillRange(sheetName, anchor);

    public CellValue EvaluateWith(SheetCell formula, IReadOnlyList<(SheetCell Cell, CellValue Value)> inputs) => inner.EvaluateWith(formula, inputs);

    public void RequestStyle(string styleName) => inner.RequestStyle(styleName);

    public CellValue LookupPivot(string? sheetName, CellAddress target, string dataField, IReadOnlyList<(string Field, string Item)> filters) =>
        inner.LookupPivot(sheetName, target, dataField, filters);

    public string? FetchResource(string url) => inner.FetchResource(url);

    public CellValue EvaluateOffset(string? sheetName, CellAddress formula, int rowDelta, int columnDelta) =>
        inner.EvaluateOffset(sheetName, formula, rowDelta, columnDelta);
}
