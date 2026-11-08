using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Drawing.Fontwork;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Fonts;
using PulseOffice.UI.Controls.Galleries;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Shapes.Fontwork;

public enum FontworkGalleryMode
{
    Create,
    Warp,
}

public sealed record FontworkGalleryServices(
    IFontworkRenderer Renderer,
    IFontPickerFactory Fonts,
    IIconCatalog Icons,
    ITextCatalog Texts,
    IGalleryNavigator? Navigator = null);

public sealed class FontworkGalleryView : UserControl, IDialogContent, IDialogFooterOwner
{
    public const int TilesPerRow = 4;

    private const double TileWidth = 112;
    private const double ThumbnailHeight = 66;
    private const double SwatchSize = 38;
    private const double OptionsWidth = 260;
    private const double PreviewHeight = 136;

    private readonly FontworkDesigner _designer;
    private readonly FontworkGalleryServices _services;
    private readonly FontworkGalleryMode _mode;
    private readonly DesignTokens.Metrics _metrics = DesignTokens.For(InterfaceDensity.Compact);
    private readonly DialogChrome _chrome;
    private readonly ThumbnailGallery<FontworkWarp> _gallery;
    private readonly List<(Button Swatch, int Look)> _swatches = [];
    private readonly FontworkPreview _preview;
    private readonly TextBlock _warpName = new();
    private readonly TextBox _text = new();
    private readonly CheckBox _shadow = new();
    private readonly ToggleButton _bold = new();
    private readonly ToggleButton _italic = new();
    private ColorFieldControl? _fill;
    private ColorFieldControl? _line;
    private Button? _confirm;
    private bool _syncing;

    public FontworkGalleryView(FontworkDesigner designer, FontworkGalleryServices services, FontworkGalleryMode mode = FontworkGalleryMode.Create)
    {
        _designer = designer ?? throw new ArgumentNullException(nameof(designer));
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _mode = mode;
        _chrome = new DialogChrome(services.Icons, _metrics);
        _preview = new FontworkPreview(services.Renderer, new Thickness(_metrics.SpacingLarge, _metrics.SpacingMedium)) { Height = PreviewHeight };
        _gallery = new ThumbnailGallery<FontworkWarp>(
            [.. designer.Warps.Select(warp => new ThumbnailGalleryItem<FontworkWarp>(warp, WarpName(warp)))],
            new FontworkWarpPainter(services.Renderer, designer),
            services.Navigator ?? new GalleryNavigator(),
            new ThumbnailGalleryOptions
            {
                TileWidth = TileWidth,
                ThumbnailHeight = ThumbnailHeight,
                FrameRadius = _metrics.Radius,
                Columns = TilesPerRow,
                AccessibleName = Texts["fontwork.warp"],
            },
            _metrics);
        _gallery.SelectionChanged += (_, warp) => _designer.Warp = warp;
        _gallery.Invoked += (_, _) => Confirm();

        Focusable = true;
        AddHandler(KeyDownEvent, Keys, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        Content = mode == FontworkGalleryMode.Create ? CreateLayout() : WarpLayout();
        Footer = new DialogFooter()
            .WithCancel(Texts["prompt.cancel"], () => Completed?.Invoke(this, null))
            .WithAction(Texts[mode == FontworkGalleryMode.Create ? "fontwork.gallery.insert" : "fontwork.gallery.apply"], Confirm, out var confirm, primary: true);
        _confirm = confirm;

        _designer.Changed += (_, _) => Refresh();
        Refresh();
        AttachedToVisualTree += (_, _) =>
        {
            if (mode == FontworkGalleryMode.Create)
            {
                _text.Focus();
                _text.SelectAll();
            }
            else Focus();
        };
    }

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; }

    public FontworkDesigner Designer => _designer;

    public FontworkPreview Preview => _preview;

    public IReadOnlyList<FontworkPreview> Thumbnails => [.. _gallery.Thumbnails.OfType<FontworkPreview>()];

    public ThumbnailGallery<FontworkWarp> Gallery => _gallery;

    private ITextCatalog Texts => _services.Texts;

    private Control CreateLayout()
    {
        var layout = new Grid { ColumnDefinitions = new ColumnDefinitions($"{OptionsWidth},Auto,*") };
        var divider = new Border { Width = 1, Margin = new Thickness(_metrics.SpacingLarge, 0) }
            .Themed(Border.BackgroundProperty, Palette.Keys.Border);
        var main = Main();
        Grid.SetColumn(divider, 1);
        Grid.SetColumn(main, 2);
        layout.Children.Add(Options());
        layout.Children.Add(divider);
        layout.Children.Add(main);
        return layout;
    }

