namespace PulseOffice.Presentations;

public sealed class ShowPlaylist
{
    public IReadOnlyList<Slide> Main(Presentation deck)
    {
        ArgumentNullException.ThrowIfNull(deck);
        var setup = deck.Setup;
        var from = setup.FromSlide > 0 ? setup.FromSlide : 1;
        var to = setup.ToSlide > 0 ? setup.ToSlide : deck.Slides.Count;

        return deck.Slides
            .Select((slide, index) => (slide, number: index + 1))
            .Where(entry => !entry.slide.Hidden
                && (!setup.HasRange || (entry.number >= from && entry.number <= to)))
            .Select(entry => entry.slide)
            .ToList();
    }
}
