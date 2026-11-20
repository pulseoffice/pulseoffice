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
public sealed class Round310TwoWindowTests(HeadlessDesktopSession headless)
{
    private readonly UiThreadSession _session = headless.Session;

    [Fact]
    public Task A_torn_off_tab_lives_in_a_real_second_window_with_its_own_scope() => _session.Dispatch(async () =>
    {
        var dataHome = Path.Combine(Path.GetTempPath(), "pulseoffice-windows-310-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, dataHome);
        var provider = ServiceConfiguration.Register(Application.Current!, NullLoggerFactory.Instance, new FontFamilyMappingTable())
            .BuildServiceProvider(ServiceConfiguration.ProviderOptions);
        var registry = new ShellComposition(provider).Compose();
        var primary = provider.GetRequiredService<ShellWindow>();
        var host = provider.GetRequiredService<IShellWindowHost>();
        var focus = provider.GetRequiredService<IShellWindowFocus>();
        var workspace = provider.GetRequiredService<IDocumentWorkspace>();
        var executor = provider.GetRequiredService<CommandExecutor>();
        ShellWindow? second = null;
        try
        {
            primary.Show();

            async Task Run(string id)
            {
                await executor.ExecuteAsync(registry.Find(new CommandId(id))!, workspace.Context);
                Dispatcher.UIThread.RunJobs();
            }

            await Run("shell.file.new.sheets");
            await Run("shell.file.new.sheets");
            var kept = primary.Workspace.Documents[0];
            var torn = primary.Workspace.Documents[1];

            second = Assert.IsType<ShellWindow>(host.TearOff(torn, new PixelPoint(40, 40)));
            Dispatcher.UIThread.RunJobs();

            Assert.NotSame(primary, second);
            Assert.True(second.IsVisible);
            Assert.Same(second, host.Focused);
            Assert.Same(second.Scope.Workspace, focus.Current.Workspace);
            Assert.NotSame(primary.Scope.Dialogs, second.Scope.Dialogs);
            Assert.Equal(new[] { kept }, primary.Workspace.Documents);
            Assert.Equal(new[] { torn }, second.Workspace.Documents);
            Assert.Same(torn, workspace.Active);
            Assert.Equal(2, workspace.Documents.Count);
            Assert.Same(torn.View, second.WorkspaceHost.Children.Single());

            await Run("shell.file.new.sheets");
            Assert.Equal(2, second.Workspace.Documents.Count);
            Assert.Single(primary.Workspace.Documents);

            var added = second.Workspace.Active!;
            Assert.True(host.MoveTo(added, primary, 0));
            Assert.Same(primary, host.Focused);
            Assert.Same(added, workspace.Active);
            Assert.Equal(new[] { added, kept }, primary.Workspace.Documents);

            torn.MarkModified();
            second.Close();
            Dispatcher.UIThread.RunJobs();
            Assert.Contains(second, host.Windows);
            Assert.True(second.Scope.Dialogs.IsDialogOpen || primary.Scope.Dialogs.IsDialogOpen);
            provider.GetRequiredService<PulseOffice.UI.Controls.Services.IDialogService>().Close(null);
            Dispatcher.UIThread.RunJobs();
            Assert.Contains(second, host.Windows);

            Assert.True(host.MoveTo(torn, primary, -1));
            Dispatcher.UIThread.RunJobs();
            Assert.DoesNotContain(second, host.Windows);
            Assert.Single(focus.Scopes);
            Assert.Equal(3, workspace.Documents.Count);
            second = null;
        }
        finally
        {
            foreach (var document in workspace.Documents.ToList()) workspace.Close(document);
            Dispatcher.UIThread.RunJobs();
            second?.Close();
            primary.Close();
            Dispatcher.UIThread.RunJobs();
            Assert.False(primary.IsVisible);
            await provider.DisposeAsync();
            Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, null);
            if (Directory.Exists(dataHome)) Directory.Delete(dataHome, recursive: true);
        }
    }, CancellationToken.None);
}
