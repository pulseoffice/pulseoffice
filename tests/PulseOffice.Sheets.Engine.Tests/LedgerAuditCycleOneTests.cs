using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class LedgerAuditCycleOneTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static SheetCell C(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return new SheetCell(0, address);
    }

    private (Workbook Workbook, CalculationEngine Engine) ConDati()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        for (var row = 1; row <= 10; row++)
        {
            engine.SetValue(C($"A{row}"), CellValue.Number(row));
            engine.SetValue(C($"B{row}"), CellValue.Number(row * 10));
        }
        string[] regions = ["N", "S", "N", "E", "S", "N", "W", "E", "N", "S"];
        for (var row = 1; row <= 10; row++) engine.SetValue(C($"F{row}"), CellValue.Text(regions[row - 1]));
        engine.SetValue(C("C1"), CellValue.Text("apple"));
        engine.SetValue(C("C2"), CellValue.Text("cherry"));
        engine.SetValue(C("D1"), CellValue.Number(1));
        engine.SetValue(C("D2"), CellValue.Text("2"));
        engine.SetValue(C("D3"), CellValue.Number(2));
        return (workbook, engine);
    }

    private CellValue Valuta(string formula)
    {
        var (_, engine) = ConDati();
        engine.SetFormula(C("H1"), formula);
        return engine.GetValue(C("H1"));
    }

    private double Numero(string formula)
    {
        var value = Valuta(formula);
        Assert.True(value.Kind == ValueKind.Number, $"{formula} ha prodotto {value}");
        return value.AsNumber();
    }

    [Theory]
    [InlineData("=IF(TRUE();1;1/0)", 1)]
    [InlineData("=IF(A1>0;5;NA())", 5)]
    [InlineData("=CHOOSE(1;7;1/0)", 7)]
    [InlineData("=SWITCH(1;1;3;2;1/0)", 3)]
    [InlineData("=IFS(TRUE();4;TRUE();1/0)", 4)]
    [InlineData("=IF(1)*7", 7)]
    [InlineData("=IF(ISREF(g100000000);1;2)", 2)]
    public void IlRamoNonSceltoNonPropagaIlSuoErrore(string formula, double atteso) =>
        Assert.Equal(atteso, Numero(formula));

    [Theory]
    [InlineData("=SUM(OFFSET(A1;0;0;3;2))", 66)]
    [InlineData("=ROWS(OFFSET(A1;0;0;3;2))", 3)]
    [InlineData("=SUM(OFFSET(A1;1;1))", 20)]
    [InlineData("=SUM(INDIRECT(\"A1:A\"&A5))", 15)]
    [InlineData("=INDIRECT(\"R3C1\";FALSE())", 3)]
    [InlineData("=SUM(INDIRECT(\"A:A\"))", 55)]
    [InlineData("=SUM(INDIRECT(\"Sheet1!A1:A3\"))", 6)]
    [InlineData("=SUM(INDIRECT(\"$Sheet1.$A$1:$Sheet1.$A$3\"))", 6)]
    [InlineData("=SUM(CHOOSE(2;A1:A3;B1:B3))", 60)]
    [InlineData("=SUM(IF(TRUE();A1:A3))", 6)]
    public void OffsetIndirectESceltaRestituisconoRiferimentiInteri(string formula, double atteso) =>
        Assert.Equal(atteso, Numero(formula));

    [Theory]
    [InlineData("=SUMPRODUCT(--(F1:F10=\"N\"))", 4)]
    [InlineData("=SUMPRODUCT(-(F1:F10=\"N\"))", -4)]
    [InlineData("=MAX(IF(F1:F10=\"N\";A1:A10))", 9)]
    [InlineData("=SUM(IF(F1:F10=\"S\";B1:B10;0))", 170)]
    public void GliOperatoriUnariEIfLavoranoElementoPerElemento(string formula, double atteso) =>
        Assert.Equal(atteso, Numero(formula));

    [Fact]
    public void IndirectSiRicalcolaDopoLaCellaACuiPunta()
    {
        var (_, engine) = fixture.CreateWorkbook();
        engine.SetValue(C("C1"), CellValue.Number(5));
        engine.SetFormula(C("A1"), "=INDIRECT(\"B1\")");
        engine.SetFormula(C("B1"), "=C1*2");
        engine.SetValue(C("C1"), CellValue.Number(7));
        Assert.Equal(14, engine.GetValue(C("A1")).AsNumber());
        engine.RecalculateAll();
        Assert.Equal(14, engine.GetValue(C("A1")).AsNumber());
    }

    [Theory]
    [InlineData("=ROUND(1.005;2)", 1.01)]
    [InlineData("=ROUND(2.675;2)", 2.68)]
    [InlineData("=ROUND(-1.005;2)", -1.01)]
    [InlineData("=COUNTIF(D1:D3;\"2\")", 2)]
    [InlineData("=COUNTIF(D1:D3;2)", 1)]
    [InlineData("=VLOOKUP(\"ch*\";C1:D2;1;0)", double.NaN)]
    [InlineData("=DATEDIF(DATE(2020;1;31);DATE(2024;3;1);\"md\")", -1)]
    [InlineData("=DATEDIF(DATE(2011;1;29);DATE(2011;3;1);\"md\")", 0)]
    [InlineData("=DATEDIF(DATE(2020;2;29);DATE(2021;3;1);\"yd\")", 0)]
    [InlineData("=\"2024-01-01\"+1", 45293)]
    [InlineData("=\"10:00\"*24", 10)]
    [InlineData("=-\"1\"", -1)]
    [InlineData("=IF(0.1+0.2=0.3;1;0)", 1)]
    [InlineData("=IF(SQRT(18)=4.24264068711929;1;0)", 1)]
    [InlineData("=COUNTIF(A1:A3;0.1+0.2+0.7)", 1)]
    [InlineData("=NORM.S.DIST(1;0)", 0.24197072451914337)]
    [InlineData("=NORM.S.DIST(1;1)", 0.8413447460685429)]
    [InlineData("=NORMDIST(1;0;1)", 0.8413447460685429)]
    [InlineData("=F.DIST(2;5;10)", 0.8358050491002613)]
    [InlineData("=LOGINV(0.5)", 1)]
    [InlineData("=RAWSUBTRACT(10;1;2;3)", 4)]
    [InlineData("=DCOUNT(A1:B10;;A1:A2)", 1)]
    [InlineData("=SUMPRODUCT(LARGE(B1:B10;{1;2;3}))", 270)]
    [InlineData("=SUM(COUNTIF(F1:F10;{\"N\";\"S\"}))", 7)]
    [InlineData("=SUM(SUMIF(F1:F10;{\"N\";\"E\"};A1:A10))", 31)]
    [InlineData("=MAX(LEN(C1:C2))", 6)]
    [InlineData("=SUMPRODUCT(ABS(-A1:A3))", 6)]
    [InlineData("=INDEX(LINEST(B1:B10;A1:A10;1;1);1;1)", 10)]
    [InlineData("=INDEX(LINEST(B1:B10;A1:A10;1;1);3;1)", 1)]
    [InlineData("=INDEX(LINEST(B1:B10;A1:A10;1;1);4;2)", 8)]
    [InlineData("=INDEX(LINEST(B1:B10;A1:A10;0;1);4;2)", 9)]
    [InlineData("=INDEX(LINEST(A1:A10;B1:B10);1;2)", 0)]
    [InlineData("=INDEX(FREQUENCY(A1:A10;{7;3});1)", 4)]
    [InlineData("=INDEX(FREQUENCY(A1:A10;{7;3});2)", 3)]
    [InlineData("=INDEX(FREQUENCY(A1:A10;{7;3});3)", 3)]
    [InlineData("=SUM(ROW(A3:A5))", 12)]
    [InlineData("=INDEX(FOURIER(A1:A4;1);1;1)", 10)]
    [InlineData("=INDEX(FOURIER(A1:A4;1);2;2)", 2)]
    [InlineData("=INDEX(FOURIER(A1:A4;1;0;1);3;1)", 2)]
    [InlineData("=INDEX(TREND(B1:B10;A1:A10;{11;12});2)", 120)]
    [InlineData("=NETWORKDAYS(DATE(2014;11;1);DATE(2014;11;30);{\"2014-11-11\";\"2014-11-28\";\"2014-11-27\"})", 17)]
    [InlineData("=DATE(100;1;1)", 36526)]
    [InlineData("=WEEKNUM(DATE(2024;1;1);150)", 1)]
    [InlineData("=LEN(\"ab😂d\")", 4)]
    [InlineData("=FIND(\"d\";\"ab😂d\")", 4)]
    [InlineData("=LENB(\"🙁🙂A\")", 5)]
    [InlineData("=NUMBERVALUE(\"3.5%\";\".\")", 0.035)]
    [InlineData("=VALUE(Z99)", 0)]
    [InlineData("=T.DIST.RT(-1;9)", 0.828281801931043)]
    [InlineData("=CHISQ.DIST.RT(13.27;5.9)", 0.0209757694030221)]
    [InlineData("=CHISQDIST(3;2;0)", 0.111565080074215)]
    [InlineData("=CHISQINV(0.93;1)", 3.28302028675954)]
    [InlineData("=BETADIST(1;5;3;-2;4;FALSE())", 0.2734375)]
    [InlineData("=BINOM.INV(123;0.5;1)", 123)]
    [InlineData("=COUNTA(5;;10)", 3)]
    [InlineData("=STDEV(1E+30;1E+30;1E+30)", 0)]
    [InlineData("=QUARTILE.INC(A1:A10;2.8)", 5.5)]
    [InlineData("=MAX(1;2;+A1:A10)", 10)]
    [InlineData("=Z99", 0)]
    [InlineData("=MATCH(842020700000002;{842020700000001;842020700000002;842020700000003};0)", 2)]
    [InlineData("=ERRORTYPE(NA())", 32767)]
    [InlineData("=HYPERLINK(\"http://a.b\";5)*2", 10)]
    [InlineData("=COLOR(0;0;255;255)", 4278190335)]
    [InlineData("=SUM(SEQUENCE(;2;4;3))", 11)]
    [InlineData("=CEILING.PRECISE(24;0)", 0)]
    [InlineData("=-0.3+0.2+0.1", 0)]
    [InlineData("=INT(0.9999999999999999)", 1)]
    [InlineData("=MULTINOMIAL(1000;0)", 1)]
    [InlineData("=EUROCONVERT(100;\"ATS\";\"EUR\")", 7.27)]
    [InlineData("=EUROCONVERT(100;\"ATS\";\"EUR\";1)", 7.267283416785971)]
    [InlineData("=SLOPE({1;2;\"x\";4};{1;2;3;4})", 1)]
    [InlineData("=MAXIFS(A1:A10;A1:A10;\">0\";B1:B10;Z99)", 0)]
    public void RisultatiAllineatiAlRiferimento(string formula, double atteso)
    {
        if (double.IsNaN(atteso))
        {
            Assert.Equal("cherry", Valuta(formula).AsText());
            return;
        }
        Assert.Equal(atteso, Numero(formula), 9);
    }

    [Theory]
    [InlineData("=ARRAYTOTEXT(C1:D2)", "apple, 1, cherry, 2")]
    [InlineData("=ARRAYTOTEXT(C1:D2;1)", "{\"apple\",1;\"cherry\",\"2\"}")]
    [InlineData("=TEXT(1/3;\"General\")", "0.333333333333333")]
    [InlineData("=TEXT(0.5;\"# ?/?\")", " 1/2")]
    [InlineData("=LEFT(\"ab😂d\";4)", "ab😂d")]
    [InlineData("=MID(\"ab😂d\";2;2)", "b😂")]
    [InlineData("=RIGHT(\"ab😂\";2)", "b😂")]
    [InlineData("=REPLACE(\"B😂c😂d\";3;1;\"xXx\")", "B😂xXx😂d")]
    [InlineData("=BAHTTEXT(1265)", "หนึ่งพันสองร้อยหกสิบห้าบาทถ้วน")]
    [InlineData("=BAHTTEXT(21.25)", "ยี่สิบเอ็ดบาทยี่สิบห้าสตางค์")]
    [InlineData("=JIS(\"ｶﾞｷﾞｯ\")&JIS(\"`\")", "ガギッ‘")]
    [InlineData("=ASC(\"ガギッ‘\")", "ｶﾞｷﾞｯ`")]
    [InlineData("=TEXTJOIN({\"-\";\"+\"};1;\"a\";\"b\";\"c\")", "a-b+c")]
    [InlineData("=TEXTAFTER(\"Brown, Lucas, Manager:1234:5678\";{\",\";\":\"};4)", "5678")]
    [InlineData("=INDEX(TEXTSPLIT(\"Apples,Lemons\";;\",\");2;1)", "Lemons")]
    [InlineData("=XLOOKUP(\"INDIA\";{\"India\";\"Italy\"};{\"+91\";\"+39\"})", "+91")]
    public void TestiAllineatiAExcel(string formula, string atteso) =>
        Assert.Equal(atteso, Valuta(formula).AsText());

    [Theory]
    [InlineData("=AMORDEGRC(2400;DATE(2008;8;19);DATE(2008;12;31);300;1;0.15;1)", 776)]
    [InlineData("=AMORLINC(2400;DATE(2008;8;19);DATE(2008;12;31);300;1;0.15;1)", 360)]
    [InlineData("=ODDLPRICE(DATE(2008;2;7);DATE(2008;6;15);DATE(2007;10;15);0.0375;0.0405;100;2)", 99.8782860147213)]
    [InlineData("=ODDLYIELD(DATE(2008;4;20);DATE(2008;6;15);DATE(2007;12;24);0.0375;99.875;100;2)", 0.045192235629169)]
    [InlineData("=ODDFPRICE(DATE(2008;11;11);DATE(2021;3;1);DATE(2008;10;15);DATE(2009;3;1);0.0785;0.0625;100;2;1)", 113.597717474079)]
    [InlineData("=ODDFYIELD(DATE(2008;11;11);DATE(2021;3;1);DATE(2008;10;15);DATE(2009;3;1);0.0575;84.5;100;2;0)", 0.0772455415979)]
    [InlineData("=VDB(2400;300;10;0;0.875;1.5)", 315)]
    [InlineData("=ACCRINT(DATE(2008;3;3);DATE(2009;3;3);DATE(2009;1;1);4.33/100;10000000;1;1)", 359650.273224044)]
    [InlineData("=ACCRINTM(DATE(2012;1;1);DATE(2013;2;15);0.065;;3)", 73.1917808219178)]
    [InlineData("=INTRATE(DATE(2008;9;2);DATE(2009;3;2);1000;1080;1)", 0.161767955801105)]
    [InlineData("=PRICEDISC(DATE(1999;2;15);DATE(1999;3;1);0.0525;100;0)", 99.7958333333333)]
    [InlineData("=DOLLARDE(1.1;6.1)", 1.16666666666667)]
    [InlineData("=PMT(1E-16;10;1000;0;0)", -100)]
    public void FunzioniFinanziarieSugliEsempiDiRiferimento(string formula, double atteso) =>
        Assert.Equal(atteso, Numero(formula), 6);

    [Theory]
    [InlineData("=IRR({-1;-2;-3})")]
    [InlineData("=VDB(2400;300;10;6;18;1.5)")]
    [InlineData("=VLOOKUP(1;A1:B10;3;0)")]
    public void GliErroriSonoQuelliDiExcel(string formula)
    {
        var value = Valuta(formula);
        Assert.True(value.IsError);
        Assert.Equal(formula.Contains("VLOOKUP") ? CellError.Reference : CellError.Number, value.Error);
    }
}

