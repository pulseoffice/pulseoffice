using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class Round206ScenarioTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    [Fact]
    public void A_scenario_over_whole_columns_captures_only_the_used_part()
    {
        var (_, engine) = fixture.CreateWorkbook();
        engine.SetValue(new SheetCell(0, new CellAddress(0, 0)), CellValue.Number(1));
        engine.SetValue(new SheetCell(0, new CellAddress(2, 1)), CellValue.Number(2));
        var columns = new CellRange(CellAddress.First, new CellAddress(CellAddress.MaxRows - 1, 1));

        var scenario = new ScenarioManager().Capture(engine, 0, columns, "Wide");

        Assert.Equal(6, scenario.Values.Count);
        Assert.Equal(columns, scenario.Range);
        Assert.Equal(2, scenario.Values[new CellAddress(2, 1)].AsNumber());
    }
}
