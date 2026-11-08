using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class MergedRowTests
{
    private TableCell Cell(LayoutFixture f, string text) =>
        new(new BlockContainer([new ParagraphBlock(f.Document.NextId("c"), new TextParagraph(text) { Runs = [new TextRun(text)], Widows = 0, Orphans = 0 })]));

    private string Words(int count) => string.Join(' ', Enumerable.Range(0, count).Select(index => "w" + index.ToString("D3", System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void UnaRigaConCelleUniteInVerticaleSiSpezza()
    {
        var f = new LayoutFixture(width: 200, height: 60);
        f.Document.Styles.Table.Add(new TableStyle("T") { Table = new TableProperties { CellPadding = 0, BorderWidth = 0 } });
        f.Add("riga");
        f.Add("riga");
        var tall = Cell(f, Words(10));
        tall.RowSpan = 3;
        var rows = new List<TableRow>
        {
            new([tall, Cell(f, "b0")]),
            new([new TableCell(new BlockContainer()) { Covered = true }, Cell(f, "b1")]),
            new([new TableCell(new BlockContainer()) { Covered = true }, Cell(f, "b2")]),
        };
        f.Document.Body.Add(new TableBlock("t", rows, [Twip.FromPoints(24), Twip.FromPoints(60)]) { StyleName = "T" });

        var layout = f.Layout();
        var words = layout.Pages.SelectMany(page => page.Tables.SelectMany(table => table.Lines)).Select(line => string.Concat(line.Line.Runs.Select(run => run.Text)).Trim()).Where(text => text.StartsWith('w')).ToList();

        Assert.Single(layout.Pages[0].Tables);
        Assert.Equal(Enumerable.Range(0, 10).Select(index => "w" + index.ToString("D3", System.Globalization.CultureInfo.InvariantCulture)), words);
        Assert.All(layout.Pages, page => Assert.All(page.Lines, line => Assert.True(line.Top + line.Line.Height <= 60 + 0.001)));
        Assert.Contains(layout.Pages[1].Tables.Single().Lines, line => line.ParagraphId == ((ParagraphBlock)tall.Content[0]).Id);
    }

    [Theory]
    [InlineData(6, 30)]
    [InlineData(14, 30)]
    [InlineData(6, 4)]
    public void UnaCellaUnitaCheIniziaNellaRigaSpezzataProsegueSulleRigheDelGruppo(int right, int merged)
    {
        var f = new LayoutFixture(width: 200, height: 60);
        f.Document.Styles.Table.Add(new TableStyle("T") { Table = new TableProperties { CellPadding = 0, BorderWidth = 0 } });
        f.Add("riga");
        f.Add("riga");
        var tall = Cell(f, Words(merged));
        tall.RowSpan = 2;
        var rows = new List<TableRow>
        {
            new([tall, Cell(f, string.Join(' ', Enumerable.Range(0, right).Select(index => "r" + index.ToString("D2", System.Globalization.CultureInfo.InvariantCulture))))]),
            new([new TableCell(new BlockContainer()) { Covered = true }, Cell(f, "b1")]),
        };
        f.Document.Body.Add(new TableBlock("t", rows, [Twip.FromPoints(24), Twip.FromPoints(60)]) { StyleName = "T" });

        var layout = f.Layout();
        var lines = layout.Pages.SelectMany(page => page.Tables.SelectMany(table => table.Lines)).ToList();
        var words = lines.Select(line => string.Concat(line.Line.Runs.Select(run => run.Text)).Trim()).Where(text => text.StartsWith('w')).ToList();
        var mergedId = ((ParagraphBlock)tall.Content[0]).Id;

        Assert.Equal(Enumerable.Range(0, merged).Select(index => "w" + index.ToString("D3", System.Globalization.CultureInfo.InvariantCulture)), words);
        Assert.All(layout.Pages, page => Assert.All(page.Tables.SelectMany(table => table.Lines), line => Assert.True(line.Top + line.Line.Height <= 60 + 0.001)));
        Assert.All(layout.Pages, page => Assert.All(page.Tables, table => Assert.True(table.Bounds.Bottom <= 60 + 0.001)));
        if (merged > 10) Assert.Contains(layout.Pages.Skip(1).SelectMany(page => page.Tables.SelectMany(table => table.Lines)), line => line.ParagraphId == mergedId);
    }

    [Fact]
    public void LeCelleSpezzateMantengonoLAllineamentoVerticale()
    {
        var f = new LayoutFixture(width: 200, height: 60);
        f.Document.Styles.Table.Add(new TableStyle("T") { Table = new TableProperties { CellPadding = 0, BorderWidth = 0 } });
        f.Add("riga");
        var middle = Cell(f, "m");
        middle.VerticalAlign = CellVerticalAlign.Middle;
        f.Document.Body.Add(new TableBlock("t", [new TableRow([Cell(f, Words(8)), middle])], [Twip.FromPoints(24), Twip.FromPoints(60)]) { StyleName = "T" });

        var layout = f.Layout();
        var line = layout.Pages[0].Tables.Single().Lines.Single(item => item.ParagraphId == ((ParagraphBlock)middle.Content[0]).Id);

        Assert.Equal(12 + (48 - 12) / 2.0, line.Top, 3);
    }
}
