using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerChangeCommands(
    IActiveSheetContext context,
    ISidePanelHost panel,
    IStatusBarService status,
    ITextCatalog texts) : LedgerCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Command("ledger.changes.record", () =>
        {
            if (Context.Workbook is not { } workbook) return;

            if (workbook.Changes.IsRecording && workbook.Changes.IsProtected)
            {
                status.Report(texts["review.protected"]);
                return;
            }

            OutsideHistory(() => workbook.Changes.IsRecording = !workbook.Changes.IsRecording);

            status.Report(texts[workbook.Changes.IsRecording
                ? "changes.recording.on"
                : "changes.recording.off"]);
        }),

        Command("ledger.changes.show", () =>
        {
            if (Context.Workbook is not { } workbook) return;

            Toggle("ledger.changes.show", () =>
            {
                workbook.Changes.ShowChanges = !workbook.Changes.ShowChanges;
                Context.RequestRefresh();
            });
        }),
    ];

    public IEnumerable<CommandDefinition> GetDialogCommands() =>
    [
        Prototype("ledger.changes.manage") with
        {
            MenuPath = "edit/changes",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Workbook is { } workbook && workbook.Changes.PendingCount > 0,
            Execute = (_, _) => ManageAsync(),
        },
    ];

    private CommandDefinition Command(string id, Action run) =>
        Prototype(id) with
        {
            CheckedWhen = id switch
            {
                "ledger.changes.record" => _ => Context.Workbook?.Changes.IsRecording == true,
                "ledger.changes.show" => _ => Context.Workbook?.Changes.ShowChanges == true,
                _ => null,
            },
            MenuPath = "edit/changes",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = id == "ledger.changes.record" ? DocumentState.ReadOnly : DocumentState.None,
            OutsideHistory = id == "ledger.changes.record",
            EnabledWhen = _ => Context.HasTarget,
            Execute = (_, _) =>
            {
                run();
                return Task.CompletedTask;
            },
        };

    private Task ManageAsync()
    {
        panel.Show("review");
        return Task.CompletedTask;
    }

    private void Toggle(string id, Action flip)
    {
        flip();
        Context.History?.Push(new PulseOffice.Document.Commands.DelegateAction(
            texts[$"command.{id}.title"], redo: flip, undo: flip));
    }
}
