using PulseOffice.UI.Controls.Notifications;
using PulseOffice.Data.UI;
using PulseOffice.Document.Commands;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Queries;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerExternalDataCommands(
    IActiveSheetContext context,
    IDataImportFlow imports,
    ISheetSnapshots snapshots,
    IQueryRefresher queries,
    IStatusBarService status,
    ITextCatalog texts,
    Data.IExternalQueryGate documentQueries) : LedgerCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Prototype("data.connections") with
        {
            IconKey = "data.connections",
            MenuPath = "data/external",
            ContextPaths = ["cell/data"],
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen,
            Execute = (_, ct) => imports.ManageConnectionsAsync(ct),
        },

        Prototype("data.import") with
        {
            IconKey = "data.import",
            MenuPath = "data/external",
            ContextPaths = ["cell/data"],
            Surface = CommandSurface.Common | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            Execute = (_, ct) => ImportAsync(ct),
        },
    ];

    private async Task ImportAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook || Context.Engine is not { } engine) return;

        var history = Context.History;
        var sheetIndex = Context.SheetIndex;
        var sheet = workbook.Sheets[sheetIndex];
        var anchor = Context.Selection.Start;

        var choice = await imports.ChooseAsync(ct).ConfigureAwait(true);
        if (choice is null) return;

        var binding = new QueryBinding
        {
            Id = Guid.NewGuid().ToString("N"),
            DisplayName = choice.DisplayName,
            ConnectionId = choice.Query.ConnectionId,
            Query = new Data.QueryDefinition
            {
                Sql = choice.Query.Sql,
                MaxRows = choice.Query.MaxRows,
            },
            Anchor = anchor,
            IncludeHeaders = choice.IncludeHeaders,
        };

        documentQueries.Grant(workbook, new Data.ExternalQueryRequest(binding.ConnectionId, binding.Query.Sql));

        var before = snapshots.Capture(sheet);

        status.Report(texts["status.query.refreshing"]);

        var outcome = await queries.RefreshAsync(workbook, sheet, binding, ct).ConfigureAwait(true);

        if (!outcome.Succeeded || outcome.Value is not { } refreshed)
        {
            status.Report(texts.Format("status.query.failed", outcome.Diagnostics.Count), NotificationSeverity.Error);
            return;
        }

        sheet.QueryBindings.AddOrUpdate(refreshed);

        var after = snapshots.Capture(sheet);

        history?.Push(new DelegateAction(
            texts["history.data.import"],
            redo: () =>
            {
                snapshots.Restore(engine, workbook.IndexOf(sheet), after);
                sheet.QueryBindings.AddOrUpdate(refreshed);
            },
            undo: () =>
            {
                snapshots.Restore(engine, workbook.IndexOf(sheet), before);
                sheet.QueryBindings.Remove(refreshed.Id);
            }));

        Context.RequestRefresh();
        status.Report(texts.Format("status.query.done", 1), NotificationSeverity.Success);
    }
}
