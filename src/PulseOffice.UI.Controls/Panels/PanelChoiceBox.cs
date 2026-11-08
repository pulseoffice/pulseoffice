using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Panels;

public sealed record PanelChoice(string Key, string Title, string? Icon = null);

public sealed class PanelChoiceBox : UserControl
{
    private readonly IIconCatalog _icons;
    private readonly DesignTokens.Metrics _metrics;
    private readonly ComboBox _box;
    private IReadOnlyList<PanelChoice> _choices = [];
    private bool _filling;

    public PanelChoiceBox(IIconCatalog icons, DesignTokens.Metrics metrics)
    {
        _icons = icons ?? throw new ArgumentNullException(nameof(icons));
        _metrics = metrics;
        _box = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinHeight = metrics.ControlHeight + 2,
            ItemTemplate = new FuncDataTemplate<PanelChoice>((choice, _) => choice is null ? new Panel() : Row(choice), supportsRecycling: false),
        };
        _box.SelectionChanged += (_, _) =>
        {
            if (_filling || _box.SelectedItem is not PanelChoice choice) return;
            Chosen?.Invoke(this, choice.Key);
        };
        Content = _box;
    }

    public event EventHandler<string>? Chosen;

    public IReadOnlyList<PanelChoice> Choices => _choices;

    public string? Selected => (_box.SelectedItem as PanelChoice)?.Key;

    public string Tip
    {
        set
        {
            ToolTip.SetTip(_box, value);
            Avalonia.Automation.AutomationProperties.SetName(_box, value);
        }
    }

    public void Show(IReadOnlyList<PanelChoice> choices, string? selected)
    {
        _filling = true;
        _choices = choices ?? throw new ArgumentNullException(nameof(choices));
        _box.ItemsSource = choices;
        _box.SelectedItem = choices.FirstOrDefault(choice => choice.Key == selected) ?? choices.FirstOrDefault();
        _filling = false;
    }

    public void Select(string key)
    {
        if (_choices.FirstOrDefault(choice => choice.Key == key) is not { } choice) return;
        _box.SelectedItem = choice;
    }

    private Control Row(PanelChoice choice)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = _metrics.SpacingSmall + 2, VerticalAlignment = VerticalAlignment.Center };
        if (choice.Icon is { } key && _icons.Find(key) is { } geometry)
            row.Children.Add(new IconView(geometry, _metrics.IconSize) { VerticalAlignment = VerticalAlignment.Center }
                .Themed(IconView.ForegroundProperty, Palette.Keys.TextSecondary));
        row.Children.Add(new TextBlock
        {
            Text = choice.Title,
            FontSize = _metrics.FontSize,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        return row;
    }
}
