using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Desktop.Composition;
using PulseOffice.Settings;
using PulseOffice.Shell;
using PulseOffice.Testing;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Fonts;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class Round314TornOffWindowTests(HeadlessDesktopSession headless)
{
    private readonly UiThreadSession _session = headless.Session;

    [Fact]
    public Task A_torn_off_window_has_its_own_side_panel_and_is_released_once_closed() => _session.Dispatch(async () =>
    {
        var dataHome = Path.Combine(Path.GetTempPath(), "pulseoffice-windows-314-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, dataHome);
        var provider = ServiceConfiguration.Register(Application.Current!, NullLoggerFactory.Instance, new FontFamilyMappingTable())
            .BuildServiceProvider(ServiceConfiguration.ProviderOptions);
        var registry = new ShellComposition(provider).Compose();
        var primary = provider.GetRequiredService<ShellWindow>();
        var workspace = provider.GetRequiredService<IDocumentWorkspace>();
        var executor = provider.GetRequiredService<CommandExecutor>();
        try
        {
            primary.Show();
            for (var count = 0; count < 2; count++)
            {
                await executor.ExecuteAsync(registry.Find(new CommandId("shell.file.new.sheets"))!, workspace.Context);
                Dispatcher.UIThread.RunJobs();
            }

            var released = TearOffShowPanelAndMoveBack(provider, primary);
            for (var pass = 0; pass < 3; pass++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Dispatcher.UIThread.RunJobs();
            }

            Assert.False(released.IsAlive, "the closed torn-off window is still referenced");
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference TearOffShowPanelAndMoveBack(IServiceProvider provider, ShellWindow primary)
    {
        var host = provider.GetRequiredService<IShellWindowHost>();
        var panel = provider.GetRequiredService<ISidePanelHost>();
        var torn = primary.Workspace.Documents[1];
        var second = Assert.IsType<ShellWindow>(host.TearOff(torn, new PixelPoint(60, 60)));
        Dispatcher.UIThread.RunJobs();

        var primaryPanel = primary.GetVisualDescendants().OfType<SidePanelView>().Single();
        var secondPanel = second.GetVisualDescendants().OfType<SidePanelView>().Single();
        Assert.Equal(primaryPanel.Pages, secondPanel.Pages);
        Assert.NotEmpty(secondPanel.Pages);
        panel.Show(secondPanel.Pages[0]);
        Assert.True(secondPanel.IsOpen);
        Assert.False(primaryPanel.IsOpen);
        panel.Close();

        Assert.True(host.MoveTo(torn, primary, -1));
        Dispatcher.UIThread.RunJobs();
        Assert.DoesNotContain(second, host.Windows);
        return new WeakReference(second);
    }
}
