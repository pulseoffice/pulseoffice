using PulseOffice.UI.Controls.Notifications;
using PulseOffice.Document.Commands;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerSparklineCommands(
    IActiveSheetContext context,
    ISheetSnapshots snapshots,
    IDialogService dialogs,
    IStatusBarService status,
    Dialogs.ISheetReferenceSources sources,
    Dialogs.ISheetReferenceReader reader,
    CellFormat.SparklineDialogServices sparklineServices,
    ISparklineGroups groups,
    ITextCatalog texts) : LedgerCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Prototype("ledger.sparkline.insert") with
        {
            IconKey = "ledger.sparkline.insert",
            MenuPath = "insert/sparkline",
            Surface = CommandSurface.Common,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.HasTarget,
            Execute = (_, ct) => InsertAsync(ct),
        },

        Prototype("ledger.sparkline.edit") with
        {
            IconKey = "ledger.sparkline.edit",
            MenuPath = "insert/sparkline",
            ContextPaths = ["cell/object"],
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => GroupAtSelection() is not null,
            Execute = (_, ct) => GroupAtSelection() is { } group ? EditAsync(group, ct) : Task.CompletedTask,
        },

        Prototype("ledger.sparkline.group") with
        {
            IconKey = "ledger.sparkline.group",
            MenuPath = "insert/sparkline",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Workbook is { } workbook
                && groups.Grouped(workbook.Sheets[Context.SheetIndex].Sparklines, Context.Selection, Context.Selection.Start, "probe") is not null,
            Execute = (_, _) =>
            {
                Regroup("ledger.sparkline.group", sparklines =>
                    groups.Grouped(sparklines, Context.Selection, Context.Selection.Start, Guid.NewGuid().ToString("N")));
                return Task.CompletedTask;
            },
        },

        Prototype("ledger.sparkline.ungroup") with
        {
            IconKey = "ledger.sparkline.ungroup",
            MenuPath = "insert/sparkline",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Workbook is { } workbook
                && groups.Ungrouped(workbook.Sheets[Context.SheetIndex].Sparklines, Context.Selection) is not null,
            Execute = (_, _) =>
            {
                Regroup("ledger.sparkline.ungroup", sparklines => groups.Ungrouped(sparklines, Context.Selection));
                return Task.CompletedTask;
            },
        },

        Prototype("ledger.sparkline.delete") with
        {
            MenuPath = "insert/sparkline",
            ContextPaths = ["cell/object"],
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Workbook is { } workbook
                && workbook.Sheets[Context.SheetIndex].Sparklines.At(Context.Selection.Start) is not null,
            Execute = (_, _) =>
            {
                Delete();
                return Task.CompletedTask;
            },
        },
    ];

    private async Task InsertAsync(CancellationToken ct) =>
        await EditAsync(null, ct).ConfigureAwait(true);

    private async Task EditAsync(SparklineGroup? editing, CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;

        var sheetIndex = Context.SheetIndex;
        var sheet = workbook.Sheets[sheetIndex];
        if (Refused(sheet)) return;

        var described = editing is null ? null : groups.Describe(workbook, sheetIndex, editing);
        var view = new CellFormat.SparklineDialogView(
            sheet, described?.Data ?? Context.Selection.ToString(), sources.For(Dialogs.ReferenceShape.Range),
            text => Source(workbook, text, sheetIndex), sparklineServices, texts,
            editing is null || described is null ? null : new CellFormat.SparklineDialogStart(described.Destination, described.Dates, editing));

        var titleKey = editing is null ? "command.ledger.sparkline.insert.title" : "command.ledger.sparkline.edit.title";
        var values = await dialogs.ShowAsync<CellFormat.SparklineDialogResult>(
            view, new DialogOptions { TitleKey = titleKey, PreferredWidth = 700 }, ct)
            .ConfigureAwait(true);

        if (values is null) return;

        var dates = values.Dates.Length == 0 ? null : ReadOne(workbook, values.Dates, sheetIndex);
        if (ReadOne(workbook, values.Data, sheetIndex) is not { } data
            || reader.ReadLocal(workbook, values.Destination, sheetIndex, Dialogs.ReferenceShape.Range) is not { } target
            || (values.Dates.Length > 0 && dates is null))
        {
            status.Report(texts["status.analysis.badReference"], NotificationSeverity.Error);
            return;
        }

        var options = (editing ?? new SparklineGroup { Id = Guid.NewGuid().ToString("N"), Sparklines = [] }) with
        {
            Kind = values.Kind,
            Style = values.Style,
            MinimumAxis = values.MinimumAxis,
            MaximumAxis = values.MaximumAxis,
            ManualMinimum = values.ManualMinimum,
            ManualMaximum = values.ManualMaximum,
            EmptyCells = values.EmptyCells,
            ShowHidden = values.ShowHidden,
            RightToLeft = values.RightToLeft,
        };

        if (groups.Lay(workbook, new SparklineLayoutRequest(sheetIndex, data, target, dates, options)) is not { } group)
        {
            status.Report(texts["status.tools.nothing"], NotificationSeverity.Warning);
            return;
        }

        Apply(editing is null ? "ledger.sparkline.insert" : "ledger.sparkline.edit", owner =>
        {
            if (editing is not null) owner.Sparklines.RemoveAt(editing.Sparklines.Select(line => line.Cell));
            owner.Sparklines.Add(group);
        });

        status.Report(texts.Format("sparkline.created", group.Sparklines.Count));
    }

    private SheetRangeReference? ReadOne(Workbook workbook, string text, int sheetIndex) =>
        reader.Read(workbook, text, sheetIndex, Dialogs.ReferenceShape.Range) is [var reference] ? reference : null;

    private CellFormat.SparklineSource? Source(Workbook workbook, string text, int sheetIndex) =>
        ReadOne(workbook, text, sheetIndex) is { } reference
            ? new CellFormat.SparklineSource(reference.Range, reference.SheetIndex == sheetIndex ? null : workbook.Sheets[reference.SheetIndex].Name)
            : null;

    private SparklineGroup? GroupAtSelection() =>
        Context.Workbook is { } workbook
            ? workbook.Sheets[Context.SheetIndex].Sparklines.At(Context.Selection.Start)?.Group
            : null;

    private void Regroup(string id, Func<SparklineCollection, IReadOnlyList<SparklineGroup>?> plan)
    {
        if (Context.Workbook is not { } workbook) return;
        if (plan(workbook.Sheets[Context.SheetIndex].Sparklines) is not { } regrouped) return;

        Apply(id, sheet =>
        {
            sheet.Sparklines.Clear();
            foreach (var group in regrouped) sheet.Sparklines.Add(group);
        });
    }

    private void Delete() =>
        Apply("ledger.sparkline.delete", sheet =>
            sheet.Sparklines.RemoveWithin(Context.Selection));

    private void Apply(string id, Action<Worksheet> change)
    {
        if (Context.Workbook is not { } workbook) return;
        if (Context.Engine is not { } engine) return;

        var sheetIndex = Context.SheetIndex;
        var sheet = workbook.Sheets[sheetIndex];
        if (Refused(sheet)) return;

        var before = snapshots.Capture(sheet);
        change(sheet);
        var after = snapshots.Capture(sheet);

        Context.History?.Push(new DelegateAction(
            texts[$"command.{id}.title"],
            redo: () => snapshots.Restore(engine, sheetIndex, after),
            undo: () => snapshots.Restore(engine, sheetIndex, before)));

        Context.RequestRefresh();
    }

    private bool Refused(Worksheet sheet)
    {
        if (!sheet.Protection.IsEnabled) return false;
        status.Report(texts["status.protection.objects"], NotificationSeverity.Error, LedgerRemedies.UnprotectSheet);
        return true;
    }
}
