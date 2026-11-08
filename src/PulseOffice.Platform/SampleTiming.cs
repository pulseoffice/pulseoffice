namespace PulseOffice.Platform.Mac;

public sealed class SampleTiming
{
    public IReadOnlyList<long>? Durations(IReadOnlyList<long> presentIndices, IReadOnlyList<long> frameDurations)
    {
        ArgumentNullException.ThrowIfNull(presentIndices);
        ArgumentNullException.ThrowIfNull(frameDurations);

        if (presentIndices.Count == 0 || presentIndices[0] != 0) return null;
        if (presentIndices.Any(index => index < 0 || index >= frameDurations.Count)) return null;

        var result = new List<long>(presentIndices.Count);

        for (var i = 0; i < presentIndices.Count; i++)
        {
            var from = presentIndices[i];
            var to = i + 1 < presentIndices.Count ? presentIndices[i + 1] : frameDurations.Count;
            if (to <= from) return null;

            long total = 0;
            for (var frame = from; frame < to; frame++) total += frameDurations[(int)frame];
            result.Add(total);
        }

        return result;
    }
}
