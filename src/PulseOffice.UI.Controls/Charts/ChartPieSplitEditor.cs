using System.Globalization;
using Avalonia.Controls;
using PulseOffice.Charts;

namespace PulseOffice.UI.Controls.Charts;

public sealed class ChartPieSplitEditor : UserControl
{
    private readonly PieSplitKind[] _kinds =
        [PieSplitKind.Automatic, PieSplitKind.Position, PieSplitKind.Value, PieSplitKind.Percent, PieSplitKind.Custom];

    private readonly ChartEditorFields _fields;
    private readonly ChartPieSplit _original;
    private readonly ComboBox _kind;
    private readonly TextBox _threshold;
    private readonly TextBox _size;
    private readonly bool _loading;

    public ChartPieSplitEditor(ChartEditorFields fields, ChartPieSplit split)
    {
        _fields = fields ?? throw new ArgumentNullException(nameof(fields));
        _original = split ?? ChartPieSplit.Automatic;
        _loading = true;
        _kind = fields.Choice("chart.pieSplit.kind", _kinds.Select(kind => $"chart.pieSplit.kind.{fields.Lower(kind.ToString())}"), Edited);
        _threshold = fields.Text("chart.pieSplit.threshold", "chart.pieSplit.threshold.hint", Edited);
        _size = fields.Number("chart.pieSplit.secondSize", Edited);

        Content = new StackPanel
        {
            Spacing = fields.Metrics.SpacingSmall,
            Children =
            {
                fields.Field("chart.pieSplit.kind", _kind),
                fields.Field("chart.pieSplit.threshold", _threshold),
                fields.Field("chart.pieSplit.secondSize", _size),
            },
        };

        _kind.SelectedIndex = Math.Max(0, Array.IndexOf(_kinds, _original.Kind));
        _threshold.Text = _original.Kind == PieSplitKind.Custom
            ? string.Join(' ', _original.CustomPoints.Select(point => (point + 1).ToString(fields.Culture)))
            : _original.Kind == PieSplitKind.Automatic ? string.Empty : fields.Shown(_original.Threshold);
        _size.Text = _original.SecondPlotSize.ToString(fields.Culture);
        _threshold.IsEnabled = _original.Kind != PieSplitKind.Automatic;
        _loading = false;
    }

    public event EventHandler? Changed;

    public ChartPieSplit Split
    {
        get
        {
            var kind = _kind.SelectedIndex >= 0 && _kind.SelectedIndex < _kinds.Length ? _kinds[_kind.SelectedIndex] : PieSplitKind.Automatic;
            var size = _fields.Parsed(_size) is { } parsed && double.IsFinite(parsed)
                ? (int)Math.Clamp(Math.Round(parsed), ChartPieSplit.MinimumSecondPlotSize, ChartPieSplit.MaximumSecondPlotSize)
                : ChartPieSplit.DefaultSecondPlotSize;
            var split = new ChartPieSplit
            {
                Kind = kind,
                Threshold = kind is PieSplitKind.Position or PieSplitKind.Value or PieSplitKind.Percent ? _fields.Parsed(_threshold) ?? 0 : 0,
                CustomPoints = kind == PieSplitKind.Custom ? Points(_threshold.Text) : [],
                SecondPlotSize = size,
            };
            return split.IsAutomatic ? ChartPieSplit.Automatic : split;
        }
    }

    private int[] Points(string? text) =>
        [.. (text ?? string.Empty)
            .Split([' ', ',', ';'], StringSplitOptions.RemoveEmptyEntries)
            .Select(item => int.TryParse(item, NumberStyles.Integer, _fields.Culture, out var point) ? point - 1 : -1)
            .Where(point => point >= 0)
            .Distinct()
            .Order()];

    private void Edited()
    {
        if (_loading) return;
        _threshold.IsEnabled = _kind.SelectedIndex > 0;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
