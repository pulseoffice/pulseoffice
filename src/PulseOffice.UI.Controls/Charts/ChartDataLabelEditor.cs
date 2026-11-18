using Avalonia.Controls;
using PulseOffice.Charts;

namespace PulseOffice.UI.Controls.Charts;

public sealed class ChartDataLabelEditor : UserControl
{
    private static readonly DataLabelPlacement[] Placements =
    [
        DataLabelPlacement.Automatic, DataLabelPlacement.Center, DataLabelPlacement.InsideEnd, DataLabelPlacement.InsideBase,
        DataLabelPlacement.OutsideEnd, DataLabelPlacement.Above, DataLabelPlacement.Below, DataLabelPlacement.Left, DataLabelPlacement.Right,
    ];

    private readonly ChartEditorFields _fields;
    private readonly ChartDataLabelOptions _original;
    private readonly CheckBox _shown;
    private readonly CheckBox _value;
    private readonly CheckBox _percent;
    private readonly CheckBox _category;
    private readonly CheckBox _seriesName;
    private readonly ComboBox _placement;
    private readonly TextBox _numberFormat;
    private readonly StackPanel _details;
    private bool _loading = true;

    public ChartDataLabelEditor(ChartEditorFields fields, bool shown, ChartDataLabelOptions options)
    {
        _fields = fields ?? throw new ArgumentNullException(nameof(fields));
        _original = options ?? ChartDataLabelOptions.ValuesOnly;
        _shown = fields.Check("chart.showDataLabels", Edited);
        _value = fields.Check("chart.labels.value", Edited);
        _percent = fields.Check("chart.labels.percent", Edited);
        _category = fields.Check("chart.labels.category", Edited);
        _seriesName = fields.Check("chart.labels.seriesName", Edited);
        _placement = fields.Choice("chart.labels.placement", Placements.Select(placement => $"chart.labels.placement.{fields.Lower(placement.ToString())}"), Edited);
        _numberFormat = fields.Text("chart.numberFormat", "chart.numberFormat.source", Edited);

        _details = new StackPanel
        {
            Spacing = fields.Metrics.SpacingSmall,
            Children =
            {
                fields.Pair(_value, _percent),
                fields.Pair(_category, _seriesName),
                fields.Field("chart.labels.placement", _placement),
                fields.Field("chart.numberFormat", _numberFormat),
            },
        };
        Content = new StackPanel { Spacing = fields.Metrics.SpacingSmall, Children = { _shown, _details } };

        _shown.IsChecked = shown;
        _value.IsChecked = _original.ShowValue;
        _percent.IsChecked = _original.ShowPercent;
        _category.IsChecked = _original.ShowCategory;
        _seriesName.IsChecked = _original.ShowSeriesName;
        _placement.SelectedIndex = Math.Max(0, Array.IndexOf(Placements, _original.Placement));
        _numberFormat.Text = _original.NumberFormat ?? string.Empty;
        _details.IsEnabled = shown;
        _loading = false;
    }

    public event EventHandler? Changed;

    public bool ShowsLabels => _shown.IsChecked == true;

    public ChartDataLabelOptions Options
    {
        get
        {
            var options = _original with
            {
                ShowValue = _value.IsChecked == true,
                ShowPercent = _percent.IsChecked == true,
                ShowCategory = _category.IsChecked == true,
                ShowSeriesName = _seriesName.IsChecked == true,
                Placement = _placement.SelectedIndex >= 0 && _placement.SelectedIndex < Placements.Length ? Placements[_placement.SelectedIndex] : DataLabelPlacement.Automatic,
                NumberFormat = _fields.Cleaned(_numberFormat),
            };
            return options.IsValuesOnly ? ChartDataLabelOptions.ValuesOnly : options;
        }
    }

    public CheckBox ShowToggle => _shown;

    private void Edited()
    {
        if (_loading) return;
        _details.IsEnabled = ShowsLabels;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
