using System.Net;
using System.Text;
using PulseOffice.Document.Fonts;
using PulseOffice.Typography.Catalog;
using PulseOffice.Typography.Embedding;
using PulseOffice.Typography.Licensing;
using PulseOffice.Typography.OpenType;
using PulseOffice.Typography.Sources;
using PulseOffice.Foundation.Diagnostics;
using Xunit;

namespace PulseOffice.Typography.Tests;

public sealed class FakeFontServer : HttpMessageHandler
{
    private readonly Dictionary<string, Func<HttpResponseMessage>> _routes = new(StringComparer.Ordinal);

    public List<Uri> Seen { get; } = [];

    public bool Offline { get; set; }

    public FakeFontServer Json(string url, string body)
    {
        _routes[url] = () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        return this;
    }

    public FakeFontServer Text(string url, string body)
    {
        _routes[url] = () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };
        return this;
    }

    public FakeFontServer Bytes(string url, byte[] body)
    {
        _routes[url] = () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) };
        return this;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Seen.Add(request.RequestUri!);
        if (Offline) throw new HttpRequestException("offline");
        return Task.FromResult(_routes.TryGetValue(request.RequestUri!.AbsoluteUri, out var answer)
            ? answer()
            : new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}

public class OnlineSourceTests
{
    private const string Listing = """[{"id":"prova-sans","family":"Prova Sans","subsets":["latin","cyrillic"],"weights":[400,700],"styles":["normal","italic"],"category":"sans-serif","license":"OFL-1.1","type":"google"},{"id":"rotto"},{"family":"Senza Id"}]""";

    private static readonly byte[] Regular = new SyntheticFont().Build();
    private static readonly byte[] Bold = new SyntheticFont { Weight = 700 }.Build();

    private const string Detail = """{"id":"prova-sans","family":"Prova Sans","defSubset":"latin","variants":{"400":{"normal":{"latin":{"url":{"woff2":"https://cdn.test/400.woff2","ttf":"https://cdn.test/400.ttf"}}}},"700":{"normal":{"latin":{"url":{"ttf":"https://cdn.test/700.ttf"}}}}}}""";

    private FontsourceSource Fontsource(FakeFontServer server) => new(
        new HttpClient(server), new FontServiceOptions { Fontsource = new(true, new Uri("https://api.test/v1/")) },
        new FontLicenseClassifier(), new FontCategoryClassifier(), new FontScriptCoverage(), new ClassicFaceSelector(), new OpenTypeReader());

    private FakeFontServer Server() => new FakeFontServer()
        .Json("https://api.test/v1/fonts", Listing)
        .Json("https://api.test/v1/fonts/prova-sans", Detail)
        .Bytes("https://cdn.test/400.ttf", Regular)
        .Bytes("https://cdn.test/700.ttf", Bold);

    [Fact]
    public async Task FontsourceElencaICaratteriEScartaLeVociIncomplete()
    {
        var families = await Fontsource(Server()).ListAsync(TestContext.Current.CancellationToken);

        var family = Assert.Single(families);
        Assert.Equal("Prova Sans", family.Family);
        Assert.Equal(FontOrigin.Online, family.Origin);
        Assert.Equal(FontCategory.SansSerif, family.Category);
        Assert.Equal(FontLicenseKind.Ofl, family.License.Kind);
        Assert.Contains(FontScript.Cyrillic, family.Scripts);
        Assert.Equal(4, family.Faces.Count);
    }

