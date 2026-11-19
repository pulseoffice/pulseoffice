using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Tests;

public sealed class Round277SheetCellTransplantTests
{
    [Fact]
    public void Dates_of_a_1900_source_keep_their_day_in_a_1904_workbook_and_keep_their_format()
    {
        var source = Workbook.CreateDefault();
        var from = source.Sheets[0];
        var day = new DateTime(2024, 5, 17);
        var dateStyle = source.Styles.Intern(CellStyle.Default with { NumberFormatId = 14 });
        var timeStyle = source.Styles.Intern(CellStyle.Default with { NumberFormatId = 21 });
        from.SetValue(CellAddress.First, CellValue.Number(source.Calculation.SerialOf(day)));
        from.SetStyle(CellAddress.First, dateStyle);
        from.SetValue(new CellAddress(1, 0), CellValue.Number(45000));
        from.SetValue(new CellAddress(2, 0), CellValue.Number(1.5));
        from.SetStyle(new CellAddress(2, 0), timeStyle);

        var target = Workbook.CreateDefault();
        target.Calculation = target.Calculation with { DateEpoch = CalculationSettings.Mac1904DateEpoch };
        var into = target.AddSheet("Imported");

        new SheetCellTransplant().Copy(source, from, target, into);

        Assert.Equal(day, target.Calculation.DateOf(into.GetValue(CellAddress.First).AsNumber()));
        Assert.Equal(14, target.Styles.Get(into.StyleIdAt(CellAddress.First)).NumberFormatId);
        Assert.Equal(45000, into.GetValue(new CellAddress(1, 0)).AsNumber());
        Assert.Equal(1.5, into.GetValue(new CellAddress(2, 0)).AsNumber());
    }
}
