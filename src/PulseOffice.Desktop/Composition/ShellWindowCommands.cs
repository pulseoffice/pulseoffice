using PulseOffice.Localization;
using PulseOffice.Shell;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Desktop.Composition;

public sealed class ShellWindowCommands(
    IDocumentWindows windows,
    IDocumentWorkspace workspace,
    IStatusBarService status,
    ITextCatalog texts)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        new CommandDefinition
        {
            Id = new CommandId("shell.window.new"),
            IconKey = "shell.window.new",
            MenuPath = "view/windows",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => workspace.Active is { } active && windows.CanOpenAnother(active),
            Execute = (_, _) =>
            {
                NewWindow();
                return Task.CompletedTask;
            },
        },

        new CommandDefinition
        {
            Id = new CommandId("shell.window.close"),
            IconKey = "shell.window.close",
            MenuPath = "view/windows",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => workspace.Active is { } active && windows.HasAnotherWindow(active),
            Execute = (_, _) =>
            {
                if (workspace.Active is { } active) windows.CloseWindow(active);
                return Task.CompletedTask;
            },
        },
    ];

    private void NewWindow()
    {
        if (workspace.Active is not { } active) return;

        if (windows.OpenAnother(active) is null)
        {
            status.Report(texts["window.cannotSplit"], NotificationSeverity.Error);
            return;
        }

        status.Report(texts["window.opened"]);
    }
}
