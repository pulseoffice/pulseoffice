using PulseOffice.Typography.OpenType;
using Xunit;

namespace PulseOffice.Typography.Tests;

public class SystemFontFuzzTests
{
    private readonly OpenTypeReader _reader = new();
    private readonly FontFileExtractor _extractor = new();
    private readonly EmbeddedOpenTypeCodec _codec = new();

    private IEnumerable<byte[]> SystemFonts() =>
        new[] { "/System/Library/Fonts", "/usr/share/fonts", @"C:\Windows\Fonts" }
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories))
            .Where(path => path.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".ttc", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".otf", StringComparison.OrdinalIgnoreCase))
            .Where(path => new FileInfo(path).Length < 4_000_000)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Take(12)
            .Select(File.ReadAllBytes);

    private void Survive(Action action)
    {
        try
        {
            action();
        }
        catch (InvalidDataException)
        {
        }
    }

    [Fact]
    public void ICaratteriDiSistemaTroncatiOAlteratiDannoSoloInvalidDataException()
    {
        var random = new Random(11);
        foreach (var font in SystemFonts())
        {
            for (var round = 0; round < 150; round++)
            {
                var copy = round % 3 == 0 ? font[..random.Next(font.Length)] : (byte[])font.Clone();
                if (round % 3 != 0)
                    for (var change = 0; change < 8; change++) copy[random.Next(round % 2 == 0 ? copy.Length : Math.Min(copy.Length, 4096))] = (byte)random.Next(256);

                Survive(() => _reader.Read(copy));
                Survive(() => _extractor.Extract(copy, 0));
                Survive(() => _codec.Decode(copy));
            }
        }
    }

    [Fact]
    public void UnEotAlteratoDaSoloInvalidDataException()
    {
        var font = new SyntheticFont().Build();
        var eot = _codec.Encode(font, _reader.Read(font)[0]);
        var random = new Random(5);
        for (var round = 0; round < 400; round++)
        {
            var copy = round % 2 == 0 ? eot[..random.Next(eot.Length)] : (byte[])eot.Clone();
            if (round % 2 != 0) for (var change = 0; change < 4; change++) copy[random.Next(copy.Length)] = (byte)random.Next(256);
            Survive(() => _codec.Decode(copy));
        }
    }
}

public class NameTableTests
{
    [Fact]
    public void INomiMacintoshSiDecodificanoInMacRoman()
    {
        var font = new SyntheticFont { Family = "Café", NameRecord = (1, 0, 0) }.Build();

        Assert.Equal("Café", new OpenTypeReader().Read(font)[0].Family);
    }

    [Fact]
    public void INomiDeiCaratteriSimboloSiLeggono()
    {
        var font = new SyntheticFont { Family = "Simboli", NameRecord = (3, 0, 0x0409) }.Build();

        Assert.Equal("Simboli", new OpenTypeReader().Read(font)[0].Family);
    }

    [Fact]
    public void UnEotConIDatiDentroLIntestazioneERifiutato()
    {
        var codec = new EmbeddedOpenTypeCodec();
        var font = new SyntheticFont().Build();
        var eot = codec.Encode(font, new OpenTypeReader().Read(font)[0]);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(eot.AsSpan(4), (uint)eot.Length - 10);

        Assert.Throws<InvalidDataException>(() => codec.Decode(eot));
    }
}
