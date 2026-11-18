namespace PulseOffice.Presentations;

public sealed record SlideSectionHeader(string Name, Slide? StartSlide, EmptySection? Empty)
{
    public bool IsEmpty => Empty is not null;
}

public sealed record SlideStripItem(SlideSectionHeader? Header, Slide? Slide, int SlideIndex);

public sealed class SlideStripLayout
{
    public IReadOnlyList<SlideStripItem> Items(Presentation deck)
    {
        ArgumentNullException.ThrowIfNull(deck);
        var items = new List<SlideStripItem>();
        var ids = deck.Slides.Select(slide => slide.Id).ToHashSet(StringComparer.Ordinal);
        for (var index = 0; index < deck.Slides.Count; index++)
        {
            var slide = deck.Slides[index];
            foreach (var empty in deck.EmptySections.Where(section => section.BeforeSlideId == slide.Id))
                items.Add(new SlideStripItem(new SlideSectionHeader(empty.Name, null, empty), null, -1));
            if (slide.SectionName is { } name)
                items.Add(new SlideStripItem(new SlideSectionHeader(name, slide, null), null, -1));
            items.Add(new SlideStripItem(null, slide, index));
        }
        foreach (var empty in deck.EmptySections.Where(section => section.BeforeSlideId is null || !ids.Contains(section.BeforeSlideId)))
            items.Add(new SlideStripItem(new SlideSectionHeader(empty.Name, null, empty), null, -1));
        return items;
    }
}
