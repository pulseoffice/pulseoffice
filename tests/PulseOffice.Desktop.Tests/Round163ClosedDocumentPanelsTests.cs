using Avalonia;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Desktop.Composition;
using PulseOffice.Settings;
using PulseOffice.Shell;
using PulseOffice.Testing;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Fonts;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class Round163ClosedDocumentPanelsTests : IDisposable
{
    private readonly UiThreadSession _session;
    private readonly string _dataHome = Path.Combine(Path.GetTempPath(), "pulseoffice-closed-panels-" + Guid.NewGuid().ToString("N"));

    public Round163ClosedDocumentPanelsTests(HeadlessDesktopSession headless)
    {
        _session = headless.Session;
        Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, _dataHome);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, null);
        if (Directory.Exists(_dataHome)) Directory.Delete(_dataHome, recursive: true);
    }

    [Theory]
    [InlineData("sheets", "ledger.view.navigator")]
    [InlineData("documents", "folio.view.navigator")]
    [InlineData("documents", "folio.view.comments")]
    [InlineData("presentations", "deck.view.navigator")]
    [InlineData("presentations", "deck.view.comments")]
    public Task A_closed_document_with_an_open_panel_is_released(string module, string panel) => _session.Dispatch(async () =>
    {
        await using var provider = ServiceConfiguration.Register(Application.Current!, NullLoggerFactory.Instance, new FontFamilyMappingTable())
            .BuildServiceProvider(ServiceConfiguration.ProviderOptions);
        var registry = new ShellComposition(provider).Compose();
        var window = provider.GetRequiredService<ShellWindow>();
        window.Show();
        var workspace = provider.GetRequiredService<IDocumentWorkspace>();
        var executor = provider.GetRequiredService<CommandExecutor>();
        var create = registry.Find(new CommandId("shell.file.new." + module))!;

        await executor.ExecuteAsync(create, workspace.Context);
        Dispatcher.UIThread.RunJobs();
        var (model, view) = await OpenAndCloseAsync(registry, workspace, executor, create, panel);

        for (var pass = 0; pass < 3; pass++)
        {
            Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
            Dispatcher.UIThread.RunJobs();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Dispatcher.UIThread.RunJobs();
        }

        Assert.Single(workspace.Documents);
        Assert.False(view.IsAlive, "the closed document's view is still alive");
        Assert.False(model.IsAlive, "the closed document's model is still alive");
        window.Close();
        Dispatcher.UIThread.RunJobs();
    }, CancellationToken.None);

    private static async Task<(WeakReference Model, WeakReference View)> OpenAndCloseAsync(
        CommandRegistry registry, IDocumentWorkspace workspace, CommandExecutor executor, CommandDefinition create, string panel)
    {
        await executor.ExecuteAsync(create, workspace.Context);
        Dispatcher.UIThread.RunJobs();
        var document = workspace.Active!;
        var references = (new WeakReference(document.Model), new WeakReference(document.View));
        await executor.ExecuteAsync(registry.Find(new CommandId(panel))!, workspace.Context);
        Dispatcher.UIThread.RunJobs();
        Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
        Dispatcher.UIThread.RunJobs();
        await executor.ExecuteAsync(registry.Find(new CommandId("shell.file.close"))!, workspace.Context);
        Dispatcher.UIThread.RunJobs();
        return references;
    }
}
