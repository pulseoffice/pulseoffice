using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Formatting;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class Round267DateEpochTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static readonly SheetCell A1 = new(0, new CellAddress(0, 0));
    private static readonly SheetCell A2 = new(0, new CellAddress(1, 0));

    [Fact]
    public void Date_functions_count_from_the_workbook_epoch()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Calculation = workbook.Calculation with { DateEpoch = CalculationSettings.Mac1904DateEpoch };
        var engine = fixture.CreateEngine(workbook);
        engine.SetFormula(A1, "=DATE(1904,1,2)");
        engine.SetFormula(A2, "=YEAR(0)+DATEVALUE(\"1904-01-03\")");

        Assert.Equal(1, engine.GetValue(A1).AsNumber());
        Assert.Equal(1906, engine.GetValue(A2).AsNumber());
    }

    [Fact]
    public void The_standard_workbook_is_unchanged()
    {
        var (_, engine) = fixture.CreateWorkbook();
        engine.SetFormula(A1, "=DATE(1904,1,1)");

        Assert.Equal(1462, engine.GetValue(A1).AsNumber());
    }

    [Fact]
    public void Typed_and_edited_dates_follow_the_entered_epoch()
    {
        var epochs = new AmbientDateEpoch();
        var locale = FixedSpreadsheetLocale.Invariant();
        using (epochs.Enter(CalculationSettings.Mac1904DateEpoch))
        {
            Assert.Equal(1, new CellInputRecognizer(locale, new SystemDateCode(), epochs).Recognize("1904-01-02").Value.AsNumber());
            Assert.Equal(new DateTime(1904, 1, 2), ((IDateEpoch)epochs).ToDate(1));
        }
        Assert.Equal(CalculationSettings.StandardDateEpoch, epochs.Current);
    }
}
