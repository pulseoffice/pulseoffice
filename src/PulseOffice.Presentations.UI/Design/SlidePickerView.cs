using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.Presentations.UI.Motion;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Galleries;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.Presentations.UI.Design;

public sealed record SlidePickerServices(IDeckSlidePictures Pictures, IGalleryNavigator Navigator, ITextCatalog Texts);

public interface ISlideCaptions
{
    string Title(Slide slide);
}

public sealed class SlideCaptions : ISlideCaptions
{
    public string Title(Slide slide)
    {
        ArgumentNullException.ThrowIfNull(slide);
        if (!string.IsNullOrWhiteSpace(slide.Name)) return slide.Name!;
        var title = slide.Elements.OfType<TextElement>().FirstOrDefault(element => element.Text.Role == PlaceholderRole.Title);
        return title?.Text.PlainText.Trim() ?? string.Empty;
    }
}

public sealed class SlideThumbnailPainter(IDeckSlidePictures pictures, Presentation deck) : IThumbnailPainter<int>
{
    public Control Create(int item, Size size) =>
        new Border { Background = Brushes.White, Child = pictures.Create(deck, deck.Slides[item], size.Width, size.Height) };

    public void Repaint(Control thumbnail, int item)
    {
    }
}

public sealed class SlidePickerView : UserControl, IDialogContent, IDialogFooterOwner, IDialogSizeHint
{
    private const double TileWidth = 150;

    private readonly ThumbnailGallery<int> _gallery;

    public SlidePickerView(Presentation deck, int current, string explanation, SlidePickerServices services, ISlideCaptions captions)
    {
        ArgumentNullException.ThrowIfNull(deck);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(captions);
        var metrics = DesignTokens.For(InterfaceDensity.Compact);
        var ratio = deck.Size.Width.Value > 0 ? (double)deck.Size.Height.Value / deck.Size.Width.Value : 9.0 / 16.0;
        var thumbnailWidth = TileWidth - 10;
        _gallery = new ThumbnailGallery<int>(
            [.. deck.Slides.Select((slide, index) =>
            {
                var title = captions.Title(slide);
                var number = (index + 1).ToString(CultureInfo.CurrentCulture);
                var caption = title.Length > 0 ? $"{number}. {title}" : number;
                return new ThumbnailGalleryItem<int>(index, caption, slide.SectionName, $"{number} {title}");
            })],
            new SlideThumbnailPainter(services.Pictures, deck),
            services.Navigator,
            new ThumbnailGalleryOptions
            {
                TileWidth = TileWidth,
                ThumbnailWidth = thumbnailWidth,
                ThumbnailHeight = Math.Round(thumbnailWidth * ratio),
                FrameRadius = metrics.RadiusTight,
                Columns = 4,
                Scrolls = true,
                MaxViewportHeight = 460,
                Filterable = true,
                FilterPlaceholder = services.Texts["deck.navigate.goTo.search"],
                EmptyText = services.Texts["deck.navigate.goTo.none"],
                AccessibleName = explanation,
            },
            metrics);
        _gallery.Select(Math.Clamp(current, 0, Math.Max(0, deck.Slides.Count - 1)));
        _gallery.Invoked += (_, index) => Completed?.Invoke(this, index);

        Content = new StackPanel
        {
            Spacing = metrics.SpacingMedium,
            Children =
            {
                new TextBlock { Text = explanation, TextWrapping = TextWrapping.Wrap }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary),
                _gallery,
            },
        };
        Footer = new DialogFooter()
            .WithCancel(services.Texts["prompt.cancel"], () => Completed?.Invoke(this, null))
            .WithAction(services.Texts["deck.navigate.goTo.go"], () => Completed?.Invoke(this, _gallery.SelectedIndex >= 0 ? _gallery.SelectedIndex : null), primary: true);
    }

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; }

    public double? PreferredWidth => 4 * (TileWidth + 8) + 60;

    public ThumbnailGallery<int> Gallery => _gallery;
}