    [Fact]
    public async Task FontsourceScaricaSoloITtfConProgresso()
    {
        var server = Server();
        var source = Fontsource(server);
        var family = (await source.ListAsync(TestContext.Current.CancellationToken)).Single();
        var steps = new List<FontProgress>();

        var result = await source.FetchAsync(family, new SynchronousProgress<FontProgress>(steps.Add), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal([400, 700], result.Value!.Select(file => file.Weight));
        Assert.Equal(Regular, result.Value![0].Data);
        Assert.DoesNotContain(server.Seen, uri => uri.AbsoluteUri.EndsWith(".woff2", StringComparison.Ordinal));
        Assert.Equal(1.0, steps[^1].Fraction);
    }

    [Fact]
    public async Task UnErroreDiReteDiventaUnaDiagnostica()
    {
        var server = Server();
        var source = Fontsource(server);
        var family = (await source.ListAsync(TestContext.Current.CancellationToken)).Single();
        server.Offline = true;

        var result = await source.FetchAsync(family, null, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("FONT022", result.Diagnostics.Single().Code);
    }

    [Fact]
    public async Task LoScaricamentoSiAnnulla()
    {
        var source = Fontsource(Server());
        var family = (await source.ListAsync(TestContext.Current.CancellationToken)).Single();
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => source.FetchAsync(family, null, cancel.Token));
    }

    [Fact]
    public async Task GoogleFontsLeggeIMetadatiPubbliciEIlRepositorio()
    {
        const string Metadata = """
            )]}'
            {"familyMetadataList":[{"family":"Prova Sans","category":"Sans Serif","subsets":["menu","latin"],"fonts":{"400":{},"400i":{}},"designers":["Ada"],"isOpenSource":true},{"family":"Chiuso","isOpenSource":false}]}
            """;
        const string Pb = """
name: "Prova Sans"
license: "OFL"
fonts {
  name: "Prova Sans"
  style: "normal"
  weight: 400
  filename: "ProvaSans-Regular.ttf"
}
fonts {
  name: "Prova Sans"
  style: "italic"
  weight: 400
  filename: "ProvaSans-Italic.ttf"
}
""";
        var server = new FakeFontServer()
            .Text("https://fonts.test/metadata", Metadata)
            .Text("https://raw.test/ofl/provasans/METADATA.pb", Pb)
            .Bytes("https://raw.test/ofl/provasans/ProvaSans-Regular.ttf", Regular)
            .Bytes("https://raw.test/ofl/provasans/ProvaSans-Italic.ttf", new SyntheticFont { Italic = true }.Build());
        var source = new GoogleFontsSource(new HttpClient(server),
            new FontServiceOptions { GoogleFonts = new(true, new Uri("https://fonts.test/metadata"), new Uri("https://raw.test/")) },
            new FontCategoryClassifier(), new FontScriptCoverage(), new ClassicFaceSelector(), new GoogleFontsMetadataParser(), new OpenTypeReader());

        var family = Assert.Single(await source.ListAsync(TestContext.Current.CancellationToken));
        Assert.Equal("Ada", family.Designer);
        Assert.True(family.License.IsFree);

        var result = await source.FetchAsync(family, null, TestContext.Current.CancellationToken);
        Assert.True(result.Succeeded);
        Assert.Equal([false, true], result.Value!.Select(file => file.Italic));
    }

    [Fact]
    public async Task UnServizioDisattivatoNonVaInRete()
    {
        var server = Server();
        var source = new FontsourceSource(new HttpClient(server), new FontServiceOptions { Fontsource = new(false, new Uri("https://api.test/v1/")) },
            new FontLicenseClassifier(), new FontCategoryClassifier(), new FontScriptCoverage(), new ClassicFaceSelector(), new OpenTypeReader());

        Assert.Empty(await source.ListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(server.Seen);
    }

    [Fact]
    public void LaConfigurazioneUtenteSovrascriveGliIndirizziSoloInHttps()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fonts-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """{"fontsource":{"enabled":false},"googleFonts":{"endpoint":"http://insicuro.test/","files":"https://mirror.test/"}}""");
        try
        {
            var options = new FileFontServiceOptionsStore(path).Load();
            Assert.False(options.Fontsource.Enabled);
            Assert.Equal("fonts.google.com", options.GoogleFonts.Endpoint.Host);
            Assert.Equal("mirror.test", options.GoogleFonts.Files!.Host);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void IlParserDeiMetadatiGoogleLeggeLeFacce()
    {
        var faces = new GoogleFontsMetadataParser().Parse("fonts {\n style: \"italic\"\n weight: 700\n filename: \"A-BoldItalic.ttf\"\n}\nfonts {\n filename: \"A.otf\"\n}\n");
        var face = Assert.Single(faces);
        Assert.Equal(700, face.Weight);
        Assert.True(face.Italic);
    }
}

public sealed class SynchronousProgress<T>(Action<T> report) : IProgress<T>
{
    public void Report(T value) => report(value);
}

public class CacheAndCatalogTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"quad-fonts-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        GC.SuppressFinalize(this);
    }

