using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Operations;

public sealed class EngineShiftedFormulas(CalculationEngine engine) : IShiftedFormulaEvaluator
{
    public CellValue EvaluateShifted(Worksheet sheet, CellAddress formula, int rowDelta)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        var index = engine.Workbook.IndexOf(sheet);
        return index < 0
            ? CellValue.FromError(CellError.Reference)
            : engine.EvaluateOffset(new SheetCell(index, formula), rowDelta, 0);
    }
}
