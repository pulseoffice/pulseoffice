using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Text.Rich;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class EmptyParagraphMarkTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    [Fact]
    public void Il_corpo_del_segno_di_paragrafo_vuoto_si_legge_e_si_riscrive()
    {
        using var source = new MemoryStream();
        using (var package = WordprocessingDocument.Create(source, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            main.Document = new W.Document(new W.Body(
                new W.Paragraph(new W.ParagraphProperties(new W.ParagraphMarkRunProperties(new W.FontSize { Val = "8" }))),
                new W.Paragraph(new W.Run(new W.Text("Testo")))));
        }
        source.Position = 0;

        var read = _composer.Reader(DocxFlavour.Document).Read(source).Value!;
        Assert.Equal(4, Size(read));

        using var written = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(read, written).Succeeded);
        written.Position = 0;
        Assert.Equal(4, Size(_composer.Reader(DocxFlavour.Document).Read(written).Value!));
    }

    private double? Size(TextDocument document)
    {
        var empty = document.Body.Blocks.OfType<ParagraphBlock>().First();
        Assert.Equal(string.Empty, empty.Text);
        return new StyleResolver().EffectiveRun(document.Styles, empty.Content, empty.Content.Runs.FirstOrDefault() ?? new TextRun(string.Empty)).FontSize;
    }
}
