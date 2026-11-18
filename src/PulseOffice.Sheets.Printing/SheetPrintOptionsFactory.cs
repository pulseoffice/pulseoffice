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
            RepeatedRowRange = Slice(sheet.PrintTitleRows),
            RepeatedColumnRange = Slice(sheet.PrintTitleColumns),
            RowBreaks = sheet.RowBreaks,
            ColumnBreaks = sheet.ColumnBreaks,
            DocumentName = documentName,
        }).ToList();
    }

    private BandSlice? Slice(PrintTitleBand band) => band.IsEmpty ? null : new BandSlice(band.Start, band.End);
}
