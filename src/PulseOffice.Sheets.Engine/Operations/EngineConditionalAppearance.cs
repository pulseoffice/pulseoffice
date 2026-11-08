using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.Formatting;

namespace PulseOffice.Sheets.Engine.Operations;

public sealed class EngineConditionalAppearance(ConditionalFormattingEvaluator evaluator, CalculationEngine engine, int sheetIndex)
    : IConditionalAppearanceSource
{
    public ConditionalAppearance At(Worksheet sheet, CellAddress address) =>
        sheet.ConditionalRules.Count == 0
            ? ConditionalAppearance.None
            : evaluator.Evaluate(sheet, address, engine.ContextAt(new SheetCell(sheetIndex, address)));
}
