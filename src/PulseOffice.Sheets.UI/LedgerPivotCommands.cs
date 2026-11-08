using PulseOffice.UI.Controls.Notifications;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Pivot;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerPivotCommands(
    IActiveSheetContext context,
    IPivotCoordinator pivots,
    IDialogService dialogs,
    IStatusBarService status,
    ITextCatalog texts,
    PulseOffice.Sheets.Engine.Operations.IWorkbookCheckpoints checkpoints,
    DataDialogs.LedgerDialogKit kit,
    IPivotEngine pivotEngine,
    IWorksheetTabPolicy tabs,
    Queries.IPivotSourceRefresher? externalSources = null) : LedgerCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Prototype("ledger.data.pivotCreate") with
        {
            MenuPath = "data/pivot",
            Surface = CommandSurface.Common,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            RequiredSelection = SelectionKind.CellRange | SelectionKind.Cell,
            EnabledWhen = _ => Context.HasTarget,
            Execute = (_, ct) => CreateAsync(ct),
        },

        Prototype("ledger.data.pivotRefresh") with
        {
            MenuPath = "data/pivot",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Workbook?.Pivots.Count > 0,
            RunsDocumentQueries = true,
            Execute = (_, ct) => RefreshAsync(ct),
        },
    ];

    private async Task RefreshAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;
        if (Locked(workbook)) return;

        var fetch = externalSources?.HasExternal(workbook) == true
            ? await externalSources.FetchAsync(workbook, ct).ConfigureAwait(true)
            : null;
        if (Locked(workbook)) return;

        var result = Checkpointed(checkpoints, texts["command.ledger.data.pivotRefresh.title"], () =>
        {
            if (fetch is not null) externalSources!.Apply(workbook, fetch);
            return pivots.RefreshAll(workbook, Labels());
        }, outcome => outcome.Succeeded);

        var problem = result.Succeeded ? fetch?.Failures.FirstOrDefault() : result.Diagnostics[0];
        if (problem is not null)
            status.Report(Describe(problem), result.Succeeded ? NotificationSeverity.Warning : NotificationSeverity.Error);
        else if (fetch is { Refreshed: > 0 } read)
            status.Report(texts.Format("status.pivot.refreshedExternal", result.Value, read.Refreshed), NotificationSeverity.Success);
        else
            status.Report(texts.Format("status.pivot.refreshed", result.Value));

        Context.RequestRefresh();
    }

    private bool Locked(Workbook workbook)
    {
        if (!workbook.Pivots.Any(pivot => workbook.FindSheet(pivot.DestinationSheet)?.Protection.IsEnabled == true)) return false;
        status.Report(texts["status.protection.locked"], NotificationSeverity.Error, LedgerRemedies.UnprotectSheet);
        return true;
    }

    private PivotLabels Labels() => new(texts["pivot.grandTotal"], texts["pivot.subtotal"]);

    private string Describe(PulseOffice.Foundation.Diagnostics.Diagnostic diagnostic) =>
        texts.Format($"pivot.diagnostic.{diagnostic.Code}", diagnostic.Location ?? string.Empty);

    private async Task CreateAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;
        if (RefuseStructureChange(status, texts)) return;

        var sheet = workbook.Sheets[Context.SheetIndex];
        var range = SourceRange(sheet);

        if (range is null)
        {
            status.Report(texts["status.pivot.noData"], NotificationSeverity.Warning);
            return;
        }

        var source = new RangePivotSource(sheet, range.Value);

        var choice = await dialogs.ShowAsync<PivotChoice>(
            new PivotPromptView(source.Headers, UniqueSheetName(workbook), kit, new SourcePivotPreviewer(pivotEngine, source, new PivotLabels(kit["pivot.grandTotal"], kit["pivot.subtotal"]))),
            new DialogOptions { TitleKey = "dialog.pivot.title" }, ct)
            .ConfigureAwait(true);

        if (choice is null || !ReferenceEquals(Context.Workbook, workbook) || workbook.IndexOf(sheet) < 0) return;
        if (RefuseStructureChange(status, texts)) return;

        if (!tabs.NameAllowed(choice.DestinationSheet))
        {
            status.Report(texts.Format("status.pivot.badSheetName", choice.DestinationSheet), NotificationSeverity.Error);
            return;
        }

        var definition = new PivotDefinition
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = choice.DestinationSheet,
            SourceSheet = sheet.Name,
            SourceRange = range.Value,
            DestinationSheet = choice.DestinationSheet,
            Destination = CellAddress.First,
            Fields = choice.Fields,
        };

        var result = Checkpointed(checkpoints, texts["command.ledger.data.pivotCreate.title"], () => pivots.Apply(workbook, definition, Labels()), outcome => outcome.Succeeded);

        status.Report(result.Succeeded
            ? texts.Format("status.pivot.created", choice.DestinationSheet)
            : Describe(result.Diagnostics[0]), result.Succeeded ? NotificationSeverity.Success : NotificationSeverity.Error);

        Context.RequestRefresh();
    }

    private CellRange? SourceRange(Worksheet sheet)
    {
        var selection = Context.Selection;

        return selection.Start == selection.End ? sheet.UsedRange() : BoundedToUsed(sheet, selection);
    }

    private static string UniqueSheetName(Workbook workbook)
    {
        var index = 1;

        while (workbook.FindSheet($"Pivot{index}") is not null) index++;

        return $"Pivot{index}";
    }
}
