using PulseOffice.Foundation.Units;

namespace PulseOffice.Printing;

public readonly record struct BandSlice(int First, int Last)
{
    public int Count => Last - First + 1;
}

public interface IBandMetrics
{
    int First { get; }

    int Last { get; }

    Twip SizeOf(int index);

    bool IsHidden(int index);

    bool BreaksBefore(int index);
}

public sealed class BandPaginator
{
    public IReadOnlyList<BandSlice> Split(IBandMetrics band, Twip available, BandSlice? repeated = null)
    {
        ArgumentNullException.ThrowIfNull(band);

        var slices = new List<BandSlice>();
        if (band.Last < band.First || available.Value <= 0) return slices;

        var titles = repeated is { } range && range.Last >= range.First ? range : (BandSlice?)null;
        var index = band.First;

        while (index <= band.Last)
        {
            while (index <= band.Last && band.IsHidden(index)) index++;
            if (index > band.Last) break;

            var start = index;
            var used = titles is { } title && start > title.First
                ? SizeOf(band, new BandSlice(title.First, Math.Min(title.Last, start - 1)))
                : 0;

            var pendingBreak = false;

            while (index <= band.Last)
            {
                if (band.IsHidden(index))
                {
                    pendingBreak |= index > start && band.BreaksBefore(index);
                    index++;
                    continue;
                }

                if (index > start && (pendingBreak || band.BreaksBefore(index))) break;

                var size = band.SizeOf(index).Value;
                if (index > start && used + size > available.Value) break;

                used += size;
                index++;
            }

            if (index == start) index++;

            slices.Add(new BandSlice(start, Math.Min(index - 1, band.Last)));
        }

        return slices;
    }

    private int SizeOf(IBandMetrics band, BandSlice range)
    {
        var total = 0;

        for (var index = range.First; index <= range.Last; index++)
            if (!band.IsHidden(index))
                total += band.SizeOf(index).Value;

        return total;
    }
}
