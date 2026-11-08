using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Text.Spelling.Dictionaries;
using PulseOffice.Text.Spelling.Hunspell;
using PulseOffice.Text.Spelling.Hunspell.Catalog;
using Xunit;

namespace PulseOffice.Text.Spelling.Tests;

public sealed class DictionarySourcesTests : IDisposable
{
    private const string WordNet = """
        <?xml version="1.0" encoding="UTF-8"?>
        <!DOCTYPE LexicalResource SYSTEM "http://globalwordnet.github.io/schemas/WN-LMF-1.3.dtd">
        <LexicalResource>
          <Lexicon id="t" language="en">
            <LexicalEntry id="e-car"><Lemma writtenForm="car" partOfSpeech="n"/><Sense id="s1" synset="y-car"/></LexicalEntry>
            <LexicalEntry id="e-auto"><Lemma writtenForm="automobile" partOfSpeech="n"/><Sense id="s2" synset="y-car"/></LexicalEntry>
            <LexicalEntry id="e-vehicle"><Lemma writtenForm="vehicle" partOfSpeech="n"/><Sense id="s3" synset="y-vehicle"/></LexicalEntry>
            <LexicalEntry id="e-lonely"><Lemma writtenForm="lonely" partOfSpeech="a"/><Sense id="s4" synset="y-lonely"/></LexicalEntry>
            <Synset id="y-car" members="e-car e-auto" partOfSpeech="n">
              <Definition>a motor vehicle</Definition>
              <SynsetRelation relType="hypernym" target="y-vehicle"/>
            </Synset>
            <Synset id="y-vehicle" members="e-vehicle" partOfSpeech="n"/>
            <Synset id="y-lonely" members="e-lonely" partOfSpeech="a"/>
          </Lexicon>
        </LexicalResource>
        """;

