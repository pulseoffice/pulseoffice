using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class FunctionBehaviourTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private (Workbook Workbook, CalculationEngine Engine) Nuovo() => fixture.CreateWorkbook();

    private static SheetCell C(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return new SheetCell(0, address);
    }

    private double Calcola(string formula)
    {
        var (_, engine) = Nuovo();
        engine.SetFormula(C("A1"), formula);
        var value = engine.GetValue(C("A1"));
        Assert.False(value.IsError, $"{formula} ha prodotto {value}");
        return value.AsNumber();
    }

    private CellValue Valuta(string formula)
    {
        var (_, engine) = Nuovo();
        engine.SetFormula(C("A1"), formula);
        return engine.GetValue(C("A1"));
    }

    [Theory]
    [InlineData("=CEILING(-4.5;-1)", -4)]
    [InlineData("=CEILING(-4.5;-1;1)", -5)]
    [InlineData("=FLOOR(-2.5;-2)", -4)]
    [InlineData("=CEILING.XCL(-4.5;-1)", -5)]
    [InlineData("=CEILING.PRECISE(-4.5;1)", -4)]
    [InlineData("=FLOOR.PRECISE(-4.5;1)", -5)]
    [InlineData("=ROUNDDOWN(-4.7;0)", -4)]
    [InlineData("=ROUNDUP(-4.2;0)", -5)]
    [InlineData("=EVEN(-1.5)", -2)]
    [InlineData("=ODD(-1.5)", -3)]
    public void ArrotondamentiSuiNegativi(string formula, double atteso) =>
        Assert.Equal(atteso, Calcola(formula), 10);

    [Fact]
    public void CeilingConSegniDiscordiEUnErrore() =>
        Assert.Equal(CellError.Number, Valuta("=CEILING(4.5;-1)").Error);

    [Theory]
    [InlineData("=COMBIN(10;3)", 120)]
    [InlineData("=PERMUT(10;3)", 720)]
    [InlineData("=FACT(10)", 3628800)]
    [InlineData("=GCD(24;36;60)", 12)]
    [InlineData("=LCM(4;6)", 12)]
    [InlineData("=ARABIC(\"MCMXCIV\")", 1994)]
    public void Combinatoria(string formula, double atteso) =>
        Assert.Equal(atteso, Calcola(formula), 6);

    [Fact]
    public void RomaniUsanoLaNotazioneSottrattiva() =>
        Assert.Equal("MCMXCIV", Valuta("=ROMAN(1994)").AsText());

    [Fact]
    public void LaDataDelSistemaStandardParteDal1899()
    {
        Assert.Equal(2, Calcola("=DATE(1900;1;1)"), 6);
        Assert.Equal(45292, Calcola("=DATE(2024;1;1)"), 6);
    }

    [Fact]
    public void MesiFuoriIntervalloTraboccano()
    {
        Assert.Equal(Calcola("=DATE(2025;1;1)"), Calcola("=DATE(2024;13;1)"), 6);
    }

    [Theory]
    [InlineData("=YEAR(DATE(2024;3;15))", 2024)]
    [InlineData("=MONTH(DATE(2024;3;15))", 3)]
    [InlineData("=DAY(DATE(2024;3;15))", 15)]
    [InlineData("=WEEKDAY(DATE(2024;3;15))", 6)]
    [InlineData("=WEEKDAY(DATE(2024;3;15);2)", 5)]
    [InlineData("=ISOWEEKNUM(DATE(2024;1;1))", 1)]
    [InlineData("=DAYS(DATE(2024;3;15);DATE(2024;3;1))", 14)]
    [InlineData("=EOMONTH(DATE(2024;2;10);0)", 45351)]
    public void ComponentiDelleDate(string formula, double atteso) =>
        Assert.Equal(atteso, Calcola(formula), 6);

    [Fact]
    public void PasquaSegueIlCalendarioGregoriano()
    {
        var pasqua = Calcola("=EASTERSUNDAY(2024)");
        Assert.Equal(Calcola("=DATE(2024;3;31)"), pasqua, 6);
    }

    [Fact]
    public void GiorniLavorativiSaltanoIlFineSettimana()
    {
        Assert.Equal(5, Calcola("=NETWORKDAYS(DATE(2024;4;1);DATE(2024;4;5))"), 6);

        Assert.Equal(7, Calcola("=NETWORKDAYS(DATE(2024;4;1);DATE(2024;4;9))"), 6);
    }

    [Theory]
    [InlineData("=LEFT(\"PulseOffice\";5)", "Pulse")]
    [InlineData("=RIGHT(\"PulseOffice\";6)", "Office")]
    [InlineData("=MID(\"PulseOffice\";6;6)", "Office")]
    [InlineData("=TRIM(\"  molti   spazi  \")", "molti spazi")]
    [InlineData("=PROPER(\"mARIO rOSSI\")", "Mario Rossi")]
    [InlineData("=SUBSTITUTE(\"a-b-c\";\"-\";\"+\";2)", "a-b+c")]
    [InlineData("=REPLACE(\"abcdef\";2;3;\"X\")", "aXef")]
    [InlineData("=TEXTJOIN(\"-\";1;\"a\";\"b\";\"c\")", "a-b-c")]
    [InlineData("=ROT13(\"Ciao\")", "Pvnb")]
    public void FunzioniDiTesto(string formula, string atteso) =>
        Assert.Equal(atteso, Valuta(formula).AsText());

    [Fact]
    public void GliIndiciDelTestoPartonoDaUno()
    {
        Assert.Equal(1, Calcola("=FIND(\"P\";\"PulseOffice\")"));

        Assert.Equal(CellError.Value, Valuta("=FIND(\"p\";\"PulseOffice\")").Error);
        Assert.Equal(1, Calcola("=SEARCH(\"p\";\"PulseOffice\")"));
    }

    [Fact]
    public void RipetiHaUnTettoDiSicurezza() =>
        Assert.Equal(CellError.Value, Valuta("=REPT(\"xx\";100000)").Error);

    [Fact]
    public void CercaVertTrovaLaCorrispondenzaEsatta()
    {
        var (wb, engine) = Nuovo();
        var sheet = wb.Sheets[0];
        string[] nomi = ["alfa", "beta", "gamma"];
        for (var i = 0; i < nomi.Length; i++)
        {
            sheet.SetValue(new CellAddress(i, 0), CellValue.Text(nomi[i]));
            sheet.SetValue(new CellAddress(i, 1), CellValue.Number((i + 1) * 10));
        }
        engine.RecalculateAll();

        engine.SetFormula(C("D1"), "=VLOOKUP(\"beta\";A1:B3;2;0)");
        Assert.Equal(20, engine.GetValue(C("D1")).AsNumber());

        engine.SetFormula(C("D2"), "=VLOOKUP(\"delta\";A1:B3;2;0)");
        Assert.Equal(CellError.NotAvailable, engine.GetValue(C("D2")).Error);
    }

    [Fact]
    public void ConfrontaRestituisceLaPosizioneNonIlValore()
    {
        var (wb, engine) = Nuovo();
        for (var i = 0; i < 5; i++)
            wb.Sheets[0].SetValue(new CellAddress(i, 0), CellValue.Number((i + 1) * 10));
        engine.RecalculateAll();

        engine.SetFormula(C("C1"), "=MATCH(30;A1:A5;0)");
        Assert.Equal(3, engine.GetValue(C("C1")).AsNumber());

        engine.SetFormula(C("C2"), "=INDEX(A1:A5;3)");
        Assert.Equal(30, engine.GetValue(C("C2")).AsNumber());
    }

    [Fact]
    public void SommaSeApplicaGliOperatoriNelCriterio()
    {
        var (wb, engine) = Nuovo();
        for (var i = 0; i < 5; i++)
            wb.Sheets[0].SetValue(new CellAddress(i, 0), CellValue.Number((i + 1) * 10));
        engine.RecalculateAll();

        engine.SetFormula(C("C1"), "=SUMIF(A1:A5;\">25\")");
        Assert.Equal(120, engine.GetValue(C("C1")).AsNumber());

        engine.SetFormula(C("C2"), "=COUNTIF(A1:A5;\"<=20\")");
        Assert.Equal(2, engine.GetValue(C("C2")).AsNumber());
    }

    [Fact]
    public void IlCriterioTestualeAccettaICaratteriJolly()
    {
        var (wb, engine) = Nuovo();
        string[] frutti = ["mela", "melone", "pera"];
        for (var i = 0; i < frutti.Length; i++)
            wb.Sheets[0].SetValue(new CellAddress(i, 0), CellValue.Text(frutti[i]));
        engine.RecalculateAll();

        engine.SetFormula(C("C1"), "=COUNTIF(A1:A3;\"mel*\")");
        Assert.Equal(2, engine.GetValue(C("C1")).AsNumber());

        engine.SetFormula(C("C2"), "=COUNTIF(A1:A3;\"<>pera\")");
        Assert.Equal(2, engine.GetValue(C("C2")).AsNumber());
    }

    [Fact]
    public void LaRataDiUnMutuoENegativa()
    {
        var rata = Calcola("=PMT(0.03/12;240;100000)");
        Assert.True(rata < 0, $"attesa rata negativa, ottenuta {rata}");
        Assert.Equal(-554.60, rata, 1);
    }

    [Theory]
    [InlineData("=FV(0.05;10;0;-1000)", 1628.894627)]
    [InlineData("=PV(0.05;10;0;-1628.894627)", 1000)]
    [InlineData("=NPER(0.05;0;-1000;1628.894627)", 10)]
    [InlineData("=SLN(10000;1000;5)", 1800)]
    [InlineData("=SYD(10000;1000;5;1)", 3000)]
    [InlineData("=EFFECT(0.12;12)", 0.126825)]
    public void MatematicaFinanziaria(string formula, double atteso) =>
        Assert.Equal(atteso, Calcola(formula), 4);

    [Fact]
    public void IlTassoSiRitrovaDallaRata()
    {
        var rata = Calcola("=PMT(0.004;120;50000)");
        var (_, engine) = Nuovo();
        engine.SetFormula(C("A1"), $"=RATE(120;{rata.ToString(System.Globalization.CultureInfo.InvariantCulture)};50000)");
        Assert.Equal(0.004, engine.GetValue(C("A1")).AsNumber(), 6);
    }

    [Fact]
    public void IlTassoInternoDiRendimentoRichiedeUnCambioDiSegno()
    {
        var (wb, engine) = Nuovo();
        double[] flussi = [-1000, 300, 400, 500];
        for (var i = 0; i < flussi.Length; i++)
            wb.Sheets[0].SetValue(new CellAddress(i, 0), CellValue.Number(flussi[i]));
        engine.RecalculateAll();

        engine.SetFormula(C("C1"), "=IRR(A1:A4)");
        var tir = engine.GetValue(C("C1"));
        Assert.False(tir.IsError);
        Assert.InRange(tir.AsNumber(), 0.08, 0.10);

        for (var i = 0; i < flussi.Length; i++)
            engine.SetValue(new SheetCell(0, new CellAddress(i, 0)), CellValue.Number(100));
        Assert.True(engine.GetValue(C("C1")).IsError);
    }

    [Theory]
    [InlineData("=NORMSDIST(0)", 0.5)]
    [InlineData("=NORMSDIST(1.96)", 0.975)]
    [InlineData("=NORMSINV(0.975)", 1.96)]
    [InlineData("=CHIINV(0.05;1)", 3.84146)]
    [InlineData("=TINV(0.05;10)", 2.22814)]
    [InlineData("=BINOMDIST(2;5;0.5;0)", 0.3125)]
    [InlineData("=POISSON(2;3;0)", 0.2240)]
    public void Distribuzioni(string formula, double atteso) =>
        Assert.Equal(atteso, Calcola(formula), 3);

    [Fact]
    public void LInversaEfettivamenteInverteLaDistribuzione()
    {
        Assert.Equal(0.3, Calcola("=NORMSDIST(NORMSINV(0.3))"), 6);
        Assert.Equal(7.5, Calcola("=CHISQ.INV(CHISQ.DIST(7.5;3;1);3)"), 4);
    }

    [Fact]
    public void LaRegressioneRitrovaUnaRettaEsatta()
    {
        var (wb, engine) = Nuovo();

        for (var i = 0; i < 5; i++)
        {
            wb.Sheets[0].SetValue(new CellAddress(i, 0), CellValue.Number(3 * (i + 1) + 2));
            wb.Sheets[0].SetValue(new CellAddress(i, 1), CellValue.Number(i + 1));
        }
        engine.RecalculateAll();

        engine.SetFormula(C("D1"), "=SLOPE(A1:A5;B1:B5)");
        engine.SetFormula(C("D2"), "=INTERCEPT(A1:A5;B1:B5)");
        engine.SetFormula(C("D3"), "=RSQ(A1:A5;B1:B5)");

        Assert.Equal(3, engine.GetValue(C("D1")).AsNumber(), 9);
        Assert.Equal(2, engine.GetValue(C("D2")).AsNumber(), 9);
        Assert.Equal(1, engine.GetValue(C("D3")).AsNumber(), 9);
    }

    [Theory]
    [InlineData("=IMSUM(\"3+4i\";\"1-2i\")", "4+2i")]
    [InlineData("=IMPRODUCT(\"2i\";\"2i\")", "-4")]
    [InlineData("=IMCONJUGATE(\"3+4i\")", "3-4i")]
    [InlineData("=COMPLEX(0;1)", "i")]
    [InlineData("=DEC2BIN(9)", "1001")]
    [InlineData("=BIN2HEX(\"11111111\")", "FF")]
    [InlineData("=DEC2BIN(-1)", "1111111111")]
    public void ComplessiEConversioni(string formula, string atteso) =>
        Assert.Equal(atteso, Valuta(formula).AsText());

    [Fact]
    public void IlModuloDiUnComplessoEIlTeoremaDiPitagora() =>
        Assert.Equal(5, Calcola("=IMABS(\"3+4i\")"), 9);

    [Fact]
    public void RifRigaSenzaArgomentoIndicaLaCellaCorrente()
    {
        var (_, engine) = Nuovo();
        engine.SetFormula(C("C7"), "=ROW()");
        Assert.Equal(7, engine.GetValue(C("C7")).AsNumber());

        engine.SetFormula(C("C8"), "=COLUMN()");
        Assert.Equal(3, engine.GetValue(C("C8")).AsNumber());
    }

    [Fact]
    public void RifRigaConArgomentoIndicaIlRiferimento()
    {
        var (_, engine) = Nuovo();
        engine.SetFormula(C("A1"), "=ROW(B5)");
        Assert.Equal(5, engine.GetValue(C("A1")).AsNumber());
    }

    [Fact]
    public void ValFormulaDistingueFormuleEValori()
    {
        var (_, engine) = Nuovo();
        engine.SetValue(C("A1"), CellValue.Number(5));
        engine.SetFormula(C("A2"), "=A1*2");

        engine.SetFormula(C("B1"), "=ISFORMULA(A1)");
        engine.SetFormula(C("B2"), "=ISFORMULA(A2)");

        Assert.False(engine.GetValue(C("B1")).AsBoolean());
        Assert.True(engine.GetValue(C("B2")).AsBoolean());
    }

    [Fact]
    public void LeFunzioniDiDatabaseFiltranoPerCriterio()
    {
        var (wb, engine) = Nuovo();
        var sheet = wb.Sheets[0];

        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("Prodotto"));
        sheet.SetValue(new CellAddress(0, 1), CellValue.Text("Importo"));
        string[] prodotti = ["mela", "pera", "mela"];
        double[] importi = [10, 20, 30];
        for (var i = 0; i < 3; i++)
        {
            sheet.SetValue(new CellAddress(i + 1, 0), CellValue.Text(prodotti[i]));
            sheet.SetValue(new CellAddress(i + 1, 1), CellValue.Number(importi[i]));
        }

        sheet.SetValue(new CellAddress(0, 3), CellValue.Text("Prodotto"));
        sheet.SetValue(new CellAddress(1, 3), CellValue.Text("mela"));
        engine.RecalculateAll();

        engine.SetFormula(C("F1"), "=DSUM(A1:B4;\"Importo\";D1:D2)");
        Assert.Equal(40, engine.GetValue(C("F1")).AsNumber());

        engine.SetFormula(C("F2"), "=DCOUNT(A1:B4;\"Importo\";D1:D2)");
        Assert.Equal(2, engine.GetValue(C("F2")).AsNumber());
    }
}
