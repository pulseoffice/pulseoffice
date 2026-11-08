using PulseOffice.Testing;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Desktop.Composition;
using PulseOffice.Settings;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Shell;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Commands.Shortcuts;
using PulseOffice.UI.Controls.Fonts;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class LedgerSelectionShortcutTests : IDisposable
{
    private readonly UiThreadSession Session;

    private readonly string _dataHome = Path.Combine(Path.GetTempPath(), "pulseoffice-shortcuts-" + Guid.NewGuid().ToString("N"));

    public LedgerSelectionShortcutTests(HeadlessDesktopSession headless)
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
    public Task Nessuna_scorciatoia_di_fabbrica_e_riservata_dal_sistema() => Session.Dispatch(async () =>
    {
        await using var provider = ServiceConfiguration.Register(Application.Current!, NullLoggerFactory.Instance, new FontFamilyMappingTable())
            .BuildServiceProvider(ServiceConfiguration.ProviderOptions);
        var registry = new ShellComposition(provider).Compose();
        var parser = new GestureChordParser();

        foreach (var reserved in new[] { ReservedShortcutTable.ForSystem(macOS: true), ReservedShortcutTable.ForSystem(macOS: false) })
        {
            var clashes = registry.All
                .Where(command => command.DefaultGesture is { } gesture && parser.Parse(gesture) is { } chord && reserved.IsReserved(chord))
                .Select(command => $"{command.Id.Value} {command.DefaultGesture}")
                .ToList();
            Assert.Empty(clashes);
        }
    }, CancellationToken.None);

    [Fact]
    public Task Maiuscolo_spazio_seleziona_la_riga_senza_scrivere_nella_cella() => Session.Dispatch(async () =>
    {
        await using var provider = ServiceConfiguration.Register(Application.Current!, NullLoggerFactory.Instance, new FontFamilyMappingTable())
            .BuildServiceProvider(ServiceConfiguration.ProviderOptions);
        var registry = new ShellComposition(provider).Compose();
        var window = provider.GetRequiredService<ShellWindow>();
        window.Show();
        var workspace = provider.GetRequiredService<IDocumentWorkspace>();
        await provider.GetRequiredService<CommandExecutor>().ExecuteAsync(registry.Find(new CommandId("shell.file.new.sheets"))!, workspace.Context);
        ((PulseOffice.UI.Controls.Services.IDocumentFocusTarget)workspace.Active!.View).FocusDocument();
        Dispatcher.UIThread.RunJobs();
        var sheet = provider.GetRequiredService<PulseOffice.Sheets.UI.IActiveSheetContext>();

        window.KeyPress(Key.Space, RawInputModifiers.Shift, PhysicalKey.Space, " ");
        window.KeyTextInput(" ");
        window.KeyRelease(Key.Space, RawInputModifiers.Shift, PhysicalKey.Space, " ");
        Dispatcher.UIThread.RunJobs();
        var selection = sheet.Selection;
        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(CellAddress.MaxColumns - 1, selection.End.Column);
        Assert.True(sheet.Workbook!.Sheets[0].GetValue(new CellAddress(0, 0)).IsEmpty);
        window.Close();
    }, CancellationToken.None);
}
