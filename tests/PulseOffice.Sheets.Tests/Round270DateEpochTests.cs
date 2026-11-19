using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Tests;

public sealed class Round270DateEpochTests
{
    private static Workbook Mac()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Calculation = workbook.Calculation with { DateEpoch = CalculationSettings.Mac1904DateEpoch };
        return workbook;
    }

    [Fact]
    public void Month_series_of_a_1904_workbook_advance_calendar_months()
    {
        var workbook = Mac();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(CellAddress.First, CellValue.Number(workbook.Calculation.SerialOf(new DateTime(2024, 1, 31))));
        var planned = new SeriesFill().Plan(sheet, new SeriesRequest(new CellRange(CellAddress.First, new CellAddress(1, 0)), SeriesKind.Date) { DateUnit = SeriesDateUnit.Month });

        Assert.Equal(new DateTime(2024, 2, 29), workbook.Calculation.DateOf(Assert.Single(planned).Value.AsNumber()));
    }

    [Fact]
    public void Weekday_series_of_a_1904_workbook_skip_the_real_weekend()
    {
        var workbook = Mac();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(CellAddress.First, CellValue.Number(workbook.Calculation.SerialOf(new DateTime(2024, 3, 15))));
        var planned = new SeriesFill().Plan(sheet, new SeriesRequest(new CellRange(CellAddress.First, new CellAddress(1, 0)), SeriesKind.Date) { DateUnit = SeriesDateUnit.Weekday });

        Assert.Equal(new DateTime(2024, 3, 18), workbook.Calculation.DateOf(Assert.Single(planned).Value.AsNumber()));
    }

    [Fact]
    public void Filter_dates_and_relative_periods_count_from_the_workbook_epoch()
    {
        var workbook = Mac();
        var sheet = workbook.Sheets[0];
        var serial = workbook.Calculation.SerialOf(new DateTime(2024, 3, 15));
        var style = workbook.Styles.Intern(workbook.Styles.Get(0) with { NumberFormatId = workbook.NumberFormats.Register("yyyy-mm-dd") });
        sheet[CellAddress.First] = new Cell { Value = CellValue.Number(serial), StyleId = style, ExplicitStyle = true };

        Assert.True(new FilterColumnDates().TryRead(sheet, CellAddress.First, workbook.Styles, workbook.NumberFormats, out var moment));
        Assert.Equal(new DateTime(2024, 3, 15), moment);
        var march = new RelativeDatePeriods().Bounds(RelativeDatePeriod.ThisMonth, new DateTime(2024, 3, 20));
        Assert.True(march.Contains(serial, workbook.Calculation));
        Assert.False(march.Contains(serial, CalculationSettings.Default));
    }

    [Fact]
    public void Serials_outside_the_calendar_of_the_epoch_are_not_dates()
    {
        Assert.False(new CalculationSettings { DateEpoch = CalculationSettings.Mac1904DateEpoch }.TryDateOf(2958465, out _));
        Assert.True(CalculationSettings.Default.TryDateOf(2958465, out var last));
        Assert.Equal(new DateTime(9999, 12, 31), last);
    }
}
