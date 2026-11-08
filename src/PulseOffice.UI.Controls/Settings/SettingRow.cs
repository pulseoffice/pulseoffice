using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Settings;
using PulseOffice.Settings.Navigation;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Settings;

public sealed class SettingRow
{
    private readonly SettingsViewServices _services;
    private readonly DesignTokens.Metrics _metrics;
    private readonly SettingField _field;
    private readonly Border _dot;
    private readonly Button _reset;
    private readonly TextBlock _hint;
    private readonly StackPanel _pills;
    private bool _hovered;

    public SettingRow(SettingDefinition definition, SettingsViewServices services, DesignTokens.Metrics metrics)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _metrics = metrics;

        _field = services.Editors.Create(new SettingEditorContext(
            definition, services.Settings, services.Texts, services.Icons, metrics, Apply));

        _dot = new Border
        {
            Width = 6,
            Height = 6,
            CornerRadius = new CornerRadius(3),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, metrics.SpacingSmall, 0),
        };
        _dot.Bind(Border.BackgroundProperty, _dot.GetResourceObservable("PulseAccent"));
        ToolTip.SetTip(_dot, services.Texts["settings.modified"]);

        _reset = ResetButton();
        _hint = new TextBlock
        {
            FontSize = metrics.FontSizeSmall,
            TextWrapping = TextWrapping.Wrap,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextDisabled);
        _pills = new StackPanel { Orientation = Orientation.Horizontal, Spacing = metrics.SpacingSmall };

        View = Layout();
        Update();
    }

    public SettingDefinition Definition { get; }

    public Control View { get; }

    public void Refresh()
    {
        _field.Refresh();
        Update();
    }

    private void Apply(object value)
    {
        try
        {
            _services.Settings.Set(Definition.Key, value);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            Refresh();
        }
    }

    private void Update()
    {
        var state = _services.States.Of(Definition);

        _dot.Opacity = state.IsModified ? 1 : 0;
        _reset.IsVisible = state.CanReset;
        ShowReset();
        _field.View.IsEnabled = !state.IsLocked;

        _hint.Text = _field.Hint;
        _hint.IsVisible = !string.IsNullOrEmpty(_field.Hint);

        _pills.Children.Clear();
        if (state.RequiresRestart) _pills.Children.Add(Pill(_services.Texts["settings.restart"], Palette.Keys.Info));
        if (state.IsLocked) _pills.Children.Add(Pill(_services.Texts["settings.locked"], Palette.Keys.Warning));
        _pills.IsVisible = _pills.Children.Count > 0;
    }

    private Control Layout()
    {
        var texts = _services.Texts;
        AutomationProperties.SetName(_field.View, texts[Definition.TitleKey]);
        var title = new TextBlock
        {
            Text = texts[Definition.TitleKey],
            FontSize = _metrics.FontSize + 1,
            FontWeight = FontWeight.Medium,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextPrimary);

        var heading = new DockPanel();
        DockPanel.SetDock(_dot, Dock.Left);
        heading.Children.Add(_dot);
        heading.Children.Add(title);

        var labels = new StackPanel
        {
            Spacing = 3,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        labels.Children.Add(heading);

        if (texts.Contains(Definition.DescriptionKey))
            labels.Children.Add(new Dialogs.HelpTip(texts[Definition.DescriptionKey], texts, _metrics)
            {
                Margin = new Thickness(_dot.Width + _metrics.SpacingSmall, 0, 0, 0),
            });

        _hint.Margin = new Thickness(_dot.Width + _metrics.SpacingSmall, 0, 0, 0);
        _pills.Margin = new Thickness(_dot.Width + _metrics.SpacingSmall, 2, 0, 0);
        labels.Children.Add(_hint);
        labels.Children.Add(_pills);

        var control = new Panel
        {
            Width = _field.IsWide ? double.NaN : 220,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _field.View },
        };
        _field.View.HorizontalAlignment = _field.IsWide ? HorizontalAlignment.Stretch : HorizontalAlignment.Right;

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            ColumnSpacing = _metrics.SpacingMedium,
            RowSpacing = _field.IsWide ? _metrics.SpacingMedium : 0,
        };
        Grid.SetColumn(_reset, 1);
        if (_field.IsWide)
        {
            Grid.SetRow(control, 1);
            Grid.SetColumnSpan(control, 3);
        }
        else
        {
            Grid.SetColumn(control, 2);
        }
        grid.Children.Add(labels);
        grid.Children.Add(_reset);
        grid.Children.Add(control);

        var row = new Border
        {
            Padding = new Thickness(_metrics.SpacingLarge - 2, _metrics.SpacingMedium + 2),
            Background = Brushes.Transparent,
            Child = grid,
        };

        row.PointerEntered += (_, _) => Hover(true);
        row.PointerExited += (_, _) => Hover(false);
        return row;
    }

    private void Hover(bool hovered)
    {
        _hovered = hovered;
        ShowReset();
    }

    private void ShowReset() => _reset.Opacity = _hovered || _reset.IsKeyboardFocusWithin ? 1 : 0;

    private Button ResetButton()
    {
        var button = new Button
        {
            Width = 26,
            Height = 26,
            Padding = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Content = _services.Icons.Find("ui.reset") is { } geometry
                ? new IconView(geometry, 14).Themed(IconView.ForegroundProperty, Palette.Keys.TextSecondary)
                : "↺",
        };

        button.Classes.Add("ribbon");
        ToolTip.SetTip(button, _services.Texts["settings.reset"]);
        AutomationProperties.SetName(button, _services.Texts["settings.reset"]);
        button.GotFocus += (_, _) => ShowReset();
        button.LostFocus += (_, _) => ShowReset();
        button.Click += (_, _) => _services.States.Reset(Definition);
        return button;
    }

    private Border Pill(string text, string colourKey)
    {
        var label = new TextBlock
        {
            Text = text,
            FontSize = _metrics.FontSizeSmall - 0.5,
            FontWeight = FontWeight.Medium,
        }.Themed(TextBlock.ForegroundProperty, colourKey);

        return new Border
        {
            CornerRadius = new CornerRadius(999),
            Padding = new Thickness(_metrics.SpacingSmall + 2, 1),
            BorderThickness = new Thickness(1),
            Child = label,
        }.Themed(Border.BorderBrushProperty, colourKey)
         .Themed(Border.BackgroundProperty, Palette.Keys.Hover);
    }
}
