namespace PulseOffice.Typography.MicroType;

internal sealed record CtfTableEntry(string Tag, int Offset, int Length);

internal sealed record SfntTable(string Tag, byte[] Data);

internal sealed class CtfFont
{
    private readonly byte[] _tables;
    private readonly Dictionary<string, CtfTableEntry> _byTag = new(StringComparer.Ordinal);
    private int[]? _advances;

    public CtfFont(MtxBlocks blocks, MtxLimits limits)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(limits);
        _tables = blocks.Tables;
        Limits = limits;
        PushData = new CtfCursor(blocks.PushData);
        Instructions = new CtfCursor(blocks.Instructions);

        var directory = new CtfCursor(_tables);
        ScalarType = directory.ReadUInt32();
        var count = directory.ReadUInt16();
        directory.ReadBytes(6);
        if (count > limits.MaxTables) throw new InvalidDataException("Compact font declares too many tables.");

        var entries = new List<CtfTableEntry>(count);
        for (var index = 0; index < count; index++)
        {
            var tag = new string([.. directory.ReadBytes(4).ToArray().Select(value => (char)value)]);
            directory.ReadUInt32();
            var offset = directory.ReadUInt32();
            var length = directory.ReadUInt32();
            if (offset > (uint)_tables.Length || length > (uint)_tables.Length - offset)
                throw new InvalidDataException($"Compact font table '{tag}' points outside the font data.");
            if (!_byTag.TryAdd(tag, new CtfTableEntry(tag, (int)offset, (int)length)))
                throw new InvalidDataException($"Compact font declares table '{tag}' twice.");
            entries.Add(_byTag[tag]);
        }

        Entries = entries;
    }

    public MtxLimits Limits { get; }

    public uint ScalarType { get; }

    public IReadOnlyList<CtfTableEntry> Entries { get; }

    public CtfCursor PushData { get; }

    public CtfCursor Instructions { get; }

    public bool LongLoca { get; set; }

    public CtfCursor Table(CtfTableEntry entry) => new(_tables, entry.Offset, entry.Length);

    public CtfCursor TableToEnd(CtfTableEntry entry) => new(_tables, entry.Offset, _tables.Length - entry.Offset);

    public byte[] Bytes(CtfTableEntry entry) => _tables.AsSpan(entry.Offset, entry.Length).ToArray();

    public CtfCursor Required(string tag) =>
        _byTag.TryGetValue(tag, out var entry) ? Table(entry) : throw new InvalidDataException($"Compact font has no '{tag}' table.");

    public int GlyphCount
    {
        get
        {
            var maxp = Required("maxp");
            maxp.Seek(4);
            var count = maxp.ReadUInt16();
            if (count > Limits.MaxGlyphs) throw new InvalidDataException("Compact font declares too many glyphs.");
            return count;
        }
    }

    public int UnitsPerEm
    {
        get
        {
            var head = Required("head");
            head.Seek(18);
            var units = head.ReadUInt16();
            return units == 0 ? throw new InvalidDataException("Compact font has zero units per em.") : units;
        }
    }

    public bool ShortLoca
    {
        get
        {
            var head = Required("head");
            head.Seek(50);
            return head.ReadInt16() == 0;
        }
    }

    public int AdvanceWidth(int glyph)
    {
        _advances ??= ReadAdvances();
        if (_advances.Length == 0) return 0;
        return _advances[Math.Min(glyph, _advances.Length - 1)];
    }

    private int[] ReadAdvances()
    {
        var hhea = Required("hhea");
        hhea.Seek(34);
        var metrics = hhea.ReadUInt16();
        var hmtx = Required("hmtx");
        var advances = new int[Math.Min(metrics, hmtx.Remaining / 4)];
        for (var index = 0; index < advances.Length; index++)
        {
            advances[index] = hmtx.ReadUInt16();
            hmtx.ReadInt16();
        }

        return advances;
    }
}
