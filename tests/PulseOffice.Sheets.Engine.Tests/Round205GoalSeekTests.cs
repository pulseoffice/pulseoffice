using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class Round205GoalSeekTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    [Fact]
    public void A_large_target_is_reached_within_a_tolerance_relative_to_it()
    {
        var (_, engine) = fixture.CreateWorkbook();
        var variable = new SheetCell(0, CellAddress.First);
        var formula = new SheetCell(0, new CellAddress(1, 0));
        engine.SetValue(variable, CellValue.Number(1000));
        engine.SetFormula(formula, "=A1^2");

        var outcome = new GoalSeek().Solve(engine, new GoalSeekRequest(formula, variable, 2e10));

        Assert.True(outcome.Found);
        Assert.Equal(Math.Sqrt(2e10), outcome.Value, 3);
    }
}
