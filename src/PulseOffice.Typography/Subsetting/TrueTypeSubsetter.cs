using System.Buffers.Binary;
using PulseOffice.Typography.OpenType.Metrics;

namespace PulseOffice.Typography.Subsetting;

public sealed record FontSubset(byte[] Data, IReadOnlyList<int> Glyphs);

public interface ITrueTypeSubsetter
{
    FontSubset Subset(FontTableData font, IReadOnlyList<int> glyphs);
}

public sealed class TrueTypeSubsetter : ITrueTypeSubsetter
{
    private const ushort MoreComponents = 0x0020;
    private const ushort ArgumentsAreWords = 0x0001;
    private const ushort HasScale = 0x0008;
    private const ushort HasXYScale = 0x0040;
    private const ushort HasTwoByTwo = 0x0080;
    private const uint ChecksumMagic = 0xB1B0AFBA;
    private const int MinimumOutlineBudget = 64 * 1024;

    private static readonly string[] CopiedTables = ["cvt ", "fpgm", "prep", "OS/2", "gasp"];

    public FontSubset Subset(FontTableData font, IReadOnlyList<int> glyphs)
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(glyphs);
        if (!font.Has("glyf") || !font.Has("loca") || !font.Has("head") || !font.Has("hhea") || !font.Has("maxp") || !font.Has("hmtx"))
            throw new InvalidDataException("The font has no TrueType outlines to subset.");

        var source = new GlyphSource(font);
        var order = new List<int> { 0 };
        var index = new Dictionary<int, int> { [0] = 0 };
        foreach (var glyph in glyphs)
        {
            if (glyph < 0 || glyph >= source.Count)
            {
                order.Add(0);
                continue;
            }
            if (index.ContainsKey(glyph)) continue;
            index[glyph] = order.Count;
            order.Add(glyph);
        }

        var outlines = new List<byte[]>();
        var limit = (2L * font.Length("glyf")) + MinimumOutlineBudget;
        long total = 0;
        for (var position = 0; position < order.Count; position++)
        {
            var outline = source.Outline(order[position]);
            total += outline.Length;
            if (total > limit) throw new InvalidDataException("The font's glyph locations overlap: the subset would be larger than the font.");
            foreach (var (offset, component) in Components(outline))
            {
                if (component >= source.Count) continue;
                if (!index.TryGetValue(component, out var renumbered))
                {
                    renumbered = order.Count;
                    index[component] = renumbered;
                    order.Add(component);
                }
                BinaryPrimitives.WriteUInt16BigEndian(outline.AsSpan(offset), (ushort)renumbered);
            }
            outlines.Add(outline);
        }

