namespace PulseOffice.Text.Rich.Bidi;

internal sealed class BidiResolution(BidiClass[] kinds, int[] codes, TextDirection? direction, IBidiMirroring mirroring)
{
    private const int MaxDepth = 125;

    private readonly BidiClass[] _types = (BidiClass[])kinds.Clone();
    private readonly byte[] _levels = new byte[kinds.Length];
    private readonly int[] _matchingPdi = new int[kinds.Length];
    private readonly bool[] _matchedPdi = new bool[kinds.Length];
    private byte _paragraph;

    public BidiClass[] Kinds => kinds;

    public int[] Codes => codes;

    public IBidiMirroring Mirroring => mirroring;

    public BidiClass[] Types => _types;

    public byte[] Levels => _levels;

    public byte Paragraph => _paragraph;

    public byte[] Embedding { get; private set; } = [];

    public byte[] Resolve(out byte baseLevel)
    {
        MatchIsolates();
        _paragraph = direction switch
        {
            TextDirection.LeftToRight => 0,
            TextDirection.RightToLeft => 1,
            _ => FirstStrong(0, kinds.Length, false) is BidiClass.R or BidiClass.AL ? (byte)1 : (byte)0,
        };
        baseLevel = _paragraph;
        Explicit();
        Embedding = (byte[])_levels.Clone();
        var kept = new List<int>(kinds.Length);
        for (var i = 0; i < kinds.Length; i++)
        {
            if (!Removed(kinds[i])) kept.Add(i);
        }
        foreach (var sequence in Sequences(kept)) new BidiIsolatingSequence(this, sequence, kept).Resolve();
        FillRemoved();
        return _levels;
    }

    public bool Initiator(BidiClass kind) => kind is BidiClass.LRI or BidiClass.RLI or BidiClass.FSI;

    public bool Unmatched(int index) => _matchingPdi[index] < 0;

    private bool Removed(BidiClass kind) => kind is BidiClass.RLE or BidiClass.LRE or BidiClass.RLO or BidiClass.LRO or BidiClass.PDF or BidiClass.BN;

    private void MatchIsolates()
    {
        Array.Fill(_matchingPdi, -1);
        var open = new Stack<int>();
        for (var i = 0; i < kinds.Length; i++)
        {
            if (Initiator(kinds[i])) open.Push(i);
            else if (kinds[i] == BidiClass.PDI && open.Count > 0)
            {
                _matchingPdi[open.Pop()] = i;
                _matchedPdi[i] = true;
            }
            else if (kinds[i] == BidiClass.B) open.Clear();
        }
    }

    private BidiClass? FirstStrong(int from, int to, bool isolate)
    {
        var depth = 0;
        for (var i = from; i < to; i++)
        {
            var kind = kinds[i];
            if (Initiator(kind)) depth++;
            else if (kind == BidiClass.PDI)
            {
                if (depth > 0) depth--;
                else if (isolate) return null;
            }
            else if (kind == BidiClass.B) return null;
            else if (depth == 0 && kind is BidiClass.L or BidiClass.R or BidiClass.AL) return kind;
        }
        return null;
    }

    private readonly record struct Status(byte Level, BidiClass Override, bool Isolate);