public class LedgerIterationTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static SheetCell C(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return new SheetCell(0, address);
    }

    [Fact]
    public void SenzaIterazioniUnRiferimentoCircolareDaErrore()
    {
        var (_, engine) = fixture.CreateWorkbook();
        engine.SetFormula(C("A1"), "=A2*0.5+10");
        engine.SetFormula(C("A2"), "=A1*0.5+1");
        Assert.True(engine.GetValue(C("A1")).IsError);
    }

    [Fact]
    public void ConLeIterazioniIRiferimentiCircolariConvergono()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        workbook.Calculation = new CalculationSettings { Iterative = true, MaxIterations = 1000, MaxChange = 1e-9 };
        engine.SetValue(C("B1"), CellValue.Number(5));
        engine.SetFormula(C("A1"), "=A2*0.5+10");
        engine.SetFormula(C("A2"), "=A1*0.5+1");
        engine.SetFormula(C("A3"), "=(A3+B1/MAX(A3;1))/2");
        engine.SetFormula(C("A4"), "=A1+A2");
        engine.RecalculateAll();

        Assert.Equal(14, engine.GetValue(C("A1")).AsNumber(), 6);
        Assert.Equal(8, engine.GetValue(C("A2")).AsNumber(), 6);
        Assert.Equal(Math.Sqrt(5), engine.GetValue(C("A3")).AsNumber(), 6);
        Assert.Equal(22, engine.GetValue(C("A4")).AsNumber(), 6);
        Assert.Empty(engine.CircularCells);
    }
}

