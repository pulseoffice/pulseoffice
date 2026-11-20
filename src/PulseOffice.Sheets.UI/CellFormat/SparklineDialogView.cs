using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using PulseOffice.Localization;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Choices;
using PulseOffice.UI.Controls.Dialogs;
using PulseOffice.UI.Controls.Galleries;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Theming;

namespace PulseOffice.Sheets.UI.CellFormat;

public sealed record SparklineSource(CellRange Range, string? Sheet);

public sealed record SparklineDialogStart(string Destination, string Dates, SparklineGroup Group);

public sealed record SparklineDialogResult(string Data, string Destination, SparklineKind Kind, SparklineStyle Style, bool SameScale)
{
    public SparklineEmptyCells EmptyCells { get; init; }

    public bool ShowHidden { get; init; }

    public bool RightToLeft { get; init; }

    public SparklineAxisBound MinimumAxis { get; init; } = SparklineAxisBound.Group;

    public SparklineAxisBound MaximumAxis { get; init; } = SparklineAxisBound.Group;

    public double? ManualMinimum { get; init; }

    public double? ManualMaximum { get; init; }

    public string Dates { get; init; } = string.Empty;
}

public sealed class SparklineDialogServices(ISparklinePainter painter, ISparklineData data, IGalleryNavigator navigator)
{
    public ISparklinePainter Painter { get; } = painter ?? throw new ArgumentNullException(nameof(painter));

    public ISparklineData Data { get; } = data ?? throw new ArgumentNullException(nameof(data));

    public IGalleryNavigator Navigator { get; } = navigator ?? throw new ArgumentNullException(nameof(navigator));
}

public sealed class SparklineSample(ISparklinePainter painter, Func<SparklinePoints> points, Func<SparklineKind> kind, Func<SparklineStyle> style) : Control
{
    private static readonly IBrush Paper = new ImmutableSolidColorBrush(Colors.White);
    private static readonly IPen Grid = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromRgb(0xD1, 0xD5, 0xDB)), 1);

    public override void Render(DrawingContext context)
    {
        var box = new Rect(Bounds.Size).Deflate(0.5);
        if (box.Width <= 4 || box.Height <= 4) return;
        context.DrawRectangle(Paper, Grid, box);
        painter.Paint(context, box.Deflate(new Thickness(4, 3)), points(), kind(), style());
    }
}

public sealed class SparklineKindPainter(ISparklinePainter painter, Func<SparklinePoints> points, Func<SparklineStyle> style) : IThumbnailPainter<SparklineKind>
{
    public Control Create(SparklineKind item, Size size) => new SparklineSample(painter, points, () => item, style) { Height = size.Height };

    public void Repaint(Control thumbnail, SparklineKind item) => thumbnail.InvalidateVisual();
}

public sealed class SparklineDialogView : UserControl, IDialogContent, IDialogFooterOwner
{
    private static readonly string[] Colours = ["#2563EB", "#16A34A", "#DC2626", "#EA580C", "#7C3AED", "#0F172A", "#64748B"];

    private readonly Worksheet _sheet;
    private readonly Func<string, SparklineSource?> _read;
    private readonly SparklineDialogServices _services;
    private readonly RangeFieldControl _data;
    private readonly RangeFieldControl _destination;
    private readonly RangeFieldControl _dates;
    private readonly SparklineBoundField _minimum;
    private readonly SparklineBoundField _maximum;
    private readonly ThumbnailGallery<SparklineKind> _kinds;
    private readonly SparklineSample _preview;
    private readonly CheckBox _showHidden;
    private readonly CheckBox _rightToLeft;
    private readonly ComboBox _emptyCells;
    private readonly DialogFooter _footer;
    private SparklineKind _kind = SparklineKind.Line;
    private SparklineStyle _style = new();

