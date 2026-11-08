using PulseOffice.Testing;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Automation.Peers;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using PulseOffice.UI.Controls.Settings;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class SwitchToggleTests
{
    private static UiThreadSession Session => MacroEventsViewTests.Session;

    private static (Window Window, SwitchToggle Toggle) Open()
    {
        var toggle = new SwitchToggle();
        var window = new Window { Width = 200, Height = 100, Content = toggle };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        toggle.Focus();
        Dispatcher.UIThread.RunJobs();
        return (window, toggle);
    }

    [Fact]
    public Task LaBarraSpaziatriceLoCommuta() => Session.Dispatch(() =>
    {
        var (window, toggle) = Open();

        window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");

        Assert.True(toggle.IsOn);
    }, CancellationToken.None);

    [Fact]
    public Task SpentoNonSiCommuta() => Session.Dispatch(() =>
    {
        var (window, toggle) = Open();
        toggle.IsEnabled = false;

        window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
        ((IToggleProvider)ControlAutomationPeer.CreatePeerForElement(toggle)).Toggle();

        Assert.False(toggle.IsOn);
    }, CancellationToken.None);

    [Fact]
    public Task SiPresentaComeCasellaCommutabile() => Session.Dispatch(() =>
    {
        var (_, toggle) = Open();
        var peer = ControlAutomationPeer.CreatePeerForElement(toggle);

        ((IToggleProvider)peer).Toggle();

        Assert.True(toggle.IsOn);
        Assert.Equal(Avalonia.Automation.Peers.AutomationControlType.CheckBox, peer.GetAutomationControlType());
        Assert.Equal(ToggleState.On, ((IToggleProvider)peer).ToggleState);
    }, CancellationToken.None);
}
