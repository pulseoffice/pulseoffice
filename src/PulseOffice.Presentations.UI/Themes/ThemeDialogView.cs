using PulseOffice.Foundation.Colors;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Fonts;
using PulseOffice.UI.Controls.Galleries;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.Presentations.UI.Themes;

public enum ThemeScope
{
    AllSlides,
    CurrentSlide,
}

public sealed record ThemeDialogChoice(ThemeChoice Choice, FontScheme Fonts, ThemeScope Scope = ThemeScope.AllSlides);

public sealed record ThemeDialogServices(
    IThemeChoices Choices, IThemeNames Names, IThemePreviews Previews, IFontPickerFactory Fonts, ITextCatalog Texts,
    IGalleryNavigator? Navigator = null);

public sealed class ThemeDialogView : UserControl, IDialogContent, IDialogFooterOwner
{
    private const double TileWidth = 150;
    private const double PreviewWidth = 360;
    private const double SwatchSize = 28;

    private readonly ThemeDialogServices _services;
    private readonly Presentation _deck;
    private readonly IReadOnlyList<ThemeChoice> _options;
    private readonly DesignTokens.Metrics _metrics = DesignTokens.For(InterfaceDensity.Compact);
    private readonly ThumbnailGallery<int> _gallery;
    private readonly ContentControl _titlePreview = new();
    private readonly ContentControl _contentPreview = new();
    private readonly WrapPanel _swatches = new() { ItemSpacing = 6, LineSpacing = 6 };
    private readonly TextBlock _name = new();
    private readonly TextBlock _majorSample = new() { TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock _minorSample = new() { TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly FontPicker _major;
    private readonly FontPicker _minor;

    private int _selected;

    public ThemeDialogView(Presentation deck, ThemeDialogServices services)
    {
        _deck = deck ?? throw new ArgumentNullException(nameof(deck));
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _options = services.Choices.For(deck);
        _selected = Math.Max(0, services.Choices.IndexOfCurrent(deck, _options));

        _major = services.Fonts.Create(deck.Theme.Fonts.Major);
        _minor = services.Fonts.Create(deck.Theme.Fonts.Minor);
        _major.FamilyChanged += (_, _) => Refresh();
        _minor.FamilyChanged += (_, _) => Refresh();

        _gallery = Gallery();
        var layout = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto") };
        var gallery = _gallery;
        var divider = new Border { Width = 1, Margin = new Thickness(_metrics.SpacingLarge, 0) }
            .Themed(Border.BackgroundProperty, Palette.Keys.Border);
        var details = Details();
        Grid.SetColumn(divider, 1);
        Grid.SetColumn(details, 2);
        layout.Children.Add(gallery);
        layout.Children.Add(divider);
        layout.Children.Add(details);
        Content = layout;

        Footer = new DialogFooter()
            .WithCancel(services.Texts["prompt.cancel"], () => Completed?.Invoke(this, null))
            .WithNote(services.Texts["deck.themeDialog.explanation"])
            .WithAction(services.Texts["deck.themeDialog.apply"], () => Completed?.Invoke(this, Result()), primary: true);

        Select(_selected, resetFonts: false);
    }

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; }

    private ITextCatalog Texts => _services.Texts;

    public ThumbnailGallery<int> ThemeGallery => _gallery;

    private ThumbnailGallery<int> Gallery()
    {
        var thumbnailWidth = TileWidth - 10;
        var gallery = new ThumbnailGallery<int>(
            [.. _options.Select((choice, index) => new ThumbnailGalleryItem<int>(index, _services.Names.Display(choice),
                Texts[choice.Origin == ThemeOrigin.Document ? "deck.theme.inDocument" : "deck.theme.catalog"]))],
            new ThemeThumbnails(_services, _deck, _options),
            _services.Navigator ?? new GalleryNavigator(),
            new ThumbnailGalleryOptions
            {
                TileWidth = TileWidth,
                ThumbnailWidth = thumbnailWidth,
                ThumbnailHeight = thumbnailWidth * 9 / 16,
                CaptionGap = _metrics.SpacingSmall / 2,
                FrameRadius = _metrics.Radius,
                FrameBorder = false,
                Scrolls = true,
                AccessibleName = Texts["deck.theme.catalog"],
            },
            _metrics);
        gallery.SelectionChanged += (_, index) => Select(index, resetFonts: true);
        gallery.Invoked += (_, _) => Completed?.Invoke(this, Result());
        return gallery;
    }

    private sealed class ThemeThumbnails(ThemeDialogServices services, Presentation deck, IReadOnlyList<ThemeChoice> options) : IThumbnailPainter<int>
    {
        public Control Create(int item, Size size)
        {
            var design = services.Choices.Design(deck, options[item]);
            return services.Previews.Create(design.Theme, design.Master, deck.Size, size.Width, size.Height);
        }

        public void Repaint(Control thumbnail, int item)
        {
        }
    }

    private Control Details()
    {
        _name.FontSize = _metrics.FontSizeHeading;
        _name.FontWeight = FontWeight.SemiBold;
        _name.TextTrimming = TextTrimming.CharacterEllipsis;

        var stack = new StackPanel
        {
            Width = PreviewWidth,
            Margin = new Thickness(0, 0, _metrics.SpacingMedium, 0),
            Spacing = _metrics.SpacingSmall,
            Children =
            {
                _name,
                Caption(Texts["deck.themeDialog.scope"]),
                Scope(),
                Caption(Texts["deck.themeDialog.preview"]),
                Framed(_titlePreview),
                Framed(_contentPreview),
                Caption(Texts["deck.themeDialog.colours"]),
                _swatches,
                Caption(Texts["deck.themeDialog.fonts"]),
                FontRow(Texts["deck.themeDialog.major"], _major, _majorSample),
                FontRow(Texts["deck.themeDialog.minor"], _minor, _minorSample),
            },
        };

        return new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = stack,
        };
    }

