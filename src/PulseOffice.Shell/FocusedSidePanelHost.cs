using PulseOffice.UI.Controls;

namespace PulseOffice.Shell;

public sealed class FocusedSidePanelHost : ISidePanelHost
{
    private readonly IShellWindowFocus _focus;
    private readonly List<SidePanelPage> _pages = [];
    private readonly HashSet<ISidePanelHost> _panels = new(ReferenceEqualityComparer.Instance);

    public FocusedSidePanelHost(IShellWindowFocus focus)
    {
        _focus = focus ?? throw new ArgumentNullException(nameof(focus));
        foreach (var scope in focus.Scopes) Join(scope);
        focus.Entered += (_, scope) => Join(scope);
        focus.Left += (_, scope) => Part(scope);
        focus.FocusChanged += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
    }

    private ISidePanelHost? Current => _focus.Current.SidePanel;

    public bool IsOpen => Current?.IsOpen ?? false;

    public IReadOnlyList<string> Pages => [.. _pages.Select(page => page.Key)];

    public event EventHandler? Changed;

    public void Register(SidePanelPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        _pages.RemoveAll(known => known.Key == page.Key);
        _pages.Add(page);
        foreach (var panel in _panels) panel.Register(page);
    }

    public void Unregister(string key)
    {
        _pages.RemoveAll(page => page.Key == key);
        foreach (var panel in _panels) panel.Unregister(key);
    }

    public void Show(string key) => Current?.Show(key);

    public void Toggle() => Current?.Toggle();

    public void Close() => Current?.Close();

    private void Join(ShellWindowScope scope)
    {
        if (scope.SidePanel is not { } panel || !_panels.Add(panel)) return;
        foreach (var page in _pages) panel.Register(page);
        panel.Changed += OnPanelChanged;
    }

    private void Part(ShellWindowScope scope)
    {
        if (scope.SidePanel is not { } panel || !_panels.Remove(panel)) return;
        panel.Changed -= OnPanelChanged;
    }

    private void OnPanelChanged(object? sender, EventArgs e)
    {
        if (ReferenceEquals(sender, Current)) Changed?.Invoke(this, e);
    }
}