    private Control WarpLayout() => new StackPanel
    {
        Spacing = _metrics.SpacingSmall,
        Children = { _chrome.Description(Texts["fontwork.path.explanation"]), Main() },
    };

    private Control Main()
    {
        _warpName.FontWeight = FontWeight.SemiBold;
        _warpName.FontSize = _metrics.FontSizeSmall;
        _warpName.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
        _warpName.VerticalAlignment = VerticalAlignment.Center;

        var previewHeader = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, _metrics.SpacingMedium, 0, 4) };
        previewHeader.Children.Add(_chrome.Caption(Texts["fontwork.gallery.preview"]));
        Grid.SetColumn(_warpName, 1);
        previewHeader.Children.Add(_warpName);

        var frame = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(_metrics.RadiusSoft),
            ClipToBounds = true,
            Child = _preview,
        }.Themed(Border.BorderBrushProperty, Palette.Keys.Border);

        return new StackPanel
        {
            Margin = new Thickness(0, 0, 0, 2),
            Children =
            {
                _chrome.Caption(Texts["fontwork.warp"], new Thickness(0, 0, 0, 4)),
                _gallery,
                previewHeader,
                frame,
            },
        };
    }

    private Control Options()
    {
        _text.Text = _designer.Text;
        _text.PlaceholderText = Texts["fontwork.sample"];
        Avalonia.Automation.AutomationProperties.SetName(_text, Texts["fontwork.text"]);
        _text.TextChanged += (_, _) => _designer.Text = _text.Text ?? string.Empty;

        var font = _services.Fonts.Create(_designer.FontFamily);
        font.FamilyChanged += (_, family) => _designer.FontFamily = family;
        Style(_bold, "B", Texts["fontwork.bold"], FontWeight.Bold, FontStyle.Normal, _designer.Bold, value => _designer.Bold = value);
        Style(_italic, "I", Texts["fontwork.italic"], FontWeight.Normal, FontStyle.Italic, _designer.Italic, value => _designer.Italic = value);
        var fontRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 4 };
        fontRow.Children.Add(font);
        Grid.SetColumn(_bold, 1);
        Grid.SetColumn(_italic, 2);
        fontRow.Children.Add(_bold);
        fontRow.Children.Add(_italic);

        var looks = new WrapPanel { ItemSpacing = 6, LineSpacing = 6 };
        for (var i = 0; i < _designer.Looks.Count; i++) looks.Children.Add(Swatch(i));

        _fill = new ColorFieldControl(_designer.Fill, false, Texts, _metrics);
        _fill.Changed += (_, _) => { if (!_syncing && _fill.Value.Length > 0) _designer.Fill = _fill.Value; };
        _line = new ColorFieldControl(_designer.Line, true, Texts, _metrics);
        _line.Changed += (_, _) => { if (!_syncing) _designer.Line = _line.Value; };

        _shadow.Content = new TextBlock { Text = Texts["fontwork.shadow"], TextWrapping = TextWrapping.Wrap };
        _shadow.IsChecked = _designer.Shadow;
        _shadow.IsCheckedChanged += (_, _) => _designer.Shadow = _shadow.IsChecked == true;

        return new StackPanel
        {
            Spacing = 4,
            Children =
            {
                _chrome.Caption(Texts["fontwork.text"]),
                _text,
                _chrome.Caption(Texts["fontwork.font"], new Thickness(0, _metrics.SpacingMedium, 0, 0)),
                fontRow,
                _chrome.Caption(Texts["fontwork.palette"], new Thickness(0, _metrics.SpacingMedium, 0, 0)),
                looks,
                Labelled(Texts["fontwork.fill"], _fill),
                Labelled(Texts["fontwork.outline"], _line),
                _chrome.Caption(Texts["fontwork.effects"], new Thickness(0, _metrics.SpacingMedium, 0, 0)),
                _shadow,
            },
        };
    }

    private Control Labelled(string label, Control editor)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("90,*"), Margin = new Thickness(0, 4, 0, 0) };
        var caption = new TextBlock { Text = label, FontSize = _metrics.FontSize, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(editor, 1);
        row.Children.Add(caption);
        row.Children.Add(editor);
        return row;
    }

    private void Style(ToggleButton toggle, string glyph, string name, FontWeight weight, FontStyle style, bool initial, Action<bool> changed)
    {
        toggle.Content = new TextBlock { Text = glyph, FontWeight = weight, FontStyle = style, FontFamily = new FontFamily("Georgia, Times New Roman, serif"), HorizontalAlignment = HorizontalAlignment.Center };
        toggle.Width = 30;
        toggle.Padding = new Thickness(0, 4);
        toggle.CornerRadius = new CornerRadius(_metrics.RadiusTight);
        toggle.HorizontalContentAlignment = HorizontalAlignment.Center;
        toggle.IsChecked = initial;
        toggle.IsCheckedChanged += (_, _) => changed(toggle.IsChecked == true);
        ToolTip.SetTip(toggle, name);
        Avalonia.Automation.AutomationProperties.SetName(toggle, name);
    }

    private Button Swatch(int look)
    {
        var name = Texts[$"fontwork.palette.{_designer.Looks[look].Key}"];
        var swatch = new Button
        {
            Width = SwatchSize + 8,
            Height = SwatchSize + 8,
            Padding = new Thickness(3),
            BorderThickness = default,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            Content = new Border
            {
                CornerRadius = new CornerRadius(_metrics.RadiusTight),
                ClipToBounds = true,
                BorderThickness = new Thickness(1),
                Child = new SwatchFace(_services.Renderer, _designer, look),
            }.Themed(Border.BorderBrushProperty, Palette.Keys.Border),
        };
        swatch.Classes.Add("ribbon");
        swatch.Classes.Add("tile");
        swatch.Click += (_, _) => _designer.ChooseLook(look);
        ToolTip.SetTip(swatch, name);
        Avalonia.Automation.AutomationProperties.SetName(swatch, name);
        _swatches.Add((swatch, look));
        return swatch;
    }

    private void Refresh()
    {
        _gallery.Select(_designer.Warp);
        _gallery.RefreshThumbnails();

        var look = _designer.Look;
        foreach (var (swatch, index) in _swatches)
        {
            swatch.Classes.Set("checked", index == look);
            (swatch.Content as Border)?.Child?.InvalidateVisual();
        }

        _syncing = true;
        if (_fill is not null && !string.Equals(_fill.Value, _designer.Fill, StringComparison.OrdinalIgnoreCase)) _fill.Select(_designer.Fill);
        if (_line is not null && !string.Equals(_line.Value, _designer.Line, StringComparison.OrdinalIgnoreCase)) _line.Select(_designer.Line);
        _syncing = false;

        _warpName.Text = WarpName(_designer.Warp);
        _preview.Show(_designer.Preview());
        if (_confirm is not null) _confirm.IsEnabled = _mode == FontworkGalleryMode.Warp || _designer.CanConfirm;
    }

    private void Confirm()
    {
        if (_mode == FontworkGalleryMode.Create && !_designer.CanConfirm)
        {
            _text.Focus();
            return;
        }

        Completed?.Invoke(this, _designer);
    }

    private void Keys(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None)
        {
            Confirm();
            e.Handled = true;
            return;
        }

        if (e.Source is TextBox) return;

        if (_gallery.HandleKey(e.Key)) e.Handled = true;
    }

    private string WarpName(FontworkWarp warp)
    {
        var name = warp.ToString();
        return Texts[$"fontwork.warp.{char.ToLowerInvariant(name[0])}{name[1..]}"];
    }

    private sealed class FontworkWarpPainter(IFontworkRenderer renderer, FontworkDesigner designer) : IThumbnailPainter<FontworkWarp>
    {
        public Control Create(FontworkWarp item, Size size) => new FontworkPreview(renderer, new Thickness(6, 4)) { Height = size.Height };

        public void Repaint(Control thumbnail, FontworkWarp item)
        {
            if (thumbnail is FontworkPreview preview) preview.Show(designer.Thumbnail(item));
        }
    }

    private sealed class SwatchFace(IFontworkRenderer renderer, FontworkDesigner designer, int look) : Control
    {
        public override void Render(DrawingContext context)
        {
            if (Bounds.Width <= 0 || Bounds.Height <= 0) return;
            var area = new Rect(Bounds.Size);
            context.FillRectangle(Brushes.White, area);
            using (context.PushClip(area))
                renderer.Render(context, designer.Swatch(look), area.Deflate(new Thickness(3, 2, 3, 6)));
        }
    }
}
