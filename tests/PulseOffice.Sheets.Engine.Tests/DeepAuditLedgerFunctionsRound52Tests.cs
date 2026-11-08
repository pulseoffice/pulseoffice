using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class DeepAuditLedgerFunctionsRound52Tests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static readonly object?[][] Orchard =
    [
        ["Tree", "Height", "Age", "Yield", "Profit", "Height", "Tree", "Rich", "Tree"],
        ["=Apple", ">10", null, null, null, "<16", null, "=E5>100", "?ear"],
        ["=Pear", null, null, null, null, null, null, null, "Ch*"],
        ["Tree", "Height", "Age", "Yield", "Profit", null],
        ["Apple", 18d, 20d, 14d, 105d, null],
        ["Pear", 12d, 12d, 10d, 96d, null],
        ["Cherry", 13d, 14d, 9d, 105d, null],
        ["Apple", 14d, 15d, 10d, 75d, null],
        ["Pear", 9d, 8d, 8d, 76.8, null],
        ["Apple", 8d, 9d, 6d, 45d, null],
    ];

    private CellValue Database(string formula)
    {
        var (_, engine) = fixture.CreateWorkbook();
        for (var row = 0; row < Orchard.Length; row++)
            for (var column = 0; column < Orchard[row].Length; column++)
                switch (Orchard[row][column])
                {
                    case string computed when computed.StartsWith("=E", StringComparison.Ordinal): engine.SetFormula(new SheetCell(0, new CellAddress(row, column)), computed); break;
                    case string text: engine.SetValue(new SheetCell(0, new CellAddress(row, column)), CellValue.Text(text)); break;
                    case double number: engine.SetValue(new SheetCell(0, new CellAddress(row, column)), CellValue.Number(number)); break;
                }

        var cell = new SheetCell(0, new CellAddress(30, 10));
        engine.SetFormula(cell, formula);
        return engine.GetValue(cell);
    }

    [Theory]
    [InlineData("=DCOUNT(A4:E10,\"Age\",A1:F2)", 1)]
    [InlineData("=DCOUNTA(A4:E10,\"Profit\",A1:F2)", 1)]
    [InlineData("=DMAX(A4:E10,\"Profit\",A1:A3)", 105)]
    [InlineData("=DMIN(A4:E10,\"Profit\",A1:B2)", 75)]
    [InlineData("=DSUM(A4:E10,\"Profit\",A1:A2)", 225)]
    [InlineData("=DSUM(A4:E10,\"Profit\",A1:F2)", 75)]
    [InlineData("=DPRODUCT(A4:E10,\"Yield\",A1:F2)", 10)]
    [InlineData("=DAVERAGE(A4:E10,\"Yield\",A1:B2)", 12)]
    [InlineData("=DAVERAGE(A4:E10,3,A4:E10)", 13)]
    [InlineData("=DSTDEV(A4:E10,\"Yield\",A1:A3)", 2.96647939483827)]
    [InlineData("=DSTDEVP(A4:E10,\"Yield\",A1:A3)", 2.65329983228432)]
    [InlineData("=DVAR(A4:E10,\"Yield\",A1:A3)", 8.8)]
    [InlineData("=DVARP(A4:E10,\"Yield\",A1:A3)", 7.04)]
    [InlineData("=DSUM(A4:E10,\"Profit\",G1:G2)", 502.8)]
    [InlineData("=DSUM(A4:E10,\"Profit\",H1:H2)", 210)]
    [InlineData("=DSUM(A4:E10,\"Profit\",I1:I3)", 277.8)]
    [InlineData("=DCOUNT(A4:E10,,A1:A3)", 5)]
    public void DatabaseFunctionsMatchTheDocumentedOrchardExample(string formula, double expected)
    {
        var value = Database(formula);
        Assert.True(value.Kind == ValueKind.Number, $"{formula} gave {value}");
        Assert.Equal(expected, value.AsNumber(), 9);
    }

    [Theory]
    [InlineData("=DGET(A4:E10,\"Yield\",A1:A3)", "#NUM!")]
    [InlineData("=DGET(A4:E10,\"Yield\",A1:A2)", "#NUM!")]
    public void DatabaseErrorsMatchTheDocumentedValues(string formula, string expected) =>
        Assert.Equal(expected, Database(formula).ToString());

    [Theory]
    [InlineData("en-US", "=DOLLAR(1234.567,2)", "$1,234.57")]
    [InlineData("it-IT", "=DOLLAR(1234.567,2)", "1.234,57 €")]
    [InlineData("de-DE", "=DOLLAR(1234.567,2)", "1.234,57 €")]
    [InlineData("en-US", "=FIXED(1234.567,1)", "1,234.6")]
    [InlineData("it-IT", "=FIXED(1234.567,1,TRUE())", "1234,6")]
    [InlineData("it-IT", "=TEXT(45292,\"gg/mm/aaaa\")", "01/01/2024")]
    [InlineData("it-IT", "=TEXT(45292,\"gggg\")", "lunedì")]
    [InlineData("de-DE", "=TEXT(45292,\"TT.MM.JJJJ\")", "01.01.2024")]
    [InlineData("fr-FR", "=TEXT(45292,\"jj/mm/aaaa\")", "01/01/2024")]
    [InlineData("es-ES", "=TEXT(45292,\"dd/mm/aaaa\")", "01/01/2024")]
    [InlineData("it-IT", "=TEXT(45292,\"dd/mm/yyyy\")", "01/01/2024")]
    [InlineData("it-IT", "=TEXT(0.75,\"h:mm AM/PM\")", "6:00 PM")]
    [InlineData("en-US", "=TEXT(45292,\"aaaa\")", "aaaa")]
    public void TextResultsFollowTheInterfaceLanguage(string culture, string formula, string expected) =>
        Assert.Equal(expected, Localized(culture, formula).ToString());

    [Theory]
    [InlineData("en-US", "=VALUE(\"1,234.5\")", 1234.5)]
    [InlineData("it-IT", "=VALUE(\"1.234,5\")", 1234.5)]
    [InlineData("de-DE", "=VALUE(\"-1.234.567,5\")", -1234567.5)]
    [InlineData("fr-FR", "=VALUE(\"1 234,5\")", 1234.5)]
    [InlineData("en-US", "=NUMBERVALUE(\"1,234.5\")", 1234.5)]
    [InlineData("it-IT", "=NUMBERVALUE(\"1.234,5\")", 1234.5)]
    [InlineData("it-IT", "=VALUE(\"1,5\")", 1.5)]
    public void NumbersAreReadWithTheInterfaceSeparators(string culture, string formula, double expected)
    {
        var value = Localized(culture, formula);
        Assert.True(value.Kind == ValueKind.Number, $"{culture} {formula} gave {value}");
        Assert.Equal(expected, value.AsNumber(), 9);
    }

    [Theory]
    [InlineData("en-US", "=VALUE(\"1,23\")")]
    [InlineData("en-US", "=VALUE(\"1,2345\")")]
    [InlineData("it-IT", "=VALUE(\"1,234.5\")")]
    public void MisplacedThousandsSeparatorsAreNotNumbers(string culture, string formula) =>
        Assert.Equal("#VALUE!", Localized(culture, formula).ToString());

    [Theory]
    [InlineData("=INFO(\"recalc\")", "Automatic")]
    [InlineData("=INFO(\"origin\")", "$A:$A$1")]
    public void InfoMatchesTheDocumentedTexts(string formula, string expected) =>
        Assert.Equal(expected, Database(formula).ToString());

    [Fact]
    public void CatastrophicRegexHitsTheTimeoutInsteadOfHanging()
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var value = Database("=REGEX(REPT(\"a\",40)&\"!\",\"^(a+)+$\")");
        clock.Stop();
        Assert.True(value.IsError, $"gave {value}");
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(10), $"took {clock.Elapsed}");
    }

    private static CellValue Localized(string culture, string formula)
    {
        using var provider = new ServiceCollection()
            .AddSingleton<ISpreadsheetLocale>(new FixedSpreadsheetLocale(new CultureInfo(culture)))
            .AddSheetsEngine()
            .BuildServiceProvider();
        var engine = provider.GetRequiredService<ICalculationEngineFactory>().Create(Workbook.CreateDefault());
        var cell = new SheetCell(0, new CellAddress(0, 0));
        engine.SetFormula(cell, formula);
        return engine.GetValue(cell);
    }
}
