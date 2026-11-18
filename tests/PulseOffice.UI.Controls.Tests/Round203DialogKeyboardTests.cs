using PulseOffice.Testing;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Icons;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class Round203DialogKeyboardTests
{
    private static UiThreadSession Session => MacroEventsViewTests.Session;

    private static readonly DialogOptions Options = new() { TitleKey = "command.ledger.data.consolidate.title" };

    private sealed record Scene(Window Window, DialogHost Host, TextBox Outside, TextCatalog Texts);

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
        var window = new Window { Width = 800, Height = 600, Content = root };
        window.Show();
        outside.Focus();
        Pump(window);
        return new Scene(window, host, outside, texts);
    }

    private static void Pump(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static ChoicePromptView Prompt(Scene scene, params PromptChoice[] choices) =>
        new("Question?", string.Empty, choices, scene.Texts);

    [Fact]
    public Task A_prompt_with_only_footer_buttons_focuses_its_default_action() => Session.Dispatch(() =>
    {
        var scene = Open();
        var prompt = Prompt(scene, new PromptChoice("Keep", "keep", IsDefault: true), new PromptChoice("Cancel", "cancel", IsCancel: true));
        _ = scene.Host.ShowAsync<object>(prompt, Options);
        Pump(scene.Window);

        var focused = scene.Window.FocusManager?.GetFocusedElement() as Button;
        Assert.NotNull(focused);
        Assert.True(focused!.IsDefault);
        scene.Window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task Card_prompts_keep_the_focus_on_their_default_card() => Session.Dispatch(() =>
    {
        var scene = Open();
        var prompt = Prompt(scene, new PromptChoice("One", 1), new PromptChoice("Two", 2, IsDefault: true), new PromptChoice("Three", 3));
        _ = scene.Host.ShowAsync<object>(prompt, Options);
        Pump(scene.Window);

        Assert.Same(prompt.DefaultCard, scene.Window.FocusManager?.GetFocusedElement());
        scene.Window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task Enter_runs_the_default_action_and_returns_focus_to_the_editor() => Session.Dispatch(() =>
    {
        var scene = Open();
        var prompt = Prompt(scene, new PromptChoice("Keep", "keep", IsDefault: true), new PromptChoice("Cancel", "cancel", IsCancel: true));
        var result = scene.Host.ShowAsync<object>(prompt, Options);
        Pump(scene.Window);

        scene.Window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        Pump(scene.Window);

        Assert.True(result.IsCompleted);
        Assert.Equal("keep", result.Result);
        Assert.True(scene.Outside.IsFocused);
        scene.Window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task Closing_a_nested_dialog_gives_focus_back_to_the_field_that_opened_it() => Session.Dispatch(() =>
    {
        var scene = Open();
        var first = new TextBox { Name = "First" };
        var second = new TextBox { Name = "Second" };
        _ = scene.Host.ShowAsync<object>(new StackPanel { Children = { first, second } }, Options);
        Pump(scene.Window);
        second.Focus();
        Pump(scene.Window);

        _ = scene.Host.ShowAsync<object>(new StackPanel { Children = { new TextBox() } }, Options);
        Pump(scene.Window);
        scene.Window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Pump(scene.Window);

        Assert.True(scene.Host.IsDialogOpen);
        Assert.True(second.IsFocused);
        scene.Window.Close();
    }, CancellationToken.None);
}
