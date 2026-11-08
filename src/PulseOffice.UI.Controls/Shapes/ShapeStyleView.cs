using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Fills;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Tabs;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Shapes;

public sealed record StyledOutline(ShapeOutline Outline, ShapeStyle Style)
{
    public string? Text { get; init; }

    public double RotationDegrees { get; init; }
}

public sealed class ShapeStyleView : UserControl, IDialogContent, IDialogFooterOwner
{
    public Control? Footer { get; }

    private readonly ITextCatalog _texts;
    private readonly Fonts.IFontPickerFactory _fonts;
    private readonly DesignTokens.Metrics _metrics;
    private readonly IDialogParts _parts;
    private readonly Preview _preview;

    private StyledOutline _shape;

    private readonly Objects.IRasterImageFiles? _images;

    private readonly IFillEditors _fills;

    public ShapeStyleView(
        StyledOutline shape, ITextCatalog texts, Fonts.IFontPickerFactory fonts, InterfaceDensity density = InterfaceDensity.Compact,
        int initialSection = 0,
        IShapeRenderer? renderer = null,
        Objects.IRasterImageFiles? images = null,
        IFillEditors? fills = null)
    {
        _images = images;
        _fills = fills ?? new FillEditors(texts, images);
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(texts);

        ArgumentNullException.ThrowIfNull(fonts);
        _shape = shape;
        _texts = texts;
        _fonts = fonts;
        _metrics = DesignTokens.For(density);
        _parts = new DialogParts(_metrics, null);

        _preview = new Preview(shape, renderer ?? new ShapeRenderer()) { Width = 190, Height = 150 };

        Control[] pages =
        [
            Page(FillSection()),
            Page(LineSection()),
            Page(EffectsSection()),
            Page(TextSection()),
        ];
        var first = Math.Clamp(initialSection, 0, pages.Length - 1);
        var host = new Panel { Margin = new Thickness(0, _metrics.SpacingSmall, 0, 0) };
        foreach (var page in pages) host.Children.Add(page);
        var strip = new PageTabStrip(_metrics, [.. new[] { "shape.section.fill", "shape.section.line", "shape.section.effects", "shape.section.text" }.Select(key => _texts[key])], first);
        void Reveal(int index)
        {
            for (var i = 0; i < pages.Length; i++) pages[i].IsVisible = i == index;
        }
        strip.SelectionChanged += (_, index) => Reveal(index);
        Reveal(first);
        var sections = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(strip, Dock.Top);
        sections.Children.Add(strip);
        sections.Children.Add(host);

        var layout = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
        };

        layout.Children.Add(sections);

        var previewBox = new Border
        {
            Child = _preview,
            Margin = new Thickness(_metrics.SpacingMedium, 0, 0, 0),
            Padding = new Thickness(_metrics.SpacingSmall),
            CornerRadius = new CornerRadius(_metrics.RadiusSoft),
            VerticalAlignment = VerticalAlignment.Top,
        }.Themed(Border.BackgroundProperty, Palette.Keys.SurfaceSunken);

        Grid.SetColumn(previewBox, 1);
        layout.Children.Add(previewBox);

        Content = layout;

