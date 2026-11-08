using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Choices;
using PulseOffice.UI.Controls.Dialogs;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.Sheets.UI.DataDialogs;

public sealed class ValidationHelpPane : UserControl
{
    private readonly TextBlock _balloonTitle;
    private readonly TextBlock _balloonText;
    private readonly Border _balloon;
    private readonly LedgerDialogKit _kit;

    public ValidationHelpPane(LedgerDialogKit kit, string title, string message, bool show = true)
    {
        ArgumentNullException.ThrowIfNull(kit);
        _kit = kit;
        Title = new TextBox { Text = title, MinWidth = 240, MaxLength = 32 };
        Message = new TextBox { Text = message, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 96, MinWidth = 240, MaxLength = 255 };
        ShowOnSelect = kit.Check("validation.showInput", show || title.Length + message.Length == 0);

        _balloonTitle = new TextBlock { FontWeight = FontWeight.SemiBold, FontSize = kit.Metrics.FontSize, Foreground = Brushes.Black, TextWrapping = TextWrapping.Wrap };
        _balloonText = new TextBlock { FontSize = kit.Metrics.FontSizeSmall, Foreground = Brushes.Black, TextWrapping = TextWrapping.Wrap };
        _balloon = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0xFF, 0xFB, 0xE6)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xA3, 0x8B, 0x2E)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(8, 6),
            Width = 170,
            Margin = new Thickness(24, 4, 0, 0),
            BoxShadow = BoxShadows.Parse("0 2 6 0 #33000000"),
            Child = new StackPanel { Spacing = 3, Children = { _balloonTitle, _balloonText } },
        };

        var layout = new TwoPaneDialogLayout(kit.Metrics)
        {
            Header = kit.Help("validation.help.explanation"),
            Preview = new StackPanel
            {
                Width = 200,
                Spacing = kit.Metrics.SpacingSmall,
                Children = { kit.Caption("validation.preview"), new ValidationCellSketch(), _balloon },
            },
        };
        var section = layout.AddSection((string?)null);
        section.Add(null, ShowOnSelect, DialogRowKind.Indented);
        section.Add(kit["validation.inputTitle"], Title);
        section.Add(kit["validation.inputMessage"], Message, alignTop: true);
        Layout = layout;
        Content = layout;

        Title.TextChanged += (_, _) => Refresh();
        Message.TextChanged += (_, _) => Refresh();
        ShowOnSelect.IsCheckedChanged += (_, _) => Refresh();
        Refresh();
    }

    public TwoPaneDialogLayout Layout { get; }

    public TextBox Title { get; }

    public TextBox Message { get; }

    public CheckBox ShowOnSelect { get; }

    public bool BalloonVisible => _balloon.IsVisible;

    public string BalloonText => $"{_balloonTitle.Text}|{_balloonText.Text}";

    private void Refresh()
    {
        var title = Title.Text ?? string.Empty;
        var message = Message.Text ?? string.Empty;
        _balloonTitle.Text = title;
        _balloonTitle.IsVisible = title.Length > 0;
        _balloonText.Text = message.Length > 0 ? message : _kit["validation.preview.noMessage"];
        _balloon.IsVisible = ShowOnSelect.IsChecked == true;
        Title.IsEnabled = Message.IsEnabled = ShowOnSelect.IsChecked == true;
    }
}

public sealed class ValidationErrorPane : UserControl
{
    private readonly LedgerDialogKit _kit;
    private readonly TextBlock _cardTitle;
    private readonly TextBlock _cardText;
    private readonly Avalonia.Controls.Shapes.Path _cardIcon;
    private readonly StackPanel _cardButtons;
    private readonly Border _card;

