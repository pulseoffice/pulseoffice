namespace PulseOffice.Sheets;

public readonly record struct StyleBlock(int FirstRow, int LastRow, int FirstColumn, int LastColumn, int StyleId)
{
    public long CellCount => (long)(LastRow - FirstRow + 1) * (LastColumn - FirstColumn + 1);
}

public sealed class StyleBlocks
{
    private readonly List<Strip> _strips = [];

    public long Revision { get; private set; }

    public bool IsEmpty => _strips.Count == 0;

    public int Count => _strips.Sum(strip => strip.Rows.Runs.Count);

    public IEnumerable<StyleBlock> All =>
        _strips.SelectMany(strip => strip.Rows.Runs.Select(run => new StyleBlock(run.First, run.Last, strip.First, strip.Last, run.Value)));

    public IEnumerable<StyleBlock> In(int firstRow, int lastRow, int firstColumn, int lastColumn)
    {
        foreach (var strip in _strips)
        {
            if (strip.Last < firstColumn || strip.First > lastColumn) continue;

            foreach (var run in strip.Rows.RunsIn(firstRow, lastRow))
                yield return new StyleBlock(run.First, run.Last, Math.Max(strip.First, firstColumn), Math.Min(strip.Last, lastColumn), run.Value);
        }
    }

    public IEnumerable<int> RowEdges => _strips.SelectMany(strip => strip.Rows.Runs.SelectMany(run => new[] { run.First, run.Last + 1 }));

    public IEnumerable<int> ColumnEdges => _strips.SelectMany(strip => new[] { strip.First, strip.Last + 1 });

    public int LastRow => _strips.Count == 0 ? -1 : _strips.Max(strip => strip.Rows.Runs.Count == 0 ? -1 : strip.Rows.Runs[^1].Last);

    public bool TouchesRow(int row) => _strips.Any(strip => strip.Rows.ContainsKey(row));

    public IReadOnlyList<IndexRun<int>> RowRuns(int row)
    {
        var result = new List<IndexRun<int>>();
        foreach (var strip in _strips)
        {
            if (!strip.Rows.TryGetValue(row, out var style)) continue;

            if (result.Count > 0 && result[^1].Last + 1 == strip.First && result[^1].Value == style)
                result[^1] = result[^1] with { Last = strip.Last };
            else result.Add(new IndexRun<int>(strip.First, strip.Last, style));
        }

        return result;
    }

    public bool TryGetValue(int row, int column, out int styleId)
    {
        var position = StripAt(column);
        if (position >= 0) return _strips[position].Rows.TryGetValue(row, out styleId);

        styleId = 0;
        return false;
    }

    public void Set(int firstRow, int lastRow, int firstColumn, int lastColumn, int styleId)
    {
        if (lastRow < firstRow || lastColumn < firstColumn) return;

        foreach (var strip in Covering(firstColumn, lastColumn, create: true)) strip.Rows.Set(firstRow, lastRow, styleId);
        Normalize();
    }

    public void Remove(int firstRow, int lastRow, int firstColumn, int lastColumn)
    {
        if (lastRow < firstRow || lastColumn < firstColumn) return;

        foreach (var strip in Covering(firstColumn, lastColumn, create: false)) strip.Rows.Remove(firstRow, lastRow);
        Normalize();
    }

    public void Clear()
    {
        if (_strips.Count == 0) return;

        _strips.Clear();
        Revision++;
    }

    public void InsertRows(int at, int count, int limit)
    {
        if (count <= 0 || _strips.Count == 0) return;

        foreach (var strip in _strips) strip.Rows.Insert(at, count, limit);
        Normalize();
    }

    public void DeleteRows(int at, int count)
    {
        if (count <= 0 || _strips.Count == 0) return;

        foreach (var strip in _strips) strip.Rows.Delete(at, count);
        Normalize();
    }

    public void InsertColumns(int at, int count, int limit)
    {
        if (count <= 0 || _strips.Count == 0) return;

        Split(at);
        foreach (var strip in _strips.Where(strip => strip.First >= at))
        {
            strip.First = (int)Math.Min((long)strip.First + count, int.MaxValue);
            strip.Last = (int)Math.Min((long)strip.Last + count, int.MaxValue);
        }

        _strips.RemoveAll(strip => strip.First > limit);
        foreach (var strip in _strips.Where(strip => strip.Last > limit)) strip.Last = limit;
        Normalize();
    }

