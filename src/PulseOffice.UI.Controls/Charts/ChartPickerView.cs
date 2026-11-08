using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Charts;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Charts;

public sealed record ChartPickerServices(
    IChartFamilyCatalog Families,
    IChartRecommender Recommender,
    IChartSampleData Samples,
    ChartRenderer Renderer,
    IChartSettingsMapper Mapper,
    IIconCatalog Icons,
    ITextCatalog Texts);

public sealed class ChartPickerView : UserControl, IDialogContent, IDialogFooterOwner
{
    private const double FamilyListWidth = 210;
    private const double TileWidth = 150;
    private const double ThumbnailHeight = 72;
    private const int TilesPerRow = 5;
    private const double FamilyIconSize = 18;

    private readonly ChartPickerRequest _request;
    private readonly ChartPickerServices _services;
    private DialogChrome? _chrome;
    private readonly DesignTokens.Metrics _metrics = DesignTokens.For(InterfaceDensity.Compact);
    private readonly List<ChartPickerSection> _sections;
    private readonly List<Button> _sectionButtons = [];
    private readonly List<Button> _tiles = [];
    private readonly WrapPanel _tilePanel;
    private readonly TextBlock _sectionTitle = new();
    private readonly TextBlock _typeName = new();
    private readonly ChartPreview _preview;
    private readonly TextBox _title = new();
    private readonly ComboBox _legend = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly CheckBox _header = new();
    private readonly TextBox _categoryAxis = new();
    private readonly TextBox _valueAxis = new();
    private readonly CheckBox _labels = new();
    private readonly CheckBox _grid = new();
    private readonly ComboBox _seriesLayout = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBox _range = new();
    private readonly TextBlock _rangeError = new() { TextWrapping = TextWrapping.Wrap, IsVisible = false };
    private readonly ChartSeriesRanges? _seriesRanges;

    private static readonly LegendPosition[] LegendPositions =
    [
        LegendPosition.None, LegendPosition.Right, LegendPosition.Left, LegendPosition.Top, LegendPosition.Bottom,
    ];

    private ChartData _data;
    private int _section;
    private ChartType _type;
    private bool _titleTouched;

    public ChartPickerView(ChartPickerRequest request, ChartPickerServices services)
    {
        _request = request ?? throw new ArgumentNullException(nameof(request));
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _preview = new ChartPreview(services.Renderer, request.Culture);
        _tilePanel = new WrapPanel
        {
            ItemSpacing = _metrics.SpacingSmall,
            LineSpacing = _metrics.SpacingSmall,
            MaxWidth = TilesPerRow * (TileWidth + _metrics.SpacingSmall),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        _data = request.Data(request.Settings);
        if (request.SeriesRanges is { } seriesRanges)
        {
            _seriesRanges = new ChartSeriesRanges(seriesRanges, services.Texts, _metrics);
            _seriesRanges.Show(request.Settings, _data, perSeries: true);
            _seriesRanges.Edited += (_, _) => Reload();
        }
        _sections = BuildSections(services.Recommender.Recommend(_data, request.Culture));
        var editing = request.Mode == ChartPickerMode.Edit;
        _type = editing ? request.Settings.Type : _sections[0].Types[0];
        _section = editing
            ? Math.Max(0, _sections.ToList().FindIndex(section => section.Family == services.Families.FamilyOf(_type)))
            : 0;

        Focusable = true;
        Content = Layout();

        Footer = new DialogFooter()
            .WithCancel(Texts["prompt.cancel"], () => Completed?.Invoke(this, null))
            .WithAction(Texts[editing ? "chart.picker.apply" : "chart.picker.insert"], Confirm, primary: true);

        ShowSection(_section, keepType: true);
        AttachedToVisualTree += (_, _) => Focus();
    }

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; }

    private ITextCatalog Texts => _services.Texts;

    private List<ChartPickerSection> BuildSections(IReadOnlyList<ChartType> recommended) =>
    [
        RecommendedSection(recommended),
        .. _services.Families.Families.Select(info =>
            new ChartPickerSection(info.Family, $"chart.family.{Lower(info.Family.ToString())}", info.Types)),
    ];

    private Control Layout()
    {
        var layout = new Grid { ColumnDefinitions = new ColumnDefinitions($"{FamilyListWidth},Auto,*") };
        var divider = new Border { Width = 1, Margin = new Thickness(_metrics.SpacingLarge, 0) }
            .Themed(Border.BackgroundProperty, Palette.Keys.Border);
        var main = Main();
        Grid.SetColumn(divider, 1);
        Grid.SetColumn(main, 2);
        layout.Children.Add(FamilyList());
        layout.Children.Add(divider);
        layout.Children.Add(main);
        return layout;
    }

