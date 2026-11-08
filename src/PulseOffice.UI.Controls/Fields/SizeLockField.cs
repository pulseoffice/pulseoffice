using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Fields;

public sealed record LockedSize(double Width, double Height, bool Locked);

public sealed class SizeLock : UserControl
{
    private readonly MeasureField _width;
    private readonly MeasureField _height;
    private readonly ToggleButton _lock;
    private double? _ratio;
    private bool _following;

    public SizeLock(MeasureEntry width, MeasureEntry height, LockedSize initial, ITextCatalog texts, DesignTokens.Metrics metrics, IIconCatalog? icons = null)
    {
        ArgumentNullException.ThrowIfNull(initial);
        ArgumentNullException.ThrowIfNull(texts);
        _width = new MeasureField(width, initial.Width, texts, metrics, icons, texts["size.width"], boxWidth: 80);
        _height = new MeasureField(height, initial.Height, texts, metrics, icons, texts["size.height"], boxWidth: 80);
        _ratio = Ratio(initial.Width, initial.Height);
        _lock = new ToggleButton
        {
            IsChecked = initial.Locked,
            Width = metrics.ControlHeight,
            Height = metrics.ControlHeight * 2 + metrics.SpacingSmall,
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            CornerRadius = new CornerRadius(metrics.RadiusTight),
            Content = icons?.Find("size.lock") is { } geometry
                ? new IconView(geometry, metrics.IconSize).Themed(IconView.ForegroundProperty, Palette.Keys.TextPrimary)
                : new TextBlock { Text = "∞" },
        };
        _lock.Classes.Add("iconChoice");
        AutomationProperties.SetName(_lock, texts["size.lock"]);
        ToolTip.SetTip(_lock, texts["size.lock"]);
        _lock.IsCheckedChanged += (_, _) => Remember();

        _width.ValueCommitted += (_, value) => Follow(ratio => _height.Value = value / ratio);
        _height.ValueCommitted += (_, value) => Follow(ratio => _width.Value = value * ratio);

        var labels = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto"), ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto"), RowSpacing = metrics.SpacingSmall, ColumnSpacing = metrics.SpacingSmall };
        var widthLabel = new TextBlock { Text = texts["size.width"], VerticalAlignment = VerticalAlignment.Center, FontSize = metrics.FontSize };
        var heightLabel = new TextBlock { Text = texts["size.height"], VerticalAlignment = VerticalAlignment.Center, FontSize = metrics.FontSize };
        Grid.SetColumn(_width, 1);
        Grid.SetRow(heightLabel, 1);
        Grid.SetRow(_height, 1);
        Grid.SetColumn(_height, 1);
        Grid.SetColumn(_lock, 2);
        Grid.SetRowSpan(_lock, 2);
        labels.Children.Add(widthLabel);
        labels.Children.Add(_width);
        labels.Children.Add(heightLabel);
        labels.Children.Add(_height);
        labels.Children.Add(_lock);
        _width.AttachScrubber(widthLabel);
        _height.AttachScrubber(heightLabel);
        Content = labels;
    }

    public MeasureField WidthField => _width;

    public MeasureField HeightField => _height;

    public ToggleButton LockButton => _lock;

    public LockedSize Value => new(_width.Read(), _height.Read(), _lock.IsChecked == true);

    private void Remember() => _ratio = Ratio(_width.Value, _height.Value);

    private double? Ratio(double width, double height) =>
        width > 0 && height > 0 && double.IsFinite(width / height) ? width / height : null;

    private void Follow(Action<double> change)
    {
        if (_following) return;
        if (_lock.IsChecked != true || _ratio is not { } ratio)
        {
            Remember();
            return;
        }
        _following = true;
        try
        {
            change(ratio);
        }
        finally
        {
            _following = false;
        }
    }
}

public sealed record SizeLockField(string Key, string Label, LockedSize Initial, string Unit, double Minimum, double Maximum, double Step, IIconCatalog? Icons = null)
    : CustomFormField(Key, Label)
{
    public override FormFieldEditor Create(ITextCatalog texts, DesignTokens.Metrics metrics)
    {
        var units = new MeasureUnits();
        MeasureEntry Entry() => new(new MeasureOptions(Unit, Minimum, Maximum, Step), units, CultureInfo.CurrentCulture);
        var control = new SizeLock(Entry(), Entry(), Initial, texts, metrics, Icons);
        return new FormFieldEditor(control, () => control.Value, Labelled: false)
        {
            Observe = changed =>
            {
                control.WidthField.ValueCommitted += (_, _) => changed();
                control.HeightField.ValueCommitted += (_, _) => changed();
            },
        };
    }
}
