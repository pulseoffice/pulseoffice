using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using PulseOffice.Robustness.Tests.Fuzzing;
using Xunit;

namespace PulseOffice.Robustness.Tests.Attacks;

public sealed class HostileFileTests : IDisposable
{
    private const string BillionLaughs = """
        <!DOCTYPE lolz [
          <!ENTITY lol "lol">
          <!ENTITY lol1 "&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;">
          <!ENTITY lol2 "&lol1;&lol1;&lol1;&lol1;&lol1;&lol1;&lol1;&lol1;&lol1;&lol1;">
          <!ENTITY lol3 "&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;">
          <!ENTITY lol4 "&lol3;&lol3;&lol3;&lol3;&lol3;&lol3;&lol3;&lol3;&lol3;&lol3;">
          <!ENTITY lol5 "&lol4;&lol4;&lol4;&lol4;&lol4;&lol4;&lol4;&lol4;&lol4;&lol4;">
          <!ENTITY lol6 "&lol5;&lol5;&lol5;&lol5;&lol5;&lol5;&lol5;&lol5;&lol5;&lol5;">
          <!ENTITY lol7 "&lol6;&lol6;&lol6;&lol6;&lol6;&lol6;&lol6;&lol6;&lol6;&lol6;">
          <!ENTITY lol8 "&lol7;&lol7;&lol7;&lol7;&lol7;&lol7;&lol7;&lol7;&lol7;&lol7;">
          <!ENTITY lol9 "&lol8;&lol8;&lol8;&lol8;&lol8;&lol8;&lol8;&lol8;&lol8;&lol8;">
        ]>
        """;

    private readonly DocumentReaders _readers = new();
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);

    public HostileFileTests() => _listener.Start();

    public void Dispose()
    {
        _readers.Dispose();
        _listener.Stop();
        _listener.Dispose();
    }

    private int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    private byte[] Package(string mimetype, params (string Name, string Content)[] entries)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            if (mimetype.Length > 0)
                using (var writer = new StreamWriter(archive.CreateEntry("mimetype", CompressionLevel.NoCompression).Open()))
                    writer.Write(mimetype);
            foreach (var (name, content) in entries)
                using (var writer = new StreamWriter(archive.CreateEntry(name).Open(), new UTF8Encoding(false)))
                    writer.Write(content);
        }

        return output.ToArray();
    }

    private async Task<string> Codes(DocumentFamily family, string name, byte[] content)
    {
        var diagnostics = await Task.Run(() => _readers.Read(family, name, new MemoryStream(content, writable: false)), TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);
        return string.Join(",", diagnostics.Where(diagnostic => diagnostic.Severity == PulseOffice.Foundation.Diagnostics.Severity.Error).Select(diagnostic => diagnostic.Code));
    }

    private string OdtContent(string doctype, string body) =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" + doctype +
        "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" office:version=\"1.3\"><office:body><office:text><text:p>" + body + "</text:p></office:text></office:body></office:document-content>";

    private const string Manifest = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><manifest:manifest xmlns:manifest=\"urn:oasis:names:tc:opendocument:xmlns:manifest:1.0\" manifest:version=\"1.3\"><manifest:file-entry manifest:full-path=\"/\" manifest:media-type=\"application/vnd.oasis.opendocument.text\"/><manifest:file-entry manifest:full-path=\"content.xml\" manifest:media-type=\"text/xml\"/></manifest:manifest>";

    [Fact]
    public async Task A_billion_laughs_document_is_refused_quickly()
    {
        var odt = Package("application/vnd.oasis.opendocument.text", ("META-INF/manifest.xml", Manifest), ("content.xml", OdtContent(BillionLaughs, "&lol9;")));

        Assert.Equal("FMT013", await Codes(DocumentFamily.Text, "laughs.odt", odt));
    }

    [Fact]
    public async Task An_external_entity_is_never_resolved()
    {
        var doctype = $"<!DOCTYPE x [<!ENTITY secret SYSTEM \"file:///etc/passwd\"><!ENTITY remote SYSTEM \"http://127.0.0.1:{Port}/xxe\">]>";
        var odt = Package("application/vnd.oasis.opendocument.text", ("META-INF/manifest.xml", Manifest), ("content.xml", OdtContent(doctype, "&secret;&remote;")));
        var accepted = _listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken).AsTask();

        Assert.Equal("FMT013", await Codes(DocumentFamily.Text, "xxe.odt", odt));
        Assert.NotSame(accepted, await Task.WhenAny(accepted, Task.Delay(300, TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task An_external_document_type_in_an_ooxml_part_is_never_fetched()
    {
        var doctype = $"<!DOCTYPE w:document SYSTEM \"http://127.0.0.1:{Port}/dtd\">";
        var docx = Package(string.Empty,
            ("[Content_Types].xml", "<?xml version=\"1.0\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/></Types>"),
            ("_rels/.rels", "<?xml version=\"1.0\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>"),
            ("word/document.xml", "<?xml version=\"1.0\"?>" + doctype + "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body><w:p><w:r><w:t>x</w:t></w:r></w:p></w:body></w:document>"));
        var accepted = _listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken).AsTask();

        Assert.DoesNotContain("FMT010", await Codes(DocumentFamily.Text, "dtd.docx", docx), StringComparison.Ordinal);
        Assert.NotSame(accepted, await Task.WhenAny(accepted, Task.Delay(300, TestContext.Current.CancellationToken)));
    }

    [Theory]
    [InlineData("laughs.fodt")]
    [InlineData("laughs.xhtml")]
    public async Task Entities_in_a_flat_xml_file_are_refused(string name)
    {
        var content = "<?xml version=\"1.0\"?>" + BillionLaughs.Replace("lolz", "office:document", StringComparison.Ordinal)
            + "<office:document xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" office:mimetype=\"application/vnd.oasis.opendocument.text\"><office:body><office:text><text:p>&lol9;</text:p></office:text></office:body></office:document>";

        Assert.Equal("FMT013", await Codes(DocumentFamily.Text, name, Encoding.UTF8.GetBytes(content)));
    }

    [Fact]
    public async Task A_zip_bomb_is_refused_before_it_is_expanded()
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var writer = new StreamWriter(archive.CreateEntry("mimetype", CompressionLevel.NoCompression).Open()))
                writer.Write("application/vnd.oasis.opendocument.spreadsheet");
            using var bomb = archive.CreateEntry("content.xml", CompressionLevel.SmallestSize).Open();
            var zeros = new byte[1 << 20];
            for (var megabyte = 0; megabyte < 600; megabyte++) bomb.Write(zeros);
        }

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var before = GC.GetTotalMemory(true);

        Assert.Equal("FMT012", await Codes(DocumentFamily.Spreadsheet, "bomb.ods", output.ToArray()));
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(30));
        Assert.True(GC.GetTotalMemory(false) - before < 256L << 20);
    }

    [Fact]
    public async Task Ordinary_seed_documents_pass_the_intake_unchanged()
    {
        var failures = new List<(string Name, string Codes)>();
        foreach (var seed in new SeedCorpus().Load(perExtension: 200))
        {
            var codes = await Codes(seed.Family, "seed" + Path.GetExtension(seed.Name), seed.Content);
            if (codes.Contains("FMT01", StringComparison.Ordinal)) failures.Add((seed.Name, codes));
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures.Select(failure => $"{failure.Name}: {failure.Codes}")));
    }
}
