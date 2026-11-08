using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets;

public sealed class MergedRegions
{
    private const int BandShift = 6;
    private const int TallBandSpan = 64;

    private readonly List<CellRange> _regions = [];
    private readonly Dictionary<int, List<CellRange>> _bands = [];
    private readonly HashSet<CellRange> _tall = [];

    public IReadOnlyList<CellRange> All => _regions;

    public int Count => _regions.Count;

    public event Action<MergedRegions>? Changed;

    public CellRange? At(CellAddress address)
    {
        foreach (var region in _tall)
            if (region.Contains(address))
                return region;

        if (!_bands.TryGetValue(address.Row >> BandShift, out var band)) return null;

        foreach (var region in band)
            if (region.Contains(address))
                return region;

        return null;
    }

    public bool IsAnchor(CellAddress address) => At(address) is { } region && region.Start == address;

    public bool IsCovered(CellAddress address) => At(address) is { } region && region.Start != address;

    public bool Add(CellRange region)
    {
        if (region.IsSingleCell) return false;

        foreach (var existing in Overlapping(region).ToList()) Detach(existing);
        _regions.Add(region);
        if (IsTall(region))
            _tall.Add(region);
        else
            for (var band = region.Start.Row >> BandShift; band <= region.End.Row >> BandShift; band++)
            {
                if (!_bands.TryGetValue(band, out var list)) _bands[band] = list = [];
                list.Add(region);
            }

        Changed?.Invoke(this);
        return true;
    }

    public void AddRange(IEnumerable<CellRange> regions)
    {
        foreach (var region in regions) Add(region);
    }

    public IReadOnlyList<CellRange> RemoveOverlapping(CellRange region)
    {
        var removed = Overlapping(region).ToList();

        foreach (var existing in removed) Detach(existing);
        if (removed.Count > 0) Changed?.Invoke(this);

        return removed;
    }

    public bool Remove(CellRange region)
    {
        if (!Detach(region)) return false;

        Changed?.Invoke(this);
        return true;
    }

    public void Clear()
    {
        if (_regions.Count == 0) return;

        _regions.Clear();
        _bands.Clear();
        _tall.Clear();
        Changed?.Invoke(this);
    }

    public IEnumerable<CellRange> Overlapping(CellRange range)
    {
        var first = range.Start.Row >> BandShift;
        var last = range.End.Row >> BandShift;

        if ((long)(last - first + 1) > _bands.Count)
            return _regions.Where(region => region.IntersectsWith(range));

        var seen = new HashSet<CellRange>();
        var found = _tall.Where(region => region.IntersectsWith(range)).ToList();
        for (var band = first; band <= last; band++)
        {
            if (!_bands.TryGetValue(band, out var list)) continue;

            foreach (var region in list)
                if (region.IntersectsWith(range) && seen.Add(region))
                    found.Add(region);
        }

        return found;
    }

    public CellRange Expand(CellRange selection)
    {
        var grown = selection;

        while (true)
        {
            var next = grown;

            foreach (var region in Overlapping(grown))
                next = new CellRange(
                    new CellAddress(
                        Math.Min(next.Start.Row, region.Start.Row),
                        Math.Min(next.Start.Column, region.Start.Column)),
                    new CellAddress(
                        Math.Max(next.End.Row, region.End.Row),
                        Math.Max(next.End.Column, region.End.Column)));

            if (next.Start == grown.Start && next.End == grown.End) return grown;

            grown = next;
        }
    }

    private static bool IsTall(CellRange region) =>
        (region.End.Row >> BandShift) - (region.Start.Row >> BandShift) >= TallBandSpan;

    private bool Detach(CellRange region)
    {
        if (!_regions.Remove(region)) return false;
        if (_tall.Remove(region)) return true;

        for (var band = region.Start.Row >> BandShift; band <= region.End.Row >> BandShift; band++)
        {
            if (!_bands.TryGetValue(band, out var list)) continue;

            list.Remove(region);
            if (list.Count == 0) _bands.Remove(band);
        }

        return true;
    }
}
