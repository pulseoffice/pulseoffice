using Avalonia;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Desktop.Composition;
using PulseOffice.Settings;
using PulseOffice.Shell;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.UI;
using PulseOffice.Testing;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Fonts;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class Round329SameDocumentTabContextTests(HeadlessDesktopSession headless)
{
    private readonly UiThreadSession _session = headless.Session;

    [Fact]
    public Task A_background_tab_of_the_same_workbook_does_not_take_the_active_tab_context() =>
        _session.Dispatch(async () =>
        {
            var dataHome = Path.Combine(Path.GetTempPath(), "pulseoffice-tabs-329-" + Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, dataHome);
            var provider = ServiceConfiguration.Register(Application.Current!, NullLoggerFactory.Instance, new FontFamilyMappingTable())
                .BuildServiceProvider(ServiceConfiguration.ProviderOptions);
            var registry = new ShellComposition(provider).Compose();
            var primary = provider.GetRequiredService<ShellWindow>();
            var workspace = provider.GetRequiredService<IDocumentWorkspace>();
            var executor = provider.GetRequiredService<CommandExecutor>();
            var context = provider.GetRequiredService<IActiveSheetContext>();
            try
            {
                primary.Show();
                await executor.ExecuteAsync(registry.Find(new CommandId("shell.file.new.sheets"))!, workspace.Context);
                Dispatcher.UIThread.RunJobs();
                await executor.ExecuteAsync(registry.Find(new CommandId("shell.window.new"))!, workspace.Context);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(2, primary.Workspace.Documents.Count);
                var first = primary.Workspace.Documents[0];
                var second = primary.Workspace.Documents[1];

                primary.Workspace.Activate(first);
                Dispatcher.UIThread.RunJobs();
                var picked = CellRange.Single(new CellAddress(4, 2));
                context.SelectRange(picked);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(picked, context.Selection);

                Assert.IsAssignableFrom<IActiveContextClaim>(second.View).ClaimActiveContext();
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(picked, context.Selection);

                primary.Workspace.Activate(second);
                Dispatcher.UIThread.RunJobs();
                Assert.NotEqual(picked, context.Selection);

                primary.Workspace.Activate(first);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(picked, context.Selection);
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
