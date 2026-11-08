using System.IO.Compression;
using System.Text;
using PulseOffice.Documents.Blocks;
using PulseOffice.Formats.Odf.Odt;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdtTableGridRepairTests
{
    private const string Head = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>"
        + "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" "
        + "xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" office:version=\"1.3\"><office:body><office:text><table:table table:name=\"T\">"
        + "<table:table-column table:number-columns-repeated=\"3\"/>";

    private const string Tail = "</table:table></office:text></office:body></office:document-content>";

    private TableBlock Read(string rows)
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            using (var writer = new StreamWriter(zip.CreateEntry("mimetype", CompressionLevel.NoCompression).Open(), new UTF8Encoding(false)))
                writer.Write("application/vnd.oasis.opendocument.text");
            using (var writer = new StreamWriter(zip.CreateEntry("content.xml").Open(), new UTF8Encoding(false)))
                writer.Write(Head + rows + Tail);
        }
        stream.Position = 0;
        return new OdtReader().Read(stream).Value!.Body.Blocks.OfType<TableBlock>().Single();
    }

    private string Cell(string text, string attributes = "") => $"<table:table-cell{attributes}><text:p>{text}</text:p></table:table-cell>";

    private void AssertSound(TableBlock table)
    {
        var owners = table.Rows.Select(row => new int[row.Cells.Count]).ToArray();
        for (var r = 0; r < table.Rows.Count; r++)
            for (var c = 0; c < table.Rows[r].Cells.Count; c++)
            {
                var cell = table.Rows[r].Cells[c];
                if (cell.Covered) continue;
                Assert.InRange(r + cell.RowSpan, 1, table.Rows.Count);
                Assert.InRange(c + cell.ColumnSpan, 1, table.Rows[r].Cells.Count);
                for (var y = r; y < r + cell.RowSpan; y++)
                    for (var x = c; x < c + cell.ColumnSpan; x++) owners[y][x]++;
            }
        for (var r = 0; r < table.Rows.Count; r++)
            for (var c = 0; c < table.Rows[r].Cells.Count; c++)
                Assert.True(owners[r][c] == 1, $"cell {r},{c} belongs to {owners[r][c]} cells");
    }

    [Fact]
    public void AShortRowIsFilledWithRealCells()
    {
        var table = Read($"<table:table-row>{Cell("a")}{Cell("b")}{Cell("c")}</table:table-row><table:table-row>{Cell("d")}</table:table-row>");

        Assert.False(table.Cell(1, 2).Covered);
        AssertSound(table);
    }

    [Fact]
    public void SpansAreClampedToTheTable()
    {
        var table = Read($"<table:table-row>{Cell("a", " table:number-rows-spanned=\"9\" table:number-columns-spanned=\"7\"")}</table:table-row><table:table-row>{Cell("d")}{Cell("e")}{Cell("f")}</table:table-row>");

        Assert.Equal("a", string.Concat(table.Cell(0, 0).Content.Blocks.OfType<ParagraphBlock>().Select(p => p.Text)));
        AssertSound(table);
    }

    [Fact]
    public void ACoveredCellWithContentAndNoOwnerKeepsItsText()
    {
        var table = Read($"<table:table-row>{Cell("a")}<table:covered-table-cell><text:p>kept</text:p></table:covered-table-cell>{Cell("c")}</table:table-row>");

        Assert.False(table.Cell(0, 1).Covered);
        Assert.Equal("kept", table.Cell(0, 1).Content.Blocks.OfType<ParagraphBlock>().Single().Text);
        AssertSound(table);
    }

    [Fact]
    public void TextInsideAnOwnedCoveredCellIsAppendedToItsOwner()
    {
        var table = Read($"<table:table-row>{Cell("a", " table:number-columns-spanned=\"2\"")}<table:covered-table-cell><text:p>hidden</text:p></table:covered-table-cell>{Cell("c")}</table:table-row>");

        Assert.True(table.Cell(0, 1).Covered);
        Assert.Equal(["a", "hidden"], table.Cell(0, 0).Content.Blocks.OfType<ParagraphBlock>().Select(p => p.Text));
        AssertSound(table);
    }

    [Fact]
    public void EmptyCoveredCellsAddNothingToTheirOwner()
    {
        var table = Read($"<table:table-row>{Cell("a", " table:number-columns-spanned=\"2\"")}<table:covered-table-cell><text:p/></table:covered-table-cell>{Cell("c")}</table:table-row>");

        Assert.Equal(["a"], table.Cell(0, 0).Content.Blocks.OfType<ParagraphBlock>().Select(p => p.Text));
    }
}
