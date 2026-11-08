namespace PulseOffice.Typography.OpenType.Metrics;

public sealed class FontMetricsFace
{
    private readonly FontTableData _tables;
    private readonly int[] _advances;
    private readonly IReadOnlyDictionary<int, int> _glyphs;
    private readonly IPairKerning _kerning;

    public FontMetricsFace(FontTableData tables, OpenTypeFace face, IReadOnlyDictionary<int, int> glyphs, IPairKerning kerning)
    {
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(face);
        _tables = tables;
        _glyphs = glyphs;
        _kerning = kerning;
        Face = face;
        var head = tables.Offset("head");
        UnitsPerEm = Math.Max(16, tables.U16(head + 18));
        var hhea = tables.Offset("hhea");
        HheaAscent = tables.S16(hhea + 4);
        HheaDescent = -tables.S16(hhea + 6);
        HheaLineGap = tables.S16(hhea + 8);
        if (tables.Has("OS/2"))
        {
            var os2 = tables.Offset("OS/2");
            WinAscent = tables.U16(os2 + 74);
            WinDescent = tables.U16(os2 + 76);
        }
        else
        {
            WinAscent = HheaAscent;
            WinDescent = HheaDescent;
        }
        _advances = ReadAdvances(tables.U16(hhea + 34));
    }

    public OpenTypeFace Face { get; }

    public FontTableData Tables => _tables;

    public bool HasTrueTypeOutlines => _tables.Has("glyf") && _tables.Has("loca");

    public bool HasCompactOutlines => _tables.Has("CFF ") || _tables.Has("CFF2");

    public int GlyphCount => _tables.Has("maxp") ? _tables.U16(_tables.Offset("maxp") + 4) : _advances.Length;

    public (int XMin, int YMin, int XMax, int YMax) Box
    {
        get
        {
            var head = _tables.Offset("head");
            return (_tables.S16(head + 36), _tables.S16(head + 38), _tables.S16(head + 40), _tables.S16(head + 42));
        }
    }

    public double ItalicAngle =>
        _tables.Has("post") ? (int)_tables.U32(_tables.Offset("post") + 4) / 65536.0 : 0;

    public int CapHeight
    {
        get
        {
            if (!_tables.Has("OS/2")) return (int)(HheaAscent * 0.7);
            var os2 = _tables.Offset("OS/2");
            return _tables.U16(os2) >= 2 && _tables.Length("OS/2") >= 90 ? _tables.S16(os2 + 88) : (int)(HheaAscent * 0.7);
        }
    }

    public IReadOnlyDictionary<int, int> CharacterMap => _glyphs;

    public string Family => Face.Family;

    public bool Bold => Face.Weight >= 600;

    public bool Italic => Face.Italic;

    public int UnitsPerEm { get; }

    public int HheaAscent { get; }

    public int HheaDescent { get; }

    public int HheaLineGap { get; }

    public int WinAscent { get; }

    public int WinDescent { get; }

    public int GlyphOf(int codePoint) => _glyphs.GetValueOrDefault(codePoint);

    public int AdvanceOfGlyph(int glyph) => _advances.Length == 0 ? 0 : _advances[Math.Clamp(glyph, 0, _advances.Length - 1)];

    public int AdvanceOf(int codePoint) => AdvanceOfGlyph(GlyphOf(codePoint));

    public int Kerning(int leftGlyph, int rightGlyph) => _kerning.Adjust(leftGlyph, rightGlyph);

    private int[] ReadAdvances(int count)
    {
        if (!_tables.Has("hmtx")) return [];
        var hmtx = _tables.Offset("hmtx");
        var available = Math.Min(count, _tables.Length("hmtx") / 4);
        var advances = new int[available];
        for (var i = 0; i < available; i++) advances[i] = _tables.U16(hmtx + i * 4);
        return advances;
    }
}

public interface ICharacterMapReader
{
    IReadOnlyDictionary<int, int> Read(FontTableData tables);
}

public sealed class CharacterMapReader : ICharacterMapReader
{
    private const int MaximumGroups = 100_000;
    private const int MaximumMappings = 0x110000;
    private const int SymbolArea = 0xF000;
    private const int BasicPlaneSize = 0x10000;

