using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.UI.Motion;

public interface IDeckElementNames
{
    string Of(Slide slide, string elementId);
}

public sealed class DeckElementNames(ITextCatalog texts) : IDeckElementNames
{
    private const int TextLength = 24;

    public string Of(Slide slide, string elementId)
    {
        ArgumentNullException.ThrowIfNull(slide);
        return slide.Find(elementId) switch
        {
            TextElement text when text.Text.PlainText.Trim() is { Length: > 0 } plain => plain[..Math.Min(TextLength, plain.Length)],
            TextElement text => Role(text.Text.Role),
            ObjectElement { Content.Name: { Length: > 0 } name } => name,
            MediaElement => texts["deck.placeholder.media"],
            ObjectElement => texts["deck.placeholder.free"],
            _ => elementId,
        };
    }

    private string Role(PlaceholderRole role) => role switch
    {
        PlaceholderRole.Title => texts["deck.placeholder.title"],
        PlaceholderRole.Subtitle => texts["deck.placeholder.subtitle"],
        PlaceholderRole.Body => texts["deck.placeholder.body"],
        PlaceholderRole.Text => texts["deck.placeholder.text"],
        PlaceholderRole.Picture => texts["deck.placeholder.picture"],
        _ => texts["deck.placeholder.free"],
    };
}
