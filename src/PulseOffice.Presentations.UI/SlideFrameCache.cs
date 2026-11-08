using PulseOffice.Presentations;

namespace PulseOffice.Presentations.UI;

public sealed class SlideFrameCache<TFrame> where TFrame : class
{
    private sealed record Entry(TFrame Frame, SlideThumbnailStamp Stamp);

    private readonly Dictionary<Slide, Entry> _entries = new(ReferenceEqualityComparer.Instance);

    public int Rebuilt { get; private set; }

    public IReadOnlyList<TFrame> Refresh(Presentation deck, Slide? current, Func<Slide, int, TFrame> build)
    {
        ArgumentNullException.ThrowIfNull(deck);
        ArgumentNullException.ThrowIfNull(build);

        Rebuilt = 0;
        var frames = new List<TFrame>(deck.Slides.Count);
        var alive = new HashSet<Slide>(ReferenceEqualityComparer.Instance);
        for (var index = 0; index < deck.Slides.Count; index++)
        {
            var slide = deck.Slides[index];
            alive.Add(slide);
            var stamp = SlideThumbnailStamp.Of(deck, slide, ReferenceEquals(slide, current));
            if (!_entries.TryGetValue(slide, out var entry) || !entry.Stamp.Equals(stamp))
            {
                entry = new Entry(build(slide, index), stamp);
                _entries[slide] = entry;
                Rebuilt++;
            }
            frames.Add(entry.Frame);
        }

        foreach (var gone in _entries.Keys.Where(slide => !alive.Contains(slide)).ToList()) _entries.Remove(gone);
        return frames;
    }

    public void Clear() => _entries.Clear();
}
