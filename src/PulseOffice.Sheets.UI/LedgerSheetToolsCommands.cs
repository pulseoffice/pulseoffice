using PulseOffice.Document.Commands;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerSheetToolsCommands(
    IActiveSheetContext context,
    ISheetOutline outline,
    ISheetSnapshots snapshots,
    IDialogService dialogs,
    IStatusBarService status,
    ITextCatalog texts) : LedgerCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Change("ledger.break.insertRow", "sheet/breaks", sheet =>
            sheet.RowBreaks.Add(Context.Selection.Start.Row),
            sheet => !sheet.RowBreaks.Contains(Context.Selection.Start.Row)),

        Change("ledger.break.insertColumn", "sheet/breaks", sheet =>
            sheet.ColumnBreaks.Add(Context.Selection.Start.Column),
            sheet => !sheet.ColumnBreaks.Contains(Context.Selection.Start.Column)),

        Change("ledger.break.removeRow", "sheet/breaks", sheet =>
            sheet.RowBreaks.Remove(Context.Selection.Start.Row),
            sheet => sheet.RowBreaks.Contains(Context.Selection.Start.Row)),

        Change("ledger.break.removeColumn", "sheet/breaks", sheet =>
            sheet.ColumnBreaks.Remove(Context.Selection.Start.Column),
            sheet => sheet.ColumnBreaks.Contains(Context.Selection.Start.Column)),

        Change("ledger.comment.showAll", "sheet/comments", sheet =>
        {
            foreach (var address in sheet.Comments.Keys.ToList())
                sheet.Comments[address] = sheet.Comments[address] with { IsAlwaysVisible = true };

            return sheet.Comments.Count > 0;
        }, sheet => sheet.Comments.Count > 0),

        Change("ledger.comment.hideAll", "sheet/comments", sheet =>
        {
            foreach (var address in sheet.Comments.Keys.ToList())
                sheet.Comments[address] = sheet.Comments[address] with { IsAlwaysVisible = false };

            return sheet.Comments.Count > 0;
        }, sheet => sheet.Comments.Count > 0),

        Change("ledger.comment.deleteAll", "sheet/comments", sheet =>
        {
            if (sheet.Comments.Count == 0) return false;

            sheet.Comments.Clear();
            return true;
        }, sheet => sheet.Comments.Count > 0),

        Change("ledger.outline.auto", "data/outline", sheet =>
        {
            var groups = outline.Detect(sheet, Context.Selection);

            if (groups.Rows.Count + groups.Columns.Count == 0)
            {
                status.Report(texts["status.outline.none"], PulseOffice.UI.Controls.Notifications.NotificationSeverity.Warning);
                return false;
            }

            foreach (var group in groups.Rows) sheet.RowGroups.Add(group);
            foreach (var group in groups.Columns) sheet.ColumnGroups.Add(group);
            return true;
        }),

        Change("ledger.outline.clear", "data/outline", sheet =>
        {
            if (sheet.RowGroups.Count == 0 && sheet.ColumnGroups.Count == 0) return false;

            sheet.RowGroups.Clear();
            sheet.ColumnGroups.Clear();
            return true;
        }, sheet => sheet.RowGroups.Count > 0 || sheet.ColumnGroups.Count > 0),
    ];

    public IEnumerable<CommandDefinition> GetDialogCommands() =>
    [
        Prototype("ledger.worksheet.goTo") with
        {
            MenuPath = "sheet/navigate",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => Context.Workbook is { Sheets.Count: > 1 },
            Execute = (_, ct) => GoToAsync(ct),
        },
    ];

    private CommandDefinition Change(
        string id, string menuPath, Func<Worksheet, bool> change, Func<Worksheet, bool>? enabled = null) =>
        Prototype(id) with
        {
            MenuPath = menuPath,
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Workbook is { } workbook
                && (enabled?.Invoke(workbook.Sheets[Context.SheetIndex]) ?? true),
            Execute = (_, _) =>
            {
                Apply(id, change);
                return Task.CompletedTask;
            },
        };

    private void Apply(string id, Func<Worksheet, bool> change)
    {
        if (Context.Workbook is not { } workbook) return;
        if (Context.Engine is not { } engine) return;

        var sheetIndex = Context.SheetIndex;
        var sheet = workbook.Sheets[sheetIndex];
        if (sheet.Protection.IsEnabled)
        {
            status.Report(
                texts[id.StartsWith("ledger.comment.", StringComparison.Ordinal) ? "status.protection.objects" : "status.protection.format"],
                PulseOffice.UI.Controls.Notifications.NotificationSeverity.Error, LedgerRemedies.UnprotectSheet);
            return;
        }

        var before = snapshots.Capture(sheet);

        if (!change(sheet)) return;

        var after = snapshots.Capture(sheet);

        Context.History?.Push(new DelegateAction(
            texts[$"command.{id}.title"],
            redo: () => snapshots.Restore(engine, sheetIndex, after),
            undo: () => snapshots.Restore(engine, sheetIndex, before)));

        Context.RequestRefresh();
    }

    private async Task GoToAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;

        var names = workbook.Sheets.Select(sheet => sheet.Name).ToList();

        var form = new FormPromptView(
            texts["sheet.goTo.explanation"],
            [new ChoiceField("sheet", texts["sheet.goTo.label"], names, Context.SheetIndex)],
            texts);

        var values = await dialogs.ShowAsync<Dictionary<string, object>>(
            form, new DialogOptions { TitleKey = "command.ledger.worksheet.goTo.title" }, ct)
            .ConfigureAwait(true);

        if (values is null) return;

        var index = Math.Clamp(
            (int)Convert.ToInt64(values["sheet"], System.Globalization.CultureInfo.InvariantCulture), 0, names.Count - 1);

        Context.SelectSheet(index);
        status.Report(names[index]);
    }
}
