using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class DynamicArrayTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static SheetCell C(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return new SheetCell(0, address);
    }

    private (Workbook Workbook, CalculationEngine Engine) Nuovo() => fixture.CreateWorkbook();

    [Fact]
    public void UnaMatriceSiRiversaNelleCelleSottostanti()
    {
        var (_, engine) = Nuovo();
        engine.SetFormula(C("A1"), "=SEQUENCE(4)");

        Assert.Equal(1, engine.GetValue(C("A1")).AsNumber());
        Assert.Equal(2, engine.GetValue(C("A2")).AsNumber());
        Assert.Equal(3, engine.GetValue(C("A3")).AsNumber());
        Assert.Equal(4, engine.GetValue(C("A4")).AsNumber());

        Assert.True(engine.IsSpilledInto(C("A2")));
        Assert.False(engine.IsSpilledInto(C("A1")));
    }

    [Fact]
    public void IlRiversamentoOccupaAncheLeColonne()
    {
        var (_, engine) = Nuovo();
        engine.SetFormula(C("A1"), "=SEQUENCE(2;3)");

        Assert.Equal(1, engine.GetValue(C("A1")).AsNumber());
        Assert.Equal(3, engine.GetValue(C("C1")).AsNumber());
        Assert.Equal(4, engine.GetValue(C("A2")).AsNumber());
        Assert.Equal(6, engine.GetValue(C("C2")).AsNumber());

        Assert.Equal(new CellRange(new CellAddress(0, 0), new CellAddress(1, 2)),
                     engine.SpillRanges[C("A1")]);
    }

    [Fact]
    public void UnOstacoloProduceSpillEnonScriveNulla()
    {
        var (wb, engine) = Nuovo();
        engine.SetValue(C("A3"), CellValue.Text("occupata"));
        engine.SetFormula(C("A1"), "=SEQUENCE(4)");

        Assert.Equal(CellError.Spill, engine.GetValue(C("A1")).Error);

        Assert.True(wb.Sheets[0][new CellAddress(1, 0)].Value.IsEmpty);
        Assert.Equal("occupata", engine.GetValue(C("A3")).AsText());
    }

    [Fact]
    public void RimuovendoLOstacoloLaMatriceSiRiversa()
    {
        var (_, engine) = Nuovo();
        engine.SetValue(C("A3"), CellValue.Number(99));
        engine.SetFormula(C("A1"), "=SEQUENCE(4)");
        Assert.Equal(CellError.Spill, engine.GetValue(C("A1")).Error);

        engine.SetValue(C("A3"), CellValue.Empty);
        Assert.Equal(1, engine.GetValue(C("A1")).AsNumber());
        Assert.Equal(3, engine.GetValue(C("A3")).AsNumber());
    }

    [Fact]
    public void UnaMatriceCheSiRestringeLiberaLeCelleInEccesso()
    {
        var (wb, engine) = Nuovo();
        engine.SetValue(C("C1"), CellValue.Number(5));
        engine.SetFormula(C("A1"), "=SEQUENCE(C1)");
        Assert.Equal(5, engine.GetValue(C("A5")).AsNumber());

        engine.SetValue(C("C1"), CellValue.Number(2));

        Assert.Equal(2, engine.GetValue(C("A2")).AsNumber());

        Assert.True(wb.Sheets[0][new CellAddress(4, 0)].Value.IsEmpty);
        Assert.False(engine.IsSpilledInto(C("A5")));
    }

    [Fact]
    public void IlRiferimentoConCancellettoPrendeTuttaLArea()
    {
        var (_, engine) = Nuovo();
        engine.SetFormula(C("A1"), "=SEQUENCE(4)");
        engine.SetFormula(C("C1"), "=SUM(A1#)");

        Assert.Equal(10, engine.GetValue(C("C1")).AsNumber());

        engine.SetFormula(C("A1"), "=SEQUENCE(3)");
        Assert.Equal(6, engine.GetValue(C("C1")).AsNumber());
    }

    [Fact]
    public void IlCancellettoSuUnaCellaSenzaMatriceEUnRiferimentoRotto()
    {
        var (_, engine) = Nuovo();
        engine.SetValue(C("A1"), CellValue.Number(1));
        engine.SetFormula(C("C1"), "=SUM(A1#)");

        Assert.Equal(CellError.Reference, engine.GetValue(C("C1")).Error);
    }

    [Fact]
    public void CioCheDipendeDaUnaCellaRiversataSiAggiorna()
    {
        var (_, engine) = Nuovo();
        engine.SetFormula(C("A1"), "=SEQUENCE(3)");
        engine.SetFormula(C("C1"), "=A2*10");

        Assert.Equal(20, engine.GetValue(C("C1")).AsNumber());
    }

    [Fact]
    public void LaFormulaMatricialeClassicaRiempieLAreaScelta()
    {
        var (wb, engine) = Nuovo();
        var area = new CellRange(new CellAddress(0, 0), new CellAddress(2, 0));
        engine.SetArrayFormula(C("A1"), area, "=SEQUENCE(3)");

        Assert.Equal(1, engine.GetValue(C("A1")).AsNumber());
        Assert.Equal(3, engine.GetValue(C("A3")).AsNumber());

        Assert.True(wb.Sheets[0][new CellAddress(1, 0)].IsPartOfArrayFormula);
        Assert.False(engine.IsSpilledInto(C("A2")));
    }

    [Fact]
    public void UnAreaPiuGrandeDellaMatriceMostraNonDisponibile()
    {
        var (_, engine) = Nuovo();
        var area = new CellRange(new CellAddress(0, 0), new CellAddress(4, 0));
        engine.SetArrayFormula(C("A1"), area, "=SEQUENCE(3)");

        Assert.Equal(3, engine.GetValue(C("A3")).AsNumber());
        Assert.Equal(CellError.NotAvailable, engine.GetValue(C("A4")).Error);
    }

    [Fact]
    public void OrdinaEFiltraLavoranoSuInteriIntervalli()
    {
        var (wb, engine) = Nuovo();
        double[] valori = [30, 10, 20];
        for (var i = 0; i < valori.Length; i++)
            wb.Sheets[0].SetValue(new CellAddress(i, 0), CellValue.Number(valori[i]));
        engine.RecalculateAll();

        engine.SetFormula(C("C1"), "=SORT(A1:A3)");
        Assert.Equal(10, engine.GetValue(C("C1")).AsNumber());
        Assert.Equal(20, engine.GetValue(C("C2")).AsNumber());
        Assert.Equal(30, engine.GetValue(C("C3")).AsNumber());

        engine.SetFormula(C("E1"), "=FILTER(A1:A3;A1:A3>15)");
        Assert.Equal(30, engine.GetValue(C("E1")).AsNumber());
        Assert.Equal(20, engine.GetValue(C("E2")).AsNumber());
    }

    [Fact]
    public void TrasponiScambiaRigheEColonne()
    {
        var (_, engine) = Nuovo();
        engine.SetFormula(C("A1"), "=SEQUENCE(1;3)");
        engine.SetFormula(C("A3"), "=TRANSPOSE(A1#)");

        Assert.Equal(1, engine.GetValue(C("A3")).AsNumber());
        Assert.Equal(2, engine.GetValue(C("A4")).AsNumber());
        Assert.Equal(3, engine.GetValue(C("A5")).AsNumber());
    }

    [Fact]
    public void LAlgebraDelleMatriciRispettaLeDimensioni()
    {
        var (_, engine) = Nuovo();
        engine.SetFormula(C("A1"), "=MUNIT(3)");
        engine.SetFormula(C("F1"), "=MDETERM(A1#)");

        Assert.Equal(1, engine.GetValue(C("F1")).AsNumber(), 9);
    }

    [Fact]
    public void UnaMatriceInContestoScalareSiRiduceAlPrimoElemento()
    {
        var (_, engine) = Nuovo();
        engine.SetFormula(C("A1"), "=SEQUENCE(3)+0");

        Assert.Equal(1, engine.GetValue(C("A1")).AsNumber());
    }

    [Fact]
    public void ScrivereInUnaCellaRiversataBloccaLoSpillESalvaIlDato()
    {
        var (workbook, engine) = Nuovo();
        engine.SetValue(C("B1"), CellValue.Number(3));
        engine.SetFormula(C("A1"), "=SEQUENCE(B1)");
        engine.SetValue(C("A2"), CellValue.Number(99));

        Assert.Equal(CellError.Spill, engine.GetValue(C("A1")).Error);

        engine.SetValue(C("B1"), CellValue.Number(4));

        Assert.Equal(99, engine.GetValue(C("A2")).AsNumber());
        Assert.Equal(CellError.Spill, engine.GetValue(C("A1")).Error);
    }

    [Fact]
    public void ChiLeggeUnaCellaRiversataSiAggiornaDopoIlRicalcoloCompleto()
    {
        var (_, engine) = Nuovo();
        engine.SetFormula(C("C1"), "=A2*10");
        engine.SetFormula(C("A1"), "=SEQUENCE(3)");
        engine.RecalculateAll();

        Assert.Equal(20, engine.GetValue(C("C1")).AsNumber());
    }
}
