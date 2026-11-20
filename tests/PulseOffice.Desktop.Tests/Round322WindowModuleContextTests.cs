using Avalonia;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Desktop.Composition;
using PulseOffice.Documents.UI;
using PulseOffice.Presentations.UI;
using PulseOffice.Settings;
using PulseOffice.Shell;
using PulseOffice.Sheets.UI;
using PulseOffice.Testing;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Fonts;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class Round322WindowModuleContextTests(HeadlessDesktopSession headless)
{
    private readonly UiThreadSession _session = headless.Session;

    [Fact]
    public Task An_unfocused_ledger_window_attaching_keeps_the_focused_window_workbook() =>
        Run("shell.file.new.sheets", provider =>
        {
            var context = provider.GetRequiredService<IActiveSheetContext>();
            var changes = 0;
            context.TargetChanged += (_, _) => changes++;
            return (() => context.Workbook, () => changes);
        });

    [Fact]
    public Task An_unfocused_folio_window_attaching_keeps_the_focused_window_document() =>
        Run("shell.file.new.documents", provider =>
        {
            var context = provider.GetRequiredService<IActiveFolioContext>();
            var changes = 0;
            context.Changed += (_, _) => changes++;
            return (() => context.Target?.Editor.Document, () => changes);
        });

    [Fact]
    public Task An_unfocused_deck_window_attaching_keeps_the_focused_window_presentation() =>
        Run("shell.file.new.presentations", provider =>
        {
            var context = provider.GetRequiredService<IActiveDeckContext>();
            var changes = 0;
            context.Changed += (_, _) => changes++;
            return (() => context.Target?.Deck, () => changes);
        });

    private Task Run(string newCommand, Func<IServiceProvider, (Func<object?> Model, Func<int> Changes)> observe) =>
        _session.Dispatch(async () =>
        {
            var dataHome = Path.Combine(Path.GetTempPath(), "pulseoffice-windows-322-" + Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, dataHome);
            var provider = ServiceConfiguration.Register(Application.Current!, NullLoggerFactory.Instance, new FontFamilyMappingTable())
                .BuildServiceProvider(ServiceConfiguration.ProviderOptions);
            var registry = new ShellComposition(provider).Compose();
            var primary = provider.GetRequiredService<ShellWindow>();
            var workspace = provider.GetRequiredService<IDocumentWorkspace>();
            var executor = provider.GetRequiredService<CommandExecutor>();
            var host = provider.GetRequiredService<IShellWindowHost>();
            var (model, changes) = observe(provider);
            try
            {
                primary.Show();
                for (var count = 0; count < 2; count++)
                {
                    await executor.ExecuteAsync(registry.Find(new CommandId(newCommand))!, workspace.Context);
                    Dispatcher.UIThread.RunJobs();
                }

                var torn = primary.Workspace.Documents[1];
                var second = Assert.IsType<ShellWindow>(host.TearOff(torn, new PixelPoint(60, 60)));
                Dispatcher.UIThread.RunJobs();
                Assert.Same(torn.Model, model());

                primary.Activate();
                Dispatcher.UIThread.RunJobs();
                var kept = primary.Workspace.Active!.Model;
                Assert.Same(kept, model());

                var before = changes();
                Assert.IsAssignableFrom<IActiveContextClaim>(torn.View).ClaimActiveContext();
                Dispatcher.UIThread.RunJobs();
                Assert.Same(kept, model());
                Assert.Equal(before, changes());

                second.Activate();
                Dispatcher.UIThread.RunJobs();
                Assert.Same(torn.Model, model());

                primary.Activate();
                Dispatcher.UIThread.RunJobs();
                Assert.Same(kept, model());
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
