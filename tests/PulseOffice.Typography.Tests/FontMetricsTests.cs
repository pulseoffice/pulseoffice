using PulseOffice.Typography.OpenType.Metrics;
using Xunit;

namespace PulseOffice.Typography.Tests;

public sealed class TableWriter
{
    private readonly List<byte> _bytes = [];

    public int Position => _bytes.Count;

    public TableWriter U16(params int[] values)
    {
        foreach (var value in values)
        {
            _bytes.Add((byte)((value >> 8) & 0xFF));
            _bytes.Add((byte)(value & 0xFF));
        }
        return this;
    }

    public TableWriter U32(uint value)
    {
        U16((int)(value >> 16), (int)(value & 0xFFFF));
        return this;
    }

    public TableWriter Tag(string tag)
    {
        foreach (var letter in tag) _bytes.Add((byte)letter);
        return this;
    }

    public byte[] ToArray() => [.. _bytes];
}

public sealed class KerningFont
{
    public byte[] Hhea() => new TableWriter().U32(0x00010000).U16(1800, -400 & 0xFFFF, 100).U16(new int[11]).U16(0, 4).ToArray();

    public byte[] Hmtx() => new TableWriter().U16(500, 0, 600, 0, 610, 0, 620, 0).ToArray();

    public byte[] Cmap() => new TableWriter()
        .U16(0, 1).U16(3, 1).U32(12)
        .U16(4, 48, 0, 8, 8, 3, 0)
        .U16(0x41, 0x54, 0x56, 0xFFFF).U16(0)
        .U16(0x41, 0x54, 0x56, 0xFFFF)
        .U16((1 - 0x41) & 0xFFFF, (3 - 0x54) & 0xFFFF, (2 - 0x56) & 0xFFFF, 1)
        .U16(0, 0, 0, 0)
        .ToArray();

    public byte[] Gpos() => new TableWriter()
        .U32(0x00010000).U16(10, 30, 46)
        .U16(1).Tag("DFLT").U16(8)
        .U16(4, 0)
        .U16(0, 0xFFFF, 1, 0)
        .U16(1).Tag("kern").U16(8)
        .U16(0, 2, 0, 1)
        .U16(2, 6, 38)
        .U16(2, 0, 1, 8)
        .U16(1, 18, 4, 0, 1, 12)
        .U16(1, 2, -80 & 0xFFFF)
        .U16(1, 1, 1)
        .U16(9, 0, 1, 8)
        .U16(1, 2).U32(8)
        .U16(2, 24, 4, 0, 30, 38, 2, 2)
        .U16(0, 0, 0, -50 & 0xFFFF)
        .U16(1, 1, 3)
        .U16(1, 3, 1, 1)
        .U16(2, 1, 1, 1, 1)
        .ToArray();

    public byte[] Kern() => new TableWriter()
        .U16(0, 1)
        .U16(0, 26, 0x0001)
        .U16(2, 12, 1, 0)
        .U16(1, 2, -30 & 0xFFFF)
        .U16(2, 1, -20 & 0xFFFF)
        .ToArray();

    public byte[] Build(bool gpos, bool kern)
    {
        var tables = new List<(string, byte[])> { ("hhea", Hhea()), ("hmtx", Hmtx()), ("cmap", Cmap()) };
        if (gpos) tables.Add(("GPOS", Gpos()));
        if (kern) tables.Add(("kern", Kern()));
        return new SyntheticFont { ExtraTables = tables }.Build();
    }
}

public sealed class FontMetricsTests
{
    private readonly FontMetricsReader _reader = new();

    [Fact]
    public void LaCrenaturaArrivaDalleCoppieGposNeiDueFormati()
    {
        var face = _reader.Read(new KerningFont().Build(gpos: true, kern: true)).Single();

        Assert.Equal(1, face.GlyphOf('A'));
        Assert.Equal(2, face.GlyphOf('V'));
        Assert.Equal(-80, face.Kerning(face.GlyphOf('A'), face.GlyphOf('V')));
        Assert.Equal(-50, face.Kerning(face.GlyphOf('T'), face.GlyphOf('A')));
        Assert.Equal(0, face.Kerning(face.GlyphOf('V'), face.GlyphOf('A')));
    }

    [Fact]
    public void SenzaGposSiUsaLaTabellaKern()
    {
        var face = _reader.Read(new KerningFont().Build(gpos: false, kern: true)).Single();

        Assert.Equal(-30, face.Kerning(1, 2));
        Assert.Equal(-20, face.Kerning(2, 1));
    }

    [Fact]
    public void SenzaTabelleLaCrenaturaENulla()
    {
        var face = _reader.Read(new KerningFont().Build(gpos: false, kern: false)).Single();

        Assert.Equal(0, face.Kerning(1, 2));
        Assert.Equal(600, face.AdvanceOf('A'));
    }

    [Fact]
    public void UnaCollezioneDaTutteLeFacce()
    {
        var builder = new KerningFont();
        var tables = new List<(string, byte[])> { ("hhea", builder.Hhea()), ("hmtx", builder.Hmtx()), ("cmap", builder.Cmap()), ("GPOS", builder.Gpos()) };
        var regular = new SyntheticFont { Family = "Coppia", ExtraTables = tables };
        var bold = new SyntheticFont { Family = "Coppia", Subfamily = "Bold", Weight = 700, ExtraTables = tables };

        var faces = _reader.Read(regular.BuildCollection(bold));

        Assert.Equal(2, faces.Count);
        Assert.False(faces[0].Bold);
        Assert.True(faces[1].Bold);
        Assert.All(faces, face => Assert.Equal(-80, face.Kerning(1, 2)));
    }
}
