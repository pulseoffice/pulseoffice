using PulseOffice.Document.Commands;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;

namespace PulseOffice.Sheets.UI;

public interface ISheetRangeFilters
{
    int Refresh(IActiveSheetContext context, Worksheet sheet);

    int Store(IActiveSheetContext context, Worksheet sheet, CellRange range, bool hasHeader, RangeFilter? filter, string title);

    IReadOnlySet<int> Preview(IActiveSheetContext context, Worksheet sheet, CellRange range, bool hasHeader, RangeFilter filter);

    SheetRangeFilter? Stored(Workbook workbook, int sheetIndex, CellRange selection);
}

public sealed class SheetRangeFilters(IRangeFilterEvaluator evaluator) : ISheetRangeFilters
{
    public int Refresh(IActiveSheetContext context, Worksheet sheet)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(sheet);
        if (context.Workbook is not { } workbook) return 0;

        var hidden = evaluator.HiddenRows(workbook, sheet, Formulas(context));
        context.ApplyRowFilter(sheet, hidden.Count > 0 ? hidden : null);
        context.RequestRefresh();
        return hidden.Count;
    }

    public IReadOnlySet<int> Preview(IActiveSheetContext context, Worksheet sheet, CellRange range, bool hasHeader, RangeFilter filter)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Workbook is { } workbook
            ? evaluator.RowsToHide(workbook, sheet, range, hasHeader, filter, Formulas(context))
            : new HashSet<int>();
    }

    public int Store(IActiveSheetContext context, Worksheet sheet, CellRange range, bool hasHeader, RangeFilter? filter, string title)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(sheet);
        if (context.Workbook is not { } workbook) return 0;

        var stored = filter is { IsEmpty: false } ? filter : null;
        var index = workbook.IndexOf(sheet);
        var named = workbook.DatabaseRanges.All.FirstOrDefault(candidate => candidate.SheetIndex == index && candidate.Range == range);

        Action apply, revert;
        if (named is not null)
        {
            var before = named;
            var after = named with { RangeFilter = stored, HasHeader = hasHeader };
            apply = () => workbook.DatabaseRanges.Define(after);
            revert = () => workbook.DatabaseRanges.Define(before);
        }
        else
        {
            var before = sheet.RangeFilter;
            var after = stored is null ? null : new SheetRangeFilter(range, hasHeader, stored);
            apply = () => sheet.RangeFilter = after;
            revert = () => sheet.RangeFilter = before;
        }

        apply();
        context.History?.Push(new DelegateAction(title,
            redo: () => { apply(); Refresh(context, sheet); },
            undo: () => { revert(); Refresh(context, sheet); }));

        return Refresh(context, sheet);
    }

    public SheetRangeFilter? Stored(Workbook workbook, int sheetIndex, CellRange selection)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        var named = workbook.DatabaseRanges.All.FirstOrDefault(candidate =>
            candidate.SheetIndex == sheetIndex && candidate.RangeFilter is not null && Covers(candidate.Range, selection));
        if (named is { RangeFilter: { } filter }) return new SheetRangeFilter(named.Range, named.HasHeader, filter);

        return workbook.Sheets[sheetIndex].RangeFilter is { } own && Covers(own.Range, selection) ? own : null;
    }

    private bool Covers(CellRange range, CellRange selection) =>
        range.Contains(selection.Start) && (selection.IsSingleCell || range.Contains(selection.End));

    private IShiftedFormulaEvaluator? Formulas(IActiveSheetContext context) =>
        context.Engine is { } engine ? new EngineShiftedFormulas(engine) : null;
}
