using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class RepeatedHeaderCarryTests
{
    private TableCell Cell(LayoutFixture f, string text) =>
        new(new BlockContainer([new ParagraphBlock(f.Document.NextId("c"), new TextParagraph(text) { Runs = [new TextRun(text)], Widows = 0, Orphans = 0 })]));

    private string Word(int index) => "w" + index.ToString("D3", System.Globalization.CultureInfo.InvariantCulture);

    [Theory]
    [InlineData(10, 3)]
    [InlineData(20, 3)]
    [InlineData(14, 2)]
    public void AMergedCellCarriedPastAPageKeepsItsTextUnderTheRepeatedHeader(int count, int span)
    {
        var f = new LayoutFixture(width: 200, height: 60);
        f.Document.Styles.Table.Add(new TableStyle("T") { Table = new TableProperties { CellPadding = 0, BorderWidth = 0 } });
        f.Add("riga");
        var tall = Cell(f, string.Join(' ', Enumerable.Range(0, count).Select(Word)));
        tall.RowSpan = span;
        var rows = new List<TableRow> { new([Cell(f, "h0"), Cell(f, "h1")]), new([tall, Cell(f, "b0")]) };
        for (var r = 1; r < span; r++) rows.Add(new([new TableCell(new BlockContainer()) { Covered = true }, Cell(f, "b" + r)]));
        f.Document.Body.Add(new TableBlock("t", rows, [Twip.FromPoints(24), Twip.FromPoints(60)]) { StyleName = "T", HeaderRows = 1 });

        var layout = f.Layout();
        var words = layout.Pages.SelectMany(page => page.Tables.SelectMany(table => table.Lines))
            .Select(line => string.Concat(line.Line.Runs.Select(run => run.Text)).Trim()).Where(text => text.StartsWith('w')).ToList();

        Assert.Equal(Enumerable.Range(0, count).Select(Word), words);
    }
}
