using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class Round206SolverTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    [Fact]
    public void An_unreachable_target_value_is_not_reported_as_solved()
    {
        var (_, engine) = fixture.CreateWorkbook();
        var variable = new SheetCell(0, CellAddress.First);
        var objective = new SheetCell(0, new CellAddress(1, 0));
        engine.SetValue(variable, CellValue.Number(1));
        engine.SetFormula(objective, "=A1*A1+5");

        var outcome = new Solver().Solve(engine, new SolverRequest
        {
            Objective = objective,
            Variables = [variable],
            Goal = SolverGoal.Value,
            TargetValue = 1,
            MaximumIterations = 200,
        });

        Assert.False(outcome.Succeeded);
        Assert.Equal("target", outcome.Reason);
        Assert.Equal(1, engine.GetValue(variable).AsNumber());
    }
}
