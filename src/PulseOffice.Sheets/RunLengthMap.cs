using System.Collections;

namespace PulseOffice.Sheets;

public readonly record struct IndexRun<T>(int First, int Last, T Value)
{
    public int Length => Last - First + 1;
}

public readonly record struct IndexSegment<T>(int First, int Last, bool Present, T Value);

public sealed class RunLengthMap<T> : IReadOnlyDictionary<int, T> where T : IEquatable<T>
{
    private readonly List<IndexRun<T>> _runs = [];

    public long Revision { get; private set; }

    public IReadOnlyList<IndexRun<T>> Runs => _runs;

    public int Count
    {
        get
        {
            var count = 0;
            foreach (var run in _runs) count += run.Length;
            return count;
        }
    }

    public T this[int key] => TryGetValue(key, out var value) ? value : throw new KeyNotFoundException(key.ToString(System.Globalization.CultureInfo.InvariantCulture));

    public IEnumerable<int> Keys => _runs.SelectMany(run => Enumerable.Range(run.First, run.Length));

    public IEnumerable<T> Values => this.Select(pair => pair.Value);

    public bool ContainsKey(int key) => Find(key) >= 0;

    public bool TryGetValue(int key, out T value)
    {
        var position = Find(key);
        if (position < 0)
        {
            value = default!;
            return false;
        }

        value = _runs[position].Value;
        return true;
    }

    public IEnumerator<KeyValuePair<int, T>> GetEnumerator()
    {
        foreach (var run in _runs)
            for (var index = run.First; index <= run.Last; index++)
                yield return new KeyValuePair<int, T>(index, run.Value);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public IEnumerable<IndexRun<T>> RunsIn(int first, int last)
    {
        for (var position = FirstEndingAtOrAfter(first); position < _runs.Count && _runs[position].First <= last; position++)
        {
            var run = _runs[position];
            yield return run with { First = Math.Max(run.First, first), Last = Math.Min(run.Last, last) };
        }
    }

    public IEnumerable<IndexSegment<T>> Segments(int first, int last)
    {
        var cursor = first;
        foreach (var run in RunsIn(first, last).ToList())
        {
            if (run.First > cursor) yield return new IndexSegment<T>(cursor, run.First - 1, false, default!);
            yield return new IndexSegment<T>(run.First, run.Last, true, run.Value);
            cursor = run.Last + 1;
        }

        if (cursor <= last) yield return new IndexSegment<T>(cursor, last, false, default!);
    }

    public void Set(int index, T value) => Set(index, index, value);

    public void Set(int first, int last, T value)
    {
        if (last < first) return;

        Cut(first, last);
        var position = FirstEndingAtOrAfter(first);
        _runs.Insert(position, new IndexRun<T>(first, last, value));
        Coalesce(position);
        Revision++;
    }

    public bool Remove(int index) => Remove(index, index);

    public bool Remove(int first, int last)
    {
        if (last < first || !Cut(first, last)) return false;

        Revision++;
        return true;
    }

    public void Clear()
    {
        if (_runs.Count == 0) return;

        _runs.Clear();
        Revision++;
    }

    public void Insert(int at, int count, int limit)
    {
        if (count <= 0) return;

        var position = FirstEndingAtOrAfter(at);
        if (position < _runs.Count && _runs[position].First < at)
        {
            var straddling = _runs[position];
            _runs[position] = straddling with { Last = at - 1 };
            _runs.Insert(position + 1, straddling with { First = at, Last = straddling.Last });
            position++;
        }

        for (var index = position; index < _runs.Count; index++)
        {
            var run = _runs[index];
            _runs[index] = run with { First = (int)Math.Min((long)run.First + count, int.MaxValue), Last = (int)Math.Min((long)run.Last + count, int.MaxValue) };
        }

        Cut(limit + 1, int.MaxValue);
        Revision++;
    }

    public void Delete(int at, int count)
    {
        if (count <= 0) return;

        var last = (int)Math.Min((long)at + count - 1, int.MaxValue);
        Cut(at, last);
        var position = FirstEndingAtOrAfter(at);
        for (var index = position; index < _runs.Count; index++)
        {
            var run = _runs[index];
            _runs[index] = run with { First = run.First - count, Last = run.Last - count };
        }

        if (position > 0) Coalesce(position - 1);
        Revision++;
    }

    public void CopyTo(RunLengthMap<T> target)
    {
        ArgumentNullException.ThrowIfNull(target);

        target._runs.Clear();
        target._runs.AddRange(_runs);
        target.Revision++;
    }

    private bool Cut(int first, int last)
    {
        var position = FirstEndingAtOrAfter(first);
        var changed = false;

        while (position < _runs.Count && _runs[position].First <= last)
        {
            var run = _runs[position];
            changed = true;

            if (run.First < first && run.Last > last)
            {
                _runs[position] = run with { Last = first - 1 };
                _runs.Insert(position + 1, run with { First = last + 1 });
                return true;
            }

            if (run.First < first)
            {
                _runs[position] = run with { Last = first - 1 };
                position++;
            }
            else if (run.Last > last)
            {
                _runs[position] = run with { First = last + 1 };
                return true;
            }
            else
            {
                var end = position;
                while (end < _runs.Count && _runs[end].Last <= last) end++;
                _runs.RemoveRange(position, end - position);
            }
        }

        return changed;
    }

    private void Coalesce(int position)
    {
        if (position + 1 < _runs.Count && Joins(_runs[position], _runs[position + 1]))
        {
            _runs[position] = _runs[position] with { Last = _runs[position + 1].Last };
            _runs.RemoveAt(position + 1);
        }

        if (position > 0 && Joins(_runs[position - 1], _runs[position]))
        {
            _runs[position - 1] = _runs[position - 1] with { Last = _runs[position].Last };
            _runs.RemoveAt(position);
        }
    }

    private bool Joins(IndexRun<T> left, IndexRun<T> right) =>
        (long)left.Last + 1 == right.First && left.Value.Equals(right.Value);

    private int Find(int key)
    {
        var position = FirstEndingAtOrAfter(key);
        return position < _runs.Count && _runs[position].First <= key ? position : -1;
    }

    private int FirstEndingAtOrAfter(int index)
    {
        var low = 0;
        var high = _runs.Count;
        while (low < high)
        {
            var middle = low + ((high - low) / 2);
            if (_runs[middle].Last < index) low = middle + 1;
            else high = middle;
        }

        return low;
    }
}

public sealed class RunLengthSet : IReadOnlyCollection<int>
{
    private readonly RunLengthMap<bool> _map = new();

    public long Revision => _map.Revision;

    public IReadOnlyList<IndexRun<bool>> Runs => _map.Runs;

    public int Count => _map.Count;

    public bool Contains(int index) => _map.ContainsKey(index);

    public void Add(int index) => _map.Set(index, true);

    public void Add(int first, int last) => _map.Set(first, last, true);

    public bool Remove(int index) => _map.Remove(index);

    public bool Remove(int first, int last) => _map.Remove(first, last);

    public void Clear() => _map.Clear();

    public void Insert(int at, int count, int limit) => _map.Insert(at, count, limit);

    public void Delete(int at, int count) => _map.Delete(at, count);

    public void CopyTo(RunLengthSet target)
    {
        ArgumentNullException.ThrowIfNull(target);
        _map.CopyTo(target._map);
    }

    public IEnumerator<int> GetEnumerator() => _map.Keys.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
