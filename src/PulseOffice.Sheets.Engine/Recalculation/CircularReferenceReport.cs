namespace PulseOffice.Sheets.Engine.Recalculation;

public sealed class CircularReferenceReport
{
    public string? FirstCell(CalculationEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        var sheets = engine.Workbook.Sheets;
        var first = engine.CircularCells
            .Where(cell => cell.SheetIndex >= 0 && cell.SheetIndex < sheets.Count)
            .OrderBy(cell => cell.SheetIndex).ThenBy(cell => cell.Address)
            .Select(cell => (SheetCell?)cell)
            .FirstOrDefault();
        return first is { } found ? $"{sheets[found.SheetIndex].Name}!{found.Address.ToA1()}" : null;
    }
}
