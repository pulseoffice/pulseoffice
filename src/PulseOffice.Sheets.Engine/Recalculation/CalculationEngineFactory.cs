using PulseOffice.Sheets.Engine.Evaluation;
using PulseOffice.Sheets.Engine.Evaluation.Formats;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.Sheets.Formatting;

namespace PulseOffice.Sheets.Engine.Recalculation;

public interface ICalculationEngineFactory
{
    CalculationEngine Create(Workbook workbook);
}

public sealed class CalculationEngineFactory(
    IFunctionRegistry functions, Evaluator evaluator, IFormatHints hints, IFormulaFormatInference formats, PulseOffice.Foundation.Identity.IUserIdentity identity)
    : ICalculationEngineFactory
{
    public CalculationEngine Create(Workbook workbook) => new(workbook, functions, evaluator, hints, formats, identity);
}
