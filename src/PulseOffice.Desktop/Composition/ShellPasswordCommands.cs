using PulseOffice.UI.Controls.Notifications;
using PulseOffice.Document.Commands;
using PulseOffice.Localization;
using PulseOffice.Shell;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Desktop.Composition;

public sealed class ShellPasswordCommands(
    IDocumentWorkspace workspace,
    IEnumerable<IDocumentSaver> savers,
    IDocumentPasswordGate gate,
    IStatusBarService status,
    ITextCatalog texts)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        new CommandDefinition
        {
            Id = new CommandId("shell.file.password"),
            MenuPath = "file",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => workspace.Active is { } document && savers.Any(saver => saver.CanSave(document)),
            Execute = (_, ct) => ProtectAsync(ct),
        },
    ];

    private async Task ProtectAsync(CancellationToken ct)
    {
        if (workspace.Active is not { } document) return;
        if (await gate.ChooseProtectionAsync(document.Title, ct).ConfigureAwait(true) is not { } choice) return;
        if (savers.FirstOrDefault(saver => saver.CanSave(document)) is not { } saver) return;

        var previous = document.Protection;
        document.Protection = choice.Protection;
        if (!await saver.SaveAsync(document, ct).ConfigureAwait(true)
            || document.Path is not { Length: > 0 } path
            || gate.IsProtected(path) != choice.Protection is not null)
        {
            document.Protection = previous;
            return;
        }

        status.Report(texts.Format(choice.Protection switch
        {
            null => "status.password.removed",
            PulseOffice.Formats.Packaging.Encryption.OpenPgpProtection => "status.password.recipients",
            _ => "status.password.set",
        }, document.Title), NotificationSeverity.Success);
    }
}
