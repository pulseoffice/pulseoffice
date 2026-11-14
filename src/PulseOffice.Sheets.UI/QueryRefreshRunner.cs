using PulseOffice.Document.Commands;
using PulseOffice.Localization;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI;

public interface IQueryRefreshRunner
{
    bool CanRefresh(Workbook? workbook);

    Task RefreshAsync(CancellationToken ct);
}

public sealed class QueryRefreshRunner(
    IActiveSheetContext context,
    Queries.IQueryRefresher queries,
    IWorkbookCheckpoints checkpoints,
    IStatusBarService status,
    ITextCatalog texts) : IQueryRefreshRunner
{
    public bool CanRefresh(Workbook? workbook) =>
        workbook is not null && workbook.Sheets.Any(sheet => sheet.QueryBindings.Count > 0);

    public async Task RefreshAsync(CancellationToken ct)
    {
        if (context.Workbook is not { } workbook || !CanRefresh(workbook)) return;

        if (Locked(workbook)) return;

        status.Report(texts["status.query.refreshing"]);

        var engine = context.Engine;
        var history = context.History;
        Queries.QueryFetch fetch;
        try
        {
            fetch = await queries.FetchAllAsync(workbook, ct).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            status.Report(texts["status.query.cancelled"]);
            return;
        }

        if (Locked(workbook)) return;

        var before = engine is not null && history is not null ? checkpoints.Capture(workbook) : null;

        var outcome = queries.Apply(workbook, fetch);

        if (before is not null && engine is not null && history is not null && outcome.Value > 0)
        {
            var after = checkpoints.Capture(workbook);
            history.Push(new DelegateAction(texts["history.query.refresh"],
                redo: () => checkpoints.Restore(engine, workbook, after),
                undo: () => checkpoints.Restore(engine, workbook, before)));
        }

        context.RequestRefresh();

        if (outcome.Diagnostics.Any(diagnostic => diagnostic.Code == Queries.QueryRefresher.DeclinedCode))
            status.Report(texts["status.query.declined"]);
        else status.Report(outcome.HasErrors
            ? texts.Format("status.query.failed", outcome.Diagnostics.Count)
            : texts.Format("status.query.done", outcome.Value),
            outcome.HasErrors ? NotificationSeverity.Error : NotificationSeverity.Success);
    }

    private bool Locked(Workbook workbook)
    {
        if (!workbook.Sheets.Any(sheet => sheet.QueryBindings.Count > 0 && sheet.Protection.IsEnabled)) return false;

        status.Report(texts["status.protection.locked"], NotificationSeverity.Error, LedgerRemedies.UnprotectSheet);
        return true;
    }
}
