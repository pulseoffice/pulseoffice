using Avalonia;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Desktop.Composition;
using PulseOffice.Presentations.UI;
using PulseOffice.Settings;
using PulseOffice.Shell;
using PulseOffice.Testing;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Fonts;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class Round321WindowContextClaimTests(HeadlessDesktopSession headless)
{
    private readonly UiThreadSession _session = headless.Session;

    [Fact]
    public Task Activating_a_window_gives_the_deck_commands_its_own_presentation() => _session.Dispatch(async () =>
    {
        var dataHome = Path.Combine(Path.GetTempPath(), "pulseoffice-windows-321-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, dataHome);
        var provider = ServiceConfiguration.Register(Application.Current!, NullLoggerFactory.Instance, new FontFamilyMappingTable())
            .BuildServiceProvider(ServiceConfiguration.ProviderOptions);
        var registry = new ShellComposition(provider).Compose();
        var primary = provider.GetRequiredService<ShellWindow>();
        var workspace = provider.GetRequiredService<IDocumentWorkspace>();
        var executor = provider.GetRequiredService<CommandExecutor>();
        var decks = provider.GetRequiredService<IActiveDeckContext>();
        var host = provider.GetRequiredService<IShellWindowHost>();
        try
        {
            primary.Show();
            for (var count = 0; count < 2; count++)
            {
                await executor.ExecuteAsync(registry.Find(new CommandId("shell.file.new.presentations"))!, workspace.Context);
                Dispatcher.UIThread.RunJobs();
            }

            var torn = primary.Workspace.Documents[1];
            var second = Assert.IsType<ShellWindow>(host.TearOff(torn, new PixelPoint(60, 60)));
            Dispatcher.UIThread.RunJobs();
            Assert.Same(torn.Model, decks.Target?.Deck);

            primary.Activate();
            Dispatcher.UIThread.RunJobs();
            Assert.Same(primary.Workspace.Active!.Model, decks.Target?.Deck);

            second.Activate();
            Dispatcher.UIThread.RunJobs();
            Assert.Same(torn.Model, decks.Target?.Deck);
        }
        finally
        {
            foreach (var document in workspace.Documents.ToList()) workspace.Close(document);
            Dispatcher.UIThread.RunJobs();
            primary.Close();
            Dispatcher.UIThread.RunJobs();
            await provider.DisposeAsync();
            Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, null);
            if (Directory.Exists(dataHome)) Directory.Delete(dataHome, recursive: true);
        }
    }, CancellationToken.None);
}
