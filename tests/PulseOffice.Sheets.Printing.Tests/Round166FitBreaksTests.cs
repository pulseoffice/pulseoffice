using System.Globalization;
using PulseOffice.Foundation.Pages;
using PulseOffice.Printing;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Printing.Tests;

public sealed class Round166FitBreaksTests
{
    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 2)]
    public void Manual_breaks_are_ignored_under_fit_to_pages(bool fit, int pages)
    {
        var sheet = new Worksheet("Sheet1");
        for (var row = 0; row < 6; row++) sheet.SetValue(new CellAddress(row, 0), CellValue.Number(row));
        var setup = new PageSetup { Paper = new PaperCatalog().Default, FitToHeightPages = fit ? 1 : null };
        var formatter = new NumberFormatter(
            CultureInfo.InvariantCulture,
            serial => new DateOnly(1899, 12, 30).ToDateTime(TimeOnly.MinValue).AddDays(serial));

        var document = new SheetPrintDocument(sheet, new Styling.StyleCatalog(), new CellPresenter(formatter),
            new HeaderFooterFields(CultureInfo.InvariantCulture),
            new SheetPrintOptions { Setup = setup, RowBreaks = new HashSet<int> { 3 } });

        Assert.Equal(pages, document.PageCount);
    }
}
