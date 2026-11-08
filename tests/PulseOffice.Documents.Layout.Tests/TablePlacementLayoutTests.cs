using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class TablePlacementLayoutTests
{
    private TableCell Cell(LayoutFixture f, string text) =>
        new(new BlockContainer([new ParagraphBlock(f.Document.NextId("c"), new TextParagraph(text) { Runs = [new TextRun(text)] })]));

    private double TableLeft(TableAlignment alignment, Twip indent)
    {
        var f = new LayoutFixture(width: 200, height: 200);
        f.Document.Styles.Table.Add(new TableStyle("T") { Table = new TableProperties { CellPadding = 0, BorderWidth = 0 } });
        f.Document.Body.Add(new TableBlock("t", [new TableRow([Cell(f, "a"), Cell(f, "b")])], [Twip.FromPoints(30), Twip.FromPoints(30)])
        {
            StyleName = "T",
            Alignment = alignment,
            Indent = indent,
        });
        return f.Layout().Pages[0].Tables.Single().Bounds.X;
    }

    [Fact]
    public void LaTabellaSiDisponeFraIMarginiSecondoLAllineamento()
    {
        var left = TableLeft(TableAlignment.Left, Twip.Zero);

        Assert.Equal(70, TableLeft(TableAlignment.Center, Twip.Zero) - left, 3);
        Assert.Equal(140, TableLeft(TableAlignment.Right, Twip.Zero) - left, 3);
        Assert.Equal(20, TableLeft(TableAlignment.Left, Twip.FromPoints(20)) - left, 3);
    }
}