    private sealed class CountingSource(IReadOnlyList<FontFamilyInfo> families, IReadOnlyList<EmbeddedFontFile> files, FontOrigin origin = FontOrigin.Online, string id = "fake") : IFontSource
    {
        public int Fetches { get; private set; }

        public bool Fail { get; set; }

        public string Id => id;

        public FontOrigin Origin => origin;

        public Task<IReadOnlyList<FontFamilyInfo>> ListAsync(CancellationToken ct = default) =>
            Fail ? throw new HttpRequestException("offline") : Task.FromResult(families);

        public Task<OperationResult<IReadOnlyList<EmbeddedFontFile>>> FetchAsync(FontFamilyInfo family, IProgress<FontProgress>? progress = null, CancellationToken ct = default)
        {
            Fetches++;
            return Task.FromResult(Fail
                ? OperationResult<IReadOnlyList<EmbeddedFontFile>>.Fail(Diagnostic.Error("X", "offline"))
                : OperationResult<IReadOnlyList<EmbeddedFontFile>>.Ok(files));
        }
    }

    private static FontFamilyInfo Online(string name, string source = "fake") => new()
    {
        Family = name,
        SourceId = source,
        Origin = FontOrigin.Online,
        License = new FontLicense(FontLicenseKind.Ofl, "OFL"),
        Faces = [new FontFaceRef(400, false, "https://cdn.test/a.ttf")],
    };

    [Fact]
    public async Task LaCacheServeLElencoSenzaReteEMarcaIScaricati()
    {
        var file = new EmbeddedFontFile("Prova Sans", 400, false, "a.ttf", new SyntheticFont().Build());
        var inner = new CountingSource([Online("Prova Sans"), Online("Altro")], [file]);
        var store = new FileFontCacheStore(_root);
        var cached = new CachedFontSource(inner, store, new FontServiceOptions { ListingLifetime = TimeSpan.Zero }, TimeProvider.System, new OpenTypeReader());

        var first = await cached.ListAsync(TestContext.Current.CancellationToken);
        await cached.FetchAsync(first[0], null, TestContext.Current.CancellationToken);
        await cached.FetchAsync(first[0], null, TestContext.Current.CancellationToken);
        Assert.Equal(1, inner.Fetches);

        inner.Fail = true;
        var offline = await cached.ListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, offline.Count);
        Assert.Equal(FontOrigin.Downloaded, offline.Single(family => family.Family == "Prova Sans").Origin);
        Assert.Equal(FontOrigin.Online, offline.Single(family => family.Family == "Altro").Origin);
        Assert.Equal(FontLicenseKind.Ofl, offline[0].License.Kind);