public class LedgerWholeRangeTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static SheetCell C(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return new SheetCell(0, address);
    }

    private (Workbook Workbook, CalculationEngine Engine) ConDati()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        for (var row = 1; row <= 10; row++)
        {
            engine.SetValue(C($"A{row}"), CellValue.Number(row));
            engine.SetValue(C($"B{row}"), CellValue.Number(row * 10));
        }
        return (workbook, engine);
    }

    [Theory]
    [InlineData("=SUM(A:A)", 55)]
    [InlineData("=SUM($A:$B)", 605)]
    [InlineData("=SUM(2:3)", 55)]
    [InlineData("=SUM($2:$2)", 22)]
    [InlineData("=VLOOKUP(7;A:B;2;0)", 70)]
    [InlineData("=MATCH(5;A:A;0)", 5)]
    [InlineData("=COUNTA(A:A)", 10)]
    [InlineData("=COUNTBLANK(A:A)", 1048566)]
    [InlineData("=ROWS(A:A)", 1048576)]
    [InlineData("=COUNTIF(A:A;\"\")", 1048566)]
    [InlineData("=SUMIF(A:A;\">5\";B:B)", 400)]
    [InlineData("=SUM(Sheet1!A:A)", 55)]
    [InlineData("=SUM(Sheet1.A:A)", 55)]
    public void ColonneERigheIntereSiCalcolano(string formula, double atteso)
    {
        var (_, engine) = ConDati();
        engine.SetFormula(C("D1"), formula);
        var value = engine.GetValue(C("D1"));
        Assert.True(value.Kind == ValueKind.Number, $"{formula} ha prodotto {value}");
        Assert.Equal(atteso, value.AsNumber());
    }

    [Fact]
    public void ColonneIntereSopravvivonoAInserimentiEdEliminazioniDiRighe()
    {
        var (workbook, engine) = ConDati();
        engine.SetFormula(C("D1"), "=SUM(A:A)+SUM($2:$2)");
        var structure = new PulseOffice.Sheets.Engine.Operations.SheetStructure();
        structure.InsertRows(engine, 0, 4, 2);
        structure.DeleteRows(engine, 0, 7, 1);

        Assert.Equal("=SUM(A:A)+SUM($2:$2)", workbook.Sheets[0][new CellAddress(0, 3)].Formula);
    }

    [Fact]
    public void EliminareRigheSottoUnIntervalloNonLoAllarga()
    {
        var (workbook, engine) = ConDati();
        engine.SetFormula(C("D1"), "=SUM(A1:A3)");
        new PulseOffice.Sheets.Engine.Operations.SheetStructure().DeleteRows(engine, 0, 7, 1);

        Assert.Equal("=SUM(A1:A3)", workbook.Sheets[0][new CellAddress(0, 3)].Formula);
        Assert.Equal(6, engine.GetValue(C("D1")).AsNumber());
    }
}

public class LedgerSpillPersistenceTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    [Fact]
    public void UnaMatriceDinamicaRilettaDalFileNonSiBloccaSuiSuoiStessiValori()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet[new CellAddress(0, 0)] = new Cell { Formula = "=SEQUENCE(3)", Value = CellValue.Number(1), SpillRange = new CellRange(new CellAddress(0, 0), new CellAddress(3, 0)) };
        sheet[new CellAddress(1, 0)] = new Cell { Value = CellValue.Number(2), IsSpilled = true };
        sheet[new CellAddress(2, 0)] = new Cell { Value = CellValue.Number(3), IsSpilled = true };
        sheet[new CellAddress(3, 0)] = new Cell { Value = CellValue.Number(4), IsSpilled = true };

        var engine = fixture.CreateEngine(workbook);
        engine.RecalculateAll();

        Assert.Equal(1, engine.GetValue(new SheetCell(0, new CellAddress(0, 0))).AsNumber());
        Assert.Equal(3, sheet[new CellAddress(2, 0)].Value.AsNumber());
        Assert.True(sheet[new CellAddress(3, 0)].Value.IsEmpty);
        Assert.False(sheet[new CellAddress(3, 0)].IsSpilled);
        Assert.Equal(new CellRange(new CellAddress(0, 0), new CellAddress(2, 0)), sheet[new CellAddress(0, 0)].SpillRange);
    }

    [Fact]
    public void UnValoreScrittoDallUtenteSuUnaCellaVersataBloccaLaMatrice()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        engine.SetFormula(new SheetCell(0, new CellAddress(0, 0)), "=SEQUENCE(3)");
        engine.SetValue(new SheetCell(0, new CellAddress(1, 0)), CellValue.Number(9));

        Assert.Equal(CellError.Spill, engine.GetValue(new SheetCell(0, new CellAddress(0, 0))).Error);
        Assert.False(workbook.Sheets[0][new CellAddress(1, 0)].IsSpilled);
    }
}

public class LedgerImplicitIntersectionTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private CellValue Legacy(string formula, string where)
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        for (var row = 0; row < 10; row++)
        {
            sheet.SetValue(new CellAddress(row, 0), CellValue.Number(row + 1));
            sheet.SetValue(new CellAddress(row, 1), CellValue.Number((row + 1) * 10));
        }
        Assert.True(CellAddress.TryParseA1(where, out var address));
        sheet[address] = new Cell { Formula = formula, ImplicitIntersection = true };
        var engine = fixture.CreateEngine(workbook);
        engine.RecalculateAll();
        return engine.GetValue(new SheetCell(0, address));
    }

    [Theory]
    [InlineData("=A$1:A$10*2", "D5", 10)]
    [InlineData("=A:A*2", "D7", 14)]
    [InlineData("=IF(A:A>5;1;0)", "D7", 1)]
    [InlineData("=IF(A:A>5;1;0)", "D3", 0)]
    [InlineData("=A1:A10", "D4", 4)]
    [InlineData("=ABS(-A1:A10)", "D6", 6)]
    [InlineData("=SUMPRODUCT((A1:A10>5)*1)", "D2", 5)]
    [InlineData("=SUMPRODUCT(--(A1:A10>5);B1:B10)", "D2", 400)]
    [InlineData("=VLOOKUP(A:A;A1:B10;2;0)", "D8", 80)]
    [InlineData("=MATCH(7;A1:A10;0)", "D1", 7)]
    [InlineData("=SUM(A1:A10)", "D1", 55)]
    [InlineData("=SUM(COUNTIF(A1:A10;{1;2;30}))", "D1", 2)]
    [InlineData("=SUM(SUMIF(A1:A10;{1;2};B1:B10))", "D1", 30)]
    public void LeFormuleDeiFileAltruiUsanoLIntersezioneImplicita(string formula, string where, double atteso)
    {
        var value = Legacy(formula, where);
        Assert.True(value.Kind == ValueKind.Number, $"{formula} in {where} ha prodotto {value}");
        Assert.Equal(atteso, value.AsNumber());
    }

    [Fact]
    public void FuoriDallIntervalloLIntersezioneDaValue() =>
        Assert.Equal(CellError.Value, Legacy("=A1:A10*2", "D15").Error);

    [Fact]
    public void UnaFormulaDinamicaSegnalaLUsoDiMatriciPerIlSalvataggio()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        engine.SetValue(new SheetCell(0, new CellAddress(0, 0)), CellValue.Number(2));
        engine.SetFormula(new SheetCell(0, new CellAddress(0, 3)), "=SUM(A1:A3*2)");
        engine.SetFormula(new SheetCell(0, new CellAddress(1, 3)), "=SUM(A1:A3)*2");

        Assert.True(workbook.Sheets[0][new CellAddress(0, 3)].UsesArrays);
        Assert.False(workbook.Sheets[0][new CellAddress(1, 3)].UsesArrays);
    }
}

