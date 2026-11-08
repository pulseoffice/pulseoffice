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

public sealed record SparklineDialogResult(string Data, string Destination, SparklineKind Kind, SparklineStyle Style, bool SameScale);

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
    private readonly Func<string, CellRange?> _read;
    private readonly SparklineDialogServices _services;
    private readonly RangeFieldControl _data;
    private readonly RangeFieldControl _destination;
    private readonly ThumbnailGallery<SparklineKind> _kinds;
    private readonly SparklineSample _preview;
    private readonly CheckBox _sameScale;
    private readonly DialogFooter _footer;
    private SparklineKind _kind = SparklineKind.Line;
    private SparklineStyle _style = new();

    public SparklineDialogView(
        Worksheet sheet, string data, IReferenceSource ranges, Func<string, CellRange?> read, SparklineDialogServices services, ITextCatalog texts)
    {
        _sheet = sheet ?? throw new ArgumentNullException(nameof(sheet));
        _read = read ?? throw new ArgumentNullException(nameof(read));
        _services = services ?? throw new ArgumentNullException(nameof(services));
        ArgumentNullException.ThrowIfNull(texts);
        var metrics = DesignTokens.For(InterfaceDensity.Compact);

        _data = new RangeFieldControl(new RangeField("data", texts["sparkline.data"], data, ranges), texts, metrics);
        _destination = new RangeFieldControl(new RangeField("destination", texts["sparkline.destination"], string.Empty, ranges), texts, metrics);

        _kinds = new ThumbnailGallery<SparklineKind>(
        [
            new(SparklineKind.Line, texts["sparkline.kind.line"]),
            new(SparklineKind.Column, texts["sparkline.kind.column"]),
            new(SparklineKind.Stacked, texts["sparkline.kind.stacked"]),
        ], new SparklineKindPainter(services.Painter, Points, () => _style), services.Navigator,
            new ThumbnailGalleryOptions { TileWidth = 112, ThumbnailHeight = 40, Columns = 3, FrameRadius = metrics.RadiusTight, AccessibleName = texts["sparkline.kind"] },
            metrics);
        _kinds.Select(SparklineKind.Line);
        _kinds.SelectionChanged += (_, kind) =>
        {
            _kind = kind;
            Refresh();
        };

        _preview = new SparklineSample(services.Painter, Points, () => _kind, () => _style) { Width = 200, Height = 56 };
        AutomationProperties.SetName(_preview, texts["format.preview"]);
        _data.LostFocus += (_, _) => Refresh();
        _data.KeyUp += (_, _) => Refresh();

        var colour = new SwatchStrip([.. Colours.Select(hex => new Swatch(hex, hex, hex))], texts, metrics, texts["sparkline.colour"], _style.ColorHex);
        colour.Changed += (_, _) =>
        {
            if (colour.Value is { } value) Change(_style with { ColorHex = value });
        };
        var negative = new SwatchStrip([.. Colours.Select(hex => new Swatch(hex, hex, hex))], texts, metrics, texts["sparkline.negativeColour"], _style.NegativeHex);
        negative.Changed += (_, _) =>
        {
            if (negative.Value is { } value) Change(_style with { NegativeHex = value });
        };
        var markers = new SwatchStrip([.. Colours.Select(hex => new Swatch(hex, hex, hex))], texts, metrics, texts["sparkline.markerColour"], _style.MarkerHex);
        markers.Changed += (_, _) =>
        {
            if (markers.Value is { } value) Change(_style with { MarkerHex = value });
        };

        var points = new WrapPanel { ItemSpacing = metrics.SpacingMedium, LineSpacing = metrics.SpacingSmall };
        points.Children.Add(Flag(texts["sparkline.point.high"], _style.ShowHighPoint, value => _style with { ShowHighPoint = value }));
        points.Children.Add(Flag(texts["sparkline.point.low"], _style.ShowLowPoint, value => _style with { ShowLowPoint = value }));
        points.Children.Add(Flag(texts["sparkline.point.first"], _style.ShowFirstPoint, value => _style with { ShowFirstPoint = value }));
        points.Children.Add(Flag(texts["sparkline.point.last"], _style.ShowLastPoint, value => _style with { ShowLastPoint = value }));
        points.Children.Add(Flag(texts["sparkline.point.negative"], _style.ShowNegative, value => _style with { ShowNegative = value }));

        _sameScale = new CheckBox { Content = texts["sparkline.sameScale"], IsChecked = true };

        var layout = new TwoPaneDialogLayout(metrics) { Explanation = texts["sparkline.explanation"], Preview = _preview };
        var source = layout.AddSection(texts["sparkline.section.data"]);
        source.Add(texts["sparkline.data"], _data, editorWidth: 240);
        source.Add(texts["sparkline.destination"], _destination, editorWidth: 240);
        var type = layout.AddSection(texts["sparkline.kind"]);
        type.Add(null, _kinds, DialogRowKind.Wide, editorWidth: 360);
        var look = layout.AddSection(texts["sparkline.section.style"]);
        look.Add(texts["sparkline.colour"], colour, editorWidth: 260);
        look.Add(texts["sparkline.negativeColour"], negative, editorWidth: 260);
        look.Add(texts["sparkline.markerColour"], markers, editorWidth: 260);
        look.Add(null, points, DialogRowKind.Indented, editorWidth: 300);
        look.Add(null, _sameScale, DialogRowKind.Indented);

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

    public SparklineDialogResult Result => new(_data.Text.Trim(), _destination.Text.Trim(), _kind, _style, _sameScale.IsChecked == true);

    public SparklinePoints Points()
    {
        if (_read(_data.Text.Trim()) is not { } range) return SparklinePoints.Empty;
        var first = range.RowCount == 1 || range.ColumnCount == 1
            ? range
            : new CellRange(range.Start, new CellAddress(range.Start.Row, range.End.Column));
        var line = new Sparkline(range.Start, first);
        var group = new SparklineGroup { Id = "preview", Sparklines = [line], Kind = _kind, Style = _style };
        return _services.Data.Read(_sheet, group, line);
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
