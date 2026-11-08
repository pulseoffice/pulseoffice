using System.Diagnostics;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

[Collection(nameof(OpenXmlLargeDocumentPerformanceTests))]
public sealed class DocxManyRunsTests
{
    private const string Namespace = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";

    [Fact]
    public void AParagraphSplitIntoVeryManyEqualRunsIsMergedInLinearTime()
    {
        const int runs = 60_000;
        var paragraph = new StringBuilder();
        for (var index = 0; index < runs; index++) paragraph.Append("<w:r><w:t>abcdefghij</w:t></w:r>");
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
            package.AddMainDocumentPart().Document = new W.Document($"<w:document {Namespace}><w:body><w:p>{paragraph}</w:p></w:body></w:document>");
        stream.Position = 0;

        var clock = Stopwatch.StartNew();
        var read = new DocxComposer(new DirectFormatting(), new DefaultStyleSheetFactory()).Reader(DocxFlavour.Document).Read(stream);
        clock.Stop();

        Assert.True(read.Succeeded);
        var content = read.Value!.Body.Blocks.OfType<ParagraphBlock>().First();
        Assert.Single(content.Content.Runs);
        Assert.Equal(runs * 10, content.Text.Length);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(4), $"read in {clock.Elapsed}");
    }
}
