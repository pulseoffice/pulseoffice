using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PulseOffice.Localization;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Icons;
using Xunit;

namespace PulseOffice.Testing;

public sealed class DialogKeyboardWalk
{
    private const int StepLimit = 120;

    private readonly Window _window;
    private readonly DialogHost _host;

    public DialogKeyboardWalk(ITextCatalog texts, double width = 1200, double height = 900)
    {
        ArgumentNullException.ThrowIfNull(texts);
        _host = new DialogHost();
        _host.Use(new IconCatalog(), texts);
        Outside = new TextBox();
        var root = new DockPanel();
        DockPanel.SetDock(Outside, Dock.Top);
        root.Children.Add(Outside);
        root.Children.Add(_host);
        _window = new Window { Width = width, Height = height, Content = root };
        _window.Show();
        Outside.Focus();
        Pump();
    }

    public TextBox Outside { get; }

    public bool IsOpen => _host.IsDialogOpen;

    public IDialogService Dialogs => _host;

    public IInputElement? Focused => _window.FocusManager?.GetFocusedElement();

    public Task<object?> Open(Control content, string titleKey)
    {
        var shown = _host.ShowAsync<object>(content, new DialogOptions { TitleKey = titleKey, PreferredWidth = 1000, PreferredHeight = 800 });
        Pump();
        return shown;
    }

    public void Press(Key key, PhysicalKey physical, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        _window.KeyPress(key, modifiers, physical, null);
        _window.KeyRelease(key, modifiers, physical, null);
        Pump();
    }

    public IReadOnlyList<IInputElement> Cycle(bool backwards)
    {
        var start = Focused ?? throw new InvalidOperationException("Nothing is focused in the dialog.");
        var stops = new List<IInputElement>();
        for (var step = 0; step < StepLimit; step++)
        {
            Press(Key.Tab, PhysicalKey.Tab, backwards ? RawInputModifiers.Shift : RawInputModifiers.None);
            var focused = Focused ?? throw new InvalidOperationException("Tab left nothing focused.");
            stops.Add(focused);
            if (ReferenceEquals(focused, start)) return stops;
        }
        throw new InvalidOperationException($"Tab did not come back to the first stop within {StepLimit} presses; it started on {Describe(start)} and last went through {string.Join(", ", stops.TakeLast(8).Select(Describe))}.");
    }

    private string Describe(IInputElement element) =>
        element is Control control ? $"{control.GetType().Name}({AutomationProperties.GetName(control)}{(control as ContentControl)?.Content as string})" : element.GetType().Name;

    public void WalkAndCancel(Control content, string titleKey)
    {
        var shown = Open(content, titleKey);
        Assert.True(IsOpen);
        Assert.NotNull(Focused);
        Assert.True(Inside(Focused!, content), "The dialog did not take the focus.");

        var forward = Cycle(backwards: false);
        Assert.True(forward.Count >= 2, "The dialog has fewer than two Tab stops.");
        Assert.Equal(forward.Count, forward.Distinct().Count());
        Assert.All(forward, stop =>
        {
            Assert.True(Inside(stop, content), $"Tab left the dialog for {stop.GetType().Name}.");
            Assert.True(stop is InputElement { IsEffectivelyEnabled: true, IsEffectivelyVisible: true }, $"Tab stopped on a hidden or disabled {stop.GetType().Name}.");
        });

        var backward = Cycle(backwards: true);
        Assert.True(forward.ToHashSet().SetEquals(backward), "Shift+Tab does not walk the same stops as Tab.");
        if (forward.Count > 2) Assert.Same(forward[^2], backward[0]);

        Press(Key.Escape, PhysicalKey.Escape);
        Assert.False(IsOpen, "Escape did not cancel the dialog.");
        Assert.True(shown.IsCompleted);
        Assert.Null(shown.Result);
        Assert.True(Outside.IsFocused, "Closing did not give the focus back.");
    }

    public void ConfirmWithEnter(Control content, string titleKey, Func<IInputElement?, bool>? from = null)
    {
        var shown = Open(content, titleKey);
        if (from is not null)
            for (var step = 0; step < StepLimit && !from(Focused); step++) Press(Key.Tab, PhysicalKey.Tab);
        Press(Key.Enter, PhysicalKey.Enter);
        Assert.False(IsOpen, $"Enter on {Focused?.GetType().Name} did not run the default action.");
        Assert.True(shown.IsCompleted);
        Assert.NotNull(shown.Result);
    }

    public bool Inside(IInputElement element, Control content) =>
        element is Visual visual && (ReferenceEquals(visual, content) || _host.IsVisualAncestorOf(visual));

    public void Close()
    {
        while (_host.IsDialogOpen) _host.Close(null);
        _window.Close();
    }

    private void Pump()
    {
        Dispatcher.UIThread.RunJobs();
        _window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }
}
