namespace PulseOffice.Sheets;

internal sealed class OccupiedIndices
{
    private readonly Dictionary<int, int> _counts = [];
    private readonly SortedSet<int> _indices = [];

    public int Last => _indices.Count == 0 ? -1 : _indices.Max;

    public void Add(int index)
    {
        var count = _counts.GetValueOrDefault(index);
        _counts[index] = count + 1;
        if (count == 0) _indices.Add(index);
    }

    public void Remove(int index)
    {
        if (!_counts.TryGetValue(index, out var count)) return;
        if (count > 1)
        {
            _counts[index] = count - 1;
            return;
        }
        _counts.Remove(index);
        _indices.Remove(index);
    }

    public void CopyTo(OccupiedIndices target)
    {
        target._counts.Clear();
        foreach (var (index, count) in _counts) target._counts[index] = count;
        target._indices.Clear();
        target._indices.UnionWith(_indices);
    }

    public void Clear()
    {
        _counts.Clear();
        _indices.Clear();
    }
}
