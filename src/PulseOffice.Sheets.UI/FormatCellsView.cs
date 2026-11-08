using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using PulseOffice.Document.Borders;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Localization;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Styling;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Borders;
using PulseOffice.UI.Controls.Fills;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;
using PulseOffice.Sheets.UI.CellFormat;

namespace PulseOffice.Sheets.UI;

public enum FormatCellsTab { Numbers, Font, Alignment, Borders, Background, Protection }

public sealed record FormatCellsResult(CellStyle Style, BorderFrame BordersBefore, BorderFrame BordersAfter);

public sealed class FormatCellsView
{
    private readonly CellStyle _original;
    private readonly NumberFormatCatalog _numbers;
    private readonly ITextCatalog _texts;
    private readonly DesignTokens.Metrics _metrics = DesignTokens.For(InterfaceDensity.Compact);

    private CellStyle _style;

    private readonly FormatCellsServices _services;
    private readonly ICellBorderFrames _frames;
    private readonly IBorderFrameResolver _resolver;
    private readonly Values.CellValue _cellValue;
    private readonly string _cellName;
    private NumberFormatPage? _numberPage;

    private readonly PulseOffice.UI.Controls.Fonts.IFontPickerFactory _fonts;

    private readonly IBorderEditors _borders;
    private readonly IFillEditors _fills;
    private readonly BorderFrameScope _scope;
    private BorderEditor? _borderEditor;

    public FormatCellsView(
        CellStyle style, NumberFormatCatalog numbers, ITextCatalog texts, PulseOffice.UI.Controls.Fonts.IFontPickerFactory fonts,
        IBorderEditors borders, IFillEditors fills, ICellBorderFrames frames, bool rows, bool columns,
        FormatCellsServices services, Values.CellValue cellValue, string cellName, IBorderFrameResolver resolver)
    {
        _frames = frames ?? throw new ArgumentNullException(nameof(frames));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _cellValue = cellValue;
        _cellName = cellName ?? string.Empty;
        _borders = borders ?? throw new ArgumentNullException(nameof(borders));
        _fills = fills ?? throw new ArgumentNullException(nameof(fills));
        ArgumentNullException.ThrowIfNull(frames);
        _scope = frames.Scope(rows, columns);
        InitialFrame = frames.Frame(style.Borders, rows, columns);
        _fonts = fonts ?? throw new ArgumentNullException(nameof(fonts));
        _original = style ?? throw new ArgumentNullException(nameof(style));
        _numbers = numbers ?? throw new ArgumentNullException(nameof(numbers));
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        _style = style;
    }

    private readonly List<CellAlignmentPreview> _previews = [];

    public IReadOnlyList<CellAlignmentPreview> Previews => _previews;

