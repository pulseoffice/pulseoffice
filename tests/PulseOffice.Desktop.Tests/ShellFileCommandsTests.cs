using PulseOffice.Desktop.Composition;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Shell;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class ShellFileCommandsTests
{
    private sealed class CancelledDialogs : IDocumentFileService
    {
        public ModuleKind Module => ModuleKind.Sheets;

        public Task SaveAsync(OpenDocument document, CancellationToken ct = default) => Task.CompletedTask;

        public Task SaveAsAsync(OpenDocument document, CancellationToken ct = default) => Task.CompletedTask;

        public Task SaveInterchangeAsync(OpenDocument document, CancellationToken ct = default) => Task.CompletedTask;

        public Task<bool> SaveToAsync(OpenDocument document, string path, CancellationToken ct = default) => Task.FromResult(false);

        public Task<bool> SaveCopyAsync(OpenDocument document, CancellationToken ct = default) => Task.FromResult(false);
    }

    private sealed class Refusing : IUnsavedChangesGuard
    {
        public bool HasUnsaved => true;

        public Task<bool> ConfirmCloseAsync(IReadOnlyList<OpenDocument> documents, CancellationToken ct = default) => Task.FromResult(false);

        public Task<bool> ConfirmCloseAllAsync(CancellationToken ct = default) => Task.FromResult(false);
    }

    private static (ShellCommands Commands, DocumentWorkspace Workspace, OpenDocument Document) Build()
    {
        var workspace = new DocumentWorkspace();
        var document = new OpenDocument(ModuleKind.Sheets, "conti", new Avalonia.Controls.Panel(), new PulseOffice.Document.Commands.UndoStack());
        document.RestoreLocation(Path.Combine(Path.GetTempPath(), "conti.ods"));
        document.MarkModified();
        workspace.Open(document);
        var status = new StatusBarService(TimeProvider.System, new NotificationCenter(TimeProvider.System), new NotificationHistory(TimeProvider.System));
        var commands = new ShellCommands(status, null!, null!, null!, workspace, null!, null!, null!, null!,
            new DocumentFileServices([new CancelledDialogs()]), null!, null!, () => new Refusing(), new TextCatalog(UiLanguage.Italian), new PulseOffice.Platform.RuntimeHostSystem());
        return (commands, workspace, document);
    }

    private static CommandDefinition Find(ShellCommands commands, string id) => commands.GetCommands().Single(command => command.Id.Value == id);

    [Fact]
    public async Task Salva_una_copia_annullato_non_fa_credere_il_documento_salvato()
    {
        var (commands, _, document) = Build();

        await Find(commands, "shell.file.saveCopy").Execute(CommandContext.Empty, TestContext.Current.CancellationToken);

        Assert.True(document.IsModified);
    }

    [Fact]
    public async Task Ricarica_chiede_prima_di_buttare_le_modifiche()
    {
        var (commands, workspace, document) = Build();

        await Find(commands, "shell.file.reload").Execute(CommandContext.Empty, TestContext.Current.CancellationToken);

        Assert.Contains(document, workspace.Documents);
    }
}