        Footer = new DialogFooter(density)
            .WithCancel(texts["dialog.cancel"], () => Completed?.Invoke(this, null))
            .WithAction(texts["dialog.confirm"], () => Completed?.Invoke(this, _shape), primary: true);
    }

    public event EventHandler<object?>? Completed;

    private ScrollViewer Page(Control content) => new()
    {
        Content = content,
        Height = 300,
        Padding = new Thickness(0, _metrics.SpacingSmall, _metrics.SpacingSmall, 0),
    };

    private void Update(Func<StyledOutline, StyledOutline> change)
    {
        _shape = change(_shape);
        _preview.Show(_shape);
    }

    private void Style(Func<ShapeStyle, ShapeStyle> change) =>
        Update(shape => shape with { Style = change(shape.Style) });

    private Control FillSection()
    {
        FillPage[] pages = _images is not null || _shape.Style.Fill.Kind == ShapeFillKind.Bitmap
            ? [FillPage.None, FillPage.Solid, FillPage.Gradient, FillPage.Pattern, FillPage.Hatch, FillPage.Image]
            : [FillPage.None, FillPage.Solid, FillPage.Gradient, FillPage.Pattern, FillPage.Hatch];
        var editor = _fills.Create(_shape.Style.Fill, new FillEditorOptions(pages) { ShowPreview = false }, _metrics);
        editor.FillChanged += (_, fill) => Style(style => style with { Fill = fill });
        return editor;
    }

    private Control LineSection()
    {
        var panel = Section();

        var dashes = Enum.GetValues<ShapeStrokeStyle>();

        panel.Children.Add(Choice("shape.line.dash",
            [.. dashes.Select(dash => _texts[$"shape.line.dash.{Lower(dash)}"])],
            Array.IndexOf(dashes, _shape.Style.Line.Dash),
            index => Style(style => style with { Line = style.Line with { Dash = dashes[index] } })));

        panel.Children.Add(Label("shape.line.colour"));

        panel.Children.Add(Colour(_shape.Style.Line.ColorHex, _shape.Style.Line.Opacity,
            (colour, opacity) => Style(style => style with
            {
                Line = style.Line with { ColorHex = colour, Opacity = opacity },
            })));

        panel.Children.Add(Number("shape.line.width", _shape.Style.Line.Width.ToPoints(), 0, 40,
            value => Style(style => style with
            {
                Line = style.Line with { Width = Twip.FromPoints(value) },
            })));

        var caps = Enum.GetValues<LineCapStyle>();

        panel.Children.Add(Choice("shape.line.cap",
            [.. caps.Select(cap => _texts[$"shape.line.cap.{Lower(cap)}"])],
            Array.IndexOf(caps, _shape.Style.Line.Cap),
            index => Style(style => style with { Line = style.Line with { Cap = caps[index] } })));

        var joins = Enum.GetValues<LineJoinStyle>();

        panel.Children.Add(Choice("shape.line.join",
            [.. joins.Select(join => _texts[$"shape.line.join.{Lower(join)}"])],
            Array.IndexOf(joins, _shape.Style.Line.Join),
            index => Style(style => style with { Line = style.Line with { Join = joins[index] } })));

        var arrows = Enum.GetValues<ArrowEnd>();

        panel.Children.Add(Choice("shape.line.startArrow",
            [.. arrows.Select(arrow => _texts[$"shape.arrow.{Lower(arrow)}"])],
            Array.IndexOf(arrows, _shape.Style.Line.StartArrow),
            index => Style(style => style with
            {
                Line = style.Line with { StartArrow = arrows[index] },
            })));

        panel.Children.Add(Choice("shape.line.endArrow",
            [.. arrows.Select(arrow => _texts[$"shape.arrow.{Lower(arrow)}"])],
            Array.IndexOf(arrows, _shape.Style.Line.EndArrow),
            index => Style(style => style with
            {
                Line = style.Line with { EndArrow = arrows[index] },
            })));

        return panel;
    }

    private Control EffectsSection()
    {
        var panel = Section();

        panel.Children.Add(Check("shape.shadow", _shape.Style.Shadow.Enabled,
            value => Style(style => style with { Shadow = style.Shadow with { Enabled = value } })));

        panel.Children.Add(Label("shape.shadow.colour"));

        panel.Children.Add(Colour(_shape.Style.Shadow.ColorHex, _shape.Style.Shadow.Opacity,
            (colour, opacity) => Style(style => style with
            {
                Shadow = style.Shadow with { ColorHex = colour, Opacity = opacity },
            })));

        panel.Children.Add(Number("shape.shadow.offsetX", _shape.Style.Shadow.OffsetX.ToPoints(), -60, 60,
            value => Style(style => style with
            {
                Shadow = style.Shadow with { OffsetX = Twip.FromPoints(value) },
            })));

        panel.Children.Add(Number("shape.shadow.offsetY", _shape.Style.Shadow.OffsetY.ToPoints(), -60, 60,
            value => Style(style => style with
            {
                Shadow = style.Shadow with { OffsetY = Twip.FromPoints(value) },
            })));

        panel.Children.Add(Number("shape.shadow.blur", _shape.Style.Shadow.Blur.ToPoints(), 0, 60,
            value => Style(style => style with
            {
                Shadow = style.Shadow with { Blur = Twip.FromPoints(value) },
            })));

        panel.Children.Add(Check("shape.glow", _shape.Style.Glow.Enabled,
            value => Style(style => style with { Glow = style.Glow with { Enabled = value } })));

        panel.Children.Add(Label("shape.glow.colour"));

        panel.Children.Add(Colour(_shape.Style.Glow.ColorHex, _shape.Style.Glow.Opacity,
            (colour, opacity) => Style(style => style with
            {
                Glow = style.Glow with { ColorHex = colour, Opacity = opacity },
            })));

        panel.Children.Add(Number("shape.glow.radius", _shape.Style.Glow.Radius.ToPoints(), 0, 60,
            value => Style(style => style with
            {
                Glow = style.Glow with { Radius = Twip.FromPoints(value) },
            })));

        panel.Children.Add(Number("shape.softEdge", _shape.Style.SoftEdge.ToPoints(), 0, 40,
            value => Style(style => style with { SoftEdge = Twip.FromPoints(value) })));

        var bevels = Enum.GetValues<BevelStyle>();

        panel.Children.Add(Choice("shape.bevel",
            [.. bevels.Select(bevel => _texts[$"shape.bevel.{Lower(bevel)}"])],
            Array.IndexOf(bevels, _shape.Style.Bevel.Style),
            index => Style(style => style with { Bevel = style.Bevel with { Style = bevels[index] } })));

        panel.Children.Add(Number("shape.bevel.depth", _shape.Style.Bevel.Depth.ToPoints(), 0, 40,
            value => Style(style => style with
            {
                Bevel = style.Bevel with { Depth = Twip.FromPoints(value) },
            })));

        return panel;
    }

    private Control TextSection()
    {
        var panel = Section();

        panel.Children.Add(Text("shape.text", _shape.Text ?? string.Empty,
            value => Update(shape => shape with { Text = string.IsNullOrWhiteSpace(value) ? null : value })));

        var family = _fonts.Create(_shape.Style.Text.Family);
        family.FamilyChanged += (_, value) => Style(style => style with { Text = style.Text with { Family = value } });
        panel.Children.Add(_parts.Field(_texts["shape.text.family"], family));

        panel.Children.Add(Number("shape.text.size", _shape.Style.Text.Size, 4, 200,
            value => Style(style => style with { Text = style.Text with { Size = value } })));

        panel.Children.Add(Check("shape.text.bold", _shape.Style.Text.Bold,
            value => Style(style => style with { Text = style.Text with { Bold = value } })));

        panel.Children.Add(Check("shape.text.italic", _shape.Style.Text.Italic,
            value => Style(style => style with { Text = style.Text with { Italic = value } })));

        panel.Children.Add(Check("shape.text.underline", _shape.Style.Text.Underline,
            value => Style(style => style with { Text = style.Text with { Underline = value } })));

        panel.Children.Add(Check("shape.text.automaticColour", _shape.Style.Text.AutomaticColor,
            value => Style(style => style with { Text = style.Text with { AutomaticColor = value } })));

        panel.Children.Add(Label("shape.text.colour"));

        panel.Children.Add(Colour(_shape.Style.Text.ColorHex, 1, (colour, _) =>
            Style(style => style with { Text = style.Text with { ColorHex = colour, AutomaticColor = false } })));

        var horizontal = Enum.GetValues<TextHorizontalPlacement>();

        panel.Children.Add(Choice("shape.text.horizontal",
            [.. horizontal.Select(value => _texts[$"shape.text.horizontal.{Lower(value)}"])],
            Array.IndexOf(horizontal, _shape.Style.Text.Horizontal),
            index => Style(style => style with
            {
                Text = style.Text with { Horizontal = horizontal[index] },
            })));

        var vertical = Enum.GetValues<TextVerticalPlacement>();

        panel.Children.Add(Choice("shape.text.vertical",
            [.. vertical.Select(value => _texts[$"shape.text.vertical.{Lower(value)}"])],
            Array.IndexOf(vertical, _shape.Style.Text.Vertical),
            index => Style(style => style with
            {
                Text = style.Text with { Vertical = vertical[index] },
            })));

        panel.Children.Add(Check("shape.text.wrap", _shape.Style.Text.Wrap,
            value => Style(style => style with { Text = style.Text with { Wrap = value } })));

        panel.Children.Add(Number("shape.text.inset", _shape.Style.Text.Inset.ToPoints(), 0, 40,
            value => Style(style => style with
            {
                Text = style.Text with { Inset = Twip.FromPoints(value) },
            })));

        panel.Children.Add(Number("shape.rotation", _shape.RotationDegrees, 0, 359,
            value => Update(shape => shape with { RotationDegrees = value })));

        return panel;
    }

    private StackPanel Section() => new() { Spacing = _metrics.SpacingSmall };

    private Control Label(string key) => new TextBlock
    {
        Text = _texts[key],
        FontSize = _metrics.FontSizeSmall,
        Margin = new Thickness(0, _metrics.SpacingSmall, 0, 0),
    }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

    private Control Colour(string colour, double opacity, Action<string, double> change)
    {
        var picker = new ColorPickerView(colour, opacity, _texts, showOpacity: true);

        picker.Changed += (_, _) => change(picker.Value, picker.Strength);
        return picker;
    }

    private Control Choice(string key, IReadOnlyList<string> options, int selected, Action<int> change)
    {
        var box = new ComboBox
        {
            ItemsSource = options,
            SelectedIndex = Math.Max(0, selected),
            FontSize = _metrics.FontSize,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        box.SelectionChanged += (_, _) =>
        {
            if (box.SelectedIndex >= 0) change(box.SelectedIndex);
        };

        return _parts.Field(_texts[key], box);
    }

    private Control Number(string key, double value, double minimum, double maximum, Action<double> change)
    {
        var box = new NumericUpDown
        {
            Minimum = (decimal)minimum,
            Maximum = (decimal)maximum,
            Value = (decimal)Math.Clamp(double.IsFinite(value) ? value : minimum, minimum, maximum),
            Increment = 1,
            FontSize = _metrics.FontSize,
        };

        box.ValueChanged += (_, _) =>
        {
            if (box.Value is { } current) change((double)current);
        };

        return _parts.Field(_texts[key], box);
    }

    private Control Text(string key, string value, Action<string> change)
    {
        var box = new TextBox { Text = value, FontSize = _metrics.FontSize };

        box.TextChanged += (_, _) => change(box.Text ?? string.Empty);
        return _parts.Field(_texts[key], box);
    }

    private Control Check(string key, bool value, Action<bool> change)
    {
        var box = new CheckBox
        {
            Content = _texts[key],
            IsChecked = value,
            FontSize = _metrics.FontSize,
        };

        box.IsCheckedChanged += (_, _) => change(box.IsChecked ?? false);
        return box;
    }

    private static string Lower<T>(T value) where T : notnull
    {
        var text = value.ToString()!;
        return char.ToLowerInvariant(text[0]) + text[1..];
    }

    private sealed class Preview : Control
    {
        private readonly IShapeRenderer _renderer;

        private StyledOutline _shape;

        public Preview(StyledOutline shape, IShapeRenderer renderer)
        {
            _shape = shape;
            _renderer = renderer;
        }

        public void Show(StyledOutline shape)
        {
            _shape = shape;
            InvalidateVisual();
        }

        public override void Render(DrawingContext context)
        {
            var bounds = new Rect(Bounds.Size).Deflate(18);
            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            _renderer.Render(
                context,
                _shape.Outline,
                _shape.Style,
                Matrix.CreateTranslation(bounds.X, bounds.Y),
                _shape.Text,
                bounds);
        }
    }
}

public interface IShapeStyleEditors
{
    ShapeStyleView Create(StyledOutline shape, int initialSection = 0);
}

public sealed class ShapeStyleEditors(
    ITextCatalog texts, Fonts.IFontPickerFactory fonts, IShapeRenderer renderer, IFillEditors fills, Objects.IRasterImageFiles? images = null) : IShapeStyleEditors
{
    public ShapeStyleView Create(StyledOutline shape, int initialSection = 0) =>
        new(shape, texts, fonts, initialSection: initialSection, renderer: renderer, images: images, fills: fills);
}