    public SparklineDialogView(
        Worksheet sheet, string data, IReferenceSource ranges, Func<string, SparklineSource?> read, SparklineDialogServices services, ITextCatalog texts,
        SparklineDialogStart? start = null)
    {
        _sheet = sheet ?? throw new ArgumentNullException(nameof(sheet));
        _read = read ?? throw new ArgumentNullException(nameof(read));
        _services = services ?? throw new ArgumentNullException(nameof(services));
        ArgumentNullException.ThrowIfNull(texts);
        var metrics = DesignTokens.For(InterfaceDensity.Compact);
        var initial = start?.Group ?? new SparklineGroup { Id = "new", Sparklines = [], SameScale = true };
        _kind = initial.Kind;
        _style = initial.Style;

        _data = new RangeFieldControl(new RangeField("data", texts["sparkline.data"], data, ranges), texts, metrics);
        _destination = new RangeFieldControl(new RangeField("destination", texts["sparkline.destination"], start?.Destination ?? string.Empty, ranges), texts, metrics);
        _dates = new RangeFieldControl(new RangeField("dates", texts["sparkline.dates"], start?.Dates ?? string.Empty, ranges) { Optional = true }, texts, metrics);
        _dates.LostFocus += (_, _) => Refresh();

        _kinds = new ThumbnailGallery<SparklineKind>(
        [
            new(SparklineKind.Line, texts["sparkline.kind.line"]),
            new(SparklineKind.Column, texts["sparkline.kind.column"]),
            new(SparklineKind.Stacked, texts["sparkline.kind.stacked"]),
        ], new SparklineKindPainter(services.Painter, Points, () => _style), services.Navigator,
            new ThumbnailGalleryOptions { TileWidth = 112, ThumbnailHeight = 40, Columns = 3, FrameRadius = metrics.RadiusTight, AccessibleName = texts["sparkline.kind"] },
            metrics);
        _kinds.Select(_kind);
        _kinds.SelectionChanged += (_, kind) =>
        {
            _kind = kind;
            Refresh();
        };

        _preview = new SparklineSample(services.Painter, Points, () => _kind, () => _style) { Width = 200, Height = 56 };
        AutomationProperties.SetName(_preview, texts["format.preview"]);
        _data.LostFocus += (_, _) => Refresh();
        _data.KeyUp += (_, _) => Refresh();

        var colour = Strip(texts, metrics, texts["sparkline.colour"], _style.ColorHex, value => _style with { ColorHex = value });
        var negative = Strip(texts, metrics, texts["sparkline.negativeColour"], _style.NegativeHex, value => _style with { NegativeHex = value });
        var markers = Strip(texts, metrics, texts["sparkline.markerColour"], _style.MarkerHex, value => _style with { MarkerHex = value });
        var high = Strip(texts, metrics, texts["sparkline.highColour"], _style.HighHex ?? _style.MarkerHex, value => _style with { HighHex = value });
        var low = Strip(texts, metrics, texts["sparkline.lowColour"], _style.LowHex ?? _style.MarkerHex, value => _style with { LowHex = value });
        var firstPoint = Strip(texts, metrics, texts["sparkline.firstColour"], _style.FirstHex ?? _style.MarkerHex, value => _style with { FirstHex = value });
        var lastPoint = Strip(texts, metrics, texts["sparkline.lastColour"], _style.LastHex ?? _style.MarkerHex, value => _style with { LastHex = value });
        var axis = Strip(texts, metrics, texts["sparkline.axisColour"], _style.AxisHex, value => _style with { AxisHex = value });

        var points = new WrapPanel { ItemSpacing = metrics.SpacingMedium, LineSpacing = metrics.SpacingSmall };
        points.Children.Add(Flag(texts["sparkline.point.high"], _style.ShowHighPoint, value => _style with { ShowHighPoint = value }));
        points.Children.Add(Flag(texts["sparkline.point.low"], _style.ShowLowPoint, value => _style with { ShowLowPoint = value }));
        points.Children.Add(Flag(texts["sparkline.point.first"], _style.ShowFirstPoint, value => _style with { ShowFirstPoint = value }));
        points.Children.Add(Flag(texts["sparkline.point.last"], _style.ShowLastPoint, value => _style with { ShowLastPoint = value }));
        points.Children.Add(Flag(texts["sparkline.point.negative"], _style.ShowNegative, value => _style with { ShowNegative = value }));
        points.Children.Add(Flag(texts["sparkline.point.markers"], _style.ShowMarkers, value => _style with { ShowMarkers = value }));
        points.Children.Add(Flag(texts["sparkline.axis"], _style.ShowAxis, value => _style with { ShowAxis = value }));

        _emptyCells = new ComboBox
        {
            ItemsSource = new[] { texts["sparkline.emptyCells.gap"], texts["sparkline.emptyCells.zero"], texts["sparkline.emptyCells.span"] },
            SelectedIndex = (int)initial.EmptyCells,
        };
        AutomationProperties.SetName(_emptyCells, texts["sparkline.emptyCells"]);
        _emptyCells.SelectionChanged += (_, _) => Refresh();
        _showHidden = new CheckBox { Content = texts["sparkline.showHidden"], IsChecked = initial.ShowHidden };
        _showHidden.IsCheckedChanged += (_, _) => Refresh();
        _rightToLeft = new CheckBox { Content = texts["sparkline.rightToLeft"], IsChecked = initial.RightToLeft };
        _rightToLeft.IsCheckedChanged += (_, _) => Refresh();

        _minimum = new SparklineBoundField(texts["sparkline.minimum"], initial.MinimumAxis, initial.ManualMinimum, texts);
        _minimum.Changed += (_, _) => Refresh();
        _maximum = new SparklineBoundField(texts["sparkline.maximum"], initial.MaximumAxis, initial.ManualMaximum, texts);
        _maximum.Changed += (_, _) => Refresh();

        var layout = new TwoPaneDialogLayout(metrics) { Explanation = texts["sparkline.explanation"], Preview = _preview };
        var source = layout.AddSection(texts["sparkline.section.data"]);
        source.Add(texts["sparkline.data"], _data, editorWidth: 240);
        source.Add(texts["sparkline.destination"], _destination, editorWidth: 240);
        source.Add(texts["sparkline.dates"], _dates, editorWidth: 240);
        var type = layout.AddSection(texts["sparkline.kind"]);
        type.Add(null, _kinds, DialogRowKind.Wide, editorWidth: 360);
        var look = layout.AddSection(texts["sparkline.section.style"]);
        look.Add(texts["sparkline.colour"], colour, editorWidth: 260);
        look.Add(texts["sparkline.negativeColour"], negative, editorWidth: 260);
        look.Add(texts["sparkline.markerColour"], markers, editorWidth: 260);
        look.Add(null, points, DialogRowKind.Indented, editorWidth: 300);
        look.Add(texts["sparkline.highColour"], high, editorWidth: 260);
        look.Add(texts["sparkline.lowColour"], low, editorWidth: 260);
        look.Add(texts["sparkline.firstColour"], firstPoint, editorWidth: 260);
        look.Add(texts["sparkline.lastColour"], lastPoint, editorWidth: 260);
        look.Add(texts["sparkline.axisColour"], axis, editorWidth: 260);
        var scale = layout.AddSection(texts["sparkline.section.axis"]);
        scale.Add(texts["sparkline.minimum"], _minimum, editorWidth: 280);
        scale.Add(texts["sparkline.maximum"], _maximum, editorWidth: 280);
        var cells = layout.AddSection(texts["sparkline.section.cells"]);
        cells.Add(texts["sparkline.emptyCells"], _emptyCells, editorWidth: 200);
        cells.Add(null, _showHidden, DialogRowKind.Indented);
        cells.Add(null, _rightToLeft, DialogRowKind.Indented);

        _footer = new DialogFooter()
            .WithCancel(texts["prompt.cancel"], () => Completed?.Invoke(this, null))
            .WithAction(texts["prompt.confirm"], () => Completed?.Invoke(this, Result), primary: true);

        Content = layout;
    }

