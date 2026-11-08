using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using PulseOffice.Presentations;

namespace PulseOffice.Presentations.UI.Motion;

public sealed record SlidePictureOptions
{
    public HeaderFooterSettings? HeaderFooter { get; init; }

    public SlideChrome? Chrome { get; init; }

    public SlideMaster? Master { get; init; }

    public Theme? Theme { get; init; }

    public bool Framed { get; init; } = true;
}

public interface IDeckSlidePictures
{
    Control Create(Presentation deck, Slide slide, double width, double height, SlidePictureOptions? options = null);
}

public sealed class DeckSlidePictures(ISlideRenderer renderer, HeaderFooterResolver chrome) : IDeckSlidePictures
{
    public DeckSlidePictures(ISlideRenderer renderer) : this(renderer, new HeaderFooterResolver())
    {
    }

    public Control Create(Presentation deck, Slide slide, double width, double height, SlidePictureOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(deck);
        ArgumentNullException.ThrowIfNull(slide);
        var settings = options ?? new SlidePictureOptions();
        var master = settings.Master ?? deck.MasterOf(slide);
        var theme = settings.Theme ?? deck.ThemeOf(slide);
        var number = Math.Max(1, deck.IndexOf(slide) + 1);
        var resolved = settings.Chrome
            ?? chrome.Resolve(settings.HeaderFooter ?? deck.HeaderFooter, slide, number, DateTime.Now, master.Elements, deck.TextLanguage);
        return new SlidePicture(renderer, slide, deck.Size, theme, master, resolved, settings.Framed) { Width = width, Height = height };
    }
}

public sealed class SlidePicture(
    ISlideRenderer renderer, Slide slide, SlideSize size, Theme theme, SlideMaster master, SlideChrome chrome, bool framed) : Control
{
    private readonly IPen _frame = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromArgb(0x30, 0, 0, 0)), 1);

    private readonly SlidePalette _palette = new(Colors.Transparent, Color.FromArgb(0x40, 0x80, 0x80, 0x80));

    public Slide Slide => slide;

    public SlideChrome Chrome => chrome;

    public Rect SlideBounds()
    {
        var ratio = size.Width.Value > 0 ? (double)size.Height.Value / size.Width.Value : 9.0 / 16.0;
        var width = Math.Min(Bounds.Width, Bounds.Height / ratio);
        var height = width * ratio;
        return new Rect((Bounds.Width - width) / 2, (Bounds.Height - height) / 2, width, height);
    }

    public override void Render(DrawingContext context)
    {
        var target = SlideBounds();
        if (target.Width <= 0 || target.Height <= 0) return;
        renderer.Render(context, slide, size, target, theme, master,
            _palette, chrome: chrome);
        if (framed) context.DrawRectangle(null, _frame, target);
    }
}
