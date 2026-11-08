using PulseOffice.Testing;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Icons;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class DialogFocusTests
{
    private static UiThreadSession Session => MacroEventsViewTests.Session;

    private sealed record Scene(Window Window, DialogHost Host, TextBox Outside, TextBox Inside, Task<object?> Result);

    private static Scene Open()
    {
        var texts = new TextCatalog(UiLanguage.English);
        var host = new DialogHost();
        host.Use(new IconCatalog(), texts);
        var outside = new TextBox { Name = "Outside" };
        var root = new DockPanel();
        DockPanel.SetDock(outside, Dock.Top);
        root.Children.Add(outside);
        root.Children.Add(host);
        var window = new Window { Width = 1000, Height = 700, Content = root };
        window.Show();

        var inside = new TextBox { Name = "Inside" };
        var result = host.ShowAsync<object>(new StackPanel { Children = { inside, new Button { Content = "Ok" } } },
            new DialogOptions { TitleKey = "command.ledger.data.consolidate.title" });
        Pump(window);
        return new Scene(window, host, outside, inside, result);
    }

    private static void Pump(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public Task Il_fuoco_non_esce_dalla_modale() => Session.Dispatch(() =>
    {
        var scene = Open();
        Assert.True(scene.Inside.IsFocused);

        scene.Outside.Focus();
        Pump(scene.Window);
        var focused = scene.Window.FocusManager?.GetFocusedElement() as Avalonia.Visual;
        Assert.NotNull(focused);
        Assert.NotSame(scene.Outside, focused);
        scene.Window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task Un_menu_a_tendina_nella_modale_si_apre_e_sceglie() => Session.Dispatch(() =>
    {
        var scene = Open();
        var combo = new ComboBox { ItemsSource = new[] { "Uno", "Due", "Tre" }, SelectedIndex = 0 };
        ((Panel)scene.Inside.Parent!).Children.Add(combo);
        Pump(scene.Window);

        combo.Focus();
        combo.IsDropDownOpen = true;
        Pump(scene.Window);
        var item = combo.ContainerFromIndex(2) as ComboBoxItem;
        Assert.NotNull(item);
        item!.Focus();
        Pump(scene.Window);
        Assert.True(combo.IsDropDownOpen);
        scene.Window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task Esc_chiude_la_modale_anche_se_il_fuoco_si_e_perso() => Session.Dispatch(() =>
    {
        var scene = Open();
        ((Panel)scene.Inside.Parent!).Children.Remove(scene.Inside);
        Pump(scene.Window);

        scene.Window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Pump(scene.Window);
        Assert.False(scene.Host.IsDialogOpen);
        Assert.True(scene.Result.IsCompleted);
        scene.Window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task Senza_campi_di_testo_il_fuoco_va_al_primo_controllo_e_Tab_resta_nella_modale() => Session.Dispatch(() =>
    {
        var texts = new TextCatalog(UiLanguage.English);
        var host = new DialogHost();
        host.Use(new IconCatalog(), texts);
        var outside = new TextBox { Name = "Outside" };
        var root = new DockPanel();
        DockPanel.SetDock(outside, Dock.Top);
        root.Children.Add(outside);
        root.Children.Add(host);
        var window = new Window { Width = 1000, Height = 700, Content = root };
        window.Show();
        outside.Focus();
        Pump(window);

        var combo = new ComboBox { ItemsSource = new[] { "Uno", "Due" }, SelectedIndex = 0 };
        var content = new StackPanel { Children = { combo, new Button { Content = "Ok" } } };
        _ = host.ShowAsync<object>(content, new DialogOptions { TitleKey = "command.ledger.data.consolidate.title" });
        Pump(window);

        Assert.True(combo.IsFocused);

        window.KeyPress(Key.Down, RawInputModifiers.None, PhysicalKey.ArrowDown, null);
        Pump(window);
        Assert.Equal(1, combo.SelectedIndex);

        for (var press = 0; press < 6; press++)
        {
            window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
            Pump(window);
            var focused = window.FocusManager?.GetFocusedElement() as Avalonia.Visual;
            Assert.NotNull(focused);
            Assert.NotSame(outside, focused);
        }

        window.Close();
    }, CancellationToken.None);
}
