using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Styling;

namespace PulseOffice.Sheets.Engine.Operations;

public enum MergeKind { Merge, MergeAndCentre }

public interface ISheetMerge
{
    bool CanMerge(Worksheet sheet, CellRange selection);

    bool CanUnmerge(Worksheet sheet, CellRange selection);

    CellRange Merge(Workbook workbook, Worksheet sheet, CellRange selection, MergeKind kind);

    IReadOnlyList<CellRange> Unmerge(Worksheet sheet, CellRange selection);
}

public sealed class SheetMerge : ISheetMerge
{
    public bool CanMerge(Worksheet sheet, CellRange selection)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        return !selection.IsSingleCell;
    }

    public bool CanUnmerge(Worksheet sheet, CellRange selection)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        return sheet.Merges.Overlapping(selection).Any();
    }

    public CellRange Merge(Workbook workbook, Worksheet sheet, CellRange selection, MergeKind kind)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(sheet);

        var region = sheet.Merges.Expand(selection);

        sheet.Merges.Add(region);

        if (kind == MergeKind.MergeAndCentre)
            sheet.SetStyle(
                region.Start,
                workbook.Styles.Derive(
                    sheet.StyleIdAt(region.Start),
                    style => style with { Horizontal = CellHorizontalAlignment.Center }));

        return region;
    }

    public IReadOnlyList<CellRange> Unmerge(Worksheet sheet, CellRange selection)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        return sheet.Merges.RemoveOverlapping(selection);
    }
}
