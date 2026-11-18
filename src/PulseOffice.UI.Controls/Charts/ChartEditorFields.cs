using System.Globalization;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Charts;

public sealed class ChartEditorFields(ITextCatalog texts, DesignTokens.Metrics metrics, CultureInfo culture)
{
    public ITextCatalog Texts { get; } = texts ?? throw new ArgumentNullException(nameof(texts));

    public DesignTokens.Metrics Metrics { get; } = metrics;

    public CultureInfo Culture { get; } = culture ?? throw new ArgumentNullException(nameof(culture));

    public TextBlock Label(string key) => new TextBlock
    {
        Text = Texts[key],
        FontSize = Metrics.FontSizeSmall,
    }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

    public Control Field(string key, Control control) => new StackPanel
    {
        Spacing = 2,
        Children = { Label(key), control },
    };

    public Control Pair(Control first, Control second)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = Metrics.SpacingSmall };
        Grid.SetColumn(second, 1);
        grid.Children.Add(first);
        grid.Children.Add(second);
        return grid;
    }

    public TextBox Number(string nameKey, Action changed)
    {
        var box = new TextBox { PlaceholderText = Texts["chart.scale.automatic"] };
        box.TextChanged += (_, _) => changed();
        Avalonia.Automation.AutomationProperties.SetName(box, Texts[nameKey]);
        return box;
    }

    public TextBox Text(string nameKey, string placeholderKey, Action changed)
    {
        var box = new TextBox { PlaceholderText = Texts[placeholderKey] };
        box.TextChanged += (_, _) => changed();
        Avalonia.Automation.AutomationProperties.SetName(box, Texts[nameKey]);
        return box;
    }

    public string Shown(double? value) => value is { } number ? number.ToString(Culture) : string.Empty;

    public double? Parsed(TextBox box) =>
        double.TryParse((box.Text ?? string.Empty).Trim(), NumberStyles.Float, Culture, out var value) && double.IsFinite(value) ? value : null;

    public string? Cleaned(TextBox box) =>
        string.IsNullOrWhiteSpace(box.Text) ? null : box.Text.Trim();

    public CheckBox Check(string key, Action changed)
    {
        var box = new CheckBox { Content = new TextBlock { Text = Texts[key], TextWrapping = TextWrapping.Wrap } };
        box.IsCheckedChanged += (_, _) => changed();
        return box;
    }

    public ComboBox Choice(string nameKey, IEnumerable<string> itemKeys, Action changed)
    {
        var box = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = itemKeys.Select(key => Texts[key]).ToArray(),
        };
        box.SelectionChanged += (_, _) => changed();
        Avalonia.Automation.AutomationProperties.SetName(box, Texts[nameKey]);
        return box;
    }

    public string Lower(string name) => char.ToLowerInvariant(name[0]) + name[1..];
}
