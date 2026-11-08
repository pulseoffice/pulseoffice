using System.Buffers.Binary;
using PulseOffice.Typography.MicroType;
using Xunit;

namespace PulseOffice.Typography.Tests;

public class CompactTableTests
{
    [Fact]
    public void HdmxSiRicostruisceDallaPrevisioneEDallaSorpresa()
    {
        var hdmx = Bytes("0000 0001 00000008 0C08 02");
        var font = Font(("hdmx", hdmx));

        var table = Translate(new HdmxTableTranslator(), font, "hdmx");

        Assert.Equal(Bytes("0000 0001 00000008 0C08 060803 000000"), table);
    }

    [Fact]
    public void HdmxNonCompressoTornaComEraConLaVersioneGiusta()
    {
        var font = Font(("hdmx", Bytes("FFFF 0001 00000008 0C08 060803 000000")));

        Assert.Equal(Bytes("0000 0001 00000008 0C08 060803 000000"), Translate(new HdmxTableTranslator(), font, "hdmx"));
    }

    [Fact]
    public void VdmxSiRicostruisceConGliScostamentiRicalcolati()
    {
        var font = Font(("VDMX", Bytes("0001 0001 0001 01010101 000C 0002 0733 019A 220A")));

        var table = Translate(new VdmxTableTranslator(), font, "VDMX");

        Assert.Equal(Bytes("0001 0001 0001 01010101 000C 0002 080A 0008 0008 FFFE 000A 0009 FFFD"), table);
    }

    [Fact]
    public void UnaTabellaFacoltativaIllegibileSiScartaSenzaFermareIlCarattere()
    {
        var font = Font(("VDMX", Bytes("0001 0001 0001 01010101 0040")));

        Assert.Empty(new VdmxTableTranslator().Translate(font, font.Entries.Single(entry => entry.Tag == "VDMX")));
    }

    [Fact]
    public void UnVdmxConMoltiGruppiVuotiNonRicopiaLaTabellaPerOgniGruppo()
    {
        const int groups = 16000;
        var vdmx = new byte[6 + 4 + 2 + (6 * groups) + (8 * 1024 * 1024)];
        BinaryPrimitives.WriteUInt16BigEndian(vdmx.AsSpan(0), 1);
        BinaryPrimitives.WriteUInt16BigEndian(vdmx.AsSpan(2), groups);
        BinaryPrimitives.WriteUInt16BigEndian(vdmx.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt16BigEndian(vdmx.AsSpan(10), 12);
        var font = Font(("VDMX", vdmx));
        var watch = System.Diagnostics.Stopwatch.StartNew();

        var table = Translate(new VdmxTableTranslator(), font, "VDMX");

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2), watch.Elapsed.ToString());
        Assert.Equal(groups, BinaryPrimitives.ReadUInt16BigEndian(table.AsSpan(2)));
    }

    [Fact]
    public void CvtSiDecodificaConDifferenzeEParoleIntere()
    {
        var font = Font(("cvt ", Bytes("0004 05 F8 0A EF 03 EE 7FFF")));

        var table = Translate(new CvtTableTranslator(), font, "cvt ");

        Assert.Equal(Bytes("0005 00FD 00FA 80F9"), table);
    }

    private byte[] Translate(CtfTableTranslator translator, CtfFont font, string tag) =>
        translator.Translate(font, font.Entries.Single(entry => entry.Tag == tag)).Single().Data;

    private CtfFont Font(params (string Tag, byte[] Data)[] extra)
    {
        var head = new byte[54];
        BinaryPrimitives.WriteUInt16BigEndian(head.AsSpan(18), 1000);
        var maxp = Bytes("00005000 0003");
        var hhea = new byte[36];
        BinaryPrimitives.WriteUInt16BigEndian(hhea.AsSpan(34), 3);
        var hmtx = Bytes("01F4 0000 0258 0000 00FA 0000");
        var tables = new List<(string Tag, byte[] Data)> { ("head", head), ("hhea", hhea), ("hmtx", hmtx), ("maxp", maxp) };
        tables.AddRange(extra);

        var directory = 12 + (16 * tables.Count);
        var block = new List<byte>(new byte[directory]);
        var span = new byte[directory];
        BinaryPrimitives.WriteUInt32BigEndian(span, 0x00010000);
        BinaryPrimitives.WriteUInt16BigEndian(span.AsSpan(4), (ushort)tables.Count);
        for (var index = 0; index < tables.Count; index++)
        {
            var record = span.AsSpan(12 + (16 * index));
            for (var letter = 0; letter < 4; letter++) record[letter] = (byte)tables[index].Tag[letter];
            BinaryPrimitives.WriteUInt32BigEndian(record[8..], (uint)block.Count);
            BinaryPrimitives.WriteUInt32BigEndian(record[12..], (uint)tables[index].Data.Length);
            block.AddRange(tables[index].Data);
        }

        var data = block.ToArray();
        span.CopyTo(data, 0);
        return new CtfFont(new MtxBlocks(data, [], []), MtxLimits.Default);
    }

    private byte[] Bytes(string hex) => Convert.FromHexString(hex.Replace(" ", string.Empty));
}
