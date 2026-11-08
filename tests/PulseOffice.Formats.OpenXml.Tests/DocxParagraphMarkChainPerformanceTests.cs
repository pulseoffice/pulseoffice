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
public sealed class DocxParagraphMarkChainPerformanceTests(ITestOutputHelper output)
{
    private const string Namespace = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";

    private const int Chain = 6_000;

    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private static MemoryStream ChainedDocx()
    {
        var body = new StringBuilder();
        for (var index = 0; index < Chain; index++)
            body.Append("<w:p><w:pPr><w:rPr><w:del w:id=\"").Append(index).Append("\" w:author=\"Ada\" w:date=\"2026-01-01T00:00:00Z\"/></w:rPr></w:pPr><w:r><w:t>part</w:t></w:r></w:p>");
        body.Append("<w:p><w:r><w:t>end</w:t></w:r></w:p>");
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
            package.AddMainDocumentPart().Document = new W.Document($"<w:document {Namespace}><w:body>{body}</w:body></w:document>");
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public void ALongChainOfDeletedParagraphMarksIsJoinedAndSplitInNearLinearTime()
    {
        var clock = Stopwatch.StartNew();
        var read = _composer.Reader(DocxFlavour.Document).Read(ChainedDocx());
        var reading = clock.Elapsed;
        Assert.True(read.Succeeded);
        var document = read.Value!;
        var joined = Assert.Single(document.Body.Blocks.OfType<ParagraphBlock>());
        Assert.Equal(Chain * 4 + 3, joined.Text.Length);

        clock.Restart();
        var written = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, written).Succeeded);
        var writing = clock.Elapsed;
        output.WriteLine($"read {reading}, written {writing}");

        written.Position = 0;
        using (var package = WordprocessingDocument.Open(written, false))
            Assert.Equal(Chain + 1, package.MainDocumentPart!.Document!.Body!.Elements<W.Paragraph>().Count(paragraph => paragraph.InnerText.Length > 0));
        Assert.True(reading < TimeSpan.FromSeconds(4), $"read in {reading}");
        Assert.True(writing < TimeSpan.FromSeconds(4), $"written in {writing}");
    }

    [Fact]
    public void ManyConditionalFieldsAreWrittenWithoutRescanningTheDocumentForEachName()
    {
        const int fields = 8_000;
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Body.Replace([.. Enumerable.Range(0, fields).Select(index => new ParagraphBlock("p" + index, new PulseOffice.Text.Rich.TextParagraph("x")
        {
            Runs = [new PulseOffice.Text.Rich.TextRun("x") { Field = PulseOffice.Text.Rich.TextFieldKind.ConditionalText, FieldArgument = "alpha == 1 and beta == 2" }],
        }))]);

        var clock = Stopwatch.StartNew();
        var written = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, written).Succeeded);
        clock.Stop();
        output.WriteLine($"written {clock.Elapsed}");

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(4), $"written in {clock.Elapsed}");
    }
}
