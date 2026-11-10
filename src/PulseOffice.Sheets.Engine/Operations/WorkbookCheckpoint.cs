using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.Pivot;

namespace PulseOffice.Sheets.Engine.Operations;

public sealed record WorkbookCheckpoint(
    IReadOnlyList<Worksheet> Sheets,
    IReadOnlyList<SheetSnapshot> Snapshots,
    IReadOnlyList<PivotDefinition> Pivots)
{
    public ChangeLogMemento? Changes { get; init; }

    public ExternalDataMemento? ExternalData { get; init; }

    public IReadOnlyList<DefinedName> Names { get; init; } = [];

    public ConsolidationSettings? Consolidation { get; init; }
}

public interface IWorkbookCheckpoints
{
    WorkbookCheckpoint Capture(Workbook workbook);

    void Restore(CalculationEngine engine, Workbook workbook, WorkbookCheckpoint checkpoint);
}

public sealed class WorkbookCheckpoints(ISheetSnapshots snapshots) : IWorkbookCheckpoints
{
    public WorkbookCheckpoint Capture(Workbook workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        return new WorkbookCheckpoint(
            [.. workbook.Sheets],
            [.. workbook.Sheets.Select(snapshots.Capture)],
            [.. workbook.Pivots])
        {
            Changes = workbook.Changes.Save(),
            ExternalData = workbook.ExternalData.Save(workbook.DatabaseRanges),
            Names = [.. workbook.DefinedNames],
            Consolidation = workbook.Consolidation,
        };
    }

    public void Restore(CalculationEngine engine, Workbook workbook, WorkbookCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(checkpoint);

        foreach (var extra in workbook.Sheets.Except(checkpoint.Sheets).ToList())
            workbook.RemoveSheet(extra);

        for (var index = 0; index < checkpoint.Sheets.Count; index++)
            if (!workbook.Sheets.Contains(checkpoint.Sheets[index]))
                workbook.InsertSheet(checkpoint.Sheets[index], index);

        for (var index = 0; index < checkpoint.Sheets.Count; index++)
            snapshots.Restore(engine, index, checkpoint.Snapshots[index]);

        workbook.Pivots.Clear();
        workbook.Pivots.AddRange(checkpoint.Pivots);

        if (checkpoint.Changes is { } changes) workbook.Changes.Restore(changes);

        foreach (var name in workbook.DefinedNames.Except(checkpoint.Names).ToList()) workbook.RemoveName(name.Name, name.Scope);
        foreach (var name in checkpoint.Names) workbook.DefineName(name);

        if (checkpoint.ExternalData is { } external) workbook.ExternalData.Restore(external, workbook.DatabaseRanges);

        workbook.Consolidation = checkpoint.Consolidation;
    }
}
