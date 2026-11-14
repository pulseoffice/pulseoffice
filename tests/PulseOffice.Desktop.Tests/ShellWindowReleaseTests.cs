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
public sealed class ShellWindowReleaseTests : IDisposable
{
    private readonly UiThreadSession _session;
    private readonly string _dataHome = Path.Combine(Path.GetTempPath(), "pulseoffice-release-" + Guid.NewGuid().ToString("N"));

    public ShellWindowReleaseTests(HeadlessDesktopSession headless)
    {
        _session = headless.Session;
        Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, _dataHome);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, null);
        if (Directory.Exists(_dataHome)) Directory.Delete(_dataHome, recursive: true);
    }

    [Fact]
    public Task A_closed_window_and_its_container_are_released() => _session.Dispatch(async () =>
    {
        var windows = new List<WeakReference>();
        for (var round = 0; round < 4; round++) windows.Add(await OpenAndCloseAsync());
        for (var pass = 0; pass < 3; pass++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Dispatcher.UIThread.RunJobs();
        }

        Assert.True(windows.Count(window => window.IsAlive) <= 1, $"{windows.Count(window => window.IsAlive)} of {windows.Count} closed windows are still alive");
    }, CancellationToken.None);

    private static async Task<WeakReference> OpenAndCloseAsync()
    {
        await using var provider = ServiceConfiguration.Register(Application.Current!, NullLoggerFactory.Instance, new FontFamilyMappingTable())
            .BuildServiceProvider(ServiceConfiguration.ProviderOptions);
        var registry = new ShellComposition(provider).Compose();
        var window = provider.GetRequiredService<ShellWindow>();
        window.Show();
        var workspace = provider.GetRequiredService<IDocumentWorkspace>();
        await provider.GetRequiredService<CommandExecutor>().ExecuteAsync(registry.Find(new CommandId("shell.file.new.sheets"))!, workspace.Context);
        Dispatcher.UIThread.RunJobs();
        provider.GetRequiredService<PulseOffice.UI.Controls.Services.IStatusBarService>().Report("closing soon");
        var reference = new WeakReference(window);
        window.Close();
        Dispatcher.UIThread.RunJobs();
        return reference;
    }
}
