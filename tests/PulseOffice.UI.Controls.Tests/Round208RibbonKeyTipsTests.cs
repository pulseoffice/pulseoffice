using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using PulseOffice.Localization;
using PulseOffice.Testing;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Ribbon;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class Round208RibbonKeyTipsTests
{
    private static UiThreadSession Session => MacroEventsViewTests.Session;

    private sealed record Scene(Window Window, RibbonView Ribbon, TextBox Editor, List<string> Invoked);

    private static RibbonItem Item(string id, string title) =>
        new(new CommandDefinition { Id = new CommandId(id), Execute = (_, _) => Task.CompletedTask }, true, title, null);

    private static Scene Open()
    {
        var services = new RibbonServices(
            new IconCatalog(), new TextCatalog(UiLanguage.English), null, new RibbonStripLayout(), new RibbonReductionPlanner(),
            new RibbonSlotCatalog([new CommandSlotFactory()]));
        var ribbon = new RibbonView(services);
        var invoked = new List<string>();
        ribbon.CommandInvoked += (_, invocation) => invoked.Add(invocation.CommandId);
        var editor = new TextBox();
        var root = new DockPanel();
        DockPanel.SetDock(ribbon, Dock.Top);
        root.Children.Add(ribbon);
        root.Children.Add(editor);
        var window = new Window { Width = 900, Height = 500, Content = root };
        window.Show();
        ribbon.Update(new RibbonModel(
        [
            new RibbonTab("Home", [new RibbonGroup("Clipboard", [Item("test.copy", "Copy"), Item("test.paste", "Paste")])]),
            new RibbonTab("Insert", [new RibbonGroup("Objects", [Item("test.table", "Table"), Item("test.picture", "Picture")])]),
        ]));
        editor.Focus();
        Pump(window);
        return new Scene(window, ribbon, editor, invoked);
    }

    private static void Pump(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static void Tap(Window window, Key key, PhysicalKey physical, string? symbol, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPress(key, modifiers, physical, symbol);
        window.KeyRelease(key, modifiers, physical, symbol);
        Pump(window);
    }

    private static void TapAlt(Window window)
    {
        window.KeyPress(Key.LeftAlt, RawInputModifiers.Alt, PhysicalKey.AltLeft, null);
        window.KeyRelease(Key.LeftAlt, RawInputModifiers.None, PhysicalKey.AltLeft, null);
        Pump(window);
    }

    [Fact]
    public Task Alt_shows_tab_tips_a_letter_opens_the_tab_and_a_second_runs_the_command() => Session.Dispatch(() =>
    {
        var scene = Open();

        TapAlt(scene.Window);
        Assert.Equal(KeyTipLevel.Tabs, scene.Ribbon.KeyTips.Level);
        Assert.Equal(new[] { "H", "I" }, scene.Ribbon.KeyTips.Tips);

        Tap(scene.Window, Key.I, PhysicalKey.I, "i");
        Assert.Equal(KeyTipLevel.Commands, scene.Ribbon.KeyTips.Level);
        Assert.Equal("P", scene.Ribbon.KeyTips.TipOf("Picture"));

        Tap(scene.Window, Key.P, PhysicalKey.P, "p");
        Assert.Equal(KeyTipLevel.Hidden, scene.Ribbon.KeyTips.Level);
        Assert.Equal(new[] { "test.picture" }, scene.Invoked);
        Assert.Equal(string.Empty, scene.Editor.Text ?? string.Empty);
        scene.Window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task Escape_steps_back_from_commands_to_tabs_and_then_closes() => Session.Dispatch(() =>
    {
        var scene = Open();

        Tap(scene.Window, Key.F10, PhysicalKey.F10, null);
        Tap(scene.Window, Key.H, PhysicalKey.H, "h");
        Assert.Equal(KeyTipLevel.Commands, scene.Ribbon.KeyTips.Level);

        Tap(scene.Window, Key.Escape, PhysicalKey.Escape, null);
        Assert.Equal(KeyTipLevel.Tabs, scene.Ribbon.KeyTips.Level);

        Tap(scene.Window, Key.Escape, PhysicalKey.Escape, null);
        Assert.Equal(KeyTipLevel.Hidden, scene.Ribbon.KeyTips.Level);
        Assert.Empty(scene.Invoked);
        scene.Window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task Alt_used_as_a_modifier_does_not_show_the_tips() => Session.Dispatch(() =>
    {
        var scene = Open();

        scene.Window.KeyPress(Key.LeftAlt, RawInputModifiers.Alt, PhysicalKey.AltLeft, null);
        scene.Window.KeyPress(Key.X, RawInputModifiers.Alt, PhysicalKey.X, "x");
        scene.Window.KeyRelease(Key.X, RawInputModifiers.Alt, PhysicalKey.X, "x");
        scene.Window.KeyRelease(Key.LeftAlt, RawInputModifiers.None, PhysicalKey.AltLeft, null);
        Pump(scene.Window);

        Assert.Equal(KeyTipLevel.Hidden, scene.Ribbon.KeyTips.Level);
        scene.Window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task Blocked_tips_stay_hidden_under_a_modal() => Session.Dispatch(() =>
    {
        var scene = Open();
        scene.Ribbon.KeyTipsBlocked = () => true;

        TapAlt(scene.Window);

        Assert.Equal(KeyTipLevel.Hidden, scene.Ribbon.KeyTips.Level);
        scene.Window.Close();
    }, CancellationToken.None);
}
