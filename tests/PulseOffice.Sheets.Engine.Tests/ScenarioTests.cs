using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class ScenarioTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static readonly IScenarioManager Manager = new ScenarioManager();

    private static readonly CellRange Inputs =
        new(new CellAddress(0, 0), new CellAddress(1, 0));

    private static SheetCell Cell(int row) => new(0, new CellAddress(row, 0));

    [Fact]
    public void UnstantaneaRegistraIValoriCorrenti()
    {
        var (_, engine) = fixture.CreateWorkbook();

        engine.SetValue(Cell(0), CellValue.Number(10));
        engine.SetValue(Cell(1), CellValue.Number(20));

        var scenario = Manager.Capture(engine, 0, Inputs, "Ottimista");

        Assert.Equal("Ottimista", scenario.Name);
        Assert.Equal(10, scenario.Values[new CellAddress(0, 0)].AsNumber());
        Assert.Equal(20, scenario.Values[new CellAddress(1, 0)].AsNumber());
    }

    [Fact]
    public void ApplicareUnoScenarioCambiaLeCelleEIlCalcolo()
    {
        var (_, engine) = fixture.CreateWorkbook();

        engine.SetValue(Cell(0), CellValue.Number(10));
        engine.SetValue(Cell(1), CellValue.Number(20));
        engine.SetFormula(Cell(2), "=A1+A2");

        var optimistic = Manager.Capture(engine, 0, Inputs, "Ottimista");

        engine.SetValue(Cell(0), CellValue.Number(1));
        engine.SetValue(Cell(1), CellValue.Number(2));

        Manager.Apply(engine, 0, optimistic);

        Assert.Equal(30, engine.GetValue(Cell(2)).AsNumber());
    }

    [Fact]
    public void ApplicareRestituisceIValoriDiPrimaPerPoterAnnullare()
    {
        var (_, engine) = fixture.CreateWorkbook();

        engine.SetValue(Cell(0), CellValue.Number(10));
        engine.SetValue(Cell(1), CellValue.Number(20));

        var scenario = Manager.Capture(engine, 0, Inputs, "Base");

        engine.SetValue(Cell(0), CellValue.Number(99));

        var previous = Manager.Apply(engine, 0, scenario);

        Assert.Equal(99, previous[new CellAddress(0, 0)].Value.AsNumber());
    }

    [Fact]
    public void LoScenarioSiTrovaPerNomeSenzaBadareAlleMaiuscole()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];

        sheet.Scenarios.Add(Manager.Capture(engine, 0, Inputs, "Prudente"));

        Assert.NotNull(Manager.Find(sheet, "prudente"));
        Assert.Null(Manager.Find(sheet, "inesistente"));
    }

    [Fact]
    public void UnoScenarioConservaERipristinaLeFormule()
    {
        var (workbook, engine) = fixture.CreateWorkbook();

        engine.SetValue(Cell(0), CellValue.Number(4));
        engine.SetFormula(Cell(1), "=A1*2");

        var scenario = Manager.Capture(engine, 0, Inputs, "Con formula");

        engine.SetValue(Cell(1), CellValue.Number(99));
        var previous = Manager.Apply(engine, 0, scenario);

        Assert.Equal("=A1*2", workbook.Sheets[0][new CellAddress(1, 0)].Formula);
        Assert.Equal(8, engine.GetValue(Cell(1)).AsNumber());

        engine.SetValue(Cell(0), CellValue.Number(5));
        Assert.Equal(10, engine.GetValue(Cell(1)).AsNumber());

        Manager.Restore(engine, 0, previous);
        Assert.False(workbook.Sheets[0][new CellAddress(1, 0)].HasFormula);
        Assert.Equal(99, engine.GetValue(Cell(1)).AsNumber());
    }
}
