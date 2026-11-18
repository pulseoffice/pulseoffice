using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round186DocxGridBeforeTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private W.TableCell Cell(string text) => new(new W.Paragraph(new W.Run(new W.Text(text))));

    [Fact]
    public void ARowStartingAfterSkippedGridColumnsKeepsItsCellsInTheirColumns()
    {
        using var source = new MemoryStream();
        using (var package = WordprocessingDocument.Create(source, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            main.Document = new W.Document(new W.Body(new W.Table(
                new W.TableGrid(new W.GridColumn { Width = "2000" }, new W.GridColumn { Width = "2000" }, new W.GridColumn { Width = "2000" }),
                new W.TableRow(Cell("a"), Cell("b"), Cell("c")),
                new W.TableRow(new W.TableRowProperties(new W.GridBefore { Val = 2 }), Cell("z")))));
        }
        source.Position = 0;

        var table = _composer.Reader(DocxFlavour.Document).Read(source).Value!.Body.Blocks.OfType<TableBlock>().Single();

        Assert.Equal(3, table.Rows[1].Cells.Count);
        Assert.Equal("z", ((ParagraphBlock)table.Cell(1, 2).Content[0]).Text);
        Assert.False(table.Cell(1, 0).Borders!.HasLines);
    }
}