    private Border Framed(Control preview)
    {
        var frame = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(_metrics.RadiusSoft),
            ClipToBounds = true,
            Child = preview,
        };

        frame.Themed(Border.BorderBrushProperty, Palette.Keys.Border);
        return frame;
    }

    private Control FontRow(string label, FontPicker picker, TextBlock sample)
    {
        sample.Text = Texts["deck.themeDialog.fontSample"];
        sample.FontSize = _metrics.FontSizeHeading;
        sample.Margin = new Thickness(0, 2, 0, _metrics.SpacingSmall);

        var title = new TextBlock
        {
            Text = label,
            FontSize = _metrics.FontSizeSmall,
            VerticalAlignment = VerticalAlignment.Center,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("120,*") };
        Grid.SetColumn(picker, 1);
        row.Children.Add(title);
        row.Children.Add(picker);

        return new StackPanel { Children = { row, sample } };
    }

    private TextBlock Caption(string text) => new TextBlock
    {
        Text = text.ToUpperInvariant(),
        FontSize = Math.Max(9, _metrics.FontSizeSmall - 1),
        LetterSpacing = 0.6,
        Margin = new Thickness(0, _metrics.SpacingSmall, 0, 2),
    }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

    private void Select(int index, bool resetFonts)
    {
        if (_options.Count == 0) return;

        _selected = Math.Clamp(index, 0, _options.Count - 1);

        _gallery.Select(_selected);

        if (resetFonts)
        {
            var fonts = _services.Choices.Design(_deck, _options[_selected]).Theme.Fonts;
            _major.Show(fonts.Major);
            _minor.Show(fonts.Minor);
        }

        Refresh();
    }

    private void Refresh()
    {
        if (_options.Count == 0) return;

        var choice = _options[_selected];
        var design = _services.Choices.Design(_deck, choice);
        var theme = design.Theme with { Fonts = Fonts(design.Theme.Fonts) };
        var master = design.Master with { Theme = theme };
        var width = PreviewWidth - 2;
        var height = _deck.Size.Width.Value > 0 ? width * _deck.Size.Height.Value / _deck.Size.Width.Value : width * 9 / 16;

        _name.Text = _services.Names.Display(choice);
        _titlePreview.Content = _services.Previews.Create(theme, master, _deck.Size,
            new ThemeSample(SlideLayout.TitleSlide, Texts["deck.themeDialog.sample.title"], Texts["deck.themeDialog.sample.subtitle"]),
            width, height);
        _contentPreview.Content = _services.Previews.Create(theme, master, _deck.Size,
            new ThemeSample(SlideLayout.TitleAndContent, Texts["deck.themeDialog.sample.heading"],
                string.Join('\n', Texts["deck.themeDialog.sample.point1"], Texts["deck.themeDialog.sample.point2"], Texts["deck.themeDialog.sample.point3"])),
            width, height);

        _majorSample.FontFamily = new FontFamily(theme.Fonts.Major);
        _minorSample.FontFamily = new FontFamily(theme.Fonts.Minor);

        _swatches.Children.Clear();
        var colours = theme.Colors;
        _swatches.Children.Add(Swatch(colours.Background, Texts["deck.themeDialog.colour.background"]));
        _swatches.Children.Add(Swatch(colours.Text, Texts["deck.themeDialog.colour.text"]));
        _swatches.Children.Add(Swatch(colours.Background2, Texts["deck.themeDialog.colour.background2"]));
        _swatches.Children.Add(Swatch(colours.Text2, Texts["deck.themeDialog.colour.text2"]));
        uint[] accents = [colours.Accent1, colours.Accent2, colours.Accent3, colours.Accent4, colours.Accent5, colours.Accent6];
        for (var i = 0; i < accents.Length; i++)
            _swatches.Children.Add(Swatch(accents[i], Texts.Format("deck.themeDialog.colour.accent", i + 1)));
    }

    private FontScheme Fonts(FontScheme fallback) => new()
    {
        Major = _major.Family is { Length: > 0 } major ? major : fallback.Major,
        Minor = _minor.Family is { Length: > 0 } minor ? minor : fallback.Minor,
    };

    private Control Swatch(uint argb, string label)
    {
        var chip = new Border
        {
            Width = SwatchSize,
            Height = SwatchSize,
            CornerRadius = new CornerRadius(_metrics.Radius),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Center,
            Background = new SolidColorBrush(Color.FromUInt32(0xFF000000 | argb)),
        };

        chip.Themed(Border.BorderBrushProperty, Palette.Keys.Border);

        var item = new StackPanel
        {
            Width = 64,
            Spacing = 2,
            Children =
            {
                chip,
                new TextBlock
                {
                    Text = label,
                    FontSize = Math.Max(9, _metrics.FontSizeSmall - 1),
                    TextAlignment = TextAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary),
            },
        };

        ToolTip.SetTip(item, $"{label}  {new ArgbColor(argb).ToHashedHex()}");
        return item;
    }

    private readonly ComboBox _scope = new() { HorizontalAlignment = HorizontalAlignment.Stretch };

    private Control Scope()
    {
        _scope.ItemsSource = new[] { Texts["deck.themeDialog.scope.all"], Texts["deck.themeDialog.scope.current"] };
        _scope.SelectedIndex = 0;
        return _scope;
    }

    private ThemeDialogChoice? Result() =>
        _options.Count == 0 ? null : new ThemeDialogChoice(_options[_selected],
            Fonts(_services.Choices.Design(_deck, _options[_selected]).Theme.Fonts),
            _scope.SelectedIndex == 1 ? ThemeScope.CurrentSlide : ThemeScope.AllSlides);
}
