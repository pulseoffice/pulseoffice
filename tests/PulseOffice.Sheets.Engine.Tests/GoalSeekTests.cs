using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class GoalSeekTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static readonly IGoalSeek Seeker = new GoalSeek();

    private static SheetCell A(int row) => new(0, new CellAddress(row, 0));

    [Fact]
    public void TrovaIlValoreCheAzzeraLaDifferenza()
    {
        var (workbook, engine) = fixture.CreateWorkbook();

        engine.SetValue(A(0), CellValue.Number(1));
        engine.SetFormula(A(1), "=A1*3+2");

        var outcome = Seeker.Solve(engine, new GoalSeekRequest(A(1), A(0), 20));

        Assert.True(outcome.Found);
        Assert.Equal(6, outcome.Value, 6);
    }

    [Fact]
    public void FunzionaAncheSeLaFormulaNonELineare()
    {
        var (_, engine) = fixture.CreateWorkbook();

        engine.SetValue(A(0), CellValue.Number(2));
        engine.SetFormula(A(1), "=A1^2");

        var outcome = Seeker.Solve(engine, new GoalSeekRequest(A(1), A(0), 9));

        Assert.True(outcome.Found);
        Assert.Equal(3, Math.Abs(outcome.Value), 4);
    }

    [Fact]
    public void LaCellaVariabileTornaComEraDopoLaRicerca()
    {
        var (_, engine) = fixture.CreateWorkbook();

        engine.SetValue(A(0), CellValue.Number(1));
        engine.SetFormula(A(1), "=A1*3");

        Seeker.Solve(engine, new GoalSeekRequest(A(1), A(0), 30));

        Assert.Equal(1, engine.GetValue(A(0)).AsNumber());
    }

    [Fact]
    public void UnaFormulaCheNonDipendeDallaVariabileNonSiRisolve()
    {
        var (_, engine) = fixture.CreateWorkbook();

        engine.SetValue(A(0), CellValue.Number(1));
        engine.SetFormula(A(1), "=7");

        var outcome = Seeker.Solve(engine, new GoalSeekRequest(A(1), A(0), 30));

        Assert.False(outcome.Found);
    }

    [Fact]
    public void SeIlValoreEGiaQuelloCercatoNonSiIteraNemmenoUnaVolta()
    {
        var (_, engine) = fixture.CreateWorkbook();

        engine.SetValue(A(0), CellValue.Number(5));
        engine.SetFormula(A(1), "=A1*2");

        var outcome = Seeker.Solve(engine, new GoalSeekRequest(A(1), A(0), 10));

        Assert.True(outcome.Found);
        Assert.Equal(0, outcome.Iterations);
    }

    [Fact]
    public void UnaFormulaCheDaErroreNonFaCadereNulla()
    {
        var (_, engine) = fixture.CreateWorkbook();

        engine.SetValue(A(0), CellValue.Number(1));
        engine.SetFormula(A(1), "=1/(A1-A1)");

        var outcome = Seeker.Solve(engine, new GoalSeekRequest(A(1), A(0), 5));

        Assert.False(outcome.Found);
    }

    [Fact]
    public void SeIlPuntoDiPartenzaDaErroreSiRiprovaAccanto()
    {
        var (_, engine) = fixture.CreateWorkbook();

        engine.SetValue(A(0), CellValue.Number(0));
        engine.SetFormula(A(1), "=1/A1");

        var outcome = Seeker.Solve(engine, new GoalSeekRequest(A(1), A(0), 0.5));

        Assert.True(outcome.Found);
        Assert.Equal(2, outcome.Value, 4);
    }
}
