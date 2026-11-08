using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class DeepAuditRound53Tests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static SheetCell At(int row, int column) => new(0, new CellAddress(row, column));

    private CellValue Single(string formula, params (int Row, int Column, CellValue Value)[] cells)
    {
        var (_, engine) = fixture.CreateWorkbook();
        foreach (var (row, column, value) in cells) engine.SetValue(At(row, column), value);
        engine.SetFormula(At(0, 10), formula);
        return engine.GetValue(At(0, 10));
    }

    [Theory]
    [InlineData("=REGEXTEST(\"Alpha 42\",\"[0-9]+\")", true)]
    [InlineData("=REGEXTEST(\"alpha\",\"ALPHA\")", false)]
    [InlineData("=REGEXTEST(\"alpha\",\"ALPHA\",1)", true)]
    [InlineData("=REGEXTEST(\"alpha\",\"^b\")", false)]
    public void RegexTestFollowsCaseSensitivity(string formula, bool expected)
    {
        var value = Single(formula);
        Assert.Equal(ValueKind.Boolean, value.Kind);
        Assert.Equal(expected, value.AsBoolean());
    }

    [Theory]
    [InlineData("=REGEXTEST(\"a\",\"a\",2)")]
    [InlineData("=REGEXTEST(\"a\",\"(\")")]
    [InlineData("=REGEXEXTRACT(\"a\",\"a\",3)")]
    [InlineData("=REGEXREPLACE(\"a\",\"[\",\"b\")")]
    public void InvalidPatternsAndModesGiveValueError(string formula) =>
        Assert.Equal(CellError.Value, Single(formula).Error);

    [Fact]
    public void RegexExtractReturnsFirstMatchAllMatchesOrGroups()
    {
        Assert.Equal("Sonia Rees", Single("=REGEXEXTRACT(\"Sonia Rees and Shu Ito\",\"[A-Z][a-z]+ [A-Z][a-z]+\")").AsText());
        Assert.Equal(CellError.NotAvailable, Single("=REGEXEXTRACT(\"abc\",\"[0-9]\")").Error);

        var (_, engine) = fixture.CreateWorkbook();
        engine.SetFormula(At(0, 0), "=REGEXEXTRACT(\"Sonia Rees and Shu Ito\",\"[A-Z][a-z]+ [A-Z][a-z]+\",1)");
        Assert.Equal("Sonia Rees", engine.GetValue(At(0, 0)).AsText());
        Assert.Equal("Shu Ito", engine.GetValue(At(1, 0)).AsText());

        engine.SetFormula(At(5, 0), "=REGEXEXTRACT(\"2026-11-06\",\"(\\d+)-(\\d+)-(x)?(\\d+)\",2)");
        Assert.Equal("2026", engine.GetValue(At(5, 0)).AsText());
        Assert.Equal("11", engine.GetValue(At(5, 1)).AsText());
        Assert.Equal(string.Empty, engine.GetValue(At(5, 2)).AsText());
        Assert.Equal("06", engine.GetValue(At(5, 3)).AsText());
    }

    [Theory]
    [InlineData("=REGEXREPLACE(\"a1b2c3\",\"[0-9]\",\"#\")", "a#b#c#")]
    [InlineData("=REGEXREPLACE(\"a1b2c3\",\"[0-9]\",\"#\",2)", "a1b#c3")]
    [InlineData("=REGEXREPLACE(\"a1b2c3\",\"[0-9]\",\"#\",-1)", "a1b2c#")]
    [InlineData("=REGEXREPLACE(\"a1b2c3\",\"[0-9]\",\"#\",9)", "a1b2c3")]
    [InlineData("=REGEXREPLACE(\"John Smith\",\"(\\w+) (\\w+)\",\"$2, $1\")", "Smith, John")]
    [InlineData("=REGEXREPLACE(\"ABC abc\",\"abc\",\"x\",0,1)", "x x")]
    public void RegexReplaceHonoursOccurrenceAndCase(string formula, string expected) =>
        Assert.Equal(expected, Single(formula).AsText());

    [Fact]
    public void RegexFunctionsShareTheBacktrackingGuard()
    {
        var value = Single("=REGEXTEST(REPT(\"a\",40)&\"!\",\"^(a+)+$\")");
        Assert.True(value.Kind == ValueKind.Boolean || value.Error == CellError.Value);
    }

    [Theory]
    [InlineData("=SUM(A:B)")]
    [InlineData("=COUNT(A:A)")]
    [InlineData("=COUNTA(A:B)")]
    [InlineData("=SUMIFS(B:B,A:A,\">2\")")]
    [InlineData("=COUNTIFS(A:A,\"\")")]
    [InlineData("=COUNTIFS(A:A,\"<>x\",B:B,\">=0\")")]
    public void WholeColumnAggregatesMatchAFreshEvaluationAfterEveryEdit(string formula)
    {
        var (_, engine) = fixture.CreateWorkbook();
        var target = At(0, 5);
        engine.SetFormula(target, formula);
        var random = new Random(53);
        for (var step = 0; step < 600; step++)
        {
            var cell = At(random.Next(0, 900), random.Next(0, 2));
            switch (random.Next(0, 7))
            {
                case 0: engine.SetValue(cell, CellValue.Empty); break;
                case 1: engine.SetValue(cell, CellValue.Text("x")); break;
                case 2: engine.SetValue(cell, CellValue.Boolean(true)); break;
                case 3: engine.SetFormula(cell, $"={random.Next(0, 9)}*2"); break;
                default: engine.SetValue(cell, CellValue.Number(random.Next(-5, 9) + 0.25)); break;
            }

            var expected = engine.EvaluateExpression(formula.TrimStart('='), target)[0];
            Assert.Equal(expected, engine.GetValue(target));
        }
    }

    [Fact]
    public void AnErrorInsideAWholeColumnPropagatesAndClearsWhenFixed()
    {
        var (_, engine) = fixture.CreateWorkbook();
        for (var row = 0; row < 2000; row++) engine.SetValue(At(row, 0), CellValue.Number(1));
        engine.SetFormula(At(0, 5), "=SUM(A:A)");
        engine.SetFormula(At(1500, 0), "=1/0");
        Assert.Equal(CellError.DivideByZero, engine.GetValue(At(0, 5)).Error);
        engine.SetValue(At(1500, 0), CellValue.Number(2));
        Assert.Equal(2001, engine.GetValue(At(0, 5)).AsNumber());
    }

    [Fact]
    public void ADependentFormulaCellInsideTheColumnRefreshesTheAggregate()
    {
        var (_, engine) = fixture.CreateWorkbook();
        engine.SetValue(At(0, 2), CellValue.Number(1));
        engine.SetFormula(At(700, 0), "=C1*10");
        engine.SetFormula(At(0, 5), "=SUM(A:A)");
        Assert.Equal(10, engine.GetValue(At(0, 5)).AsNumber());
        engine.SetValue(At(0, 2), CellValue.Number(3));
        Assert.Equal(30, engine.GetValue(At(0, 5)).AsNumber());
    }

    [Theory]
    [InlineData("it", "REGEXREPLACE", "REGEX.SOSTITUISCI")]
    [InlineData("de", "REGEXEXTRACT", "REGEXEXTRAHIEREN")]
    [InlineData("es", "REGEXTEST", "REGEX.PRUEBA")]
    [InlineData("fr", "REGEXEXTRACT", "REGEX.EXTRAIRE")]
    [InlineData("en", "REGEXTEST", "REGEXTEST")]
    public void RegexFunctionNamesAreLocalized(string language, string canonical, string local)
    {
        var vocabulary = new PulseOffice.Sheets.Engine.Localization.FunctionNameVocabulary();
        Assert.Equal(local, vocabulary.ToLanguage(canonical, language));
        Assert.Equal(canonical, vocabulary.ToCanonical(local, language));
    }

    [Fact]
    public void ABatchRecalculatesOnceWithEveryValueInPlace()
    {
        var (_, engine) = fixture.CreateWorkbook();
        engine.SetFormula(At(0, 5), "=A1+A2+A3");
        engine.Batch(() =>
        {
            engine.SetValue(At(0, 0), CellValue.Number(1));
            engine.SetValue(At(1, 0), CellValue.Number(2));
            engine.SetValue(At(2, 0), CellValue.Number(3));
        });
        Assert.Equal(6, engine.GetValue(At(0, 5)).AsNumber());
    }

    [Fact]
    public void ClearingTheSheetDropsCachedBlocks()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        for (var row = 0; row < 300; row++) engine.SetValue(At(row, 0), CellValue.Number(1));
        engine.SetFormula(At(0, 5), "=SUM(A:A)");
        Assert.Equal(300, engine.GetValue(At(0, 5)).AsNumber());
        workbook.Sheets[0].Clear();
        engine.RecalculateFrom([At(0, 0)]);
        engine.SetFormula(At(0, 5), "=SUM(A:A)");
        engine.SetValue(At(10, 0), CellValue.Number(4));
        Assert.Equal(4, engine.GetValue(At(0, 5)).AsNumber());
    }
}
