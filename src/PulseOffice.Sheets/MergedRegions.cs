using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets;

public sealed class MergedRegions
{
    private const int ShiftStep = 2;

    private readonly Dictionary<CellRange, long> _order = [];
    private readonly Dictionary<int, TileLevel> _levels = [];
    private List<CellRange>? _all;
    private long _sequence;

    public IReadOnlyList<CellRange> All =>
        _all ??= _order.OrderBy(entry => entry.Value).Select(entry => entry.Key).ToList();

    public int Count => _order.Count;

    public event Action<MergedRegions>? Changed;

    public CellRange? At(CellAddress address)
    {
        foreach (var level in _levels.Values)
            if (level.At(address) is { } region)
                return region;

        return null;
    }

    public bool IsAnchor(CellAddress address) => At(address) is { } region && region.Start == address;

    public bool IsCovered(CellAddress address) => At(address) is { } region && region.Start != address;

    public bool Add(CellRange region)
    {
        if (region.IsSingleCell) return false;

        foreach (var existing in Overlapping(region).ToList()) Detach(existing);
        _order[region] = _sequence++;
        _all = null;
        var key = LevelKey(region);
        if (!_levels.TryGetValue(key, out var level)) _levels[key] = level = new TileLevel(key / 32, key % 32);
        level.Add(region);

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
        if (_order.Count == 0) return;

        _order.Clear();
        _all = null;
        _levels.Clear();
        Changed?.Invoke(this);
    }

    public IEnumerable<CellRange> Overlapping(CellRange range)
    {
        var found = new List<CellRange>();
        var seen = new HashSet<CellRange>();
        foreach (var level in _levels.Values)
            level.Collect(range, seen, found);
        return found.OrderBy(region => _order[region]).ToList();
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

    private static int LevelKey(CellRange region) =>
        Shift(region.Start.Row, region.End.Row) * 32 + Shift(region.Start.Column, region.End.Column);

    private static int Shift(int start, int end)
    {
        var shift = ShiftStep;
        while ((end >> shift) - (start >> shift) > 1) shift += ShiftStep;
        return shift;
    }

    private bool Detach(CellRange region)
    {
        if (!_order.Remove(region)) return false;

        _all = null;
        var key = LevelKey(region);
        if (_levels.TryGetValue(key, out var level))
        {
            level.Remove(region);
            if (level.IsEmpty) _levels.Remove(key);
        }

        return true;
    }

    private sealed class TileLevel(int rowShift, int columnShift)
    {
        private readonly Dictionary<long, List<CellRange>> _tiles = [];

        public bool IsEmpty => _tiles.Count == 0;

        public CellRange? At(CellAddress address)
        {
            if (!_tiles.TryGetValue(Tile(address.Row >> rowShift, address.Column >> columnShift), out var tile)) return null;

            foreach (var region in tile)
                if (region.Contains(address))
                    return region;

            return null;
        }

        public void Add(CellRange region)
        {
            foreach (var key in Tiles(region))
            {
                if (!_tiles.TryGetValue(key, out var list)) _tiles[key] = list = [];
                list.Add(region);
            }
        }

        public void Remove(CellRange region)
        {
            foreach (var key in Tiles(region))
            {
                if (!_tiles.TryGetValue(key, out var list)) continue;

                list.Remove(region);
                if (list.Count == 0) _tiles.Remove(key);
            }
        }

        public void Collect(CellRange range, HashSet<CellRange> seen, List<CellRange> found)
        {
            var rows = (long)(range.End.Row >> rowShift) - (range.Start.Row >> rowShift) + 1;
            var columns = (long)(range.End.Column >> columnShift) - (range.Start.Column >> columnShift) + 1;

            if (rows * columns > _tiles.Count)
            {
                foreach (var list in _tiles.Values)
                    Gather(list, range, seen, found);
                return;
            }

            foreach (var key in Tiles(range))
                if (_tiles.TryGetValue(key, out var list))
                    Gather(list, range, seen, found);
        }

        private IEnumerable<long> Tiles(CellRange region)
        {
            for (var row = region.Start.Row >> rowShift; row <= region.End.Row >> rowShift; row++)
                for (var column = region.Start.Column >> columnShift; column <= region.End.Column >> columnShift; column++)
                    yield return Tile(row, column);
        }

        private static long Tile(int row, int column) => ((long)row << 20) | (uint)column;

        private static void Gather(List<CellRange> list, CellRange range, HashSet<CellRange> seen, List<CellRange> found)
        {
            foreach (var region in list)
                if (region.IntersectsWith(range) && seen.Add(region))
                    found.Add(region);
        }
    }
}
