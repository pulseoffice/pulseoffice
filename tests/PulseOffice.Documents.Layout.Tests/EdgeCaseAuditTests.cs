using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class EdgeCaseAuditTests
{
    [Fact(Timeout = 20000)]
    public async Task UnaRigaVuotaPiuAltaDellaPaginaNonBloccaLImpaginazione()
    {
        var f = new LayoutFixture(width: 200, height: 60);
        f.Document.Styles.Table.Add(new TableStyle("T") { Table = new TableProperties { CellPadding = 0, BorderWidth = 0 } });
        f.Add("prima");
        var row = new TableRow([new TableCell(new BlockContainer()), new TableCell(new BlockContainer())]) { Height = Twip.FromPoints(500) };
        f.Document.Body.Add(new TableBlock("t", [row], [Twip.FromPoints(60), Twip.FromPoints(60)]) { StyleName = "T" });
        f.Add("dopo");

        var layout = await Task.Run(() => f.Layout(), TestContext.Current.CancellationToken);

        Assert.Contains(layout.Pages, page => page.Tables.Count > 0);
        Assert.Contains(layout.Pages.SelectMany(page => page.Lines), line => string.Concat(line.Line.Runs.Select(run => run.Text)).Contains("dopo", StringComparison.Ordinal));
    }

    [Fact]
    public void UnaColonnaPiuStrettaDiUnaEmojiNonSpezzaLaCoppiaSurrogata()
    {
        var f = new LayoutFixture(width: 5, height: 200);
        var block = f.Add("😀😀");

        var lines = f.LineTexts(f.Layout(), block.Id);

        Assert.Equal(["😀", "😀"], lines);
    }
}
