using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.Sheets.Engine.Recalculation;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class EngineFixture : IDisposable
{
    private readonly ServiceProvider _provider;

    public EngineFixture()
    {
        _provider = new ServiceCollection()
            .AddSingleton<PulseOffice.Sheets.Formatting.ISpreadsheetLocale>(PulseOffice.Sheets.Formatting.FixedSpreadsheetLocale.Invariant())
            .AddSheetsEngine()
            .BuildServiceProvider();
    }

    public IFunctionRegistry Functions => _provider.GetRequiredService<IFunctionRegistry>();

    public PulseOffice.Sheets.Engine.Evaluation.Evaluator Evaluator => _provider.GetRequiredService<PulseOffice.Sheets.Engine.Evaluation.Evaluator>();

    public ISpecialFormRegistry SpecialForms => _provider.GetRequiredService<ISpecialFormRegistry>();

    private ICalculationEngineFactory Factory => _provider.GetRequiredService<ICalculationEngineFactory>();

    public (Workbook Workbook, CalculationEngine Engine) CreateWorkbook()
    {
        var workbook = Workbook.CreateDefault();
        return (workbook, Factory.Create(workbook));
    }

    public CalculationEngine CreateEngine(Workbook workbook) => Factory.Create(workbook);

    public void Dispose() => _provider.Dispose();
}
