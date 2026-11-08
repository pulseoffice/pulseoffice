using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Text.Rich;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DocxReferenceAcrossParagraphsTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private static W.Run Text(string text) => new(new W.Text(text) { Space = SpaceProcessingModeValues.Preserve });

    private static MemoryStream Package()
    {
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var main = package.AddMainDocumentPart();
            main.Document = new W.Document(new W.Body(
                new W.Paragraph(new W.BookmarkStart { Id = "1", Name = "Testo" }, Text("Prima riga")),
                new W.Paragraph(Text("Seconda riga"), new W.BookmarkEnd { Id = "1" }),
                new W.Paragraph(Text("Vedi: "),
                    new W.Run(new W.FieldChar { FieldCharType = W.FieldCharValues.Begin }),
                    new W.Run(new W.FieldCode(" REF Testo \\h ") { Space = SpaceProcessingModeValues.Preserve }),
                    new W.Run(new W.FieldChar { FieldCharType = W.FieldCharValues.Separate }),
                    Text("Prima riga")),
                new W.Paragraph(Text("Seconda riga"), new W.Run(new W.FieldChar { FieldCharType = W.FieldCharValues.End }), Text(" fine")),
                new W.Paragraph(Text("Dopo"))));
        }
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public void UnRiferimentoIlCuiRisultatoAttraversaIParagrafiNonInghiotteIlTestoSeguente()
    {
        var document = _composer.Reader(DocxFlavour.Document).Read(Package()).Value!;
        var paragraphs = new BlockTree().Paragraphs(document.Body).ToList();

        Assert.Equal(5, paragraphs.Count);
        var reference = Assert.Single(paragraphs[2].Content.Runs, run => run.Field == TextFieldKind.Bookmark);
        Assert.Equal("Testo", reference.FieldArgument);
        Assert.EndsWith("fine", paragraphs[3].Text, StringComparison.Ordinal);
        Assert.DoesNotContain(paragraphs[3].Content.Runs, run => run.Field != TextFieldKind.None);
        Assert.Equal("Dopo", paragraphs[4].Text);
    }
}
