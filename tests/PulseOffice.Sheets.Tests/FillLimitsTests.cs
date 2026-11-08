using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Tests;

public sealed class FillLimitsTests
{
    private static CellRange Column(int rows) => new(CellAddress.First, new CellAddress(rows - 1, 0));

    [Fact]
    public void MonthSeriesStopsAtTheLastRepresentableDateInsteadOfThrowing()
    {
        var sheet = new Worksheet("S");
        sheet.SetValue(CellAddress.First, CellValue.Number(2958000));
        var plan = new SeriesFill().Plan(sheet, new SeriesRequest(Column(40), SeriesKind.Date) { DateUnit = SeriesDateUnit.Year });
        Assert.InRange(plan.Count, 1, 39);
    }

    [Fact]
    public void WeekdaySeriesWithAHugeStepAnswersAtOnce()
    {
        var sheet = new Worksheet("S");
        sheet.SetValue(CellAddress.First, CellValue.Number(45000));
        var plan = new SeriesFill().Plan(sheet, new SeriesRequest(Column(3), SeriesKind.Date) { DateUnit = SeriesDateUnit.Weekday, Step = 1e12 });
        Assert.All(plan, cell => Assert.True(double.IsFinite(cell.Value.AsNumber())));
    }

    [Fact]
    public void WeekdaySeriesSkipsWeekends()
    {
        var sheet = new Worksheet("S");
        sheet.SetValue(CellAddress.First, CellValue.Number(45658));
        var plan = new SeriesFill().Plan(sheet, new SeriesRequest(Column(13), SeriesKind.Date) { DateUnit = SeriesDateUnit.Weekday });
        Assert.All(plan, cell => Assert.DoesNotContain(DateTime.FromOADate(cell.Value.AsNumber()).DayOfWeek, new[] { DayOfWeek.Saturday, DayOfWeek.Sunday }));
        Assert.Equal(45658 + 16, plan[^1].Value.AsNumber());
    }

    [Fact]
    public void RandomIntegersAcceptReversedAndHugeBounds()
    {
        var fill = new SheetRandomFill(new SelectionWorkArea());
        var reversed = fill.Plan(new RandomFillRequest(Column(50), RandomDistribution.UniformInteger) { Minimum = 10, Maximum = 1, Seed = 3 });
        Assert.All(reversed, cell => Assert.InRange(cell.Value.AsNumber(), 1, 10));
        var huge = fill.Plan(new RandomFillRequest(Column(5), RandomDistribution.UniformInteger) { Minimum = 0, Maximum = int.MaxValue, Seed = 3 });
        Assert.All(huge, cell => Assert.InRange(cell.Value.AsNumber(), 0, int.MaxValue));
    }
}
