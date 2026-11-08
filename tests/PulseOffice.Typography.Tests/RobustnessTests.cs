using System.Buffers.Binary;
using System.Text;
using PulseOffice.Document.Fonts;
using PulseOffice.Typography.Catalog;
using PulseOffice.Typography.Licensing;
using PulseOffice.Typography.OpenType;
using PulseOffice.Typography.Sources;
using Xunit;

namespace PulseOffice.Typography.Tests;

public class RobustnessTests
{
    [Fact]
    public void UnaTabellaRipetutaNonFaCadereLaLettura()
    {
        var font = new SyntheticFont().Build();
        var count = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(4));
        var first = Encoding.ASCII.GetString(font, 12, 4);
        Encoding.ASCII.GetBytes(first).CopyTo(font, 12 + 16 * (count - 1));

        Assert.Single(new OpenTypeReader().Read(font));
    }

    [Fact]
    public void UnaCollezioneConTabelleSovrappostePerTuttoIlFileNonMoltiplicaLaMemoria()
    {
        const int tables = 512;
        var data = new byte[1024 * 1024];
        BinaryPrimitives.WriteUInt32BigEndian(data, 0x74746366);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(12), 16);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(16), 0x00010000);
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(20), tables);
        for (var index = 0; index < tables; index++)
        {
            var record = data.AsSpan(28 + (16 * index));
            Encoding.ASCII.GetBytes($"t{index:D3}").CopyTo(record);
            BinaryPrimitives.WriteUInt32BigEndian(record[8..], 0);
            BinaryPrimitives.WriteUInt32BigEndian(record[12..], (uint)data.Length);
        }
        var before = GC.GetAllocatedBytesForCurrentThread();

        Assert.Throws<InvalidDataException>(() => new FontFileExtractor().Extract(data, 0));

        Assert.True(GC.GetAllocatedBytesForCurrentThread() - before < 64L * 1024 * 1024);
    }

    [Fact]
    public void SegmentiCmapSovrappostiNonCostanoMiliardiDiPassi()
    {
        const int segments = 32000;
        var subtable = 14 + (8 * segments) + 2;
        var cmapLength = 12 + subtable;
        var data = new byte[28 + cmapLength];
        BinaryPrimitives.WriteUInt32BigEndian(data, 0x00010000);
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(4), 1);
        Encoding.ASCII.GetBytes("cmap").CopyTo(data, 12);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(20), 28);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(24), (uint)cmapLength);
        var cmap = data.AsSpan(28);
        BinaryPrimitives.WriteUInt16BigEndian(cmap[2..], 1);
        BinaryPrimitives.WriteUInt16BigEndian(cmap[4..], 3);
        BinaryPrimitives.WriteUInt16BigEndian(cmap[6..], 1);
        BinaryPrimitives.WriteUInt32BigEndian(cmap[8..], 12);
        var table = cmap[12..];
        BinaryPrimitives.WriteUInt16BigEndian(table, 4);
        BinaryPrimitives.WriteUInt16BigEndian(table[6..], segments * 2);
        for (var segment = 0; segment < segments; segment++)
        {
            BinaryPrimitives.WriteUInt16BigEndian(table[(14 + (segment * 2))..], 0xFFFE);
            BinaryPrimitives.WriteUInt16BigEndian(table[(14 + (segments * 4) + 2 + (segment * 2))..], 1);
        }
        var watch = System.Diagnostics.Stopwatch.StartNew();

        var glyphs = new PulseOffice.Typography.OpenType.Metrics.CharacterMapReader().Read(new PulseOffice.Typography.OpenType.Metrics.FontTableData(data));

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2), watch.Elapsed.ToString());
        Assert.Equal(1, glyphs[0]);
    }

    [Fact]
    public void GlifiChePuntanoTuttiAllIntereTabellaNonMoltiplicanoIlSottoinsieme()
    {
        const int glyphs = 2000;
        const int glyfLength = 200 * 1024;
        var glyf = new byte[glyfLength];
        BinaryPrimitives.WriteInt16BigEndian(glyf, -1);
        var cursor = 10;
        for (var component = 2; component < glyphs && cursor + 8 <= glyfLength; component += 2, cursor += 6)
        {
            BinaryPrimitives.WriteUInt16BigEndian(glyf.AsSpan(cursor), 0x0020);
            BinaryPrimitives.WriteUInt16BigEndian(glyf.AsSpan(cursor + 2), (ushort)component);
        }
        var loca = new byte[(glyphs + 1) * 4];
        for (var glyph = 0; glyph <= glyphs; glyph++)
            BinaryPrimitives.WriteUInt32BigEndian(loca.AsSpan(glyph * 4), glyph % 2 == 0 ? 0u : glyfLength);
        var head = new byte[54];
        BinaryPrimitives.WriteUInt16BigEndian(head.AsSpan(18), 1000);
        BinaryPrimitives.WriteInt16BigEndian(head.AsSpan(50), 1);
        var hhea = new byte[36];
        BinaryPrimitives.WriteUInt16BigEndian(hhea.AsSpan(34), 1);
        var maxp = new byte[6];
        BinaryPrimitives.WriteUInt16BigEndian(maxp.AsSpan(4), glyphs);
        var font = Sfnt(("glyf", glyf), ("head", head), ("hhea", hhea), ("hmtx", new byte[4]), ("loca", loca), ("maxp", maxp));
        var before = GC.GetAllocatedBytesForCurrentThread();

        Assert.Throws<InvalidDataException>(() => new PulseOffice.Typography.Subsetting.TrueTypeSubsetter().Subset(new PulseOffice.Typography.OpenType.Metrics.FontTableData(font), [0]));

        Assert.True(GC.GetAllocatedBytesForCurrentThread() - before < 64L * 1024 * 1024);
    }

    [Fact]
    public void UnGlifoFuoriDallaTabellaTieneIlSuoPostoNelSottoinsieme()
    {
        var head = new byte[54];
        BinaryPrimitives.WriteUInt16BigEndian(head.AsSpan(18), 1000);
        BinaryPrimitives.WriteInt16BigEndian(head.AsSpan(50), 1);
        var hhea = new byte[36];
        BinaryPrimitives.WriteUInt16BigEndian(hhea.AsSpan(34), 1);
        var maxp = new byte[6];
        BinaryPrimitives.WriteUInt16BigEndian(maxp.AsSpan(4), 3);
        var font = Sfnt(("glyf", new byte[4]), ("head", head), ("hhea", hhea), ("hmtx", new byte[4]), ("loca", new byte[16]), ("maxp", maxp));

        var subset = new PulseOffice.Typography.Subsetting.TrueTypeSubsetter().Subset(new PulseOffice.Typography.OpenType.Metrics.FontTableData(font), [0, 99, 2]);

        Assert.Equal([0, 0, 2], subset.Glyphs);
    }

    private byte[] Sfnt(params (string Tag, byte[] Data)[] tables)
    {
        var header = 12 + (16 * tables.Length);
        var data = new byte[header + tables.Sum(table => (table.Data.Length + 3) & ~3)];
        BinaryPrimitives.WriteUInt32BigEndian(data, 0x00010000);
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(4), (ushort)tables.Length);
        var offset = header;
        for (var index = 0; index < tables.Length; index++)
        {
            var record = data.AsSpan(12 + (16 * index));
            Encoding.ASCII.GetBytes(tables[index].Tag).CopyTo(record);
            BinaryPrimitives.WriteUInt32BigEndian(record[8..], (uint)offset);
            BinaryPrimitives.WriteUInt32BigEndian(record[12..], (uint)tables[index].Data.Length);
            tables[index].Data.CopyTo(data, offset);
            offset += (tables[index].Data.Length + 3) & ~3;
        }
        return data;
    }

    [Fact]
    public void UnaCollezioneConTabellaEnormeNonAlloca()
    {
        var collection = new SyntheticFont().BuildCollection(new SyntheticFont { Family = "Seconda" });
        var faceOffset = BinaryPrimitives.ReadUInt32BigEndian(collection.AsSpan(12));
        BinaryPrimitives.WriteUInt32BigEndian(collection.AsSpan((int)faceOffset + 12 + 12), uint.MaxValue);

        Assert.Throws<InvalidDataException>(() => new FontFileExtractor().Extract(collection, 0));
    }

    [Fact]
    public void LEstrattoreResisteAiByteAlterati()
    {
        var collection = new SyntheticFont().BuildCollection(new SyntheticFont { Family = "Seconda" });
        var random = new Random(7);
        for (var round = 0; round < 500; round++)
        {
            var copy = (byte[])collection.Clone();
            for (var change = 0; change < 4; change++) copy[random.Next(copy.Length)] = (byte)random.Next(256);
            try
            {
                new FontFileExtractor().Extract(copy, 0);
                new OpenTypeReader().Read(copy);
            }
            catch (Exception ex) when (ex is InvalidDataException or ArgumentException or EndOfStreamException)
            {
            }
        }
    }

    [Fact]
    public async Task UnFileScaricatoCheNonEUnCarattereERifiutato()
    {
        var server = new FakeFontServer()
            .Json("https://api.test/v1/fonts/prova-sans", """{"id":"prova-sans","family":"Prova Sans","defSubset":"latin","variants":{"400":{"normal":{"latin":{"url":{"ttf":"https://cdn.test/400.ttf"}}}}}}""")
            .Bytes("https://cdn.test/400.ttf", Encoding.UTF8.GetBytes("<html><body>Rate limited</body></html>"));
        var source = new FontsourceSource(new HttpClient(server), new FontServiceOptions { Fontsource = new(true, new Uri("https://api.test/v1/")) },
            new FontLicenseClassifier(), new FontCategoryClassifier(), new FontScriptCoverage(), new ClassicFaceSelector(), new OpenTypeReader());
        var family = new FontFamilyInfo
        {
            Family = "Prova Sans", SourceId = FontsourceSource.SourceId, Origin = FontOrigin.Online, OnlineId = "prova-sans",
            Faces = [new FontFaceRef(400, false, "https://cdn.test/400.ttf")],
        };

        var result = await source.FetchAsync(family, null, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void DueFamiglieConLoStessoNomeDiCartellaNonSiConfondono()
    {
        var root = Path.Combine(Path.GetTempPath(), $"fontcache-{Guid.NewGuid():N}");
        try
        {
            var store = new FileFontCacheStore(root);
            store.Save("google", "Foo Bar", [new EmbeddedFontFile("Foo Bar", 400, false, "a.ttf", new SyntheticFont().Build())]);

            Assert.True(store.Contains("google", "Foo Bar"));
            Assert.False(store.Contains("google", "Foo-Bar"));
            Assert.Null(store.Load("google", "Foo-Bar"));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SalvataggiConcorrentiNonLascianoFileTemporanei()
    {
        var root = Path.Combine(Path.GetTempPath(), $"fontcache-{Guid.NewGuid():N}");
        try
        {
            var store = new FileFontCacheStore(root);
            var file = new EmbeddedFontFile("Prova", 400, false, "a.ttf", new SyntheticFont().Build());
            await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() =>
            {
                try
                {
                    store.Save("google", "Prova", [file]);
                }
                catch (IOException)
                {
                }
            }, TestContext.Current.CancellationToken)));

            Assert.NotNull(store.Load("google", "Prova"));
            Assert.Empty(Directory.EnumerateFiles(root, "*.tmp", SearchOption.AllDirectories));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
