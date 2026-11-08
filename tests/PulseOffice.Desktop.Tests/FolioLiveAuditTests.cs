using PulseOffice.Testing;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Desktop.Composition;
using PulseOffice.Settings;
using PulseOffice.Shell;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Fonts;
using PulseOffice.UI.Controls.Ribbon;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class FolioLiveAuditTests : IDisposable
{
    private readonly UiThreadSession _session;
    private readonly string _dataHome = Path.Combine(Path.GetTempPath(), "pulseoffice-folio-live-" + Guid.NewGuid().ToString("N"));

    public FolioLiveAuditTests(HeadlessDesktopSession headless)
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
    public Task La_galleria_degli_stili_si_chiude_dopo_la_scelta() => _session.Dispatch(async () =>
    {
        await using var provider = ServiceConfiguration.Register(Application.Current!, NullLoggerFactory.Instance, new FontFamilyMappingTable())
            .BuildServiceProvider(ServiceConfiguration.ProviderOptions);
        var registry = new ShellComposition(provider).Compose();
        var window = provider.GetRequiredService<ShellWindow>();
        window.Show();
        try
        {
            await provider.GetRequiredService<CommandExecutor>().ExecuteAsync(registry.Find(new CommandId("shell.file.new.documents"))!,
                provider.GetRequiredService<IDocumentWorkspace>().Context);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            var slot = window.GetVisualDescendants().OfType<ChoiceSlot>().First(slot => slot.CommandId == "folio.style.paragraph" && slot.IsEffectivelyVisible);
            var opener = slot.GetVisualDescendants().OfType<Button>().First();
            opener.Focus();
            opener.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            var flyout = (Flyout)typeof(ChoiceSlot).GetField("_flyout", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(slot)!;
            Assert.True(flyout.IsOpen);
            var tile = ((Control)flyout.Content!).GetLogicalDescendants().OfType<Button>()
                .First(button => ToolTip.GetTip(button) is string tip && tip.StartsWith("Titolo", StringComparison.Ordinal) || ToolTip.GetTip(button) is string en && en.StartsWith("Title", StringComparison.Ordinal));
            tile.Focus();
            tile.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            Assert.False(flyout.IsOpen);
            AssertDocumentFocused(window);
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<ChoiceSlot>().Where(other => other.CommandId == "folio.style.paragraph")
                .Select(other => typeof(ChoiceSlot).GetField("_flyout", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(other)).OfType<Flyout>(), open => open.IsOpen);
        }
        finally
        {
            window.Close();
        }
    }, CancellationToken.None);

    [Fact]
    public Task Dopo_un_pulsante_della_ribbon_si_continua_a_scrivere_nel_documento() => _session.Dispatch(async () =>
    {
        await using var provider = ServiceConfiguration.Register(Application.Current!, NullLoggerFactory.Instance, new FontFamilyMappingTable())
            .BuildServiceProvider(ServiceConfiguration.ProviderOptions);
        var registry = new ShellComposition(provider).Compose();
        var window = provider.GetRequiredService<ShellWindow>();
        window.Show();
        try
        {
            await provider.GetRequiredService<CommandExecutor>().ExecuteAsync(registry.Find(new CommandId("shell.file.new.documents"))!,
                provider.GetRequiredService<IDocumentWorkspace>().Context);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            var bold = window.GetVisualDescendants().OfType<RibbonSlot>().First(slot => slot.CommandId == "folio.format.bold" && slot.IsEffectivelyVisible)
                .GetVisualDescendants().OfType<Button>().First();
            bold.Focus();
            bold.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            AssertDocumentFocused(window);
        }
        finally
        {
            window.Close();
        }
    }, CancellationToken.None);

    [Fact]
    public Task Maiusc_F6_dal_documento_porta_alla_ribbon_le_frecce_cambiano_scheda_ed_Esc_torna_al_documento() => _session.Dispatch(async () =>
    {
        await using var provider = ServiceConfiguration.Register(Application.Current!, NullLoggerFactory.Instance, new FontFamilyMappingTable())
            .BuildServiceProvider(ServiceConfiguration.ProviderOptions);
        var registry = new ShellComposition(provider).Compose();
        var window = provider.GetRequiredService<ShellWindow>();
        window.Show();
        try
        {
            await provider.GetRequiredService<CommandExecutor>().ExecuteAsync(registry.Find(new CommandId("shell.file.new.documents"))!,
                provider.GetRequiredService<IDocumentWorkspace>().Context);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            AssertDocumentFocused(window);

            window.KeyPress(Avalonia.Input.Key.F6, Avalonia.Input.RawInputModifiers.Shift, Avalonia.Input.PhysicalKey.F6, null);
            Dispatcher.UIThread.RunJobs();
            var strip = Assert.IsType<RibbonTabStrip>(window.FocusManager?.GetFocusedElement());
            var before = strip.Current;

            window.KeyPress(Avalonia.Input.Key.Right, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.ArrowRight, null);
            Dispatcher.UIThread.RunJobs();
            Assert.NotEqual(before, strip.Current);

            window.KeyPress(Avalonia.Input.Key.Escape, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.Escape, null);
            Dispatcher.UIThread.RunJobs();
            AssertDocumentFocused(window);
        }
        finally
        {
            window.Close();
        }
    }, CancellationToken.None);

    private static void AssertDocumentFocused(Window window)
    {
        var focused = window.FocusManager?.GetFocusedElement() as Visual;
        Assert.NotNull(focused);
        Assert.Contains(focused!.GetSelfAndVisualAncestors(), visual => visual is PulseOffice.Documents.UI.FolioView);
    }
}
