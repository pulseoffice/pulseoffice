using PulseOffice.Agent.Hosting;
using PulseOffice.Agent.UI;
using PulseOffice.Document.Commands;
using PulseOffice.Foundation.Modules;
using PulseOffice.Shell;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public class AgentDocumentHoldTests
{
    public class Inert : System.Reflection.DispatchProxy
    {
        protected override object? Invoke(System.Reflection.MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.ReturnType is { IsValueType: true } type && type != typeof(void) ? Activator.CreateInstance(type) : null;
    }

    private static T Fake<T>() where T : class => System.Reflection.DispatchProxy.Create<T, Inert>();

    private sealed class InlineUi : IAgentUiThread
    {
        public Task<T> InvokeAsync<T>(Func<Task<T>> work) => work();
    }

    private static (ShellAgentWorkspace Agent, DocumentWorkspace Workspace, AgentDocumentIds Ids) Scene()
    {
        var workspace = new DocumentWorkspace();
        var ids = new AgentDocumentIds();
        var services = new ShellAgentServices(
            workspace, Fake<IDocumentOpenRouter>(), Fake<IDocumentFileServices>(), Fake<IDocumentOperationsCatalog>(),
            Fake<PulseOffice.Formats.Pdf.IPdfExporter>(), Fake<PulseOffice.Platform.IAtomicFileWriter>(),
            new PulseOffice.UI.Commands.CommandRegistry(Fake<PulseOffice.Localization.ITextCatalog>()), () => null!, ids, new InlineUi(),
            Fake<IAgentPageRenderer>(), Fake<PulseOffice.Localization.ITextCatalog>(), Fake<IUnattendedOpening>(),
            Fake<PulseOffice.UI.Commands.ICommandEffectChecks>(), new AgentOperationGate(workspace, Fake<PulseOffice.UI.Controls.Services.IDialogService>()));
        return (new ShellAgentWorkspace(services), workspace, ids);
    }

    [Fact]
    public async Task The_user_switching_tab_while_an_agent_call_runs_does_not_redirect_it()
    {
        var (agent, workspace, ids) = Scene();
        var target = new OpenDocument(ModuleKind.Sheets, "Target", new Avalonia.Controls.Panel(), new UndoStack());
        var other = new OpenDocument(ModuleKind.Sheets, "Other", new Avalonia.Controls.Panel(), new UndoStack());
        workspace.Open(target);
        workspace.Open(other);
        var running = new TaskCompletionSource();
        var resume = new TaskCompletionSource();

        var call = agent.WithDocumentAsync(new AgentTarget(ids.IdOf(target), ModuleKind.Sheets), null, async (_, _) =>
        {
            running.SetResult();
            await resume.Task;
            return workspace.Active;
        }, TestContext.Current.CancellationToken);

        await running.Task;
        workspace.Activate(other);
        workspace.Close(target);
        resume.SetResult();

        Assert.Same(target, await call);
        Assert.DoesNotContain(target, workspace.Documents);
        Assert.Same(other, workspace.Active);
    }

    [Fact]
    public async Task A_second_agent_call_waits_for_the_first()
    {
        var (agent, workspace, ids) = Scene();
        var first = new OpenDocument(ModuleKind.Sheets, "First", new Avalonia.Controls.Panel(), new UndoStack());
        var second = new OpenDocument(ModuleKind.Sheets, "Second", new Avalonia.Controls.Panel(), new UndoStack());
        workspace.Open(first);
        workspace.Open(second);
        var resume = new TaskCompletionSource();

        var one = agent.WithDocumentAsync(new AgentTarget(ids.IdOf(first), null), null, async (_, _) =>
        {
            await resume.Task;
            return workspace.Active;
        }, TestContext.Current.CancellationToken);
        var two = agent.WithDocumentAsync(new AgentTarget(ids.IdOf(second), null), null, (_, _) => Task.FromResult(workspace.Active), TestContext.Current.CancellationToken);

        Assert.False(two.IsCompleted);
        resume.SetResult();

        Assert.Same(first, await one);
        Assert.Same(second, await two);
    }
}
