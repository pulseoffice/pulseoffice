using PulseOffice.Agent.UI;
using PulseOffice.Document.Commands;
using PulseOffice.Foundation.Modules;
using PulseOffice.Platform;
using PulseOffice.Shell;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public class AgentOpenPathCaseTests
{
    private static T Fake<T>() where T : class => System.Reflection.DispatchProxy.Create<T, AgentDocumentHoldTests.Inert>();

    private sealed class InlineUi : IAgentUiThread
    {
        public Task<T> InvokeAsync<T>(Func<Task<T>> work) => work();
    }

    private sealed class Opener(IDocumentWorkspace workspace, string storedPath) : IWorkspaceOpener
    {
        public IReadOnlyList<FileFilter> ReadableFilters => [];

        public bool Handles(string path) => path.EndsWith(".ods", StringComparison.OrdinalIgnoreCase);

        public Task OpenAsync(string path, CancellationToken ct = default)
        {
            var document = new OpenDocument(ModuleKind.Sheets, "Report", new Avalonia.Controls.Panel(), new UndoStack());
            document.MarkSaved(storedPath, Path.GetFileName(storedPath));
            workspace.Open(document);
            return Task.CompletedTask;
        }

        public Task<bool> OpenAsNewAsync(string path, CancellationToken ct = default) => Task.FromResult(false);
    }

    [Fact]
    public async Task Opening_a_file_with_a_different_letter_case_finds_the_opened_document_where_paths_ignore_case()
    {
        if (OperatingSystem.IsLinux()) return;
        var folder = Directory.CreateTempSubdirectory("pulse-agent-case");
        var stored = Path.Combine(folder.FullName, "Report.ods");
        await File.WriteAllBytesAsync(stored, [], TestContext.Current.CancellationToken);
        var requested = Path.Combine(folder.FullName, "REPORT.ODS");
        if (!File.Exists(requested)) return;

        var workspace = new DocumentWorkspace();
        var router = new DocumentOpenRouter([new Opener(workspace, stored)], Fake<IFileDialogService>(),
            Fake<PulseOffice.Localization.ITextCatalog>(), workspace, Fake<ITemplateLibrary>(), new PulseOffice.Platform.RuntimeHostSystem());
        var services = new ShellAgentServices(
            workspace, router, Fake<IDocumentFileServices>(), Fake<IDocumentOperationsCatalog>(),
            Fake<PulseOffice.Formats.Pdf.IPdfExporter>(), Fake<IAtomicFileWriter>(),
            new PulseOffice.UI.Commands.CommandRegistry(Fake<PulseOffice.Localization.ITextCatalog>()), () => null!, new AgentDocumentIds(), new InlineUi(),
            Fake<IAgentPageRenderer>(), Fake<PulseOffice.Localization.ITextCatalog>(), Fake<IUnattendedOpening>(),
            Fake<PulseOffice.UI.Commands.ICommandEffectChecks>(), new AgentOperationGate(workspace, Fake<PulseOffice.UI.Controls.Services.IDialogService>()));

        var info = await new ShellAgentWorkspace(services).OpenAsync(requested, new Dictionary<string, string>(), TestContext.Current.CancellationToken);

        Assert.Equal("Report", info.Title);
    }
}
