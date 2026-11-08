using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DocxVerticalMergeTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private W.TableCell Cell(string text, int span = 1, W.MergedCellValues? merge = null)
    {
        var properties = new W.TableCellProperties();
        if (span > 1) properties.Append(new W.GridSpan { Val = span });
        if (merge is { } value) properties.Append(new W.VerticalMerge { Val = value });
        return new W.TableCell(properties, new W.Paragraph(new W.Run(new W.Text(text))));
    }

    private TableBlock Read(params W.TableRow[] rows)
    {
        using var source = new MemoryStream();
        using (var package = WordprocessingDocument.Create(source, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            main.Document = new W.Document(new W.Body(new W.Table(
                [new W.TableGrid(new W.GridColumn { Width = "2000" }, new W.GridColumn { Width = "2000" }, new W.GridColumn { Width = "2000" }), .. rows])));
        }
        source.Position = 0;
        return _composer.Reader(DocxFlavour.Document).Read(source).Value!.Body.Blocks.OfType<TableBlock>().Single();
    }

    private void AssertEveryCoveredCellHasAnOrigin(TableBlock table)
    {
        for (var r = 0; r < table.Rows.Count; r++)
        {
            for (var c = 0; c < table.Rows[r].Cells.Count; c++)
            {
                if (!table.Rows[r].Cells[c].Covered) continue;
                var owners = 0;
                for (var row = 0; row <= r; row++)
                    for (var column = 0; column <= c; column++)
                    {
                        var cell = table.Rows[row].Cells[column];
                        if (!cell.Covered && row + cell.RowSpan > r && column + cell.ColumnSpan > c) owners++;
                    }
                Assert.True(owners == 1, $"cell {r},{c} has {owners} origins");
            }
        }
    }

    [Fact]
    public void AContinuationWithoutACellAboveKeepsItsText()
    {
        var table = Read(
            new W.TableRow(Cell("a"), Cell("b"), Cell("c")),
            new W.TableRow(Cell("d", merge: W.MergedCellValues.Restart), Cell("e"), Cell("f")));
        Assert.Equal(1, table.Cell(0, 0).RowSpan);

        var orphan = Read(new W.TableRow(Cell("a", 2), Cell("b")), new W.TableRow(Cell("x"), Cell("kept", merge: W.MergedCellValues.Continue), Cell("y")));

        Assert.False(orphan.Cell(1, 1).Covered);
        Assert.Equal(1, orphan.Cell(0, 0).RowSpan);
        AssertEveryCoveredCellHasAnOrigin(orphan);
    }

    [Fact]
    public void AContinuationDoesNotJumpOverAHorizontalMergeToAnOlderRow()
    {
        var table = Read(
            new W.TableRow(Cell("a"), Cell("b"), Cell("c")),
            new W.TableRow(Cell("wide", 2), Cell("f")),
            new W.TableRow(Cell("g"), Cell("h", merge: W.MergedCellValues.Continue), Cell("i")));

        Assert.Equal(1, table.Cell(0, 1).RowSpan);
        Assert.False(table.Cell(2, 1).Covered);
        AssertEveryCoveredCellHasAnOrigin(table);
    }

    [Fact]
    public void AContinuationWithADifferentSpanStartsAFreshCell()
    {
        var table = Read(
            new W.TableRow(Cell("a", 2, W.MergedCellValues.Restart), Cell("c")),
            new W.TableRow(Cell("d", merge: W.MergedCellValues.Continue), Cell("e"), Cell("f")));

        Assert.Equal(1, table.Cell(0, 0).RowSpan);
        Assert.False(table.Cell(1, 0).Covered);
        AssertEveryCoveredCellHasAnOrigin(table);
    }

    [Fact]
    public void ALongVerticalMergeStillSpansEveryRow()
    {
        var rows = new List<W.TableRow> { new(Cell("top", merge: W.MergedCellValues.Restart), Cell("b"), Cell("c")) };
        for (var r = 1; r < 50; r++) rows.Add(new W.TableRow(Cell(string.Empty, merge: W.MergedCellValues.Continue), Cell("b"), Cell("c")));

        var table = Read([.. rows]);

        Assert.Equal(50, table.Cell(0, 0).RowSpan);
        AssertEveryCoveredCellHasAnOrigin(table);
    }
}