    public event EventHandler<object?>? Completed;

    public Control? Footer => _footer;

    public ThumbnailGallery<SparklineKind> Kinds => _kinds;

    public SparklineSample Preview => _preview;

    public RangeFieldControl Destination => _destination;

    public RangeFieldControl Dates => _dates;

    public SparklineBoundField Minimum => _minimum;

    public SparklineBoundField Maximum => _maximum;

    public SparklineDialogResult Result => new(
        _data.Text.Trim(), _destination.Text.Trim(), _kind, _style,
        _minimum.Bound == SparklineAxisBound.Group && _maximum.Bound == SparklineAxisBound.Group)
    {
        EmptyCells = EmptyCells,
        ShowHidden = _showHidden.IsChecked == true,
        RightToLeft = _rightToLeft.IsChecked == true,
        MinimumAxis = _minimum.Bound,
        MaximumAxis = _maximum.Bound,
        ManualMinimum = _minimum.Manual,
        ManualMaximum = _maximum.Manual,
        Dates = _dates.Text.Trim(),
    };

    private SparklineEmptyCells EmptyCells => _emptyCells?.SelectedIndex switch
    {
        1 => SparklineEmptyCells.Zero,
        2 => SparklineEmptyCells.Span,
        _ => SparklineEmptyCells.Gap,
    };

