using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PulseOffice.Drawing.Assets;
using PulseOffice.Drawing.Assets.Filters;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Dialogs;
using PulseOffice.UI.Controls.Fields;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Objects;

public sealed class ImageToneView : UserControl, IDialogContent, IDialogFooterOwner
{
    private const int PreviewWidth = 320;

    private static readonly ImageColorMode[] Modes = [ImageColorMode.Standard, ImageColorMode.BlackWhite, ImageColorMode.Watermark];

    private readonly Bitmap _preview;
    private readonly IBitmapPixels _pixels;
    private readonly IPixelFilterCatalog _filters;
    private readonly Image _after = new() { Stretch = Stretch.Uniform, Width = 260, Height = 170 };
    private readonly Dictionary<string, MeasureField> _fields = [];
    private readonly ComboBox _mode = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private bool _syncing;

    public ImageToneView(
        Bitmap original, ImageTone initial, IBitmapPixels pixels, IPixelFilterCatalog filters,
        IMeasureUnits units, ITextCatalog texts, InterfaceDensity density = InterfaceDensity.Compact)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(initial);
        ArgumentNullException.ThrowIfNull(texts);
        _pixels = pixels ?? throw new ArgumentNullException(nameof(pixels));
        _filters = filters ?? throw new ArgumentNullException(nameof(filters));
        var metrics = DesignTokens.For(density);
        _preview = Scaled(original);
        AutomationProperties.SetName(_after, texts["imageTone.preview"]);

        var layout = new TwoPaneDialogLayout(metrics) { Header = new HelpTip(texts["imageTone.explanation"], texts, metrics) };
        var corrections = layout.AddSection(texts["imageTone.section.corrections"]);
        Add(corrections, "brightness", texts["imageColor.brightness"], -100, 100, units, texts, metrics);
        Add(corrections, "contrast", texts["imageColor.contrast"], -100, 100, units, texts, metrics);

        var colour = layout.AddSection(texts["imageTone.section.colour"]);
        _mode.ItemsSource = Modes.Select(mode => texts[$"imageTone.mode.{mode}"]).ToList();
        AutomationProperties.SetName(_mode, texts["imageTone.mode"]);
        _mode.SelectionChanged += (_, _) =>
        {
            if (!_syncing) Refresh();
        };
        colour.Add(texts["imageTone.mode"], _mode, editorWidth: 300);

        var transparency = layout.AddSection(texts["imageTone.section.transparency"]);
        Add(transparency, "transparency", texts["imageTone.transparency"], 0, 100, units, texts, metrics);

        layout.Preview = new Border
        {
            Child = _after,
            CornerRadius = new CornerRadius(metrics.RadiusTight),
            ClipToBounds = true,
            HorizontalAlignment = HorizontalAlignment.Left,
        }.Themed(Border.BackgroundProperty, Palette.Keys.Surface);

        Content = new ScrollViewer { Content = layout, MaxHeight = 640, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };

        Footer = new DialogFooter(density)
            .WithCancel(texts["prompt.cancel"], () => Completed?.Invoke(this, null))
            .WithAction(texts["imageColor.reset"], () => Apply(ImageTone.Neutral))
            .WithAction(texts["prompt.confirm"], () => Completed?.Invoke(this, Tone), primary: true);

        Apply(initial);
    }

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; }

    public Image After => _after;

    public ImageTone Tone
    {
        get
        {
            var tone = new ImageTone(
                _fields["brightness"].Value, _fields["contrast"].Value, _fields["transparency"].Value,
                Modes[Math.Clamp(_mode.SelectedIndex, 0, Modes.Length - 1)]);
            return tone.IsNeutral ? ImageTone.Neutral : tone;
        }
    }

    public void Apply(ImageTone tone)
    {
        ArgumentNullException.ThrowIfNull(tone);
        _syncing = true;
        try
        {
            _fields["brightness"].Value = tone.Brightness;
            _fields["contrast"].Value = tone.Contrast;
            _fields["transparency"].Value = tone.Transparency;
            _mode.SelectedIndex = Math.Max(0, Array.IndexOf(Modes, tone.Mode));
        }
        finally
        {
            _syncing = false;
        }

        Refresh();
    }

    private void Refresh()
    {
        var tone = Tone;
        _after.Opacity = tone.Opacity;
        if (!tone.ChangesColours)
        {
            _after.Source = _preview;
            return;
        }

        _after.Source = _pixels.Edit(_preview, image =>
        {
            if (Math.Abs(tone.EffectiveBrightness) > 1e-6 || Math.Abs(tone.EffectiveContrast) > 1e-6)
                _filters.Adjust(new ColorAdjustment(tone.EffectiveBrightness, tone.EffectiveContrast)).Apply(image);
            if (tone.Mode == ImageColorMode.BlackWhite) _filters.BlackWhite().Apply(image);
        });
    }

    private void Add(DialogSection section, string key, string label, double minimum, double maximum,
        IMeasureUnits units, ITextCatalog texts, DesignTokens.Metrics metrics)
    {
        var field = new MeasureField(
            new MeasureEntry(new MeasureOptions("%", minimum, maximum, 1) { ShowSlider = true, LargeStep = 10 }, units, CultureInfo.CurrentCulture),
            0, texts, metrics, null, label, boxWidth: 56)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        field.ValueCommitted += (_, _) =>
        {
            if (!_syncing) Refresh();
        };
        _fields[key] = field;
        var row = section.Add(label, field, editorWidth: 300);
        if (row.Label is { } handle) field.AttachScrubber(handle);
    }

    private Bitmap Scaled(Bitmap source)
    {
        if (source.PixelSize.Width <= PreviewWidth) return source;
        var height = Math.Max(1, (int)Math.Round(source.PixelSize.Height * (double)PreviewWidth / source.PixelSize.Width));
        return source.CreateScaledBitmap(new PixelSize(PreviewWidth, height));
    }
}
