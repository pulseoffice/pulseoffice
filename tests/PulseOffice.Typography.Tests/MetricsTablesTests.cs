using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;
using PulseOffice.Typography.OpenType.Metrics;
using Xunit;

namespace PulseOffice.Typography.Tests;

public class MetricsTablesTests
{
    [Fact]
    public void UnaTabellaKernConPiuDi10920CoppieSiLeggeIntera()
    {
        const int count = 11000;
        var body = new List<byte>();
        Add16(body, 0);
        Add16(body, 2);
        Add16(body, 0);
        Add16(body, (14 + count * 6) & 0xFFFF);
        Add16(body, 0x0001);
        Add16(body, count);
        Add16(body, 0);
        Add16(body, 0);
        Add16(body, 0);
        for (var i = 0; i < count; i++)
        {
            Add16(body, 1);
            Add16(body, 2 + i);
            Add16(body, unchecked((ushort)(short)-(i % 100 + 1)));
        }
        AddSubtable(body, 0x0001, -7, 5, 6);
        var kerning = new KernTableReader().Read(Font(("kern", body.ToArray())));

        Assert.NotNull(kerning);
        Assert.Equal(-7, kerning.Adjust(5, 6));
        Assert.Equal(-((count - 1) % 100 + 1), kerning.Adjust(1, 2 + count - 1));
    }

    [Fact]
    public void LaTabellaKernMinimaNonSiSommaAllaCrenatura()
    {
        var body = new List<byte>();
        Add16(body, 0);
        Add16(body, 2);
        AddSubtable(body, 0x0001, -50);
        AddSubtable(body, 0x0003, 10);
        var kerning = new KernTableReader().Read(Font(("kern", body.ToArray())));

        Assert.Equal(-50, kerning!.Adjust(1, 2));
    }

    [Fact]
    public void UnaCmapSimboloMappaAncheICodiciSenzaAreaPrivata()
    {
        var body = new List<byte>();
        Add16(body, 0);
        Add16(body, 1);
        Add16(body, 3);
        Add16(body, 0);
        Add32(body, 12);
        Add16(body, 4);
        Add16(body, 32);
        Add16(body, 0);
        Add16(body, 4);
        Add16(body, 4);
        Add16(body, 1);
        Add16(body, 0);
        Add16(body, 0xF0FF);
        Add16(body, 0xFFFF);
        Add16(body, 0);
        Add16(body, 0xF020);
        Add16(body, 0xFFFF);
        Add16(body, unchecked((ushort)(short)(5 - 0xF020)));
        Add16(body, 1);
        Add16(body, 0);
        Add16(body, 0);

        var glyphs = new CharacterMapReader().Read(Font(("cmap", body.ToArray())));

        Assert.Equal(5, glyphs[0xF020]);
        Assert.Equal(5, glyphs[0x20]);
        Assert.Equal(38, glyphs['A']);
    }

    [Fact]
    public void UnaCmapConGruppiSovrappostiEnormiNonSiBlocca()
    {
        const int groups = 100_000;
        var body = new List<byte>();
        Add16(body, 0);
        Add16(body, 1);
        Add16(body, 3);
        Add16(body, 10);
        Add32(body, 12);
        Add16(body, 12);
        Add16(body, 0);
        Add32(body, (uint)(16 + groups * 12));
        Add32(body, 0);
        Add32(body, groups);
        for (var g = 0; g < groups; g++)
        {
            Add32(body, 0);
            Add32(body, 0x10FFFF);
            Add32(body, 1);
        }
        var watch = Stopwatch.StartNew();
        var glyphs = new CharacterMapReader().Read(Font(("cmap", body.ToArray())));

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10));
        Assert.Equal(66, glyphs['A']);
    }

    private static void AddSubtable(List<byte> body, int coverage, int value, int left = 1, int right = 2)
    {
        Add16(body, 0);
        Add16(body, 20);
        Add16(body, coverage);
        Add16(body, 1);
        Add16(body, 0);
        Add16(body, 0);
        Add16(body, 0);
        Add16(body, left);
        Add16(body, right);
        Add16(body, unchecked((ushort)(short)value));
    }

    private static FontTableData Font(params (string Tag, byte[] Data)[] tables)
    {
        var header = 12 + tables.Length * 16;
        var bytes = new List<byte>();
        Add32(bytes, 0x00010000);
        Add16(bytes, tables.Length);
        Add16(bytes, 0);
        Add16(bytes, 0);
        Add16(bytes, 0);
        var offset = header;
        foreach (var (tag, data) in tables)
        {
            bytes.AddRange(Encoding.ASCII.GetBytes(tag));
            Add32(bytes, 0);
            Add32(bytes, (uint)offset);
            Add32(bytes, (uint)data.Length);
            offset += data.Length;
        }
        foreach (var (_, data) in tables) bytes.AddRange(data);
        return new FontTableData([.. bytes]);
    }

    private static void Add16(List<byte> bytes, int value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(buffer, (ushort)value);
        bytes.AddRange(buffer.ToArray());
    }

    private static void Add32(List<byte> bytes, uint value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
        bytes.AddRange(buffer.ToArray());
    }
}