    public IReadOnlyDictionary<int, int> Read(FontTableData tables)
    {
        var glyphs = new Dictionary<int, int>();
        if (!tables.Has("cmap")) return glyphs;
        var cmap = tables.Offset("cmap");
        var count = tables.U16(cmap + 2);
        var best = -1;
        var bestRank = -1;
        for (var i = 0; i < count; i++)
        {
            var record = cmap + 4 + i * 8;
            var platform = tables.U16(record);
            var encoding = tables.U16(record + 2);
            var offset = cmap + (int)tables.U32(record + 4);
            var format = tables.U16(offset);
            var rank = (platform, encoding, format) switch
            {
                (3, 10, 12) or (0, _, 12) => 3,
                (3, 1, 4) => 2,
                (0, _, 4) => 1,
                (3, 0, 4) => 0,
                _ => -1,
            };
            if (rank <= bestRank) continue;
            bestRank = rank;
            best = offset;
        }
        if (best < 0) return glyphs;
        if (tables.U16(best) == 12) ReadSegmentedCoverage(tables, best, glyphs);
        else ReadSegmentMapping(tables, best, glyphs);
        if (bestRank == 0) AddSymbolAliases(glyphs);
        return glyphs;
    }

    private void AddSymbolAliases(Dictionary<int, int> glyphs)
    {
        for (var code = 0x20; code <= 0xFF; code++)
            if (!glyphs.ContainsKey(code) && glyphs.TryGetValue(SymbolArea + code, out var glyph)) glyphs[code] = glyph;
    }

    private void ReadSegmentMapping(FontTableData tables, int offset, Dictionary<int, int> glyphs)
    {
        var segments = tables.U16(offset + 6) / 2;
        var ends = offset + 14;
        var starts = ends + segments * 2 + 2;
        var deltas = starts + segments * 2;
        var ranges = deltas + segments * 2;
        var budget = BasicPlaneSize;
        for (var s = 0; s < segments && budget > 0; s++)
        {
            var end = tables.U16(ends + s * 2);
            var start = tables.U16(starts + s * 2);
            var delta = tables.S16(deltas + s * 2);
            var rangeOffset = tables.U16(ranges + s * 2);
            for (var c = start; c <= end && c != 0xFFFF && budget > 0; c++, budget--)
            {
                int glyph;
                if (rangeOffset == 0) glyph = (c + delta) & 0xFFFF;
                else
                {
                    glyph = tables.U16(ranges + s * 2 + rangeOffset + (c - start) * 2);
                    if (glyph != 0) glyph = (glyph + delta) & 0xFFFF;
                }
                if (glyph != 0) glyphs[c] = glyph;
            }
        }
    }

    private void ReadSegmentedCoverage(FontTableData tables, int offset, Dictionary<int, int> glyphs)
    {
        var groups = (int)Math.Min(tables.U32(offset + 12), MaximumGroups);
        var budget = MaximumMappings;
        for (var g = 0; g < groups && budget > 0; g++)
        {
            var group = offset + 16 + g * 12;
            var start = (int)Math.Min(tables.U32(group), 0x10FFFF);
            var end = (int)Math.Min(Math.Min(tables.U32(group + 4), (uint)(start + 0xFFFF)), 0x10FFFF);
            var glyph = (int)tables.U32(group + 8);
            for (var c = start; c <= end && budget > 0; c++, budget--) glyphs[c] = glyph + c - start;
        }
    }
}

public interface IFontMetricsReader
{
    IReadOnlyList<FontMetricsFace> Read(byte[] data);
}

public sealed class FontMetricsReader(IOpenTypeReader reader, IFontFileExtractor extractor, ICharacterMapReader characters, IEnumerable<IPairKerningReader> kerning) : IFontMetricsReader
{
    private readonly IReadOnlyList<IPairKerningReader> _kerning = [.. kerning];

    public FontMetricsReader() : this(new OpenTypeReader(), new FontFileExtractor(), new CharacterMapReader(), [new GposKerningReader(), new KernTableReader()])
    {
    }

    public IReadOnlyList<FontMetricsFace> Read(byte[] data)
    {
        var faces = new List<FontMetricsFace>();
        foreach (var face in reader.Read(data))
        {
            var tables = new FontTableData(extractor.Extract(data, face.CollectionIndex));
            if (!tables.Has("head") || !tables.Has("hhea")) continue;
            var pairs = _kerning.Select(source => source.Read(tables)).FirstOrDefault(found => found is not null);
            faces.Add(new FontMetricsFace(tables, face, characters.Read(tables), pairs is null ? new NoKerning() : new CachedKerning(pairs)));
        }
        return faces;
    }
}
