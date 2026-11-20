using PulseOffice.Testing;
using Avalonia;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Desktop.Composition;
using PulseOffice.Settings;
using PulseOffice.Shell;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Fonts;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class Round300SecondWindowTests(HeadlessDesktopSession headless)
{
    private readonly UiThreadSession _session = headless.Session;

    [Fact]
    public Task Every_module_opens_a_real_second_window_of_the_same_document_and_closes_it() => _session.Dispatch(async () =>
    {
        var dataHome = Path.Combine(Path.GetTempPath(), "pulseoffice-window-300-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, dataHome);
        var provider = ServiceConfiguration.Register(Application.Current!, NullLoggerFactory.Instance, new FontFamilyMappingTable())
            .BuildServiceProvider(ServiceConfiguration.ProviderOptions);
        var registry = new ShellComposition(provider).Compose();
        var window = provider.GetRequiredService<ShellWindow>();
        try
        {
            window.Show();
            var workspace = provider.GetRequiredService<IDocumentWorkspace>();
            var executor = provider.GetRequiredService<CommandExecutor>();

            async Task Run(string id)
            {
                await executor.ExecuteAsync(registry.Find(new CommandId(id))!, workspace.Context);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
            }

            foreach (var (module, viewType) in new[]
            {
                ("sheets", typeof(PulseOffice.Sheets.UI.LedgerView)),
                ("documents", typeof(PulseOffice.Documents.UI.FolioView)),
                ("presentations", typeof(PulseOffice.Presentations.UI.DeckView)),
            })
            {
                await Run($"shell.file.new.{module}");
                var first = workspace.Active!;

                await Run("shell.window.new");

                var second = workspace.Active!;
                Assert.NotSame(first, second);
                Assert.IsType(viewType, second.View);
                Assert.NotSame(first.View, second.View);
                Assert.Same(first.History, second.History);
                Assert.Same(first.Model, second.Model);
                Assert.True(second.View.IsEffectivelyVisible);
                if (first.View is PulseOffice.Sheets.UI.LedgerView ledger)
                    Assert.Same(ledger.Engine, ((PulseOffice.Sheets.UI.LedgerView)second.View).Engine);

                await Run("shell.window.close");

                Assert.DoesNotContain(second, workspace.Documents);
                Assert.True(((IDocumentViewRelease)second.View).IsSharedModelReleased);
                Assert.False(((IDocumentViewRelease)first.View).IsSharedModelReleased);
                Assert.Contains(first, workspace.Documents);
                Assert.False(registry.Find(new CommandId("shell.window.close"))!.IsEnabled(workspace.Context));
            }
        }
        finally
        {
            window.Close();
            await provider.DisposeAsync();
            Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, null);
            if (Directory.Exists(dataHome)) Directory.Delete(dataHome, recursive: true);
        }
    }, CancellationToken.None);
}
