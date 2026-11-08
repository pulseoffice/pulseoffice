using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class NumberToTextAndRuleTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static CellAddress A(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    private CellValue Calc(string formula)
    {
        var (_, engine) = fixture.CreateWorkbook();
        engine.SetFormula(new SheetCell(0, A("A1")), formula);
        return engine.GetValue(new SheetCell(0, A("A1")));
    }

    [Theory]
    [InlineData("=\"[\"&(0.1+0.2)&\"]\"", "[0.3]")]
    [InlineData("=CONCATENATE(2/3)", "0.666666666666667")]
    [InlineData("=\"[\"&1E+20&\"]\"", "[1E+020]")]
    [InlineData("=\"[\"&1E-15&\"]\"", "[1E-015]")]
    [InlineData("=\"[\"&0.00001&\"]\"", "[0.00001]")]
    [InlineData("=\"[\"&1234567890123456&\"]\"", "[1234567890123456]")]
    [InlineData("=\"[\"&12345678901234567&\"]\"", "[1.23456789012346E+016]")]
    [InlineData("=\"[\"&(1=1)&\"]\"", "[1]")]
    [InlineData("=\"[\"&9007199254740992&\"]\"", "[9.00719925474099E+015]")]
    [InlineData("=\"[\"&9999999999999990&\"]\"", "[9.99999999999999E+015]")]
    [InlineData("=\"[\"&5555555555555550&\"]\"", "[5555555555555550]")]
    [InlineData("=\"[\"&123456789012345.6&\"]\"", "[123456789012346]")]
    [InlineData("=\"[\"&LEFT(1/3;4)&\"]\"", "[0.33]")]
    [InlineData("=TEXTJOIN(\";\";TRUE();1/3;2)", "0.333333333333333;2")]
    public void INumeriDiventanoTestoComeInCalc(string formula, string expected) =>
        Assert.Equal(expected, Calc(formula).ToString());

    [Theory]
    [InlineData("=TRUE()", true)]
    [InlineData("=FALSE()", false)]
    [InlineData("=AND(TRUE();TRUE)", true)]
    public void TrueEFalseSiScrivonoAncheComeFunzioni(string formula, bool expected) =>
        Assert.Equal(CellValue.Boolean(expected), Calc(formula));

    [Theory]
    [InlineData("=VLOOKUP(4;{1;\"a\"|3;\"b\"|5;\"c\"};2;1)", "b")]
    [InlineData("=HLOOKUP(\"x\";{\"x\";\"y\"|1;2};2;0)", "1")]
    [InlineData("=ROWS({1;2|3;4|5;6})", "3")]
    [InlineData("=ATAN2(0;0)", "0")]
    [InlineData("=FIND(\"\";\"abc\")", "#VALUE!")]
    [InlineData("=SEARCH(\"\";\"abc\";4)", "#VALUE!")]
    public void RisultatiComeInCalc(string formula, string expected) =>
        Assert.Equal(expected, Calc(formula).ToString());

    [Fact]
    public void LenContaQuindiciCifreSignificative() =>
        Assert.Equal(17, Calc("=LEN(1/3)").AsNumber());

    [Fact]
    public void UnaRegolaCondizionaleConosceLet()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        engine.SetValue(new SheetCell(0, A("A1")), CellValue.Number(5));
        sheet.ConditionalRules.Add(new ConditionalRule
        {
            Range = new CellRange(A("A1"), A("A1")),
            Formula = "LET(x;A1;x>0)",
            Style = new CellStyle { BackgroundHex = "#00FF00" },
        });

        var evaluator = new ConditionalFormattingEvaluator(fixture.Evaluator);
        Assert.False(evaluator.Evaluate(sheet, A("A1"), engine.ContextAt(new SheetCell(0, A("A1")))).IsEmpty);
    }
}

public class ItalianNumberToTextTests
{
    [Theory]
    [InlineData("=LEFT(1/3;4)", "0,33")]
    [InlineData("=TEXTJOIN(\"-\";1;1.5;2)", "1,5-2")]
    [InlineData("=REPT(0.5;2)", "0,50,5")]
    [InlineData("=\"x\"&1E+20", "x1E+020")]
    public void NellaLinguaItalianaIlTestoUsaLaVirgola(string formula, string expected)
    {
        using var provider = new ServiceCollection()
            .AddSingleton<ISpreadsheetLocale>(new FixedSpreadsheetLocale(new System.Globalization.CultureInfo("it-IT")))
            .AddSheetsEngine()
            .BuildServiceProvider();
        var engine = provider.GetRequiredService<Recalculation.ICalculationEngineFactory>().Create(Workbook.CreateDefault());
        Assert.True(CellAddress.TryParseA1("A1", out var a1));
        engine.SetFormula(new SheetCell(0, a1), formula);
        Assert.Equal(expected, engine.GetValue(new SheetCell(0, a1)).ToString());
    }
}
