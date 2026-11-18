using PulseOffice.Sheets.Engine.Recalculation;

namespace PulseOffice.Sheets.Engine.Operations;

public interface ITableRenaming
{
    bool Rename(CalculationEngine engine, string table, string newName);
}

public sealed class TableRenaming : ITableRenaming
{
    public bool Rename(CalculationEngine engine, string table, string newName)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);
        var workbook = engine.Workbook;
        if (workbook.DatabaseRanges.Find(table) is not { } existing) return false;
        if (!string.Equals(existing.Name, newName, StringComparison.OrdinalIgnoreCase) && workbook.DatabaseRanges.Find(newName) is not null) return false;

        IReadOnlyList<TableRename> renames = [new TableRename(existing.Name, newName, null, null)];
        engine.Batch(() =>
        {
            foreach (var sheet in workbook.Sheets)
                foreach (var (address, cell) in sheet.NonEmptyCells().Where(pair => pair.Value.Formula?.Contains('[', StringComparison.Ordinal) == true).ToList())
                {
                    var renamer = new TableReferenceRenamer(renames);
                    if (renamer.Rewrite(cell.Formula!) is { } rewritten && renamer.Changed)
                        sheet[address] = cell with { Formula = rewritten };
                }
            workbook.DatabaseRanges.Remove(existing.Name);
            workbook.DatabaseRanges.Define(existing with { Name = newName });
        });
        engine.RecalculateAll();
        return true;
    }
}