    public SparklinePoints Points()
    {
        if (_read(_data.Text.Trim()) is not { } source) return SparklinePoints.Empty;
        var range = source.Range;
        var first = range.RowCount == 1 || range.ColumnCount == 1
            ? range
            : new CellRange(range.Start, new CellAddress(range.Start.Row, range.End.Column));
        var line = new Sparkline(range.Start, first, source.Sheet);
        var dates = _dates is { Text.Length: > 0 } ? _read(_dates.Text) : null;
        var group = new SparklineGroup
        {
            Id = "preview",
            Sparklines = [line],
            Kind = _kind,
            Style = _style,
            EmptyCells = EmptyCells,
            ShowHidden = _showHidden?.IsChecked == true,
            RightToLeft = _rightToLeft?.IsChecked == true,
            MinimumAxis = _minimum?.Bound == SparklineAxisBound.Custom ? SparklineAxisBound.Custom : SparklineAxisBound.Individual,
            MaximumAxis = _maximum?.Bound == SparklineAxisBound.Custom ? SparklineAxisBound.Custom : SparklineAxisBound.Individual,
            ManualMinimum = _minimum?.Manual,
            ManualMaximum = _maximum?.Manual,
            DateRange = dates?.Range,
            DateSheet = dates?.Sheet,
        };
        return _services.Data.Read(_sheet, group, line);
    }

    private SwatchStrip Strip(ITextCatalog texts, DesignTokens.Metrics metrics, string caption, string selected, Func<string, SparklineStyle> change)
    {
        var strip = new SwatchStrip([.. Colours.Select(hex => new Swatch(hex, hex, hex))], texts, metrics, caption, selected);
        strip.Changed += (_, _) =>
        {
            if (strip.Value is { } value) Change(change(value));
        };
        return strip;
    }

    private CheckBox Flag(string caption, bool value, Func<bool, SparklineStyle> change)
    {
        var box = new CheckBox { Content = caption, IsChecked = value };
        box.IsCheckedChanged += (_, _) => Change(change(box.IsChecked == true));
        return box;
    }

    private void Change(SparklineStyle style)
    {
        _style = style;
        Refresh();
    }

    private void Refresh()
    {
        _kinds.RefreshThumbnails();
        _preview.InvalidateVisual();
    }
}
