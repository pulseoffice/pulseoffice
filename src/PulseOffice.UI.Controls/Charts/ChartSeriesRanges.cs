using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PulseOffice.Charts;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Charts;

public sealed record ChartSeriesRangeEditing(
    Func<ChartSettings, IReadOnlyList<ChartSeriesSource>> Expand,
    Func<string, string?> Normalize,
    PulseOffice.UI.Controls.References.IReferencePicker? Picker);

public sealed class ChartSeriesRanges : StackPanel
{
    private readonly ChartSeriesRangeEditing _editing;
    private readonly ITextCatalog _texts;
    private readonly DesignTokens.Metrics _metrics;
    private readonly List<TextBox> _boxes = [];
    private IReadOnlyList<ChartSeriesSource> _sources = [];
    private bool _filling;

    public ChartSeriesRanges(ChartSeriesRangeEditing editing, ITextCatalog texts, DesignTokens.Metrics metrics)
    {
        _editing = editing ?? throw new ArgumentNullException(nameof(editing));
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        _metrics = metrics;
        Spacing = metrics.SpacingSmall;
    }

    public event EventHandler? Edited;

    public bool PerSeries { get; private set; }

    public bool Valid => !PerSeries || _boxes.All(box => _editing.Normalize(Text(box)) is not null);

    public IReadOnlyList<ChartSeriesSource>? Sources =>
        PerSeries
            ? [.. _sources.Select((source, index) => source with { Values = index < _boxes.Count ? _editing.Normalize(Text(_boxes[index])) ?? source.Values : source.Values })]
            : null;

    public void Show(ChartSettings settings, ChartData data, bool perSeries)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(data);

        PerSeries = perSeries && settings.SeriesSources is { Count: > 0 };
        _sources = PerSeries ? settings.SeriesSources! : _editing.Expand(settings);
        _filling = true;
        Children.Clear();
        _boxes.Clear();

        for (var index = 0; index < _sources.Count; index++)
        {
            var name = index < data.Series.Count ? data.Series[index].Name : (index + 1).ToString(System.Globalization.CultureInfo.CurrentCulture);
            var box = new TextBox { Text = _sources[index].Values ?? string.Empty, PlaceholderText = _texts["chart.range"] };
            Avalonia.Automation.AutomationProperties.SetName(box, _texts.Format("chart.seriesRange.values", name));
            box.TextChanged += (_, _) => Changed(box);
            _boxes.Add(box);
            Children.Add(new TextBlock { Text = name, FontSize = _metrics.FontSizeSmall, TextTrimming = TextTrimming.CharacterEllipsis }
                .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary));
            Children.Add(Row(box));
        }

        _filling = false;
    }

    private void Changed(TextBox box)
    {
        if (_filling) return;
        PerSeries = true;
        box.Classes.Set("invalid", _editing.Normalize(Text(box)) is null);
        Edited?.Invoke(this, EventArgs.Empty);
    }

    private Control Row(TextBox box)
    {
        if (_editing.Picker is not { } picker) return box;

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = _metrics.SpacingSmall };
        var pick = new PulseOffice.UI.Controls.References.ReferencePickButton(picker, _texts, _metrics);
        pick.Click += async (_, _) =>
        {
            var chosen = await picker.PickAsync(new PulseOffice.UI.Controls.References.ReferencePickRequest(
                _texts["chart.range"], Text(box), text => _editing.Normalize(text) is not null)).ConfigureAwait(true);
            if (chosen is not null) box.Text = chosen;
            box.Focus();
        };
        Grid.SetColumn(pick, 1);
        row.Children.Add(box);
        row.Children.Add(pick);
        return row;
    }

    private string Text(TextBox box) => (box.Text ?? string.Empty).Trim();
}
