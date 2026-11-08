using System.IO.Compression;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests.Odt;

public sealed class OdtDeferredPageStyleTests
{
    [Fact]
    public void APageStyleDeferredToTheNextPageCrossesOdtWithoutAPageBreak()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        var deferred = new PageStyle("Second Section");
        deferred.Header.Enabled = true;
        deferred.Header.Default.Add(new ParagraphBlock(document.NextId("p"), "Second header"));
        document.Styles.Page.Add(deferred);
        document.Body.Add(new ParagraphBlock(document.NextId("p"), "Opening"));
        document.Body.Add(new ParagraphBlock(document.NextId("p"), new TextParagraph("Continuous start") { Runs = [new TextRun("Continuous start")], NextPageStyleName = "Second Section", PageNumberRestart = 4 }));
        using var stream = new MemoryStream();

        Assert.True(new OdtWriter().Write(document, stream).Succeeded);

        stream.Position = 0;
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true))
        using (var reader = new StreamReader(archive.GetEntry("content.xml")!.Open()))
        {
            var content = reader.ReadToEnd();
            Assert.Contains("next-master-page=\"Second_20_Section\"", content, StringComparison.Ordinal);
            Assert.DoesNotContain("style:master-page-name=\"Second_20_Section\"", content, StringComparison.Ordinal);
        }
        stream.Position = 0;
        var back = new OdtReader().Read(stream).Value!;
        var lead = back.Body.Blocks.OfType<ParagraphBlock>().Single(block => block.Content.Text == "Continuous start");
        Assert.Null(lead.Content.PageStyleName);
        Assert.Equal("Second Section", lead.Content.NextPageStyleName);
        Assert.Equal(4, lead.Content.PageNumberRestart);
        Assert.Equal("Second header", ((ParagraphBlock)back.Styles.Page.Find("Second Section")!.Header.Default.Blocks.Single()).Content.Text);
    }
}
