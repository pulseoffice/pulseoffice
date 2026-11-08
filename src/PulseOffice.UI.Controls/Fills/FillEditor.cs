using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Objects;
using PulseOffice.UI.Controls.Shapes;
using PulseOffice.UI.Controls.Tabs;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Fills;

public sealed class FillEditor : UserControl
{
    private readonly FillEditorModel _model;
    private readonly ITextCatalog _texts;
    private readonly IShapeBrushes _brushes;
    private readonly IRasterImageFiles? _images;
    private readonly DesignTokens.Metrics _metrics;
    private readonly Dictionary<FillPage, Control> _pages = [];
    private readonly ColorFieldControl _stopColour;
    private readonly NumberBox _stopPosition;
    private readonly List<(FillPattern Pattern, Button Button)> _patternButtons = [];
    private readonly Slider _transparency;
    private readonly TextBlock _transparencyValue;
    private readonly FillSwatch? _preview;

    public FillEditor(FillEditorModel model, ITextCatalog texts, IShapeBrushes brushes, DesignTokens.Metrics metrics, IRasterImageFiles? images = null)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        _brushes = brushes ?? throw new ArgumentNullException(nameof(brushes));
        _images = images;
        _metrics = metrics;

        _stopColour = new ColorFieldControl(CurrentStop.ColorHex, false, texts, metrics);
        AutomationProperties.SetName(_stopColour, texts["fillEditor.stop.color"]);
        _stopColour.Changed += (_, _) => { if (_stopColour.Value != CurrentStop.ColorHex) _model.RecolorStop(_stopColour.Value); };
        _stopPosition = new NumberBox(new NumberEntry(new NumericBounds(0, 100, 1), true, CultureInfo.CurrentCulture), CurrentStop.Position * 100, metrics, unit: "%", boxWidth: 48);
        AutomationProperties.SetName(_stopPosition, texts["fillEditor.stop.position"]);
        _stopPosition.ValueCommitted += (_, value) => _model.MoveStop(value / 100);

        _transparency = new Slider { Minimum = 0, Maximum = 100, Value = Math.Round(model.Transparency * 100), Width = 160, VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(_transparency, texts["fillEditor.transparency"]);
        _transparencyValue = new TextBlock { VerticalAlignment = VerticalAlignment.Center, FontSize = metrics.FontSize, MinWidth = 40 };
        _transparency.PropertyChanged += (_, e) =>
        {
            if (e.Property != Avalonia.Controls.Primitives.RangeBase.ValueProperty) return;
            _transparencyValue.Text = string.Format(CultureInfo.CurrentCulture, "{0:0} %", _transparency.Value);
            if (Math.Abs(_model.Transparency * 100 - _transparency.Value) > 0.5) _model.SetTransparency(_transparency.Value / 100);
        };
        _transparencyValue.Text = string.Format(CultureInfo.CurrentCulture, "{0:0} %", _transparency.Value);

        var pages = model.Options.Pages;
        var strip = new PageTabStrip(metrics, [.. pages.Select(page => texts[$"fillEditor.page.{Key(page)}"])], Math.Max(0, IndexOf(pages, model.Page)));
        strip.SelectionChanged += (_, index) => _model.ShowPage(pages[index]);
        Tabs = strip;

        var host = new Panel { Margin = new Thickness(0, metrics.SpacingSmall, 0, 0), MinHeight = 150 };
        foreach (var page in pages)
        {
            var content = Build(page);
            _pages[page] = content;
            host.Children.Add(content);
        }

        var settings = new StackPanel { Spacing = metrics.SpacingSmall };
        settings.Children.Add(strip);
        settings.Children.Add(host);
        if (model.Options.ShowTransparency)
            settings.Children.Add(Row("fillEditor.transparency", new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = metrics.SpacingSmall,
                Children = { _transparency, _transparencyValue },
            }));

