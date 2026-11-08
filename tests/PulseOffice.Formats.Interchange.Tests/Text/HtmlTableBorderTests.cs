using System.Text;
using PulseOffice.Documents.Blocks;
using PulseOffice.Formats.Interchange.Text.Html;
using Xunit;

namespace PulseOffice.Formats.Interchange.Tests.Text;

public sealed class HtmlTableBorderTests
{
    private readonly TextInterchange _formats = new();

    private TableBlock Table(string html) =>
        TextInterchange.Read(Encoding.UTF8.GetBytes(html), _formats.HtmlReader).Body.Blocks.OfType<TableBlock>().Single();

    [Fact]
    public void Una_tabella_con_bordo_incollata_da_un_altra_app_tiene_i_bordi_delle_celle()
    {
        var table = Table("<table border=\"1\"><tr><td>Citta</td><td>Vendite</td></tr><tr><td>Bari</td><td>950,5</td></tr></table>");
        Assert.All(table.Rows.SelectMany(row => row.Cells), cell => Assert.NotNull(cell.Borders?.Top));
    }

    [Fact]
    public void Senza_bordo_o_con_bordo_nullo_nello_stile_le_celle_restano_senza_bordi()
    {
        Assert.All(Table("<table><tr><td>a</td></tr></table>").Rows.SelectMany(row => row.Cells), cell => Assert.Null(cell.Borders));
        Assert.All(Table("<table border=\"1\"><tr><td style=\"border: none\">a</td></tr></table>").Rows.SelectMany(row => row.Cells), cell => Assert.Null(cell.Borders));
    }

    [Fact]
    public void La_lettura_dei_bordi_e_la_stessa_per_le_celle_di_Ledger()
    {
        var reader = new HtmlTableReader(new HtmlTokenizer(), new CssDeclarations(), new HtmlCellBorders(new CssDeclarations()));
        var table = reader.FirstTable("<table border=\"1\"><tr><td>a</td><td style=\"border:none\">b</td><td style=\"border-bottom:2pt solid #FF0000\">c</td></tr></table>")!;
        Assert.NotNull(table.Rows[0][0]!.Borders?.Top);
        Assert.Null(table.Rows[0][1]!.Borders);
        var bottom = table.Rows[0][2]!.Borders!;
        Assert.Equal(new HtmlBorderLine(2, 0xFFFF0000), bottom.Bottom);
        Assert.NotNull(bottom.Top);
        Assert.Null(reader.FirstTable("<table><tr><td style=\"border-left:1px solid blue\">x</td></tr></table>")!.Rows[0][0]!.Borders!.Top);
    }

    [Fact]
    public void Lo_stile_delle_celle_di_Ledger_arriva_nei_paragrafi_e_nei_tratti_della_tabella()
    {
        var table = Table("<table><tr><td style=\"font-weight:bold;font-style:italic\">Totale</td><td style=\"text-align:right\">1.250</td></tr></table>");
        var cells = table.Rows[0].Cells;
        var label = (ParagraphBlock)cells[0].Content[0];
        var number = (ParagraphBlock)cells[1].Content[0];
        Assert.All(label.Content.Runs, run => Assert.True(run.Bold && run.Italic));
        Assert.Equal(PulseOffice.Text.Rich.TextAlign.Right, number.Content.Align);
        Assert.All(number.Content.Runs, run => Assert.False(run.Bold));
    }
}