        var again = await cached.FetchAsync(offline[0], null, TestContext.Current.CancellationToken);
        Assert.True(again.Succeeded);
        Assert.Equal(file.Data, again.Value![0].Data);
        Assert.Single(store.Downloaded());
    }

    private sealed class SlowSource(IReadOnlyList<EmbeddedFontFile> files) : IFontSource
    {
        private int _fetches;

        public int Fetches => _fetches;

        public string Id => "slow";

        public FontOrigin Origin => FontOrigin.Online;

        public Task<IReadOnlyList<FontFamilyInfo>> ListAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<FontFamilyInfo>>([]);

        public async Task<OperationResult<IReadOnlyList<EmbeddedFontFile>>> FetchAsync(FontFamilyInfo family, IProgress<FontProgress>? progress = null, CancellationToken ct = default)
        {
            Interlocked.Increment(ref _fetches);
            await Task.Delay(50, ct);
            return OperationResult<IReadOnlyList<EmbeddedFontFile>>.Ok(files);
        }
    }

    [Fact]
    public async Task UnFileInCacheRovinatoSiScaricaDiNuovo()
    {
        var file = new EmbeddedFontFile("Prova Sans", 400, false, "a.ttf", new SyntheticFont().Build());
        var inner = new CountingSource([Online("Prova Sans")], [file]);
        var store = new FileFontCacheStore(_root);
        store.Save("fake", "Prova Sans", [file with { Data = System.Text.Encoding.UTF8.GetBytes("<html>error</html>") }]);
        var cached = new CachedFontSource(inner, store, new FontServiceOptions(), TimeProvider.System, new OpenTypeReader());

        var result = await cached.FetchAsync(Online("Prova Sans"), null, TestContext.Current.CancellationToken);

        Assert.Equal(1, inner.Fetches);
        Assert.Equal(file.Data, result.Value![0].Data);
        Assert.Equal(file.Data, store.Load("fake", "Prova Sans")![0].Data);
    }

    [Fact]
    public async Task ScaricamentiConcorrentiDelloStessoCarattereSonoUnoSolo()
    {
        var file = new EmbeddedFontFile("Prova Sans", 400, false, "a.ttf", new SyntheticFont().Build());
        var inner = new SlowSource([file]);
        var cached = new CachedFontSource(inner, new FileFontCacheStore(_root), new FontServiceOptions(), TimeProvider.System, new OpenTypeReader());

        await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => cached.FetchAsync(Online("Prova Sans"), null, TestContext.Current.CancellationToken)));

        Assert.Equal(1, inner.Fetches);
    }

    [Fact]
    public async Task IlCatalogoUnisceLeFontiDandoPrioritaAlSistema()
    {
        var system = new CountingSource([new FontFamilyInfo { Family = "Prova Sans", SourceId = "system", Origin = FontOrigin.System, Faces = [new FontFaceRef(400, false, "/x.ttf")] }], [], FontOrigin.System, "system");
        var online = new CountingSource([Online("prova sans"), Online("Solo Online")], []);
        var catalog = new CompositeFontCatalog([system, online]);

        var families = await catalog.ListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, families.Count);
        Assert.Equal(FontOrigin.System, catalog.Find("PROVA SANS")!.Origin);
        Assert.Equal(FontOrigin.Online, catalog.Find("Solo Online")!.Origin);
    }

    [Fact]
    public async Task UnaFonteCheFallisceNonSvuotaIlCatalogo()
    {
        var broken = new CountingSource([Online("X")], []) { Fail = true };
        var good = new CountingSource([Online("Buono", "good")], [], FontOrigin.Online, "good");
        var catalog = new CompositeFontCatalog([broken, good]);

        Assert.Equal("Buono", Assert.Single(await catalog.ListAsync(TestContext.Current.CancellationToken)).Family);
    }

    [Fact]
    public async Task ICaratteriDelDocumentoVengonoMarcati()
    {
        var document = new FakeDocument { Fonts = new DocumentFonts(false, [new EmbeddedFontFile("Incorporato", 400, false, "i.ttf", new SyntheticFont { Family = "Incorporato" }.Build())]) };
        var active = new FakeActive(document, ["Prova Sans", "Mancante"]);
        var source = new DocumentFontSource(active, new OpenTypeReader(), new FontLicenseClassifier(), new FontCategoryClassifier(), new FontScriptCoverage());
        var system = new CountingSource([new FontFamilyInfo { Family = "Prova Sans", SourceId = "system", Origin = FontOrigin.System }], [], FontOrigin.System, "system");
        var catalog = new CompositeFontCatalog([source, system]);

        var families = await catalog.ListAsync(TestContext.Current.CancellationToken);

        Assert.True(catalog.Find("Prova Sans")!.InDocument);
        Assert.Equal(FontOrigin.System, catalog.Find("Prova Sans")!.Origin);
        Assert.Equal(FontOrigin.Document, catalog.Find("Incorporato")!.Origin);
        Assert.Equal(FontLicenseKind.Ofl, catalog.Find("Incorporato")!.License.Kind);
        Assert.True(catalog.Find("Mancante")!.InDocument);
        Assert.Equal(3, families.Count);
    }

    [Fact]
    public void IRecentiSiRicordanoInTestaESenzaDoppioni()
    {
        var path = Path.Combine(_root, "recent-fonts.json");
        var recent = new FileRecentFonts(path, capacity: 3);
        foreach (var name in new[] { "A", "B", "C", "a", "D" }) recent.Remember(name);

        Assert.Equal(["D", "a", "C"], new FileRecentFonts(path).All);
    }

    private sealed class FakeDocument : IFontDocument
    {
        public DocumentFonts Fonts { get; set; } = DocumentFonts.Empty;
    }

    private sealed class FakeActive(IFontDocument? document, IReadOnlyCollection<string> used) : IActiveFontDocument
    {
        public IFontDocument? Current => document;

        public IReadOnlyCollection<string> UsedFamilies => used;
    }

    private sealed class FixedUsed(params string[] families) : IUsedFonts
    {
        public IReadOnlyCollection<string> Of(object? model) => families;
    }

    [Fact]
    public async Task IncorporaICaratteriScaricatiERifiutaQuelliRiservati()
    {
        var free = new EmbeddedFontFile("Libero", 400, false, "l.ttf", new SyntheticFont { Family = "Libero" }.Build());
        var reserved = new EmbeddedFontFile("Riservato", 400, false, "r.ttf", new SyntheticFont { Family = "Riservato", FsType = 0x0002 }.Build());
        var source = new MultiSource(new()
        {
            ["Libero"] = (Downloaded("Libero"), [free]),
            ["Riservato"] = (Downloaded("Riservato"), [reserved]),
            ["Sistema"] = (new FontFamilyInfo { Family = "Sistema", SourceId = "multi", Origin = FontOrigin.System, Faces = [new FontFaceRef(400, false, "/s.ttf")] }, [free with { Family = "Sistema" }]),
        });
        var document = new FakeDocument();
        var embedder = new DocumentFontEmbedder(new CompositeFontCatalog([source]), new FixedUsed("Libero", "Riservato", "Sistema", "Ignoto"), new OpenTypeReader());

        var report = await embedder.PrepareAsync(document, TestContext.Current.CancellationToken);

        Assert.Equal(["Libero"], report.Embedded);
        Assert.Equal(["Riservato"], report.Restricted);
        Assert.Equal(["Libero"], document.Fonts.Families);

        document.Fonts = document.Fonts with { EmbedSystemFonts = true };
        report = await embedder.PrepareAsync(document, TestContext.Current.CancellationToken);
        Assert.Equal(["Libero", "Sistema"], report.Embedded);
    }

    [Fact]
    public async Task UnaCollezioneConUnaFacciaRiservataNonSiIncorpora()
    {
        var collection = new SyntheticFont { Family = "Misto" }.BuildCollection(new SyntheticFont { Family = "Misto", Weight = 700, FsType = 0x0002 });
        var file = new EmbeddedFontFile("Misto", 400, false, "m.ttc", collection);
        var source = new MultiSource(new() { ["Misto"] = (Downloaded("Misto"), [file]) });
        var document = new FakeDocument();
        var embedder = new DocumentFontEmbedder(new CompositeFontCatalog([source]), new FixedUsed("Misto"), new OpenTypeReader());

        var report = await embedder.PrepareAsync(document, TestContext.Current.CancellationToken);

        Assert.Equal(["Misto"], report.Restricted);
        Assert.Empty(document.Fonts.Families);
    }

    [Fact]
    public async Task IFileGiaIncorporatiRestanoSeIlCarattereEAncoraUsato()
    {
        var kept = new EmbeddedFontFile("Vecchio", 400, false, "v.ttf", [1, 2, 3]);
        var unused = new EmbeddedFontFile("Inutile", 400, false, "u.ttf", [1, 2, 3]);
        var document = new FakeDocument { Fonts = new DocumentFonts(false, [kept, unused]) };
        var embedder = new DocumentFontEmbedder(new CompositeFontCatalog([]), new FixedUsed("Vecchio"), new OpenTypeReader());

        await embedder.PrepareAsync(document, TestContext.Current.CancellationToken);

        Assert.Equal(["Vecchio"], document.Fonts.Families);
    }

    private static FontFamilyInfo Downloaded(string name) => new()
    {
        Family = name,
        SourceId = "multi",
        Origin = FontOrigin.Downloaded,
        Faces = [new FontFaceRef(400, false, "x")],
    };

    private sealed class MultiSource(Dictionary<string, (FontFamilyInfo Info, IReadOnlyList<EmbeddedFontFile> Files)> entries) : IFontSource
    {
        public string Id => "multi";

        public FontOrigin Origin => FontOrigin.Online;

        public Task<IReadOnlyList<FontFamilyInfo>> ListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<FontFamilyInfo>>([.. entries.Values.Select(entry => entry.Info)]);

        public Task<OperationResult<IReadOnlyList<EmbeddedFontFile>>> FetchAsync(FontFamilyInfo family, IProgress<FontProgress>? progress = null, CancellationToken ct = default) =>
            Task.FromResult(OperationResult<IReadOnlyList<EmbeddedFontFile>>.Ok(entries[family.Family].Files));
    }
}