        if (model.Options.ShowPreview)
        {
            _preview = new FillSwatch(model.Fill, brushes) { Width = 140, Height = 110, VerticalAlignment = VerticalAlignment.Top };
            AutomationProperties.SetName(_preview, texts["fillEditor.preview"]);
            var layout = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = metrics.SpacingMedium };
            var previewColumn = new StackPanel { Spacing = metrics.SpacingSmall, Children = { Caption("fillEditor.preview"), _preview } };
            Grid.SetColumn(previewColumn, 1);
            layout.Children.Add(settings);
            layout.Children.Add(previewColumn);
            Content = layout;
        }
        else
        {
            Content = settings;
        }

        _model.Changed += (_, _) => Sync();
        Sync();
    }

    public FillEditorModel Model => _model;

    public ShapeFill Fill => _model.Fill;

    public PageTabStrip Tabs { get; }

    public event EventHandler<ShapeFill>? FillChanged;

    private PulseOffice.Drawing.Shapes.GradientStop CurrentStop => _model.Fill.Gradient.Stops[Math.Clamp(_model.SelectedStop, 0, _model.Fill.Gradient.Stops.Count - 1)];

    private Control Build(FillPage page) => page switch
    {
        FillPage.None => Note("fillEditor.none.note"),
        FillPage.Solid => SolidPage(),
        FillPage.Gradient => GradientPage(),
        FillPage.Pattern => PatternPage(),
        FillPage.Hatch => HatchPage(),
        _ => ImagePage(),
    };

    private Control SolidPage()
    {
        var picker = new ColorPickerView(_model.Fill.ColorHex, 1, _texts);
        picker.Changed += (_, _) => _model.SetColor(picker.Value);
        return picker;
    }

    private Control GradientPage()
    {
        var panel = Section();
        var kinds = Enum.GetValues<GradientKind>();
        panel.Children.Add(Row("fillEditor.gradient.kind", Choice(
            [.. kinds.Select(kind => _texts[$"shape.gradient.kind.{Lower(kind)}"])],
            Array.IndexOf(kinds, _model.Fill.Gradient.Kind),
            index => _model.SetGradientKind(kinds[index]))));
        var angle = new NumberBox(new NumberEntry(new NumericBounds(0, 359, 5), true, CultureInfo.CurrentCulture), _model.Fill.Gradient.AngleDegrees, _metrics, unit: "°", boxWidth: 48);
        AutomationProperties.SetName(angle, _texts["fillEditor.gradient.angle"]);
        angle.ValueCommitted += (_, value) => _model.SetGradientAngle(value);
        panel.Children.Add(Row("fillEditor.gradient.angle", angle));
        panel.Children.Add(Caption("fillEditor.gradient.stops"));
        panel.Children.Add(new GradientStopBar(_model, _brushes, _texts["fillEditor.gradient.stops"], _texts["fillEditor.gradient.stops.help"]));
        panel.Children.Add(Row("fillEditor.stop.color", _stopColour));
        panel.Children.Add(Row("fillEditor.stop.position", _stopPosition));
        var remove = Quiet("fillEditor.stop.remove", _model.RemoveStop);
        var reverse = Quiet("fillEditor.gradient.reverse", _model.ReverseGradient);
        panel.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = _metrics.SpacingSmall, Children = { remove, reverse } });
        return panel;
    }

    private Control PatternPage()
    {
        var panel = Section();
        var gallery = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var pattern in _model.Patterns)
        {
            var name = _texts[$"fillEditor.pattern.{pattern.Key}"];
            var sample = new ShapeFill { Kind = ShapeFillKind.Hatch, Hatch = new ShapeHatch { Kind = pattern.Kind, AngleDegrees = pattern.AngleDegrees, Distance = Foundation.Units.Twip.FromPoints(pattern.DistancePoints), ColorHex = _model.Fill.Hatch.ColorHex, BackgroundHex = _model.Fill.Hatch.BackgroundHex } };
            var button = new Button
            {
                Content = new FillSwatch(sample, _brushes) { Width = 30, Height = 30 },
                Padding = new Thickness(2),
                Margin = new Thickness(0, 0, 4, 4),
                BorderThickness = new Thickness(2),
            };
            button.Classes.Add("dialogQuiet");
            AutomationProperties.SetName(button, name);
            ToolTip.SetTip(button, name);
            button.Click += (_, _) => _model.ApplyPattern(pattern);
            _patternButtons.Add((pattern, button));
            gallery.Children.Add(button);
        }
        panel.Children.Add(gallery);
        panel.Children.Add(Row("fillEditor.hatch.color", Colour(_model.Fill.Hatch.ColorHex, false, hex => _model.SetHatch(hatch => hatch with { ColorHex = hex }))));
        panel.Children.Add(Row("fillEditor.hatch.background", Colour(_model.Fill.Hatch.BackgroundHex, true, hex => _model.SetHatch(hatch => hatch with { BackgroundHex = hex.Length == 0 ? null : hex }))));
        return panel;
    }

    private Control HatchPage()
    {
        var panel = Section();
        var kinds = Enum.GetValues<HatchKind>();
        panel.Children.Add(Row("fillEditor.hatch.kind", Choice(
            [.. kinds.Select(kind => _texts[$"shape.hatch.kind.{Lower(kind)}"])],
            Array.IndexOf(kinds, _model.Fill.Hatch.Kind),
            index => _model.SetHatch(hatch => hatch with { Kind = kinds[index] }))));
        var angle = new NumberBox(new NumberEntry(new NumericBounds(0, 179, 5), true, CultureInfo.CurrentCulture), _model.Fill.Hatch.AngleDegrees, _metrics, unit: "°", boxWidth: 48);
        AutomationProperties.SetName(angle, _texts["fillEditor.hatch.angle"]);
        angle.ValueCommitted += (_, value) => _model.SetHatch(hatch => hatch with { AngleDegrees = value });
        panel.Children.Add(Row("fillEditor.hatch.angle", angle));
        var distance = new NumberBox(new NumberEntry(new NumericBounds(1, 40, 0.5), false, CultureInfo.CurrentCulture), _model.Fill.Hatch.Distance.ToPoints(), _metrics, unit: "pt", boxWidth: 48);
        AutomationProperties.SetName(distance, _texts["fillEditor.hatch.distance"]);
        distance.ValueCommitted += (_, value) => _model.SetHatchDistance(value);
        panel.Children.Add(Row("fillEditor.hatch.distance", distance));
        panel.Children.Add(Row("fillEditor.hatch.color", Colour(_model.Fill.Hatch.ColorHex, false, hex => _model.SetHatch(hatch => hatch with { ColorHex = hex }))));
        panel.Children.Add(Row("fillEditor.hatch.background", Colour(_model.Fill.Hatch.BackgroundHex, true, hex => _model.SetHatch(hatch => hatch with { BackgroundHex = hex.Length == 0 ? null : hex }))));
        return panel;
    }

    private Control ImagePage()
    {
        var panel = Section();
        if (_images is not { } images)
        {
            panel.Children.Add(Note("fillEditor.image.unavailable"));
            return panel;
        }
        var choose = Quiet("fillEditor.image.choose", () => { });
        choose.Click += async (_, _) =>
        {
            if (await images.PickAsync(CancellationToken.None).ConfigureAwait(true) is { } picked) _model.SetImage(picked.Data, picked.MediaType);
        };
        panel.Children.Add(choose);
        var tile = new CheckBox { Content = _texts["fillEditor.image.tile"], IsChecked = _model.Fill.Bitmap?.Tiled ?? false, FontSize = _metrics.FontSize };
        tile.IsCheckedChanged += (_, _) => _model.SetTiled(tile.IsChecked ?? false);
        panel.Children.Add(tile);
        return panel;
    }

    private void Sync()
    {
        foreach (var (page, control) in _pages) control.IsVisible = page == _model.Page;
        var index = IndexOf(_model.Options.Pages, _model.Page);
        if (index >= 0 && Tabs.Selected != index) Tabs.Select(index);
        if (_stopColour.Value != CurrentStop.ColorHex) _stopColour.Select(CurrentStop.ColorHex);
        _stopPosition.Value = Math.Round(CurrentStop.Position * 100);
        var current = _model.CurrentPattern;
        foreach (var (pattern, button) in _patternButtons)
            button.Themed(Button.BorderBrushProperty, pattern == current ? Palette.Keys.BorderStrong : Palette.Keys.Border);
        _transparency.IsEnabled = _model.Fill.Kind != ShapeFillKind.None;
        _preview?.Show(_model.Fill);
        FillChanged?.Invoke(this, _model.Fill);
    }

    private ColorFieldControl Colour(string? value, bool allowNone, Action<string> change)
    {
        var field = new ColorFieldControl(new ColorFieldValue().Normalize(value), allowNone, _texts, _metrics);
        field.Changed += (_, _) => change(field.Value);
        return field;
    }

    private ComboBox Choice(IReadOnlyList<string> options, int selected, Action<int> change)
    {
        var box = new ComboBox { ItemsSource = options, SelectedIndex = Math.Max(0, selected), FontSize = _metrics.FontSize, MinWidth = 140 };
        box.SelectionChanged += (_, _) => { if (box.SelectedIndex >= 0) change(box.SelectedIndex); };
        return box;
    }

    private Button Quiet(string key, Action run)
    {
        var button = new Button { Content = _texts[key], FontSize = _metrics.FontSize };
        button.Classes.Add("dialogQuiet");
        button.Click += (_, _) => run();
        return button;
    }

    private Control Row(string key, Control editor)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("120,*"), ColumnSpacing = _metrics.SpacingMedium };
        var label = new TextBlock { Text = _texts[key], FontSize = _metrics.FontSizeSmall, VerticalAlignment = VerticalAlignment.Center }
            .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
        editor.HorizontalAlignment = HorizontalAlignment.Left;
        Grid.SetColumn(editor, 1);
        grid.Children.Add(label);
        grid.Children.Add(editor);
        return grid;
    }

    private StackPanel Section() => new() { Spacing = _metrics.SpacingSmall };

    private TextBlock Caption(string key) => new TextBlock
    {
        Text = _texts[key],
        FontSize = _metrics.FontSizeSmall,
        FontWeight = FontWeight.SemiBold,
    }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

    private TextBlock Note(string key) => new TextBlock
    {
        Text = _texts[key],
        FontSize = _metrics.FontSize,
        TextWrapping = TextWrapping.Wrap,
        MaxWidth = 320,
        HorizontalAlignment = HorizontalAlignment.Left,
    }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

    private int IndexOf(IReadOnlyList<FillPage> pages, FillPage page)
    {
        for (var i = 0; i < pages.Count; i++)
            if (pages[i] == page) return i;
        return -1;
    }

    private string Key(FillPage page) => Lower(page);

    private string Lower<T>(T value) where T : struct, Enum
    {
        var text = value.ToString();
        return char.ToLowerInvariant(text[0]) + text[1..];
    }
}

public interface IFillEditors
{
    FillEditor Create(ShapeFill initial, FillEditorOptions options, DesignTokens.Metrics metrics);
}

public sealed class FillEditors(
    ITextCatalog texts,
    IShapeBrushes brushes,
    IGradientStopEditing stops,
    IFillPatternCatalog patterns,
    IRasterImageFiles? images = null) : IFillEditors
{
    public FillEditors(ITextCatalog texts, IRasterImageFiles? images = null)
        : this(texts, new ShapeBrushes(), new GradientStopEditing(), new FillPatternCatalog(), images)
    {
    }

    public FillEditor Create(ShapeFill initial, FillEditorOptions options, DesignTokens.Metrics metrics) =>
        new(new FillEditorModel(initial, options, stops, patterns), texts, brushes, metrics, images);
}
