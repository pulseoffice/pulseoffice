using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class TableAppearanceLayoutTests
{
    private TableCell Cell(LayoutFixture f, string text) =>
        new(new BlockContainer([new ParagraphBlock(f.Document.NextId("c"), new TextParagraph(text) { Runs = [new TextRun(text)], Widows = 0, Orphans = 0 })]));

    private TableBlock Table(LayoutFixture f, int rows, string style) =>
        new(f.Document.NextId("t"), Enumerable.Range(0, rows).Select(r => new TableRow([Cell(f, $"r{r}a"), Cell(f, $"r{r}b")])), [Twip.FromPoints(60), Twip.FromPoints(60)]) { StyleName = style };

    [Fact]
    public void IBordiPropriDellaCellaSiDisegnanoLatoPerLato()
    {
        var f = new LayoutFixture(width: 200, height: 200);
        f.Document.Styles.Table.Add(new TableStyle("T") { Table = new TableProperties { CellPadding = 2, BorderWidth = 0.5, BorderColor = 0xFF000000 } });
        var table = Table(f, 2, "T");
        table.Cell(0, 0).Borders = new BoxBorders { Bottom = new BorderLine(2, 0xFFFF0000) { Style = BorderLineStyle.Dashed } };
        table.Cell(1, 1).Background = 0xFF00FF00;
        f.Document.Body.Add(table);

        var cells = f.Layout().Pages[0].Tables.Single().Cells;
        var decorations = cells.SelectMany(cell => cell.Content.OfType<DecorationBox>()).ToList();

        var edge = Assert.Single(decorations, box => box.Kind == DecorationKind.CellEdge);
        Assert.Equal(BorderLineStyle.Dashed, edge.LineStyle);
        Assert.Equal(0xFFFF0000u, edge.Color);
        Assert.Equal(3, decorations.Count(box => box.Kind == DecorationKind.CellBorder));
        Assert.Contains(decorations, box => box.Kind == DecorationKind.CellBackground && box.Color == 0xFF00FF00);
    }

    [Fact]
    public void LIntestazioneDelloStilePrendeColoreGrassettoEDLeBande()
    {
        var f = new LayoutFixture(width: 200, height: 200);
        f.Document.Styles.Table.Add(new TableStyle("T") { Table = new TableProperties { CellPadding = 2, BorderWidth = 0, HeaderBackground = 0xFF2563EB, HeaderColor = 0xFFFFFFFF, HeaderBold = true, BandBackground = 0xFFDBEAFE } });
        var table = Table(f, 4, "T");
        table.HeaderRows = 1;
        f.Document.Body.Add(table);

        var cells = f.Layout().Pages[0].Tables.Single().Cells;

        var header = cells.First(cell => cell.Row == 0);
        var run = header.Lines.First().Line.Runs.First(item => item.Kind == GlyphRunKind.Text);
        Assert.Equal(0xFFFFFFFFu, run.Style!.Color);
        Assert.True(run.Style.Bold);
        Assert.Contains(header.Content.OfType<DecorationBox>(), box => box.Color == 0xFF2563EB);
        Assert.DoesNotContain(cells.First(cell => cell.Row == 1).Content.OfType<DecorationBox>(), box => box.Kind == DecorationKind.CellBackground);
        Assert.Contains(cells.First(cell => cell.Row == 2).Content.OfType<DecorationBox>(), box => box.Color == 0xFFDBEAFE);
        Assert.Null(cells.First(cell => cell.Row == 1).Lines.First().Line.Runs.First(item => item.Kind == GlyphRunKind.Text).Style!.Color);
    }

    [Fact]
    public void UnaRigaCheNonSiSpezzaPassaInteraAllaPaginaDopo()
    {
        var f = new LayoutFixture(width: 200, height: 60);
        f.Document.Styles.Table.Add(new TableStyle("T") { Table = new TableProperties { CellPadding = 0, BorderWidth = 0 } });
        f.Add("riga");
        f.Add("riga");
        var words = string.Join(' ', Enumerable.Range(0, 6).Select(_ => "aaaaaaaaa"));
        var table = new TableBlock("t", [new TableRow([Cell(f, words), Cell(f, "x")]) { AllowBreakAcrossPages = false }], [Twip.FromPoints(60), Twip.FromPoints(60)]) { StyleName = "T" };
        f.Document.Body.Add(table);

        var layout = f.Layout();

        Assert.Empty(layout.Pages[0].Tables);
        Assert.Equal(6, layout.Pages[1].Tables.Single().Lines.Count(line => line.Line.Runs.Any(run => run.Text.StartsWith('a'))));
    }
}
