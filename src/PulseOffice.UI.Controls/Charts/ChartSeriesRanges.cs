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
    PulseOffice.UI.Controls.References.IReferencePicker? Picker,
    ChartSeriesTables? Tables = null);

public sealed record ChartTableChoice(string Name, IReadOnlyList<string> Columns);

public sealed record ChartTableColumn(string Table, string? Column);

public sealed record ChartSeriesTables(
    Func<IReadOnlyList<ChartTableChoice>> Choices,
    Func<string, ChartTableColumn?> Describe,
    Func<string, string, string> Reference);

public sealed class ChartSeriesRanges : StackPanel
{
    private readonly ChartSeriesRangeEditing _editing;
    private readonly ITextCatalog _texts;
    private readonly DesignTokens.Metrics _metrics;
    private readonly List<TextBox> _boxes = [];
    private readonly Dictionary<TextBox, TextBlock> _captions = new();
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
        _captions.Clear();

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
            if (_editing.Tables is not null)
            {
                var caption = Caption(box);
                _captions[box] = caption;
                Children.Add(caption);
            }
        }

        _filling = false;
    }

    private void Changed(TextBox box)
    {
        if (_captions.TryGetValue(box, out var caption)) Describe(box, caption);
        if (_filling) return;
        PerSeries = true;
        box.Classes.Set("invalid", _editing.Normalize(Text(box)) is null);
        Edited?.Invoke(this, EventArgs.Empty);
    }

    private TextBlock Caption(TextBox box)
    {
        var caption = new TextBlock { FontSize = _metrics.FontSizeSmall, TextTrimming = TextTrimming.CharacterEllipsis }
            .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
        Describe(box, caption);
        return caption;
    }

    private void Describe(TextBox box, TextBlock caption)
    {
        var described = _editing.Tables?.Describe(Text(box));
        caption.Text = described switch
        {
            null => string.Empty,
            { Column: { } column } => _texts.Format("chart.seriesRange.tableColumn", described.Table, column),
            _ => _texts.Format("chart.seriesRange.tableWhole", described.Table),
        };
        caption.IsVisible = described is not null;
    }

    private Button? TablePicker(TextBox box)
    {
        if (_editing.Tables is not { } tables) return null;
        var button = new Button { Content = _texts["chart.seriesRange.table"] };
        Avalonia.Automation.AutomationProperties.SetName(button, _texts["chart.seriesRange.tablePick"]);
        var flyout = new MenuFlyout();
        flyout.Opening += (_, _) =>
        {
            flyout.Items.Clear();
            var choices = tables.Choices();
            if (choices.Count == 0)
                flyout.Items.Add(new MenuItem { Header = _texts["chart.seriesRange.noTables"], IsEnabled = false });
            foreach (var table in choices)
            {
                var item = new MenuItem { Header = table.Name };
                foreach (var column in table.Columns)
                {
                    var pick = new MenuItem { Header = column };
                    pick.Click += (_, _) =>
                    {
                        box.Text = tables.Reference(table.Name, column);
                        box.Focus();
                    };
                    item.Items.Add(pick);
                }
                item.IsEnabled = table.Columns.Count > 0;
                flyout.Items.Add(item);
            }
        };
        button.Flyout = flyout;
        return button;
    }

    private Control Row(TextBox box)
    {
        var tablePicker = TablePicker(box);
        if (_editing.Picker is not { } picker)
        {
            if (tablePicker is null) return box;
            var plain = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = _metrics.SpacingSmall };
            Grid.SetColumn(tablePicker, 1);
            plain.Children.Add(box);
            plain.Children.Add(tablePicker);
            return plain;
        }

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = _metrics.SpacingSmall };
        if (tablePicker is not null)
        {
            Grid.SetColumn(tablePicker, 2);
            row.Children.Add(tablePicker);
        }
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
