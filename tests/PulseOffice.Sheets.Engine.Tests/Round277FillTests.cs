using System.Globalization;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class Round277FillTests
{
    private sealed class English : PulseOffice.Sheets.Formatting.ISpreadsheetLocale
    {
        public CultureInfo Culture => CultureInfo.GetCultureInfo("en-US");
    }

    private static readonly SheetFill Fill = new(new English(), new PulseOffice.Sheets.Formatting.CalendarFormats());

    [Fact]
    public void A_single_date_advances_by_days_while_a_single_number_is_copied()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(CellAddress.First, CellValue.Number(45000));
        sheet.SetStyle(CellAddress.First, workbook.Styles.Intern(CellStyle.Default with { NumberFormatId = 14 }));
        sheet.SetValue(new CellAddress(0, 1), CellValue.Number(7));

        var filled = Fill.Extend(sheet, new CellRange(CellAddress.First, new CellAddress(0, 1)), new CellRange(CellAddress.First, new CellAddress(2, 1)), FillDirection.Down);

        Assert.Equal([45001d, 45002d], filled.Where(pair => pair.Address.Column == 0).Select(pair => pair.Cell.Value.AsNumber()));
        Assert.Equal([7d, 7d], filled.Where(pair => pair.Address.Column == 1).Select(pair => pair.Cell.Value.AsNumber()));
    }

    [Fact]
    public void Day_and_month_names_continue_in_the_typed_case()
    {
        var sheet = new Worksheet("Data");
        sheet.SetValue(CellAddress.First, CellValue.Text("Sat"));
        sheet.SetValue(new CellAddress(0, 1), CellValue.Text("NOVEMBER"));

        var filled = Fill.Extend(sheet, new CellRange(CellAddress.First, new CellAddress(0, 1)), new CellRange(CellAddress.First, new CellAddress(2, 1)), FillDirection.Down);

        Assert.Equal(["Sun", "Mon"], filled.Where(pair => pair.Address.Column == 0).Select(pair => pair.Cell.Value.AsText()));
        Assert.Equal(["DECEMBER", "JANUARY"], filled.Where(pair => pair.Address.Column == 1).Select(pair => pair.Cell.Value.AsText()));
    }
}
