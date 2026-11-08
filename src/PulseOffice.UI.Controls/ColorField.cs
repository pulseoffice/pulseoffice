using PulseOffice.Foundation.Colors;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls;

public sealed class ColorFieldValue
{
    public string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var trimmed = text.Trim();
        if (!trimmed.StartsWith('#')) trimmed = "#" + trimmed;

        var digits = trimmed[1..];
        if (digits.Length == 3) digits = string.Concat(digits.Select(digit => new string(digit, 2)));
        if (digits.Length == 8) digits = digits[2..];

        return digits.Length == 6 && int.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _)
            ? "#" + digits.ToUpperInvariant()
            : string.Empty;
    }

    public string Hex(uint? argb) => argb is { } value ? new ArgbColor(value).ToHashedHex() : string.Empty;

    public uint? Argb(string? text) =>
        ArgbColor.FromRgbHex(Normalize(text))?.Value;
}

public sealed record ColorField(string Key, string Label, string? Initial, bool AllowNone = true)
    : CustomFormField(Key, Label)
{
    public override FormFieldEditor Create(ITextCatalog texts, DesignTokens.Metrics metrics)
    {
        var editor = new ColorFieldControl(new ColorFieldValue().Normalize(Initial), AllowNone, texts, metrics);
        return new FormFieldEditor(editor, () => editor.Value) { Observe = changed => editor.Changed += (_, _) => changed() };
    }
}

public sealed class ColorFieldControl : UserControl
{
    private readonly ColorFieldValue _values = new();
    private readonly Border _swatch;
    private readonly TextBlock _caption;
    private readonly ITextCatalog _texts;
    private readonly bool _allowNone;
    private readonly ColorPickerView _picker;

    public ColorFieldControl(string value, bool allowNone, ITextCatalog texts, DesignTokens.Metrics metrics, IFlyoutDismissal? dismissal = null)
    {
        ArgumentNullException.ThrowIfNull(texts);

        _texts = texts;
        _allowNone = allowNone;
        Value = value;

        _swatch = new Border
        {
            Width = 22,
            Height = 16,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(metrics.RadiusTight),
            VerticalAlignment = VerticalAlignment.Center,
        }.Themed(Border.BorderBrushProperty, Palette.Keys.Border);

        _caption = new TextBlock
        {
            FontSize = metrics.FontSize,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var face = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(metrics.SpacingSmall, metrics.SpacingSmall / 2),
            CornerRadius = new CornerRadius(metrics.RadiusTight),
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = metrics.SpacingSmall,
                Children = { _swatch, _caption },
            },
        };

        _picker = new ColorPickerView(value.Length > 0 ? value : null, 1, texts, allowNone);
        _picker.Changed += (_, _) => Set(_picker.IsNone ? string.Empty : _picker.Value);

        var flyout = new Flyout
        {
            Placement = PlacementMode.BottomEdgeAlignedLeft,
            Content = _picker,
        };

        var flyouts = dismissal ?? new FlyoutDismissal();
        flyouts.PassThrough(flyout);
        face.Click += (_, _) => flyouts.Toggle(flyout, face);

        Content = face;
        Show();
    }

    public string Value { get; private set; }

    public event EventHandler? Changed;

    public void Select(string? value)
    {
        Value = _values.Normalize(value);
        _picker.Select(Value.Length > 0 ? Value : null);
        Show();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Set(string value)
    {
        Value = _values.Normalize(value);
        Show();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Show()
    {
        if (Value.Length > 0 && Color.TryParse(Value, out var colour))
        {
            _swatch.Background = new SolidColorBrush(colour);
            _caption.Text = Value;
        }
        else
        {
            _swatch.Background = Brushes.Transparent;
            _caption.Text = _texts[_allowNone ? "colour.none" : "colour.automatic"];
        }
    }
}
