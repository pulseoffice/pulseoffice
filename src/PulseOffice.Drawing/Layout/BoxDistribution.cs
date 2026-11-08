namespace PulseOffice.Drawing.Layout;

public readonly record struct DistributedBox(string Id, double Start, double Size);

public interface IBoxDistribution
{
    IReadOnlyList<DistributedBox> EqualGaps(IReadOnlyList<DistributedBox> boxes);
}

public sealed class BoxDistribution : IBoxDistribution
{
    public IReadOnlyList<DistributedBox> EqualGaps(IReadOnlyList<DistributedBox> boxes)
    {
        ArgumentNullException.ThrowIfNull(boxes);
        if (boxes.Count < 3) return boxes;
        if (boxes.Any(box => !double.IsFinite(box.Start) || !double.IsFinite(box.Size))) return boxes;

        var ordered = boxes
            .Select((box, index) => (box, index))
            .OrderBy(entry => entry.box.Start + entry.box.Size / 2)
            .ThenBy(entry => entry.index)
            .Select(entry => entry.box)
            .ToList();

        var start = ordered.Min(box => box.Start);
        var end = ordered.Max(box => box.Start + box.Size);
        var gap = (end - start - ordered.Sum(box => box.Size)) / (ordered.Count - 1);

        var result = new List<DistributedBox>(ordered.Count) { ordered[0] };
        var cursor = ordered[0].Start + ordered[0].Size + gap;
        for (var i = 1; i < ordered.Count - 1; i++)
        {
            result.Add(ordered[i] with { Start = cursor });
            cursor += ordered[i].Size + gap;
        }

        result.Add(ordered[^1]);
        return result;
    }
}
