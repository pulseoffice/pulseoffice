using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Tabs;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls;

public sealed record SidePanelPage(string Key, Func<Control> Build);

public interface ISidePanelHost
{
    bool IsOpen { get; }

    IReadOnlyList<string> Pages { get; }

    void Register(SidePanelPage page);

    void Unregister(string key);

    void Show(string key);

    void Toggle();

    void Close();

    event EventHandler? Changed;
}

public sealed class SidePanelView : UserControl, ISidePanelHost
{
    private readonly ITextCatalog _texts;
    private readonly DesignTokens.Metrics _metrics;
    private readonly List<SidePanelPage> _pages = [];
    private readonly PageTabStrip _tabs;
    private readonly Border _body = new();
    private readonly TextBlock _title = new();

    private string? _current;

    private readonly Button _close;

    public SidePanelView(ITextCatalog texts, InterfaceDensity density = InterfaceDensity.Compact)
    {
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        _metrics = DesignTokens.For(density);

        _tabs = new PageTabStrip(_metrics);
        _tabs.SelectionChanged += (_, index) =>
        {
            if (index >= 0 && index < _pages.Count && _pages[index].Key != _current) Show(_pages[index].Key);
        };

        _title.FontSize = _metrics.FontSizeSmall;
        _title.FontWeight = FontWeight.SemiBold;
        _title.VerticalAlignment = VerticalAlignment.Center;
        _title.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextPrimary);

        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Margin = new Thickness(_metrics.SpacingMedium, _metrics.SpacingSmall),
        };

        var close = new Button
        {
            Content = "✕",
            Width = 24,
            Height = 24,
            Classes = { "dialogClose" },
            VerticalAlignment = VerticalAlignment.Center,
        };
        close.Click += (_, _) => Close();
        _close = close;

        Grid.SetColumn(_title, 0);
        Grid.SetColumn(close, 1);

        header.Children.Add(_title);
        header.Children.Add(close);

        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*") };

        Grid.SetRow(header, 0);
        Grid.SetRow(_tabs, 1);
        Grid.SetRow(_body, 2);

        _tabs.Margin = new Thickness(_metrics.SpacingMedium, 0, _metrics.SpacingMedium, _metrics.SpacingSmall);
        _body.Margin = new Thickness(_metrics.SpacingMedium, 0, _metrics.SpacingMedium, _metrics.SpacingMedium);

        layout.Children.Add(header);
        layout.Children.Add(_tabs);
        layout.Children.Add(_body);

        Width = 280;
        IsVisible = false;

        Content = layout;

        this.Themed(BackgroundProperty, Palette.Keys.SurfaceRaised);
        Avalonia.Automation.AutomationProperties.SetName(_close, _texts["titlebar.close"]);

        AttachedToVisualTree += (_, _) => _texts.LanguageChanged += OnLanguageChanged;
        DetachedFromVisualTree += (_, _) => _texts.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        Avalonia.Automation.AutomationProperties.SetName(_close, _texts["titlebar.close"]);
        if (IsVisible && _current is { } current) Show(current);
        else RebuildTabs();
    }

    public bool IsOpen => IsVisible;

    public IReadOnlyList<string> Pages => [.. _pages.Select(page => page.Key)];

    public event EventHandler? Changed;

    public void Register(SidePanelPage page)
    {
        ArgumentNullException.ThrowIfNull(page);

        _pages.RemoveAll(known => known.Key == page.Key);
        _pages.Add(page);

        RebuildTabs();
    }

    public void Unregister(string key)
    {
        _pages.RemoveAll(page => page.Key == key);

        if (_current == key) _current = null;

        RebuildTabs();
    }

    public void Show(string key)
    {
        if (_pages.FirstOrDefault(page => page.Key == key) is not { } page) return;

        _current = key;
        _title.Text = _texts[$"panel.{key}.title"];
        _body.Child = page.Build();

        IsVisible = true;

        RebuildTabs();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Toggle()
    {
        if (IsVisible)
        {
            Close();
            return;
        }

        Show(_current ?? _pages.FirstOrDefault()?.Key ?? string.Empty);
    }

    public void Close()
    {
        IsVisible = false;
        _body.Child = null;

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void RebuildTabs()
    {
        _tabs.IsVisible = _pages.Count > 1;
        var selected = _pages.FindIndex(page => page.Key == _current);
        _tabs.Show([.. _pages.Select(page => _texts[$"panel.{page.Key}.title"])], Math.Max(0, selected));
    }
}
