using PulseOffice.Agent.Hosting;
using PulseOffice.Agent.UI;
using PulseOffice.Document.Commands;
using PulseOffice.Foundation.Modules;
using PulseOffice.Shell;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public class Round176AgentExportTests
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

    private sealed class OneModule(IDocumentOperations module) : IDocumentOperationsCatalog
    {
        public IReadOnlyList<IDocumentOperations> All => [module];

        public IDocumentOperations? For(OpenDocument? document) => module;
    }

    private sealed class RecordingCopies : IDocumentCopyWriter
    {
        public List<string> Paths { get; } = [];

        public DocumentCopyOutcome Write(OpenDocument document, string path)
        {
            Paths.Add(path);
            return DocumentCopyOutcome.Written;
        }
    }

    [Fact]
    public async Task An_agent_export_goes_through_the_copy_writer_that_keeps_the_password()
    {
        var workspace = new DocumentWorkspace();
        var ids = new AgentDocumentIds();
        var copies = new RecordingCopies();
        var services = new ShellAgentServices(
            workspace, Fake<IDocumentOpenRouter>(), Fake<IDocumentFileServices>(), new OneModule(Fake<IDocumentOperations>()),
            Fake<PulseOffice.Formats.Pdf.IPdfExporter>(), Fake<PulseOffice.Platform.IAtomicFileWriter>(), copies,
            new PulseOffice.UI.Commands.CommandRegistry(Fake<PulseOffice.Localization.ITextCatalog>()), () => null!, ids, new InlineUi(),
            Fake<IAgentPageRenderer>(), Fake<PulseOffice.Localization.ITextCatalog>(), Fake<IUnattendedOpening>(),
            Fake<PulseOffice.UI.Commands.ICommandEffectChecks>(), new AgentOperationGate(workspace, Fake<PulseOffice.UI.Controls.Services.IDialogService>()));
        var document = new OpenDocument(ModuleKind.Sheets, "Secret", new Avalonia.Controls.Panel(), new UndoStack());
        workspace.Open(document);
        var path = Path.Combine(Path.GetTempPath(), "round176-secret.ods");

        var written = await new ShellAgentWorkspace(services).ExportAsync(ids.IdOf(document), path, TestContext.Current.CancellationToken);

        Assert.Equal(path, written);
        Assert.Equal([path], copies.Paths);
    }
}