        var tables = new SortedDictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["glyf"] = Concatenate(outlines, out var offsets),
            ["loca"] = Locations(offsets),
            ["hmtx"] = Metrics(font, order),
            ["hhea"] = Patched(font.Bytes("hhea"), 34, order.Count),
            ["maxp"] = Patched(font.Bytes("maxp"), 4, order.Count),
            ["head"] = Header(font.Bytes("head")),
            ["post"] = Postscript(font),
        };
        foreach (var tag in CopiedTables)
            if (font.Has(tag)) tables[tag] = font.Bytes(tag);

        return new FontSubset(Assemble(tables), order);
    }

    private IEnumerable<(int Offset, int Glyph)> Components(byte[] outline)
    {
        if (outline.Length < 10 || BinaryPrimitives.ReadInt16BigEndian(outline) >= 0) yield break;
        var cursor = 10;
        while (cursor + 4 <= outline.Length)
        {
            var flags = BinaryPrimitives.ReadUInt16BigEndian(outline.AsSpan(cursor));
            yield return (cursor + 2, BinaryPrimitives.ReadUInt16BigEndian(outline.AsSpan(cursor + 2)));
            cursor += 4 + ((flags & ArgumentsAreWords) != 0 ? 4 : 2);
            if ((flags & HasScale) != 0) cursor += 2;
            else if ((flags & HasXYScale) != 0) cursor += 4;
            else if ((flags & HasTwoByTwo) != 0) cursor += 8;
            if ((flags & MoreComponents) == 0) yield break;
        }
    }

    private byte[] Concatenate(List<byte[]> outlines, out List<int> offsets)
    {
        offsets = new List<int>(outlines.Count + 1);
        using var buffer = new MemoryStream();
        foreach (var outline in outlines)
        {
            offsets.Add((int)buffer.Length);
            buffer.Write(outline);
            while (buffer.Length % 4 != 0) buffer.WriteByte(0);
        }
        offsets.Add((int)buffer.Length);
        return buffer.ToArray();
    }

    private byte[] Locations(List<int> offsets)
    {
        var table = new byte[offsets.Count * 4];
        for (var i = 0; i < offsets.Count; i++) BinaryPrimitives.WriteUInt32BigEndian(table.AsSpan(i * 4), (uint)offsets[i]);
        return table;
    }

    private byte[] Metrics(FontTableData font, List<int> order)
    {
        var longMetrics = Math.Max(1, font.U16(font.Offset("hhea") + 34));
        var metrics = font.Offset("hmtx");
        var available = font.Length("hmtx");
        var table = new byte[order.Count * 4];
        for (var i = 0; i < order.Count; i++)
        {
            var glyph = order[i];
            int advance, bearing;
            if (glyph < longMetrics)
            {
                advance = font.U16(metrics + glyph * 4);
                bearing = font.S16(metrics + glyph * 4 + 2);
            }
            else
            {
                advance = font.U16(metrics + (longMetrics - 1) * 4);
                var position = longMetrics * 4 + (glyph - longMetrics) * 2;
                bearing = position + 2 <= available ? font.S16(metrics + position) : 0;
            }
            BinaryPrimitives.WriteUInt16BigEndian(table.AsSpan(i * 4), (ushort)advance);
            BinaryPrimitives.WriteInt16BigEndian(table.AsSpan(i * 4 + 2), (short)bearing);
        }
        return table;
    }

    private byte[] Patched(byte[] table, int offset, int value)
    {
        if (table.Length >= offset + 2) BinaryPrimitives.WriteUInt16BigEndian(table.AsSpan(offset), (ushort)value);
        return table;
    }

    private byte[] Header(byte[] head)
    {
        if (head.Length < 54) throw new InvalidDataException("The font header is truncated.");
        BinaryPrimitives.WriteUInt32BigEndian(head.AsSpan(8), 0);
        BinaryPrimitives.WriteInt16BigEndian(head.AsSpan(50), 1);
        return head;
    }

    private byte[] Postscript(FontTableData font)
    {
        var table = new byte[32];
        var original = font.Bytes("post");
        original.AsSpan(0, Math.Min(32, original.Length)).CopyTo(table);
        BinaryPrimitives.WriteUInt32BigEndian(table, 0x00030000);
        return table;
    }

    private byte[] Assemble(SortedDictionary<string, byte[]> tables)
    {
        var count = tables.Count;
        var header = 12 + count * 16;
        var total = header + tables.Values.Sum(table => (table.Length + 3) & ~3);
        var output = new byte[total];
        BinaryPrimitives.WriteUInt32BigEndian(output, 0x00010000);
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(4), (ushort)count);
        var power = 1;
        var exponent = 0;
        while (power * 2 <= count)
        {
            power *= 2;
            exponent++;
        }
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(6), (ushort)(power * 16));
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(8), (ushort)exponent);
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(10), (ushort)(count * 16 - power * 16));

        var cursor = header;
        var record = 12;
        var headOffset = 0;
        foreach (var (tag, data) in tables)
        {
            for (var letter = 0; letter < 4; letter++) output[record + letter] = (byte)tag[letter];
            BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(record + 4), Checksum(data));
            BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(record + 8), (uint)cursor);
            BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(record + 12), (uint)data.Length);
            if (tag == "head") headOffset = cursor;
            data.CopyTo(output.AsSpan(cursor));
            cursor += (data.Length + 3) & ~3;
            record += 16;
        }

        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(headOffset + 8), unchecked(ChecksumMagic - Checksum(output)));
        return output;
    }

    private uint Checksum(byte[] data)
    {
        uint sum = 0;
        var whole = data.Length & ~3;
        for (var i = 0; i < whole; i += 4) sum = unchecked(sum + BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(i)));
        if (whole < data.Length)
        {
            Span<byte> tail = stackalloc byte[4];
            data.AsSpan(whole).CopyTo(tail);
            sum = unchecked(sum + BinaryPrimitives.ReadUInt32BigEndian(tail));
        }
        return sum;
    }

    private sealed class GlyphSource(FontTableData font)
    {
        private readonly int _loca = font.Offset("loca");
        private readonly int _glyf = font.Offset("glyf");
        private readonly int _glyfLength = font.Length("glyf");
        private readonly bool _long = font.S16(font.Offset("head") + 50) == 1;

        public int Count { get; } = Math.Min(font.U16(font.Offset("maxp") + 4),
            font.Length("loca") / (font.S16(font.Offset("head") + 50) == 1 ? 4 : 2) - 1);

        public byte[] Outline(int glyph)
        {
            var start = Location(glyph);
            var end = Location(glyph + 1);
            if (end <= start || start < 0 || end > _glyfLength) return [];
            return font.Slice(_glyf + start, end - start);
        }

        private int Location(int glyph) =>
            _long ? (int)font.U32(_loca + glyph * 4) : font.U16(_loca + glyph * 2) * 2;
    }
}
