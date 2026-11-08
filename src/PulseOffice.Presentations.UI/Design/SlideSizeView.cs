using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Foundation.Units;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Choices;
using PulseOffice.UI.Controls.Dialogs;
using PulseOffice.UI.Controls.Fields;
using PulseOffice.UI.Controls.Galleries;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.Presentations.UI.Design;

public sealed record SlideSizeResult(SlideSize Size, bool FitContents);

public sealed record SlideSizeServices(ISlideResize Resize, IMeasureUnits Units, IIconCatalog Icons, ITextCatalog Texts, IGalleryNavigator Navigator);

public sealed class SlideProportionPainter(ISlideResize resize, ITextCatalog texts) : IThumbnailPainter<SlideSizePreset>
{
    public Control Create(SlideSizePreset item, Size size)
    {
        var slide = resize.For(item);
        var ratio = (double)slide.Width.Value / Math.Max(1, slide.Height.Value);
        var width = Math.Min(size.Width - 12, (size.Height - 12) * ratio);
        var height = width / ratio;
        var sheet = new Border
        {
            Width = item == SlideSizePreset.Custom ? (size.Height - 16) * 1.3 : width,
            Height = item == SlideSizePreset.Custom ? size.Height - 16 : height,
            BorderThickness = new Thickness(item == SlideSizePreset.Custom ? 1.5 : 1),
            CornerRadius = new CornerRadius(2),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = item == SlideSizePreset.Custom ? "?" : Label(slide),
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary),
        }.Themed(Border.BorderBrushProperty, item == SlideSizePreset.Custom ? "PulseAccent" : Palette.Keys.BorderStrong)
         .Themed(Border.BackgroundProperty, Palette.Keys.Surface);
        return new Border { Width = size.Width, Height = size.Height, Child = sheet };
    }

    public void Repaint(Control thumbnail, SlideSizePreset item)
    {
    }

    private string Label(SlideSize slide)
    {
        var width = slide.Width.ToCentimeters();
        var height = slide.Height.ToCentimeters();
        var ratio = width / Math.Max(0.01, height);
        foreach (var (wide, tall) in new[] { (16, 9), (16, 10), (4, 3) })
            if (Math.Abs(ratio - (double)wide / tall) < 0.01) return $"{wide}:{tall}";
        return texts.Format("deck.slideSize.cm", width.ToString("0.#", CultureInfo.CurrentCulture), height.ToString("0.#", CultureInfo.CurrentCulture));
    }
}

public sealed class SlideSizeView : UserControl, IDialogContent, IDialogFooterOwner, IDialogSizeHint
{
    private readonly SlideSizeServices _services;
    private readonly DesignTokens.Metrics _metrics = DesignTokens.For(InterfaceDensity.Compact);
    private readonly ThumbnailGallery<SlideSizePreset> _gallery;
    private readonly MeasureField _width;
    private readonly MeasureField _height;
    private readonly IconChoiceGroup _orientation;
    private readonly StackPanel _scaling = new() { Spacing = 6 };
    private bool _fit = true;
    private bool _syncing;