    private Control FamilyList()
    {
        var stack = new StackPanel { Spacing = 2 };

        for (var i = 0; i < _sections.Count; i++)
        {
            if (i == 1) stack.Children.Add(Caption(Texts["chart.picker.families"]));

            var index = i;
            var section = _sections[i];
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = _metrics.SpacingSmall,
                Children = { Icon(section.TitleKey), new TextBlock { Text = Texts[section.TitleKey], VerticalAlignment = VerticalAlignment.Center } },
            };

            var button = new Button
            {
                Content = row,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(_metrics.SpacingSmall, 6),
                BorderThickness = default,
            };

            button.Classes.Add("ribbon");
            Avalonia.Automation.AutomationProperties.SetName(button, Texts[section.TitleKey]);
            button.Click += (_, _) => ShowSection(index, keepType: false);
            _sectionButtons.Add(button);
            stack.Children.Add(button);
        }

        return new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = stack,
        };
    }

    private Control Icon(string key)
    {
        if (_services.Icons.Find(key) is not { } geometry) return new Border { Width = FamilyIconSize };
        return new IconView(geometry, FamilyIconSize) { VerticalAlignment = VerticalAlignment.Center };
    }

    private Control Main()
    {
        _sectionTitle.FontSize = _metrics.FontSizeHeading;
        _sectionTitle.FontWeight = FontWeight.SemiBold;
        _typeName.FontWeight = FontWeight.SemiBold;
        _typeName.TextTrimming = TextTrimming.CharacterEllipsis;

        var tiles = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = (ThumbnailHeight + 44) * 2 + _metrics.SpacingSmall,
            Content = _tilePanel,
        };

        var previewFrame = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(_metrics.RadiusSoft),
            ClipToBounds = true,
            Child = _preview,
        }.Themed(Border.BorderBrushProperty, Palette.Keys.Border);

        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,*"),
            ColumnDefinitions = new ColumnDefinitions("*,Auto,240"),
        };

        Place(grid, _sectionTitle, 0, 0, 3);
        Place(grid, tiles, 1, 0, 3);
        Place(grid, Caption(Texts["chart.picker.preview"]), 2, 0, 1);
        Place(grid, _typeName, 3, 0, 1);
        Place(grid, previewFrame, 4, 0, 1);
        var gap = new Border { Width = _metrics.SpacingLarge };
        Place(grid, gap, 4, 1, 1);
        Place(grid, Options(), 4, 2, 1);
        _typeName.Margin = new Thickness(0, 0, 0, _metrics.SpacingSmall);
        tiles.Margin = new Thickness(0, _metrics.SpacingSmall, 0, _metrics.SpacingMedium);
        return grid;
    }

    private void Place(Grid grid, Control control, int row, int column, int span)
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column);
        Grid.SetColumnSpan(control, span);
        grid.Children.Add(control);
    }

    private Control Options()
    {
        var settings = _request.Settings;

        _title.Text = settings.Title ?? string.Empty;
        _title.TextChanged += (_, _) =>
        {
            if (_title.IsFocused) _titleTouched = true;
            Refresh();
        };

        _categoryAxis.Text = settings.CategoryAxisTitle ?? string.Empty;
        _categoryAxis.PlaceholderText = Texts["chart.axis.category"];
        _categoryAxis.TextChanged += (_, _) => Refresh();
        _valueAxis.Text = settings.ValueAxisTitle ?? string.Empty;
        _valueAxis.PlaceholderText = Texts["chart.axis.value"];
        _valueAxis.TextChanged += (_, _) => Refresh();

        _legend.ItemsSource = LegendPositions.Select(position => Texts[$"chart.legend.{Lower(position.ToString())}"]).ToArray();
        _legend.SelectedIndex = Math.Max(0, Array.IndexOf(LegendPositions, settings.Legend));
        _legend.SelectionChanged += (_, _) => Refresh();

        Check(_labels, "chart.showDataLabels", settings.ShowDataLabels, Refresh);
        Check(_grid, "chart.showGridLines", settings.ShowGridLines, Refresh);
        Check(_header, "chart.firstRowIsHeader", settings.FirstRowIsHeader, Reload);
        _header.IsEnabled = _request.HeaderEditable;

        _seriesLayout.ItemsSource = new[] { Texts["chart.series.inColumns"], Texts["chart.series.inRows"] };
        _seriesLayout.SelectedIndex = settings.SeriesInColumns ? 0 : 1;
        _seriesLayout.IsEnabled = _request.SeriesOrientationEditable;
        _seriesLayout.SelectionChanged += (_, _) => Reload();

        Avalonia.Automation.AutomationProperties.SetName(_title, Texts["chart.title"]);
        Avalonia.Automation.AutomationProperties.SetName(_categoryAxis, Texts["chart.axis.category"]);
        Avalonia.Automation.AutomationProperties.SetName(_valueAxis, Texts["chart.axis.value"]);
        Avalonia.Automation.AutomationProperties.SetName(_legend, Texts["chart.legend"]);
        Avalonia.Automation.AutomationProperties.SetName(_range, Texts["chart.range"]);
        Avalonia.Automation.AutomationProperties.SetName(_seriesLayout, Texts["chart.picker.data"]);

        var stack = new StackPanel
        {
            Spacing = _metrics.SpacingSmall,
            Children =
            {
                Caption(Texts["chart.title"]),
                _title,
                Caption(Texts["chart.axes"]),
                _categoryAxis,
                _valueAxis,
                Caption(Texts["chart.picker.appearance"]),
                FieldLabel(Texts["chart.legend"]),
                _legend,
                _labels,
                _grid,
                Caption(Texts["chart.picker.data"]),
            },
        };

        if (_request.RangeValidator is not null)
        {
            _range.Text = settings.SourceRange ?? string.Empty;
            _range.PlaceholderText = Texts["chart.range"];
            _range.TextChanged += (_, _) => RangeChanged();
            _rangeError.Text = Texts["status.analysis.badReference"];
            _rangeError.FontSize = _metrics.FontSizeSmall;
            _rangeError.Themed(TextBlock.ForegroundProperty, Palette.Keys.Danger);
            stack.Children.Add(RangeRow(_request.RangeValidator));
            stack.Children.Add(_rangeError);
        }

        stack.Children.Add(_seriesLayout);
        stack.Children.Add(_header);

        if (_seriesRanges is not null)
        {
            stack.Children.Add(Caption(Texts["chart.seriesRanges"]));
            stack.Children.Add(_seriesRanges);
        }

        return new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = stack,
        };
    }

    private void Check(CheckBox box, string key, bool value, Action changed)
    {
        box.Content = new TextBlock { Text = Texts[key], TextWrapping = TextWrapping.Wrap };
        box.IsChecked = value;
        box.IsCheckedChanged += (_, _) => changed();
    }

    private ChartPickerSection RecommendedSection(IReadOnlyList<ChartType> recommended) =>
        new(null, "chart.family.recommended",
            recommended.Count > 0 ? recommended : _services.Families.Families.SelectMany(info => info.Types).Take(1).ToList());

    private bool RangeValid =>
        (_request.RangeValidator is not { } validate || _seriesRanges?.PerSeries == true || validate((_range.Text ?? string.Empty).Trim()))
        && _seriesRanges?.Valid != false;

    private void RangeChanged()
    {
        if (_seriesRanges is not null && (_request.RangeValidator?.Invoke((_range.Text ?? string.Empty).Trim()) ?? false))
            _seriesRanges.Show(CurrentSettings() with { SeriesSources = null }, _data, perSeries: false);
        Reload();
    }

    private void Reload()
    {
        var valid = RangeValid;
        _rangeError.IsVisible = !valid;
        if (!valid)
        {
            Refresh();
            return;
        }

        _data = _request.Data(CurrentSettings());
        var recommended = RecommendedSection(_services.Recommender.Recommend(_data, _request.Culture));
        var changed = !recommended.Types.SequenceEqual(_sections[0].Types);
        _sections[0] = recommended;

        if (changed && _section == 0) ShowSection(0, keepType: true);
        else Refresh();
    }

    private ChartSettings CurrentSettings() => new()
    {
        Type = _type,
        Title = _title.Text,
        Legend = SelectedLegend,
        FirstRowIsHeader = _header.IsChecked == true,
        SeriesInColumns = _seriesLayout.SelectedIndex != 1,
        CategoryAxisTitle = _categoryAxis.Text,
        ValueAxisTitle = _valueAxis.Text,
        ShowDataLabels = _labels.IsChecked == true,
        ShowGridLines = _grid.IsChecked == true,
        SourceRange = _request.RangeValidator is null ? _request.Settings.SourceRange : (_range.Text ?? string.Empty).Trim(),
        SeriesSources = _seriesRanges is null ? _request.Settings.SeriesSources : _seriesRanges.Sources,
    };

    private DialogChrome Chrome => _chrome ??= new DialogChrome(_services.Icons, _metrics);

    private Control RangeRow(Func<string, bool> validator)
    {
        if (_request.RangePicker is not { } picker) return _range;

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = _metrics.SpacingSmall };
        var pick = new PulseOffice.UI.Controls.References.ReferencePickButton(picker, Texts, _metrics);
        pick.Click += async (_, _) =>
        {
            var chosen = await picker.PickAsync(new PulseOffice.UI.Controls.References.ReferencePickRequest(
                Texts["chart.range"], (_range.Text ?? string.Empty).Trim(), validator)).ConfigureAwait(true);

            if (chosen is not null) _range.Text = chosen;
            _range.Focus();
        };

        Grid.SetColumn(pick, 1);
        row.Children.Add(_range);
        row.Children.Add(pick);
        return row;
    }

    private TextBlock Caption(string text) =>
        Chrome.Caption(text, new Thickness(0, _metrics.SpacingSmall, 0, 2));

    private TextBlock FieldLabel(string text) => new TextBlock
    {
        Text = text,
        FontSize = _metrics.FontSizeSmall,
    }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

    private void ShowSection(int index, bool keepType)
    {
        _section = Math.Clamp(index, 0, _sections.Count - 1);
        var section = _sections[_section];

        for (var i = 0; i < _sectionButtons.Count; i++) _sectionButtons[i].Classes.Set("checked", i == _section);

        _sectionTitle.Text = Texts[section.TitleKey];
        _tiles.Clear();
        _tilePanel.Children.Clear();
        foreach (var type in section.Types) _tilePanel.Children.Add(Tile(type));

        if (!keepType || !section.Types.Contains(_type)) _type = section.Types[0];
        Choose(_type);
    }

    private Button Tile(ChartType type)
    {
        var thumbnail = new ChartPreview(_services.Renderer, _request.Culture, ChartRenderMode.Thumbnail) { Height = ThumbnailHeight };
        thumbnail.Show(new ChartDefinition
        {
            Id = "preview",
            Type = type,
            SourceSheet = string.Empty,
            SourceRange = string.Empty,
            Legend = LegendPosition.None,
            ShowGridLines = false,
        }, _services.Samples.For(_services.Families.FamilyOf(type)));

        var body = new StackPanel
        {
            Spacing = _metrics.SpacingSmall / 2,
            Children =
            {
                new Border { CornerRadius = new CornerRadius(_metrics.Radius), ClipToBounds = true, Child = thumbnail },
                new TextBlock
                {
                    Text = TypeName(type),
                    FontSize = _metrics.FontSizeSmall,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    TextAlignment = TextAlignment.Center,
                },
            },
        };

        var button = new Button
        {
            Content = body,
            Width = TileWidth,
            Padding = new Thickness(4),
            BorderThickness = default,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Tag = type,
        };

        button.Classes.Add("ribbon");
        button.Classes.Add("tile");
        button.Click += (_, _) => Choose(type);
        button.DoubleTapped += (_, _) =>
        {
            Choose(type);
            Confirm();
        };
        ToolTip.SetTip(button, TypeName(type));
        Avalonia.Automation.AutomationProperties.SetName(button, TypeName(type));
        _tiles.Add(button);
        return button;
    }

    private void Choose(ChartType type)
    {
        _type = type;
        foreach (var tile in _tiles) tile.Classes.Set("checked", tile.Tag is ChartType tagged && tagged == type);
        Refresh();
    }

    private void Refresh()
    {
        if (!_titleTouched && _request.SuggestsTitle && !_title.IsFocused)
            _title.Text = TypeName(_type);

        _typeName.Text = TypeName(_type);
        _preview.Show(Definition(), _data);
    }

    private ChartDefinition Definition() => _services.Mapper.Apply(new ChartDefinition
    {
        Id = "preview",
        Type = _type,
        SourceSheet = string.Empty,
        SourceRange = string.Empty,
    }, CurrentSettings() with { SourceRange = null });

    private LegendPosition SelectedLegend =>
        _legend.SelectedIndex >= 0 && _legend.SelectedIndex < LegendPositions.Length
            ? LegendPositions[_legend.SelectedIndex]
            : LegendPosition.Right;

    private void Confirm()
    {
        if (!RangeValid)
        {
            _rangeError.IsVisible = true;
            _range.Focus();
            return;
        }

        Completed?.Invoke(this, CurrentSettings());
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Source is TextBox or ComboBox)
        {
            base.OnKeyDown(e);
            return;
        }

        var types = _sections[_section].Types;
        var position = Math.Max(0, types.ToList().IndexOf(_type));
        int? target = e.Key switch
        {
            Key.Left => position - 1,
            Key.Right => position + 1,
            Key.Up => position - TilesPerRow,
            Key.Down => position + TilesPerRow,
            _ => null,
        };

        if (target is { } next)
        {
            Choose(types[Math.Clamp(next, 0, types.Count - 1)]);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            Confirm();
            e.Handled = true;
            return;
        }

        if (e.Key is Key.PageDown or Key.PageUp)
        {
            ShowSection(_section + (e.Key == Key.PageDown ? 1 : -1), keepType: false);
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    private string TypeName(ChartType type) => Texts[$"chart.type.{Lower(type.ToString())}"];

    private string Lower(string name) => char.ToLowerInvariant(name[0]) + name[1..];

    private sealed record ChartPickerSection(ChartFamily? Family, string TitleKey, IReadOnlyList<ChartType> Types);
}
