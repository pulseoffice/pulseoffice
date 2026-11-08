using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DocxPendingBreakTests
{
    [Fact]
    public void L_interruzione_prima_di_un_sommario_in_un_controllo_va_al_sommario_non_al_paragrafo_dopo()
    {
        using var source = new MemoryStream();
        using (var package = WordprocessingDocument.Create(source, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            main.Document = new W.Document(new W.Body(
                new W.Paragraph(new W.Run(new W.Text("Copertina"))),
                new W.Paragraph(new W.Run(new W.Break { Type = W.BreakValues.Page })),
                new W.SdtBlock(new W.SdtProperties(), new W.SdtContentBlock(new W.Paragraph(new W.Run(new W.Text("Sommario"))))),
                new W.Paragraph(new W.Run(new W.Text("Dopo")))));
        }
        source.Position = 0;

        var read = new DocxComposer(new DirectFormatting(), new DefaultStyleSheetFactory()).Reader(DocxFlavour.Document).Read(source);

        var paragraphs = read.Value!.Body.Blocks.OfType<ParagraphBlock>().ToList();
        Assert.True(paragraphs.Single(paragraph => paragraph.Text == "Sommario").Content.PageBreakBefore);
        Assert.NotEqual(true, paragraphs.Single(paragraph => paragraph.Text == "Dopo").Content.PageBreakBefore);
    }
}
