namespace PulseOffice.Presentations;

public sealed class ShowPlaylist
{
    public IReadOnlyList<Slide> Main(Presentation deck)
    {
        ArgumentNullException.ThrowIfNull(deck);
        var setup = deck.Setup;
        if (setup.CustomShowName is { } name && deck.CustomShows.FirstOrDefault(show => show.Name == name) is { } custom)
            return [.. custom.SlideIds.Select(id => deck.Slides.FirstOrDefault(slide => slide.Id == id)).OfType<Slide>().Where(slide => !slide.Hidden)];

        var from = setup.FromSlide > 0 ? setup.FromSlide : 1;
        var to = setup.ToSlide > 0 ? setup.ToSlide : deck.Slides.Count;

        return deck.Slides
            .Select((slide, index) => (slide, number: index + 1))
            .Where(entry => !entry.slide.Hidden
                && (!setup.HasRange || (entry.number >= from && entry.number <= to)))
            .Select(entry => entry.slide)
            .ToList();
    }

    public int DetourPosition(Presentation deck, IReadOnlyList<Slide> playlist, Slide detour)
    {
        ArgumentNullException.ThrowIfNull(deck);
        ArgumentNullException.ThrowIfNull(playlist);
        ArgumentNullException.ThrowIfNull(detour);
        var order = deck.IndexOf(detour);
        var position = 0;
        for (var index = 0; index < playlist.Count; index++)
            if (deck.IndexOf(playlist[index]) is var placed && placed >= 0 && placed < order) position = index + 1;
        return position;
    }
}
