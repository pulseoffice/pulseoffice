namespace PulseOffice.Text.Rich.Bidi;

internal sealed class BidiIsolatingSequence(BidiResolution owner, List<int> indices, List<int> kept)
{
    private const int MaxBracketDepth = 63;

    private readonly BidiClass[] _types = owner.Types;
    private BidiClass _sos;
    private BidiClass _eos;
    private BidiClass _embedding;

    private BidiClass this[int position]
    {
        get => _types[indices[position]];
        set => _types[indices[position]] = value;
    }

    public void Resolve()
    {
        var levels = owner.Embedding;
        var level = levels[indices[0]];
        var first = kept.BinarySearch(indices[0]);
        var last = kept.BinarySearch(indices[^1]);
        var before = first > 0 ? levels[kept[first - 1]] : owner.Paragraph;
        var lastIndex = indices[^1];
        var after = (owner.Initiator(owner.Kinds[lastIndex]) && owner.Unmatched(lastIndex)) || last + 1 >= kept.Count
            ? owner.Paragraph
            : levels[kept[last + 1]];
        _sos = (Math.Max(level, before) & 1) == 1 ? BidiClass.R : BidiClass.L;
        _eos = (Math.Max(level, after) & 1) == 1 ? BidiClass.R : BidiClass.L;
        _embedding = (level & 1) == 1 ? BidiClass.R : BidiClass.L;
        Weak();
        Brackets();
        Neutrals();
        Implicit();
    }

    private void Weak()
    {
        var count = indices.Count;
        for (var k = 0; k < count; k++)
        {
            if (this[k] != BidiClass.NSM) continue;
            this[k] = k == 0 ? _sos : this[k - 1] is BidiClass.LRI or BidiClass.RLI or BidiClass.FSI or BidiClass.PDI ? BidiClass.ON : this[k - 1];
        }
        var strong = _sos;
        for (var k = 0; k < count; k++)
        {
            if (this[k] is BidiClass.L or BidiClass.R or BidiClass.AL) strong = this[k];
            else if (this[k] == BidiClass.EN && strong == BidiClass.AL) this[k] = BidiClass.AN;
        }
        for (var k = 0; k < count; k++)
        {
            if (this[k] == BidiClass.AL) this[k] = BidiClass.R;
        }
        for (var k = 1; k < count - 1; k++)
        {
            var previous = this[k - 1];
            var next = this[k + 1];
            if (this[k] == BidiClass.ES && previous == BidiClass.EN && next == BidiClass.EN) this[k] = BidiClass.EN;
            else if (this[k] == BidiClass.CS && previous == next && previous is BidiClass.EN or BidiClass.AN) this[k] = previous;
        }
        for (var k = 0; k < count; k++)
        {
            if (this[k] != BidiClass.ET) continue;
            var end = k;
            while (end < count && this[end] == BidiClass.ET) end++;
            if ((k > 0 && this[k - 1] == BidiClass.EN) || (end < count && this[end] == BidiClass.EN))
            {
                for (var j = k; j < end; j++) this[j] = BidiClass.EN;
            }
            k = end - 1;
        }
        for (var k = 0; k < count; k++)
        {
            if (this[k] is BidiClass.ES or BidiClass.ET or BidiClass.CS) this[k] = BidiClass.ON;
        }
        strong = _sos;
        for (var k = 0; k < count; k++)
        {
            if (this[k] is BidiClass.L or BidiClass.R) strong = this[k];
            else if (this[k] == BidiClass.EN && strong == BidiClass.L) this[k] = BidiClass.L;
        }
    }

    private BidiClass? StrongOf(BidiClass kind) => kind switch
    {
        BidiClass.L => BidiClass.L,
        BidiClass.R or BidiClass.EN or BidiClass.AN => BidiClass.R,
        _ => null,
    };

    private void Brackets()
    {
        var mirroring = owner.Mirroring;
        var pairs = new List<(int Open, int Close)>();
        var stack = new List<(int Closer, int Position)>();
        for (var k = 0; k < indices.Count; k++)
        {
            if (this[k] != BidiClass.ON) continue;
            var code = owner.Codes[indices[k]];
            var kind = mirroring.Bracket(code);
            if (kind == BracketKind.Opening)
            {
                if (stack.Count == MaxBracketDepth) break;
                stack.Add((mirroring.Canonical(mirroring.Mirror(code)), k));
            }
            else if (kind == BracketKind.Closing)
            {
                var canonical = mirroring.Canonical(code);
                for (var p = stack.Count - 1; p >= 0; p--)
                {
                    if (stack[p].Closer != canonical) continue;
                    pairs.Add((stack[p].Position, k));
                    stack.RemoveRange(p, stack.Count - p);
                    break;
                }
            }
        }
        pairs.Sort((left, right) => left.Open.CompareTo(right.Open));
        var opposite = _embedding == BidiClass.L ? BidiClass.R : BidiClass.L;
        foreach (var (open, close) in pairs)
        {
            var matching = false;
            var contrary = false;
            for (var k = open + 1; k < close; k++)
            {
                var strong = StrongOf(this[k]);
                if (strong == _embedding) matching = true;
                else if (strong == opposite) contrary = true;
            }
            BidiClass resolved;
            if (matching) resolved = _embedding;
            else if (contrary) resolved = Context(open) == opposite ? opposite : _embedding;
            else continue;
            Set(open, resolved);
            Set(close, resolved);
        }
    }

    private BidiClass Context(int open)
    {
        for (var k = open - 1; k >= 0; k--)
        {
            if (StrongOf(this[k]) is { } strong) return strong;
        }
        return _sos;
    }

    private void Set(int position, BidiClass resolved)
    {
        this[position] = resolved;
        for (var k = position + 1; k < indices.Count && owner.Kinds[indices[k]] == BidiClass.NSM; k++) this[k] = resolved;
    }

    private bool Neutral(BidiClass kind) =>
        kind is BidiClass.B or BidiClass.S or BidiClass.WS or BidiClass.ON or BidiClass.LRI or BidiClass.RLI or BidiClass.FSI or BidiClass.PDI;

    private void Neutrals()
    {
        var count = indices.Count;
        for (var k = 0; k < count; k++)
        {
            if (!Neutral(this[k])) continue;
            var end = k;
            while (end < count && Neutral(this[end])) end++;
            var before = k == 0 ? _sos : StrongOf(this[k - 1]) ?? _embedding;
            var after = end == count ? _eos : StrongOf(this[end]) ?? _embedding;
            var resolved = before == after ? before : _embedding;
            for (var j = k; j < end; j++) this[j] = resolved;
            k = end - 1;
        }
    }

    private void Implicit()
    {
        var levels = owner.Levels;
        foreach (var index in indices)
        {
            var level = levels[index];
            var kind = _types[index];
            if ((level & 1) == 0)
            {
                if (kind == BidiClass.R) level += 1;
                else if (kind is BidiClass.AN or BidiClass.EN) level += 2;
            }
            else if (kind is BidiClass.L or BidiClass.EN or BidiClass.AN) level += 1;
            levels[index] = level;
        }
    }
}
