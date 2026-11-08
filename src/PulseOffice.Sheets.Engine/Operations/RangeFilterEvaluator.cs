using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets.Engine.Operations;

public interface IRangeFilterEvaluator
{
    IReadOnlySet<int> RowsToHide(Workbook workbook, Worksheet sheet, CellRange range, bool hasHeader, RangeFilter filter, IShiftedFormulaEvaluator? formulas = null);

    IReadOnlySet<int> HiddenRows(Workbook workbook, Worksheet sheet, IShiftedFormulaEvaluator? formulas = null);

    void Restore(Workbook workbook, IShiftedFormulaEvaluator? formulas = null);
}

public sealed class RangeFilterEvaluator(ISheetFilter filters, IFilterRowKey rowKeys) : IRangeFilterEvaluator
{
    public RangeFilterEvaluator(ISheetFilter filters) : this(filters, new FilterRowKey())
    {
    }

    public IReadOnlySet<int> RowsToHide(Workbook workbook, Worksheet sheet, CellRange range, bool hasHeader, RangeFilter filter, IShiftedFormulaEvaluator? formulas = null)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(filter);

        var hidden = filter.Condition is { } condition
            ? new HashSet<int>(filters.RowsToHide(sheet, range, condition, hasHeader))
            : [];

        if (filter.Criteria is { } criteria && criteria.SheetIndex >= 0 && criteria.SheetIndex < workbook.Sheets.Count)
            hidden.UnionWith(filters.RowsToHide(sheet, new AdvancedFilterRequest(range, criteria.Range)
            {
                HasHeader = hasHeader,
                CriteriaSheet = workbook.Sheets[criteria.SheetIndex],
                Formulas = formulas,
            }));

        if (filter.UniqueOnly) hidden.UnionWith(Duplicates(sheet, range, hasHeader, hidden).ToList());

        return hidden;
    }

    public IReadOnlySet<int> HiddenRows(Workbook workbook, Worksheet sheet, IShiftedFormulaEvaluator? formulas = null)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(sheet);

        var index = workbook.IndexOf(sheet);
        var rows = new HashSet<int>();

        if (sheet.AutoFilter is { Range: { } filtered, Filters.Count: > 0 })
            rows.UnionWith(filters.RowsToHide(sheet, filtered, sheet.AutoFilter.Filters, sheet.AutoFilter.HasHeader, workbook.Styles));

        foreach (var range in workbook.DatabaseRanges.All.Where(range => range.SheetIndex == index && index >= 0))
        {
            if (range.RangeFilter is { IsEmpty: false } stored)
                rows.UnionWith(RowsToHide(workbook, sheet, range.RangeWithoutTotals, range.HasHeader, stored, formulas));
            if (range.Filters.Count > 0)
                rows.UnionWith(filters.RowsToHide(sheet, range.RangeWithoutTotals, range.Filters, range.HasHeader, workbook.Styles));
        }

        if (sheet.RangeFilter is { Filter.IsEmpty: false } anonymous)
            rows.UnionWith(RowsToHide(workbook, sheet, anonymous.Range, anonymous.HasHeader, anonymous.Filter, formulas));

        return rows;
    }

    public void Restore(Workbook workbook, IShiftedFormulaEvaluator? formulas = null)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        foreach (var sheet in workbook.Sheets)
        {
            var hidden = HiddenRows(workbook, sheet, formulas);
            if (hidden.Count > 0) sheet.Layout.ApplyFilteredRows(hidden, adoptHiddenRows: true);
        }
    }

    private IEnumerable<int> Duplicates(Worksheet sheet, CellRange range, bool hasHeader, IReadOnlySet<int> hidden)
    {
        var seen = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
        for (var row = range.Start.Row + (hasHeader ? 1 : 0); row <= range.End.Row; row++)
        {
            if (hidden.Contains(row)) continue;
            if (!seen.Add(rowKeys.Key(sheet, row, range.Start.Column, range.ColumnCount))) yield return row;
        }
    }
}
