using PulseOffice.Printing;
using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets.Printing;

public interface ISheetPrintOptionsFactory
{
    IReadOnlyList<SheetPrintOptions> For(Workbook workbook, Worksheet sheet, string documentName, IReadOnlyList<CellRange?>? chosen = null);
}

public sealed class SheetPrintOptionsFactory : ISheetPrintOptionsFactory
{
    public IReadOnlyList<SheetPrintOptions> For(Workbook workbook, Worksheet sheet, string documentName, IReadOnlyList<CellRange?>? chosen = null)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(sheet);

        var setup = workbook.PageStyles.For(sheet).Setup;
        var areas = chosen ?? (sheet.PrintRanges.Count > 0
            ? sheet.PrintRanges.Select(range => (CellRange?)range).ToList()
            : [null]);

        return areas.Select(area => new SheetPrintOptions
        {
            Setup = setup,
            Area = area,
            RepeatedRows = sheet.RepeatedPrintRows,
            RepeatedColumns = sheet.RepeatedPrintColumns,
            RepeatedRowRange = Leading(sheet.RepeatedPrintRows),
            RepeatedColumnRange = Leading(sheet.RepeatedPrintColumns),
            RowBreaks = sheet.RowBreaks,
            ColumnBreaks = sheet.ColumnBreaks,
            DocumentName = documentName,
        }).ToList();
    }

    private BandSlice? Leading(int count) => count > 0 ? new BandSlice(0, count - 1) : null;
}