    private void Explicit()
    {
        var stack = new List<Status> { new(_paragraph, BidiClass.ON, false) };
        var overflowIsolate = 0;
        var overflowEmbedding = 0;
        var validIsolate = 0;
        for (var i = 0; i < kinds.Length; i++)
        {
            var kind = kinds[i];
            var top = stack[^1];
            switch (kind)
            {
                case BidiClass.RLE or BidiClass.LRE or BidiClass.RLO or BidiClass.LRO:
                {
                    _levels[i] = top.Level;
                    var next = kind is BidiClass.RLE or BidiClass.RLO ? Odd(top.Level) : Even(top.Level);
                    if (next <= MaxDepth && overflowIsolate == 0 && overflowEmbedding == 0)
                        stack.Add(new Status(next, kind == BidiClass.RLO ? BidiClass.R : kind == BidiClass.LRO ? BidiClass.L : BidiClass.ON, false));
                    else if (overflowIsolate == 0) overflowEmbedding++;
                    break;
                }
                case BidiClass.RLI or BidiClass.LRI or BidiClass.FSI:
                {
                    _levels[i] = top.Level;
                    if (top.Override != BidiClass.ON) _types[i] = top.Override;
                    var end = _matchingPdi[i] >= 0 ? _matchingPdi[i] : kinds.Length;
                    var rtl = kind == BidiClass.RLI || (kind == BidiClass.FSI && FirstStrong(i + 1, end, true) is BidiClass.R or BidiClass.AL);
                    var next = rtl ? Odd(top.Level) : Even(top.Level);
                    if (next <= MaxDepth && overflowIsolate == 0 && overflowEmbedding == 0)
                    {
                        validIsolate++;
                        stack.Add(new Status(next, BidiClass.ON, true));
                    }
                    else overflowIsolate++;
                    break;
                }
                case BidiClass.PDI:
                {
                    if (overflowIsolate > 0) overflowIsolate--;
                    else if (validIsolate > 0)
                    {
                        overflowEmbedding = 0;
                        while (!stack[^1].Isolate) stack.RemoveAt(stack.Count - 1);
                        stack.RemoveAt(stack.Count - 1);
                        validIsolate--;
                    }
                    top = stack[^1];
                    _levels[i] = top.Level;
                    if (top.Override != BidiClass.ON) _types[i] = top.Override;
                    break;
                }
                case BidiClass.PDF:
                    _levels[i] = top.Level;
                    if (overflowIsolate > 0) break;
                    if (overflowEmbedding > 0) overflowEmbedding--;
                    else if (!top.Isolate && stack.Count >= 2) stack.RemoveAt(stack.Count - 1);
                    break;
                case BidiClass.B:
                    _levels[i] = _paragraph;
                    break;
                case BidiClass.BN:
                    _levels[i] = top.Level;
                    break;
                default:
                    _levels[i] = top.Level;
                    if (top.Override != BidiClass.ON) _types[i] = top.Override;
                    break;
            }
        }
    }

    private byte Odd(byte level) => (byte)((level + 1) | 1);

    private byte Even(byte level) => (byte)((level + 2) & ~1);

    private IEnumerable<List<int>> Sequences(List<int> kept)
    {
        var runs = new List<List<int>>();
        foreach (var index in kept)
        {
            if (runs.Count == 0 || _levels[runs[^1][^1]] != _levels[index]) runs.Add([]);
            runs[^1].Add(index);
        }
        var startingAt = new Dictionary<int, int>();
        for (var r = 0; r < runs.Count; r++) startingAt[runs[r][0]] = r;
        var visited = new bool[runs.Count];
        for (var r = 0; r < runs.Count; r++)
        {
            if (visited[r] || (kinds[runs[r][0]] == BidiClass.PDI && _matchedPdi[runs[r][0]])) continue;
            var sequence = new List<int>();
            var current = r;
            while (true)
            {
                visited[current] = true;
                sequence.AddRange(runs[current]);
                var last = runs[current][^1];
                if (!Initiator(kinds[last]) || _matchingPdi[last] < 0 || !startingAt.TryGetValue(_matchingPdi[last], out var next) || visited[next]) break;
                current = next;
            }
            yield return sequence;
        }
        for (var r = 0; r < runs.Count; r++)
        {
            if (!visited[r]) yield return runs[r];
        }
    }

    private void FillRemoved()
    {
        var previous = _paragraph;
        for (var i = 0; i < kinds.Length; i++)
        {
            if (Removed(kinds[i])) _levels[i] = previous;
            else previous = _levels[i];
        }
    }
}
