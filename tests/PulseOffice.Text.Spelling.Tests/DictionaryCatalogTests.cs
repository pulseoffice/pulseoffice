using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Platform;
using PulseOffice.Text.Spelling.Dictionaries;
using PulseOffice.Text.Spelling.Hunspell;
using PulseOffice.Text.Spelling.Hunspell.Catalog;
using Xunit;

namespace PulseOffice.Text.Spelling.Tests;

public sealed class DictionaryCatalogTests : IDisposable
{
    private const string TexHeader = """
        % title: Hyphenation patterns for Testish
        % copyright: Copyright (C) 2020 Someone
        % language:
        %     name: Testish
        %     tag: zz
        % licence:
        %     - This file is available under any of the following licences:
        %     -
        %         name: LPPL
        %         version: 1.3
        %         or_later: true
        %     -
        %         name: MIT
        %         url: https://opensource.org/licenses/MIT
        % hyphenmins:
        %     typesetting:
        %         left: 2
        \patterns{
        """;

    private readonly string _root = Directory.CreateTempSubdirectory("dictionary-catalog").FullName;

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private sealed class FakeServer : HttpMessageHandler
    {
        public Dictionary<string, string> Text { get; } = new(StringComparer.Ordinal);

        public List<string> Requests { get; } = [];

        public bool Down { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();
            Requests.Add(url);
            Assert.Contains("PulseOffice", request.Headers.UserAgent.ToString(), StringComparison.Ordinal);

            if (Down) throw new HttpRequestException("offline");
            return Task.FromResult(Text.TryGetValue(url, out var body)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8) }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    private sealed class Locations(string user) : IDictionaryLocations
    {
        public IReadOnlyList<string> Folders => [user, .. SystemFolders];

        public IReadOnlyList<string> SystemFolders { get; init; } = [];

        public string UserFolder => user;
    }

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static readonly DictionaryServiceOptions Options = new()
    {
        Spelling = new DictionaryEndpoint(true, new Uri("https://dict.test/listing"), new Uri("https://raw.test/spell/")),
        Hyphenation = new DictionaryEndpoint(true, new Uri("https://dict.test/hyph"), new Uri("https://raw.test/hyph/")),
    };

    private FakeServer Server()
    {
        var server = new FakeServer();
        server.Text["https://dict.test/listing"] = """
            [{"name":"zz","type":"dir","sha":"v1"},{"name":"zz-YY","type":"dir","sha":"v2"},{"name":"index.js","type":"file","sha":"x"}]
            """;
        server.Text["https://raw.test/spell/dictionaries/zz/package.json"] = """{"license":"MIT","repository":"https://example.test/zz"}""";
        server.Text["https://raw.test/spell/dictionaries/zz/index.aff"] = "SET UTF-8\n";
        server.Text["https://raw.test/spell/dictionaries/zz/index.dic"] = "2\ncasa\ncane\n";
        server.Text["https://raw.test/spell/dictionaries/zz/license"] = "MIT licence text";
        server.Text["https://raw.test/spell/dictionaries/zz-YY/package.json"] = """{"license":"(GPL-2.0 OR LGPL-2.1 OR MPL-1.1)"}""";
        server.Text["https://dict.test/hyph"] = """
            [{"name":"hyph-zz.pat.txt","sha":"p1"},{"name":"hyph-zz.hyp.txt","sha":"e1"},{"name":"hyph-la-x-classic.pat.txt","sha":"q"}]
            """;
        server.Text["https://raw.test/hyph/tex/hyph-zz.tex"] = TexHeader;
        server.Text["https://raw.test/hyph/txt/hyph-zz.pat.txt"] = "1ba\n1ca\n";
        server.Text["https://raw.test/hyph/txt/hyph-zz.hyp.txt"] = "cas-sa-pan-ca\n";
        return server;
    }

    private (DictionaryManager Manager, DictionaryChanges Changes, Locations Locations) Build(FakeServer server, Clock? clock = null, string? systemFolder = null)
    {
        var http = new DictionaryHttp(new HttpClient(server));
        var codes = new DictionaryCodes();
        var licenses = new DictionaryLicenseClassifier();
        var store = new DictionaryCacheStore(Path.Combine(_root, "cache"));
        var time = clock ?? new Clock(DateTimeOffset.UtcNow);
        var locations = new Locations(Path.Combine(_root, "user")) { SystemFolders = systemFolder is null ? [] : [systemFolder] };
        var changes = new DictionaryChanges();
        IRefreshableDictionarySource[] sources =
        [
            new CachedDictionarySource(new WooormSpellingSource(http, codes, licenses, Options), store, Options, time, codes),
            new CachedDictionarySource(new TexHyphenationSource(http, codes, licenses, Options, new TexHyphenationConverter(), new TexLicenseReader()), store, Options, time, codes),
        ];

        var manager = new DictionaryManager(
            sources, new InstalledDictionaries(locations), new SystemDictionaryInventory(locations), codes, changes, new DictionaryRanking(), NullLogger<DictionaryManager>.Instance);
        return (manager, changes, locations);
    }

    [Fact]
    public void LeCartelleDiSistemaNonCercanoNelleAltreSuite()
    {
        var folders = new SystemDictionaryDirectories().Directories;

        Assert.NotEmpty(folders);
        Assert.DoesNotContain(folders, folder => folder.Contains("libreoffice", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(folders, folder => folder.Contains("openoffice", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("en-gb", "en-GB")]
    [InlineData("de-1996", "de")]
    [InlineData("sr-cyrl", "sr-Cyrl")]
    [InlineData("el-monoton", "el")]
    [InlineData("pt_BR", "pt-BR")]
    [InlineData("pt-PT", "pt-PT")]
    [InlineData("en-us", "en")]
    [InlineData("en_US", "en")]
    [InlineData("de-DE", "de")]
    [InlineData("de-CH", "de-CH")]
    [InlineData("it-IT", "it")]
    [InlineData("es-MX", "es-MX")]
    [InlineData("sr-Latn-RS", "sr-Latn-RS")]
    [InlineData("la-x-classic", null)]
    [InlineData("de-1901", null)]
    [InlineData("el-polyton", null)]
    [InlineData("index.js", null)]
    public void ICodiciSiNormalizzano(string raw, string? expected) =>
        Assert.Equal(expected, new DictionaryCodes().Normalize(raw));

    [Theory]
    [InlineData("MIT", DictionaryFreedom.Free)]
    [InlineData("GPL-3.0", DictionaryFreedom.Copyleft)]
    [InlineData("(GPL-2.0 OR LGPL-2.1 OR MPL-1.1)", DictionaryFreedom.Copyleft)]
    [InlineData("(MIT OR GPL-3.0)", DictionaryFreedom.Free)]
    [InlineData("MIT AND GPL-2.0", DictionaryFreedom.Copyleft)]
    [InlineData("CC-BY-SA-4.0", DictionaryFreedom.Copyleft)]
    [InlineData("LPPL-1.3", DictionaryFreedom.Free)]
    [InlineData("Proprietary", DictionaryFreedom.Unknown)]
    [InlineData(null, DictionaryFreedom.Unknown)]
    public void LeLicenzeSiClassificano(string? spdx, DictionaryFreedom expected) =>
        Assert.Equal(expected, new DictionaryLicenseClassifier().FromSpdx(spdx).Freedom);

    [Fact]
    public void LaLicenzaDiHyphUtf8SiLeggeDallIntestazione() =>
        Assert.Equal("MIT OR LPPL-1.3", new TexLicenseReader().Read(TexHeader));

    [Fact]
    public void IModelliTeXDiventanoUnFileLibhyphenConLeEccezioni()
    {
        var converted = new TexHyphenationConverter().Convert("% comment\n1ba 1ca\n", "cas-sa\n");
        var patterns = HyphenationPatterns.Parse(LanguageTag.Parse("zz"), converted.Split('\n'));

        Assert.StartsWith("UTF-8\n", converted, StringComparison.Ordinal);
        Assert.Equal([1, 3], patterns.Positions("abaca", 1));
        Assert.Equal([3], patterns.Positions("cassa", 1));
    }

    [Fact]
    public async Task IlCatalogoUnisceOrtografiaESillabazioneConLeLicenze()
    {
        var (manager, _, _) = Build(Server());

        var catalog = await manager.CatalogAsync(ct: TestContext.Current.CancellationToken);

        Assert.False(catalog.Offline);
        var zz = Assert.Single(catalog.Languages, language => language.Code == "zz");
        Assert.Equal(DictionaryStatus.Available, zz[DictionaryPart.Spelling].Status);
        Assert.Equal(DictionaryStatus.Available, zz[DictionaryPart.Hyphenation].Status);
        Assert.Equal(DictionaryStatus.Unavailable, zz[DictionaryPart.Thesaurus].Status);
        Assert.Equal(DictionaryFreedom.Free, zz[DictionaryPart.Spelling].Package!.License.Freedom);
        Assert.Equal("MIT OR LPPL-1.3", zz[DictionaryPart.Hyphenation].Package!.License.Spdx);
        var regional = Assert.Single(catalog.Languages, language => language.Code == "zz-YY");
        Assert.Equal(DictionaryFreedom.Copyleft, regional[DictionaryPart.Spelling].Package!.License.Freedom);
        Assert.DoesNotContain(catalog.Languages, language => language.Code.StartsWith("la", StringComparison.Ordinal));
    }

    [Fact]
    public async Task InstallareRendeAttiviOrtografiaESillabazioneSenzaRiavvio()
    {
        var (manager, changes, locations) = Build(Server());
        var spelling = new HunspellDictionaryProvider(locations, NullLogger<HunspellDictionaryProvider>.Instance, changes);
        var hyphenation = new FileHyphenationProvider(locations, NullLogger<FileHyphenationProvider>.Instance, new DeclaredEncodingText(), changes);
        var ct = TestContext.Current.CancellationToken;

        Assert.Null(spelling.For(LanguageTag.Parse("zz")));
        Assert.Null(hyphenation.For(LanguageTag.Parse("zz")));

        var catalog = await manager.CatalogAsync(ct: ct);
        var zz = catalog.Languages.Single(language => language.Code == "zz");
        var reported = new List<DictionaryProgress>();
        Assert.True((await manager.InstallAsync(zz[DictionaryPart.Spelling].Package!, new SyncProgress(reported), ct)).Succeeded);
        Assert.True((await manager.InstallAsync(zz[DictionaryPart.Hyphenation].Package!, ct: ct)).Succeeded);

        Assert.Equal(3, reported[^1].Completed);
        Assert.True(spelling.For(LanguageTag.Parse("zz"))!.Check("casa"));
        Assert.Equal([3, 5, 8], hyphenation.For(LanguageTag.Parse("zz"))!.Positions("cassapanca", 1));
        Assert.True(File.Exists(Path.Combine(locations.UserFolder, "zz", "zz.license.txt")));

        var after = (await manager.CatalogAsync(ct: ct)).Languages.Single(language => language.Code == "zz");
        Assert.Equal(DictionaryStatus.Installed, after[DictionaryPart.Spelling].Status);

        Assert.True(manager.Remove("zz", DictionaryPart.Spelling).Succeeded);
        Assert.Null(spelling.For(LanguageTag.Parse("zz")));
        Assert.False(Directory.Exists(Path.Combine(locations.UserFolder, "zz", "zz.aff")));
        Assert.False(manager.Remove("zz", DictionaryPart.Spelling).Succeeded);
    }

    [Fact]
    public async Task UnaVersioneNuovaNelCatalogoSegnalaLAggiornamento()
    {
        var server = Server();
        var clock = new Clock(DateTimeOffset.UtcNow);
        var (manager, _, _) = Build(server, clock);
        var ct = TestContext.Current.CancellationToken;
        var zz = (await manager.CatalogAsync(ct: ct)).Languages.Single(language => language.Code == "zz");
        await manager.InstallAsync(zz[DictionaryPart.Spelling].Package!, ct: ct);

        server.Text["https://dict.test/listing"] = """[{"name":"zz","type":"dir","sha":"v9"}]""";
        var cached = (await manager.CatalogAsync(ct: ct)).Languages.Single(language => language.Code == "zz");
        Assert.Equal(DictionaryStatus.Installed, cached[DictionaryPart.Spelling].Status);

        clock.Now = clock.Now.AddDays(8);
        var refreshed = (await manager.CatalogAsync(ct: ct)).Languages.Single(language => language.Code == "zz");
        Assert.Equal(DictionaryStatus.UpdateAvailable, refreshed[DictionaryPart.Spelling].Status);
    }

    [Fact]
    public async Task SenzaReteSiUsaLUltimoCatalogoSalvato()
    {
        var server = Server();
        var (manager, _, _) = Build(server);
        var ct = TestContext.Current.CancellationToken;
        await manager.CatalogAsync(ct: ct);

        server.Down = true;
        var offline = await manager.CatalogAsync(refresh: true, ct);

        Assert.True(offline.Offline);
        Assert.Contains(offline.Languages, language => language.Code == "zz" && language[DictionaryPart.Spelling].Package is not null);
    }

    [Fact]
    public async Task UnDizionarioDelSistemaRisultaNelSistema()
    {
        var system = Directory.CreateDirectory(Path.Combine(_root, "system")).FullName;
        File.WriteAllText(Path.Combine(system, "zz.aff"), "SET UTF-8\n");
        File.WriteAllText(Path.Combine(system, "zz.dic"), "1\ncasa\n");
        File.WriteAllText(Path.Combine(system, "th_qq_v2.dat"), "UTF-8\n");
        var (manager, _, _) = Build(Server(), systemFolder: system);

        var catalog = await manager.CatalogAsync(ct: TestContext.Current.CancellationToken);

        Assert.Equal(DictionaryStatus.OnSystem, catalog.Languages.Single(language => language.Code == "zz")[DictionaryPart.Spelling].Status);
        Assert.Equal(DictionaryStatus.OnSystem, catalog.Languages.Single(language => language.Code == "qq")[DictionaryPart.Thesaurus].Status);
    }

    [Fact]
    public async Task IlDownloadInHttpSiRifiuta()
    {
        var http = new DictionaryHttp(new HttpClient(Server()));

        await Assert.ThrowsAsync<InvalidDataException>(() => http.BytesAsync(new Uri("http://raw.test/x"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void LeOpzioniAccettanoSoloHttps()
    {
        var path = Path.Combine(_root, "dictionaries.json");
        File.WriteAllText(path, """{"spelling":{"listing":"http://evil.test/x","files":"https://mirror.test/"},"hyphenation":{"enabled":false}}""");

        var options = new FileDictionaryServiceOptionsStore(path).Load();

        Assert.Equal(new DictionaryServiceOptions().Spelling.Listing, options.Spelling.Listing);
        Assert.Equal(new Uri("https://mirror.test/"), options.Spelling.Files);
        Assert.False(options.Hyphenation.Enabled);
    }

    [Fact]
    public void LAvvisoFsfInteramentePermissivoEUnaLicenzaLibera()
    {
        const string header = """
            % title: Hyphenation patterns for American English
            % licence:
            %     text: >
            %         Copying and distribution of this file, with or without modification,
            %         are permitted in any medium without royalty provided the copyright
            %         notice and this notice are preserved.
            % hyphenmins:
            """;

        var spdx = new TexLicenseReader().Read(header);

        Assert.Equal("FSFAP", spdx);
        Assert.Equal(DictionaryFreedom.Free, new DictionaryLicenseClassifier().FromSpdx(spdx).Freedom);
        Assert.Null(new TexLicenseReader().Read("% licence:\n%     text: >\n%         Some other words.\n"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnCatalogoSalvatoInUnFormatoVecchioSiScarta(bool offline)
    {
        var server = Server();
        var store = new DictionaryCacheStore(Path.Combine(_root, "cache"));
        var stale = new DictionaryPackage(WooormSpellingSource.SourceId, "zz-OLD", DictionaryPart.Spelling, "v0", DictionaryLicense.Unknown, [], "old");
        store.Save(WooormSpellingSource.SourceId, new CachedListing(DateTimeOffset.UtcNow, [stale]));
        server.Down = offline;
        var (manager, _, _) = Build(server);

        var catalog = await manager.CatalogAsync(ct: TestContext.Current.CancellationToken);

        Assert.DoesNotContain(catalog.Languages, language => language.Code == "zz-OLD");
        Assert.Equal(offline, catalog.Offline);
        Assert.Equal(!offline, catalog.Languages.Any(language => language.Code == "zz"));
    }

    private sealed class SyncProgress(List<DictionaryProgress> sink) : IProgress<DictionaryProgress>
    {
        public void Report(DictionaryProgress value) => sink.Add(value);
    }

    [Fact]
    public void LaRicercaDeiFileNonSegueICollegamentiACiclo()
    {
        var system = Directory.CreateDirectory(Path.Combine(_root, "ciclo")).FullName;
        Directory.CreateSymbolicLink(Path.Combine(system, "giro"), system);
        File.WriteAllText(Path.Combine(system, "hyph_xx_YY.dic"), "UTF-8\n");
        var locations = new Locations(Path.Combine(_root, "user")) { SystemFolders = [system] };

        Assert.Null(ResourceFiles.Find(locations, "hyph", ".dic", LanguageTag.Parse("it-IT")));
    }
}
