using System.Globalization;
using PulseOffice.Printing;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Printing.Tests;

public class SheetPrintOptionsFactoryTests
{
    private static SheetPrintDocument Printed(Workbook workbook)
    {
        var sheet = workbook.Sheets[0];
        var presenter = new CellPresenter(new NumberFormatter(CultureInfo.InvariantCulture,
            serial => new DateOnly(1899, 12, 30).ToDateTime(TimeOnly.MinValue).AddDays(serial)));
        var options = Assert.Single(new SheetPrintOptionsFactory().For(workbook, sheet, "Conti"));
        return new SheetPrintDocument(sheet, workbook.Styles, presenter, new HeaderFooterFields(CultureInfo.InvariantCulture), options);
    }

    private static Workbook TenRows()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");
        for (var row = 0; row < 10; row++) sheet.SetValue(new CellAddress(row, 0), CellValue.Number(row));
        return workbook;
    }

    [Fact]
    public void Manual_row_and_column_breaks_split_the_printed_pages()
    {
        var workbook = TenRows();
        workbook.Sheets[0].SetValue(new CellAddress(0, 3), CellValue.Number(1));
        Assert.Equal(1, Printed(workbook).PageCount);

        workbook.Sheets[0].RowBreaks.Add(4);
        workbook.Sheets[0].ColumnBreaks.Add(2);
        var document = Printed(workbook);

        Assert.Equal(4, document.PageCount);
        Assert.Equal(new BandSlice(0, 3), document.Pages[0].Rows);
        Assert.Equal(new BandSlice(4, 9), document.Pages[1].Rows);
        Assert.Equal(new BandSlice(2, 3), document.Pages[2].Columns);
    }

    [Fact]
    public void Each_print_range_gets_its_own_options_with_the_sheet_titles()
    {
        var workbook = TenRows();
        var sheet = workbook.Sheets[0];
        sheet.RepeatedPrintRows = 2;
        sheet.PrintRanges.Add(new CellRange(new CellAddress(0, 0), new CellAddress(2, 0)));
        sheet.PrintRanges.Add(new CellRange(new CellAddress(4, 0), new CellAddress(8, 0)));

        var options = new SheetPrintOptionsFactory().For(workbook, sheet, "Conti");

        Assert.Equal(2, options.Count);
        Assert.All(options, option => Assert.Equal(new BandSlice(0, 1), option.RepeatedRowRange));
        Assert.Same(sheet.RowBreaks, options[0].RowBreaks);
    }
}
