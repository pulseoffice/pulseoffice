using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Search;

public interface IFindPanelSource
{
    IFindReplaceTarget? Target();

    void Highlight(FindRequest request);

    void ClearHighlights();

    Task OpenReplaceAsync(string query);
}

public sealed class FindPanelView : UserControl
{
    private readonly IFindPanelSource _source;
    private readonly ITextCatalog _texts;
    private readonly TextBox _query;
    private readonly StackPanel _options;
    private readonly TextBlock _summary;
    private readonly List<(FindOption Option, CheckBox Box)> _boxes = [];

    public FindPanelView(IFindPanelSource source, ITextCatalog texts, InterfaceDensity density = InterfaceDensity.Compact)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        var metrics = DesignTokens.For(density);
        _query = new TextBox { PlaceholderText = texts["find.query"] };
        Avalonia.Automation.AutomationProperties.SetName(_query, texts["find.query"]);
        _options = new StackPanel { Spacing = metrics.SpacingSmall / 2 };
        _summary = new TextBlock { FontSize = metrics.FontSizeSmall, TextWrapping = Avalonia.Media.TextWrapping.Wrap }
            .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
        Avalonia.Automation.AutomationProperties.SetLiveSetting(_summary, Avalonia.Automation.AutomationLiveSetting.Polite);

        var previous = new Button { Content = texts["find.previous"] };
        var next = new Button { Content = texts["find.next"] };
        next.Classes.Add("accent");
        var replace = new Button { Content = texts["find.panel.replace"] };
        previous.Click += (_, _) => Go(backwards: true);
        next.Click += (_, _) => Go(backwards: false);
        replace.Click += (_, _) => _ = _source.OpenReplaceAsync(_query.Text ?? string.Empty);

        _query.TextChanged += (_, _) => Refresh();
        _query.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            Go(backwards: e.KeyModifiers.HasFlag(KeyModifiers.Shift));
            e.Handled = true;
        };

        Content = new StackPanel
        {
            Spacing = metrics.SpacingMedium,
            Margin = new Thickness(metrics.SpacingMedium),
            Children =
            {
                _query,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = metrics.SpacingSmall, Children = { previous, next, replace } },
                _options,
                _summary,
            },
        };
    }

    public string Query
    {
        get => _query.Text ?? string.Empty;
        set => _query.Text = value;
    }

    public string Summary => _summary.Text ?? string.Empty;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Rebuild();
        _query.Focus();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _source.ClearHighlights();
        base.OnDetachedFromVisualTree(e);
    }

    public void Rebuild()
    {
        var checkedKeys = _boxes.Where(pair => pair.Box.IsChecked == true).Select(pair => pair.Option.Key).ToHashSet(StringComparer.Ordinal);
        _boxes.Clear();
        _options.Children.Clear();
        foreach (var option in _source.Target()?.Options ?? Array.Empty<FindOption>())
        {
            var box = new CheckBox { Content = _texts[option.LabelKey], IsChecked = checkedKeys.Contains(option.Key) || option.Initial };
            box.IsCheckedChanged += (_, _) => Refresh();
            _boxes.Add((option, box));
            _options.Children.Add(box);
        }
        Refresh();
    }

    public void Refresh()
    {
        var request = Request();
        if (_source.Target() is not { } target || request.Query.Length == 0)
        {
            _source.ClearHighlights();
            _summary.Text = string.Empty;
            return;
        }
        _summary.Text = target.Summarize(request) ?? string.Empty;
        _source.Highlight(request);
    }

    public void Go(bool backwards)
    {
        var request = Request();
        if (request.Query.Length == 0 || _source.Target() is not { } target) return;
        _summary.Text = backwards && target.SupportsFindPrevious ? target.FindPrevious(request) : target.FindNext(request);
        if (_summary.Text.Length == 0) _summary.Text = target.Summarize(request) ?? string.Empty;
    }

    private FindRequest Request() => new(
        _query.Text ?? string.Empty,
        string.Empty,
        _boxes.Where(pair => pair.Box.IsChecked == true).Select(pair => pair.Option.Key).ToHashSet(StringComparer.Ordinal));
}
