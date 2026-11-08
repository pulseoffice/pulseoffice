namespace PulseOffice.Presentations;

public sealed class ShowStart
{
    public int PositionFor(IReadOnlyList<Slide> slides, IReadOnlyList<Slide> playlist, int startIndex)
    {
        ArgumentNullException.ThrowIfNull(slides);
        ArgumentNullException.ThrowIfNull(playlist);

        if (startIndex < 0 || startIndex >= slides.Count) return 0;

        for (var index = startIndex; index < slides.Count; index++)
        {
            for (var position = 0; position < playlist.Count; position++)
                if (ReferenceEquals(playlist[position], slides[index])) return position;
        }

        return 0;
    }
}
