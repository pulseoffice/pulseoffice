using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class DeepAuditSheetsEngineTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private CellValue Valuta(string formula)
    {
        var (_, engine) = fixture.CreateWorkbook();
        engine.SetValue(new SheetCell(0, new CellAddress(0, 0)), CellValue.Text("x"));
        engine.SetValue(new SheetCell(0, new CellAddress(1, 0)), CellValue.Number(1));
        engine.SetFormula(new SheetCell(0, new CellAddress(0, 3)), formula);
        return engine.GetValue(new SheetCell(0, new CellAddress(0, 3)));
    }

    [Theory]
    [InlineData("=OR(0;\"\")")]
    [InlineData("=AND(1;\"a\")")]
    [InlineData("=XOR(1;\"a\")")]
    public void IConnettiviLogiciRifiutanoIlTestoScrittoComeArgomento(string formula) =>
        Assert.Equal(CellError.Value, Valuta(formula).Error);

    [Theory]
    [InlineData("=AND(1;\"TRUE\")", true)]
    [InlineData("=OR(0;\"false\")", false)]
    [InlineData("=AND(1;A1:A2)", true)]
    [InlineData("=OR(A1)", null)]
    public void IConnettiviLogiciLeggonoIValoriLogiciScrittiEIgnoranoIlTestoNeiRiferimenti(string formula, bool? atteso)
    {
        var value = Valuta(formula);
        if (atteso is null) Assert.Equal(CellError.Value, value.Error);
        else Assert.Equal(atteso, value.AsBoolean());
    }

    [Theory]
    [InlineData("=TIMEVALUE(\"25:00\")", 1.0 / 24)]
    [InlineData("=TIMEVALUE(\"48:30:30\")", (30 * 60 + 30) / 86400.0)]
    [InlineData("=TIMEVALUE(\"12:00\")", 0.5)]
    public void TimevalueAccettaLeDurateOltreLeVentiquattroOre(string formula, double atteso) =>
        Assert.Equal(atteso, Valuta(formula).AsNumber(), 12);

    [Theory]
    [InlineData("=VALUE(\"(12)\")", -12)]
    [InlineData("=VALUE(\" (1.5) \")", -1.5)]
    public void ValueLeggeINegativiTraParentesi(string formula, double atteso) =>
        Assert.Equal(atteso, Valuta(formula).AsNumber());

    [Fact]
    public void ValueRifiutaLeParentesiSenzaNumero() =>
        Assert.Equal(CellError.Value, Valuta("=VALUE(\"(a)\")").Error);

    [Fact]
    public void LArgomentoDiZeroNonEDefinito() =>
        Assert.Equal(CellError.Number, Valuta("=IMARGUMENT(\"0\")").Error);

    [Fact]
    public void UnFoglioConLApostrofoNelNomeSiRiferisceNelleFormule()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.AddSheet("L'anno");
        sheet.SetValue(new CellAddress(0, 0), CellValue.Number(7));
        engine.SetFormula(new SheetCell(0, new CellAddress(0, 0)), "='L''anno'!A1*2");
        Assert.Equal(14, engine.GetValue(new SheetCell(0, new CellAddress(0, 0))).AsNumber());
    }

    [Theory]
    [InlineData("=ROUNDDOWN(1.5;400)", 1.5)]
    [InlineData("=ROUNDUP(1.5;400)", 1.5)]
    [InlineData("=TRUNC(1.5;10000000000)", 1.5)]
    [InlineData("=TRUNC(-1.5;-10000000000)", 0)]
    public void TroncareConMoltissimeCifreNonPerdeIlValore(string formula, double atteso) =>
        Assert.Equal(atteso, Valuta(formula).AsNumber());

    [Theory]
    [InlineData("=FACTDOUBLE(10000000000)")]
    [InlineData("=GCD(1E+20)")]
    [InlineData("=LCM(4294967296;4294967297)")]
    public void InteriFuoriPortataDannoErroreNumerico(string formula) =>
        Assert.Equal(CellError.Number, Valuta(formula).Error);

    [Theory]
    [InlineData("=SEQUENCE(100000;100000)")]
    [InlineData("=SEQUENCE(2000000)")]
    [InlineData("=MUNIT(1000000)")]
    [InlineData("=RANDARRAY(100000;100000)")]
    [InlineData("=EXPAND(A1;100000;100000)")]
    [InlineData("=WRAPROWS(A1;1000000000)")]
    public void LeMatriciTroppoGrandiDannoErroreSenzaAllocare(string formula) =>
        Assert.Equal(CellError.Number, Valuta(formula).Error);

    [Fact]
    public void ExpandConDimensioniNegativeEUnErroreNumericoComeNelRiferimento() =>
        Assert.Equal(CellError.Number, Valuta("=EXPAND(A1:A2;-3)").Error);

    [Theory]
    [InlineData("=RANDBETWEEN(0.5;0.7)")]
    [InlineData("=RANDARRAY(1;1;0.5;0.7;TRUE())")]
    public void GliEstremiInteriCasualiSiArrotondanoInSuComeNelRiferimento(string formula) =>
        Assert.Equal(1, Valuta(formula).AsNumber());

    private CellValue ValutaConErroreInB1(string formula)
    {
        var (_, engine) = fixture.CreateWorkbook();
        engine.SetValue(new SheetCell(0, new CellAddress(0, 0)), CellValue.Text("x"));
        engine.SetValue(new SheetCell(0, new CellAddress(1, 0)), CellValue.Number(1));
        engine.SetFormula(new SheetCell(0, new CellAddress(0, 1)), "=1/0");
        engine.SetValue(new SheetCell(0, new CellAddress(1, 1)), CellValue.Number(5));
        engine.SetFormula(new SheetCell(0, new CellAddress(0, 3)), formula);
        return engine.GetValue(new SheetCell(0, new CellAddress(0, 3)));
    }

    [Theory]
    [InlineData("=SUMIF(A1:A2;\"x\";B1:B2)")]
    [InlineData("=AVERAGEIF(A1:A2;\"<>z\";B1:B2)")]
    [InlineData("=SUMIFS(B1:B2;A1:A2;\"x\")")]
    [InlineData("=MAXIFS(B1:B2;A1:A2;\"x\")")]
    public void UnErroreNelleCelleSceltePerLaSommaSiPropagaComeNelRiferimento(string formula) =>
        Assert.Equal(CellError.DivideByZero, ValutaConErroreInB1(formula).Error);

    [Fact]
    public void UnErroreNelleCelleNonScelteNonConta() =>
        Assert.Equal(5, ValutaConErroreInB1("=SUMIF(A1:A2;1;B1:B2)").AsNumber());

    [Theory]
    [InlineData("=NEGBINOM.DIST(3;5;0.5;TRUE())", 0.36328125)]
    [InlineData("=NEGBINOM.DIST(1E+12;5;0.5;TRUE())", 1)]
    [InlineData("=RANDBETWEEN.NV(0.5;0.7)", 1)]
    public void DistribuzioniEdEstremiComeNelRiferimento(string formula, double atteso) =>
        Assert.Equal(atteso, Valuta(formula).AsNumber(), 10);

    [Theory]
    [InlineData("=TIME(1193047;0;0)", 7.0 / 24)]
    [InlineData("=TIME(600000;30;0)", 0.5 / 24)]
    public void TimeConMoltissimeOreNonTraboccaComeNelRiferimento(string formula, double atteso) =>
        Assert.Equal(atteso, Valuta(formula).AsNumber(), 9);

    [Theory]
    [InlineData("=BINOM.DIST(600;1200;0.5;FALSE())", 0.023028145269)]
    [InlineData("=CHISQDIST(100000;100000;TRUE())", 0.500594708096)]
    [InlineData("=GAMMA.DIST(100000;100000;1;TRUE())", 0.500420522183)]
    [InlineData("=BINOM.DIST(400;1000;0.4;TRUE())", 0.513729858287)]
    [InlineData("=POISSON(1000;1000;TRUE())", 0.508409367169)]
    public void DistribuzioniConParametriGrandiComeNelRiferimento(string formula, double atteso) =>
        Assert.Equal(atteso, Valuta(formula).AsNumber(), 9);

    [Theory]
    [InlineData("=BINOM.DIST(5E+11;1E+12;0.5;TRUE())", 0.5000004, 7)]
    [InlineData("=CRITBINOM(1000000;0.5;0.5)", 500000, 9)]
    [InlineData("=POISSON(1E+7;1E+7;TRUE())", 0.500084, 4)]
    public void DistribuzioniCumulateSenzaCicliLunghi(string formula, double atteso, int cifre) =>
        Assert.Equal(atteso, Valuta(formula).AsNumber(), cifre);

    [Theory]
    [InlineData("=BESSELJ(1;100000000)")]
    [InlineData("=BESSELY(1;1E+9)")]
    [InlineData("=BESSELK(1;1E+9)")]
    public void BesselConOrdiniEnormiDaErroreSubitoComeNelRiferimento(string formula)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        Assert.Equal(CellError.Number, Valuta(formula).Error);
        Assert.True(watch.ElapsedMilliseconds < 500, $"{watch.ElapsedMilliseconds} ms");
    }

    [Theory]
    [InlineData("=DDB(2400;300;10;2.5)", 343.460041)]
    [InlineData("=DDB(2400;300;10;2)", 384)]
    public void DdbConPeriodoFrazionarioComeNelRiferimento(string formula, double atteso) =>
        Assert.Equal(atteso, Valuta(formula).AsNumber(), 6);

    [Fact]
    public void DdbConValoreResiduoOltreIlCostoEUnErrore() =>
        Assert.Equal(CellError.Number, Valuta("=DDB(300;2400;10;2)").Error);

    [Fact]
    public void DdbSuMiliardiDiPeriodiSenzaCiclo()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        Assert.Equal(ValueKind.Number, Valuta("=DDB(1;0;1E+9;1E+9)").Kind);
        Assert.True(watch.ElapsedMilliseconds < 500, $"{watch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void BesselJConArgomentoEnormeUsaLAsintotica() =>
        Assert.Equal(0.0000246874720836085, Valuta("=BESSELJ(1E+9;0)").AsNumber(), 9);

    [Theory]
    [InlineData("=CUMIPMT(0.09/12;360;125000;13;24;1)", -11052.339584)]
    [InlineData("=CUMPRINC(0.09/12;360;125000;13;24;1)", -927.153472)]
    [InlineData("=CUMPRINC(0.09/12;360;125000;1;1;1)", -998.291088)]
    [InlineData("=CUMIPMT(0.09/12;360;125000;1;360;1)", -234384.791687)]
    [InlineData("=CUMIPMT(0.09/12;360;125000;1;1;0)", -937.5)]
    [InlineData("=CUMPRINC(0.09/12;360;125000;13;24;0)", -934.1071234208985)]
    public void CumulateDeiPagamentiComeNelRiferimento(string formula, double atteso) =>
        Assert.Equal(atteso, Valuta(formula).AsNumber(), 5);

    [Fact]
    public void CumulateSuMiliardiDiPeriodiSenzaCiclo()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var value = Valuta("=CUMPRINC(1E-9;1E+10;1000;1;2000000000;0)");
        Assert.True(value.Kind == ValueKind.Number && double.IsFinite(value.AsNumber()));
        Assert.True(watch.ElapsedMilliseconds < 500, $"{watch.ElapsedMilliseconds} ms");
    }

    [Theory]
    [InlineData("=FILTERXML(\"<!DOCTYPE a [<!ENTITY e \"\"x\"\">]><a>&e;</a>\";\"/a\")")]
    [InlineData("=FILTERXML(\"<!DOCTYPE a [<!ENTITY e \"\"xxxxxxxxxx\"\"><!ENTITY f \"\"&e;&e;&e;&e;&e;&e;&e;&e;&e;&e;\"\"><!ENTITY g \"\"&f;&f;&f;&f;&f;&f;&f;&f;&f;&f;\"\">]><a>&g;</a>\";\"/a\")")]
    public void FilterxmlRifiutaLeDichiarazioniDiTipoDelDocumento(string formula) =>
        Assert.Equal(CellError.Value, Valuta(formula).Error);

    [Fact]
    public void FourierSuMoltiCampioniNonEQuadratico()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var value = Valuta("=INDEX(FOURIER(SEQUENCE(20000);TRUE());1;1)");
        Assert.Equal(200010000, value.AsNumber(), 3);
        Assert.True(watch.ElapsedMilliseconds < 1000, $"{watch.ElapsedMilliseconds} ms");
    }

    [Theory]
    [InlineData("=INDEX(FOURIER(SEQUENCE(1000);TRUE());2;2)")]
    [InlineData("=INDEX(FOURIER(SEQUENCE(1000);TRUE();TRUE());3;1)")]
    public void FourierVeloceConcordaConLaDefinizione(string formula)
    {
        var n = 1000;
        var k = formula.Contains("2;2") ? 1 : 2;
        var inverse = formula.Contains("TRUE();TRUE()");
        var sign = inverse ? 1.0 : -1.0;
        var sum = System.Numerics.Complex.Zero;
        for (var t = 0; t < n; t++)
            sum += (t + 1) * System.Numerics.Complex.FromPolarCoordinates(1, sign * 2 * System.Math.PI * ((long)k * t % n) / n);
        var atteso = inverse ? sum.Real / n : sum.Imaginary;
        Assert.Equal(atteso, Valuta(formula).AsNumber(), 6);
    }

    [Theory]
    [InlineData("=B(1E+12;0.5;0;5E+11)", 0.5000004, 7)]
    [InlineData("=B(1200;0.5;600)", 0.023028145269, 9)]
    [InlineData("=B(10;0.3;2;4)", 0.7004233, 6)]
    public void LaBinomialeEsternoSenzaCicliLunghi(string formula, double atteso, int cifre) =>
        Assert.Equal(atteso, Valuta(formula).AsNumber(), cifre);

    [Fact]
    public void DollarConMoltiDecimaliNegativiNonScriveNaN() =>
        Assert.DoesNotContain("NaN", Valuta("=DOLLAR(1;-400)").AsText());

    [Theory]
    [InlineData("=XMATCH(1;A1:A2;3)")]
    [InlineData("=XMATCH(1;A1:A2;0;0)")]
    [InlineData("=XLOOKUP(1;A1:A2;A1:A2;;5)")]
    [InlineData("=XLOOKUP(1;A1:A2;A1:A2;;0;3)")]
    public void XlookupEXmatchRifiutanoModiInesistenti(string formula) =>
        Assert.Equal(CellError.Value, Valuta(formula).Error);

    [Theory]
    [InlineData("=BASE(5;2;100000)")]
    [InlineData("=BASE(5;2;1E+9)")]
    public void BaseConLunghezzaMinimaEnormeDaErroreComeNelRiferimento(string formula) =>
        Assert.Equal(CellError.Number, Valuta(formula).Error);

    [Fact]
    public void BaseConLunghezzaMinimaAmmessa() =>
        Assert.Equal(256, Valuta("=LEN(BASE(5;2;256))").AsNumber());

    [Theory]
    [InlineData("=CORREL({1;2;3};{5;5;5})")]
    [InlineData("=RSQ({5;5;5};{1;2;3})")]
    [InlineData("=PEARSON({1;2;3};{5;5;5})")]
    [InlineData("=CORREL({5;5;5};{1;2;3})")]
    public void CorrelazioneConUnaSerieCostanteDaDivisionePerZeroComeNelRiferimento(string formula) =>
        Assert.Equal(CellError.DivideByZero, Valuta(formula).Error);

    [Theory]
    [InlineData("=DB(1000000;100000;6;2;7)", 259639.42, 2)]
    [InlineData("=DB(1000000;100000;6;6;7)", 55841.76, 2)]
    [InlineData("=DB(1000000;100000;6;7;7)", 15845.10, 2)]
    public void DbComeNellEsempioDiExcel(string formula, double atteso, int cifre) =>
        Assert.Equal(atteso, Valuta(formula).AsNumber(), cifre);

    [Theory]
    [InlineData("=DB(10000;1000;1E+9;1E+9)")]
    [InlineData("=AMORDEGRC(10000;1;2;0;1E+9;1E-12)")]
    public void AmmortamentiSenzaUnCicloPerPeriodo(string formula)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        Assert.Equal(ValueKind.Number, Valuta(formula).Kind);
        Assert.True(watch.ElapsedMilliseconds < 500, $"{watch.ElapsedMilliseconds} ms");
    }

    [Theory]
    [InlineData("=LARGE({1;2;3};1.5)", 2)]
    [InlineData("=SMALL({1;2;3};1.2)", 1)]
    [InlineData("=LARGE({1;2;3};0.5)", 3)]
    public void LargeArrotondaInSuESmallInGiuComeNelRiferimento(string formula, double atteso) =>
        Assert.Equal(atteso, Valuta(formula).AsNumber());

    [Theory]
    [InlineData("=SUBTOTAL(9;F1:F5)", 6)]
    [InlineData("=SUBTOTAL(109;F1:F5)", 6)]
    [InlineData("=SUBTOTAL(2;F1:F5)", 3)]
    [InlineData("=AGGREGATE(9;0;F1:F5)", 6)]
    [InlineData("=AGGREGATE(9;4;F1:F5)", 18)]
    [InlineData("=SUM(F1:F5)", 18)]
    public void SubtotalIgnoraISubtotaliAnnidatiComeExcelEIlRiferimento(string formula, double atteso)
    {
        var (_, engine) = fixture.CreateWorkbook();
        for (var row = 0; row < 3; row++)
            engine.SetValue(new SheetCell(0, new CellAddress(row, 5)), CellValue.Number(row + 1));
        engine.SetFormula(new SheetCell(0, new CellAddress(3, 5)), "=SUBTOTAL(9;F1:F3)");
        engine.SetFormula(new SheetCell(0, new CellAddress(4, 5)), "=AGGREGATE(9;0;F1:F3)+0*SUBTOTAL(9;F1)");
        engine.SetFormula(new SheetCell(0, new CellAddress(0, 7)), formula);
        Assert.Equal(atteso, engine.GetValue(new SheetCell(0, new CellAddress(0, 7))).AsNumber());
    }

    [Theory]
    [InlineData("=MDETERM({1E-13;0|0;1E-13})*1E+26", 1)]
    [InlineData("=INDEX(MINVERSE({1E-13;0|0;1E-13});1;1)/1E+13", 1)]
    public void MatriciConElementiPiccoliNonSonoSingolariComeNelRiferimento(string formula, double atteso) =>
        Assert.Equal(atteso, Valuta(formula).AsNumber(), 9);

    [Theory]
    [InlineData("=IMPOWER(\"0\";-1)")]
    [InlineData("=IMPRODUCT(\"1E+200\";\"1E+200\")")]
    [InlineData("=IMSUB(\"1E+308\";\"-1E+308\")")]
    public void ComplessiNonFinitiDannoErroreNumerico(string formula) =>
        Assert.Equal(CellError.Number, Valuta(formula).Error);

    [Fact]
    public void IntervalloStagionaleMoltoLontanoDaErroreSenzaAllocare()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        Assert.Equal(CellError.Number, Valuta("=FORECAST.ETS.PI.ADD(100024;SEQUENCE(24);MOD(SEQUENCE(24);4)+SEQUENCE(24);0.95;4)").Error);
        Assert.True(watch.ElapsedMilliseconds < 1000, $"{watch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void IntervalloNonStagionaleLontanissimoSenzaAllocare()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        Assert.Equal(ValueKind.Number, Valuta("=FORECAST.ETS.PI.ADD(1E+9;SEQUENCE(24);MOD(SEQUENCE(24);4)+SEQUENCE(24);0.95;0)").Kind);
        Assert.True(watch.ElapsedMilliseconds < 1000, $"{watch.ElapsedMilliseconds} ms");
    }
}
