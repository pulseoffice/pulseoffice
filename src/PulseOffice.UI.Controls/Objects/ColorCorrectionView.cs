using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PulseOffice.Drawing.Assets.Filters;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Dialogs;
using PulseOffice.UI.Controls.Fields;
using PulseOffice.UI.Controls.Galleries;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Objects;

public sealed record ColorCorrectionPreset(string Key, ColorAdjustment Adjustment);

public interface IColorCorrectionPresets
{
    IReadOnlyList<ColorCorrectionPreset> All { get; }
}

public sealed class ColorCorrectionPresets : IColorCorrectionPresets
{
    public IReadOnlyList<ColorCorrectionPreset> All { get; } =
    [
        new("original", new ColorAdjustment()),
        new("brighter", new ColorAdjustment(Brightness: 20)),
        new("darker", new ColorAdjustment(Brightness: -20)),
        new("contrast", new ColorAdjustment(Contrast: 25)),
        new("soft", new ColorAdjustment(Brightness: 10, Contrast: -25)),
        new("vivid", new ColorAdjustment(Brightness: 5, Contrast: 20, Gamma: 0.9)),
        new("warmer", new ColorAdjustment(Red: 15, Blue: -15)),
        new("cooler", new ColorAdjustment(Red: -15, Blue: 15)),
    ];
}

public sealed class AdjustedImagePainter(IBitmapPixels pixels, Bitmap source) : IThumbnailPainter<ColorCorrectionPreset>
{
    public Control Create(ColorCorrectionPreset item, Size size)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new Image { Source = Render(item.Adjustment), Width = size.Width, Height = size.Height, Stretch = Stretch.Uniform };
    }

    public void Repaint(Control thumbnail, ColorCorrectionPreset item)
    {
        if (thumbnail is Image image) image.Source = Render(item.Adjustment);
    }

    public Bitmap Render(ColorAdjustment adjustment) =>
        adjustment.IsIdentity ? source : pixels.Edit(source, new ColorAdjustmentFilter(adjustment).Apply);
}

public sealed class ColorCorrectionView : UserControl, IDialogContent, IDialogFooterOwner
{
    private const int PreviewWidth = 320;
    private const int ThumbnailWidth = 96;

    private readonly AdjustedImagePainter _large;
    private readonly Image _before = new() { Stretch = Stretch.Uniform, Width = 240, Height = 150 };
    private readonly Image _after = new() { Stretch = Stretch.Uniform, Width = 240, Height = 150 };
    private readonly Dictionary<string, MeasureField> _fields = [];
    private bool _syncing;

    public ColorCorrectionView(
        Bitmap original, IBitmapPixels pixels, IColorCorrectionPresets presets, IGalleryNavigator navigator,
        IMeasureUnits units, ITextCatalog texts, InterfaceDensity density = InterfaceDensity.Compact)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(pixels);
        ArgumentNullException.ThrowIfNull(presets);
        ArgumentNullException.ThrowIfNull(texts);
        var metrics = DesignTokens.For(density);

        var preview = Scaled(original, PreviewWidth);
        _large = new AdjustedImagePainter(pixels, preview);
        _before.Source = preview;
        _after.Source = preview;
        AutomationProperties.SetName(_before, texts["imageColor.before"]);
        AutomationProperties.SetName(_after, texts["imageColor.after"]);

        Presets = new ThumbnailGallery<ColorCorrectionPreset>(
            [.. presets.All.Select(preset => new ThumbnailGalleryItem<ColorCorrectionPreset>(preset, texts[$"imageColor.preset.{preset.Key}"]))],
            new AdjustedImagePainter(pixels, Scaled(original, ThumbnailWidth)), navigator,
            new ThumbnailGalleryOptions { TileWidth = 92, ThumbnailHeight = 62, Columns = 4, AccessibleName = texts["imageColor.presets"] },
            metrics);
        Presets.Select(0);
        Presets.SelectionChanged += (_, preset) => Apply(preset.Adjustment);

        var layout = new TwoPaneDialogLayout(metrics) { Header = new HelpTip(texts["imageColor.explanation"], texts, metrics) };
        layout.AddSection(texts["imageColor.presets"]).Add(null, Presets, DialogRowKind.Wide, editorWidth: 400);

