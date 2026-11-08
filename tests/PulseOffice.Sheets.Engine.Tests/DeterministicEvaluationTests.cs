using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.Sheets.Engine.Evaluation.Functions.Providers;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class DeterministicEvaluationTests
{
    private sealed class FixedTime(DateTimeOffset moment) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => moment;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private CellValue Evaluate(string formula, string culture = "en-US", int seed = 7)
    {
        using var provider = new ServiceCollection()
            .AddSingleton<TimeProvider>(new FixedTime(new DateTimeOffset(2024, 2, 29, 10, 0, 0, TimeSpan.Zero)))
            .AddSingleton<IRandomSource>(new SeededRandomSource(seed))
            .AddSingleton<PulseOffice.Sheets.Formatting.ISpreadsheetLocale>(new PulseOffice.Sheets.Formatting.FixedSpreadsheetLocale(System.Globalization.CultureInfo.GetCultureInfo(culture)))
            .AddSheetsEngine()
            .BuildServiceProvider();
        var workbook = Workbook.CreateDefault();
        var engine = provider.GetRequiredService<ICalculationEngineFactory>().Create(workbook);
        engine.SetFormula(new SheetCell(0, new CellAddress(0, 1)), "=SUM(1.5; 2)");
        engine.SetFormula(new SheetCell(0, CellAddress.First), formula);
        engine.RecalculateAll();
        return engine.GetValue(new SheetCell(0, CellAddress.First));
    }

    [Fact]
    public void OggiVieneDalTimeProviderIniettato() =>
        Assert.Equal(45351, Evaluate("=TODAY()").AsNumber());

    [Fact]
    public void LoStessoSemeDaGliStessiNumeriCasuali() =>
        Assert.Equal(Evaluate("=RAND()", seed: 42).AsNumber(), Evaluate("=RAND()", seed: 42).AsNumber());

    [Fact]
    public void FormulaMostraLaFormulaNellaLinguaDelFoglio()
    {
        Assert.Equal("=SUM(1.5, 2)", Evaluate("=FORMULA(B1)").AsText());
        Assert.Equal("=SOMMA(1,5; 2)", Evaluate("=FORMULA(B1)", "it-IT").AsText());
    }

    [Fact]
    public void CleanTogieAncheINonCaratteriUnicode() =>
        Assert.Equal("ab", Evaluate("=CLEAN(\"a\" & UNICHAR(65534) & UNICHAR(1) & \"b\" & UNICHAR(65535))").AsText());
}
