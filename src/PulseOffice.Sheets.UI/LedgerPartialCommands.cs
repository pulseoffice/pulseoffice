using PulseOffice.UI.Controls.Notifications;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerPartialCommands(
    IActiveSheetContext context,
    ISeriesFill series,
    IStandardFilter standardFilter,
    ISheetRangeFilters rangeFilters,
    ILedgerDocumentFactory documents,
    PulseOffice.Shell.IDocumentWorkspace workspace,
    IDialogService dialogs,
    IStatusBarService status,
    ITextCatalog texts,
    ISheetDuplicator duplicator,
    DataDialogs.LedgerDialogKit kit,
    DataDialogs.ISeriesPreviewers previewers,
    ISheetValueWrites writes,
    IWorksheetTabPolicy tabs) : LedgerCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Command("ledger.fill.series", "sheet/fill", false, ct => SeriesAsync(ct)),
        Command("ledger.sheet.moveOrCopy", "sheet/organise", false, ct => MoveAsync(ct)),
        Command("ledger.data.standardFilter", "data/filter", false, ct => StandardFilterAsync(ct)),
        Command("ledger.file.protectStructure", "tools/protect", true, ct => ProtectAsync(ct)) with { OutsideHistory = true },

        Prototype("ledger.window.new") with
        {
            IconKey = "ledger.window.new",
            MenuPath = "view/windows",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => Context.HasTarget,
            Execute = (_, _) =>
            {
                NewWindow();
                return Task.CompletedTask;
            },
        },

        Prototype("ledger.window.close") with
        {
            MenuPath = "view/windows",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => workspace.Active is { } active && HasAnotherWindow(active),
            Execute = (_, _) =>
            {
                if (workspace.Active is { } active && HasAnotherWindow(active)) workspace.Close(active);
                return Task.CompletedTask;
            },
        },
    ];

    private bool HasAnotherWindow(PulseOffice.Shell.OpenDocument document) =>
        workspace.Documents.Any(other => !ReferenceEquals(other, document) && ReferenceEquals(other.History, document.History));

    private void NewWindow()
    {
        if (workspace.Active is not { } active) return;

        if (documents.CreateSecondView(active) is not { } second)
        {
            status.Report(texts["window.cannotSplit"], NotificationSeverity.Error);
            return;
        }

        workspace.Open(second);
        status.Report(texts["window.opened"]);
    }

    private CommandDefinition Command(
        string id, string path, bool alwaysOn, Func<CancellationToken, Task> run) => Prototype(id) with
    {
        IconKey = id,
        MenuPath = path,
        Surface = CommandSurface.Advanced,
        RequiredState = DocumentState.DocumentOpen,
        ForbiddenState = alwaysOn ? DocumentState.None : DocumentState.ReadOnly,
        EnabledWhen = _ => Context.HasTarget,
        Execute = (_, ct) => run(ct),
    };

    private async Task SeriesAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;
        if (Context.Engine is not { } engine) return;

        var selected = workbook.Sheets[Context.SheetIndex];
        var choice = await dialogs.ShowAsync<DataDialogs.SeriesChoice>(
            new DataDialogs.SeriesView(kit, previewers.For(selected, Context.Selection), Context.Selection.RowCount >= Context.Selection.ColumnCount),
            new DialogOptions { TitleKey = "command.ledger.fill.series.title" },
            ct).ConfigureAwait(true);

        if (choice is null) return;

        var request = new SeriesRequest(Context.Selection, choice.Kind)
        {
            ByColumns = choice.ByColumns,
            DateUnit = choice.Unit,
            Step = choice.Step,
            Stop = choice.Stop,
        };

        var sheet = workbook.Sheets[Context.SheetIndex];
        var plan = series.Plan(sheet, request);

        if (plan.Count == 0)
        {
            status.Report(texts["series.nothing"], NotificationSeverity.Warning);
            return;
        }

        writes.Write(Context, engine, plan, texts["history.fill.series"]);
    }

    private async Task MoveAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;
        if (RefuseStructureChange(status, texts)) return;

        var choice = await dialogs.ShowAsync<DataDialogs.SheetMoveChoice>(
            new DataDialogs.SheetMoveView(kit,
                new DataDialogs.SheetMovePlan([.. workbook.Sheets.Select(sheet => sheet.Name)], Context.SheetIndex, tabs),
                tabs.UniqueName(workbook, workbook.Sheets[Context.SheetIndex].Name, keepWhenFree: false)),
            new DialogOptions { TitleKey = "command.ledger.sheet.moveOrCopy.title" },
            ct).ConfigureAwait(true);

        if (choice is null) return;

        var target = choice.Position;
        var copy = choice.Copy;
        var from = Context.SheetIndex;
        var source = workbook.Sheets[from];

        if (copy)
        {
            var name = choice.Name is { Length: > 0 } chosen && chosen.Trim().Length > 0 ? chosen.Trim() : tabs.UniqueName(workbook, source.Name, keepWhenFree: false);

            if (!tabs.NameAllowed(name))
            {
                status.Report(texts.Format("sheet.name.invalid", name), NotificationSeverity.Error);
                return;
            }

            if (workbook.FindSheet(name) is not null)
            {
                status.Report(texts.Format("status.sheet.nameTaken", name), NotificationSeverity.Error);
                return;
            }

            var duplicate = duplicator.Duplicate(workbook, source, name);
            var at = Math.Clamp(target, 0, workbook.Sheets.Count);

            duplicator.Insert(workbook, duplicate, at);

            Context.History?.Push(new Document.Commands.DelegateAction(
                texts["history.sheet.duplicate"],
                redo: () => duplicator.Insert(workbook, duplicate, at),
                undo: () => duplicator.Remove(workbook, duplicate)));

            Context.SelectSheet(at);
            Context.RequestRefresh();
            status.Report(texts.Format("sheet.move.copied", name));

            return;
        }

        var to = Math.Clamp(target > from ? target - 1 : target, 0, workbook.Sheets.Count - 1);
        if (to == from) return;

        workbook.MoveSheet(from, to);

        Context.History?.Push(new Document.Commands.DelegateAction(
            texts["history.sheet.move"],
            redo: () => workbook.MoveSheet(from, to),
            undo: () => workbook.MoveSheet(to, from)));

        Context.SelectSheet(to);
        Context.RequestRefresh();
    }

    private async Task StandardFilterAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;

        var sheet = workbook.Sheets[Context.SheetIndex];
        if (sheet.Protection is { IsEnabled: true, AllowFilter: false })
        {
            status.Report(texts["status.protection.filter"], NotificationSeverity.Error, LedgerRemedies.UnprotectSheet);
            return;
        }
        var stored = rangeFilters.Stored(workbook, Context.SheetIndex, Context.Selection);
        var range = stored?.Range ?? Context.Selection;
        var (initial, notice) = Reloaded(stored);

        var headers = Enumerable
            .Range(range.Start.Column, range.End.Column - range.Start.Column + 1)
            .Select(column => sheet[new CellAddress(range.Start.Row, column)].Value.AsText())
            .Select((header, index) => header is { Length: > 0 }
                ? header
                : CellAddress.ColumnName(range.Start.Column + index))
            .ToList();

        var explanation = notice is null ? texts["filter.explanation"] : texts["filter.explanation"] + "\n\n" + texts[notice];
        var values = await dialogs.ShowAsync<Dictionary<string, object>>(
            new FormPromptView(explanation,
                [new Dialogs.StandardFilterField("conditions", texts["filter.conditions"], headers) { Initial = initial }], texts),
            new DialogOptions { TitleKey = "command.ledger.data.standardFilter.title", PreferredWidth = 640 },
            ct).ConfigureAwait(true);

        if (values is null) return;

        var rows = (values["conditions"] as IReadOnlyList<StandardFilterRow> ?? [])
            .Where(standardFilter.Applies)
            .ToList();

        var condition = standardFilter.Compose(rows);
        var unique = stored?.Filter is { Criteria: null, UniqueOnly: true };
        var hidden = rangeFilters.Store(Context, sheet, range, stored?.HasHeader ?? true,
            condition is null && !unique ? null : new RangeFilter { Condition = condition, UniqueOnly = unique },
            texts["command.ledger.data.standardFilter.title"]);

        status.Report(condition is null ? texts["filter.cleared"] : texts.Format("filter.hidden", hidden));
    }

    private (IReadOnlyList<StandardFilterRow> Rows, string? Notice) Reloaded(SheetRangeFilter? stored)
    {
        if (stored is null) return ([], null);
        if (stored.Filter.Criteria is not null) return ([], "filter.replacesAdvanced");
        if (stored.Filter.Condition is not { } condition) return ([], null);

        return standardFilter.Decompose(condition, Dialogs.StandardFilterEditor.MaximumConditions) is { } rows
            ? (rows, null)
            : ([], "filter.tooComplex");
    }

    private async Task ProtectAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;

        var current = workbook.Protection;

        var values = await dialogs.ShowAsync<Dictionary<string, object>>(
            new FormPromptView(
                texts["protect.explanation"],
                [
                    new CheckField("structure", texts["protect.structure"], current.Structure),
                    new CheckField("windows", texts["protect.windows"], current.Windows),
                    new TextField("password", texts["protect.password"], string.Empty),
                ],
                texts),
            new DialogOptions { TitleKey = "command.ledger.file.protectStructure.title" }, ct)
            .ConfigureAwait(true);

        if (values is null) return;

        if (current.PasswordHash is not null && !current.Accepts(values["password"] as string))
        {
            status.Report(texts["protect.wrongPassword"], NotificationSeverity.Error);
            return;
        }

        var password = values["password"] as string;

        OutsideHistory(() => workbook.Protection = new WorkbookProtection
        {
            Structure = values["structure"] is true,
            Windows = values["windows"] is true,
            PasswordHash = password is { Length: > 0 } ? WorkbookProtection.Hash(password) : null,
        });

        status.Report(texts[workbook.Protection.IsEnabled ? "protect.on" : "protect.off"]);
    }
}
