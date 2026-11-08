using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Foundation.Text;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class LocaleReadingTests : IDisposable
{
    private static readonly CultureInfo Italian = new("it-IT");

    private readonly ServiceProvider _provider = new ServiceCollection()
        .AddSingleton<ISpreadsheetLocale>(new FixedSpreadsheetLocale(Italian))
        .AddSheetsEngine()
        .BuildServiceProvider();

    public void Dispose() => _provider.Dispose();

    private (Workbook Workbook, CalculationEngine Engine) Create()
    {
        var workbook = Workbook.CreateDefault();
        return (workbook, _provider.GetRequiredService<ICalculationEngineFactory>().Create(workbook));
    }

    private CellValue Evaluate(string formula)
    {
        var (_, engine) = Create();
        var cell = new SheetCell(0, new CellAddress(5, 5));
        engine.SetFormula(cell, formula);
        return engine.GetValue(cell);
    }

    [Fact]
    public void ValoreLeggeLaVirgolaDecimaleItaliana() =>
        Assert.Equal(1.5, Evaluate("=VALUE(\"1,5\")").AsNumber(), 10);

    [Fact]
    public void ValoreRifiutaIlPuntoCheInItalianoSeparaLeMigliaia() =>
        Assert.Equal(CellError.Value, Evaluate("=VALUE(\"1.5\")").Error);

    [Fact]
    public void IlLettoreRicadeSullaFormaInvarianteQuandoNonEAmbigua()
    {
        var numbers = new NumericText(new FixedSpreadsheetLocale(new CultureInfo("fr-FR")));

        Assert.True(numbers.TryRead("1.5", out var number));
        Assert.Equal(1.5, number, 10);
    }

    [Fact]
    public void DataValoreLeggeIlGiornoPrimaDelMese()
    {
        var serial = Evaluate("=DATEVALUE(\"31/12/2024\")");
        var expected = Evaluate("=DATE(2024;12;31)");

        Assert.Equal(expected.AsNumber(), serial.AsNumber());
    }

    [Fact]
    public void OrarioValoreLeggeLOraItaliana() =>
        Assert.Equal(0.5, Evaluate("=TIMEVALUE(\"12:00\")").AsNumber(), 6);

    [Fact]
    public void ICriteriLeggonoINumeriNellaLingua()
    {
        var (_, engine) = Create();
        engine.SetValue(new SheetCell(0, new CellAddress(0, 0)), CellValue.Number(1));
        engine.SetValue(new SheetCell(0, new CellAddress(1, 0)), CellValue.Number(2));
        var target = new SheetCell(0, new CellAddress(0, 2));
        engine.SetFormula(target, "=COUNTIF(A1:A2;\">1,5\")");

        Assert.Equal(1, engine.GetValue(target).AsNumber());
    }

    [Fact]
    public void FissoScriveNellaLingua() =>
        Assert.Equal("1.234,50", Evaluate("=FIXED(1234.5;2)").AsText());

    [Fact]
    public void TestoInColonneLeggeINumeriNellaLingua()
    {
        var sheet = Workbook.CreateDefault().Sheets[0];
        sheet.SetValue(CellAddress.First, CellValue.Text("1,5;x"));

        var plan = new TextToColumns(new DelimitedText(), new FixedSpreadsheetLocale(Italian))
            .Plan(sheet, new TextToColumnsRequest(new CellRange(CellAddress.First, CellAddress.First)));

        Assert.Equal(1.5, plan.Values[CellAddress.First].AsNumber(), 10);
    }
}
