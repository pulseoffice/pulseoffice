using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class FunctionRegressionTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static SheetCell C(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return new SheetCell(0, address);
    }

    private CellValue Valuta(string formula, params (string Cell, CellValue Value)[] cells)
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        foreach (var (cell, value) in cells)
        {
            Assert.True(CellAddress.TryParseA1(cell, out var address));
            workbook.Sheets[0].SetValue(address, value);
        }
        engine.RecalculateAll();
        engine.SetFormula(C("Z99"), formula);
        return engine.GetValue(C("Z99"));
    }

    private double Numero(string formula, params (string Cell, CellValue Value)[] cells)
    {
        var value = Valuta(formula, cells);
        Assert.False(value.IsError, $"{formula} ha prodotto {value}");
        return value.AsNumber();
    }

    private static (string, CellValue) N(string cell, double value) => (cell, CellValue.Number(value));
    private static (string, CellValue) T(string cell, string value) => (cell, CellValue.Text(value));

    [Theory]
    [InlineData("=ROUNDUP(1.1;1)", 1.1)]
    [InlineData("=TRUNC(0.29;2)", 0.29)]
    [InlineData("=ROUNDDOWN(0.29;2)", 0.29)]
    [InlineData("=CEILING(4.2;0.1)", 4.2)]
    [InlineData("=FLOOR(4.2;0.1)", 4.2)]
    [InlineData("=CEILING.MATH(4.2;0.1)", 4.2)]
    [InlineData("=FLOOR.PRECISE(0.3;0.1)", 0.3)]
    [InlineData("=MROUND(0.15;0.1)", 0.2)]
    public void GliArrotondamentiNonSoffronoDellErroreBinario(string formula, double atteso) =>
        Assert.Equal(atteso, Numero(formula), 12);

    [Fact]
    public void ErfcHaPrecisioneRelativaSulleCode()
    {
        var value = Numero("=ERFC(5)");
        Assert.Equal(1.5374597944280349e-12, value, 1e-24);
        Assert.Equal(0.8427007929497149, Numero("=ERF(1)"), 14);
        Assert.Equal(-0.8427007929497149, Numero("=ERF(-1)"), 14);
        Assert.Equal(1.9999999999984626, Numero("=ERFC(-5)"), 14);
    }

    [Theory]
    [InlineData("=SEARCH(\"b?d\";\"abcdbxd\")", 2)]
    [InlineData("=SEARCH(\"c*x\";\"abcdbxd\")", 3)]
    [InlineData("=SEARCH(\"~*\";\"a*b\")", 2)]
    [InlineData("=SEARCH(\"I\";\"aıbi\")", 4)]
    public void SearchAccettaICaratteriJolly(string formula, double atteso) =>
        Assert.Equal(atteso, Numero(formula));

    [Fact]
    public void FixedConDecimaliNegativiArrotondaAlleDecine() =>
        Assert.Equal(1230.ToString("N0", System.Globalization.CultureInfo.InvariantCulture),
                     Valuta("=FIXED(1234.5;-1)").AsText());

    [Theory]
    [InlineData("=VALUE(\"1.5\")", 1.5)]
    [InlineData("=VALUE(\" -2e3 \")", -2000)]
    [InlineData("=VALUE(\"50%\")", 0.5)]
    public void ValueLeggeINumeriInFormaCanonica(string formula, double atteso) =>
        Assert.Equal(atteso, Numero(formula), 12);

    [Fact]
    public void ValueRifiutaLeFormeAmbigue()
    {
        Assert.Equal(CellError.Value, Valuta("=VALUE(\"1,000\")").Error);
    }

    [Fact]
    public void DatevalueLeggeLaFormaIso() =>
        Assert.Equal(45292, Numero("=DATEVALUE(\"2024-01-01\")"));

    [Fact]
    public void IlCriterioNumericoSiLeggeInFormaCanonica() =>
        Assert.Equal(1, Numero("=COUNTIF(A1:A3;\">1.5\")", N("A1", 1), N("A2", 1.4), N("A3", 2)));

    [Fact]
    public void CharEUnicharFuoriIntervalloDannoValore()
    {
        Assert.Equal(CellError.Value, Valuta("=CHAR(256)").Error);
        Assert.Equal(CellError.Value, Valuta("=CHAR(0)").Error);
        Assert.Equal("A", Valuta("=CHAR(65)").AsText());
        Assert.Equal(CellError.NotAvailable, Valuta("=UNICHAR(55296)").Error);
        Assert.Equal(CellError.Value, Valuta("=UNICHAR(0)").Error);
        Assert.Equal(CellError.Value, Valuta("=UNICHAR(1114112)").Error);
    }

    [Theory]
    [InlineData("o'neil 2nd-place", "O'Neil 2Nd-Place")]
    [InlineData("ISTANBUL istanbul", "Istanbul Istanbul")]
    public void ProperMetteLaMaiuscolaDopoOgniNonLettera(string testo, string atteso) =>
        Assert.Equal(atteso, Valuta($"=PROPER(\"{testo}\")").AsText());

    [Fact]
    public void CountifConCriteriVuotiContaLeCelleVuote()
    {
        (string, CellValue)[] celle = [N("A1", 1), T("A3", "x")];
        Assert.Equal(2, Numero("=COUNTIF(A1:A4;\"\")", celle));
        Assert.Equal(2, Numero("=COUNTIF(A1:A4;\"=\")", celle));
        Assert.Equal(2, Numero("=COUNTIF(A1:A4;\"<>\")", celle));
    }

    [Fact]
    public void IlJollyAttraversaGliACapo() =>
        Assert.Equal(1, Numero("=COUNTIF(A1;\"a*b\")", T("A1", "a\nb")));

    [Fact]
    public void SumifEstendeLIntervalloDiSommaAllaFormaDeiCriteri()
    {
        (string, CellValue)[] celle = [T("A1", "x"), T("A2", "y"), T("A3", "x"), N("B1", 1), N("B2", 2), N("B3", 4)];
        Assert.Equal(5, Numero("=SUMIF(A1:A3;\"x\";B1)", celle));
        Assert.Equal(2.5, Numero("=AVERAGEIF(A1:A3;\"x\";B1)", celle));
    }

    [Fact]
    public void SubtotaleEAggregateConCodice3ContanoIValoriNonVuoti()
    {
        (string, CellValue)[] celle = [N("A1", 1), T("A2", "a"), N("A3", 3)];
        Assert.Equal(3, Numero("=SUBTOTAL(3;A1:A4)", celle));
        Assert.Equal(3, Numero("=AGGREGATE(3;0;A1:A4)", celle));
        Assert.Equal(2, Numero("=SUBTOTAL(2;A1:A4)", celle));
    }

    [Fact]
    public void AggregateLeggeLArgomentoKAParte()
    {
        (string, CellValue)[] celle = [N("A1", 1), N("A2", 2), N("A3", 3), N("A4", 4), N("A5", 5)];
        Assert.Equal(4, Numero("=AGGREGATE(14;0;A1:A5;2)", celle));
        Assert.Equal(1, Numero("=AGGREGATE(15;0;A1:A5;1)", celle));
        Assert.Equal(3, Numero("=AGGREGATE(16;0;A1:A5;0.5)", celle));
        Assert.Equal(1.5, Numero("=AGGREGATE(18;0;A1:A5;0.25)", celle), 12);
        Assert.Equal(1.5, Numero("=AGGREGATE(19;0;A1:A5;1)", celle), 12);
        Assert.Equal(CellError.Number, Valuta("=AGGREGATE(18;0;A1:A5;0.1)", celle).Error);
    }

    [Fact]
    public void LookupSuMatriceRestituisceDallUltimaColonna()
    {
        (string, CellValue)[] celle = [N("A1", 1), N("A2", 2), N("A3", 3), T("C1", "a"), T("C2", "b"), T("C3", "c")];
        Assert.Equal("b", Valuta("=LOOKUP(2.5;A1:C3)", celle).AsText());
        (string, CellValue)[] orizzontale = [N("A1", 1), N("B1", 2), N("C1", 3), T("A2", "x"), T("B2", "y"), T("C2", "z")];
        Assert.Equal("y", Valuta("=LOOKUP(2;A1:C2)", orizzontale).AsText());
    }

    [Fact]
    public void IndexConZeroRestituisceLaColonnaIntera()
    {
        (string, CellValue)[] celle = [N("A1", 1), N("A2", 2), N("B1", 10), N("B2", 20)];
        Assert.Equal(30, Numero("=SUM(INDEX(A1:B2;0;2))", celle));
        Assert.Equal(11, Numero("=SUM(INDEX(A1:B2;1;0))", celle));
    }

    [Fact]
    public void MatchUsaIlSegnoDelTipoEIJolly()
    {
        (string, CellValue)[] celle = [T("A1", "mela"), T("A2", "pera"), T("A3", "prugna")];
        Assert.Equal(3, Numero("=MATCH(\"pr*\";A1:A3;0)", celle));
        (string, CellValue)[] numeri = [N("B1", 1), N("B2", 3), N("B3", 5)];
        Assert.Equal(2, Numero("=MATCH(4;B1:B3;7)", numeri));
        (string, CellValue)[] discendenti = [N("B1", 5), N("B2", 3), N("B3", 1)];
        Assert.Equal(2, Numero("=MATCH(2;B1:B3;-5)", discendenti));
    }

    [Theory]
    [InlineData("=ADDRESS(2;3;1;0)", "R2C3")]
    [InlineData("=ADDRESS(2;3;4;0)", "R[2]C[3]")]
    [InlineData("=ADDRESS(2;3;1;1;\"Dati 2024\")", "'Dati 2024'.$C$2")]
    [InlineData("=ADDRESS(2;3;1;1;\"Dati\")", "Dati.$C$2")]
    public void AddressRispettaLoStileELeVirgolette(string formula, string atteso) =>
        Assert.Equal(atteso, Valuta(formula).AsText());

    [Theory]
    [InlineData("=DAYS360(DATE(1995;2;28);DATE(1995;8;31))", 180)]
    [InlineData("=DAYS360(DATE(1996;2;29);DATE(1996;8;31))", 180)]
    [InlineData("=DAYS360(DATE(1996;2;28);DATE(1996;3;31))", 33)]
    [InlineData("=DAYS360(DATE(1996;3;31);DATE(1996;1;30);1)", -60)]
    public void Days360GestisceLaFineDiFebbraio(string formula, double atteso) =>
        Assert.Equal(atteso, Numero(formula));

    [Fact]
    public void MultinomialEWeeknumRifiutanoGliArgomentiNonValidi()
    {
        Assert.Equal(CellError.Number, Valuta("=MULTINOMIAL(2;-1)").Error);
        Assert.Equal(1260, Numero("=MULTINOMIAL(2;3;4)"));
        Assert.Equal(CellError.Number, Valuta("=WEEKNUM(DATE(2024;1;10);5)").Error);
    }

    [Theory]
    [InlineData("=IPMT(0.1/12;1;36;8000)", -66.66666666666667)]
    [InlineData("=PPMT(0.1/12;1;36;8000)", -191.4708308840332)]
    [InlineData("=CUMIPMT(0.09/12;360;125000;1;1;0)", -937.5)]
    [InlineData("=CUMPRINC(0.09/12;360;125000;13;24;0)", -934.1071234208985)]
    [InlineData("=PRICE(39493;43054;0.0575;0.065;100;2;0)", 94.63436162132213)]
    [InlineData("=YIELD(39493;42689;0.0575;95.04287;100;2;0)", 0.06500000688075519)]
    [InlineData("=MDURATION(39448;42370;0.08;0.09;2;1)", 5.733923577059269)]
    [InlineData("=HYPGEOM.DIST(1;4;8;20;1)", 0.46542827657378744)]
    [InlineData("=BETA.DIST(2;8;10;1;1;3)", 0.6854705810546875)]
    [InlineData("=PERCENTRANK({13;12;11;8;4;3;2;1;1;1};2)", 0.333)]
    [InlineData("=PERCENTRANK.INC({1;2;3;6;6;6;7;8;9};5.43)", 0.351)]
    [InlineData("=BESSELY(2.5;1)", 0.14591813796678593)]
    [InlineData("=BESSELK(1.5;1)", 0.27738780360270165)]
    [InlineData("=ERF(0;1.5)", 0.9661051464753108)]
    [InlineData("=POWER(-8;1/3)", -2)]
    [InlineData("=-2^2", 4)]
    [InlineData("=2^3^2", 64)]
    [InlineData("=MAXIFS({10;1;100};{\"b\";\"a\";\"a\"};\"a\")", 100)]
    [InlineData("=SUMIFS({10;1;100};{\"b\";\"a\";\"a\"};\"a\")", 101)]
    [InlineData("=AVERAGEIFS({10;1;100};{\"b\";\"a\";\"a\"};\"a\")", 50.5)]
    [InlineData("=CHITEST({58;35|11;25|10;23};{45.35;47.65|17.56;18.44|16.09;16.91})", 0.0003081920170083095)]
    [InlineData("=YEARFRAC(DATE(2020;2;29);DATE(2021;2;28);1)", 0.9972677595628415)]
    [InlineData("=YEARFRAC(DATE(2020;3;31);DATE(2020;2;29);0)", 0.08611111111111111)]
    [InlineData("=SECOND(TIME(1;2;59.6))", 0)]
    [InlineData("=MINUTE(0.5104166)", 14)]
    [InlineData("=COUNT(1;\"2\";TRUE())", 3)]
    [InlineData("=COUNTBLANK({1;\"\"})", 1)]
    [InlineData("=TYPE({1;2})", 64)]
    [InlineData("=VALUE(\"12:00\")", 0.5)]
    [InlineData("=GAMMA.DIST(0;1;1;0)", 1)]
    [InlineData("=F.DIST(0;2;3;0)", 1)]
    public void ConcordaConCalc(string formula, double atteso) =>
        Assert.Equal(atteso, Numero(formula), 9);

    [Theory]
    [InlineData("=TEXTJOIN(\"-\";1;\"a\";\"\";\"b\")", "a-b")]
    [InlineData("=XLOOKUP(25;{10;20;30};{\"a\";\"b\";\"c\"};\"none\";-1)", "b")]
    [InlineData("=INDEX(SORTBY({\"a\";\"b\";\"c\"};{3;1;2});1)", "b")]
    [InlineData("=INDEX(SORTBY({\"a\"|\"b\"|\"c\"};{1|1|0};1;{3|1|2};-1);1)", "c")]
    [InlineData("=IMDIV(\"-238+240i\";\"10+24i\")", "5+12i")]
    [InlineData("=IMCONJUGATE(\"3j\")", "-3j")]
    [InlineData("=IMLOG10(\"100\")", "2")]
    [InlineData("=ROMAN(499;4)", "ID")]
    [InlineData("=ROMAN(499;1)", "LDVLIV")]
    [InlineData("=TEXT(DATE(2026;3;5);\"yyyy-mm-dd\")", "2026-03-05")]
    [InlineData("=TEXT(0.5;\"h:mm\")", "12:00")]
    [InlineData("=TEXT(0.285;\"0.0%\")", "28.5%")]
    [InlineData("=TEXT(1;\"@\")", "1")]
    [InlineData("=TEXTBEFORE(\"abc\";\"x\";1;0;1)", "abc")]
    [InlineData("=TEXTAFTER(\"aXbxc\";\"x\";1;1)", "bxc")]
    [InlineData("=TEXTAFTER(\"abc\";\"x\";1;0;0;\"none\")", "none")]
    public void TestiComeCalc(string formula, string atteso) =>
        Assert.Equal(atteso, Valuta(formula).AsText());

    [Theory]
    [InlineData("=TIME(0;-1;0)")]
    [InlineData("=1E+308*10")]
    [InlineData("=IMLN(\"0\")")]
    public void FuoriDominioDaNum(string formula) =>
        Assert.Equal(CellError.Number, Valuta(formula).Error);

    [Fact]
    public void LeFunzioniIfsSommanoLIntervalloDaSommareNonQuelloDeiCriteri()
    {
        (string, CellValue)[] celle = [N("A1", 10), N("A2", 20), N("A3", 30), T("B1", "x"), T("B2", "y"), T("B3", "x")];
        Assert.Equal(40, Numero("=SUMIFS(A1:A3;B1:B3;\"x\")", celle));
        Assert.Equal(30, Numero("=MAXIFS(A1:A3;B1:B3;\"x\")", celle));
        Assert.Equal(CellError.Value, Valuta("=SUMIFS(A1:A2;B1:B3;\"x\")", celle).Error);
    }

    [Fact]
    public void SteyxConDuePuntiDaDivisionePerZero() =>
        Assert.Equal(CellError.DivideByZero, Valuta("=STEYX({1;2};{3;4})").Error);
}
