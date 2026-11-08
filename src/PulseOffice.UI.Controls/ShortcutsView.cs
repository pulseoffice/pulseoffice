using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Shortcuts;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls;

public sealed class ShortcutsView : UserControl
{
    private readonly StackPanel _list;
    private readonly IReadOnlyList<(CommandPresentation Presentation, ShortcutGesture Gesture)> _all;
    private readonly DesignTokens.Metrics _metrics;
    private readonly ITextCatalog _texts;

    public ShortcutsView(
        IReadOnlyList<(CommandPresentation Presentation, ShortcutGesture Gesture)> shortcuts,
        ITextCatalog texts,
        InterfaceDensity density = InterfaceDensity.Compact)
    {
        ArgumentNullException.ThrowIfNull(shortcuts);

        _all = shortcuts;
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        _metrics = DesignTokens.For(density);

        var search = new TextBox
        {
            PlaceholderText = _texts["shortcuts.search"],
            Margin = new Thickness(0, 0, 0, _metrics.SpacingMedium),
        };

        Avalonia.Automation.AutomationProperties.SetName(search, _texts["shortcuts.search"]);
        search.TextChanged += (_, _) => Show(search.Text);

        _list = new StackPanel { Spacing = 1 };

        var layout = new DockPanel { LastChildFill = true };

        DockPanel.SetDock(search, Dock.Top);
        layout.Children.Add(search);

        layout.Children.Add(new ScrollViewer
        {
            Content = _list,
            MaxHeight = 420,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        });

        Show(null);
        Content = layout;
    }

    private void Show(string? query)
    {
        _list.Children.Clear();

        var matching = string.IsNullOrWhiteSpace(query)
            ? _all
            : [.. _all.Where(entry => Matches(entry, query!))];

        if (matching.Count == 0)
        {
            _list.Children.Add(new TextBlock
            {
                Text = _texts["shortcuts.none"],
                FontSize = _metrics.FontSize,
                Margin = new Thickness(0, _metrics.SpacingMedium),
            }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary));

            return;
        }

        foreach (var entry in matching) _list.Children.Add(Row(entry));
    }

    private static bool Matches(
        (CommandPresentation Presentation, ShortcutGesture Gesture) entry, string query) =>
        entry.Presentation.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase)
        || entry.Presentation.Path?.Contains(query, StringComparison.CurrentCultureIgnoreCase) == true
        || entry.Gesture.Display().Contains(query, StringComparison.CurrentCultureIgnoreCase);

    private Control Row((CommandPresentation Presentation, ShortcutGesture Gesture) entry)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };

        var labels = new StackPanel { Spacing = 1 };

        labels.Children.Add(new TextBlock
        {
            Text = entry.Presentation.Title,
            FontSize = _metrics.FontSize,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextPrimary));

        if (entry.Presentation.Path is { Length: > 0 } path)
            labels.Children.Add(new TextBlock
            {
                Text = path,
                FontSize = _metrics.FontSizeSmall,
            }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextDisabled));

        var gesture = new Border
        {
            Padding = new Thickness(_metrics.SpacingSmall, 2),
            CornerRadius = new CornerRadius(_metrics.RadiusTight),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = entry.Gesture.Display(),
                FontSize = _metrics.FontSizeSmall,
                FontWeight = FontWeight.Medium,
                FontFamily = new FontFamily("Menlo, Consolas, monospace"),
            }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary),
        }.Themed(Border.BackgroundProperty, Palette.Keys.SurfaceSunken);

        Grid.SetColumn(labels, 0);
        Grid.SetColumn(gesture, 1);

        grid.Children.Add(labels);
        grid.Children.Add(gesture);

        return new Border
        {
            Child = grid,
            Padding = new Thickness(_metrics.SpacingSmall, _metrics.SpacingSmall / 2),
            CornerRadius = new CornerRadius(_metrics.RadiusTight),
        };
    }
}
