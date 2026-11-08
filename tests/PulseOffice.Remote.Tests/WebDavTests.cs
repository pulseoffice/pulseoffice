using PulseOffice.Remote;
using Xunit;

namespace PulseOffice.Remote.Tests;

public class WebDavTests
{
    private readonly WebDavResponseReader _reader = new();

    private const string Listing =
        """
        <?xml version="1.0" encoding="utf-8"?>
        <D:multistatus xmlns:D="DAV:">
          <D:response>
            <D:href>/conti/</D:href>
            <D:propstat><D:prop>
              <D:resourcetype><D:collection/></D:resourcetype>
            </D:prop><D:status>HTTP/1.1 200 OK</D:status></D:propstat>
          </D:response>
          <D:response>
            <D:href>/conti/bilancio%202026.ods</D:href>
            <D:propstat><D:prop>
              <D:resourcetype/>
              <D:getcontentlength>4096</D:getcontentlength>
              <D:getlastmodified>Tue, 24 Mar 2026 10:00:00 GMT</D:getlastmodified>
            </D:prop><D:status>HTTP/1.1 200 OK</D:status></D:propstat>
          </D:response>
          <D:response>
            <D:href>/conti/archivio/</D:href>
            <D:propstat><D:prop>
              <D:resourcetype><D:collection/></D:resourcetype>
            </D:prop><D:status>HTTP/1.1 200 OK</D:status></D:propstat>
          </D:response>
        </D:multistatus>
        """;

    [Fact]
    public void LaCartellaChiestaNonCompareFraIProprioContenuti()
    {
        var entries = _reader.ReadListing(Listing, "/conti/");

        Assert.Equal(2, entries.Count);
        Assert.DoesNotContain(entries, entry => entry.Path == "/conti");
    }

    [Fact]
    public void IlNomeDelFileSiLeggeDecodificato()
    {
        var file = _reader.ReadListing(Listing, "/conti/").Single(entry => !entry.IsFolder);

        Assert.Equal("bilancio 2026.ods", file.Name);
        Assert.Equal(4096, file.Size);
        Assert.NotNull(file.Modified);
    }

    [Fact]
    public void UnaRaccoltaSiRiconosceDalTipoDiRisorsa()
    {
        var folder = _reader.ReadListing(Listing, "/conti/").Single(entry => entry.IsFolder);

        Assert.Equal("archivio", folder.Name);
    }

    [Fact]
    public void IPercorsiTornanoRelativiAllaRadiceDellArchivio()
    {
        const string xml =
            """
            <D:multistatus xmlns:D="DAV:">
              <D:response><D:href>/dav/conti/</D:href><D:propstat><D:prop>
                <D:resourcetype><D:collection/></D:resourcetype>
              </D:prop></D:propstat></D:response>
              <D:response><D:href>/dav/conti/bilancio.ods</D:href><D:propstat><D:prop>
                <D:resourcetype/>
              </D:prop></D:propstat></D:response>
            </D:multistatus>
            """;

        var entries = _reader.ReadListing(xml, "/dav/conti/", "/dav/");

        Assert.Equal("/conti/bilancio.ods", Assert.Single(entries).Path);
    }

    [Fact]
    public void UnaRispostaRottaNonFaCadereNulla()
    {
        Assert.Empty(_reader.ReadListing("<non è xml", "/"));
        Assert.Null(_reader.ReadLockToken("<non è xml"));
    }

    [Fact]
    public void IlGettoneDelBloccoSiLeggeDallaRisposta()
    {
        const string response =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <D:prop xmlns:D="DAV:"><D:lockdiscovery><D:activelock>
              <D:locktype><D:write/></D:locktype>
              <D:lockscope><D:exclusive/></D:lockscope>
              <D:locktoken><D:href>opaquelocktoken:e71d4fae-5dec-22d6</D:href></D:locktoken>
            </D:activelock></D:lockdiscovery></D:prop>
            """;

        Assert.Equal("opaquelocktoken:e71d4fae-5dec-22d6", _reader.ReadLockToken(response));
    }

    [Theory]
    [InlineData("https://cloud.example.org/dav", "conti/bilancio.ods", "https://cloud.example.org/dav/conti/bilancio.ods")]
    [InlineData("https://cloud.example.org/dav/", "/conti/", "https://cloud.example.org/dav/conti/")]
    [InlineData("https://cloud.example.org/dav", "", "https://cloud.example.org/dav/")]
    [InlineData("https://cloud.example.org/dav", "conti/report#1 a?b.ods", "https://cloud.example.org/dav/conti/report%231%20a%3Fb.ods")]
    public void LIndirizzoSiComponeSenzaBarreDoppie(string url, string path, string expected)
    {
        var connection = new RemoteConnection { Name = "nuvola", Url = url };

        Assert.Equal(expected, new WebDavPaths().Address(connection, path));
    }

    [Fact]
    public void LaPasswordNonStaNellaConnessione()
    {
        var connection = new RemoteConnection { Name = "nuvola", Url = "https://x", User = "ada" };

        var json = System.Text.Json.JsonSerializer.Serialize(connection);

        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("remote:nuvola", connection.SecretKey);
    }

    [Fact]
    public void LeConnessioniSiSalvanoESiRileggono()
    {
        var path = Path.Combine(Directory.CreateTempSubdirectory().FullName, "remote.json");

        var directory = new FileRemoteConnectionDirectory(path);
        directory.Save(new RemoteConnection { Name = "nuvola", Url = "https://x/dav", User = "ada" });

        var reread = new FileRemoteConnectionDirectory(path);

        Assert.Equal("https://x/dav", reread.Find("nuvola")?.Url);
        Assert.True(reread.Remove("nuvola"));
        Assert.Empty(new FileRemoteConnectionDirectory(path).All);
    }

    [Fact]
    public void UnDocumentoRemotoRicordaIlProprioBlocco()
    {
        var documents = new RemoteDocuments();
        var connection = new RemoteConnection { Name = "nuvola", Url = "https://x" };

        documents.Follow(new RemoteDocument
        {
            LocalPath = "/tmp/bilancio.ods",
            Connection = connection,
            RemotePath = "/conti/bilancio.ods",
            Held = new RemoteLock("opaquelocktoken:1", "/conti/bilancio.ods"),
        });

        Assert.True(documents.For("/tmp/bilancio.ods")!.IsCheckedOut);

        documents.Forget("/tmp/bilancio.ods");

        Assert.Null(documents.For("/tmp/bilancio.ods"));
    }
}
