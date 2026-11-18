using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using PulseOffice.Localization;
using PulseOffice.Testing;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Ribbon;
using PulseOffice.UI.Icons;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class Round211RibbonKeyTipsTests
{
    private static UiThreadSession Session => MacroEventsViewTests.Session;

    private sealed record Scene(Window Window, RibbonView Ribbon, Button Status, MenuItem View, List<string> Invoked, List<string> Clicked);

    private static RibbonItem Item(string id, string title) =>
        new(new CommandDefinition { Id = new CommandId(id), Execute = (_, _) => Task.CompletedTask }, true, title, null);

    private static Scene Open()
    {
        var services = new RibbonServices(
            new IconCatalog(), new TextCatalog(UiLanguage.English), null, new RibbonStripLayout(), new RibbonReductionPlanner(),
            new RibbonSlotCatalog([new MenuSlotFactory(), new CommandSlotFactory()]));
        var ribbon = new RibbonView(services);
        var invoked = new List<string>();
        var clicked = new List<string>();
        ribbon.CommandInvoked += (_, invocation) => invoked.Add(invocation.CommandId);

        var view = new MenuItem { Header = "_View", Items = { new MenuItem { Header = "Zoom" } } };
        var menus = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Children = { new Menu { Items = { view } } } };
        var status = new Button { Content = "Words: 12" };
        status.Click += (_, _) => clicked.Add("status");
        var statusBar = new StackPanel { Children = { status } };

        var editor = new TextBox();
        var root = new DockPanel();
        DockPanel.SetDock(menus, Dock.Top);
        DockPanel.SetDock(ribbon, Dock.Top);
        DockPanel.SetDock(statusBar, Dock.Bottom);
        root.Children.Add(menus);
        root.Children.Add(ribbon);
        root.Children.Add(statusBar);
        root.Children.Add(editor);
        ribbon.KeyTipRegions.Add(new MenuKeyTipRegion(menus, ribbon.KeyTipCollector));
        ribbon.KeyTipRegions.Add(new ButtonKeyTipRegion(statusBar, ribbon.KeyTipCollector));

        var window = new Window { Width = 900, Height = 500, Content = root };
        window.Show();
        var paste = Item("test.paste", "Paste") with { Menu = [Item("test.pasteSpecial", "Special"), Item("test.pasteText", "Text only")] };
        ribbon.Update(new RibbonModel(
        [
            new RibbonTab("Home", [new RibbonGroup("Clipboard", [Item("test.copy", "Copy"), paste])]),
        ]));
        editor.Focus();
        Pump(window);
        return new Scene(window, ribbon, status, view, invoked, clicked);
    }

    private static void Pump(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static void Tap(Window window, Key key, PhysicalKey physical, string? symbol)
    {
        window.KeyPress(key, RawInputModifiers.None, physical, symbol);
        window.KeyRelease(key, RawInputModifiers.None, physical, symbol);
        Pump(window);
    }

    private static void Type(Scene scene, string tip)
    {
        foreach (var letter in tip)
        {
            var key = letter is >= '0' and <= '9' ? Key.D0 + (letter - '0') : Key.A + (letter - 'A');
            Tap(scene.Window, key, PhysicalKey.None, letter.ToString().ToLowerInvariant());
        }
    }

    [Fact]
    public Task A_menu_command_opens_its_list_and_a_third_letter_runs_an_entry() => Session.Dispatch(() =>
    {
        var scene = Open();

        Tap(scene.Window, Key.F10, PhysicalKey.F10, null);
        Type(scene, scene.Ribbon.KeyTips.TipOf("Home")!);
        Assert.Equal(KeyTipLevel.Commands, scene.Ribbon.KeyTips.Level);

        Type(scene, scene.Ribbon.KeyTips.TipOf("Paste")!);
        Assert.Equal(KeyTipLevel.Items, scene.Ribbon.KeyTips.Level);
        Assert.NotNull(scene.Ribbon.KeyTips.TipOf("Special"));
        Assert.NotNull(scene.Ribbon.KeyTips.TipOf("Text only"));
        Assert.Empty(scene.Invoked);

        Type(scene, scene.Ribbon.KeyTips.TipOf("Special")!);
        Assert.Equal(KeyTipLevel.Hidden, scene.Ribbon.KeyTips.Level);
        Assert.Equal(new[] { "test.pasteSpecial" }, scene.Invoked);
        Assert.Null(scene.Ribbon.Items());
        scene.Window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task Escape_from_the_list_closes_it_and_steps_back_to_the_commands() => Session.Dispatch(() =>
    {
        var scene = Open();

        Tap(scene.Window, Key.F10, PhysicalKey.F10, null);
        Type(scene, scene.Ribbon.KeyTips.TipOf("Home")!);
        Type(scene, scene.Ribbon.KeyTips.TipOf("Paste")!);
        Assert.Equal(KeyTipLevel.Items, scene.Ribbon.KeyTips.Level);

        Tap(scene.Window, Key.Escape, PhysicalKey.Escape, null);
        Assert.Equal(KeyTipLevel.Commands, scene.Ribbon.KeyTips.Level);
        Assert.Null(scene.Ribbon.Items());
        Assert.NotNull(scene.Ribbon.KeyTips.TipOf("Copy"));
        Assert.Empty(scene.Invoked);
        scene.Window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task Status_bar_commands_and_title_bar_menus_take_tips_beside_the_tabs() => Session.Dispatch(() =>
    {
        var scene = Open();

        Tap(scene.Window, Key.F10, PhysicalKey.F10, null);
        Assert.Equal(KeyTipLevel.Tabs, scene.Ribbon.KeyTips.Level);
        Assert.NotNull(scene.Ribbon.KeyTips.TipOf("View"));
        var status = scene.Ribbon.KeyTips.TipOf("Words: 12");
        Assert.NotNull(status);

        Type(scene, status!);
        Assert.Equal(KeyTipLevel.Hidden, scene.Ribbon.KeyTips.Level);
        Assert.Equal(new[] { "status" }, scene.Clicked);

        Tap(scene.Window, Key.F10, PhysicalKey.F10, null);
        Type(scene, scene.Ribbon.KeyTips.TipOf("View")!);
        Assert.Equal(KeyTipLevel.Hidden, scene.Ribbon.KeyTips.Level);
        Assert.True(scene.View.IsSubMenuOpen);
        scene.Window.Close();
    }, CancellationToken.None);
}
