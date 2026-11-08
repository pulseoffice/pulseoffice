using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DocxStoredPageStyleTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    [Fact]
    public void An_unused_page_style_with_a_footer_survives_a_docx_round_trip()
    {
        var document = new TextDocument();
        var unused = new PageStyle("Appendix") { Usage = PageUsage.Right, NextStyleName = DefaultStyleNames.Standard };
        unused.Size = new DocumentSize(Twip.FromCentimeters(15), Twip.FromCentimeters(20));
        unused.Footer.Enabled = true;
        unused.Footer.Default.Add(new ParagraphBlock("pf", new TextParagraph("page ").WithRuns([new TextRun("page "), new TextRun("1") { Field = TextFieldKind.PageNumber }])));
        document.Styles.Page.Add(unused);

        var (read, bytes) = RoundTrip(document);

        var restored = Assert.IsType<PageStyle>(read.Styles.Page.Find("Appendix"));
        Assert.True(restored.Footer.Enabled);
        Assert.Equal(PageUsage.Right, restored.Usage);
        Assert.Equal(DefaultStyleNames.Standard, restored.NextStyleName);
        Assert.Equal(Twip.FromCentimeters(15).Value, restored.Width.Value);
        var paragraph = Assert.IsType<ParagraphBlock>(Assert.Single(restored.Footer.Default.Blocks));
        Assert.Contains(paragraph.Content.Runs, run => run.Field == TextFieldKind.PageNumber);
        using var package = WordprocessingDocument.Open(new MemoryStream(bytes), false);
        Assert.Single(package.MainDocumentPart!.CustomXmlParts);
        Assert.Single(package.MainDocumentPart.FooterParts);
        Assert.Single(package.MainDocumentPart.Document!.Body!.Descendants<W.SectionProperties>());
    }

    [Fact]
    public void Untouched_standard_page_styles_are_not_stored()
    {
        var (_, bytes) = RoundTrip(new TextDocument());
        using var package = WordprocessingDocument.Open(new MemoryStream(bytes), false);
        Assert.Empty(package.MainDocumentPart!.CustomXmlParts);
    }

    [Fact]
    public void A_stored_style_does_not_replace_the_style_of_a_written_section()
    {
        var document = new TextDocument();
        var standard = new PageStyle(DefaultStyleNames.Standard);
        document.Styles.Page.Replace(standard);
        document.InitialPageStyle = DefaultStyleNames.Standard;
        standard.Footer.Enabled = true;
        standard.Footer.Default.Add(new ParagraphBlock("pf", new TextParagraph("used").WithRuns([new TextRun("used")])));
        var other = new PageStyle("Other");
        other.Header.Enabled = true;
        other.Header.Default.Add(new ParagraphBlock("ph", new TextParagraph("stored").WithRuns([new TextRun("stored")])));
        document.Styles.Page.Add(other);

        var (read, _) = RoundTrip(document);

        Assert.Equal("used", Assert.IsType<ParagraphBlock>(read.Styles.Page.Find(DefaultStyleNames.Standard)!.Footer.Default.Blocks.Single()).Text);
        Assert.Equal("stored", Assert.IsType<ParagraphBlock>(read.Styles.Page.Find("Other")!.Header.Default.Blocks.Single()).Text);
    }

    [Fact]
    public void A_foreign_or_broken_part_is_ignored()
    {
        var document = new TextDocument();
        var (_, bytes) = RoundTrip(document);
        using var buffer = new MemoryStream();
        buffer.Write(bytes);
        using (var package = WordprocessingDocument.Open(buffer, true))
        {
            var part = package.MainDocumentPart!.AddCustomXmlPart(CustomXmlPartType.CustomXml);
            using var stream = part.GetStream(FileMode.Create, FileAccess.Write);
            using var writer = new StreamWriter(stream);
            writer.Write("<ps:pageStyles xmlns:ps=\"urn:pulseoffice:page-styles\"><ps:pageStyle name=\"X\"><notSectPr/></ps:pageStyle><ps:pageStyle/></ps:pageStyles>");
        }
        buffer.Position = 0;
        var read = _composer.Reader(DocxFlavour.Document).Read(buffer);
        Assert.True(read.Succeeded);
        Assert.Null(read.Value!.Styles.Page.Find("X"));
    }

    private (TextDocument Document, byte[] Bytes) RoundTrip(TextDocument document)
    {
        var writer = _composer.Writer(DocxFlavour.Document);
        Assert.DoesNotContain(writer.CheckFidelity(document).Losses, loss => loss.Code is "DOCX171" or "DOCX020");
        using var buffer = new MemoryStream();
        Assert.True(writer.Write(document, buffer).Succeeded);
        var bytes = buffer.ToArray();
        var read = _composer.Reader(DocxFlavour.Document).Read(new MemoryStream(bytes));
        Assert.True(read.Succeeded);
        return (read.Value!, bytes);
    }
}
