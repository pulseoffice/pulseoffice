using PulseOffice.UI.Controls.Notifications;
using PulseOffice.Document.Commands;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerDatabaseRangeCommands(
    IActiveSheetContext context,
    ISheetRegions regions,
    IQueryRefreshRunner queryRefresh,
    IDialogService dialogs,
    IStatusBarService status,
    Dialogs.ISheetReferenceSources sources,
    Dialogs.ISheetReferenceReader reader,
    ITextCatalog texts,
    IDefinedNameRules names) : LedgerCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Command("ledger.database.define", ct => DefineAsync(ct)),
        Command("ledger.database.select", ct => SelectAsync(ct)),
        Command("ledger.database.refresh", ct => queryRefresh.RefreshAsync(ct)) with { RunsDocumentQueries = true },
    ];

    private CommandDefinition Command(string id, Func<CancellationToken, Task> run) =>
        Prototype(id) with
        {
            MenuPath = "data/ranges",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = id.EndsWith("select", StringComparison.Ordinal) ? DocumentState.None : DocumentState.ReadOnly,
            EnabledWhen = _ => Context.HasTarget
                && (!id.EndsWith("refresh", StringComparison.Ordinal) || queryRefresh.CanRefresh(Context.Workbook)),
            Execute = (_, ct) => run(ct),
        };

    private async Task DefineAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;

        var sheet = workbook.Sheets[Context.SheetIndex];

        var area = Context.Selection.IsSingleCell
            ? regions.CurrentRegion(sheet, Context.Selection.Start)
            : Context.Selection;

        var form = new FormPromptView(
            texts["database.define.explanation"],
            [
                new TextField("name", texts["database.name"], string.Empty),
                new RangeField("area", texts["database.range"], area.ToString(), sources.For(Dialogs.ReferenceShape.Range)),
                new CheckField("header", texts["database.hasHeader"], true),
            ],
            texts);

        var values = await dialogs.ShowAsync<Dictionary<string, object>>(
            form, new DialogOptions { TitleKey = "command.ledger.database.define.title" }, ct)
            .ConfigureAwait(true);

        if (values is null) return;
        var name = (values["name"] as string ?? string.Empty).Trim();
        if (name.Length == 0) return;
        if (!names.IsValid(name))
        {
            status.Report(texts.Format("names.invalid", name), NotificationSeverity.Error);
            return;
        }
        if (reader.Read(workbook, values["area"] as string ?? string.Empty, Context.SheetIndex, Dialogs.ReferenceShape.Range)
            is not [{ Range: var range, SheetIndex: var areaSheet }])
        {
            status.Report(texts["status.analysis.badReference"], NotificationSeverity.Error);
            return;
        }
        var previous = workbook.DatabaseRanges.Find(name);

        var hasHeader = values["header"] is true;
        var definition = previous is null
            ? new DatabaseRange { Name = name, SheetIndex = areaSheet, Range = range, HasHeader = hasHeader }
            : Redefined(previous, areaSheet, range, hasHeader);

        workbook.DatabaseRanges.Define(definition);

        Context.History?.Push(new DelegateAction(
            texts["command.ledger.database.define.title"],
            redo: () => workbook.DatabaseRanges.Define(definition),
            undo: () =>
            {
                workbook.DatabaseRanges.Remove(name);
                if (previous is not null) workbook.DatabaseRanges.Define(previous);
            }));

        status.Report(texts.Format("database.defined", name));
    }

    private DatabaseRange Redefined(DatabaseRange previous, int sheetIndex, Addressing.CellRange range, bool hasHeader)
    {
        var sameColumns = previous.SheetIndex == sheetIndex && previous.Range.Start.Column == range.Start.Column && previous.Range.End.Column == range.End.Column
            && previous.HasHeader == hasHeader;
        return previous with
        {
            SheetIndex = sheetIndex,
            Range = range,
            HasHeader = hasHeader,
            HasTotalsRow = previous.HasTotalsRow && range.RowCount > (hasHeader ? 1 : 0),
            Filters = sameColumns ? previous.Filters : [],
            RangeFilter = sameColumns ? previous.RangeFilter : null,
        };
    }

    private async Task SelectAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;

        var ranges = workbook.DatabaseRanges.All.ToList();

        if (ranges.Count == 0)
        {
            status.Report(texts["database.none"], NotificationSeverity.Warning);
            return;
        }

        var form = new FormPromptView(
            texts["database.select.explanation"],
            [new ChoiceField("area", texts["database.name"], [.. ranges.Select(range => range.Name)])],
            texts);

        var values = await dialogs.ShowAsync<Dictionary<string, object>>(
            form, new DialogOptions { TitleKey = "command.ledger.database.select.title" }, ct)
            .ConfigureAwait(true);

        if (values is null) return;

        var index = (int)Convert.ToInt64(values["area"], System.Globalization.CultureInfo.InvariantCulture);
        var chosen = ranges[Math.Clamp(index, 0, ranges.Count - 1)];

        Context.SelectSheet(chosen.SheetIndex);
        Context.SelectRange(chosen.Range);
    }
}
