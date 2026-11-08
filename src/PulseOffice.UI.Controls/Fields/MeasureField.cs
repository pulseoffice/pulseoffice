using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Fields;

public sealed class MeasureField : UserControl
{
    private const double PixelsPerStep = 4;

    private readonly MeasureEntry _entry;
    private readonly ITextCatalog _texts;
    private readonly DesignTokens.Metrics _metrics;
    private readonly TextBox _box;
    private readonly TextBlock _unitText;
    private readonly TextBlock _message;
    private readonly Slider? _slider;
    private double _value;
    private bool _syncing;
    private Point? _scrubOrigin;
    private double _scrubStart;

    public MeasureField(
        MeasureEntry entry, double initial, ITextCatalog texts, DesignTokens.Metrics metrics,
        IIconCatalog? icons = null, string? accessibleName = null, double boxWidth = 72)
    {
        _entry = entry ?? throw new ArgumentNullException(nameof(entry));
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        _metrics = metrics;
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
        if (accessibleName is { Length: > 0 }) AutomationProperties.SetName(_box, accessibleName);
        _box.LostFocus += (_, _) => Accept();
        _box.AddHandler(KeyDownEvent, OnKey, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        _box.TextChanged += (_, _) => { if (!_syncing) Hide(); };

        _unitText = new TextBlock
        {
            FontSize = metrics.FontSizeSmall,
            VerticalAlignment = VerticalAlignment.Center,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(_box);
        row.Children.Add(UnitPart());
        if (entry.Options.Presets.Count > 0) row.Children.Add(PresetsButton(icons));

        var steppers = new StackPanel { VerticalAlignment = VerticalAlignment.Stretch };
        steppers.Children.Add(StepButton(icons, "ui.chevronUp", "▴", 1));
        steppers.Children.Add(StepButton(icons, "ui.chevronDown", "▾", -1));
        row.Children.Add(new Border { BorderThickness = new Thickness(1, 0, 0, 0), Child = steppers }
            .Themed(Border.BorderBrushProperty, Palette.Keys.Border));

        var frame = new Border
        {
            Height = metrics.ControlHeight,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(metrics.RadiusTight),
            ClipToBounds = true,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Child = row,
        }.Themed(Border.BackgroundProperty, Palette.Keys.Surface);
        frame.Classes.Add("measureFrame");
        frame.PointerWheelChanged += OnWheel;

        _message = new TextBlock
        {
            FontSize = metrics.FontSizeSmall,
            TextWrapping = TextWrapping.Wrap,
            IsVisible = false,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.Danger);

        var layout = new StackPanel { Spacing = metrics.SpacingSmall / 2 };

        if (entry.Options.ShowSlider && HasFiniteRange)
        {
            _slider = new Slider
            {
                Minimum = entry.Bounds.Minimum,
                Maximum = entry.Bounds.Maximum,
                Value = _value,
                SmallChange = entry.Bounds.Step,
                LargeChange = entry.LargeStep,
                MinWidth = 120,
                VerticalAlignment = VerticalAlignment.Center,
                Focusable = false,
            };
            _slider.ValueChanged += (_, e) => { if (!_syncing) Set(e.NewValue); };

            var top = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = metrics.SpacingMedium };
            Grid.SetColumn(_slider, 1);
            top.Children.Add(frame);
            top.Children.Add(_slider);
            layout.Children.Add(top);
        }
        else
        {
            layout.Children.Add(frame);
        }

        layout.Children.Add(_message);
        Content = layout;
        Show();
    }

    public event EventHandler<double>? ValueCommitted;

    public MeasureEntry Entry => _entry;

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

    public string UnitLabel => _entry.DisplayLabel;

    public string? Message => _message.IsVisible ? _message.Text : null;

    public double Read()
    {
        Accept();
        return _value;
    }

    public void StepBy(int direction, bool large) => Set(_entry.Step(_value, direction, large));

    public void ShowIn(MeasureUnit unit)
    {
        Accept();
        _entry.ShowIn(unit);
        Show();
    }

    public void AttachScrubber(Control handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        handle.Cursor = new Cursor(StandardCursorType.SizeWestEast);
        handle.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(handle).Properties.IsLeftButtonPressed) return;
            Accept();
            _scrubOrigin = e.GetPosition(handle);
            _scrubStart = _value;
            e.Pointer.Capture(handle);
            e.Handled = true;
        };
        handle.PointerMoved += (_, e) =>
        {
            if (_scrubOrigin is not { } origin) return;
            Scrub(e.GetPosition(handle).X - origin.X, e.KeyModifiers.HasFlag(KeyModifiers.Shift));
        };
        handle.PointerReleased += (_, e) =>
        {
            if (_scrubOrigin is null) return;
            _scrubOrigin = null;
            e.Pointer.Capture(null);
            _box.Focus();
        };
        handle.PointerCaptureLost += (_, _) => _scrubOrigin = null;
    }

    public void Scrub(double horizontalPixels, bool large)
    {
        var steps = (int)Math.Truncate(horizontalPixels / PixelsPerStep);
        var increment = large ? _entry.LargeStep : _entry.Bounds.Step;
        Set(_entry.Normalize(_scrubStart + steps * increment));
    }

    public void BeginScrub()
    {
        Accept();
        _scrubStart = _value;
    }

    private bool HasFiniteRange =>
        _entry.Bounds.Minimum > -NumericBounds.Limit && _entry.Bounds.Maximum < NumericBounds.Limit;

    private void OnKey(object? sender, KeyEventArgs e)
    {
        var large = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        switch (e.Key)
        {
            case Key.Enter:
                Accept();
                return;
            case Key.Escape:
                if (!_message.IsVisible && _box.Text == _entry.Format(_value)) return;
                Show();
                break;
            case Key.Up:
                Accept();
                StepBy(1, large);
                break;
            case Key.Down:
                Accept();
                StepBy(-1, large);
                break;
            case Key.PageUp:
                Accept();
                StepBy(1, true);
                break;
            case Key.PageDown:
                Accept();
                StepBy(-1, true);
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    private void OnWheel(object? sender, PointerWheelEventArgs e)
    {
        if (!_box.IsFocused || e.Delta.Y == 0) return;
        Accept();
        StepBy(Math.Sign(e.Delta.Y), e.KeyModifiers.HasFlag(KeyModifiers.Shift));
        e.Handled = true;
    }

    private void Accept()
    {
        if (_box.Text == _entry.Format(_value)) return;

        var parsed = _entry.Parse(_box.Text);
        switch (parsed.Outcome)
        {
            case MeasureParseOutcome.Invalid:
                Warn(HasFiniteRange ? "measure.invalidRange" : "measure.invalid");
                return;
            case MeasureParseOutcome.Clamped:
                Set(parsed.Value);
                Warn("measure.clamped");
                return;
            default:
                Set(parsed.Value);
                return;
        }
    }

    private void Warn(string key)
    {
        _message.Text = string.Format(CultureInfo.CurrentCulture, _texts[key],
            _entry.FormatBound(_entry.Bounds.Minimum), _entry.FormatBound(_entry.Bounds.Maximum));
        _message.IsVisible = true;
        AutomationProperties.SetHelpText(_box, _message.Text);
    }

    private void Hide()
    {
        if (!_message.IsVisible) return;
        _message.IsVisible = false;
        AutomationProperties.SetHelpText(_box, null);
    }

    private void Set(double value)
    {
        var normalized = _entry.Normalize(value);
        var changed = Math.Abs(normalized - _value) > double.Epsilon;
        _value = normalized;
        Show();
        if (changed) ValueCommitted?.Invoke(this, normalized);
    }

    private void Show()
    {
        _syncing = true;
        try
        {
            _box.Text = _entry.Format(_value);
            _unitText.Text = _entry.DisplayLabel;
            if (_slider is not null) _slider.Value = _value;
            _message.IsVisible = false;
        }
        finally
        {
            _syncing = false;
        }
    }

    private Control UnitPart()
    {
        _unitText.Margin = new Thickness(0, 0, _metrics.SpacingSmall, 0);
        if (_entry.Alternatives.Count < 2) return _unitText;

        var menu = new MenuFlyout { Placement = PlacementMode.BottomEdgeAlignedLeft };
        foreach (var unit in _entry.Alternatives)
        {
            var item = new MenuItem { Header = unit == _entry.Unit ? _entry.BaseLabel : unit.Symbol };
            item.Click += (_, _) => ShowIn(unit);
            menu.Items.Add(item);
        }

        var button = FlatButton(_unitText, _texts["measure.unit"]);
        button.Padding = new Thickness(0, 0, 2, 0);
        _unitText.Margin = new Thickness(0, 0, _metrics.SpacingSmall / 2, 0);
        button.Flyout = menu;
        return button;
    }

    private Button PresetsButton(IIconCatalog? icons)
    {
        var menu = new MenuFlyout { Placement = PlacementMode.BottomEdgeAlignedRight };
        foreach (var preset in _entry.Options.Presets)
        {
            var value = _entry.Normalize(preset);
            var item = new MenuItem();
            item.Click += (_, _) => Set(value);
            menu.Items.Add(item);
        }

        menu.Opening += (_, _) =>
        {
            for (var index = 0; index < menu.Items.Count; index++)
                if (menu.Items[index] is MenuItem item) item.Header = _entry.FormatWithUnit(_entry.Normalize(_entry.Options.Presets[index]));
        };

        var button = FlatButton(Glyph(icons, "ui.chevronDown", "▾", 10), _texts["measure.presets"]);
        button.Width = 18;
        button.Flyout = menu;
        return button;
    }

    private Button FlatButton(Control content, string name)
    {
        var button = new Button
        {
            Padding = new Thickness(0),
            Margin = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            BorderThickness = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Focusable = false,
            Content = content,
        };
        button.Classes.Add("ribbon");
        AutomationProperties.SetName(button, name);
        ToolTip.SetTip(button, name);
        return button;
    }

    private static Control Glyph(IIconCatalog? icons, string icon, string fallback, double size) =>
        icons?.Find(icon) is { } geometry
            ? new IconView(geometry, size).Themed(IconView.ForegroundProperty, Palette.Keys.TextSecondary)
            : new TextBlock { Text = fallback, FontSize = size }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

    private Button StepButton(IIconCatalog? icons, string icon, string glyph, int direction)
    {
        var button = new Button
        {
            Width = 20,
            Height = (_metrics.ControlHeight - 2) / 2,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Focusable = false,
            Content = Glyph(icons, icon, glyph, 9),
        };

        button.Classes.Add("ribbon");
        AutomationProperties.SetName(button, _texts[direction > 0 ? "measure.increase" : "measure.decrease"]);
        button.Click += (_, _) =>
        {
            Accept();
            StepBy(direction, false);
        };
        return button;
    }
}
