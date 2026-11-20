using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Recalculation;

namespace PulseOffice.Sheets.Engine.Operations;

public interface IInvalidDataFinder
{
    IReadOnlyList<CellAddress> Find(CalculationEngine engine, int sheetIndex, int limit);
}

public sealed class InvalidDataFinder(IDataValidator validator) : IInvalidDataFinder
{
    public IReadOnlyList<CellAddress> Find(CalculationEngine engine, int sheetIndex, int limit)
    {
        ArgumentNullException.ThrowIfNull(engine);

        var sheet = engine.Workbook.Sheets[sheetIndex];
        if (sheet.Validations.Count == 0) return [];

        var found = new List<CellAddress>();
        foreach (var (address, cell) in sheet.NonEmptyCells().OrderBy(pair => pair.Key.Row).ThenBy(pair => pair.Key.Column))
        {
            if (cell.Value.IsEmpty || validator.RuleAt(sheet, address) is null) continue;
            if (validator.Validate(engine, sheetIndex, address, cell.Value).IsValid) continue;
            found.Add(address);
            if (found.Count >= limit) break;
        }
        return found;
    }
}
