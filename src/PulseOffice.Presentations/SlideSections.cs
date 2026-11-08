namespace PulseOffice.Presentations;

public sealed record DeckSection(string Name, IReadOnlyList<Slide> Slides);

public sealed class SlideSections
{
    public IReadOnlyList<DeckSection> Group(IReadOnlyList<Slide> slides)
    {
        ArgumentNullException.ThrowIfNull(slides);

        var sections = new List<DeckSection>();
        List<Slide>? current = null;

        foreach (var slide in slides)
        {
            if (slide.SectionName is { } name)
            {
                current = [];
                sections.Add(new DeckSection(name, current));
            }

            current?.Add(slide);
        }

        return sections;
    }

    public Slide? StartOf(IReadOnlyList<Slide> slides, Slide slide)
    {
        ArgumentNullException.ThrowIfNull(slides);
        ArgumentNullException.ThrowIfNull(slide);

        var index = -1;
        for (var i = 0; i < slides.Count; i++)
            if (ReferenceEquals(slides[i], slide)) { index = i; break; }

        if (index < 0) return null;

        for (var i = index; i >= 0; i--)
            if (slides[i].SectionName is not null) return slides[i];

        return null;
    }

    public IReadOnlyList<string?> MarkersAfterMove(IReadOnlyList<Slide> slides, int from, int to)
    {
        ArgumentNullException.ThrowIfNull(slides);
        var markers = slides.Select(slide => slide.SectionName).ToList();
        if (from < 0 || from >= markers.Count) return markers;

        var moved = markers[from];
        markers.RemoveAt(from);
        if (moved is not null && from < markers.Count && markers[from] is null) markers[from] = moved;

        var target = Math.Clamp(to, 0, markers.Count);
        markers.Insert(target, null);
        if (target == 0 && markers.Count > 1 && markers[1] is { } first)
        {
            markers[0] = first;
            markers[1] = null;
        }

        return markers;
    }
}
