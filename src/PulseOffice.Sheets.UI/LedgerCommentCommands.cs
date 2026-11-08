using PulseOffice.UI.Controls.Notifications;
using PulseOffice.Document.Commands;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerCommentCommands(
    IActiveSheetContext context,
    IDialogService dialogs,
    IStatusBarService status,
    ITextCatalog texts,
    PulseOffice.Foundation.Identity.IUserIdentity identity) : LedgerCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Prototype("ledger.comment.edit") with
        {
            MenuPath = "insert/comment",
            ContextPaths = ["cell/notes"],
            Surface = CommandSurface.Common | CommandSurface.ContextMenu,
            DefaultGesture = "Ctrl+Alt+C",
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            RequiredSelection = SelectionKind.Cell | SelectionKind.CellRange,
            EnabledWhen = _ => Context.HasTarget,
            Execute = (_, ct) => EditAsync(ct),
        },

        Prototype("ledger.comment.delete") with
        {
            MenuPath = "insert/comment",
            ContextPaths = ["cell/notes"],
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Workbook is { } workbook
                && workbook.Sheets[Context.SheetIndex].Comments.ContainsKey(Context.Selection.Start),
            Execute = (_, _) =>
            {
                Delete();
                return Task.CompletedTask;
            },
        },
    ];

    private async Task EditAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;

        var sheet = workbook.Sheets[Context.SheetIndex];
        if (Protected(sheet)) return;
        var address = Context.Selection.Start;

        var existing = sheet.Comments.GetValueOrDefault(address);

        var text = await dialogs.ShowAsync<string>(
            new TextPromptView(
                texts["comment.explanation"], existing?.Text ?? string.Empty, texts, multiline: true),
            new DialogOptions { TitleKey = "dialog.comment.title", PreferredWidth = 460 }, ct)
            .ConfigureAwait(true);

        if (text is null) return;

        var previous = existing;
        var trimmed = text.Trim();

        void Apply(CellComment? comment)
        {
            if (comment is null) sheet.Comments.Remove(address);
            else sheet.Comments[address] = comment;

            Context.RequestRefresh();
        }

        var updated = trimmed.Length == 0
            ? null
            : existing is null
                ? new CellComment { Text = trimmed, Author = identity.Name }
                : existing.Text == trimmed ? existing : existing with { Text = trimmed };

        if (Equals(updated, previous)) return;

        Apply(updated);

        Context.History?.Push(new DelegateAction(
            texts["command.ledger.comment.edit.title"],
            redo: () => Apply(updated),
            undo: () => Apply(previous)));

        status.Report(texts[updated is null ? "status.comment.removed" : "status.comment.saved"]);
    }

    private void Delete()
    {
        if (Context.Workbook is not { } workbook) return;

        var sheet = workbook.Sheets[Context.SheetIndex];
        if (Protected(sheet)) return;
        var address = Context.Selection.Start;

        if (!sheet.Comments.TryGetValue(address, out var previous)) return;

        sheet.Comments.Remove(address);
        Context.RequestRefresh();

        Context.History?.Push(new DelegateAction(
            texts["command.ledger.comment.delete.title"],
            redo: () =>
            {
                sheet.Comments.Remove(address);
                Context.RequestRefresh();
            },
            undo: () =>
            {
                sheet.Comments[address] = previous;
                Context.RequestRefresh();
            }));

        status.Report(texts["status.comment.removed"]);
    }

    private bool Protected(Worksheet sheet)
    {
        if (!sheet.Protection.IsEnabled) return false;

        status.Report(texts["status.protection.objects"], NotificationSeverity.Error, LedgerRemedies.UnprotectSheet);
        return true;
    }
}