    public SlideSizeView(SlideSize current, SlideSizeServices services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        var presets = Enum.GetValues<SlideSizePreset>();
        _gallery = new ThumbnailGallery<SlideSizePreset>(
            [.. presets.Select(preset => new ThumbnailGalleryItem<SlideSizePreset>(preset, Texts[$"deck.slideSize.preset.{Key(preset)}"]))],
            new SlideProportionPainter(services.Resize, services.Texts),
            services.Navigator,
            new ThumbnailGalleryOptions
            {
                TileWidth = 104,
                ThumbnailHeight = 64,
                Columns = 4,
                FrameRadius = _metrics.RadiusTight,
                AccessibleName = Texts["deck.slideSize.preset"],
            },
            _metrics);
        _gallery.SelectionChanged += (_, preset) => ChoosePreset(preset);

        _width = Centimetres(current.Width.ToCentimeters(), Texts["deck.slideSize.widthShort"]);
        _height = Centimetres(current.Height.ToCentimeters(), Texts["deck.slideSize.heightShort"]);
        _width.ValueCommitted += (_, _) => Custom();
        _height.ValueCommitted += (_, _) => Custom();

        _orientation = new IconChoiceGroup(
            [
                new IconChoice("landscape", "deck.slideSize.landscape", Texts["deck.slideSize.landscape"]),
                new IconChoice("portrait", "deck.slideSize.portrait", Texts["deck.slideSize.portrait"]),
            ],
            services.Icons, _metrics, Texts["deck.slideSize.orientation"], services.Resize.IsPortrait(current) ? 1 : 0, showLabels: true);
        _orientation.SelectionChanged += (_, _) => Rotate();

        var size = new DialogSection(Texts["deck.slideSize.custom"], _metrics);
        var widthRow = size.Add(Texts["deck.slideSize.widthShort"], _width);
        if (widthRow.Label is { } scrubWidth) _width.AttachScrubber(scrubWidth);
        var heightRow = size.Add(Texts["deck.slideSize.heightShort"], _height);
        if (heightRow.Label is { } scrubHeight) _height.AttachScrubber(scrubHeight);
        size.Add(Texts["deck.slideSize.orientation"], _orientation, editorWidth: 260);

        var scaling = new DialogSection(Texts["deck.slideSize.scaling"], _metrics);
        scaling.Add(null, _scaling, DialogRowKind.Wide);
        BuildScaling();

        var settings = new StackPanel { Spacing = _metrics.SpacingLarge, Width = 300, Children = { size, scaling } };
        Grid.SetIsSharedSizeScope(settings, true);

        var layout = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = _metrics.SpacingLarge };
        Grid.SetColumn(settings, 1);
        layout.Children.Add(_gallery);
        layout.Children.Add(settings);
        Content = new StackPanel
        {
            Spacing = _metrics.SpacingMedium,
            Children =
            {
                new TextBlock { Text = Texts["deck.slideSize.explanation"], TextWrapping = TextWrapping.Wrap }
                    .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary),
                layout,
            },
        };

        Footer = new DialogFooter()
            .WithCancel(Texts["prompt.cancel"], () => Completed?.Invoke(this, null))
            .WithAction(Texts["deck.motion.apply"], () => Completed?.Invoke(this, Result()), primary: true);

        _syncing = true;
        _gallery.Select(services.Resize.Recognize(current));
        _syncing = false;
    }

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; }

    public double? PreferredWidth => 820;

    public ThumbnailGallery<SlideSizePreset> Gallery => _gallery;

    public MeasureField WidthField => _width;

    public MeasureField HeightField => _height;

    public IconChoiceGroup Orientation => _orientation;

    public bool FitContents => _fit;

    public IReadOnlyList<ChoiceOptionCard> ScalingCards => [.. _scaling.Children.OfType<ChoiceOptionCard>()];

    private ITextCatalog Texts => _services.Texts;

    public SlideSize Size => new(Twip.FromCentimeters(_width.Value), Twip.FromCentimeters(_height.Value));

    public SlideSizeResult Result() => new(Size, _fit);

    public void ChooseScaling(bool fit)
    {
        _fit = fit;
        BuildScaling();
    }

    private void BuildScaling()
    {
        _scaling.Children.Clear();
        foreach (var (fit, key, icon) in new[] { (true, "fit", "deck.slideSize.fitContent"), (false, "keep", "deck.slideSize.keepContent") })
        {
            var card = new ChoiceOptionCard(new PromptChoice(Texts[$"deck.slideSize.scaling.{key}"], fit, IsDefault: fit == _fit)
            {
                Description = Texts[$"deck.slideSize.scaling.{key}.description"],
                Icon = icon,
            }, _metrics, _services.Icons);
            card.Click += (_, _) => ChooseScaling(fit);
            _scaling.Children.Add(card);
        }
    }

    private void ChoosePreset(SlideSizePreset preset)
    {
        if (_syncing || preset == SlideSizePreset.Custom) return;
        var size = _services.Resize.For(preset);
        if (_orientation.SelectedIndex == 1 && !_services.Resize.IsPortrait(size)) size = _services.Resize.Rotated(size);
        Show(size);
    }

    private void Rotate()
    {
        if (_syncing) return;
        var size = Size;
        if (_services.Resize.IsPortrait(size) != (_orientation.SelectedIndex == 1)) Show(_services.Resize.Rotated(size));
    }

    private void Custom()
    {
        if (_syncing) return;
        _syncing = true;
        _gallery.Select(_services.Resize.Recognize(Size));
        _orientation.SelectedIndex = _services.Resize.IsPortrait(Size) ? 1 : 0;
        _syncing = false;
    }

    private void Show(SlideSize size)
    {
        _syncing = true;
        _width.Value = Math.Round(size.Width.ToCentimeters(), 2);
        _height.Value = Math.Round(size.Height.ToCentimeters(), 2);
        _orientation.SelectedIndex = _services.Resize.IsPortrait(size) ? 1 : 0;
        _gallery.Select(_services.Resize.Recognize(size));
        _syncing = false;
    }

    private MeasureField Centimetres(double value, string name) =>
        new(new MeasureEntry(new MeasureOptions("cm", 1, 150, 0.1) { LargeStep = 1 }, _services.Units, CultureInfo.CurrentCulture),
            Math.Round(value, 2), Texts, _metrics, _services.Icons, name, boxWidth: 72)
        {
            HorizontalAlignment = HorizontalAlignment.Left,
        };

    private string Key(SlideSizePreset preset) => char.ToLowerInvariant(preset.ToString()[0]) + preset.ToString()[1..];
}
