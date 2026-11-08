using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Objects;

public sealed record ExternalLink(string Id, string Name, string Path, bool Available);

public enum ExternalLinkAction { Update, ChangeSource, Break }

public sealed record ExternalLinkRequest(string Id, ExternalLinkAction Action);

public sealed class ExternalLinksView : UserControl, IDialogContent, IDialogFooterOwner
{
    private readonly ListBox _list;
    private readonly IReadOnlyList<ExternalLink> _links;
    private readonly Button[] _actions;

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; }

    public ExternalLinksView(IReadOnlyList<ExternalLink> links, ITextCatalog texts, InterfaceDensity density = InterfaceDensity.Compact)
    {
        ArgumentNullException.ThrowIfNull(links);
        ArgumentNullException.ThrowIfNull(texts);
        _links = links;
        var metrics = DesignTokens.For(density);

        _list = new ListBox
        {
            Height = 240,
            ItemsSource = links.Select(link => Row(link, texts, metrics)).ToList(),
            SelectedIndex = links.Count > 0 ? 0 : -1,
        };

        Button Action(string key, ExternalLinkAction action)
        {
            var button = new Button { Content = texts[key] };
            button.Click += (_, _) =>
            {
                if (_list.SelectedIndex is var index && index >= 0 && index < _links.Count)
                    Completed?.Invoke(this, new ExternalLinkRequest(_links[index].Id, action));
            };
            return button;
        }

        _actions =
        [
            Action("links.update", ExternalLinkAction.Update),
            Action("links.changeSource", ExternalLinkAction.ChangeSource),
            Action("links.break", ExternalLinkAction.Break),
        ];
        _list.SelectionChanged += (_, _) => Refresh();
        Refresh();

        Footer = new DialogFooter(density).WithCancel(texts["document.close"], () => Completed?.Invoke(this, null));

        Content = new StackPanel
        {
            Spacing = metrics.SpacingMedium,
            Children =
            {
                new TextBlock { Text = texts[links.Count == 0 ? "links.empty" : "links.explanation"], TextWrapping = TextWrapping.Wrap }
                    .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary),
                _list,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = metrics.SpacingSmall, Children = { _actions[0], _actions[1], _actions[2] } },
            },
        };
    }

    private void Refresh()
    {
        var chosen = _list.SelectedIndex >= 0;
        foreach (var button in _actions) button.IsEnabled = chosen;
    }

    private static Control Row(ExternalLink link, ITextCatalog texts, PulseOffice.UI.Theming.DesignTokens.Metrics metrics) => new StackPanel
    {
        Spacing = 2,
        Margin = new Thickness(metrics.SpacingSmall, metrics.SpacingSmall / 2),
        Children =
        {
            new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = metrics.SpacingSmall,
                Children =
                {
                    new TextBlock { Text = link.Name, FontWeight = FontWeight.SemiBold },
                    new TextBlock { Text = texts[link.Available ? "links.available" : "links.missing"], Foreground = link.Available ? Brushes.SeaGreen : Brushes.IndianRed },
                },
            },
            new TextBlock { Text = link.Path, FontSize = metrics.FontSize - 1, TextTrimming = TextTrimming.PathSegmentEllipsis }
                .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary),
        },
    };
}
