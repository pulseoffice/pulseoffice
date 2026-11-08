using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;

namespace PulseOffice.Sheets.UI.DataDialogs;

public sealed record QuickSortPlan(CellRange Range, bool HasHeader, int Column, bool ReappliesAutoFilter);

public interface IQuickSortPlanner
{
    QuickSortPlan? Plan(Workbook workbook, int sheetIndex, CellRange selection);
}

public sealed class QuickSortPlanner(ISheetRegions regions, IHeaderRowDetector headers) : IQuickSortPlanner
{
    public QuickSortPlan? Plan(Workbook workbook, int sheetIndex, CellRange selection)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        if (sheetIndex < 0 || sheetIndex >= workbook.Sheets.Count) return null;
        var sheet = workbook.Sheets[sheetIndex];
        var cursor = selection.Start;

        if (sheet.AutoFilter.Range is { } filtered && filtered.Contains(cursor) && Covers(filtered, sheet.AutoFilter.HasHeader, selection))
            return Planned(filtered, sheet.AutoFilter.HasHeader, cursor, true);

        if (workbook.DatabaseRanges.At(sheetIndex, cursor) is { } database && Covers(database.RangeWithoutTotals, database.HasHeader, selection))
            return Planned(database.RangeWithoutTotals, database.HasHeader, cursor, false);

        if (!selection.IsSingleCell)
            return selection.RowCount < 2 ? null : Planned(selection, false, cursor, false);

        var region = regions.CurrentRegion(sheet, cursor);
        return region.RowCount < 2 ? null : Planned(region, headers.LooksLikeHeader(sheet, region), cursor, false);
    }

    private bool Covers(CellRange owner, bool header, CellRange selection) =>
        selection.IsSingleCell || selection == owner
        || header && owner.RowCount > 1 && selection == new CellRange(new CellAddress(owner.Start.Row + 1, owner.Start.Column), owner.End);

    private QuickSortPlan? Planned(CellRange range, bool header, CellAddress cursor, bool reapplies) =>
        range.RowCount < (header ? 3 : 2)
            ? null
            : new QuickSortPlan(range, header, Math.Clamp(cursor.Column - range.Start.Column, 0, range.ColumnCount - 1), reapplies);
}
