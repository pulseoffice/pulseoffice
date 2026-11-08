using Avalonia.Controls;
using PulseOffice.Agent.Hosting;
using PulseOffice.Agent.UI;
using PulseOffice.Document.Commands;
using PulseOffice.Foundation.Modules;
using PulseOffice.Shell;
using PulseOffice.UI.Controls.Services;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public class AgentDialogGateTests
{
    private static T Fake<T>() where T : class => System.Reflection.DispatchProxy.Create<T, AgentDocumentHoldTests.Inert>();

    private sealed class InlineUi : IAgentUiThread
    {
        public Task<T> InvokeAsync<T>(Func<Task<T>> work) => work();
    }

    private sealed class OpenDialog : IDialogService
    {
        public bool IsDialogOpen { get; set; } = true;

        public Task<TResult?> ShowAsync<TResult>(Control content, DialogOptions options, CancellationToken ct = default) => Task.FromResult(default(TResult));

        public Task ShowAsync(Control content, DialogOptions options, CancellationToken ct = default) => Task.CompletedTask;

        public void Close(object? result)
        {
        }
    }

    private sealed class PendingEditor : Panel, IPendingEditsOwner
    {
        public int Commits { get; private set; }

        public void CommitPendingEdits() => Commits++;
    }

    private sealed record Scene(ShellAgentWorkspace Agent, ShellAgentCommands Commands, DocumentWorkspace Workspace, AgentDocumentIds Ids, OpenDocument First, OpenDocument Second, OpenDialog Dialogs);

    private static Scene Build()
    {
        var workspace = new DocumentWorkspace();
        var ids = new AgentDocumentIds();
        var dialogs = new OpenDialog();
        var gate = new AgentOperationGate(workspace, dialogs);
        var registry = new PulseOffice.UI.Commands.CommandRegistry(Fake<PulseOffice.Localization.ITextCatalog>());
        var services = new ShellAgentServices(
            workspace, Fake<IDocumentOpenRouter>(), Fake<IDocumentFileServices>(), Fake<IDocumentOperationsCatalog>(),
            Fake<PulseOffice.Formats.Pdf.IPdfExporter>(), Fake<PulseOffice.Platform.IAtomicFileWriter>(),
            registry, () => null!, ids, new InlineUi(),
            Fake<IAgentPageRenderer>(), Fake<PulseOffice.Localization.ITextCatalog>(), Fake<IUnattendedOpening>(),
            new PulseOffice.UI.Commands.CommandEffectChecks([]), gate);
        var first = new OpenDocument(ModuleKind.Sheets, "First", new PendingEditor(), new UndoStack());
        var second = new OpenDocument(ModuleKind.Sheets, "Second", new Panel(), new UndoStack());
        workspace.Open(first);
        workspace.Open(second);
        var commands = new ShellAgentCommands(workspace, registry, () => null!, new InlineUi(), gate, Fake<PulseOffice.Localization.ITextCatalog>());
        return new Scene(new ShellAgentWorkspace(services), commands, workspace, ids, first, second, dialogs);
    }

    [Fact]
    public async Task An_agent_edit_is_refused_while_a_dialog_is_open()
    {
        var scene = Build();
        var ran = false;

        await Assert.ThrowsAsync<AgentHostException>(() => scene.Agent.WithDocumentAsync(new AgentTarget(scene.Ids.IdOf(scene.First), null), "edit",
            (_, _) => Task.FromResult(ran = true), TestContext.Current.CancellationToken));

        Assert.False(ran);
    }

    [Fact]
    public async Task An_agent_read_still_runs_while_a_dialog_is_open()
    {
        var scene = Build();

        var read = await scene.Agent.WithDocumentAsync(new AgentTarget(scene.Ids.IdOf(scene.First), null), null,
            (info, _) => Task.FromResult(info.Title), TestContext.Current.CancellationToken);

        Assert.Equal("First", read);
    }

    [Fact]
    public async Task The_agent_cannot_switch_the_active_document_under_an_open_dialog()
    {
        var scene = Build();

        await Assert.ThrowsAsync<AgentHostException>(() => scene.Agent.ActivateAsync(scene.Ids.IdOf(scene.First), TestContext.Current.CancellationToken));

        Assert.Same(scene.Second, scene.Workspace.Active);
    }

    [Fact]
    public async Task The_agent_cannot_save_or_run_a_command_under_an_open_dialog()
    {
        var scene = Build();

        await Assert.ThrowsAsync<AgentHostException>(() => scene.Agent.SaveAsync(scene.Ids.IdOf(scene.First), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<AgentHostException>(() => scene.Commands.RunAsync("shell.file.close", "close", TestContext.Current.CancellationToken));

        Assert.Contains(scene.First, scene.Workspace.Documents);
    }

    [Fact]
    public async Task Once_the_dialog_closes_the_agent_edits_again()
    {
        var scene = Build();
        scene.Dialogs.IsDialogOpen = false;

        var edited = await scene.Agent.WithDocumentAsync(new AgentTarget(scene.Ids.IdOf(scene.First), null), "edit",
            (_, _) => Task.FromResult(true), TestContext.Current.CancellationToken);

        Assert.True(edited);
    }

    [Fact]
    public async Task Drawing_or_exporting_for_the_agent_does_not_commit_an_edit_pending_under_an_open_dialog()
    {
        var scene = Build();
        var editor = (PendingEditor)scene.First.View;

        await Assert.ThrowsAsync<AgentHostException>(() => scene.Agent.RenderAsync(new AgentTarget(scene.Ids.IdOf(scene.First), null), 1, null, 400, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<AgentHostException>(() => scene.Agent.ExportAsync(scene.Ids.IdOf(scene.First), "/tmp/never.pdf", TestContext.Current.CancellationToken));
        Assert.Equal(0, editor.Commits);

        scene.Dialogs.IsDialogOpen = false;
        await Assert.ThrowsAsync<AgentHostException>(() => scene.Agent.RenderAsync(new AgentTarget(scene.Ids.IdOf(scene.First), null), 1, null, 400, TestContext.Current.CancellationToken));
        Assert.Equal(1, editor.Commits);
    }
}
