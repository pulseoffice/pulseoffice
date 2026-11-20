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

    public ShowPlaylistStart FromCurrent(Presentation deck, IReadOnlyList<Slide> playlist, int current, ShowStart start)
    {
        ArgumentNullException.ThrowIfNull(deck);
        ArgumentNullException.ThrowIfNull(playlist);
        ArgumentNullException.ThrowIfNull(start);
        if (current < 0 || current >= deck.Slides.Count || deck.Slides[current] is not { Hidden: true } hidden
            || playlist.Any(slide => ReferenceEquals(slide, hidden)) || deck.Setup.CustomShowName is not null && deck.CustomShows.Any(show => show.Name == deck.Setup.CustomShowName))
            return new ShowPlaylistStart(playlist, start.PositionFor(deck.Slides, playlist, current));
        var position = DetourPosition(deck, playlist, hidden);
        var extended = playlist.ToList();
        extended.Insert(position, hidden);
        return new ShowPlaylistStart(extended, position);
    }
}

public sealed record ShowPlaylistStart(IReadOnlyList<Slide> Playlist, int Position);
