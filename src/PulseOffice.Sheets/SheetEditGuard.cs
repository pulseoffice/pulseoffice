using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets;

public interface ISheetEditGuard
{
    bool CanWrite(Workbook workbook, Worksheet sheet, CellRange range);

    bool CanFormat(Worksheet sheet);
}

public sealed class SheetEditGuard : ISheetEditGuard
{
    public bool CanWrite(Workbook workbook, Worksheet sheet, CellRange range)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(sheet);

        if (!sheet.Protection.IsEnabled) return true;

        bool Locked(int styleId) => workbook.Styles.Get(styleId).Locked;

        var stored = 0L;
        foreach (var pair in sheet.NonEmptyCellsIn(range))
        {
            stored++;
            if (Locked(sheet.StyleIdAt(pair.Key))) return false;
        }
        if (stored == range.CellCount) return true;

        var layout = sheet.Layout;
        var rowStyles = layout.RowStyles.RunsIn(range.Start.Row, range.End.Row).ToList();
        var columnStyles = layout.ColumnStyles.RunsIn(range.Start.Column, range.End.Column).ToList();

        if (rowStyles.Any(run => Locked(run.Value)) || columnStyles.Any(run => Locked(run.Value))) return false;
        if (layout.StyleBlocks.In(range.Start.Row, range.End.Row, range.Start.Column, range.End.Column).Any(block => Locked(block.StyleId))) return false;

        var rowsCovered = rowStyles.Sum(run => (long)run.Length) == range.RowCount;
        var columnsCovered = columnStyles.Sum(run => (long)run.Length) == range.ColumnCount;
        return rowsCovered || columnsCovered || !Locked(Styling.StyleCatalog.DefaultStyleId);
    }

    public bool CanFormat(Worksheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        return !sheet.Protection.IsEnabled || sheet.Protection.AllowFormatCells;
    }
}
