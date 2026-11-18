using Avalonia.Controls;
using PulseOffice.Charts;

namespace PulseOffice.UI.Controls.Charts;

public enum ChartAxisRole { Category, Value, SecondaryValue }

public sealed class ChartAxisScaleEditor : UserControl
{
    private static readonly ChartAxisRole[] Roles = [ChartAxisRole.Category, ChartAxisRole.Value, ChartAxisRole.SecondaryValue];
    private static readonly AxisCrossing[] Crossings = [AxisCrossing.Automatic, AxisCrossing.Minimum, AxisCrossing.Maximum, AxisCrossing.Value];

    private readonly ChartEditorFields _fields;
    private readonly ChartAxisScale[] _scales;
    private readonly ComboBox _axis;
    private readonly TextBox _minimum;
    private readonly TextBox _maximum;
    private readonly TextBox _majorUnit;
    private readonly TextBox _minorUnit;
    private readonly CheckBox _logarithmic;
    private readonly CheckBox _reversed;
    private readonly ComboBox _crossing;
    private readonly TextBox _crossingValue;
    private readonly TextBox _numberFormat;
    private int _shown = (int)ChartAxisRole.Value;
    private bool _loading;

    public ChartAxisScaleEditor(ChartEditorFields fields, ChartAxisScale category, ChartAxisScale value, ChartAxisScale secondary)
    {
        _fields = fields ?? throw new ArgumentNullException(nameof(fields));
        _scales = [category ?? ChartAxisScale.Automatic, value ?? ChartAxisScale.Automatic, secondary ?? ChartAxisScale.Automatic];
        _axis = fields.Choice("chart.scale.axis", ["chart.axis.category", "chart.axis.value", "chart.axis.secondaryValue"], AxisChosen);
        _minimum = fields.Number("chart.scale.minimum", Edited);
        _maximum = fields.Number("chart.scale.maximum", Edited);
        _majorUnit = fields.Number("chart.scale.majorUnit", Edited);
        _minorUnit = fields.Number("chart.scale.minorUnit", Edited);
        _logarithmic = fields.Check("chart.scale.logarithmic", Edited);
        _reversed = fields.Check("chart.scale.reversed", Edited);
        _crossing = fields.Choice("chart.scale.crossing", Crossings.Select(crossing => $"chart.scale.crossing.{fields.Lower(crossing.ToString())}"), Edited);
        _crossingValue = fields.Number("chart.scale.crossingValue", Edited);
        _numberFormat = fields.Text("chart.numberFormat", "chart.numberFormat.source", Edited);

        Content = new StackPanel
        {
            Spacing = fields.Metrics.SpacingSmall,
            Children =
            {
                fields.Field("chart.scale.axis", _axis),
                fields.Pair(fields.Field("chart.scale.minimum", _minimum), fields.Field("chart.scale.maximum", _maximum)),
                fields.Pair(fields.Field("chart.scale.majorUnit", _majorUnit), fields.Field("chart.scale.minorUnit", _minorUnit)),
                _logarithmic,
                _reversed,
                fields.Field("chart.scale.crossing", _crossing),
                fields.Field("chart.scale.crossingValue", _crossingValue),
                fields.Field("chart.numberFormat", _numberFormat),
            },
        };

        _loading = true;
        _axis.SelectedIndex = _shown;
        _loading = false;
        Load();
    }

    public event EventHandler? Changed;

    public ChartAxisRole Shown => Roles[_shown];

    public ChartAxisScale Scale(ChartAxisRole role)
    {
        var scale = _scales[(int)role];
        return scale.IsAutomatic ? ChartAxisScale.Automatic : scale;
    }

    public void Show(ChartAxisRole role) => _axis.SelectedIndex = (int)role;

    private void AxisChosen()
    {
        if (_loading || _axis.SelectedIndex < 0) return;
        _shown = Math.Clamp(_axis.SelectedIndex, 0, Roles.Length - 1);
        Load();
    }

    private void Load()
    {
        _loading = true;
        var scale = _scales[_shown];
        _minimum.Text = _fields.Shown(scale.Minimum);
        _maximum.Text = _fields.Shown(scale.Maximum);
        _majorUnit.Text = _fields.Shown(scale.MajorUnit);
        _minorUnit.Text = _fields.Shown(scale.MinorUnit);
        _logarithmic.IsChecked = scale.Logarithmic;
        _logarithmic.IsEnabled = Roles[_shown] != ChartAxisRole.Category;
        _reversed.IsChecked = scale.Reversed;
        _crossing.SelectedIndex = Math.Max(0, Array.IndexOf(Crossings, scale.Crossing));
        _crossingValue.Text = scale.Crossing == AxisCrossing.Value ? _fields.Shown(scale.CrossingValue) : string.Empty;
        _crossingValue.IsEnabled = scale.Crossing == AxisCrossing.Value;
        _numberFormat.Text = scale.NumberFormat ?? string.Empty;
        _loading = false;
    }

    private void Edited()
    {
        if (_loading) return;
        var crossing = _crossing.SelectedIndex >= 0 && _crossing.SelectedIndex < Crossings.Length ? Crossings[_crossing.SelectedIndex] : AxisCrossing.Automatic;
        var original = _scales[_shown];
        _scales[_shown] = original with
        {
            Minimum = _fields.Parsed(_minimum),
            Maximum = _fields.Parsed(_maximum),
            MajorUnit = _fields.Parsed(_majorUnit) is > 0 and var major ? major : null,
            MinorUnit = _fields.Parsed(_minorUnit) is > 0 and var minor ? minor : null,
            Logarithmic = Roles[_shown] != ChartAxisRole.Category && _logarithmic.IsChecked == true,
            Reversed = _reversed.IsChecked == true,
            Crossing = crossing,
            CrossingValue = crossing == AxisCrossing.Value ? _fields.Parsed(_crossingValue) ?? original.CrossingValue : original.CrossingValue,
            NumberFormat = _fields.Cleaned(_numberFormat),
        };
        _crossingValue.IsEnabled = crossing == AxisCrossing.Value;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
