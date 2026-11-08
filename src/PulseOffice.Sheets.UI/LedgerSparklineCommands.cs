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

public sealed class LedgerSparklineCommands(
    IActiveSheetContext context,
    ISheetSnapshots snapshots,
    IDialogService dialogs,
    IStatusBarService status,
    Dialogs.ISheetReferenceSources sources,
    Dialogs.ISheetReferenceReader reader,
    CellFormat.SparklineDialogServices sparklineServices,
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

    private async Task InsertAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;

        var sheetIndex = Context.SheetIndex;
        if (Refused(workbook.Sheets[sheetIndex])) return;

        var view = new CellFormat.SparklineDialogView(
            workbook.Sheets[sheetIndex], Context.Selection.ToString(), sources.For(Dialogs.ReferenceShape.Range),
            text => reader.ReadLocal(workbook, text, sheetIndex, Dialogs.ReferenceShape.Range), sparklineServices, texts);

        var values = await dialogs.ShowAsync<CellFormat.SparklineDialogResult>(
            view, new DialogOptions { TitleKey = "command.ledger.sparkline.insert.title", PreferredWidth = 700 }, ct)
            .ConfigureAwait(true);

        if (values is null) return;

        if (reader.ReadLocal(workbook, values.Data, sheetIndex, Dialogs.ReferenceShape.Range) is not { } requested
            || reader.ReadLocal(workbook, values.Destination, sheetIndex, Dialogs.ReferenceShape.Range) is not { } target)
        {
            status.Report(texts["status.analysis.badReference"], NotificationSeverity.Error);
            return;
        }

        if (workbook.Sheets[sheetIndex].UsedRange() is not { } used
            || used.End.Row < requested.Start.Row || used.End.Column < requested.Start.Column)
        {
            status.Report(texts["status.tools.nothing"], NotificationSeverity.Warning);
            return;
        }

        var data = new CellRange(requested.Start,
            new CellAddress(Math.Min(requested.End.Row, used.End.Row), Math.Min(requested.End.Column, used.End.Column)));

        var byRow = data.RowCount >= data.ColumnCount || target.ColumnCount == 1;

        var lines = new List<Sparkline>();
        var destinations = target.ColumnCount == 1 ? target.RowCount : target.ColumnCount;
        var slots = byRow ? data.RowCount : data.ColumnCount;
        if (destinations > 1) slots = Math.Min(slots, destinations);

        for (var index = 0; index < slots; index++)
        {
            var source = byRow
                ? new CellRange(
                    new CellAddress(data.Start.Row + index, data.Start.Column),
                    new CellAddress(data.Start.Row + index, data.End.Column))
                : new CellRange(
                    new CellAddress(data.Start.Row, data.Start.Column + index),
                    new CellAddress(data.End.Row, data.Start.Column + index));

            var cell = target.ColumnCount == 1
                ? new CellAddress(target.Start.Row + index, target.Start.Column)
                : new CellAddress(target.Start.Row, target.Start.Column + index);

            if (!cell.IsValid) continue;

            lines.Add(new Sparkline(cell, source));
        }

        if (lines.Count == 0) return;

        var group = new SparklineGroup
        {
            Id = Guid.NewGuid().ToString("N"),
            Sparklines = lines,
            Kind = values.Kind,
            Style = values.Style,
            SameScale = values.SameScale,
        };

        Apply("ledger.sparkline.insert", sheet => sheet.Sparklines.Add(group));

        status.Report(texts.Format("sparkline.created", lines.Count));
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
