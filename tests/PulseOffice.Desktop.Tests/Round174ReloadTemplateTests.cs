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
public sealed class Round174ReloadTemplateTests
{
    private sealed class Router : IDocumentOpenRouter
    {
        public List<string> Calls { get; } = [];

        public bool CanOpen(string path) => true;

        public Task OpenAsync(string path, CancellationToken ct = default)
        {
            Calls.Add("open " + path);
            return Task.CompletedTask;
        }

        public Task OpenDialogAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task<bool> OpenAsNewAsync(string path, CancellationToken ct = default)
        {
            Calls.Add("new " + path);
            return Task.FromResult(true);
        }

        public Task EditAsync(string path, CancellationToken ct = default)
        {
            Calls.Add("edit " + path);
            return Task.CompletedTask;
        }

        public OpenDocument? FindOpen(string path) => null;
    }

    private sealed class Allowing : IUnsavedChangesGuard
    {
        public bool HasUnsaved => false;

        public Task<bool> ConfirmCloseAsync(IReadOnlyList<OpenDocument> documents, CancellationToken ct = default) => Task.FromResult(true);

        public Task<bool> ConfirmCloseAllAsync(CancellationToken ct = default) => Task.FromResult(true);
    }

    [Fact]
    public async Task Reloading_a_template_being_edited_reopens_the_template_itself()
    {
        var workspace = new DocumentWorkspace();
        var template = Path.Combine(Path.GetTempPath(), "invoice.ots");
        var document = new OpenDocument(ModuleKind.Sheets, "invoice", new Avalonia.Controls.Panel(), new PulseOffice.Document.Commands.UndoStack());
        document.RestoreLocation(template);
        workspace.Open(document);
        var router = new Router();
        var status = new StatusBarService(TimeProvider.System, new NotificationCenter(TimeProvider.System), new NotificationHistory(TimeProvider.System));
        var commands = new ShellCommands(status, null!, null!, null!, new PulseOffice.Shell.Desktop.DesktopIntegrationCoordinator(new PulseOffice.Platform.UnavailableDesktopIntegration(), null!, null!, null!, null!, null!), workspace, null!, null!, null!, router,
            new DocumentFileServices([]), null!, null!, () => new Allowing(), new TextCatalog(UiLanguage.English), new PulseOffice.Platform.RuntimeHostSystem());

        await commands.GetCommands().Single(command => command.Id.Value == "shell.file.reload")
            .Execute(CommandContext.Empty, TestContext.Current.CancellationToken);

        Assert.Equal(["edit " + template], router.Calls);
    }
}