public class SystemSourceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"quad-system-fonts-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        GC.SuppressFinalize(this);
    }

    private sealed class Folders(string root) : PulseOffice.Platform.ISystemFontDirectories
    {
        public IReadOnlyList<string> Directories => [root];
    }

    private sealed class Names(params string[] names) : IInstalledFontNames
    {
        public IReadOnlyList<string> Families => names;
    }

    [Fact]
    public async Task ScandisceLeCartelleRaggruppaPerFamigliaELeggeLaLicenza()
    {
        Directory.CreateDirectory(Path.Combine(_root, "sotto"));
        File.WriteAllBytes(Path.Combine(_root, "regular.ttf"), new SyntheticFont().Build());
        File.WriteAllBytes(Path.Combine(_root, "sotto", "bold.otf"), new SyntheticFont { Weight = 700 }.Build());
        File.WriteAllBytes(Path.Combine(_root, "collezione.ttc"), new SyntheticFont { Family = "Riservato", FsType = 2, LicenseDescription = "All rights reserved.", LicenseUrl = null }
            .BuildCollection(new SyntheticFont { Family = "Riservato", Weight = 700, FsType = 2 }));
        File.WriteAllText(Path.Combine(_root, "rotto.ttf"), "rotto");
        File.WriteAllText(Path.Combine(_root, "leggimi.txt"), "x");

        var source = new SystemFontSource(new Folders(_root), new Names("Prova Sans", "Solo Avalonia"), new OpenTypeReader(), new FontFileExtractor(),
            new FontLicenseClassifier(), new FontCategoryClassifier(), new FontScriptCoverage(), new ClassicFaceSelector());

        var families = await source.ListAsync(TestContext.Current.CancellationToken);

        var prova = families.Single(family => family.Family == "Prova Sans");
        Assert.Equal(2, prova.Faces.Count);
        Assert.True(prova.License.IsFree);
        Assert.Equal(EmbeddingLevel.Installable, prova.Embedding.Level);
        var reserved = families.Single(family => family.Family == "Riservato");
        Assert.Equal(LicenseFreedom.Restricted, reserved.License.Freedom);
        Assert.False(reserved.Embedding.AllowsEmbedding);
        Assert.Contains(families, family => family.Family == "Solo Avalonia");

        var fetched = await source.FetchAsync(reserved, null, TestContext.Current.CancellationToken);
        Assert.Equal(2, fetched.Value!.Count);
        Assert.Equal("Riservato", new OpenTypeReader().Read(fetched.Value![1].Data).Single().Family);
    }
}
