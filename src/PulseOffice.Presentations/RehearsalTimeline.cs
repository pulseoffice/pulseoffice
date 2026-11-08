namespace PulseOffice.Presentations;

public sealed class RehearsalTimeline
{
    private readonly Dictionary<string, int> _times = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> Times => _times;

    public int TotalMs => _times.Values.Sum();

    public bool HasTimings => _times.Count > 0;

    public void Record(string slideId, int elapsedMs)
    {
        ArgumentNullException.ThrowIfNull(slideId);
        _times[slideId] = Math.Max(0, elapsedMs);
    }
}
