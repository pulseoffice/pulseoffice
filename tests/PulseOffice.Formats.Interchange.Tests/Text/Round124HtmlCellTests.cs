using System.Text;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.Interchange.Tests.Text;

public sealed class Round124HtmlCellTests
{
    private readonly TextInterchange _formats = new();

    private TextDocument Document(string html) => TextInterchange.Read(Encoding.UTF8.GetBytes(html), _formats.HtmlReader);

    private TableBlock Table(string html) => Document(html).Body.Blocks.OfType<TableBlock>().Single();

    [Fact]
    public void The_reader_takes_the_cell_padding_from_css()
    {
        var cell = Table("<table><tr><td style=\"border:1pt solid black;padding:4pt 6pt 8pt 10pt\">a</td></tr></table>").Rows[0].Cells[0];

        Assert.Equal((4d, 6d, 8d, 10d), (cell.Borders!.PaddingTop, cell.Borders.PaddingRight, cell.Borders.PaddingBottom, cell.Borders.PaddingLeft));
    }

    [Fact]
    public void A_padding_longhand_overrides_the_shorthand_and_needs_no_border()
    {
        var cell = Table("<table><tr><td style=\"padding:3pt;padding-left:9pt\">a</td></tr></table>").Rows[0].Cells[0];

        Assert.False(cell.Borders!.HasLines);
        Assert.Equal(3, cell.Borders.PaddingTop);
        Assert.Equal(9, cell.Borders.PaddingLeft);
    }

    [Fact]
    public void The_writer_writes_the_cell_padding_and_it_reads_back()
    {
        var document = Document("<table><tr><td style=\"border:1pt solid black;padding:4pt 6pt 8pt 10pt\">a</td></tr></table>");

        var html = Encoding.UTF8.GetString(TextInterchange.Bytes(document, _formats.HtmlWriter));
        var cell = TextInterchange.Read(Encoding.UTF8.GetBytes(html), _formats.HtmlReader).Body.Blocks.OfType<TableBlock>().Single().Rows[0].Cells[0];

        Assert.Contains("padding:4pt 6pt 8pt 10pt", html, StringComparison.Ordinal);
        Assert.Equal((4d, 6d, 8d, 10d), (cell.Borders!.PaddingTop, cell.Borders.PaddingRight, cell.Borders.PaddingBottom, cell.Borders.PaddingLeft));
    }

    [Fact]
    public void A_cell_font_family_and_size_reach_the_runs()
    {
        var cell = Table("<table><tr><td style=\"font-family:'Georgia';font-size:14pt;text-decoration:underline line-through\">a</td></tr></table>").Rows[0].Cells[0];

        var run = cell.Content.Blocks.OfType<ParagraphBlock>().Single().Content.Runs.Single();
        Assert.Equal("Georgia", run.Font);
        Assert.Equal(14, run.FontSize);
        Assert.True(run.Strikethrough);
        Assert.True(run.Underline);
    }
}
