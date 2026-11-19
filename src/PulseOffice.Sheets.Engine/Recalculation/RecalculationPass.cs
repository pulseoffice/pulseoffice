namespace PulseOffice.Sheets.Engine.Recalculation;

internal sealed class RecalculationPass(IEnumerable<SheetCell> order, HashSet<SheetCell> spilled)
{
    public HashSet<SheetCell> Pending { get; } = [.. order];
    public HashSet<SheetCell> InProgress { get; } = [];
    public HashSet<SheetCell> SelfReads { get; } = [];
    public HashSet<SheetCell> Evaluating { get; } = [];
    public HashSet<SheetCell> Spilled => spilled;
}
