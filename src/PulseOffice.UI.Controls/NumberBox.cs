using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls;

public sealed class NumberBox : UserControl
{
    private readonly NumberEntry _entry;
    private readonly TextBox _box;
    private double _value;

    public NumberBox(
        NumberEntry entry, double initial, DesignTokens.Metrics metrics,
        IIconCatalog? icons = null, string? unit = null, double boxWidth = 64)
    {
        _entry = entry ?? throw new ArgumentNullException(nameof(entry));
        _value = entry.Normalize(initial);

        _box = new TextBox
        {
            Width = boxWidth,
            MinWidth = 0,
            MinHeight = 0,
            Padding = new Thickness(metrics.SpacingSmall, 2),
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            TextAlignment = TextAlignment.Right,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        _box.Classes.Add("settingsNumber");
        _box.Classes.Add("measure");
        _box.LostFocus += (_, _) => Accept();
        _box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                Accept();
                return;
            }
            if (e.Key == Key.Escape) Show();
            else if (e.Key == Key.Up) Step(1);
            else if (e.Key == Key.Down) Step(-1);
            else return;

            e.Handled = true;
        };

        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(_box);

        if (unit is { Length: > 0 })
            row.Children.Add(new TextBlock
            {
                Text = unit,
                FontSize = metrics.FontSizeSmall,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, metrics.SpacingSmall, 0),
            }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary));

        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Stretch };
        stack.Children.Add(StepButton(icons, "ui.chevronUp", "▴", 1, metrics));
        stack.Children.Add(StepButton(icons, "ui.chevronDown", "▾", -1, metrics));
        row.Children.Add(new Border { BorderThickness = new Thickness(1, 0, 0, 0), Child = stack }
            .Themed(Border.BorderBrushProperty, Palette.Keys.Border));

        Content = new Border
        {
            Height = metrics.ControlHeight,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(metrics.RadiusTight),
            ClipToBounds = true,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Child = row,
            Classes = { "measureFrame" },
        }.Themed(Border.BackgroundProperty, Palette.Keys.Surface);

        Show();
    }

    public event EventHandler<double>? ValueCommitted;

    public double Value
    {
        get => _value;
        set
        {
            _value = _entry.Normalize(value);
            Show();
        }
    }

    public string Text => _box.Text ?? string.Empty;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == Avalonia.Automation.AutomationProperties.NameProperty)
            Avalonia.Automation.AutomationProperties.SetName(_box, change.GetNewValue<string?>());
        else if (change.Property == Avalonia.Automation.AutomationProperties.LabeledByProperty)
        {
            if (change.GetNewValue<Control?>() is { } label) Avalonia.Automation.AutomationProperties.SetLabeledBy(_box, label);
            else _box.ClearValue(Avalonia.Automation.AutomationProperties.LabeledByProperty);
        }
        else if (change.Property == Avalonia.Automation.AutomationProperties.HelpTextProperty)
            Avalonia.Automation.AutomationProperties.SetHelpText(_box, change.GetNewValue<string?>());
    }

    public double Read()
    {
        Accept();
        return _value;
    }

    private void Show() => _box.Text = _entry.Format(_value);

    private void Accept()
    {
        if (!_entry.TryParse(_box.Text, out var typed))
        {
            Show();
            return;
        }

        Set(typed);
    }

    private void Step(int direction) => Set(_entry.Step(_value, direction));

    private void Set(double value)
    {
        var changed = Math.Abs(value - _value) > double.Epsilon;
        _value = value;
        Show();
        if (changed) ValueCommitted?.Invoke(this, value);
    }

    private Button StepButton(IIconCatalog? icons, string icon, string glyph, int direction, DesignTokens.Metrics metrics)
    {
        var button = new Button
        {
            Width = 20,
            Height = (metrics.ControlHeight - 2) / 2,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Focusable = false,
            Content = icons?.Find(icon) is { } geometry
                ? new IconView(geometry, 9).Themed(IconView.ForegroundProperty, Palette.Keys.TextSecondary)
                : new TextBlock { Text = glyph, FontSize = 9 }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary),
        };

        button.Classes.Add("ribbon");
        button.Click += (_, _) => Step(direction);
        return button;
    }
}
