using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests.Odt;

public sealed class OdtListThreadRound101Tests
{
    private static readonly XNamespace Text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
    private static readonly XNamespace Xml = "http://www.w3.org/XML/1998/namespace";

    private const string Content = """
        <office:document-content xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0" xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0" xmlns:style="urn:oasis:names:tc:opendocument:xmlns:style:1.0" office:version="1.3">
          <office:automatic-styles>
            <text:list-style style:name="L1"><text:list-level-style-number text:level="1" style:num-format="1" style:num-suffix="."/></text:list-style>
          </office:automatic-styles>
          <office:body><office:text>
            <text:list xml:id="first" text:style-name="L1"><text:list-item><text:p>A one</text:p></text:list-item><text:list-item><text:p>A two</text:p></text:list-item></text:list>
            <text:p>Between</text:p>
            <text:list text:style-name="L1"><text:list-item><text:p>B one</text:p></text:list-item></text:list>
            <text:p>Between</text:p>
            <text:list text:style-name="L1" text:continue-list="first"><text:list-item><text:p>A three</text:p></text:list-item></text:list>
          </office:text></office:body>
        </office:document-content>
        """;

    [Fact]
    public void Continue_list_resumes_the_named_list_and_not_the_latest_one()
    {
        var document = new OdtReader().Read(Package(Content)).Value!;
        var items = Items(document);
        Assert.Equal(["A one", "A two", "B one", "A three"], items.Select(item => item.Text));
        Assert.Null(items[0].ListId);
        Assert.Null(items[1].ListId);
        Assert.NotNull(items[2].ListId);
        Assert.Null(items[3].ListId);
        Assert.Null(items[3].ListRestart);
    }

    [Fact]
    public void An_interrupted_list_keeps_resuming_the_right_list_after_round_trips()
    {
        var document = new OdtReader().Read(Package(Content)).Value!;
        var expected = Items(document).Select(item => (item.ListId, item.ListRestart)).ToList();
        for (var round = 0; round < 2; round++)
        {
            using var stream = new MemoryStream();
            Assert.True(new OdtWriter().Write(document, stream).Succeeded);
            stream.Position = 0;
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true))
            {
                using var content = archive.GetEntry("content.xml")!.Open();
                var lists = XDocument.Load(content).Descendants(Text + "list").ToList();
                var resumed = lists.Single(list => list.Attribute(Text + "continue-list") is not null);
                Assert.Contains(lists, list => (string?)list.Attribute(Xml + "id") == (string?)resumed.Attribute(Text + "continue-list"));
            }
            stream.Position = 0;
            document = new OdtReader().Read(stream).Value!;
            Assert.Equal(expected, Items(document).Select(item => (item.ListId, item.ListRestart)));
        }
    }

    [Fact]
    public void Model_threads_cross_the_odt_round_trip()
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        document.Body.Replace([Item("a1", "A1", null), Item("b1", "B1", "list1"), Item("a2", "A2", null), Item("b2", "B2", "list1")]);
        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        stream.Position = 0;
        var read = new OdtReader().Read(stream).Value!;
        Assert.Equal([null, "list1", null, "list1"], Items(read).Select(item => item.ListId));
        Assert.All(Items(read), item => Assert.Null(item.ListRestart));
    }

    [Fact]
    public void Custom_index_level_styles_cross_the_round_trip()
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        document.Styles.Paragraph.Add(new ParagraphStyle("Mine") { ParentName = DefaultStyleNames.Standard });
        document.Styles.Paragraph.Add(new ParagraphStyle("Top") { ParentName = DefaultStyleNames.Standard });
        var styles = PulseOffice.Documents.Indexes.IndexLevelStyles.Defaults.WithEntry(2, "Mine").WithHeading("Top");
        document.Body.Replace([
            new ParagraphBlock("h", new TextParagraph("Chapter") { Runs = [new TextRun("Chapter")], StyleName = DefaultStyleNames.HeadingLevel(1), OutlineLevel = 1 }),
            new SectionBlock("toc", "Contents", new BlockContainer([new ParagraphBlock("t", new TextParagraph("Contents") { Runs = [new TextRun("Contents")], StyleName = "Top" }), new ParagraphBlock("e", new TextParagraph("Chapter") { Runs = [new TextRun("Chapter")], StyleName = DefaultStyleNames.ContentsLevel(1) })]))
            {
                Index = new PulseOffice.Documents.Indexes.IndexDefinition(PulseOffice.Documents.Indexes.IndexKind.Contents) { Title = "Contents", LevelStyles = styles },
            }]);
        var read = RoundTrip(document);
        var index = new BlockTree().Blocks(read.Body).OfType<SectionBlock>().Single(section => section.IsIndex).Index!;
        Assert.Equal(styles, index.LevelStyles);
    }

    private static TextDocument RoundTrip(TextDocument document)
    {
        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        stream.Position = 0;
        return new OdtReader().Read(stream).Value!;
    }

    private static ParagraphBlock Item(string id, string text, string? thread) =>
        new(id, new TextParagraph(text) { Runs = [new TextRun(text)], ListStyleName = DefaultStyleNames.Numbering123, Numbered = true, ListId = thread });

    private static List<TextParagraph> Items(TextDocument document) =>
        [.. document.Body.Blocks.OfType<ParagraphBlock>().Select(paragraph => paragraph.Content).Where(content => content.ListStyleName is not null)];

    private static MemoryStream Package(string content)
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            using (var mime = new StreamWriter(zip.CreateEntry("mimetype", CompressionLevel.NoCompression).Open(), Encoding.ASCII)) mime.Write("application/vnd.oasis.opendocument.text");
            using var writer = new StreamWriter(zip.CreateEntry("content.xml").Open(), Encoding.UTF8);
            writer.Write(content);
        }
        stream.Position = 0;
        return stream;
    }
}
