using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Styling;
using Xunit;

namespace PulseOffice.Sheets.Tests;

public sealed class StyleBandsTests
{
    private static readonly int LastRow = CellAddress.MaxRows - 1;
    private static readonly int LastColumn = CellAddress.MaxColumns - 1;

    private static CellRange Range(int r1, int c1, int r2, int c2) => new(new CellAddress(r1, c1), new CellAddress(r2, c2));

    [Fact]
    public void ACellResolvesBlockThenRowThenColumnThenDefault()
    {
        var layout = new SheetLayout();
        layout.SetColumnStyle(2, 5);
        layout.SetRowStyle(3, 6);
        layout.SetStyleBlock(Range(3, 2, 3, 2), 7);

        Assert.Equal(7, layout.DefaultStyleAt(new CellAddress(3, 2)));
        Assert.Equal(6, layout.DefaultStyleAt(new CellAddress(3, 4)));
        Assert.Equal(5, layout.DefaultStyleAt(new CellAddress(9, 2)));
        Assert.Equal(StyleCatalog.DefaultStyleId, layout.DefaultStyleAt(new CellAddress(9, 4)));
    }

    [Fact]
    public void BlocksCoalesceIntoStripsAndRuns()
    {
        var blocks = new StyleBlocks();
        for (var row = 0; row < 1000; row++)
            for (var column = 1; column <= 3; column++) blocks.Set(row, row, column, column, 9);

        Assert.Equal([new StyleBlock(0, 999, 1, 3, 9)], blocks.All);
    }

    [Fact]
    public void ALaterBlockWinsOverAnEarlierOne()
    {
        var blocks = new StyleBlocks();
        blocks.Set(0, 10, 0, 10, 1);
        blocks.Set(5, 5, 5, 5, 2);

        Assert.True(blocks.TryGetValue(5, 5, out var inner));
        Assert.Equal(2, inner);
        Assert.True(blocks.TryGetValue(4, 5, out var outer));
        Assert.Equal(1, outer);
        Assert.False(blocks.TryGetValue(11, 5, out _));
    }

    [Fact]
    public void InsertingAndDeletingRowsAndColumnsShiftsBlocks()
    {
        var bands = new StyleBands();
        bands.SetBlock(10, 20, 3, 4, 8);

        bands.InsertRows(5, 2, LastRow);
        bands.InsertColumns(0, 1, LastColumn);

        Assert.Equal([new StyleBlock(12, 22, 4, 5, 8)], bands.Blocks.All);

        bands.DeleteRows(12, 5);
        bands.DeleteColumns(4, 1);

        Assert.Equal([new StyleBlock(12, 17, 4, 4, 8)], bands.Blocks.All);

        bands.DeleteColumns(4, 1);

        Assert.True(bands.Blocks.IsEmpty);
    }

    [Fact]
    public void InsertingInsideABlockLeavesAGapForTheNeighbourCopy()
    {
        var bands = new StyleBands();
        bands.SetRows(10, 20, 4);
        bands.SetBlock(10, 20, 3, 3, 8);

        bands.InsertRows(15, 2, LastRow);
        bands.CopyRow(14, [15, 16]);

        Assert.Equal([new StyleBlock(10, 22, 3, 3, 8)], bands.Blocks.All);
        Assert.Equal(4, bands.Rows[16]);
    }

    [Fact]
    public void ACopiedColumnTakesTheBlocksOfItsNeighbour()
    {
        var bands = new StyleBands();
        bands.SetBlock(1, 9, 2, 2, 8);

        bands.InsertColumns(3, 1, LastColumn);
        bands.CopyColumn(2, [3]);

        Assert.Equal([new StyleBlock(1, 9, 2, 3, 8)], bands.Blocks.All);
    }

