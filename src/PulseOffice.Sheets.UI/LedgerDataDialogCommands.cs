using PulseOffice.UI.Controls.Notifications;
using PulseOffice.Document.Commands;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerDataDialogCommands(
    IActiveSheetContext context,
    ISheetConsolidation consolidation,
    IMultipleOperations operations,
    ISheetFilter filters,
    ISheetValueWrites writes,
    Dialogs.ISheetReferenceReader reader,
    Dialogs.ISheetReferenceSources sources,
    IDialogService dialogs,
    IStatusBarService status,
    ITextCatalog texts,
    DataDialogs.LedgerDialogKit kit,
    DataDialogs.IAdvancedFilterProbes probes,
    DataDialogs.ILedgerDialogProbes dialogProbes,
    ISheetRangeReferences references,
    ISheetRangeFilters rangeFilters) : LedgerCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Command("ledger.data.consolidate", "data/tools", ct => ConsolidateAsync(ct)),
        Command("ledger.data.multipleOperations", "data/tools", ct => MultipleAsync(ct)),
        Command("ledger.data.advancedFilter", "data/filter", ct => AdvancedAsync(ct)),
    ];

    private CommandDefinition Command(string id, string menuPath, Func<CancellationToken, Task> run) =>
        Prototype(id) with
        {
            MenuPath = menuPath,
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.HasTarget,
            Execute = (_, ct) => run(ct),
        };

    private async Task ConsolidateAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;
        if (Context.Engine is not { } engine) return;

        var sheetIndex = Context.SheetIndex;
        var remembered = workbook.Consolidation is { Sources.Count: > 0 } last && last.TargetSheet == sheetIndex ? last : null;
        IReadOnlyList<string> initialSources = remembered is null
            ? [Context.Selection.ToString()]
            : [.. remembered.Sources.Select(area => references.Qualify(workbook, area.SheetIndex, area.Range.ToString(), sheetIndex))];
        var initialTarget = remembered?.Target.ToA1() ?? Context.Selection.Start.ToA1();
        var labels = remembered is null ? null
            : new DataDialogs.ConsolidateLabels((SubtotalFunction)(int)remembered.Function, remembered.TopRowLabels, remembered.LeftColumnLabels)
            {
                LinkToSource = remembered.LinkToSource,
            };
        var choice = await dialogs.ShowAsync<DataDialogs.ConsolidateChoice>(
            new DataDialogs.ConsolidateView(kit, dialogProbes.Consolidation(workbook, sheetIndex), initialSources,
                initialTarget, sources.Picker(Dialogs.ReferenceShape.Range), sources.For(Dialogs.ReferenceShape.Cell), labels),
            new DialogOptions { TitleKey = "command.ledger.data.consolidate.title" }, ct)
            .ConfigureAwait(true);

        if (choice is null) return;

        var areas = choice.Sources
            .Select(text => reader.Read(workbook, text, sheetIndex, Dialogs.ReferenceShape.Range)?[0])
            .OfType<SheetRangeReference>()
            .Select(reference => new ConsolidationSource(reference.SheetIndex, reference.Range))
            .ToList();

        if (areas.Count == 0)
        {
            status.Report(texts["consolidate.noSources"], NotificationSeverity.Error);
            return;
        }

        if (reader.Read(workbook, choice.Destination, sheetIndex, Dialogs.ReferenceShape.Cell)
            is not [{ SheetIndex: var destinationSheet } destination] || destinationSheet != sheetIndex)
        {
            status.Report(texts["status.analysis.badReference"], NotificationSeverity.Error);
            return;
        }

        var plan = consolidation.Plan(workbook, new ConsolidationRequest(areas, destination.Range.Start)
        {
            Function = choice.Function,
            LabelsInFirstRow = choice.RowLabels,
            LabelsInFirstColumn = choice.ColumnLabels,
            LinkToSource = choice.LinkToSource,
        });

        var settings = new ConsolidationSettings
        {
            Sources = [.. areas.Select(area => new ConsolidationArea(area.SheetIndex, area.Range))],
            TargetSheet = sheetIndex,
            Target = destination.Range.Start,
            Function = (ConsolidationFunction)(int)choice.Function,
            TopRowLabels = choice.RowLabels,
            LeftColumnLabels = choice.ColumnLabels,
            LinkToSource = choice.LinkToSource,
        };
        var title = texts["command.ledger.data.consolidate.title"];
        using (Context.History?.BeginTransaction(title))
        {
            Write(engine, plan, title);
            Remember(workbook, settings, title);
        }
    }

    private void Write(Engine.Recalculation.CalculationEngine engine, ConsolidationPlan plan, string title)
    {
        var values = plan.Values.Where(pair => !plan.Formulas.ContainsKey(pair.Key)).Select(pair => (pair.Key, pair.Value)).ToList();
        var formulas = plan.Formulas.Select(pair => (pair.Key, pair.Value)).ToList();
        if (values.Count + formulas.Count == 0) return;

        writes.Write(Context, engine, values, formulas, title);
        status.Report(texts.Format("status.cells.written", values.Count + formulas.Count));
    }

    private void Remember(Workbook workbook, ConsolidationSettings settings, string title)
    {
        var previous = workbook.Consolidation;
        if (settings.Equivalent(previous)) return;
        workbook.Consolidation = settings;
        Context.History?.Push(new DelegateAction(title, redo: () => workbook.Consolidation = settings, undo: () => workbook.Consolidation = previous));
    }

    private async Task MultipleAsync(CancellationToken ct)
    {
        if (Context.Engine is not { } engine) return;

        var previewer = dialogProbes.MultipleOperations(engine, Context.SheetIndex, Context.Selection);
        var choice = await dialogs.ShowAsync<DataDialogs.MultipleOperationsChoice>(
            new DataDialogs.MultipleOperationsView(kit, previewer, sources.For(Dialogs.ReferenceShape.RangeList), sources.For(Dialogs.ReferenceShape.Cell)),
            new DialogOptions { TitleKey = "command.ledger.data.multipleOperations.title" }, ct)
            .ConfigureAwait(true);

        if (choice is null) return;

        if (previewer.Request(choice) is not { IsValid: true } request)
        {
            status.Report(texts["operations.invalid"], NotificationSeverity.Error);
            return;
        }

        var result = operations.Compute(engine, Context.SheetIndex, request);

        Write(engine, result.Values, "command.ledger.data.multipleOperations.title");
    }

    private async Task AdvancedAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;

        var (initial, notice) = Reloaded(workbook, Context.SheetIndex, rangeFilters.Stored(workbook, Context.SheetIndex, Context.Selection));
        var choice = await dialogs.ShowAsync<DataDialogs.AdvancedFilterChoice>(
            new DataDialogs.AdvancedFilterView(kit, probes.For(workbook, Context.SheetIndex, Context.Engine is { } engine ? new EngineShiftedFormulas(engine) : null), initial, notice is null ? null : texts[notice], sources.For(Dialogs.ReferenceShape.Range)),
            new DialogOptions { TitleKey = "command.ledger.data.advancedFilter.title" }, ct)
            .ConfigureAwait(true);

        if (choice is null) return;

        var values = new Dictionary<string, object>
        {
            ["data"] = choice.Data,
            ["criteria"] = choice.Criteria,
            ["unique"] = choice.Unique,
            ["output"] = choice.Output,
        };

        var sheetIndex = Context.SheetIndex;
        var sheet = workbook.Sheets[sheetIndex];

        var data = references.Parse(workbook, values["data"] as string ?? string.Empty, sheetIndex);
        if (data is null || data.SheetIndex != sheetIndex)
        {
            status.Report(texts["status.filter.invalidData"], NotificationSeverity.Error);
            return;
        }

        var criteria = references.Parse(workbook, values["criteria"] as string ?? string.Empty, sheetIndex);
        if (criteria is null)
        {
            status.Report(texts["status.filter.invalidCriteria"], NotificationSeverity.Error);
            return;
        }

        var request = new AdvancedFilterRequest(data.Range, criteria.Range)
        {
            UniqueOnly = values["unique"] is true,
            CriteriaSheet = workbook.Sheets[criteria.SheetIndex],
        };

        var outputText = values["output"] as string ?? string.Empty;

        if (outputText.Trim().Length > 0)
        {
            var output = references.Parse(workbook, outputText, sheetIndex);
            if (output is null)
            {
                status.Report(texts["status.filter.invalidOutput"], NotificationSeverity.Error);
                return;
            }

            CopyResults(workbook, sheet, request, output);
            return;
        }

        if (sheet.Protection is { IsEnabled: true, AllowFilter: false })
        {
            status.Report(texts["status.protection.filter"], NotificationSeverity.Error, LedgerRemedies.UnprotectSheet);
            return;
        }

        var hidden = rangeFilters.Store(Context, sheet, data.Range, request.HasHeader, new RangeFilter
        {
            Criteria = new AdvancedCriteria(criteria.SheetIndex, criteria.Range),
            UniqueOnly = request.UniqueOnly,
        }, texts["command.ledger.data.advancedFilter.title"]);

        status.Report(texts.Format("status.filter.rows", hidden));
    }

    private (DataDialogs.AdvancedFilterChoice Choice, string? Notice) Reloaded(Workbook workbook, int sheetIndex, SheetRangeFilter? stored)
    {
        var data = stored?.Range ?? Context.Selection;
        if (stored?.Filter is not { } filter)
            return (new DataDialogs.AdvancedFilterChoice(data.ToString(), string.Empty, false, string.Empty), null);

        var criteria = filter.Criteria is { } source
            ? references.Qualify(workbook, source.SheetIndex, source.Range.ToString(), sheetIndex)
            : string.Empty;
        return (new DataDialogs.AdvancedFilterChoice(data.ToString(), criteria, filter.UniqueOnly, string.Empty),
            filter.Criteria is null && filter.Condition is not null ? "advancedFilter.replacesStandard" : null);
    }

    private void CopyResults(Workbook workbook, Worksheet source, AdvancedFilterRequest request, SheetRangeReference output)
    {
        var target = workbook.Sheets[output.SheetIndex];
        var cells = filters.CopyMatches(source, request with { Formulas = Context.Engine is { } engine ? new EngineShiftedFormulas(engine) : null }, output.Range.Start);

        if (target.Protection.IsEnabled
            && cells.Any(pair => workbook.Styles.Get(target.StyleIdAt(pair.Address)).Locked))
        {
            status.Report(texts["status.filter.protected"], NotificationSeverity.Error);
            return;
        }

        using var transaction = Context.History?.BeginTransaction(texts["command.ledger.data.advancedFilter.title"]);

        foreach (var (address, cell) in cells)
        {
            var previous = target[address];
            target[address] = cell;
            Context.History?.Push(new DelegateAction(
                texts["command.ledger.data.advancedFilter.title"],
                redo: () => target[address] = cell,
                undo: () => target[address] = previous));
        }

        Context.RequestRefresh();
        status.Report(texts.Format("status.filter.copied", Math.Max(0, cells.Count / Math.Max(1, request.Data.ColumnCount) - 1)));
    }

    private void Write(
        Engine.Recalculation.CalculationEngine engine,
        IReadOnlyDictionary<CellAddress, Values.CellValue> values,
        string titleKey)
    {
        if (values.Count == 0) return;

        writes.Write(Context, engine, [.. values.Select(pair => (pair.Key, pair.Value))], texts[titleKey]);
        status.Report(texts.Format("status.cells.written", values.Count));
    }
}
