using System.Runtime.CompilerServices;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Evaluation;

public sealed record SubstitutionSources(
    IReadOnlyDictionary<SheetCell, CellValue> Inputs,
    Func<SheetCell, string?> FormulaOf,
    Func<string, IEvaluationContext, CellValue> Evaluate,
    Func<SheetCell, IEvaluationContext> ContextFor,
    Func<string, int> SheetIndexOf);

public sealed class SubstitutingContext : IEvaluationContext
{
    private const int MaxNesting = 256;

    private readonly IEvaluationContext _inner;
    private readonly SubstitutionSources _sources;
    private readonly Dictionary<SheetCell, CellValue> _computed;
    private readonly HashSet<SheetCell> _visiting;

    public SubstitutingContext(IEvaluationContext inner, SubstitutionSources sources)
        : this(inner, sources, [], [])
    {
    }

    private SubstitutingContext(
        IEvaluationContext inner, SubstitutionSources sources,
        Dictionary<SheetCell, CellValue> computed, HashSet<SheetCell> visiting)
    {
        _inner = inner;
        _sources = sources;
        _computed = computed;
        _visiting = visiting;
    }

    private int SheetIndexOf(string? sheetName) =>
        sheetName is null ? _inner.CurrentCell.SheetIndex : _sources.SheetIndexOf(sheetName);

    public SheetCell CurrentCell => _inner.CurrentCell;

    public Functions.NumberToText NumberText => _inner.NumberText;

    public Functions.NumericText TextNumbers => _inner.TextNumbers;

    public bool ImplicitIntersection => _inner.ImplicitIntersection;

    public Functions.TextRules TextRules => _inner.TextRules;

    public void NoteArrayUse() => _inner.NoteArrayUse();

    public CellRange? ContentWithin(string? sheetName, CellRange range) => _inner.ContentWithin(sheetName, range);

    public CellValue GetCellValue(string? sheetName, CellAddress address)
    {
        var index = SheetIndexOf(sheetName);
        if (index < 0) return _inner.GetCellValue(sheetName, address);

        var cell = new SheetCell(index, address);

        if (_sources.Inputs.TryGetValue(cell, out var substituted)) return substituted;
        if (_computed.TryGetValue(cell, out var cached)) return cached;

        if (_sources.FormulaOf(cell) is not { Length: > 0 } formula) return _inner.GetCellValue(sheetName, address);

        return Compute(cell, formula);
    }

    public void Prepare(SheetCell cell)
    {
        if (_sources.Inputs.ContainsKey(cell) || _computed.ContainsKey(cell)) return;
        if (_sources.FormulaOf(cell) is { Length: > 0 } formula) Compute(cell, formula);
    }

    private CellValue Compute(SheetCell cell, string formula)
    {
        if (_visiting.Count >= MaxNesting || !RuntimeHelpers.TryEnsureSufficientExecutionStack())
            return CellValue.FromError(CellError.Value);

        if (!_visiting.Add(cell)) return CellValue.FromError(CellError.Reference);

        try
        {
            var value = _sources.Evaluate(formula, new SubstitutingContext(_sources.ContextFor(cell), _sources, _computed, _visiting));
            _computed[cell] = value;
            return value;
        }
        finally
        {
            _visiting.Remove(cell);
        }
    }

    public IEnumerable<CellValue> GetRangeValues(string? sheetName, CellRange range)
    {
        foreach (var address in range.Cells()) yield return GetCellValue(sheetName, address);
    }

    public string? ResolveName(string name) => _inner.ResolveName(name);

    public bool SheetExists(string sheetName) => _inner.SheetExists(sheetName);

    public string CurrentSheetName => _inner.CurrentSheetName;

    public int SheetCount => _inner.SheetCount;

    public int SheetIndex(string? sheetName) => _inner.SheetIndex(sheetName);

    public CellRange? ArrayArea => _inner.ArrayArea;

    public IReadOnlyList<string> SheetsBetween(string firstSheet, string lastSheet) => _inner.SheetsBetween(firstSheet, lastSheet);

    public string? GetFormulaText(string? sheetName, CellAddress address) =>
        _inner.GetFormulaText(sheetName, address);

    public int GetStyle(string? sheetName, CellAddress address) => _inner.GetStyle(sheetName, address);

    public RowVisibility RowVisibilityAt(string? sheetName, int row) => _inner.RowVisibilityAt(sheetName, row);

    public CellRange? GetSpillRange(string? sheetName, CellAddress anchor) =>
        _inner.GetSpillRange(sheetName, anchor);

    public CellValue EvaluateWith(
        SheetCell formula, IReadOnlyList<(SheetCell Cell, CellValue Value)> inputs) =>
        _inner.EvaluateWith(formula, inputs);

    public void RequestStyle(string styleName) => _inner.RequestStyle(styleName);

    public CellValue LookupPivot(
        string? sheetName, CellAddress target, string dataField,
        IReadOnlyList<(string Field, string Item)> filters) =>
        _inner.LookupPivot(sheetName, target, dataField, filters);

    public string? FetchResource(string url) => _inner.FetchResource(url);

    public CellValue EvaluateOffset(string? sheetName, CellAddress formula, int rowDelta, int columnDelta) =>
        _inner.EvaluateOffset(sheetName, formula, rowDelta, columnDelta);
}