    private readonly string _root = Directory.CreateTempSubdirectory("dictionary-sources").FullName;

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private sealed class FakeServer : HttpMessageHandler
    {
        public Dictionary<string, byte[]> Content { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, string> Tags { get; } = new(StringComparer.Ordinal);

        public List<string> Requests { get; } = [];

        public void Text(string url, string body) => Content[url] = Encoding.UTF8.GetBytes(body);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();
            Requests.Add($"{request.Method} {url}");

            if (!Content.TryGetValue(url, out var body)) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = request.Method == HttpMethod.Head ? new ByteArrayContent([]) : new ByteArrayContent(body),
            };
            if (Tags.TryGetValue(url, out var tag)) response.Headers.ETag = new EntityTagHeaderValue(tag);
            return Task.FromResult(response);
        }
    }

    private sealed class Locations(string user) : IDictionaryLocations
    {
        public IReadOnlyList<string> Folders => [user];

        public IReadOnlyList<string> SystemFolders => [];

        public string UserFolder => user;
    }

    private static readonly DictionaryServiceOptions Options = new()
    {
        Spelling = new DictionaryEndpoint(true, new Uri("https://wooorm.test/listing"), new Uri("https://wooorm.test/raw/")),
        Hyphenation = new DictionaryEndpoint(false, new Uri("https://hyph.test/"), new Uri("https://hyph.test/")),
        Mozilla = new DictionaryEndpoint(true, new Uri("https://amo.test/api/language-tools/"), new Uri("https://amo.test/api/search/?page=1")),
        WordNet = new DictionaryEndpoint(true, new Uri("https://api.test/releases/latest"), new Uri("https://files.test/download/")),
        OpenThesaurus = new DictionaryEndpoint(true, new Uri("https://ot.test/export/"), new Uri("https://ot.test/export/")),
    };

    private static byte[] Zip(params (string Name, string Text)[] entries)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, text) in entries)
            {
                using var stream = zip.CreateEntry(name).Open();
                stream.Write(Encoding.UTF8.GetBytes(text));
            }
        }

        return buffer.ToArray();
    }

    private static byte[] Gzip(string text)
    {
        using var buffer = new MemoryStream();
        using (var gzip = new GZipStream(buffer, CompressionMode.Compress, leaveOpen: true)) gzip.Write(Encoding.UTF8.GetBytes(text));
        return buffer.ToArray();
    }

    private FakeServer Server()
    {
        var server = new FakeServer();
        server.Text("https://wooorm.test/listing", """[{"name":"zz","type":"dir","sha":"v1"}]""");
        server.Text("https://wooorm.test/raw/dictionaries/zz/package.json", """{"license":"GPL-3.0"}""");

        server.Text("https://amo.test/api/language-tools/", """
            {"results":[{"id":1,"target_locale":"zz"},{"id":2,"target_locale":"zz"},{"id":3,"target_locale":"qq-QQ"},{"id":4,"target_locale":"ca-valencia"}]}
            """);
        server.Text("https://amo.test/api/search/?page=1", """
            {"next":"https://amo.test/api/search/?page=2","results":[
              {"id":1,"average_daily_users":10,"name":{"en-US":"Small"},"url":"https://amo.test/small",
               "current_version":{"version":"1.0","license":{"slug":"GPL-2.0-only","url":"https://spdx.test/gpl"},"file":{"id":11,"size":100,"url":"https://amo.test/small.xpi"}}},
              {"id":2,"average_daily_users":500,"name":{"en-US":"Zz dictionary"},"url":"https://amo.test/zz",
               "current_version":{"version":"2.0","license":{"slug":"MIT","url":"https://spdx.test/mit"},"file":{"id":12,"size":100,"url":"https://amo.test/zz.xpi"}}}
            ]}
            """);
        server.Text("https://amo.test/api/search/?page=2", """
            {"next":"https://evil.test/api/search/?page=3","results":[
              {"id":3,"average_daily_users":5,"name":"Q","current_version":{"version":"3","license":null,"file":{"id":13,"size":100,"url":"http://amo.test/q.xpi"}}},
              {"id":99,"average_daily_users":5,"name":"Theme","current_version":{"version":"3","file":{"id":14,"size":100,"url":"https://amo.test/t.xpi"}}}
            ]}
            """);
        server.Content["https://amo.test/zz.xpi"] = Zip(
            ("manifest.json", """{"dictionaries":{"zz":"dictionaries/zz_ZZ.dic"}}"""),
            ("dictionaries/zz_ZZ.dic", "2\ncasa\ncane\n"),
            ("dictionaries/zz_ZZ.aff", "SET UTF-8\n"),
            ("../escape.dic", "1\nevil\n"));

        server.Text("https://api.test/releases/latest", """
            {"tag_name":"2025-edition","assets":[
              {"name":"english-wordnet-2025-plus.xml.gz","browser_download_url":"https://files.test/download/plus.xml.gz"},
              {"name":"english-wordnet-2025.xml.gz","browser_download_url":"https://files.test/download/2025/english-wordnet-2025.xml.gz"}]}
            """);
        server.Content["https://files.test/download/2025/english-wordnet-2025.xml.gz"] = Gzip(WordNet);

        server.Content["https://ot.test/export/Deutscher-Thesaurus.oxt"] = Zip(
            ("th_de_DE_v2.dat", "UTF-8\nhaus|1\n-|Gebäude|Heim\n"),
            ("th_de_DE_v2.idx", "UTF-8\n1\nhaus|6\n"),
            ("README.txt", "OpenThesaurus licence"));
        server.Tags["https://ot.test/export/Deutscher-Thesaurus.oxt"] = "\"v1\"";
        return server;
    }

    private (DictionaryManager Manager, Locations Locations, DictionaryChanges Changes) Build(FakeServer server)
    {
        var http = new DictionaryHttp(new HttpClient(server));
        var codes = new DictionaryCodes();
        var licenses = new DictionaryLicenseClassifier();
        var archive = new DictionaryArchive();
        var store = new DictionaryCacheStore(Path.Combine(_root, "cache"));
        var locations = new Locations(Path.Combine(_root, "user"));
        var changes = new DictionaryChanges();
        IDictionarySource[] inner =
        [
            new WooormSpellingSource(http, codes, licenses, Options),
            new MozillaSpellingSource(http, codes, licenses, archive, Options),
            new WordNetThesaurusSource(http, codes, licenses, new WordNetThesaurusConverter(), new MyThesWriter(), Options),
            new OpenThesaurusSource(http, codes, licenses, archive, Options),
        ];

        var manager = new DictionaryManager(
            [.. inner.Select(source => (IRefreshableDictionarySource)new CachedDictionarySource(source, store, Options, TimeProvider.System, codes))],
            new InstalledDictionaries(locations),
            new SystemDictionaryInventory(locations),
            codes,
            changes,
            new DictionaryRanking(),
            NullLogger<DictionaryManager>.Instance);
        return (manager, locations, changes);
    }

    [Fact]
    public void WordNetDiventaUnDizionarioDeiSinonimiMyThes()
    {
        var entries = new WordNetThesaurusConverter().Convert(new MemoryStream(Encoding.UTF8.GetBytes(WordNet)));
        var files = new MyThesWriter().Write(entries);
        var lines = Encoding.UTF8.GetString(files.Data).Split('\n');

        var thesaurus = MyThesThesaurus.Parse(LanguageTag.Parse("en"), lines);

        var car = Assert.Single(thesaurus.Lookup("Car"));
        Assert.Equal("noun", car.PartOfSpeech);
        Assert.Equal(["automobile", "vehicle (generic term)"], car.Synonyms);
        Assert.Empty(thesaurus.Lookup("lonely"));
        Assert.Equal("UTF-8", lines[0]);

        var index = Encoding.UTF8.GetString(files.Index).Split('\n');
        Assert.Equal("2", index[1]);
        var automobile = index.Single(line => line.StartsWith("automobile|", StringComparison.Ordinal));
        var offset = int.Parse(automobile.Split('|')[1], System.Globalization.CultureInfo.InvariantCulture);
        Assert.StartsWith("automobile|1", Encoding.UTF8.GetString(files.Data, offset, 12), StringComparison.Ordinal);
    }

    [Fact]
    public void LArchivioScartaIPercorsiPericolosiEITroppoGrandi()
    {
        var archive = new DictionaryArchive(new DictionaryArchiveLimits(10, 8, 64));
        var data = Zip(("ok.txt", "1234"), ("../evil.txt", "x"), ("/abs.txt", "x"), ("a/../b.txt", "x"), ("c:x.txt", "x"));

        var entries = archive.Extract(data, _ => true);
        Assert.Equal(["ok.txt"], entries.Keys);

        Assert.Throws<InvalidDataException>(() => archive.Extract(Zip(("big.txt", "123456789")), _ => true));
        Assert.Throws<InvalidDataException>(() => new DictionaryArchive(new DictionaryArchiveLimits(1, 8, 64)).Extract(Zip(("a", "1"), ("b", "2")), _ => true));
        Assert.Throws<InvalidDataException>(() => archive.Extract([1, 2, 3, 4], _ => true));
    }

    [Fact]
    public async Task IlCatalogoUnisceLeFontiPerLinguaConLaPreferita()
    {
        var server = Server();
        var (manager, _, _) = Build(server);

        var catalog = await manager.CatalogAsync(ct: TestContext.Current.CancellationToken);

        var zz = catalog.Languages.Single(language => language.Code == "zz")[DictionaryPart.Spelling];
        Assert.Equal(2, zz.Alternatives.Count);
        Assert.Equal(MozillaSpellingSource.SourceId, zz.Package!.SourceId);
        Assert.Equal("2.0", zz.Package.Version);
        Assert.Equal(DictionaryFreedom.Free, zz.Package.License.Freedom);
        Assert.Equal(WooormSpellingSource.SourceId, zz.Alternatives[1].SourceId);
        Assert.DoesNotContain(catalog.Languages, language => language.Code == "qq-QQ");
        Assert.DoesNotContain(server.Requests, request => request.Contains("evil.test", StringComparison.Ordinal));

        var english = catalog.Languages.Single(language => language.Code == "en")[DictionaryPart.Thesaurus];
        Assert.Equal("2025-edition", english.Package!.Version);
        Assert.Equal(DictionaryFreedom.Free, english.Package.License.Freedom);

        var german = catalog.Languages.Single(language => language.Code == "de")[DictionaryPart.Thesaurus];
        Assert.Equal("\"v1\"", german.Package!.Version);
        Assert.Equal(DictionaryFreedom.Copyleft, german.Package.License.Freedom);
        Assert.DoesNotContain(catalog.Languages, language => language.Code == "de-CH");
    }

    [Fact]
    public async Task InstallareDaMozillaEstraeIlDizionarioHunspell()
    {
        var (manager, locations, changes) = Build(Server());
        var ct = TestContext.Current.CancellationToken;
        var spelling = new HunspellDictionaryProvider(locations, NullLogger<HunspellDictionaryProvider>.Instance, changes);

        var zz = (await manager.CatalogAsync(ct: ct)).Languages.Single(language => language.Code == "zz")[DictionaryPart.Spelling];
        Assert.True((await manager.InstallAsync(zz.Package!, ct: ct)).Succeeded);

        Assert.True(spelling.For(LanguageTag.Parse("zz"))!.Check("cane"));
        Assert.False(File.Exists(Path.Combine(locations.UserFolder, "escape.dic")));
        Assert.Equal(["zz.aff", "zz.dic", "zz.license.txt"], Directory.GetFiles(Path.Combine(locations.UserFolder, "zz")).Select(Path.GetFileName).Order(StringComparer.Ordinal));

        var after = (await manager.CatalogAsync(ct: ct)).Languages.Single(language => language.Code == "zz")[DictionaryPart.Spelling];
        Assert.Equal(DictionaryStatus.Installed, after.Status);
        Assert.Equal(MozillaSpellingSource.SourceId, after.Installed!.SourceId);
    }

    [Fact]
    public async Task ISinonimiInstallatiFunzionanoSubito()
    {
        var (manager, locations, changes) = Build(Server());
        var ct = TestContext.Current.CancellationToken;
        var thesauri = new FileThesaurusProvider(locations, NullLogger<FileThesaurusProvider>.Instance, new DeclaredEncodingText(), changes);
        Assert.Null(thesauri.For(LanguageTag.Parse("en-US")));

        var catalog = await manager.CatalogAsync(ct: ct);
        Assert.True((await manager.InstallAsync(catalog.Languages.Single(language => language.Code == "en")[DictionaryPart.Thesaurus].Package!, ct: ct)).Succeeded);
        Assert.True((await manager.InstallAsync(catalog.Languages.Single(language => language.Code == "de")[DictionaryPart.Thesaurus].Package!, ct: ct)).Succeeded);

        Assert.Contains("automobile", thesauri.For(LanguageTag.Parse("en-US"))!.Lookup("car").Single().Synonyms);
        Assert.Equal(["Gebäude", "Heim"], thesauri.For(LanguageTag.Parse("de-DE"))!.Lookup("Haus").Single().Synonyms);
        Assert.True(File.Exists(Path.Combine(locations.UserFolder, "en", "th_en_v2.idx")));
        Assert.Equal("OpenThesaurus licence", File.ReadAllText(Path.Combine(locations.UserFolder, "de", "th_de.license.txt")));
    }

    [Fact]
    public void LeOpzioniDelleFontiNuoveAccettanoSoloHttps()
    {
        var path = Path.Combine(_root, "dictionaries.json");
        File.WriteAllText(path, """{"mozilla":{"enabled":false},"wordnet":{"listing":"http://evil.test/"},"openthesaurus":{"files":"https://mirror.test/ot/"}}""");

        var options = new FileDictionaryServiceOptionsStore(path).Load();

        Assert.False(options.Mozilla.Enabled);
        Assert.Equal(new DictionaryServiceOptions().WordNet.Listing, options.WordNet.Listing);
        Assert.Equal(new Uri("https://mirror.test/ot/"), options.OpenThesaurus.Files);
    }

    [Fact]
    public void LaClassificaPreferisceLeLicenzeLibereEPoiLaPriorita()
    {
        var classifier = new DictionaryLicenseClassifier();
        DictionaryPackage Package(string source, string spdx) => new(source, "xx", DictionaryPart.Spelling, "1", classifier.FromSpdx(spdx), [], source);

        var ordered = new DictionaryRanking().Order(
            [Package("c", "Custom"), Package("b", "GPL-3.0"), Package("a", "GPL-2.0"), Package("d", "MIT")],
            source => source == "a" ? 30 : 10);

        Assert.Equal(["d", "b", "a", "c"], ordered.Select(package => package.SourceId));
    }

    [Fact]
    public async Task LeVariantiRegionaliSiRaggruppanoEEreditanoLeParti()
    {
        var server = Server();
        server.Text("https://wooorm.test/listing", """[{"name":"zz","type":"dir","sha":"v1"},{"name":"en-GB","type":"dir","sha":"g1"}]""");
        server.Text("https://amo.test/api/language-tools/", """{"results":[{"id":2,"target_locale":"en-US"}]}""");
        var (manager, locations, changes) = Build(server);
        var ct = TestContext.Current.CancellationToken;

        var catalog = await manager.CatalogAsync(ct: ct);

        Assert.DoesNotContain(catalog.Languages, language => language.Code == "en-US");
        var english = catalog.Languages.Single(language => language.Code == "en");
        Assert.Equal(MozillaSpellingSource.SourceId, english[DictionaryPart.Spelling].Package!.SourceId);
        Assert.False(english[DictionaryPart.Thesaurus].Inherited);

        var british = catalog.Languages.Single(language => language.Code == "en-GB");
        Assert.Equal("en", british.Base);
        Assert.False(british[DictionaryPart.Spelling].Inherited);
        Assert.Equal("en-GB", british[DictionaryPart.Spelling].Package!.Code);
        Assert.True(british[DictionaryPart.Thesaurus].Inherited);
        Assert.Equal("en", british[DictionaryPart.Thesaurus].Package!.Code);

        Assert.True((await manager.InstallAsync(british[DictionaryPart.Thesaurus].Package!, ct: ct)).Succeeded);
        var after = (await manager.CatalogAsync(ct: ct)).Languages.Single(language => language.Code == "en-GB")[DictionaryPart.Thesaurus];
        Assert.Equal(DictionaryStatus.Installed, after.Status);
        Assert.True(after.Inherited);

        var thesauri = new FileThesaurusProvider(locations, NullLogger<FileThesaurusProvider>.Instance, new DeclaredEncodingText(), changes);
        Assert.NotNull(thesauri.For(LanguageTag.Parse("en-GB")));
        Assert.NotNull(thesauri.For(LanguageTag.Parse("en-US")));
    }
}
