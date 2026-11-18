using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class Round210ScenarioTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    [Fact]
    public void Showing_a_whole_column_scenario_clears_cells_filled_beyond_the_captured_area()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        engine.SetValue(new SheetCell(0, new CellAddress(0, 0)), CellValue.Number(1));
        var column = new CellRange(CellAddress.First, new CellAddress(CellAddress.MaxRows - 1, 0));
        var manager = new ScenarioManager();
        var scenario = manager.Capture(engine, 0, column, "Wide");
        var late = new CellAddress(5, 0);
        engine.SetValue(new SheetCell(0, late), CellValue.Number(9));

        var previous = manager.Apply(engine, 0, scenario);

        Assert.True(workbook.Sheets[0][late].Value.IsEmpty);
        manager.Restore(engine, 0, previous);
        Assert.Equal(9, workbook.Sheets[0][late].Value.AsNumber());
    }
}
