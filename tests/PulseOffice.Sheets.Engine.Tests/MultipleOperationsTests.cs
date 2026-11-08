using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class MultipleOperationsTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static readonly IMultipleOperations Operations = new MultipleOperations();

    private static SheetCell Cell(int row, int column) => new(0, new CellAddress(row, column));

    [Fact]
    public void UnaColonnaDiIngressiProduceUnaColonnaDiRisultati()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];

        engine.SetValue(Cell(0, 0), CellValue.Number(1));
        engine.SetFormula(Cell(0, 1), "=A1*10");

        foreach (var (row, value) in new[] { (3, 2.0), (4, 5.0), (5, 7.0) })
            engine.SetValue(Cell(row, 0), CellValue.Number(value));

        var result = Operations.Compute(engine, 0, new MultipleOperationsRequest
        {
            Target = new CellRange(new CellAddress(3, 0), new CellAddress(5, 1)),
            Formulas = [Cell(0, 1)],
            ColumnInput = Cell(0, 0),
        });

        Assert.Equal(20, result.Values[new CellAddress(3, 1)].AsNumber());
        Assert.Equal(50, result.Values[new CellAddress(4, 1)].AsNumber());
        Assert.Equal(70, result.Values[new CellAddress(5, 1)].AsNumber());
    }

    [Fact]
    public void LaCellaDIngressoTornaComEra()
    {
        var (_, engine) = fixture.CreateWorkbook();

        engine.SetValue(Cell(0, 0), CellValue.Number(1));
        engine.SetFormula(Cell(0, 1), "=A1*10");
        engine.SetValue(Cell(3, 0), CellValue.Number(9));

        Operations.Compute(engine, 0, new MultipleOperationsRequest
        {
            Target = new CellRange(new CellAddress(3, 0), new CellAddress(3, 1)),
            Formulas = [Cell(0, 1)],
            ColumnInput = Cell(0, 0),
        });

        Assert.Equal(1, engine.GetValue(Cell(0, 0)).AsNumber());
    }

    [Fact]
    public void DueIngressiRiempionoUnaGrigliaIncrociata()
    {
        var (_, engine) = fixture.CreateWorkbook();

        engine.SetValue(Cell(0, 0), CellValue.Number(1));
        engine.SetValue(Cell(1, 0), CellValue.Number(1));
        engine.SetFormula(Cell(0, 1), "=A1*A2");

        engine.SetValue(Cell(4, 1), CellValue.Number(2));
        engine.SetValue(Cell(4, 2), CellValue.Number(3));
        engine.SetValue(Cell(5, 0), CellValue.Number(10));
        engine.SetValue(Cell(6, 0), CellValue.Number(20));

        var result = Operations.Compute(engine, 0, new MultipleOperationsRequest
        {
            Target = new CellRange(new CellAddress(4, 0), new CellAddress(6, 2)),
            Formulas = [Cell(0, 1)],
            RowInput = Cell(0, 0),
            ColumnInput = Cell(1, 0),
        });

        Assert.Equal(20, result.Values[new CellAddress(5, 1)].AsNumber());
        Assert.Equal(30, result.Values[new CellAddress(5, 2)].AsNumber());
        Assert.Equal(40, result.Values[new CellAddress(6, 1)].AsNumber());
        Assert.Equal(60, result.Values[new CellAddress(6, 2)].AsNumber());
    }

    [Fact]
    public void PiuFormuleDannoPiuColonne()
    {
        var (_, engine) = fixture.CreateWorkbook();

        engine.SetValue(Cell(0, 0), CellValue.Number(1));
        engine.SetFormula(Cell(0, 1), "=A1*10");
        engine.SetFormula(Cell(0, 2), "=A1+100");
        engine.SetValue(Cell(3, 0), CellValue.Number(4));

        var result = Operations.Compute(engine, 0, new MultipleOperationsRequest
        {
            Target = new CellRange(new CellAddress(3, 0), new CellAddress(3, 2)),
            Formulas = [Cell(0, 1), Cell(0, 2)],
            ColumnInput = Cell(0, 0),
        });

        Assert.Equal(40, result.Values[new CellAddress(3, 1)].AsNumber());
        Assert.Equal(104, result.Values[new CellAddress(3, 2)].AsNumber());
    }

    [Fact]
    public void SenzaCelleDIngressoNonSiCalcolaNulla()
    {
        var (_, engine) = fixture.CreateWorkbook();

        var result = Operations.Compute(engine, 0, new MultipleOperationsRequest
        {
            Target = new CellRange(new CellAddress(3, 0), new CellAddress(5, 1)),
            Formulas = [Cell(0, 1)],
        });

        Assert.Empty(result.Values);
    }

    [Fact]
    public void LeRigheSenzaIngressoVengonoSaltate()
    {
        var (_, engine) = fixture.CreateWorkbook();

        engine.SetValue(Cell(0, 0), CellValue.Number(1));
        engine.SetFormula(Cell(0, 1), "=A1*10");
        engine.SetValue(Cell(3, 0), CellValue.Number(2));

        var result = Operations.Compute(engine, 0, new MultipleOperationsRequest
        {
            Target = new CellRange(new CellAddress(3, 0), new CellAddress(5, 1)),
            Formulas = [Cell(0, 1)],
            ColumnInput = Cell(0, 0),
        });

        Assert.Single(result.Values);
    }
}