public class LedgerComparisonRulesTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private CellValue Valuta(CalculationSettings settings, string formula)
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        workbook.Calculation = settings;
        engine.SetValue(new SheetCell(0, new CellAddress(0, 0)), CellValue.Text("Apple"));
        engine.SetValue(new SheetCell(0, new CellAddress(1, 0)), CellValue.Text("apple"));
        engine.SetValue(new SheetCell(0, new CellAddress(2, 0)), CellValue.Text("Eva"));
        engine.SetFormula(new SheetCell(0, new CellAddress(0, 3)), formula);
        return engine.GetValue(new SheetCell(0, new CellAddress(0, 3)));
    }

    [Theory]
    [InlineData("=\"A\"=\"a\"", true, 0)]
    [InlineData("=\"A\"=\"a\"", false, 1)]
    [InlineData("=A1=\"APPLE\"", true, 0)]
    [InlineData("=COUNTIF(A1:A3;\"apple\")", true, 2)]
    [InlineData("=COUNTIF(A1:A3;\"apple\")", false, 2)]
    [InlineData("=COUNTIF(A1:A3;\"APP*\")", true, 2)]
    [InlineData("=SUMIF(A1:A3;\"EVA\";A1:A3)", true, 0)]
    [InlineData("=MATCH(\"apple\";A1:A3;0)", true, 1)]
    [InlineData("=MATCH(\"apple\";A1:A3;0)", false, 1)]
    [InlineData("=MATCH(\"EVA\";A1:A3;0)", true, 3)]
    [InlineData("=VLOOKUP(\"EVA\";A1:A3;1;0)=\"Eva\"", true, 1)]
    [InlineData("=HLOOKUP(\"EVA\";A3:A3;1;0)=\"Eva\"", true, 1)]
    [InlineData("=XMATCH(\"EVA\";A1:A3)", true, 3)]
    public void LaDistinzioneDelleMaiuscoleValeSoloPerIConfrontiNonPerRicercheECriteri(string formula, bool caseSensitive, double atteso)
    {
        var value = Valuta(new CalculationSettings { CaseSensitive = caseSensitive }, formula);
        Assert.Equal(atteso, value.Kind == ValueKind.Boolean ? (value.AsBoolean() ? 1 : 0) : value.AsNumber());
    }

    [Theory]
    [InlineData("=COUNTIF(A1:A3;\"[E,a].*\")", CriteriaSyntax.RegularExpressions, 3)]
    [InlineData("=COUNTIF(A1:A3;\"a*\")", CriteriaSyntax.Wildcards, 2)]
    [InlineData("=COUNTIF(A1:A3;\"a*\")", CriteriaSyntax.Literal, 0)]
    [InlineData("=SEARCH(\"p+l\";A1)", CriteriaSyntax.RegularExpressions, 2)]
    [InlineData("=VLOOKUP(\"E.F\";{\"C\";1|\"EF\";3|\"E.F\";4};2;0)", CriteriaSyntax.RegularExpressions, 4)]
    [InlineData("=MATCH(\"E.F\";{\"C\";\"EF\";\"E.F\"};0)", CriteriaSyntax.RegularExpressions, 3)]
    public void ICriteriSeguonoLaSintassiDelDocumento(string formula, CriteriaSyntax syntax, double atteso) =>
        Assert.Equal(atteso, Valuta(new CalculationSettings { Criteria = syntax }, formula).AsNumber());

    [Fact]
    public void VlookupSuIntervalloConEspressioniRegolari()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        workbook.Calculation = new CalculationSettings { Criteria = CriteriaSyntax.RegularExpressions, CaseSensitive = true };
        string[] keys = ["C", "DD", "EF", "E.F", "E\\.F", "F", "G"];
        for (var row = 0; row < keys.Length; row++)
        {
            engine.SetValue(new SheetCell(0, new CellAddress(row + 1, 11)), CellValue.Text(keys[row]));
            engine.SetValue(new SheetCell(0, new CellAddress(row + 1, 12)), CellValue.Number(row + 1));
        }
        engine.SetValue(new SheetCell(0, new CellAddress(4, 15)), CellValue.Text("E.F"));
        engine.SetFormula(new SheetCell(0, new CellAddress(5, 0)), "=VLOOKUP(P5;$L$2:$M$8;2;0)");
        Assert.Equal(4, engine.GetValue(new SheetCell(0, new CellAddress(5, 0))).AsNumber());
        workbook.Sheets[0][new CellAddress(5, 0)] = workbook.Sheets[0][new CellAddress(5, 0)] with { ImplicitIntersection = true };
        engine.RecalculateAll();
        Assert.Equal(4, engine.GetValue(new SheetCell(0, new CellAddress(5, 0))).AsNumber());
    }

    [Fact]
    public void ICriteriPossonoCercareDentroLaCella() =>
        Assert.Equal(2, Valuta(new CalculationSettings { WholeCellCriteria = false }, "=COUNTIF(A1:A3;\"pp\")").AsNumber());
}

public class LedgerArrayMemberDependencyTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    [Fact]
    public void UnaCellaCheLeggeUnMembroDiUnaFormulaMatricialeSiAggiorna()
    {
        var (_, engine) = fixture.CreateWorkbook();
        for (var row = 0; row < 3; row++) engine.SetValue(new SheetCell(0, new CellAddress(row, 0)), CellValue.Number(row + 1));
        engine.SetArrayFormula(new SheetCell(0, new CellAddress(0, 1)), new CellRange(new CellAddress(0, 1), new CellAddress(2, 1)), "=A1:A3*2");
        engine.SetFormula(new SheetCell(0, new CellAddress(1, 2)), "=B2+1");
        engine.SetFormula(new SheetCell(0, new CellAddress(1, 3)), "=SUM(B1:B3)");
        Assert.Equal(5, engine.GetValue(new SheetCell(0, new CellAddress(1, 2))).AsNumber());

        engine.SetValue(new SheetCell(0, new CellAddress(1, 0)), CellValue.Number(10));

        Assert.Equal(21, engine.GetValue(new SheetCell(0, new CellAddress(1, 2))).AsNumber());
        Assert.Equal(28, engine.GetValue(new SheetCell(0, new CellAddress(1, 3))).AsNumber());
        engine.RecalculateAll();
        Assert.Equal(21, engine.GetValue(new SheetCell(0, new CellAddress(1, 2))).AsNumber());
    }

    [Fact]
    public void UnaCellaCheLeggeUnaCellaVersataSiAggiorna()
    {
        var (_, engine) = fixture.CreateWorkbook();
        engine.SetValue(new SheetCell(0, new CellAddress(0, 0)), CellValue.Number(3));
        engine.SetFormula(new SheetCell(0, new CellAddress(0, 2)), "=B2*10");
        engine.SetFormula(new SheetCell(0, new CellAddress(0, 1)), "=SEQUENCE(A1)");
        engine.SetFormula(new SheetCell(0, new CellAddress(5, 1)), "=SUM(B1:B3)");
        var first = engine.GetValue(new SheetCell(0, new CellAddress(5, 1)));
        engine.SetValue(new SheetCell(0, new CellAddress(0, 0)), CellValue.Number(4));
        var second = engine.GetValue(new SheetCell(0, new CellAddress(5, 1)));
        engine.RecalculateAll();
        var third = engine.GetValue(new SheetCell(0, new CellAddress(5, 1)));
        Assert.Equal("6 6 6 20", $"{first} {second} {third} {engine.GetValue(new SheetCell(0, new CellAddress(0, 2)))}");

    }
}

public class LedgerArrayFormulaShapesTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    [Theory]
    [InlineData("=ROW(D5:D8)", 4, 1, "5 6 7 8")]
    [InlineData("=COLUMN(B4:C6)", 1, 2, "2 3")]
    public void LeFormuleMatricialiRiempionoLaLoroArea(string formula, int rows, int columns, string atteso)
    {
        var (_, engine) = fixture.CreateWorkbook();
        var area = new CellRange(new CellAddress(0, 0), new CellAddress(rows - 1, columns - 1));
        engine.SetArrayFormula(new SheetCell(0, area.Start), area, formula);

        Assert.Equal(atteso, string.Join(" ", area.Cells().Select(address => engine.GetValue(new SheetCell(0, address)).ToString())));
    }
}

public class LedgerGeometryDependencyTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    [Theory]
    [InlineData("=ROWS($A$1:A3)", 3)]
    [InlineData("=ROW(A1:A5)", 1)]
    [InlineData("=COLUMNS(A:C)", 3)]
    public void LeFunzioniDiGeometriaNonCreanoRiferimentiCircolari(string formula, double atteso)
    {
        var (_, engine) = fixture.CreateWorkbook();
        engine.SetFormula(new SheetCell(0, new CellAddress(2, 0)), formula);
        Assert.Equal(atteso, engine.GetValue(new SheetCell(0, new CellAddress(2, 0))).AsNumber());
        Assert.Empty(engine.CircularCells);
    }
}

public class LedgerSheetNameReferenceTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    [Theory]
    [InlineData("=AOO109200.B14")]
    [InlineData("='AOO109200'.B14")]
    [InlineData("=SUM(AOO109200.B14:B15)")]
    public void UnFoglioDalNomeSimileAUnRiferimentoSiRaggiunge(string formula)
    {
        var workbook = Workbook.CreateDefault();
        workbook.AddSheet("AOO109200").SetValue(new CellAddress(13, 1), CellValue.Number(7));
        var engine = fixture.CreateEngine(workbook);
        engine.SetFormula(new SheetCell(0, new CellAddress(0, 0)), formula);
        Assert.Equal(7, engine.GetValue(new SheetCell(0, new CellAddress(0, 0))).AsNumber());
    }
}
