using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Documents;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests.Odt;

public sealed class OdtNoteStartLimitTests
{
    private static readonly XNamespace Text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";

    [Fact]
    public void AHugeNoteStartValueIsBoundedLikeTheListStarts()
    {
        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(new TextDocument(new DefaultStyleSheetFactory().Create()), stream).Succeeded);
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Update, true))
        {
            var entry = zip.GetEntry("styles.xml")!;
            XDocument styles;
            using (var input = entry.Open()) styles = XDocument.Load(input);
            foreach (var configuration in styles.Descendants(Text + "notes-configuration"))
                configuration.SetAttributeValue(Text + "start-value", "2147483647");
            entry.Delete();
            using var output = zip.CreateEntry("styles.xml").Open();
            styles.Save(output);
        }
        stream.Position = 0;
        var read = new OdtReader().Read(stream);
        Assert.True(read.Succeeded);
        Assert.InRange(read.Value!.Notes.FootnoteSettings.StartAt, 1, ushort.MaxValue + 1);
        Assert.InRange(read.Value!.Notes.EndnoteSettings.StartAt, 1, ushort.MaxValue + 1);
    }

    [Fact]
    public void AHugeColumnCountIsBoundedLikeInDocx()
    {
        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(new TextDocument(new DefaultStyleSheetFactory().Create()), stream).Succeeded);
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Update, true))
        {
            var entry = zip.GetEntry("styles.xml")!;
            XDocument styles;
            using (var input = entry.Open()) styles = XDocument.Load(input);
            XNamespace style = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
            XNamespace fo = "urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0";
            foreach (var layout in styles.Descendants(style + "page-layout-properties"))
                layout.Add(new XElement(style + "columns", new XAttribute(fo + "column-count", "2000000000")));
            entry.Delete();
            using var output = zip.CreateEntry("styles.xml").Open();
            styles.Save(output);
        }
        stream.Position = 0;
        var read = new OdtReader().Read(stream);
        Assert.True(read.Succeeded);
        Assert.All(read.Value!.Styles.Page.All, page => Assert.InRange(page.Columns.Count, 1, PulseOffice.Documents.Blocks.SectionColumns.LargestCount));
    }

    [Fact]
    public void RepeatedSpaceElementsCannotExpandWithoutLimit()
    {
        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(new TextDocument(new DefaultStyleSheetFactory().Create()), stream).Succeeded);
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Update, true))
        {
            var entry = zip.GetEntry("content.xml")!;
            XDocument content;
            using (var input = entry.Open()) content = XDocument.Load(input);
            var paragraph = new XElement(Text + "p");
            content.Descendants(XName.Get("text", "urn:oasis:names:tc:opendocument:xmlns:office:1.0")).First().Add(paragraph);
            for (var index = 0; index < 300; index++) paragraph.Add(new XElement(Text + "s", new XAttribute(Text + "c", "100000")));
            entry.Delete();
            using var output = zip.CreateEntry("content.xml").Open();
            content.Save(output);
        }
        stream.Position = 0;
        var read = new OdtReader().Read(stream);
        Assert.True(read.Succeeded);
        var length = read.Value!.Body.Blocks.OfType<PulseOffice.Documents.Blocks.ParagraphBlock>().Sum(block => block.Length);
        Assert.True(length <= 10_001_000, $"{length} characters");
    }
}
