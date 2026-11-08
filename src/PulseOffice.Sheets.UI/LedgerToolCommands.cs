using PulseOffice.UI.Controls.Notifications;
using System.Globalization;
using PulseOffice.Document.Commands;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerToolCommands(
    IActiveSheetContext context,
    ISheetSnapshots snapshots,
    ISheetSubtotals subtotals,
    ITextToColumns textToColumns,
    IDialogService dialogs,
    IStatusBarService status,
    ISheetEditGuard guard,
    ISheetSortGate sortGate,
    Dialogs.IValidationRules validationRules,
    IValidationPlacement validationPlacement,
    Dialogs.ISheetReferenceSources references,
    Dialogs.ISheetReferenceReader reader,
    ITextCatalog texts,
    DataDialogs.LedgerSortServices sorting,
    DataDialogs.LedgerDialogKit kit,
    DataDialogs.ILedgerDialogProbes probes,
    ISheetRangeFilters rangeFilters) : LedgerCommandSet(context)
{
    private static readonly SubtotalFunction[] Functions =
    [
        SubtotalFunction.Sum, SubtotalFunction.Count, SubtotalFunction.Average,
        SubtotalFunction.Max, SubtotalFunction.Min, SubtotalFunction.Product, SubtotalFunction.CountNumbers,
        SubtotalFunction.StandardDeviation, SubtotalFunction.StandardDeviationPopulation,
        SubtotalFunction.Variance, SubtotalFunction.VariancePopulation,
    ];

    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Tool("ledger.data.sort", "data", ct => SortAsync(ct)),
        Tool("ledger.data.subtotals", "data/tools", ct => SubtotalsAsync(ct)),
        Tool("ledger.data.textToColumns", "data/tools", ct => TextToColumnsAsync(ct)),
        Tool("ledger.data.validation", "data/tools", ct => ValidationAsync(ct)),
        Tool("ledger.data.statistics", "data/analysis", ct => StatisticsAsync(ct)),
    ];

    private CommandDefinition Tool(string id, string path, Func<CancellationToken, Task> run) =>
        Prototype(id) with
        {
            MenuPath = path,
            ContextPaths = id.EndsWith("validation", StringComparison.Ordinal) ? ["cell/notes"] : [],
            Surface = id.EndsWith("validation", StringComparison.Ordinal)
                ? CommandSurface.Advanced | CommandSurface.ContextMenu
                : CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = id.EndsWith("statistics", StringComparison.Ordinal) ? DocumentState.None : DocumentState.ReadOnly,
            RequiredSelection = SelectionKind.Cell | SelectionKind.CellRange,
            EnabledWhen = _ => Context.HasTarget,
            Execute = (_, ct) => run(ct),
        };

    private async Task SortAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;

        var sheet = workbook.Sheets[Context.SheetIndex];
        var range = Context.Selection.IsSingleCell ? sorting.Regions.CurrentRegion(sheet, Context.Selection.Start) : Context.Selection;
        if (BoundedToUsed(sheet, range) is not { RowCount: >= 2 } bounded)
        {
            status.Report(texts["status.tools.nothing"], NotificationSeverity.Warning);
            return;
        }

        if (sortGate.Refuses(workbook, sheet, bounded)) return;

        var remembered = sorting.Memory.Recall(workbook, Context.SheetIndex, bounded);
        var orders = sorting.Orders.Including(remembered?.Fields.Select(field => field.CustomOrder).OfType<IReadOnlyList<string>>() ?? []);
        var headers = Headers(sheet, bounded);
        var letters = Enumerable.Range(bounded.Start.Column, bounded.ColumnCount)
            .Select(column => texts.Format("sort.levels.columnLetter", CellAddress.ColumnName(column)))
            .ToList();
        var rowLetters = Enumerable.Range(bounded.Start.Row, bounded.RowCount)
            .Select(row => texts.Format("sort.levels.rowNumber", row + 1))
            .ToList();
        var leftToRight = remembered?.LeftToRight == true;
        var start = leftToRight
            ? Math.Clamp(Context.Selection.Start.Row - bounded.Start.Row, 0, bounded.RowCount - 1)
            : Math.Clamp(Context.Selection.Start.Column - bounded.Start.Column, 0, bounded.ColumnCount - 1);

        var choice = await sorting.Dialog.AskAsync(new PulseOffice.UI.Controls.Sorting.SortLevelsRequest(
            "command.ledger.data.sort.title", headers, letters)
        {
            HasHeader = sorting.Headers.LooksLikeHeader(sheet, bounded),
            CustomOrders = [.. orders.Select(order => order.Label)],
            CaseSensitive = remembered?.CaseSensitive == true,
            RowHeaders = RowHeaders(sheet, bounded),
            RowLetters = rowLetters,
            LeftToRight = leftToRight,
            Initial = remembered is { } stored
                ? [.. stored.Fields.Select(field => new PulseOffice.UI.Controls.Sorting.SortLevel(field.ColumnOffset, !field.Ascending, CustomOrderIndex(orders, field.CustomOrder)))]
                : [new PulseOffice.UI.Controls.Sorting.SortLevel(start)],
            ExplanationKey = "tools.sort.explanation",
        }, ct).ConfigureAwait(true);

        if (choice is null) return;

        var keys = choice.Levels
            .Select(level => new Engine.Operations.SortKey(level.Column, !level.Descending)
            {
                CaseSensitive = choice.CaseSensitive,
                CustomOrder = level.CustomOrder >= 0 && level.CustomOrder < orders.Count ? orders[level.CustomOrder].Items : null,
            })
            .ToList();

        using (Context.History?.BeginTransaction(texts["history.ledger.sort"]))
        {
            var orientation = choice.LeftToRight ? SortOrientation.LeftToRight : SortOrientation.TopToBottom;
            sorting.Sorter.Sort(sheet, bounded, keys, choice.HasHeader, Context.History, texts["history.ledger.sort"], orientation);
            sorting.Memory.Remember(workbook, Context.SheetIndex, bounded, keys, Context.History, texts["history.ledger.sort"], choice.HasHeader, orientation);
        }
        if (sheet.AutoFilter.Range is { } filtered && filtered.Contains(bounded.Start)) rangeFilters.Refresh(Context, sheet);
        Context.RequestRefresh();
        var lines = choice.LeftToRight ? bounded.ColumnCount : bounded.RowCount;
        status.Report(texts.Format("status.tools.sorted", lines - (choice.HasHeader ? 1 : 0), keys.Count), NotificationSeverity.Success);
    }

    private IReadOnlyList<string> RowHeaders(Worksheet sheet, CellRange range) =>
        [.. Enumerable.Range(range.Start.Row, range.RowCount).Select(row =>
            sheet.GetValue(new CellAddress(row, range.Start.Column)) is { IsEmpty: false } value
                ? value.ToString()
                : texts.Format("sort.levels.rowNumber", row + 1))];

    private int CustomOrderIndex(IReadOnlyList<DataDialogs.CustomSortOrder> orders, IReadOnlyList<string>? items)
    {
        if (items is not { Count: > 0 }) return -1;
        for (var index = 0; index < orders.Count; index++)
            if (orders[index].Items.SequenceEqual(items, StringComparer.OrdinalIgnoreCase)) return index;
        return -1;
    }

    private async Task SubtotalsAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook || Context.Engine is not { } engine) return;

        var sheet = workbook.Sheets[Context.SheetIndex];
        if (Protected(sheet)) return;
        var selected = Context.Selection.IsSingleCell ? sorting.Regions.CurrentRegion(sheet, Context.Selection.Start) : Context.Selection;
        if (BoundedToUsed(sheet, selected) is not { } range)
        {
            status.Report(texts["status.tools.nothing"], NotificationSeverity.Warning);
            return;
        }

        var headers = Headers(sheet, range);

        var functionNames = Functions.Select(function => texts[$"subtotal.{Camel(function.ToString())}"]).ToList();
        var answers = await AskAsync("subtotals",
        [
            new ChoiceField("group", texts["tools.groupColumn"], headers),
            new Dialogs.SubtotalColumnsField("values", texts["tools.valueColumns"], headers, functionNames,
                [new Dialogs.SubtotalColumnChoice(headers.Count - 1, 0)]),
            new CheckField("header", texts["tools.hasHeader"], true),
            new CheckField("replace", texts["tools.subtotals.replace"], true),
            new CheckField("outline", texts["tools.subtotals.outline"], true),
            new CheckField("breaks", texts["tools.subtotals.pageBreaks"]),
            new CheckField("below", texts["tools.subtotals.summaryBelow"], true),
        ], ct).ConfigureAwait(true);

        if (answers is null) return;

        var values = answers["values"] as IReadOnlyList<Dialogs.SubtotalColumnChoice> ?? [];
        if (values.Count == 0)
        {
            status.Report(texts["tools.subtotals.noColumns"], NotificationSeverity.Warning);
            return;
        }

        var functions = values.ToDictionary(value => range.Start.Column + value.Offset, value => Functions[value.Function]);
        var distinct = functions.Values.Distinct().ToList();
        var request = new SubtotalRequest(
            range,
            GroupColumn: range.Start.Column + (int)answers["group"],
            ValueColumns: [.. functions.Keys])
        {
            Function = distinct[0],
            ColumnFunctions = functions,
            HasHeader = (bool)answers["header"],
            ReplaceCurrent = (bool)answers["replace"],
            Outline = (bool)answers["outline"],
            PageBreaks = (bool)answers["breaks"],
            SummaryBelow = (bool)answers["below"],
            FunctionLabel = distinct.Count == 1 ? texts[$"subtotal.{Camel(distinct[0].ToString())}"] : texts["tools.subtotals.result"],
            GrandTotalLabel = texts["pivot.grandTotal"],
        };

        if (subtotals.Plan(sheet, request).Rows.Count == 0)
        {
            status.Report(texts["status.tools.nothing"], NotificationSeverity.Warning);
            return;
        }

        var before = snapshots.Capture(sheet);
        var inserted = subtotals.Apply(engine, Context.SheetIndex, request).Inserted;

        Record(engine, sheet, before, texts["command.ledger.data.subtotals.title"]);
        status.Report(texts.Format("status.tools.subtotals", inserted), NotificationSeverity.Success);
    }

    private async Task TextToColumnsAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook || Context.Engine is not { } engine) return;

        var sheet = workbook.Sheets[Context.SheetIndex];
        if (!guard.CanWrite(workbook, sheet, Context.Selection))
        {
            status.Report(texts["status.protection.locked"], NotificationSeverity.Error, LedgerRemedies.UnprotectSheet);
            return;
        }

        var range = Context.Selection;
        var sample = Enumerable.Range(range.Start.Row, Math.Min(range.RowCount, 8))
            .Select(row => sheet.GetValue(new CellAddress(row, range.Start.Column)))
            .Where(value => value.Kind == ValueKind.Text)
            .Select(value => value.AsText())
            .ToList();

        var answers = await AskAsync("textToColumns",
            [new Dialogs.TextToColumnsField("options", sample, range, textToColumns)], ct, width: 640).ConfigureAwait(true);

        if (answers?.GetValueOrDefault("options") is not Dialogs.TextToColumnsChoice choice) return;

        if (choice.Separators().Count == 0)
        {
            status.Report(texts["textToColumns.noSeparator"], NotificationSeverity.Warning);
            return;
        }

        var plan = textToColumns.Plan(sheet, choice.Request(range));

        if (plan.Values.Count == 0)
        {
            status.Report(texts["status.tools.nothing"], NotificationSeverity.Warning);
            return;
        }

        var written = new CellRange(
            new CellAddress(plan.Values.Min(entry => entry.Key.Row), plan.Values.Min(entry => entry.Key.Column)),
            new CellAddress(plan.Values.Max(entry => entry.Key.Row), plan.Values.Max(entry => entry.Key.Column)));
        if (!guard.CanWrite(workbook, sheet, written))
        {
            status.Report(texts["status.protection.locked"], NotificationSeverity.Error, LedgerRemedies.UnprotectSheet);
            return;
        }

        var before = snapshots.Capture(sheet);

        foreach (var (address, value) in plan.Values)
            engine.SetValue(new SheetCell(Context.SheetIndex, address), value);

        Record(engine, sheet, before, texts["command.ledger.data.textToColumns.title"]);
        status.Report(texts.Format("status.tools.textToColumns", plan.WidestRow));
    }

    private async Task ValidationAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;

        var sheet = workbook.Sheets[Context.SheetIndex];
        if (Protected(sheet)) return;

        var range = Context.Selection;
        var existing = sheet.Validations.LastOrDefault(rule => rule.Range.Contains(range.Start));
        var initial = validationRules.From(existing);

        var criteria = new Dialogs.ValidationCriteriaEditor(
            new Dialogs.ValidationCriteriaField("criteria", initial, validationRules, references.For(Dialogs.ReferenceShape.Range), kit.Icons), texts, kit.Metrics);
        var help = new DataDialogs.ValidationHelpPane(kit, initial.InputTitle, initial.InputMessage, initial.ShowInputMessage);
        var error = new DataDialogs.ValidationErrorPane(kit, initial.Alert, initial.ErrorTitle, initial.ErrorMessage, initial.ShowErrorAlert);

        var view = new TabbedDialogView(
            [
                new DialogTab(texts["validation.tab.criteria"], () => criteria),
                new DialogTab(texts["validation.tab.help"], () => help),
                new DialogTab(texts["validation.tab.error"], () => error),
            ],
            () => criteria.Draft with
            {
                InputTitle = help.Title.Text ?? string.Empty,
                InputMessage = help.Message.Text ?? string.Empty,
                ShowInputMessage = help.ShowOnSelect.IsChecked == true,
                Alert = error.Chosen,
                ErrorTitle = error.Title.Text ?? string.Empty,
                ErrorMessage = error.Message.Text ?? string.Empty,
                ShowErrorAlert = error.ShowOnError.IsChecked == true,
            },
            texts);

        var draft = await dialogs.ShowAsync<Dialogs.ValidationDraft>(
            view, new DialogOptions { TitleKey = "dialog.validation.title", PreferredWidth = 720 }, ct).ConfigureAwait(true);

        if (draft is null) return;

        var rule = validationRules.Build(draft, range);

        List<DataValidation> before = [.. sheet.Validations];

        List<DataValidation> after = [.. validationPlacement.Place(before, range, rule)];
        sheet.Validations.Clear();
        sheet.Validations.AddRange(after);
        if (!before.SequenceEqual(after))
        {
            void Set(List<DataValidation> rules)
            {
                sheet.Validations.Clear();
                sheet.Validations.AddRange(rules);
                Context.RequestRefresh();
            }

            Context.History?.Push(new DelegateAction(
                texts["command.ledger.data.validation.title"], redo: () => Set(after), undo: () => Set(before)));
        }

        status.Report(texts[rule is null
            ? "status.tools.validationCleared"
            : "status.tools.validationSet"]);
    }

    private async Task StatisticsAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook || Context.Engine is not { } engine) return;

        var sheet = workbook.Sheets[Context.SheetIndex];

        var choice = await dialogs.ShowAsync<DataDialogs.StatisticsChoice>(
            new DataDialogs.StatisticsView(kit, probes.Statistics(workbook, Context.SheetIndex), Context.Selection.ToString(), "H1",
                references.For(Dialogs.ReferenceShape.Range), references.For(Dialogs.ReferenceShape.Cell)),
            new DialogOptions { TitleKey = "command.ledger.data.statistics.title" }, ct).ConfigureAwait(true);

        if (choice is null) return;

        if (reader.ReadLocal(workbook, choice.Input, Context.SheetIndex, Dialogs.ReferenceShape.Range) is not { } input
            || reader.ReadLocal(workbook, choice.Destination, Context.SheetIndex, Dialogs.ReferenceShape.Cell) is not { Start: var destination })
        {
            status.Report(texts["status.analysis.badReference"], NotificationSeverity.Error);
            return;
        }

        var table = probes.Statistics(workbook, Context.SheetIndex).Table(choice);

        if (table.Rows.Count == 0)
        {
            status.Report(texts["status.tools.nothing"], NotificationSeverity.Warning);
            return;
        }

        var width = table.Rows.Max(entry => entry.Values.Count) + 1;
        var output = new CellRange(destination, new CellAddress(
            Math.Min(destination.Row + table.Rows.Count - 1, CellAddress.MaxRows - 1),
            Math.Min(destination.Column + width - 1, CellAddress.MaxColumns - 1)));
        if (!guard.CanWrite(workbook, sheet, output))
        {
            status.Report(texts["status.protection.locked"], NotificationSeverity.Error, LedgerRemedies.UnprotectSheet);
            return;
        }

        var before = snapshots.Capture(sheet);

        for (var row = 0; row < table.Rows.Count; row++)
        {
            var entry = table.Rows[row];

            engine.SetValue(
                new SheetCell(Context.SheetIndex, new CellAddress(destination.Row + row, destination.Column)),
                CellValue.Text(texts[entry.LabelKey]));

            for (var column = 0; column < entry.Values.Count; column++)
                engine.SetValue(
                    new SheetCell(Context.SheetIndex,
                        new CellAddress(destination.Row + row, destination.Column + column + 1)),
                    entry.Values[column]);
        }

        Record(engine, sheet, before, texts["command.ledger.data.statistics.title"]);
        status.Report(texts.Format("status.tools.statistics", table.Rows.Count), NotificationSeverity.Success);
    }

    private void Record(
        Engine.Recalculation.CalculationEngine engine, Worksheet sheet, SheetSnapshot before, string description)
    {
        var after = snapshots.Capture(sheet);
        var index = Context.SheetIndex;

        Context.History?.Push(new DelegateAction(
            description,
            redo: () =>
            {
                snapshots.Restore(engine, index, after);
                Context.RequestRefresh();
            },
            undo: () =>
            {
                snapshots.Restore(engine, index, before);
                Context.RequestRefresh();
            }));

        Context.RequestRefresh();
    }

    private async Task<IReadOnlyDictionary<string, object>?> AskAsync(
        string key, IReadOnlyList<FormField> fields, CancellationToken ct, double width = 480) =>
        await dialogs.ShowAsync<Dictionary<string, object>>(
            new FormPromptView(texts[$"tools.{key}.explanation"], fields, texts),
            new DialogOptions { TitleKey = $"dialog.{key}.title", PreferredWidth = width }, ct)
            .ConfigureAwait(true);

    private bool Protected(Worksheet sheet)
    {
        if (!sheet.Protection.IsEnabled) return false;

        status.Report(texts["status.protection.locked"], NotificationSeverity.Error, LedgerRemedies.UnprotectSheet);
        return true;
    }

    private static IReadOnlyList<string> Headers(Worksheet sheet, CellRange range)
    {
        var headers = new List<string>();

        for (var column = range.Start.Column; column <= range.End.Column; column++)
        {
            var value = sheet.GetValue(new CellAddress(range.Start.Row, column));

            headers.Add(value.IsEmpty ? CellAddress.ColumnName(column) : value.ToString());
        }

        return headers;
    }

    private static string Camel(string name) =>
        string.Create(CultureInfo.InvariantCulture, $"{char.ToLowerInvariant(name[0])}{name[1..]}");
}
