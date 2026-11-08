using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class CellPaddingLayoutTests
{
    private TableCell Cell(LayoutFixture f, string text) =>
        new(new BlockContainer([new ParagraphBlock(f.Document.NextId("c"), new TextParagraph(text) { Runs = [new TextRun(text)] })]));

    [Fact]
    public void ACellsOwnPaddingMovesItsTextAndGrowsItsRow()
    {
        var f = new LayoutFixture(width: 400, height: 400);
        f.Document.Styles.Table.Add(new TableStyle("T") { Table = new TableProperties { CellPadding = 2, VerticalCellPadding = 1, BorderWidth = 0 } });
        var plain = Cell(f, "plain");
        var padded = Cell(f, "padded");
        padded.Padding = new CellPadding(20, 10, 30, 5);
        f.Document.Body.Add(new TableBlock("t", [new TableRow([plain, padded])], [Twip.FromPoints(100), Twip.FromPoints(100)]) { StyleName = "T" });

        var table = f.Layout().Pages[0].Tables.Single();
        var first = table.Cells.Single(cell => cell.Column == 0);
        var second = table.Cells.Single(cell => cell.Column == 1);
        var plainLine = first.Lines.Single();
        var paddedLine = second.Lines.Single();

        Assert.Equal(first.Bounds.X + 2, plainLine.Left, 1);
        Assert.Equal(second.Bounds.X + 30, paddedLine.Left, 1);
        Assert.Equal(second.Bounds.Y + 20, paddedLine.Top, 1);
        Assert.Equal(paddedLine.Line.Height + 30, table.Bounds.Height, 1);
    }
}