    public void DeleteColumns(int at, int count)
    {
        if (count <= 0 || _strips.Count == 0) return;

        var last = (int)Math.Min((long)at + count - 1, int.MaxValue);
        Split(at);
        if (last < int.MaxValue) Split(last + 1);
        _strips.RemoveAll(strip => strip.First >= at && strip.Last <= last);
        foreach (var strip in _strips.Where(strip => strip.First > last))
        {
            strip.First -= count;
            strip.Last -= count;
        }

        Normalize();
    }

    public void CopyRow(int source, IEnumerable<int> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);
        if (_strips.Count == 0) return;

        var positions = targets.ToList();
        foreach (var strip in _strips)
        {
            var present = strip.Rows.TryGetValue(source, out var style);
            foreach (var position in positions)
                if (present) strip.Rows.Set(position, style);
                else strip.Rows.Remove(position);
        }

        Normalize();
    }

    public void CopyColumn(int source, IEnumerable<int> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);
        if (_strips.Count == 0) return;

        var origin = StripAt(source) is var found && found >= 0 ? _strips[found].Rows : null;
        foreach (var position in targets)
        {
            var strip = Covering(position, position, create: origin is not null).FirstOrDefault();
            if (strip is null) continue;

            if (origin is null) strip.Rows.Clear();
            else if (!ReferenceEquals(strip.Rows, origin)) origin.CopyTo(strip.Rows);
        }

        Normalize();
    }

    public void CopyTo(StyleBlocks target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (ReferenceEquals(target, this)) return;

        target._strips.Clear();
        foreach (var strip in _strips) target._strips.Add(strip.Clone());
        target.Revision++;
    }

    public bool SameAs(StyleBlocks other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return _strips.Count == other._strips.Count && _strips.Zip(other._strips).All(pair => pair.First.First == pair.Second.First && pair.First.Last == pair.Second.Last && pair.First.SameAs(pair.Second));
    }

    public void AddTo(ref HashCode hash)
    {
        foreach (var block in All) hash.Add(block);
    }

    private List<Strip> Covering(int first, int last, bool create)
    {
        Split(first);
        if (last < int.MaxValue) Split(last + 1);

        if (create)
        {
            var cursor = first;
            foreach (var strip in _strips.Where(strip => strip.Last >= first && strip.First <= last).ToList())
            {
                if (strip.First > cursor) Insert(new Strip(cursor, strip.First - 1));
                cursor = strip.Last + 1;
            }

            if (cursor <= last) Insert(new Strip(cursor, last));
        }

        return [.. _strips.Where(strip => strip.Last >= first && strip.First <= last)];
    }

    private void Insert(Strip strip)
    {
        var position = _strips.FindIndex(existing => existing.First > strip.First);
        if (position < 0) _strips.Add(strip);
        else _strips.Insert(position, strip);
    }

    private void Split(int at)
    {
        var position = StripAt(at);
        if (position < 0 || _strips[position].First == at) return;

        var strip = _strips[position];
        var right = strip.Clone();
        right.First = at;
        strip.Last = at - 1;
        _strips.Insert(position + 1, right);
    }

    private int StripAt(int column)
    {
        var low = 0;
        var high = _strips.Count;
        while (low < high)
        {
            var middle = low + ((high - low) / 2);
            if (_strips[middle].Last < column) low = middle + 1;
            else high = middle;
        }

        return low < _strips.Count && _strips[low].First <= column ? low : -1;
    }

    private void Normalize()
    {
        _strips.RemoveAll(strip => strip.Rows.Runs.Count == 0);
        for (var position = _strips.Count - 1; position > 0; position--)
        {
            var left = _strips[position - 1];
            var right = _strips[position];
            if ((long)left.Last + 1 != right.First || !left.SameAs(right)) continue;

            left.Last = right.Last;
            _strips.RemoveAt(position);
        }

        Revision++;
    }

    private sealed class Strip(int first, int last)
    {
        public int First { get; set; } = first;

        public int Last { get; set; } = last;

        public RunLengthMap<int> Rows { get; } = new();

        public Strip Clone()
        {
            var copy = new Strip(First, Last);
            Rows.CopyTo(copy.Rows);
            return copy;
        }

        public bool SameAs(Strip other) => Rows.Runs.SequenceEqual(other.Rows.Runs);
    }
}
