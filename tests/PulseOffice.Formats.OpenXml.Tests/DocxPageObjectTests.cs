using PulseOffice.Documents;
using System.IO.Compression;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Drawing.Assets;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public class DocxPageObjectTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private static TextDocument NewDocument()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Styles.Page.Replace(new PageStyle(DefaultStyleNames.Standard));
        return document;
    }

    private static AnchoredObject PageBox(string id, string text) =>
        new(id, new PageAnchor(1), new TextBoxContent(new BlockContainer([new ParagraphBlock(id + "-p", text, DefaultStyleNames.Standard)])),
            new DocumentSize(Twip.FromCentimeters(5), Twip.FromCentimeters(2)));

    private TextDocument RoundTrip(TextDocument document)
    {
        using var stream = new MemoryStream();
        var written = _composer.Writer(DocxFlavour.Document).Write(document, stream);
        Assert.True(written.Succeeded);
        stream.Position = 0;
        var read = _composer.Reader(DocxFlavour.Document).Read(stream);
        Assert.True(read.Succeeded);
        return read.Value!;
    }

    [Fact]
    public void PageAnchoredBoxesSurviveAnEmptyBody()
    {
        var document = NewDocument();
        document.Objects.Add(PageBox("box1", "First"));
        document.Objects.Add(PageBox("box2", "Second"));
        document.Objects.Add(PageBox("box3", "Third"));

        var read = RoundTrip(document);

        Assert.Equal(3, read.Objects.All.Count);
        Assert.Equal(["First", "Second", "Third"],
            read.Objects.All.Select(item => ((ParagraphBlock)((TextBoxContent)item.Content).Content[0]).Text).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void PageAnchoredBoxesSurviveABodyWithoutParagraphs()
    {
        var document = NewDocument();
        document.Body.Add(new TableBlock("t1", [new TableRow([new TableCell(new BlockContainer([]))])], [Twip.FromCentimeters(4)]));
        document.Objects.Add(PageBox("box1", "Only"));

        var read = RoundTrip(document);

        Assert.Single(read.Objects.All);
    }

    [Fact]
    public void PicturesInsideATrackedDeletionAreWrittenAsDeletedDrawings()
    {
        var document = NewDocument();
        var removed = new ParagraphBlock("removed", "gone", DefaultStyleNames.Standard);
        document.Changes.Add(new Deletion("del", "Ada", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), new BlockContainer([removed])));
        document.Objects.Add(new AnchoredObject("img", new ParagraphAnchor(removed.Id),
            new EmbeddedContent(new RasterImageObject { Id = "raster", Data = Png, MediaType = "image/png" }),
            new DocumentSize(Twip.FromCentimeters(1), Twip.FromCentimeters(1))));
        document.Body.Add(new ParagraphBlock("kept", "kept", DefaultStyleNames.Standard) { Marks = [new ChangeMark(0, "del", MarkEdge.Point)] });

        using var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, stream).Succeeded);
        stream.Position = 0;
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        using var reader = new StreamReader(archive.GetEntry("word/document.xml")!.Open());
        var xml = System.Xml.Linq.XDocument.Parse(reader.ReadToEnd());
        System.Xml.Linq.XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        Assert.Contains(xml.Descendants(w + "del"), deleted => deleted.Descendants(w + "drawing").Any());
        stream.Position = 0;
        var read = _composer.Reader(DocxFlavour.Document).Read(stream).Value!;
        var deletion = Assert.IsType<Deletion>(Assert.Single(read.Changes.All));
        var paragraph = deletion.Removed.Blocks.OfType<ParagraphBlock>().Single();
        Assert.Contains(read.Objects.All, item => item.Anchor is ParagraphAnchor anchor && anchor.ParagraphId == paragraph.Id
            || paragraph.Marks.OfType<ObjectMark>().Any(mark => mark.ObjectId == item.Id));
    }

    [Fact]
    public void AnOutlineParagraphWithAPlainStyleKeepsItsOutlineNumbering()
    {
        var document = NewDocument();
        document.Styles.Outline = new ListStyle(StyleSheet.OutlineStyleName,
            [.. Enumerable.Range(0, ListStyle.LevelCount).Select(_ => new ListLevel(NumberFormat.Arabic))]);
        document.Body.Add(new ParagraphBlock("h", new PulseOffice.Text.Rich.TextParagraph("foo") { StyleName = DefaultStyleNames.Standard, OutlineLevel = 2, Runs = [new PulseOffice.Text.Rich.TextRun("foo")] }));

        using var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, stream).Succeeded);
        stream.Position = 0;
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true))
        using (var reader = new StreamReader(archive.GetEntry("word/document.xml")!.Open()))
            Assert.Contains("numPr", reader.ReadToEnd());
        stream.Position = 0;
        var read = _composer.Reader(DocxFlavour.Document).Read(stream).Value!;
        var paragraph = Assert.IsType<ParagraphBlock>(read.Body[0]);
        Assert.Equal(2, paragraph.Content.OutlineLevel);
    }

    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFBQIAX8jx0gAAAABJRU5ErkJggg==");
}
