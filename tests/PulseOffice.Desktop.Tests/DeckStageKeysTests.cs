using PulseOffice.Testing;
using Avalonia;
using Avalonia.Input;
using Avalonia.Headless;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Desktop.Composition;
using PulseOffice.Presentations.UI;
using PulseOffice.Settings;
using PulseOffice.Shell;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Fonts;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class DeckStageKeysTests : IDisposable
{
    private readonly UiThreadSession _session;
    private readonly string _dataHome = Path.Combine(Path.GetTempPath(), "pulseoffice-stagekeys-" + Guid.NewGuid().ToString("N"));

    public DeckStageKeysTests(HeadlessDesktopSession headless)
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
    public Task CancellareDueOggettiSelezionatiEUnSoloPassoDaAnnullare() => _session.Dispatch(async () =>
    {
        await using var provider = ServiceConfiguration.Register(Application.Current!, NullLoggerFactory.Instance, new FontFamilyMappingTable())
            .BuildServiceProvider(ServiceConfiguration.ProviderOptions);
        var registry = new ShellComposition(provider).Compose();
        var window = provider.GetRequiredService<ShellWindow>();
        window.Show();
        var workspace = provider.GetRequiredService<IDocumentWorkspace>();
        await provider.GetRequiredService<CommandExecutor>().ExecuteAsync(registry.Find(new CommandId("shell.file.new.presentations"))!, workspace.Context);
        Dispatcher.UIThread.RunJobs();

        var target = provider.GetRequiredService<IActiveDeckContext>().Target!;
        var view = (DeckView)workspace.Active!.View;
        var slide = target.CurrentSlide()!;
        var before = slide.Elements.Count;
        Assert.True(before >= 2);
        target.Select([.. slide.Elements.Take(2)]);
        Dispatcher.UIThread.RunJobs();

        view.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Delete });
        Assert.Equal(before - 2, slide.Elements.Count);

        target.Actions.History.Undo();
        Assert.Equal(before, slide.Elements.Count);
        window.Close();
    }, CancellationToken.None);
}
