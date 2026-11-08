using System.Collections.Concurrent;
using System.Numerics;

namespace PulseOffice.Typography.OpenType.Metrics;

public interface IPairKerning
{
    int Adjust(int left, int right);
}

public sealed class NoKerning : IPairKerning
{
    public int Adjust(int left, int right) => 0;
}

public sealed class CachedKerning(IPairKerning inner) : IPairKerning
{
    private readonly ConcurrentDictionary<(int, int), int> _pairs = new();

    public int Adjust(int left, int right) => _pairs.GetOrAdd((left, right), pair => inner.Adjust(pair.Item1, pair.Item2));
}

public interface IPairKerningReader
{
    IPairKerning? Read(FontTableData data);
}

public interface IPairAdjustment
{
    bool TryAdjust(int left, int right, out int value);
}

public sealed class LookupKerning(IReadOnlyList<IReadOnlyList<IPairAdjustment>> lookups) : IPairKerning
{
    public int Adjust(int left, int right)
    {
        var total = 0;
        foreach (var subtables in lookups)
        {
            foreach (var subtable in subtables)
            {
                if (!subtable.TryAdjust(left, right, out var value)) continue;
                total += value;
                break;
            }
        }
        return total;
    }
}

public sealed class ValueFormat(int flags)
{
    private const int XAdvanceFlag = 0x0004;

    public int Size => BitOperations.PopCount((uint)flags & 0xFF) * 2;

    public bool HasXAdvance => (flags & XAdvanceFlag) != 0;

    public int XAdvanceOffset => BitOperations.PopCount((uint)flags & (XAdvanceFlag - 1)) * 2;
}

public sealed class GlyphPairTable(FontTableData data, int offset) : IPairAdjustment
{
    private readonly GlyphCoverage _coverage = new(data, offset + data.U16(offset + 2));
    private readonly ValueFormat _first = new(data.U16(offset + 4));
    private readonly ValueFormat _second = new(data.U16(offset + 6));

    public bool TryAdjust(int left, int right, out int value)
    {
        value = 0;
        var index = _coverage.IndexOf(left);
        if (index < 0 || index >= data.U16(offset + 8)) return false;
        var set = offset + data.U16(offset + 10 + index * 2);
        var count = data.U16(set);
        var record = 2 + _first.Size + _second.Size;
        var low = 0;
        var high = count - 1;
        while (low <= high)
        {
            var middle = (low + high) / 2;
            var entry = set + 2 + middle * record;
            var glyph = data.U16(entry);
            if (glyph == right)
            {
                value = _first.HasXAdvance ? data.S16(entry + 2 + _first.XAdvanceOffset) : 0;
                return true;
            }
            if (glyph < right) low = middle + 1;
            else high = middle - 1;
        }
        return false;
    }
}

public sealed class ClassPairTable(FontTableData data, int offset) : IPairAdjustment
{
    private readonly GlyphCoverage _coverage = new(data, offset + data.U16(offset + 2));
    private readonly ValueFormat _first = new(data.U16(offset + 4));
    private readonly ValueFormat _second = new(data.U16(offset + 6));
    private readonly GlyphClasses _leftClasses = new(data, offset + data.U16(offset + 8));
    private readonly GlyphClasses _rightClasses = new(data, offset + data.U16(offset + 10));

    public bool TryAdjust(int left, int right, out int value)
    {
        value = 0;
        if (_coverage.IndexOf(left) < 0) return false;
        var leftCount = data.U16(offset + 12);
        var rightCount = data.U16(offset + 14);
        var leftClass = _leftClasses.ClassOf(left);
        var rightClass = _rightClasses.ClassOf(right);
        if (leftClass >= leftCount || rightClass >= rightCount) return false;
        var record = _first.Size + _second.Size;
        var entry = offset + 16 + (leftClass * rightCount + rightClass) * record;
        value = _first.HasXAdvance ? data.S16(entry + _first.XAdvanceOffset) : 0;
        return true;
    }
}

public sealed class GposKerningReader : IPairKerningReader
{
    private const int PairLookup = 2;
    private const int ExtensionLookup = 9;
    private const int MaximumLookups = 4096;

