using PulseOffice.Presentations;
using PulseOffice.Printing;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.UI.Rendering;

public interface ISlideStructureTags
{
    PrintTag SlideTag(Slide slide);

    IReadOnlyList<PrintTag> ReadingOrder(Slide slide);

    PrintTag? For(Slide slide, SlideElement? element);
}

public sealed class SlideStructureTags : ISlideStructureTags
{
    public PrintTag SlideTag(Slide slide)
    {
        ArgumentNullException.ThrowIfNull(slide);
        return new PrintTag("slide:" + slide.Id, PrintRole.Slide) { Alternate = slide.Name };
    }

    public IReadOnlyList<PrintTag> ReadingOrder(Slide slide)
    {
        ArgumentNullException.ThrowIfNull(slide);
        return [.. slide.Elements.Where(element => !element.Hidden)
            .OrderBy(element => IsTitle(element) ? 0 : 1)
            .ThenBy(element => element.ZOrder)
            .Select(element => For(slide, element))
            .OfType<PrintTag>()];
    }

    public PrintTag? For(Slide slide, SlideElement? element)
    {
        ArgumentNullException.ThrowIfNull(slide);
        if (element is null || !slide.Elements.Any(owned => string.Equals(owned.Id, element.Id, StringComparison.Ordinal))) return null;
        var parent = SlideTag(slide);
        var key = parent.Key + ":" + element.Id;
        return element switch
        {
            TextElement text when IsTitle(text) => new PrintTag(key, PrintRole.Title, parent),
            TextElement text when text.Text.PlainText.Trim().Length == 0 => null,
            TextElement => new PrintTag(key, PrintRole.Paragraph, parent),
            ObjectElement { Content: var content } => new PrintTag(key, PrintRole.Figure, parent)
            {
                Alternate = new[] { content.Description, content.Title, content.Name }.FirstOrDefault(text => !string.IsNullOrWhiteSpace(text)),
            },
            _ => null,
        };
    }

    private bool IsTitle(SlideElement element) => element is TextElement { Text.Role: PlaceholderRole.Title };
}
