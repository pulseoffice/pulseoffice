using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Text.Rich;
using PulseOffice.Text.Rich.Bidi;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class BidiParityTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private TextDocument Through(TextDocument document, ITextDocumentWriter writer, ITextDocumentReader reader)
    {
        using var stream = new MemoryStream();
        var written = writer.Write(document, stream);
        Assert.True(written.Succeeded, string.Join("; ", written.Diagnostics.Select(d => d.Message)));
        stream.Position = 0;
        var read = reader.Read(stream);
        Assert.True(read.Succeeded, string.Join("; ", read.Diagnostics.Select(d => d.Message)));
        return read.Value!;
    }

    private TextDocument Sample()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Styles.Paragraph.Replace(new ParagraphStyle("Ebraico")
        {
            ParentName = DefaultStyleNames.Standard,
            Paragraph = new ParagraphProperties { Direction = TextDirection.RightToLeft, MarginLeft = 12 },
        });
        document.Body.Add(new ParagraphBlock(document.NextId("p"), new TextParagraph("שלום עולם")
        {
            StyleName = DefaultStyleNames.Standard,
            Direction = TextDirection.RightToLeft,
            Align = TextAlign.Right,
            Explicit = ParagraphOverrides.Align,
            MarginLeft = 10,
            MarginRight = 30,
            Runs = [new TextRun("שלום עולם")],
        }));
        document.Body.Add(new ParagraphBlock(document.NextId("p"), new TextParagraph("styled") { StyleName = "Ebraico", Runs = [new TextRun("styled")] }));
        document.Body.Add(new ParagraphBlock(document.NextId("p"), new TextParagraph(string.Empty)
        {
            StyleName = DefaultStyleNames.Standard,
            Direction = TextDirection.LeftToRight,
        }.WithRuns([new TextRun("ab "), new TextRun("12") { RightToLeft = true }])));
        return document;
    }

    private string Describe(TextDocument document)
    {
        var paragraphs = document.Body.Blocks.OfType<ParagraphBlock>().Select(block => block.Content).ToList();
        var first = paragraphs[0];
        var style = document.Styles.Paragraph.Find("Ebraico")!.Paragraph;
        var last = paragraphs[2];
        return string.Join("|",
            first.Direction, first.Align, first.MarginLeft, first.MarginRight,
            style.Direction, style.MarginLeft,
            last.Direction, string.Join(",", last.Runs.Select(run => run.Text + ":" + (run.RightToLeft == true))));
    }

    [Fact]
    public void ParagraphAndRunDirectionsCrossOdtAndDocxAlike()
    {
        var expected = Describe(Sample());

        Assert.Equal(expected, Describe(Through(Sample(), new OdtWriter(), new OdtReader())));
        Assert.Equal(expected, Describe(Through(Sample(), _composer.Writer(DocxFlavour.Document), _composer.Reader(DocxFlavour.Document))));
    }

    [Fact]
    public void OdtStoresRightToLeftMarginsPhysically()
    {
        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(Sample(), stream).Succeeded);
        stream.Position = 0;
        using var zip = new System.IO.Compression.ZipArchive(stream);
        using var reader = new StreamReader(zip.GetEntry("content.xml")!.Open());
        var content = reader.ReadToEnd();

        Assert.Contains("style:writing-mode=\"rl-tb\"", content);
        Assert.Contains("fo:margin-left=\"30pt\"", content);
        Assert.Contains("fo:margin-right=\"10pt\"", content);
    }

    [Fact]
    public void DocxWritesBidiAndRtl()
    {
        using var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(Sample(), stream).Succeeded);
        stream.Position = 0;
        using var zip = new System.IO.Compression.ZipArchive(stream);
        using var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open());
        var content = reader.ReadToEnd();

        Assert.Contains("<w:bidi", content);
        Assert.Contains("<w:rtl", content);
    }
}
