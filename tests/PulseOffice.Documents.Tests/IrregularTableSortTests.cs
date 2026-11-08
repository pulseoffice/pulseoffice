using PulseOffice.Document.Tables;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Editing.Tables;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class IrregularTableSortTests
{
    private TableCell Cell(string id, string text) => new(new BlockContainer([new ParagraphBlock(id, text)]));

    [Fact]
    public void UnaTabellaConRigheDiLunghezzaDiversaNonSiPuoOrdinareMaNonSolleva()
    {
        var table = new TableBlock("t1", [new TableRow([Cell("a", "b"), Cell("b", "x")]), new TableRow([Cell("c", "a")])], [new Twip(1000), new Twip(1000)]);
        var owner = new BlockContainer([table]);
        var selection = new TableSelection(new TableCellLocation(table, 0, 0, owner), new TableCellRange(0, 0, 0, 0));
        var sorting = new TableSorting(new TableGrid(new BlockTree()), new TableSorter());

        Assert.False(sorting.CanSort(selection));
    }

    [Fact]
    public void OrdinareUnaTabellaIrregolareNonSollevaENonCambiaNulla()
    {
        var table = new TableBlock("t1", [new TableRow([Cell("a", "b"), Cell("b", "x")]), new TableRow([Cell("c", "a")])], [new Twip(1000), new Twip(1000)]);
        var selection = new TableSelection(new TableCellLocation(table, 0, 0, new BlockContainer([table])), new TableCellRange(0, 0, 1, 1));
        var sorting = new TableSorting(new TableGrid(new BlockTree()), new TableSorter());
        var document = new TextDocument();

        sorting.Sort(document, new PulseOffice.Document.Commands.UndoStack(), selection, [new TableSortKey(0)], System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal("a", table.Rows[0].Cells[0].Content.Blocks[0].Id);
    }
}
