using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class LambdaTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static SheetCell C(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return new SheetCell(0, address);
    }

    private (Workbook Workbook, CalculationEngine Engine) Nuovo() => fixture.CreateWorkbook();

    private CellValue Valuta(string formula)
    {
        var (_, engine) = Nuovo();
        engine.SetFormula(C("A1"), formula);
        return engine.GetValue(C("A1"));
    }

    private double Calcola(string formula)
    {
        var value = Valuta(formula);
        Assert.False(value.IsError, $"{formula} ha prodotto {value}");
        return value.AsNumber();
    }

    [Fact]
    public void LetLegaUnNomeEPoiLoUsa() =>
        Assert.Equal(25, Calcola("=LET(x;5;x*x)"));

    [Fact]
    public void LetLegaPiuNomiInSequenza() =>

        Assert.Equal(30, Calcola("=LET(x;5;y;x+1;x*y)"));

    [Fact]
    public void UnNomeLocaleNascondeQuelloDelDocumento()
    {
        var (wb, engine) = Nuovo();
        wb.DefineName(new DefinedName("Aliquota", "0.22"));

        engine.SetFormula(C("A1"), "=LET(Aliquota;0.5;100*Aliquota)");
        Assert.Equal(50, engine.GetValue(C("A1")).AsNumber());

        engine.SetFormula(C("A2"), "=100*Aliquota");
        Assert.Equal(22, engine.GetValue(C("A2")).AsNumber(), 9);
    }

    [Fact]
    public void UnaLambdaSiPuoChiamareTramiteUnNomeLocale() =>
        Assert.Equal(6, Calcola("=LET(doppio;LAMBDA(x;x*2);doppio(3))"));

    [Fact]
    public void LaLambdaCatturaLAmbienteInCuiENata() =>

        Assert.Equal(15, Calcola("=LET(fattore;5;scala;LAMBDA(x;x*fattore);scala(3))"));

    [Fact]
    public void UnaLambdaChiamataConArgomentiSbagliatiEUnErrore() =>
        Assert.Equal(CellError.Value, Valuta("=LET(f;LAMBDA(x;y;x+y);f(1))").Error);

    [Fact]
    public void MapApplicaLaLambdaAOgniElemento()
    {
        var (wb, engine) = Nuovo();
        for (var i = 0; i < 3; i++)
            wb.Sheets[0].SetValue(new CellAddress(i, 0), CellValue.Number(i + 1));
        engine.RecalculateAll();

        engine.SetFormula(C("C1"), "=MAP(A1:A3;LAMBDA(v;v*10))");

        Assert.Equal(10, engine.GetValue(C("C1")).AsNumber());
        Assert.Equal(20, engine.GetValue(C("C2")).AsNumber());
        Assert.Equal(30, engine.GetValue(C("C3")).AsNumber());
    }

    [Fact]
    public void MapAccettaPiuMatriciInParallelo()
    {
        var (wb, engine) = Nuovo();
        for (var i = 0; i < 3; i++)
        {
            wb.Sheets[0].SetValue(new CellAddress(i, 0), CellValue.Number(i + 1));
            wb.Sheets[0].SetValue(new CellAddress(i, 1), CellValue.Number((i + 1) * 100));
        }
        engine.RecalculateAll();

        engine.SetFormula(C("D1"), "=MAP(A1:A3;B1:B3;LAMBDA(a;b;a+b))");

        Assert.Equal(101, engine.GetValue(C("D1")).AsNumber());
        Assert.Equal(303, engine.GetValue(C("D3")).AsNumber());
    }

    [Fact]
    public void ReduceAccumulaUnValoreSolo()
    {
        var (wb, engine) = Nuovo();
        for (var i = 0; i < 4; i++)
            wb.Sheets[0].SetValue(new CellAddress(i, 0), CellValue.Number(i + 1));
        engine.RecalculateAll();

        engine.SetFormula(C("C1"), "=REDUCE(0;A1:A4;LAMBDA(acc;v;acc+v))");
        Assert.Equal(10, engine.GetValue(C("C1")).AsNumber());

        engine.SetFormula(C("C2"), "=REDUCE(1;A1:A4;LAMBDA(acc;v;acc*v))");
        Assert.Equal(24, engine.GetValue(C("C2")).AsNumber());
    }

    [Fact]
    public void ScanConservaIRisultatiIntermedi()
    {
        var (wb, engine) = Nuovo();
        for (var i = 0; i < 4; i++)
            wb.Sheets[0].SetValue(new CellAddress(i, 0), CellValue.Number(i + 1));
        engine.RecalculateAll();

        engine.SetFormula(C("C1"), "=SCAN(0;A1:A4;LAMBDA(acc;v;acc+v))");

        Assert.Equal(1, engine.GetValue(C("C1")).AsNumber());
        Assert.Equal(3, engine.GetValue(C("C2")).AsNumber());
        Assert.Equal(6, engine.GetValue(C("C3")).AsNumber());
        Assert.Equal(10, engine.GetValue(C("C4")).AsNumber());
    }

    [Fact]
    public void ByRowRiduceOgniRigaAUnValore()
    {
        var (wb, engine) = Nuovo();

        var valore = 1;
        for (var row = 0; row < 2; row++)
            for (var column = 0; column < 3; column++)
                wb.Sheets[0].SetValue(new CellAddress(row, column), CellValue.Number(valore++));
        engine.RecalculateAll();

        engine.SetFormula(C("E1"), "=BYROW(A1:C2;LAMBDA(riga;SUM(riga)))");

        Assert.Equal(6, engine.GetValue(C("E1")).AsNumber());
        Assert.Equal(15, engine.GetValue(C("E2")).AsNumber());
    }

    [Fact]
    public void ByColRiduceOgniColonnaEProduceUnVettoreOrizzontale()
    {
        var (wb, engine) = Nuovo();
        var valore = 1;
        for (var row = 0; row < 2; row++)
            for (var column = 0; column < 3; column++)
                wb.Sheets[0].SetValue(new CellAddress(row, column), CellValue.Number(valore++));
        engine.RecalculateAll();

        engine.SetFormula(C("E1"), "=BYCOL(A1:C2;LAMBDA(col;SUM(col)))");

        Assert.Equal(5, engine.GetValue(C("E1")).AsNumber());
        Assert.Equal(7, engine.GetValue(C("F1")).AsNumber());
        Assert.Equal(9, engine.GetValue(C("G1")).AsNumber());
    }

    [Fact]
    public void MakeArrayCostruisceDagliIndici()
    {
        var (_, engine) = Nuovo();
        engine.SetFormula(C("A1"), "=MAKEARRAY(2;3;LAMBDA(r;c;r*10+c))");

        Assert.Equal(11, engine.GetValue(C("A1")).AsNumber());
        Assert.Equal(13, engine.GetValue(C("C1")).AsNumber());
        Assert.Equal(21, engine.GetValue(C("A2")).AsNumber());
        Assert.Equal(23, engine.GetValue(C("C2")).AsNumber());
    }

    [Fact]
    public void MakeArrayHaUnTettoDiSicurezza() =>
        Assert.Equal(CellError.Number, Valuta("=MAKEARRAY(2000;2000;LAMBDA(r;c;1))").Error);

    [Fact]
    public void UnaLambdaSenzaChiamataDaCalc()
    {
        var value = Valuta("=LAMBDA(x;x*2)");
        Assert.Equal(PulseOffice.Sheets.Values.CellError.Calc, value.Error);
    }

    [Fact]
    public void LaRicorsioneSenzaFineVianeFermata()
    {
        var value = Valuta("=LET(f;LAMBDA(x;f(x));f(1))");
        Assert.True(value.IsError);
    }

    [Fact]
    public void LeFormeSpecialiSonoNoteAlRegistro()
    {
        foreach (var name in (string[])["LAMBDA", "LET", "MAP", "REDUCE", "SCAN", "BYROW", "BYCOL", "MAKEARRAY", "ISOMITTED"])
            Assert.NotNull(fixture.SpecialForms.Find(name));
    }

    [Fact]
    public void Una_lambda_si_puo_chiamare_subito_anche_con_i_parametri_scritti_da_Excel()
    {
        Assert.Equal(6, Calcola("=LAMBDA(x;x*2)(3)"));
        Assert.Equal(1, Calcola("=LAMBDA(1)()"));
        Assert.Equal(7, Calcola("=LAMBDA(_xlpm.a;_xlpm.b;_xlpm.a+_xlpm.b)(3;4)"));
    }

    [Fact]
    public void Parametri_facoltativi_LET_senza_nomi_e_lambda_con_nome_come_nel_riferimento()
    {
        Assert.Equal(5, Calcola("=LAMBDA(_xlop.x;IF(ISOMITTED(_xlpm.x);12;5))(1)"));
        Assert.Equal(12, Calcola("=LAMBDA(_xlop.x;IF(ISOMITTED(_xlpm.x);12;5))()"));
        Assert.Equal(1, Calcola("=LET(1)"));
        Assert.Equal(CellError.Value, Valuta("=LAMBDA(_xlop.x;_xlpm.y;0)").Error);

        var (workbook, engine) = Nuovo();
        workbook.DefineName(new DefinedName("PIU2", "=LAMBDA(x;x+2)"));
        var cella = new SheetCell(0, new PulseOffice.Sheets.Addressing.CellAddress(0, 0));
        engine.SetFormula(cella, "=PIU2(5)");
        Assert.Equal(7, engine.GetValue(cella).AsNumber());
    }
}
