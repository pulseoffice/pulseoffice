using System.Buffers.Binary;
using PulseOffice.Typography.MicroType;
using PulseOffice.Typography.OpenType;
using Xunit;

namespace PulseOffice.Typography.Tests;

public class MicroTypeExpressTests
{
    private readonly EmbeddedOpenTypeCodec _codec = new();
    private readonly OpenTypeReader _reader = new();

    [Theory]
    [InlineData("boldonse")]
    [InlineData("cff")]
    [InlineData("variable")]
    public void LeTabelleCoincidonoConQuelleDiRiferimento(string name)
    {
        var font = _codec.Decode(Fixture($"{name}.fntdata"));
        var expected = Tables(Fixture($"{name}.libeot.ttf"));
        var actual = Tables(font);

        Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), actual.Keys.Order(StringComparer.Ordinal));
        foreach (var (tag, data) in expected)
        {
            if (tag == "head")
            {
                Assert.Equal(WithoutAdjustment(data), WithoutAdjustment(actual[tag]));
                continue;
            }

            Assert.True(data.AsSpan().SequenceEqual(actual[tag]), $"Table '{tag}' differs.");
        }
    }

    [Theory]
    [InlineData("boldonse", 0x00010000u)]
    [InlineData("cff", 0x4F54544Fu)]
    [InlineData("variable", 0x00010000u)]
    public void IlCaratterePortaFirmaChecksumEAllineamentoGiusti(string name, uint scalar)
    {
        var font = _codec.Decode(Fixture($"{name}.fntdata"));

        Assert.Equal(scalar, BinaryPrimitives.ReadUInt32BigEndian(font));
        Assert.Equal(0xB1B0AFBAu, Sum(font));
        Assert.Equal(0, font.Length % 4);

        var count = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(4));
        for (var index = 0; index < count; index++)
        {
            var record = font.AsSpan(12 + (16 * index));
            var offset = (int)BinaryPrimitives.ReadUInt32BigEndian(record[8..]);
            var length = (int)BinaryPrimitives.ReadUInt32BigEndian(record[12..]);
            Assert.Equal(0, offset % 4);
            if (index > 0) Assert.True(string.CompareOrdinal(Tag(font, index - 1), Tag(font, index)) < 0);
            if (Tag(font, index) != "head")
                Assert.Equal(BinaryPrimitives.ReadUInt32BigEndian(record[4..]), Sum(font.AsSpan(offset, (length + 3) & ~3)));
        }
    }

    [Fact]
    public void IlLettoreOpenTypeRiconosceNomiEGlifi()
    {
        var face = _reader.Read(_codec.Decode(Fixture("boldonse.fntdata"))).Single();

        Assert.Equal("Boldonse", face.Family);
        Assert.Equal("Regular", face.Subfamily);
        Assert.True(face.HasOs2);
    }

    [Fact]
    public void UnaLocaCortaSiRicostruisceCoerenteConLeLunghezzeDeiGlifi()
    {
        var tables = Tables(_codec.Decode(Fixture("boldonse.fntdata")));
        var glyphs = BinaryPrimitives.ReadUInt16BigEndian(tables["maxp"].AsSpan(4));
        var longFormat = BinaryPrimitives.ReadInt16BigEndian(tables["head"].AsSpan(50)) != 0;
        var loca = tables["loca"];
        var last = longFormat ? BinaryPrimitives.ReadUInt32BigEndian(loca.AsSpan(4 * glyphs)) : 2u * BinaryPrimitives.ReadUInt16BigEndian(loca.AsSpan(2 * glyphs));

        Assert.Equal((uint)tables["glyf"].Length, last);
    }

    [Fact]
    public void DatiTroncatiNonFannoCadereNulla()
    {
        var eot = Fixture("boldonse.fntdata");
        foreach (var keep in Enumerable.Range(1, 60).Select(step => eot.Length * step / 61))
        {
            var truncated = eot[..keep];
            BinaryPrimitives.WriteUInt32LittleEndian(truncated, (uint)truncated.Length);
            BinaryPrimitives.WriteUInt32LittleEndian(truncated.AsSpan(4), (uint)Math.Max(0, truncated.Length - (eot.Length - BinaryPrimitives.ReadInt32LittleEndian(eot.AsSpan(4)))));
            AssertFailsCleanly(truncated);
        }
    }

    [Fact]
    public void DatiCorrottiNonFannoCadereNulla()
    {
        var original = Fixture("cff.fntdata");
        var random = new Random(20261005);
        for (var round = 0; round < 150; round++)
        {
            var corrupt = original.ToArray();
            var start = original.Length - BinaryPrimitives.ReadInt32LittleEndian(original.AsSpan(4));
            for (var flip = 0; flip < 1 + random.Next(6); flip++)
                corrupt[random.Next(start, corrupt.Length)] ^= (byte)(1 << random.Next(8));
            AssertFailsCleanly(corrupt);
        }
    }

    [Fact]
    public void BlocchiCtfCorrottiNonFannoCadereNulla()
    {
        var decoder = new LzcompDecoder();
        var random = new Random(7);
        for (var round = 0; round < 300; round++)
        {
            var noise = new byte[random.Next(1, 400)];
            random.NextBytes(noise);
            try
            {
                decoder.Decode(noise, 0, noise.Length, (byte)random.Next(1, 4));
            }
            catch (InvalidDataException)
            {
            }
        }
    }

    [Fact]
    public void UnBloccoCheDichiaraTroppiByteSiRifiuta()
    {
        var decoder = new LzcompDecoder(new MtxLimits { MaxBlockBytes = 1024 });
        Assert.Throws<InvalidDataException>(() => decoder.Decode([0x00, 0x7F, 0xFF, 0xFF, 0xFF, 0xFF], 0, 6, 1));
    }

    [Fact]
    public void UnCarattereOltreIlLimiteDiGlifiSiRifiuta()
    {
        var codec = new EmbeddedOpenTypeCodec(new MicroTypeExpressDecoder(new MtxLimits { MaxGlyphs = 10 }));
        Assert.Throws<InvalidDataException>(() => codec.Decode(Fixture("boldonse.fntdata")));
    }

    private void AssertFailsCleanly(byte[] eot)
    {
        try
        {
            var font = _codec.Decode(eot);
            Assert.NotNull(font);
        }
        catch (InvalidDataException)
        {
        }
    }

    private byte[] Fixture(string name) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Mtx", name));

    private Dictionary<string, byte[]> Tables(byte[] font)
    {
        var count = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(4));
        var tables = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        for (var index = 0; index < count; index++)
        {
            var record = font.AsSpan(12 + (16 * index));
            var offset = (int)BinaryPrimitives.ReadUInt32BigEndian(record[8..]);
            var length = (int)BinaryPrimitives.ReadUInt32BigEndian(record[12..]);
            tables[Tag(font, index)] = font.AsSpan(offset, length).ToArray();
        }

        return tables;
    }

    private string Tag(byte[] font, int index) => new([.. font.AsSpan(12 + (16 * index), 4).ToArray().Select(value => (char)value)]);

    private byte[] WithoutAdjustment(byte[] head)
    {
        var copy = head.ToArray();
        copy.AsSpan(8, 4).Clear();
        return copy;
    }

    private uint Sum(ReadOnlySpan<byte> data)
    {
        var sum = 0u;
        for (var index = 0; index + 4 <= data.Length; index += 4)
            sum = unchecked(sum + BinaryPrimitives.ReadUInt32BigEndian(data[index..]));
        return sum;
    }
}
