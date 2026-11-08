using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class SolverTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static readonly ISolver Solver = new Solver();

    private static SheetCell Cell(int row, int column = 0) => new(0, new CellAddress(row, column));

    [Fact]
    public void TrovaIlMassimoDiUnaParabola()
    {
        var (_, engine) = fixture.CreateWorkbook();

        engine.SetValue(Cell(0), CellValue.Number(0));
        engine.SetFormula(Cell(1), "=10-(A1-4)^2");

        var outcome = Solver.Solve(engine, new SolverRequest
        {
            Objective = Cell(1),
            Variables = [Cell(0)],
            Goal = SolverGoal.Maximum,
        });

        Assert.True(outcome.Succeeded);
        Assert.Equal(4, outcome.Variables[0], 2);
        Assert.Equal(10, outcome.Objective, 2);
    }

    [Fact]
    public void RispettaUnVincoloDiMassimo()
    {
        var (_, engine) = fixture.CreateWorkbook();

        engine.SetValue(Cell(0), CellValue.Number(0));
        engine.SetFormula(Cell(1), "=A1*2");

        var outcome = Solver.Solve(engine, new SolverRequest
        {
            Objective = Cell(1),
            Variables = [Cell(0)],
            Goal = SolverGoal.Maximum,
            Constraints = [new SolverConstraint(Cell(0), ConstraintOperator.LessOrEqual, 7)],
        });

        Assert.True(outcome.Succeeded);
        Assert.Equal(7, outcome.Variables[0], 2);
    }

    [Fact]
    public void DueVariabiliConUnVincoloDiSomma()
    {
        var (_, engine) = fixture.CreateWorkbook();

        engine.SetValue(Cell(0), CellValue.Number(1));
        engine.SetValue(Cell(1), CellValue.Number(1));
        engine.SetFormula(Cell(2), "=A1*A2");
        engine.SetFormula(Cell(3), "=A1+A2");

        var outcome = Solver.Solve(engine, new SolverRequest
        {
            Objective = Cell(2),
            Variables = [Cell(0), Cell(1)],
            Goal = SolverGoal.Maximum,
            Constraints = [new SolverConstraint(Cell(3), ConstraintOperator.Equal, 10)],
        });

        Assert.True(outcome.Succeeded);
        Assert.Equal(5, outcome.Variables[0], 1);
        Assert.Equal(5, outcome.Variables[1], 1);
    }

    [Fact]
    public void PuoCercareUnValorePreciso()
    {
        var (_, engine) = fixture.CreateWorkbook();

        engine.SetValue(Cell(0), CellValue.Number(1));
        engine.SetFormula(Cell(1), "=A1^3");

        var outcome = Solver.Solve(engine, new SolverRequest
        {
            Objective = Cell(1),
            Variables = [Cell(0)],
            Goal = SolverGoal.Value,
            TargetValue = 27,
        });

        Assert.True(outcome.Succeeded);
        Assert.Equal(3, outcome.Variables[0], 2);
    }

    [Fact]
    public void UnVincoloIntero()
    {
        var (_, engine) = fixture.CreateWorkbook();

        engine.SetValue(Cell(0), CellValue.Number(0));
        engine.SetFormula(Cell(1), "=0-(A1-2.6)^2");

        var outcome = Solver.Solve(engine, new SolverRequest
        {
            Objective = Cell(1),
            Variables = [Cell(0)],
            Goal = SolverGoal.Maximum,
            Constraints = [new SolverConstraint(Cell(0), ConstraintOperator.Integer)],
        });

        Assert.Equal(3, outcome.Variables[0], 6);
    }

    [Fact]
    public void SenzaVariabiliNonSiRisolveNulla()
    {
        var (_, engine) = fixture.CreateWorkbook();

        var outcome = Solver.Solve(engine, new SolverRequest
        {
            Objective = Cell(1),
            Variables = [],
        });

        Assert.False(outcome.Succeeded);
    }

    [Fact]
    public void LeCelleVariabiliTornanoComeErano()
    {
        var (_, engine) = fixture.CreateWorkbook();

        engine.SetValue(Cell(0), CellValue.Number(3));
        engine.SetFormula(Cell(1), "=0-(A1-9)^2");

        Solver.Solve(engine, new SolverRequest
        {
            Objective = Cell(1),
            Variables = [Cell(0)],
            Goal = SolverGoal.Maximum,
        });

        Assert.Equal(3, engine.GetValue(Cell(0)).AsNumber());
    }
}
