using System.Globalization;
using System.Text.RegularExpressions;

namespace PulseOffice.Presentations;

public interface ISlideTargetResolver
{
    Slide? Find(Presentation deck, string target);
}

public sealed partial class SlideTargetResolver : ISlideTargetResolver
{
    public Slide? Find(Presentation deck, string target) =>
        deck.Slides.FirstOrDefault(slide => SlideNames.PageName(slide) == target)
        ?? deck.Slides.FirstOrDefault(slide => slide.Id == target)
        ?? ByPosition(deck, target)
        ?? deck.Slides.FirstOrDefault(slide => slide.Elements.Any(element => element is ObjectElement { Content.Name: { } name } && name == target));

    private static Slide? ByPosition(Presentation deck, string target) =>
        PositionalName().Match(target.Trim()) is { Success: true } match
        && int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var position)
        && position >= 1 && position <= deck.Slides.Count
            ? deck.Slides[position - 1]
            : null;

    [GeneratedRegex(@"^(?:slide|page)\s*(\d{1,6})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PositionalName();
}
