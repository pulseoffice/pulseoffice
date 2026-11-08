using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using PulseOffice.UI.Controls.Sorting;

namespace PulseOffice.Sheets.UI.DataDialogs;

public interface IHeaderRowDetector
{
    bool LooksLikeHeader(Worksheet sheet, CellRange range);
}

public sealed class HeaderRowDetector : IHeaderRowDetector
{
    public bool LooksLikeHeader(Worksheet sheet, CellRange range)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        if (range.RowCount < 2) return false;

        var first = Row(sheet, range, range.Start.Row);
        if (first.Any(value => value.Kind != ValueKind.Text)) return false;

        var below = Enumerable.Range(range.Start.Row + 1, Math.Min(range.RowCount - 1, 20))
            .SelectMany(row => Row(sheet, range, row))
            .ToList();
        return below.Any(value => value.Kind is ValueKind.Number or ValueKind.Boolean)
            || !below.Select(value => value.ToString()).Intersect(first.Select(value => value.ToString()), StringComparer.CurrentCultureIgnoreCase).Any();
    }

    private IReadOnlyList<CellValue> Row(Worksheet sheet, CellRange range, int row) =>
        [.. Enumerable.Range(range.Start.Column, range.ColumnCount).Select(column => sheet.GetValue(new CellAddress(row, column)))];
}

public sealed record LedgerSortServices(
    ISheetSort Sorter,
    ISheetRegions Regions,
    ISortLevelsDialog Dialog,
    ILocaleSortOrders Orders,
    IHeaderRowDetector Headers,
    IQuickSortPlanner QuickSort,
    ISortSettingsMemory Memory);
