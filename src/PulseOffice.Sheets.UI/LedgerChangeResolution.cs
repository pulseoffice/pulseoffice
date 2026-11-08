using PulseOffice.Sheets.Engine.Operations;

namespace PulseOffice.Sheets.UI;

public interface ILedgerChangeResolution
{
    int Resolve(IReadOnlyList<string> ids, bool accept, string description);
}

public sealed class LedgerChangeResolution(
    IActiveSheetContext context, IWorkbookCheckpoints checkpoints, ISheetEditGuard guard,
    PulseOffice.UI.Controls.Services.IStatusBarService status, PulseOffice.Localization.ITextCatalog texts) : ILedgerChangeResolution
{
    public int Resolve(IReadOnlyList<string> ids, bool accept, string description)
    {
        ArgumentNullException.ThrowIfNull(ids);
        if (context.Workbook is not { } workbook || context.Engine is not { } engine) return 0;
        if (workbook.Changes.IsProtected)
        {
            status.Report(texts["review.protected"], PulseOffice.UI.Controls.Notifications.NotificationSeverity.Error);
            return 0;
        }

        var wanted = ids.ToHashSet(StringComparer.Ordinal);
        var candidates = workbook.Changes.Changes.Where(change => change.State == ChangeState.Pending && wanted.Contains(change.Id)).ToList();
        var pending = accept ? candidates : candidates.Where(change => Writable(workbook, change)).ToList();
        if (pending.Count < candidates.Count)
            status.Report(texts["status.protection.locked"], PulseOffice.UI.Controls.Notifications.NotificationSeverity.Error, LedgerRemedies.UnprotectSheet);
        if (pending.Count == 0) return 0;
        var statesBefore = workbook.Changes.Changes.ToDictionary(change => change.Id, change => change.State);
        var before = checkpoints.Capture(workbook);
        foreach (var change in Enumerable.Reverse(pending))
        {
            if (accept) workbook.Changes.Resolve(change.Id, ChangeState.Accepted);
            else Reject(workbook, engine, change);
        }
        var after = checkpoints.Capture(workbook);
        var statesAfter = workbook.Changes.Changes.ToDictionary(change => change.Id, change => change.State);
        context.History?.Push(new PulseOffice.Document.Commands.DelegateAction(description,
            redo: () =>
            {
                checkpoints.Restore(engine, workbook, after);
                Apply(workbook, statesAfter);
            },
            undo: () =>
            {
                checkpoints.Restore(engine, workbook, before);
                Apply(workbook, statesBefore);
            }));
        context.RequestRefresh();
        return pending.Count;
    }

    private bool Writable(Workbook workbook, TrackedChange change) =>
        change.SheetIndex >= 0 && change.SheetIndex < workbook.Sheets.Count
        && guard.CanWrite(workbook, workbook.Sheets[change.SheetIndex], Addressing.CellRange.Single(change.Address));

    private void Apply(Workbook workbook, Dictionary<string, ChangeState> states)
    {
        foreach (var (id, state) in states) workbook.Changes.Resolve(id, state);
    }

    private void Reject(Workbook workbook, Engine.Recalculation.CalculationEngine engine, TrackedChange change)
    {
        var target = new SheetCell(change.SheetIndex, change.Address);
        var recording = workbook.Changes.IsRecording;
        workbook.Changes.IsRecording = false;
        try
        {
            if (change.FormulaBefore is { Length: > 0 } formula) engine.SetFormula(target, formula);
            else engine.SetValue(target, change.Before);
        }
        finally
        {
            workbook.Changes.IsRecording = recording;
        }
        workbook.Changes.Resolve(change.Id, ChangeState.Rejected);
    }
}