    public IPairKerning? Read(FontTableData data)
    {
        if (!data.Has("GPOS")) return null;
        var gpos = data.Offset("GPOS");
        var features = FeatureIndices(data, gpos);
        if (features.Count == 0) return null;
        var featureList = gpos + data.U16(gpos + 6);
        var lookupIndices = new SortedSet<int>();
        foreach (var feature in features)
        {
            var record = featureList + 2 + feature * 6;
            if (data.Tag(record) != "kern") continue;
            var table = featureList + data.U16(record + 4);
            var count = Math.Min(data.U16(table + 2), MaximumLookups);
            for (var i = 0; i < count; i++) lookupIndices.Add(data.U16(table + 4 + i * 2));
        }
        if (lookupIndices.Count == 0) return null;
        var lookupList = gpos + data.U16(gpos + 8);
        var lookupCount = data.U16(lookupList);
        var lookups = new List<IReadOnlyList<IPairAdjustment>>();
        foreach (var index in lookupIndices.Where(index => index < lookupCount))
        {
            var lookup = lookupList + data.U16(lookupList + 2 + index * 2);
            lookups.Add(Subtables(data, lookup));
        }
        return new LookupKerning(lookups);
    }

    private IReadOnlyList<IPairAdjustment> Subtables(FontTableData data, int lookup)
    {
        var type = data.U16(lookup);
        var count = data.U16(lookup + 4);
        var result = new List<IPairAdjustment>();
        for (var i = 0; i < count; i++)
        {
            var subtable = lookup + data.U16(lookup + 6 + i * 2);
            var subtableType = type;
            if (type == ExtensionLookup)
            {
                subtableType = data.U16(subtable + 2);
                subtable += (int)data.U32(subtable + 4);
            }
            if (subtableType != PairLookup) continue;
            var adjustment = data.U16(subtable) switch
            {
                1 => (IPairAdjustment)new GlyphPairTable(data, subtable),
                2 => new ClassPairTable(data, subtable),
                _ => null,
            };
            if (adjustment is not null) result.Add(adjustment);
        }
        return result;
    }

    private IReadOnlyList<int> FeatureIndices(FontTableData data, int gpos)
    {
        var scriptList = gpos + data.U16(gpos + 4);
        var count = data.U16(scriptList);
        var script = -1;
        foreach (var wanted in new[] { "DFLT", "latn" })
        {
            for (var i = 0; i < count && script < 0; i++)
            {
                var record = scriptList + 2 + i * 6;
                if (data.Tag(record) == wanted) script = scriptList + data.U16(record + 4);
            }
        }
        if (script < 0 && count > 0) script = scriptList + data.U16(scriptList + 6);
        if (script < 0) return [];
        var languageOffset = data.U16(script);
        if (languageOffset == 0) return [];
        var language = script + languageOffset;
        var featureCount = data.U16(language + 4);
        var indices = new List<int>(featureCount);
        for (var i = 0; i < featureCount; i++) indices.Add(data.U16(language + 6 + i * 2));
        return indices;
    }
}

public sealed class KernTableReader : IPairKerningReader
{
    private const int HorizontalBit = 0x0001;
    private const int MinimumBit = 0x0002;
    private const int CrossStreamBit = 0x0004;
    private const int OverrideBit = 0x0008;
    private const int PairsHeader = 14;
    private const int PairSize = 6;

    public IPairKerning? Read(FontTableData data)
    {
        if (!data.Has("kern")) return null;
        var kern = data.Offset("kern");
        if (data.U16(kern) != 0) return null;
        var tables = data.U16(kern + 2);
        var pairs = new Dictionary<(int, int), int>();
        var cursor = kern + 4;
        var end = kern + data.Length("kern");
        for (var t = 0; t < tables && cursor + 6 <= end; t++)
        {
            var length = data.U16(cursor + 2);
            var coverage = data.U16(cursor + 4);
            var format = coverage >> 8;
            var size = length;
            if (format == 0)
            {
                var count = data.U16(cursor + 6);
                size = PairsHeader + count * PairSize;
                if ((coverage & HorizontalBit) != 0 && (coverage & (CrossStreamBit | MinimumBit)) == 0)
                {
                    for (var i = 0; i < count; i++)
                    {
                        var entry = cursor + PairsHeader + i * PairSize;
                        if (entry + PairSize > end) break;
                        var key = (data.U16(entry), data.U16(entry + 2));
                        var value = data.S16(entry + 4);
                        pairs[key] = (coverage & OverrideBit) != 0 ? value : pairs.GetValueOrDefault(key) + value;
                    }
                }
            }
            if (size < 6) break;
            cursor += size;
        }
        return pairs.Count == 0 ? null : new TablePairKerning(pairs);
    }
}

public sealed class TablePairKerning(IReadOnlyDictionary<(int, int), int> pairs) : IPairKerning
{
    public int Adjust(int left, int right) => pairs.GetValueOrDefault((left, right));
}
