using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class DependencyTracerTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static readonly IDependencyTracer Tracer = new DependencyTracer();

    private static SheetCell Cell(int row, int column = 0) => new(0, new CellAddress(row, column));

    [Fact]
    public void IPrecedentiSonoLeCelleLetteDallaFormula()
    {
        var (_, engine) = fixture.CreateWorkbook();

        engine.SetValue(Cell(0), CellValue.Number(1));
        engine.SetValue(Cell(1), CellValue.Number(2));
        engine.SetFormula(Cell(2), "=A1+A2");

        var arrows = Tracer.Precedents(engine, Cell(2));

        Assert.Equal(2, arrows.Count);
        Assert.All(arrows, arrow => Assert.Equal(new CellAddress(2, 0), arrow.To));
        Assert.Equal(new CellAddress(0, 0), arrows[0].From);
    }

    [Fact]
    public void UnIntervalloSiEspandeInCelle()
    {
        var (_, engine) = fixture.CreateWorkbook();

        for (var row = 0; row < 3; row++) engine.SetValue(Cell(row), CellValue.Number(row + 1));
        engine.SetFormula(Cell(4), "=SUM(A1:A3)");

        Assert.Equal(3, Tracer.Precedents(engine, Cell(4)).Count);
    }

    [Fact]
    public void IDipendentiSonoLeFormuleCheLeggonoLaCella()
    {
        var (_, engine) = fixture.CreateWorkbook();

        engine.SetValue(Cell(0), CellValue.Number(5));
        engine.SetFormula(Cell(1), "=A1*2");
        engine.SetFormula(Cell(2), "=A1+1");

        var arrows = Tracer.Dependents(engine, Cell(0));

        Assert.Equal(2, arrows.Count);
        Assert.All(arrows, arrow => Assert.Equal(new CellAddress(0, 0), arrow.From));
    }

    [Fact]
    public void UnaCellaSenzaFormuleNonHaPrecedenti()
    {
        var (_, engine) = fixture.CreateWorkbook();

        engine.SetValue(Cell(0), CellValue.Number(5));

        Assert.Empty(Tracer.Precedents(engine, Cell(0)));
    }

    [Fact]
    public void LOrigineDellErroreSiRisaleFinoInFondo()
    {
        var (_, engine) = fixture.CreateWorkbook();

        engine.SetFormula(Cell(0), "=1/0");
        engine.SetFormula(Cell(1), "=A1*2");
        engine.SetFormula(Cell(2), "=A2+1");

        var arrows = Tracer.ErrorSources(engine, Cell(2));

        Assert.Equal(2, arrows.Count);
        Assert.All(arrows, arrow => Assert.Equal(TraceKind.Error, arrow.Kind));
    }

    [Fact]
    public void SeLaCellaNonEInErroreNonSiRisaleNulla()
    {
        var (_, engine) = fixture.CreateWorkbook();

        engine.SetValue(Cell(0), CellValue.Number(1));
        engine.SetFormula(Cell(1), "=A1*2");

        Assert.Empty(Tracer.ErrorSources(engine, Cell(1)));
    }
}
