using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Ribbon;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.UI.Themes;

public interface IThemeNames
{
    string Display(Theme theme);

    string Display(ThemeChoice choice);
}

public sealed class ThemeNames(IThemeCatalog catalog, ITextCatalog texts) : IThemeNames
{
    public string Display(Theme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        return catalog.Find(theme.Name) is { } known && texts.Contains(Key(known))
            ? texts[Key(known)]
            : theme.Name;
    }

    private string Key(Theme theme) => $"deck.theme.{theme.Name.ToLowerInvariant()}";

    public string Display(ThemeChoice choice)
    {
        ArgumentNullException.ThrowIfNull(choice);
        return choice.Origin == ThemeOrigin.Document ? texts.Format("deck.theme.documentEntry", choice.Theme.Name) : Display(choice.Theme);
    }
}

public sealed record ThemeSample(SlideLayout Layout, string Title, string? Body);

public interface IThemePreviews
{
    Control Create(Theme theme, SlideMaster master, SlideSize size, double width, double height);

    Control Create(Theme theme, SlideMaster master, SlideSize size, ThemeSample sample, double width, double height);
}

public sealed class ThemePreviews(ISlideRenderer renderer, ISlideFactory slides) : IThemePreviews
{
    public Control Create(Theme theme, SlideMaster master, SlideSize size, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(theme);
        return Create(theme, master, size, new ThemeSample(SlideLayout.TitleSlide, "Aa", theme.Fonts.Minor), width, height);
    }

    public Control Create(Theme theme, SlideMaster master, SlideSize size, ThemeSample sample, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(master);
        ArgumentNullException.ThrowIfNull(sample);
        var slide = slides.Create("preview", master, sample.Layout);
        foreach (var element in slide.Elements.OfType<TextElement>().ToList())
        {
            var text = element.Text.Role switch
            {
                PlaceholderRole.Title => sample.Title,
                PlaceholderRole.Subtitle or PlaceholderRole.Body => sample.Body,
                _ => null,
            };
            if (text is not null) slide.Replace(element with { Text = element.Text.WithPlainText(text) });
        }

        return new ThemePreview(renderer, slide, size, theme, master) { Width = width, Height = height };
    }
}

public sealed class ThemePreview(ISlideRenderer renderer, Slide slide, SlideSize size, Theme theme, SlideMaster master) : Control
{
    private readonly SlidePalette _palette = new(Colors.Transparent, Color.FromArgb(0x40, 0x80, 0x80, 0x80));

    public override void Render(DrawingContext context)
    {
        var ratio = size.Width.Value > 0 && size.Height.Value > 0 ? (double)size.Height.Value / size.Width.Value : 9.0 / 16.0;
        var width = Math.Min(Bounds.Width, Bounds.Height / ratio);
        var height = width * ratio;
        if (width <= 0 || height <= 0) return;
        var target = new Rect((Bounds.Width - width) / 2, (Bounds.Height - height) / 2, width, height);
        renderer.Render(context, slide, size, target, theme, master, _palette);
    }
}

public sealed record ThemeThumbnail(Theme Theme, SlideMaster Master, SlideSize Size) : RibbonThumbnail;

public sealed class ThemeThumbnailPainter(IThemePreviews previews) : IRibbonThumbnailPainter
{
    public bool Accepts(RibbonThumbnail thumbnail) => thumbnail is ThemeThumbnail;

    public Control Paint(RibbonThumbnail thumbnail, double width, double height)
    {
        var theme = (ThemeThumbnail)thumbnail;
        return previews.Create(theme.Theme, theme.Master, theme.Size, width, height);
    }
}