        var light = layout.AddSection(texts["imageColor.section.light"]);
        Add(light, "brightness", texts["imageColor.brightness"], "%", -100, 100, 1, 0, units, texts, metrics);
        Add(light, "contrast", texts["imageColor.contrast"], "%", -100, 100, 1, 0, units, texts, metrics);
        Add(light, "gamma", texts["imageColor.gamma"], string.Empty, 0.1, 10, 0.05, 1, units, texts, metrics);

        var colour = layout.AddSection(texts["imageColor.section.colour"]);
        Add(colour, "red", texts["imageColor.red"], "%", -100, 100, 1, 0, units, texts, metrics);
        Add(colour, "green", texts["imageColor.green"], "%", -100, 100, 1, 0, units, texts, metrics);
        Add(colour, "blue", texts["imageColor.blue"], "%", -100, 100, 1, 0, units, texts, metrics);

        var comparison = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto"), RowSpacing = metrics.SpacingSmall };
        comparison.Children.Add(Caption(texts["imageColor.before"], metrics, 0));
        comparison.Children.Add(Caption(texts["imageColor.after"], metrics, 1));
        comparison.Children.Add(Framed(_before, 0, metrics));
        comparison.Children.Add(Framed(_after, 1, metrics));
        layout.Preview = comparison;

        Content = new ScrollViewer { Content = layout, MaxHeight = 640, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };

        Footer = new DialogFooter(density)
            .WithCancel(texts["prompt.cancel"], () => Completed?.Invoke(this, null))
            .WithAction(texts["imageColor.reset"], () => Apply(new ColorAdjustment()))
            .WithAction(texts["prompt.confirm"], () => Completed?.Invoke(this, Adjustment), primary: true);
    }

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; }

    public ThumbnailGallery<ColorCorrectionPreset> Presets { get; }

    public Image After => _after;

    public ColorAdjustment Adjustment => new(
        _fields["brightness"].Value, _fields["contrast"].Value, _fields["red"].Value,
        _fields["green"].Value, _fields["blue"].Value, _fields["gamma"].Value);

    public void Apply(ColorAdjustment adjustment)
    {
        ArgumentNullException.ThrowIfNull(adjustment);
        _syncing = true;
        try
        {
            _fields["brightness"].Value = adjustment.Brightness;
            _fields["contrast"].Value = adjustment.Contrast;
            _fields["gamma"].Value = adjustment.Gamma;
            _fields["red"].Value = adjustment.Red;
            _fields["green"].Value = adjustment.Green;
            _fields["blue"].Value = adjustment.Blue;
        }
        finally
        {
            _syncing = false;
        }

        Refresh();
    }

    private void Refresh() => _after.Source = _large.Render(Adjustment);

    private void Add(DialogSection section, string key, string label, string unit, double minimum, double maximum, double step, double initial,
        IMeasureUnits units, ITextCatalog texts, DesignTokens.Metrics metrics)
    {
        var field = new MeasureField(
            new MeasureEntry(new MeasureOptions(unit, minimum, maximum, step) { ShowSlider = true, LargeStep = step * 10 }, units, CultureInfo.CurrentCulture),
            initial, texts, metrics, null, label, boxWidth: 56)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        field.ValueCommitted += (_, _) =>
        {
            if (_syncing) return;
            Refresh();
        };
        _fields[key] = field;
        var row = section.Add(label, field, editorWidth: 300);
        if (row.Label is { } handle) field.AttachScrubber(handle);
    }

    private Bitmap Scaled(Bitmap source, int width)
    {
        if (source.PixelSize.Width <= width) return source;
        var height = Math.Max(1, (int)Math.Round(source.PixelSize.Height * (double)width / source.PixelSize.Width));
        return source.CreateScaledBitmap(new PixelSize(width, height));
    }

    private Control Caption(string text, DesignTokens.Metrics metrics, int index)
    {
        var caption = new TextBlock { Text = text, FontSize = metrics.FontSizeSmall, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 0, 0, 4) }
            .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
        Grid.SetRow(caption, index * 2);
        return caption;
    }

    private Control Framed(Image image, int index, DesignTokens.Metrics metrics)
    {
        var frame = new Border
        {
            Child = image,
            CornerRadius = new CornerRadius(metrics.RadiusTight),
            ClipToBounds = true,
            Background = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        Grid.SetRow(frame, index * 2 + 1);
        return frame;
    }
}
