using PulseOffice.Formats.Interchange.Text.Html;
using Xunit;

namespace PulseOffice.Formats.Interchange.Tests.Text;

public sealed class HtmlTableReaderTests
{
    private readonly HtmlTableReader _reader = new(new HtmlTokenizer(), new CssDeclarations(), new HtmlCellBorders(new CssDeclarations()));

    [Fact]
    public void LaTabellaDiExcelODiUnBrowserDiventaUnaGrigliaConLaFormattazione()
    {
        const string html = """
            <html><body><!--StartFragment--><table>
            <tr><th>Nome</th><th style="background:#FFFF00">Valore</th></tr>
            <tr><td><b>Rossi &amp; C.</b></td><td style="color:rgb(255, 0, 0);font-style:italic">1,5</td></tr>
            <tr><td colspan="2">Totale<br>finale</td></tr>
            <tr><td>dopo</td><td>x</td></tr>
            </table><!--EndFragment--></body></html>
            """;

        var table = _reader.FirstTable(html)!;

        Assert.Equal(4, table.Rows.Count);
        Assert.Equal(2, table.ColumnCount);
        Assert.True(table.Rows[0][0]!.Bold);
        Assert.Equal("#FFFF00", table.Rows[0][1]!.BackgroundHex);
        Assert.Equal("Rossi & C.", table.Rows[1][0]!.Text);
        Assert.True(table.Rows[1][0]!.Bold);
        Assert.Equal("#FF0000", table.Rows[1][1]!.ColorHex);
        Assert.True(table.Rows[1][1]!.Italic);
        Assert.Equal("Totale\nfinale", table.Rows[2][0]!.Text);
        Assert.Null(table.Rows[2][1]);
        Assert.Equal("x", table.Rows[3][1]!.Text);
    }

    [Fact]
    public void DidascaliaUnioniEAllineamentoDiUnaTabellaDelWebArrivanoAlFoglio()
    {
        const string html = """
            <table><caption style="font-weight:700">Popolazione</caption>
            <tr><th style="text-align: center">Regione</th><th>Abitanti</th></tr>
            <tr><td rowspan="2" style="vertical-align: middle">Isole</td><td style="text-align: right">4 794 512</td></tr>
            <tr><td align="right">1 569 832</td></tr>
            <tr><td colspan="2" style="text-align: start">Totale</td></tr>
            </table>
            """;

        var table = _reader.FirstTable(html)!;

        Assert.Equal("Popolazione", table.Caption);
        Assert.Equal(HtmlTextAlignment.Center, table.Rows[0][0]!.Alignment);
        Assert.Equal(2, table.Rows[1][0]!.RowSpan);
        Assert.Equal(HtmlVerticalAlignment.Middle, table.Rows[1][0]!.VerticalAlignment);
        Assert.Equal(HtmlTextAlignment.Right, table.Rows[1][1]!.Alignment);
        Assert.Equal(HtmlTextAlignment.Right, table.Rows[2][1]!.Alignment);
        Assert.Equal(2, table.Rows[3][0]!.ColumnSpan);
        Assert.Equal(HtmlTextAlignment.Unspecified, table.Rows[3][0]!.Alignment);
    }

    [Fact]
    public void SenzaTabellaNonCeGriglia() =>
        Assert.Null(_reader.FirstTable("<p>solo testo</p>"));
}
