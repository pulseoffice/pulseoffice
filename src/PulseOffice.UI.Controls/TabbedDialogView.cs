using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Tabs;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls;

public sealed record DialogTab(string Title, Func<Control> Build);

public sealed class TabbedDialogView : UserControl, IDialogContent, IDialogFooterOwner
{
    private readonly List<(string Title, Control Content)> _tabs = [];
    private readonly PageTabStrip _strip;
    private readonly Grid _host = new();
    private readonly DesignTokens.Metrics _metrics;

    private int _current = -1;

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; private set; }

    public TabbedDialogView(
        IReadOnlyList<DialogTab> tabs, Func<object?> collect, ITextCatalog texts,
        InterfaceDensity density = InterfaceDensity.Compact, int initialTab = 0)
    {
        ArgumentNullException.ThrowIfNull(tabs);
        ArgumentNullException.ThrowIfNull(collect);
        ArgumentNullException.ThrowIfNull(texts);

        _metrics = DesignTokens.For(density);

        foreach (var tab in tabs)
        {
            var content = tab.Build();
            _tabs.Add((tab.Title, content));
            _host.Children.Add(content);
        }

        _strip = new PageTabStrip(_metrics, [.. tabs.Select(tab => tab.Title)], initialTab);
        _strip.SelectionChanged += (_, index) => Show(index);
        _strip.IsVisible = tabs.Count > 1;

        _host.Margin = new Thickness(0, tabs.Count > 1 ? _metrics.SpacingMedium : 0, 0, 0);

        Footer = new DialogFooter()
            .WithCancel(texts["prompt.cancel"], () => Completed?.Invoke(this, null))
            .WithAction(texts["prompt.confirm"], () => Completed?.Invoke(this, collect()), primary: true);

        Content = new StackPanel
        {
            Children =
            {
                _strip,
                _host,
            },
        };

        Show(Math.Clamp(initialTab, 0, Math.Max(0, _tabs.Count - 1)));
    }

    public double StripWidth
    {
        get
        {
            _strip.Measure(Size.Infinity);
            return _strip.DesiredSize.Width;
        }
    }

    private void Show(int index)
    {
        if (index == _current || index < 0 || index >= _tabs.Count) return;

        _current = index;
        _strip.Select(index);

        for (var i = 0; i < _tabs.Count; i++)
        {
            var selected = i == index;
            var content = _tabs[i].Content;
            content.Opacity = selected ? 1 : 0;
            content.IsHitTestVisible = selected;
            content.IsEnabled = selected;
            content.ZIndex = selected ? 1 : 0;
        }
    }
}
