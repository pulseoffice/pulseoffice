using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations;

public sealed class SlideFieldsFactory
{
    public TextFields For(Presentation deck, Slide? slide)
    {
        ArgumentNullException.ThrowIfNull(deck);

        return new TextFields(
            slide is null ? deck.FirstSlideNumber : deck.NumberOf(slide),
            DateTime.Now,
            deck.Slides.Count,
            slide is null ? string.Empty : TitleOf(slide),
            deck.Properties.Author ?? string.Empty,
            deck.FileName ?? string.Empty,
            deck.TextLanguage);
    }

    private string TitleOf(Slide slide) =>
        slide.Name is { Length: > 0 } name
            ? name
            : slide.Elements.OfType<TextElement>().FirstOrDefault(element => element.Text.Role == PlaceholderRole.Title)?.Text.PlainText ?? string.Empty;
}
