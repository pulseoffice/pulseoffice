using Avalonia.Controls;
using PulseOffice.Charts;

namespace PulseOffice.UI.Controls.Charts;

public sealed class ChartSeriesFormatEditor : UserControl
{
    private const int SmallestMarker = 2;
    private const int LargestMarker = 72;

    private static readonly LineDash[] Dashes = [LineDash.Solid, LineDash.Dash, LineDash.Dot, LineDash.DashDot];

    private static readonly MarkerSymbol[] Markers =
    [
        MarkerSymbol.Automatic, MarkerSymbol.None, MarkerSymbol.Circle, MarkerSymbol.Square, MarkerSymbol.Diamond, MarkerSymbol.Triangle,
        MarkerSymbol.Cross, MarkerSymbol.Star, MarkerSymbol.Plus, MarkerSymbol.Dash, MarkerSymbol.Dot,
    ];

    private readonly ChartEditorFields _fields;
    private readonly List<ChartSeriesFormat> _formats;
    private readonly ComboBox _series;
    private readonly TextBox _lineWidth;
    private readonly ComboBox _dash;
    private readonly ComboBox _marker;
    private readonly TextBox _markerSize;
    private readonly CheckBox _secondary;
    private readonly TextBox _point;
    private readonly ColorFieldControl _pointColour;
    private readonly TextBox _explosion;
    private int _shown;
    private bool _loading;

    public ChartSeriesFormatEditor(ChartEditorFields fields, IReadOnlyList<ChartSeriesFormat>? formats, IReadOnlyList<string> names)
    {
        _fields = fields ?? throw new ArgumentNullException(nameof(fields));
        _formats = new List<ChartSeriesFormat>(formats ?? Array.Empty<ChartSeriesFormat>());
        _series = new ComboBox { HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch };
        _series.SelectionChanged += (_, _) => SeriesChosen();
        Avalonia.Automation.AutomationProperties.SetName(_series, fields.Texts["chart.series.choose"]);
        _lineWidth = fields.Number("chart.series.lineWidth", Edited);
        _dash = fields.Choice("chart.series.dash", Dashes.Select(dash => $"chart.dash.{fields.Lower(dash.ToString())}"), Edited);
        _marker = fields.Choice("chart.series.marker", Markers.Select(marker => $"chart.marker.{fields.Lower(marker.ToString())}"), Edited);
        _markerSize = fields.Number("chart.series.markerSize", Edited);
        _secondary = fields.Check("chart.series.secondaryAxis", Edited);
        _point = fields.Number("chart.point.number", PointChosen);
        _point.PlaceholderText = fields.Texts["chart.point.none"];
        _pointColour = new ColorFieldControl(string.Empty, true, fields.Texts, fields.Metrics);
        _pointColour.Changed += (_, _) => PointEdited();
        Avalonia.Automation.AutomationProperties.SetName(_pointColour, fields.Texts["chart.point.colour"]);
        _explosion = fields.Number("chart.point.explosion", PointEdited);

        Content = new StackPanel
        {
            Spacing = fields.Metrics.SpacingSmall,
            Children =
            {
                fields.Field("chart.series.choose", _series),
                fields.Pair(fields.Field("chart.series.lineWidth", _lineWidth), fields.Field("chart.series.dash", _dash)),
                fields.Pair(fields.Field("chart.series.marker", _marker), fields.Field("chart.series.markerSize", _markerSize)),
                _secondary,
                fields.Field("chart.point.number", _point),
                fields.Pair(fields.Field("chart.point.colour", _pointColour), fields.Field("chart.point.explosion", _explosion)),
            },
        };

        ShowSeries(names);
    }

    public event EventHandler? Changed;

    public IReadOnlyList<ChartSeriesFormat> Formats => _formats;

    public int Shown => _shown;

    public void Choose(int series) => _series.SelectedIndex = series;

    public void ShowSeries(IReadOnlyList<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);
        while (_formats.Count < names.Count) _formats.Add(ChartSeriesFormat.Plain);
        _loading = true;
        _series.ItemsSource = names.Select((name, index) => string.IsNullOrWhiteSpace(name)
            ? string.Format(_fields.Culture, _fields.Texts["chart.series.fallback"], index + 1)
            : name).ToArray();
        _shown = names.Count == 0 ? 0 : Math.Clamp(_shown, 0, names.Count - 1);
        _series.SelectedIndex = names.Count == 0 ? -1 : _shown;
        _loading = false;
        IsEnabled = names.Count > 0;
        Load();
    }

    private void SeriesChosen()
    {
        if (_loading || _series.SelectedIndex < 0) return;
        _shown = _series.SelectedIndex;
        Load();
    }

    private ChartSeriesFormat Current => _shown < _formats.Count ? _formats[_shown] : ChartSeriesFormat.Plain;

    private void Load()
    {
        _loading = true;
        var format = Current;
        _lineWidth.Text = _fields.Shown(format.LineWidthPoints);
        _dash.SelectedIndex = Math.Max(0, Array.IndexOf(Dashes, format.Dash));
        _marker.SelectedIndex = Math.Max(0, Array.IndexOf(Markers, format.Marker));
        _markerSize.Text = format.MarkerSize is { } size ? size.ToString(_fields.Culture) : string.Empty;
        _secondary.IsChecked = format.SecondaryAxis;
        _loading = false;
        LoadPoint();
    }

    public int? ChosenPoint => _fields.Parsed(_point) is { } number && number >= 1 && number <= MostPoints ? (int)Math.Round(number) - 1 : null;

    private const int MostPoints = 100_000;

    public void ChoosePoint(int? point) => _point.Text = point is { } index ? (index + 1).ToString(_fields.Culture) : string.Empty;

    private void PointChosen()
    {
        if (_loading) return;
        LoadPoint();
    }

    private void LoadPoint()
    {
        _loading = true;
        var point = ChosenPoint is { } index ? Current.Point(index) : null;
        _pointColour.IsEnabled = point is not null;
        _explosion.IsEnabled = point is not null;
        _pointColour.Select(point?.ColorHex);
        _explosion.Text = point is { ExplosionPercent: > 0 } exploded ? exploded.ExplosionPercent.ToString(_fields.Culture) : string.Empty;
        _loading = false;
    }

    private void PointEdited()
    {
        if (_loading || _shown >= _formats.Count || ChosenPoint is not { } index) return;
        var explosion = _fields.Parsed(_explosion) is > 0 and var percent ? (int)Math.Round(Math.Min(percent, ChartPointFormats.MaximumExplosion)) : 0;
        _formats[_shown] = Current.WithPoint(new ChartPointFormat(index)
        {
            ColorHex = _pointColour.Value.Length > 0 ? _pointColour.Value : null,
            ExplosionPercent = explosion,
        });
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Edited()
    {
        if (_loading || _shown >= _formats.Count) return;
        var size = _fields.Parsed(_markerSize);
        _formats[_shown] = Current with
        {
            LineWidthPoints = _fields.Parsed(_lineWidth) is > 0 and var width ? width : null,
            Dash = _dash.SelectedIndex >= 0 && _dash.SelectedIndex < Dashes.Length ? Dashes[_dash.SelectedIndex] : LineDash.Solid,
            Marker = _marker.SelectedIndex >= 0 && _marker.SelectedIndex < Markers.Length ? Markers[_marker.SelectedIndex] : MarkerSymbol.Automatic,
            MarkerSize = size is { } chosen ? Math.Clamp((int)Math.Round(chosen), SmallestMarker, LargestMarker) : null,
            SecondaryAxis = _secondary.IsChecked == true,
        };
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
