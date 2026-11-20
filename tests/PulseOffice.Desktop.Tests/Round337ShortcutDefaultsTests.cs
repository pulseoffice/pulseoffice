using PulseOffice.Testing;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Desktop.Composition;
using PulseOffice.Foundation.Modules;
using PulseOffice.Settings;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Fonts;
using PulseOffice.UI.Controls.Shapes;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class Round337ShortcutDefaultsTests : IDisposable
{
    private readonly UiThreadSession Session;

    private readonly string _dataHome = Path.Combine(Path.GetTempPath(), "pulseoffice-r337-" + Guid.NewGuid().ToString("N"));

    public Round337ShortcutDefaultsTests(HeadlessDesktopSession headless)
    {
        Session = headless.Session;
        Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, _dataHome);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, null);
        if (Directory.Exists(_dataHome)) Directory.Delete(_dataHome, recursive: true);
    }

    [Fact]
    public Task New_has_its_shortcut_and_a_rebound_nudge_reaches_the_views() => Session.Dispatch(async () =>
    {
        await using var provider = ServiceConfiguration.Register(Application.Current!, NullLoggerFactory.Instance, new FontFamilyMappingTable())
            .BuildServiceProvider(ServiceConfiguration.ProviderOptions);
        var registry = new ShellComposition(provider).Compose();

        Assert.Equal("Ctrl+N", registry.Find(new CommandId("shell.file.newBlank"))!.DefaultGesture);
        foreach (var (find, replace) in new[] { ("folio.edit.find", "folio.edit.findReplace"), ("ledger.data.find", "ledger.data.findReplace"), ("deck.edit.find", "deck.edit.findReplace") })
        {
            Assert.Equal("Ctrl+F", registry.Find(new CommandId(find))!.DefaultGesture);
            Assert.Equal("Ctrl+Shift+H", registry.Find(new CommandId(replace))!.DefaultGesture);
        }

        var nudges = provider.GetRequiredService<IObjectNudgeCommands>();
        var keys = provider.GetRequiredService<IObjectNudgeKeys>();
        Assert.True(registry.Rebind(nudges.IdOf(ModuleKind.Presentations, "moveLeft"), "Ctrl+Alt+J"));

        Assert.Null(keys.Read(ModuleKind.Presentations, Key.Left, KeyModifiers.None));
        Assert.NotNull(keys.Read(ModuleKind.Sheets, Key.Left, KeyModifiers.None));
    }, CancellationToken.None);
}
