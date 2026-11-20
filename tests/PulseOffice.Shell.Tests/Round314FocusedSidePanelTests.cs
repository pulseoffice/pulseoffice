using Avalonia.Controls;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.References;
using PulseOffice.UI.Controls.Services;
using Xunit;

namespace PulseOffice.Shell.Tests;

public sealed class Round314FocusedSidePanelTests
{
    private sealed class RecordingPanel : ISidePanelHost
    {
        private readonly List<string> _pages = [];

        public bool IsOpen { get; private set; }

        public string? Shown { get; private set; }

        public IReadOnlyList<string> Pages => _pages;

        public event EventHandler? Changed;

        public void Register(SidePanelPage page)
        {
            _pages.Remove(page.Key);
            _pages.Add(page.Key);
        }

        public void Unregister(string key) => _pages.Remove(key);

        public void Show(string key)
        {
            Shown = key;
            IsOpen = true;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Show(_pages.FirstOrDefault() ?? string.Empty);
        }

        public void Close()
        {
            IsOpen = false;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class NoDialogs : IDialogService, IDialogCollapse
    {
        public bool IsDialogOpen => false;
        public bool IsCollapsed => false;
        public string? TopDialogTitle => null;

        public event EventHandler? CollapsedChanged
        {
            add { }
            remove { }
        }

        public Task<TResult?> ShowAsync<TResult>(Control content, DialogOptions options, CancellationToken ct = default) =>
            Task.FromResult<TResult?>(default);

        public Task ShowAsync(Control content, DialogOptions options, CancellationToken ct = default) => Task.CompletedTask;

        public void Close(object? result)
        {
        }

        public IDisposable Collapse(Control bar, Action interrupted) => throw new NotSupportedException();
    }

    private static ShellWindowScope Scope(ISidePanelHost panel)
    {
        var dialogs = new NoDialogs();
        return new ShellWindowScope(new DocumentWorkspace(), dialogs, dialogs, panel);
    }

    [Fact]
    public void A_torn_off_window_gets_every_page_and_panel_commands_reach_the_focused_window()
    {
        var primaryPanel = new RecordingPanel();
        var secondPanel = new RecordingPanel();
        var primary = Scope(primaryPanel);
        var second = Scope(secondPanel);
        var focus = new ShellWindowFocus(primary);
        var host = new FocusedSidePanelHost(focus);
        host.Register(new SidePanelPage("navigator", () => new Panel()));

        focus.Enter(second);
        focus.Focus(second);
        host.Register(new SidePanelPage("styles", () => new Panel()));
        var changes = 0;
        host.Changed += (_, _) => changes++;
        host.Show("styles");

        Assert.Equal(new[] { "navigator", "styles" }, secondPanel.Pages);
        Assert.Equal(new[] { "navigator", "styles" }, primaryPanel.Pages);
        Assert.Equal("styles", secondPanel.Shown);
        Assert.False(primaryPanel.IsOpen);
        Assert.True(host.IsOpen);
        Assert.Equal(1, changes);

        focus.Focus(primary);
        Assert.False(host.IsOpen);
        secondPanel.Close();
        Assert.Equal(2, changes);

        focus.Leave(second);
        host.Unregister("styles");
        Assert.Contains("styles", secondPanel.Pages);
        Assert.DoesNotContain("styles", primaryPanel.Pages);
    }
}