    public ValidationErrorPane(LedgerDialogKit kit, ValidationAlert alert, string title, string message, bool show = true)
    {
        _kit = kit ?? throw new ArgumentNullException(nameof(kit));
        Alert = kit.Choices("validation.alert",
        [
            ("stop", "validation.alert.stop", "validation.alert.stop"),
            ("warning", "validation.alert.warning", "validation.alert.warning"),
            ("information", "validation.alert.information", "validation.alert.information"),
        ], (int)alert, showLabels: true, columns: 1);
        Title = new TextBox { Text = title, MinWidth = 240, MaxLength = 32 };
        Message = new TextBox { Text = message, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 96, MinWidth = 240, MaxLength = 255 };
        ShowOnError = kit.Check("validation.showError", show);

        _cardTitle = new TextBlock { FontWeight = FontWeight.SemiBold, FontSize = kit.Metrics.FontSize, Foreground = Brushes.Black, TextTrimming = TextTrimming.CharacterEllipsis };
        _cardText = new TextBlock { FontSize = kit.Metrics.FontSizeSmall, Foreground = Brushes.Black, TextWrapping = TextWrapping.Wrap, MaxWidth = 150 };
        _cardIcon = new Avalonia.Controls.Shapes.Path { StrokeThickness = 1.8, Width = 24, Height = 24, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Top };
        _cardButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, HorizontalAlignment = HorizontalAlignment.Right };
        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 8 };
        body.Children.Add(_cardIcon);
        Grid.SetColumn(_cardText, 1);
        body.Children.Add(_cardText);
        _card = new Border
        {
            Width = 210,
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xD1, 0xD5, 0xDB)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 8),
            BoxShadow = BoxShadows.Parse("0 4 12 0 #40000000"),
            Child = new StackPanel { Spacing = 8, Children = { _cardTitle, body, _cardButtons } },
        };

        var layout = new TwoPaneDialogLayout(kit.Metrics)
        {
            Header = kit.Help("validation.error.explanation"),
            Preview = new StackPanel { Width = 220, Spacing = kit.Metrics.SpacingSmall, Children = { kit.Caption("validation.preview"), _card } },
        };
        var section = layout.AddSection((string?)null);
        section.Add(null, ShowOnError, DialogRowKind.Indented);
        section.Add(kit["validation.alert"], Alert, alignTop: true);
        section.Add(kit["validation.errorTitle"], Title);
        section.Add(kit["validation.errorMessage"], Message, alignTop: true);
        Layout = layout;
        Content = layout;

        Alert.SelectionChanged += (_, _) => Refresh();
        Title.TextChanged += (_, _) => Refresh();
        Message.TextChanged += (_, _) => Refresh();
        ShowOnError.IsCheckedChanged += (_, _) => Refresh();
        Refresh();
    }

    public TwoPaneDialogLayout Layout { get; }

    public IconChoiceGroup Alert { get; }

    public TextBox Title { get; }

    public TextBox Message { get; }

    public CheckBox ShowOnError { get; }

    public ValidationAlert Chosen => (ValidationAlert)Math.Max(Alert.SelectedIndex, 0);

    public IReadOnlyList<string> CardButtons => [.. _cardButtons.Children.OfType<Border>().Select(button => (button.Child as TextBlock)?.Text ?? string.Empty)];

    public string CardTitle => _cardTitle.Text ?? string.Empty;

    private void Refresh()
    {
        var chosen = Chosen;
        var key = chosen switch
        {
            ValidationAlert.Warning => "warning",
            ValidationAlert.Information => "information",
            _ => "stop",
        };
        _cardIcon.Data = _kit.Icons.Find("validation.alert." + key) is { } icon ? Geometry.Parse(icon.Path) : null;
        _cardIcon.Stroke = chosen switch
        {
            ValidationAlert.Warning => new SolidColorBrush(Color.FromRgb(0xD9, 0x77, 0x06)),
            ValidationAlert.Information => new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB)),
            _ => new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26)),
        };
        var title = Title.Text ?? string.Empty;
        var message = Message.Text ?? string.Empty;
        _cardTitle.Text = title.Length > 0 ? title : _kit["validation.preview.defaultTitle"];
        _cardText.Text = message.Length > 0 ? message : _kit["validation.preview.defaultError"];

        _cardButtons.Children.Clear();
        foreach (var label in chosen switch
        {
            ValidationAlert.Warning => new[] { "validation.button.yes", "validation.button.no", "validation.button.cancel" },
            ValidationAlert.Information => ["validation.button.ok", "validation.button.cancel"],
            _ => ["validation.button.retry", "validation.button.cancel"],
        })
            _cardButtons.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0xF3, 0xF4, 0xF6)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xD1, 0xD5, 0xDB)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(8, 2),
                Child = new TextBlock { Text = _kit[label], FontSize = _kit.Metrics.FontSizeSmall, Foreground = Brushes.Black },
            });

        _card.Opacity = ShowOnError.IsChecked == true ? 1 : 0.35;
        Alert.IsEnabled = Title.IsEnabled = Message.IsEnabled = ShowOnError.IsChecked == true;
    }
}

public sealed class ValidationCellSketch : UserControl
{
    public ValidationCellSketch()
    {
        var line = new SolidColorBrush(Color.FromRgb(0xD1, 0xD5, 0xDB));
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("60,60,60"), RowDefinitions = new RowDefinitions("20,20") };
        for (var row = 0; row < 2; row++)
            for (var column = 0; column < 3; column++)
            {
                var selected = row == 1 && column == 1;
                var cell = new Border
                {
                    Background = Brushes.White,
                    BorderBrush = selected ? new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A)) : line,
                    BorderThickness = selected ? new Thickness(2) : new Thickness(0, 0, 1, 1),
                };
                Grid.SetRow(cell, row);
                Grid.SetColumn(cell, column);
                grid.Children.Add(cell);
            }

        Content = new Border { BorderBrush = line, BorderThickness = new Thickness(1, 1, 0, 0), Child = grid, HorizontalAlignment = HorizontalAlignment.Left };
    }
}
