using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class TableTurnedAndSplitTests
{
    private TableCell Cell(LayoutFixture f, params string[] texts) =>
        new(new BlockContainer([.. texts.Select(text => (Block)new ParagraphBlock(f.Document.NextId("c"), new TextParagraph(text) { Runs = [new TextRun(text)] }))]));

    private IEnumerable<TurnedBox> Turns(TableBox table) =>
        table.Cells.SelectMany(cell => cell.Content.OfType<TurnedBox>().Concat(cell.Content.OfType<TableBox>().SelectMany(Turns)));

    private void Plain(LayoutFixture f) =>
        f.Document.Styles.Table.Add(new TableStyle("T") { Table = new TableProperties { CellPadding = 1, VerticalCellPadding = 1, BorderWidth = 0 } });

    [Theory]
    [InlineData(CellTextDirection.TopToBottom, 90)]
    [InlineData(CellTextDirection.BottomToTop, 270)]
    public void AVerticalCellIsLaidOutTurnedAndItsRowGrowsToTheTextLength(CellTextDirection direction, double degrees)
    {
        var f = new LayoutFixture(width: 400, height: 600);
        Plain(f);
        var turned = Cell(f, "a rather long vertical heading");
        turned.TextDirection = direction;
        f.Document.Body.Add(new TableBlock("t", [new TableRow([turned, Cell(f, "x")])], [Twip.FromPoints(40), Twip.FromPoints(100)]) { StyleName = "T" });

        var table = f.Layout().Pages[0].Tables.Single();
        var cell = table.Cells.Single(item => item.Column == 0);
        var turn = cell.Content.OfType<TurnedBox>().Single();
        var line = Assert.Single(turn.Lines);
        var length = line.Line.Runs.Sum(run => run.Width);

        Assert.Equal(degrees, turn.RotationDegrees);
        Assert.Single(cell.Lines);
        Assert.True(table.Bounds.Height >= length, $"row {table.Bounds.Height} shorter than the text {length}");
        Assert.True(turn.RotatedBounds.Width <= cell.Bounds.Width + 0.01);
        Assert.True(turn.RotatedBounds.X >= cell.Bounds.X - 0.01 && turn.RotatedBounds.Bottom <= cell.Bounds.Bottom + 0.01);
    }

    [Fact]
    public void AVerticalCellInAnExactRowKeepsTheRowHeight()
    {
        var f = new LayoutFixture(width: 400, height: 600);
        Plain(f);
        var turned = Cell(f, "short");
        turned.TextDirection = CellTextDirection.TopToBottom;
        f.Document.Body.Add(new TableBlock("t", [new TableRow([turned]) { Height = Twip.FromPoints(80), MinimumHeight = false }], [Twip.FromPoints(60)]) { StyleName = "T" });

        var table = f.Layout().Pages[0].Tables.Single();

        Assert.Equal(80, table.Bounds.Height, 1);
        Assert.Equal(78, table.Cells.Single().Content.OfType<TurnedBox>().Single().Bounds.Width, 1);
    }

    [Fact]
    public void AClickInsideATurnedCellLandsInItsParagraph()
    {
        var f = new LayoutFixture(width: 400, height: 600);
        Plain(f);
        var turned = Cell(f, "vertical words here");
        turned.TextDirection = CellTextDirection.TopToBottom;
        var other = Cell(f, "beside");
        f.Document.Body.Add(new TableBlock("t", [new TableRow([turned, other])], [Twip.FromPoints(30), Twip.FromPoints(200)]) { StyleName = "T" });
        var layout = f.Layout();
        var cell = layout.Pages[0].Tables.Single().Cells.Single(item => item.Column == 0);
        var paragraph = turned.Content.Blocks.OfType<ParagraphBlock>().Single().Id;

        var hit = f.Queries.HitTest(layout, 0, cell.Bounds.X + cell.Bounds.Width / 2, cell.Bounds.Bottom - 3);
        var beside = f.Queries.HitTest(layout, 0, cell.Bounds.Right + 20, cell.Bounds.Y + 4);

        Assert.Equal(paragraph, hit?.ParagraphId);
        Assert.True(hit!.Value.Offset > 5);
        Assert.Equal(other.Content.Blocks.OfType<ParagraphBlock>().Single().Id, beside?.ParagraphId);
    }

    [Fact]
    public void ARowSplitAcrossPagesKeepsEachCellsOwnBottomPadding()
    {
        var f = new LayoutFixture(width: 300, height: 200);
        Plain(f);
        var padded = Cell(f, [.. Enumerable.Range(0, 30).Select(i => $"line {i}")]);
        padded.Padding = new CellPadding(25, 25, 2, 2);
        f.Document.Body.Add(new TableBlock("t", [new TableRow([padded])], [Twip.FromPoints(200)]) { StyleName = "T" });

        var layout = f.Layout();
        var cells = layout.Pages.SelectMany(page => page.Tables.SelectMany(table => table.Cells).Select(cell => (page, cell))).ToList();

        Assert.True(cells.Count > 1);
        foreach (var (page, cell) in cells)
        {
            Assert.True(cell.Bounds.Bottom <= page.Height + 0.01, $"cell bottom {cell.Bounds.Bottom} past page {page.Height}");
            foreach (var line in cell.Lines) Assert.True(line.Top + line.Line.Height <= cell.Bounds.Bottom - 25 + 0.01, $"line bottom {line.Top + line.Line.Height} inside padding of {cell.Bounds.Bottom}");
        }
        var texts = layout.Pages.SelectMany(page => page.Tables.SelectMany(table => table.Lines)).Select(line => line.Line.Start).Count();
        Assert.Equal(30, texts);
    }

    [Fact]
    public void ANestedTableTallerThanAPageBreaksBetweenItsRows()
    {
        var f = new LayoutFixture(width: 300, height: 200);
        Plain(f);
        var inner = new TableBlock("inner", [.. Enumerable.Range(0, 40).Select(i => new TableRow([Cell(f, $"row {i}")]))], [Twip.FromPoints(150)]) { StyleName = "T" };
        var outer = new TableCell(new BlockContainer([inner]));
        f.Document.Body.Add(new TableBlock("outer", [new TableRow([outer])], [Twip.FromPoints(200)]) { StyleName = "T" });

        var layout = f.Layout();
        var lines = layout.Pages.SelectMany(page => page.Tables.SelectMany(table => table.Lines).Distinct().Select(line => (page, line))).ToList();

        Assert.True(layout.Pages.Count > 1);
        Assert.Equal(40, lines.Count);
        foreach (var (page, line) in lines) Assert.True(line.Top + line.Line.Height <= page.Height + 0.01, $"nested line at {line.Top} beyond page bottom");
    }

    [Fact]
    public void ASingleNestedRowTallerThanAPageContinuesOnTheNextPage()
    {
        var f = new LayoutFixture(width: 300, height: 200);
        Plain(f);
        var inner = new TableBlock("inner", [new TableRow([Cell(f, [.. Enumerable.Range(0, 40).Select(i => $"nested {i}")]), Cell(f, "side")])], [Twip.FromPoints(100), Twip.FromPoints(50)]) { StyleName = "T" };
        f.Document.Body.Add(new TableBlock("outer", [new TableRow([new TableCell(new BlockContainer([inner]))])], [Twip.FromPoints(200)]) { StyleName = "T" });

        var layout = f.Layout();
        var lines = layout.Pages.SelectMany(page => page.Tables.SelectMany(table => table.Lines).Distinct().Select(line => (page, line))).ToList();

        Assert.True(layout.Pages.Count > 1);
        Assert.Equal(41, lines.Count);
        foreach (var (page, line) in lines) Assert.True(line.Top + line.Line.Height <= page.Height + 0.01, $"nested line at {line.Top} beyond page bottom {page.Height}");
    }

    [Fact]
    public void AVerticalCellLongerThanAPageIsCappedAtThePageAndWrapsIntoMoreLines()
    {
        var f = new LayoutFixture(width: 300, height: 200);
        Plain(f);
        var turned = Cell(f, string.Join(' ', Enumerable.Range(0, 60).Select(i => $"word{i}")));
        turned.TextDirection = CellTextDirection.TopToBottom;
        f.Document.Body.Add(new TableBlock("t", [new TableRow([turned])], [Twip.FromPoints(250)]) { StyleName = "T" });

        var layout = f.Layout();
        var page = layout.Pages[0];
        var table = page.Tables.Single();
        var turn = table.Cells.Single().Content.OfType<TurnedBox>().Single();

        Assert.True(table.Bounds.Bottom <= page.Height + 0.01, $"row bottom {table.Bounds.Bottom} past page {page.Height}");
        Assert.True(turn.Lines.Count() > 1);
        Assert.Equal(60, string.Concat(turn.Lines.SelectMany(line => line.Line.Runs).Select(run => run.Text)).Split("word").Length - 1);
    }

    [Fact]
    public void OnAPageTallerThanTwentyTwoInchesANestedVerticalCellUsesThePageHeight()
    {
        var f = new LayoutFixture(width: 400, height: 4000);
        Plain(f);
        var turned = Cell(f, string.Join(' ', Enumerable.Range(0, 400).Select(i => $"w{i:000}")));
        turned.TextDirection = CellTextDirection.TopToBottom;
        var inner = new TableBlock("inner", [new TableRow([turned])], [Twip.FromPoints(200)]) { StyleName = "T" };
        f.Document.Body.Add(new TableBlock("outer", [new TableRow([new TableCell(new BlockContainer([inner]))])], [Twip.FromPoints(250)]) { StyleName = "T" });

        var page = f.Layout().Pages[0];
        var turn = Assert.Single(page.Tables.SelectMany(Turns));
        var longest = turn.Lines.Max(line => line.Line.Runs.Sum(run => run.Width));

        Assert.True(longest > 1600, $"longest lying line {longest} is still capped at 22 in");
        Assert.True(turn.RotatedBounds.Bottom <= page.Height + 0.01);
    }

    [Fact]
    public void AVerticalCellInsideANestedTableIsCappedAtThePageNotAtTwentyTwoInches()
    {
        var f = new LayoutFixture(width: 300, height: 200);
        Plain(f);
        var turned = Cell(f, string.Join(' ', Enumerable.Range(0, 60).Select(i => $"word{i}")));
        turned.TextDirection = CellTextDirection.TopToBottom;
        var inner = new TableBlock("inner", [new TableRow([turned])], [Twip.FromPoints(200)]) { StyleName = "T" };
        f.Document.Body.Add(new TableBlock("outer", [new TableRow([new TableCell(new BlockContainer([inner]))])], [Twip.FromPoints(250)]) { StyleName = "T" });

        var layout = f.Layout();
        var page = layout.Pages[0];
        var turns = page.Tables.SelectMany(Turns).ToList();
        var turn = Assert.Single(turns);

        Assert.Single(layout.Pages);
        Assert.True(turn.RotatedBounds.Bottom <= page.Height + 0.01, $"nested vertical text bottom {turn.RotatedBounds.Bottom} past page {page.Height}");
        Assert.True(turn.Lines.Count() > 1);
        Assert.Equal(60, string.Concat(turn.Lines.SelectMany(line => line.Line.Runs).Select(run => run.Text)).Split("word").Length - 1);
    }
}