    public TabbedDialogView Build(FormatCellsTab initial = FormatCellsTab.Numbers)
    {
        var view = new TabbedDialogView(
            [
                new DialogTab(_texts["format.tab.numbers"], Numbers),
                new DialogTab(_texts["format.tab.font"], Font),
                new DialogTab(_texts["format.tab.alignment"], Alignment),
                new DialogTab(_texts["format.tab.borders"], () => WithPreview(Borders())),
                new DialogTab(_texts["format.tab.background"], () => WithPreview(Background())),
                new DialogTab(_texts["format.tab.protection"], () => WithPreview(Protection())),
            ],
            () => Collect(),
            _texts,
            initialTab: (int)initial);
        view.AddHandler(Avalonia.Input.InputElement.PointerReleasedEvent, (_, _) => RefreshPreviews(), Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
        view.AddHandler(Avalonia.Input.InputElement.KeyUpEvent, (_, _) => RefreshPreviews(), Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
        view.AddHandler(Avalonia.Input.InputElement.LostFocusEvent, (_, _) => RefreshPreviews(), Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
        RefreshPreviews();
        return view;
    }

    public void RefreshPreviews()
    {
        var style = PreviewStyle();
        var sample = PreviewSample();
        foreach (var preview in _previews)
        {
            preview.Sample = sample;
            preview.Show(style);
        }
    }

    private CellStyle PreviewStyle()
    {
        var style = _style;
        if (_borderEditor is not null && Frame != InitialFrame)
            style = style with { Borders = _frames.Apply(style.Borders, _resolver.Resolve(InitialFrame, Frame, _resolver.Place(0, 0, 0, 0, 0, 0))) };
        return style;
    }

    private string PreviewSample()
    {
        if (_numberPage is { } page && page.Render(page.Code) is { Valid: true, Text.Length: > 0 } formatted) return formatted.Text;
        return _cellValue.Kind == Values.ValueKind.Text && _cellValue.AsText() is { Length: > 0 } text ? text : _texts["format.sample.text"];
    }

    private Control WithPreview(Control content)
    {
        var preview = new CellAlignmentPreview(_texts["format.sample.text"]) { Width = 200, Height = 150, VerticalAlignment = VerticalAlignment.Top };
        Avalonia.Automation.AutomationProperties.SetName(preview, _texts["format.preview"]);
        _previews.Add(preview);
        var frame = new Border
        {
            Child = preview,
            Padding = new Thickness(_metrics.SpacingMedium),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(_metrics.RadiusTight),
            VerticalAlignment = VerticalAlignment.Top,
        }.Themed(Border.BackgroundProperty, Palette.Keys.SurfaceSunken)
         .Themed(Border.BorderBrushProperty, Palette.Keys.Border);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = _metrics.SpacingLarge };
        grid.Children.Add(content);
        Grid.SetColumn(frame, 1);
        grid.Children.Add(frame);
        return grid;
    }

    public BorderFrame InitialFrame { get; }

    public BorderFrame Frame => _borderEditor?.Frame ?? InitialFrame;

    private FormatCellsResult Collect()
    {
        var code = _numberPage?.Code;

        var style = !string.IsNullOrWhiteSpace(code) && code != _numbers.CodeOf(_style.NumberFormatId)
            ? _style with { NumberFormatId = _numbers.Register(code) }
            : _style;
        return new FormatCellsResult(style, InitialFrame, Frame);
    }

    private Control Numbers()
    {
        _numberPage = new NumberFormatPage(_numbers.CodeOf(_style.NumberFormatId), _numbers, _cellValue, _cellName, _services, _texts, _metrics);
        return _numberPage;
    }

    private Control Font()
    {
        var request = _services.CellCharacters.Request(_style.Font, PreviewSample());
        var tabs = _services.Characters.Tabs(request);
        var panel = new FormPromptView(tabs[0].Explanation, [.. tabs.SelectMany(tab => tab.Fields)], _texts) { ShowButtons = false };
        var feed = new FormValuesFeed();
        feed.Attach(panel);
        panel.Preview = tabs[0].LivePreview?.Invoke(feed);
        panel.ValuesChanged += (_, _) =>
            _style = _style with { Font = _services.CellCharacters.Apply(_style.Font, _services.Characters.Read(request, panel.Values)) };
        return panel;
    }

    private Control Alignment()
    {
        var sample = _cellValue.Kind == Values.ValueKind.Text && _cellValue.AsText() is { Length: > 0 and <= 24 } text ? text : _texts["format.sample.text"];
        var page = new CellAlignmentPage(_style, sample, _services, _texts, _metrics);
        page.StyleChanged += (_, aligned) => _style = _style with
        {
            Horizontal = aligned.Horizontal,
            Vertical = aligned.Vertical,
            Indent = aligned.Indent,
            RotationDegrees = aligned.RotationDegrees,
            WrapText = aligned.WrapText,
        };
        return page;
    }

    private Control Borders()
    {
        var layout = new StackPanel { Spacing = _metrics.SpacingSmall };
        layout.Children.Add(Label(_texts[_scope.IsRange ? "format.borders.rangeExplanation" : "format.borders.explanation"]));
        _borderEditor = _borders.Create(_scope, InitialFrame, _metrics);
        layout.Children.Add(_borderEditor);
        return layout;
    }

    private Control Background()
    {
        var layout = new StackPanel { Spacing = _metrics.SpacingSmall };
        layout.Children.Add(Label(_texts["format.background.explanation"]));
        var initial = _style.BackgroundHex is { Length: > 0 } hex
            ? new ShapeFill { Kind = ShapeFillKind.Solid, ColorHex = hex }
            : ShapeFill.None;
        var editor = _fills.Create(initial, new FillEditorOptions([FillPage.None, FillPage.Solid]) { ShowTransparency = false }, _metrics);
        editor.FillChanged += (_, fill) =>
            _style = _style with { BackgroundHex = fill.Kind == ShapeFillKind.None ? null : fill.ColorHex };
        layout.Children.Add(editor);
        return layout;
    }

    private Control Protection()
    {
        var layout = new StackPanel { Spacing = _metrics.SpacingSmall };

        layout.Children.Add(Label(_texts["format.protection.explanation"]));

        layout.Children.Add(Toggle(_texts["format.protection.locked"], _style.Locked,
            value => _style = _style with { Locked = value }));

        return layout;
    }

    private TextBlock Label(string caption) =>
        new TextBlock
        {
            Text = caption,
            FontSize = _metrics.FontSizeSmall,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

    private static CheckBox Toggle(string caption, bool value, Action<bool> write)
    {
        var box = new CheckBox { Content = caption, IsChecked = value };
        box.IsCheckedChanged += (_, _) => write(box.IsChecked ?? false);
        return box;
    }
}
