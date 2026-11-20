using System.Globalization;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class Round336LedgerDisplayedSearchTests
{
    private static readonly CultureInfo Italian = CultureInfo.GetCultureInfo("it-IT");

    private readonly SheetSearch _search = new(new PresentedCellSearchText(
        new CellPresenter(new NumberFormatter(Italian, serial => new DateTime(1899, 12, 30).AddDays(serial))),
        new FixedSpreadsheetLocale(Italian)));

    private static Worksheet SheetWithAmount()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");
        var twoDecimals = workbook.Styles.Intern(new CellStyle { NumberFormatId = 4 });
        sheet[new CellAddress(0, 0)] = new Cell { Value = CellValue.Number(1234.5), StyleId = twoDecimals };
        return sheet;
    }

    [Fact]
    public void Values_are_searched_as_displayed()
    {
        var sheet = SheetWithAmount();

        Assert.Single(_search.FindAll(sheet, "1.234,50"));
        Assert.Empty(_search.FindAll(sheet, "1234.5"));
    }

    [Fact]
    public void A_numeric_replacement_is_read_in_the_spreadsheet_locale()
    {
        var sheet = new Workbook().AddSheet("Dati");
        sheet[new CellAddress(0, 0)] = new Cell { Value = CellValue.Text("x") };

        _search.ReplaceAll(sheet, "x", "2,5", new SearchOptions { WholeCell = true });

        Assert.Equal(CellValue.Number(2.5), sheet[new CellAddress(0, 0)].Value);
    }
}
