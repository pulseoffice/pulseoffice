using System.Globalization;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using PulseOffice.Localization;

namespace PulseOffice.Sheets.UI.CellFormat;

public sealed class SparklineBoundField : UserControl
{
    private readonly ComboBox _mode;
    private readonly TextBox _value;

    public SparklineBoundField(string accessibleName, SparklineAxisBound bound, double? manual, ITextCatalog texts)
    {
        ArgumentNullException.ThrowIfNull(texts);

        _mode = new ComboBox
        {
            ItemsSource = new[] { texts["sparkline.bound.individual"], texts["sparkline.bound.group"], texts["sparkline.bound.custom"] },
            SelectedIndex = (int)bound,
            MinWidth = 180,
        };
        AutomationProperties.SetName(_mode, accessibleName);
        _value = new TextBox
        {
            Width = 90,
            Text = manual?.ToString(CultureInfo.CurrentCulture) ?? string.Empty,
            IsEnabled = bound == SparklineAxisBound.Custom,
        };
        AutomationProperties.SetName(_value, texts["sparkline.bound.value"]);
        _mode.SelectionChanged += (_, _) =>
        {
            _value.IsEnabled = Bound == SparklineAxisBound.Custom;
            Changed?.Invoke(this, EventArgs.Empty);
        };
        _value.LostFocus += (_, _) => Changed?.Invoke(this, EventArgs.Empty);

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        row.Children.Add(_mode);
        row.Children.Add(_value);
        Content = row;
    }

    public event EventHandler? Changed;

    public ComboBox Mode => _mode;

    public TextBox ValueBox => _value;

    public SparklineAxisBound Bound => _mode.SelectedIndex switch
    {
        1 => SparklineAxisBound.Group,
        2 => SparklineAxisBound.Custom,
        _ => SparklineAxisBound.Individual,
    };

    public double? Manual =>
        Bound == SparklineAxisBound.Custom
        && double.TryParse(_value.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value)
        && double.IsFinite(value)
            ? value
            : null;
}
