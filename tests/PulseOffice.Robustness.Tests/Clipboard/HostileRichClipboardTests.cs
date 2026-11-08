using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using PulseOffice.Formats.Interchange.Text.Html;
using PulseOffice.Robustness.Tests.Fuzzing;
using Xunit;

namespace PulseOffice.Robustness.Tests.Clipboard;

public sealed class HostileRichClipboardTests : IDisposable
{
    private readonly HtmlTableReader _tables = new(new HtmlTokenizer(), new CssDeclarations(), new HtmlCellBorders(new CssDeclarations()));
    private readonly DocumentReaders _readers = new();
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);

    public HostileRichClipboardTests() => _listener.Start();

    public void Dispose()
    {
        _readers.Dispose();
        _listener.Stop();
        _listener.Dispose();
    }

    private int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    private async Task<IReadOnlyList<PulseOffice.Foundation.Diagnostics.Diagnostic>> Read(string name, string content) =>
        await Task.Run(() => _readers.Read(DocumentFamily.Text, name, new MemoryStream(Encoding.UTF8.GetBytes(content), writable: false)), TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);

    [Fact]
    public async Task HugeSpansDoNotExplodeTheTableGrid()
    {
        var html = new StringBuilder("<table><tr>");
        for (var cell = 0; cell < 400; cell++) html.Append("<td colspan=\"1000\" rowspan=\"1000\">x</td>");
        html.Append("</tr></table>");
        var clock = Stopwatch.StartNew();
        var table = await Task.Run(() => _tables.FirstTable(html.ToString()), TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        Assert.NotNull(table);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(10), clock.Elapsed.ToString());
        Assert.True((long)table.Rows.Count * table.ColumnCount <= HtmlTableReader.MaximumCells);
    }

    [Fact]
    public void EnormousTablesAreCut()
    {
        var html = new StringBuilder("<table>");
        for (var row = 0; row < 3000; row++)
        {
            html.Append("<tr>");
            for (var column = 0; column < 2000; column++) html.Append("<td>1");
        }
        var table = _tables.FirstTable(html.ToString());
        Assert.NotNull(table);
        Assert.True(table.Rows.Sum(row => (long)row.Count) <= HtmlTableReader.MaximumCells);
    }

    [Fact]
    public void DeeplyNestedTablesDoNotOverflow()
    {
        var html = string.Concat(Enumerable.Repeat("<table><tr><td>", 100_000)) + "x";
        Assert.NotNull(_tables.FirstTable(html));
    }

    [Fact]
    public async Task HostileHtmlNeverTouchesNetworkOrFiles()
    {
        var secret = Path.Combine(Path.GetTempPath(), "pulse-secret-" + Guid.NewGuid().ToString("N") + ".png");
        var html = $"""
            <html><body>
            <script>alert(1)</script><p onclick="x()">ok</p>
            <img src="http://127.0.0.1:{Port}/a.png"><img src="file://{secret}"><img src="{secret}">
            <link rel="stylesheet" href="http://127.0.0.1:{Port}/s.css"><iframe src="http://127.0.0.1:{Port}/"></iframe>
            <object data="http://127.0.0.1:{Port}/o"></object><embed src="file://{secret}">
            {string.Concat(Enumerable.Repeat("<div>", 50_000))}deep
            </body></html>
            """;
        await Read("clip.html", html);
        Assert.False(_listener.Pending());
        Assert.False(File.Exists(secret));
    }

    [Theory]
    [InlineData("{\\rtf1 {\\object\\objemb{\\*\\objclass Package}{\\*\\objdata 0105000002000000}}}")]
    [InlineData("{\\rtf1 {\\field{\\*\\fldinst INCLUDEPICTURE \"http://127.0.0.1:PORT/x.png\"}}{\\field{\\*\\fldinst INCLUDETEXT \"/etc/passwd\"}}}")]
    [InlineData("{\\rtf1 {\\pict\\pngblip\\picw99999999\\pich99999999 89504e47}}")]
    [InlineData("{\\rtf1 \\bin999999999 abc}")]
    [InlineData("{\\rtf1 \\u-99999999? \\ansicpg99999 \\'zz \\fs-2147483648 \\trowd\\cellx-5\\cellx999999999\\cell\\row}")]
    [InlineData("{\\rtf1")]
    public async Task MalformedRtfFailsCleanly(string rtf)
    {
        await Read("clip.rtf", rtf.Replace("PORT", Port.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal));
        Assert.False(_listener.Pending());
    }

    [Fact]
    public async Task ClipboardOpenDocumentGoesThroughTheIncomingInspection()
    {
        using var output = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(output, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var writer = new StreamWriter(archive.CreateEntry("mimetype", System.IO.Compression.CompressionLevel.NoCompression).Open()))
                writer.Write("application/vnd.oasis.opendocument.text");
            using (var writer = new StreamWriter(archive.CreateEntry("content.xml").Open()))
            {
                writer.Write("<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\"><office:body><office:text>");
                writer.Write("<text:p>");
                for (var level = 0; level < 20_000; level++) writer.Write("<text:span>");
                for (var level = 0; level < 20_000; level++) writer.Write("</text:span>");
                writer.Write("</text:p>");
                writer.Write("</office:text></office:body></office:document-content>");
            }
        }
        var document = await Task.Run(() => _readers.Interchange.Read("odt", output.ToArray()), TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);
        Assert.Null(document);
    }

    [Fact]
    public async Task DeeplyNestedRtfFailsCleanly()
    {
        await Read("deep.rtf", "{\\rtf1 " + new string('{', 200_000) + "x" + new string('}', 200_000) + "}");
    }
}