    [Fact]
    public void TheMementoRestoresRowsColumnsAndBlocks()
    {
        var bands = new StyleBands();
        bands.SetColumns(1, 2, 3);
        var before = bands.Save();
        bands.SetRows(0, LastRow, 4);
        bands.SetBlock(0, LastRow, 1, 2, 5);
        var after = bands.Save();

        bands.Restore(before);

        Assert.Empty(bands.Rows.Runs);
        Assert.True(bands.Blocks.IsEmpty);
        Assert.False(before.SameAs(after));

        bands.Restore(after);

        Assert.Equal(5, bands.StyleAt(new CellAddress(LastRow, 2)));
    }

    [Fact]
    public void LayoutCopiesCarryTheBlocks()
    {
        var source = new SheetLayout();
        source.SetStyleBlock(Range(1, 1, 2, 2), 6);
        var copy = new SheetLayout();

        source.CopyTo(copy);

        Assert.Equal(6, copy.DefaultStyleAt(new CellAddress(2, 2)));
        Assert.NotEqual(new SheetLayout().ShapeSignature(), copy.ShapeSignature());
    }

    [Fact]
    public void WholeRowsOverStyledColumnsBecomeBlocksNotCells()
    {
        var layout = new SheetLayout();
        layout.SetColumnStyles(1, 3, 10);
        var selection = Range(1, 0, LastRow, LastColumn);
        var work = new SelectionWorkArea().Plan(selection, null);

        var change = new StyleBandFormatter().Apply(layout, selection, work, (_, style) => style + 100);

        Assert.True(change.Changed);
        Assert.Equal(100, layout.RowStyle(LastRow));
        Assert.Equal([new StyleBlock(1, LastRow, 1, 3, 110)], layout.StyleBlocks.All);
        Assert.Equal(10, layout.DefaultStyleAt(new CellAddress(0, 2)));

        layout.Styles.Restore(change.Before);

        Assert.True(layout.StyleBlocks.IsEmpty);
        Assert.Null(layout.RowStyle(5));
    }

    [Fact]
    public void WholeColumnsOverStyledRowsBecomeBlocks()
    {
        var layout = new SheetLayout();
        layout.SetRowStyles(4, 6, 20);
        var selection = Range(0, 2, LastRow, 2);

        new StyleBandFormatter().Apply(layout, selection, new SelectionWorkArea().Plan(selection, null), (_, style) => style + 1);

        Assert.Equal(1, layout.ColumnStyle(2));
        Assert.Equal(21, layout.DefaultStyleAt(new CellAddress(5, 2)));
        Assert.Equal(20, layout.DefaultStyleAt(new CellAddress(5, 3)));
        Assert.Equal(1, layout.DefaultStyleAt(new CellAddress(9, 2)));
    }

    [Fact]
    public void TheWholeSheetRestylesExistingBlocksWithoutAddingRowBands()
    {
        var layout = new SheetLayout();
        layout.SetColumnStyle(1, 10);
        layout.SetRowStyles(3, 4, 20);
        layout.SetStyleBlock(Range(3, 1, 4, 1), 30);
        var selection = Range(0, 0, LastRow, LastColumn);

        new StyleBandFormatter().Apply(layout, selection, new SelectionWorkArea().Plan(selection, null), (_, style) => style + 1);

        Assert.Equal(31, layout.DefaultStyleAt(new CellAddress(3, 1)));
        Assert.Equal(21, layout.DefaultStyleAt(new CellAddress(3, 2)));
        Assert.Equal(11, layout.DefaultStyleAt(new CellAddress(9, 1)));
        Assert.Single(layout.RowStyles.Runs);
    }

    [Fact]
    public void ARestyleThatMatchesTheLineDropsTheBlock()
    {
        var layout = new SheetLayout();
        layout.SetColumnStyle(1, 10);
        layout.SetRowStyle(3, 20);
        layout.SetStyleBlock(Range(3, 1, 3, 1), 30);
        var selection = Range(3, 0, 3, LastColumn);

        new StyleBandFormatter().Apply(layout, selection, new SelectionWorkArea().Plan(selection, null), (_, _) => 40);

        Assert.True(layout.StyleBlocks.IsEmpty);
        Assert.Equal(40, layout.DefaultStyleAt(new CellAddress(3, 1)));
    }
}
