using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations;

public sealed class SlideFieldsFactory
{
    public TextFields For(Presentation deck, Slide? slide)
    {
        ArgumentNullException.ThrowIfNull(deck);

        var index = slide is null ? 0 : deck.IndexOf(slide);
        return new TextFields(
            Math.Max(index, 0) + 1,
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
