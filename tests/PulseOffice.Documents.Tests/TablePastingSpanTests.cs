using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Clipboard;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Editing.Tables;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class TablePastingSpanTests
{
    private readonly Fixture _f = new();
    private readonly TableEditing _tables;
    private readonly TablePasting _pasting;

    public TablePastingSpanTests()
    {
        _tables = new TableEditing(_f.Stories, _f.Deletion);
        _pasting = new TablePasting(new FragmentAdoption(new FragmentRemapper(_f.Stories, _f.Tree), new StyleMerger()), new TableSpanCutting());
    }

    private TableBlock Table()
    {
        _f.Editor.InsertTable(_f.Document, _f.History, new DocumentPosition(_f.First.Id, 0), 3, 3);
        return _f.Document.Body.Blocks.OfType<TableBlock>().Single();
    }

    private TableCellLocation At(TableBlock table, int row, int column) =>
        _tables.Locate(_f.Document, table.Cell(row, column).Content.Blocks.OfType<ParagraphBlock>().First().Id)!;

    private DocumentFragment Fragment(int rows, int columns, bool coverSecond = false)
    {
        var source = new TableBlock("src", Enumerable.Range(0, rows).Select(r => new TableRow(Enumerable.Range(0, columns).Select(c =>
            coverSecond && c == 1
                ? new TableCell(new BlockContainer()) { Covered = true }
                : new TableCell(new BlockContainer([new ParagraphBlock($"s{r}{c}", $"x{r}{c}", DefaultStyleNames.Standard)])) { ColumnSpan = coverSecond && c == 0 ? 2 : 1 }))),
            Enumerable.Repeat(Twip.FromPoints(40), columns));
        return new DocumentFragment(new BlockContainer([source]), new StyleSheet(), Array.Empty<Note>(), Array.Empty<Comment>(), Array.Empty<AnchoredObject>());
    }

    private void AssertSound(TableBlock table)
    {
        var owners = table.Rows.Select(row => new int[row.Cells.Count]).ToArray();
        for (var r = 0; r < table.Rows.Count; r++)
            for (var c = 0; c < table.Rows[r].Cells.Count; c++)
            {
                var cell = table.Rows[r].Cells[c];
                if (cell.Covered) continue;
                Assert.NotEqual(0, cell.Content.Count);
                for (var y = r; y < r + cell.RowSpan; y++)
                    for (var x = c; x < c + cell.ColumnSpan; x++) owners[y][x]++;
            }
        for (var r = 0; r < table.Rows.Count; r++)
            for (var c = 0; c < table.Rows[r].Cells.Count; c++)
                Assert.True(owners[r][c] == 1, $"cell {r},{c} belongs to {owners[r][c]} cells");
    }

    [Fact]
    public void RowsPastedInsideAVerticalMergeDoNotOverlapIt()
    {
        var table = Table();
        table.Cell(0, 2).RowSpan = 3;
        table.Cell(1, 2).Covered = true;
        table.Cell(2, 2).Covered = true;

        _pasting.PasteRows(_f.Document, _f.History, At(table, 1, 0), Fragment(1, 3));

        Assert.Equal(4, table.Rows.Count);
        Assert.Equal("x02", table.Cell(1, 2).Content.Blocks.OfType<ParagraphBlock>().Single().Text);
        AssertSound(table);
    }

    [Fact]
    public void ColumnsPastedInsideAHorizontalMergeDoNotOverlapIt()
    {
        var table = Table();
        table.Cell(2, 0).ColumnSpan = 3;
        table.Cell(2, 1).Covered = true;
        table.Cell(2, 2).Covered = true;

        _pasting.PasteColumns(_f.Document, _f.History, At(table, 0, 1), Fragment(3, 1));

        Assert.Equal(4, table.Rows[0].Cells.Count);
        AssertSound(table);
    }

    [Fact]
    public void SplittingKeepsAHorizontalMergeInTheFirstMovedRow()
    {
        var table = Table();
        table.Cell(1, 0).ColumnSpan = 2;
        table.Cell(1, 1).Covered = true;
        table.Cell(1, 1).Content.Replace([]);
        var conversion = new TableConversion(_f.Stories, _f.Text, new TableCellFactory(), _f.Deletion, new TableSpanCutting());

        conversion.SplitTable(_f.Document, _f.History, At(table, 1, 0), copyHeading: false);

        var second = _f.Document.Body.Blocks.OfType<TableBlock>().Last();
        Assert.True(second.Cell(0, 1).Covered);
        AssertSound(table);
        AssertSound(second);
    }

    [Fact]
    public void SplittingAcrossAWideVerticalMergeLeavesOneHeir()
    {
        var table = Table();
        table.Cell(0, 0).ColumnSpan = 2;
        table.Cell(0, 0).RowSpan = 3;
        foreach (var (r, c) in new[] { (0, 1), (1, 0), (1, 1), (2, 0), (2, 1) }) table.Cell(r, c).Covered = true;
        var conversion = new TableConversion(_f.Stories, _f.Text, new TableCellFactory(), _f.Deletion, new TableSpanCutting());

        conversion.SplitTable(_f.Document, _f.History, At(table, 1, 2), copyHeading: false);

        var second = _f.Document.Body.Blocks.OfType<TableBlock>().Last();
        Assert.Equal(2, second.Cell(0, 0).ColumnSpan);
        Assert.Equal(2, second.Cell(0, 0).RowSpan);
        AssertSound(table);
        AssertSound(second);
    }

    [Fact]
    public void PastedCoveredCellsBecomeEditableCells()
    {
        var table = Table();

        _pasting.PasteRows(_f.Document, _f.History, At(table, 1, 0), Fragment(1, 3, coverSecond: true));

        AssertSound(table);
    }
}
