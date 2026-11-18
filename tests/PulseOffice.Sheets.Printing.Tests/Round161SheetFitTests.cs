using System.Globalization;
using PulseOffice.Foundation.Pages;
using PulseOffice.Printing;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Printing.Tests;

public sealed class Round161SheetFitTests
{
    private static SheetPrintDocument Build(Worksheet sheet, SheetPrintOptions options)
    {
        var formatter = new NumberFormatter(
            CultureInfo.InvariantCulture,
            serial => new DateOnly(1899, 12, 30).ToDateTime(TimeOnly.MinValue).AddDays(serial));
        return new SheetPrintDocument(sheet, new Styling.StyleCatalog(), new CellPresenter(formatter),
            new HeaderFooterFields(CultureInfo.InvariantCulture), options);
    }

    private static Worksheet Column(int rows)
    {
        var sheet = new Worksheet("Sheet1");
        for (var row = 0; row < rows; row++) sheet.SetValue(new CellAddress(row, 0), CellValue.Number(row));
        return sheet;
    }

    [Fact]
    public void Fit_to_pages_down_counts_the_repeated_rows_printed_on_every_page()
    {
        var setup = new PageSetup { Paper = new PaperCatalog().Default, FitToHeightPages = 2 };

        var document = Build(Column(200), new SheetPrintOptions { Setup = setup, RepeatedRowRange = new BandSlice(0, 29) });

        Assert.Equal(2, document.PageCount);
    }

    [Fact]
    public void Fit_to_pages_down_holds_when_whole_rows_do_not_divide_the_page_evenly()
    {
        var setup = new PageSetup { Paper = new PaperCatalog().Default, FitToHeightPages = 3, PrintHeadings = true };

        var document = Build(Column(301), new SheetPrintOptions { Setup = setup });

        Assert.Equal(3, document.PageCount);
    }
}
