using System.Text;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.Interchange.Tests.Text;

public sealed class Round124RtfTableTests
{
    private readonly TextInterchange _formats = new();

    private TableBlock RtfTable(string rtf) =>
        TextInterchange.Read(Encoding.ASCII.GetBytes(rtf), _formats.RtfReader).Body.Blocks.OfType<TableBlock>().Single();

    private TextDocument Html(string html) => TextInterchange.Read(Encoding.UTF8.GetBytes(html), _formats.HtmlReader);

    private string Rtf(TextDocument document) => Encoding.ASCII.GetString(TextInterchange.Bytes(document, _formats.RtfWriter));

    [Fact]
    public void The_reader_keeps_cell_borders_with_their_style_width_and_colour()
    {
        var table = RtfTable(@"{\rtf1\ansi{\colortbl;\red255\green0\blue0;}\trowd\clbrdrt\brdrdash\brdrw30\brdrcf1\clbrdrb\brdrs\brdrw10\cellx2000\clbrdrl\brdrnone\cellx4000\pard\intbl a\cell b\cell\row}");

        var first = table.Rows[0].Cells[0].Borders!;
        Assert.Equal(new BorderLine(1.5, 0xFFFF0000) { Style = BorderLineStyle.Dashed }, first.Top);
        Assert.Equal(0.5, first.Bottom!.Width);
        Assert.Null(first.Left);
        Assert.Null(table.Rows[0].Cells[1].Borders);
    }

    [Fact]
    public void The_reader_takes_the_cell_padding_with_the_swapped_left_and_top_words()
    {
        var table = RtfTable(@"{\rtf1\ansi\trowd\clpadl40\clpadfl3\clpadt100\clpadft3\clpadb60\clpadfb3\clpadr80\clpadfr3\cellx2000\pard\intbl a\cell\row}");

        var borders = table.Rows[0].Cells[0].Borders!;
        Assert.Equal((2d, 5d, 3d, 4d), (borders.PaddingTop, borders.PaddingLeft, borders.PaddingBottom, borders.PaddingRight));
    }

    [Fact]
    public void A_borderless_table_is_written_without_borders()
    {
        var rtf = Rtf(Html("<table><tr><td>a</td><td>b</td></tr></table>"));

        Assert.DoesNotContain(@"\clbrdr", rtf, StringComparison.Ordinal);
    }

    [Fact]
    public void Cell_borders_and_padding_survive_writing_and_reading_rtf()
    {
        var document = Html("<table><tr><td style=\"border-top:1.5pt dashed #FF0000;border-left:2pt double #0000FF;padding:2pt 4pt 3pt 5pt\">a</td><td>b</td></tr></table>");

        var cells = RtfTable(Rtf(document)).Rows[0].Cells;

        Assert.Equal(new BorderLine(1.5, 0xFFFF0000) { Style = BorderLineStyle.Dashed }, cells[0].Borders!.Top);
        Assert.Equal(new BorderLine(2, 0xFF0000FF) { Style = BorderLineStyle.Double }, cells[0].Borders!.Left);
        Assert.Null(cells[0].Borders!.Bottom);
        var box = cells[0].Borders!;
        Assert.Equal((2d, 4d, 3d, 5d), (box.PaddingTop, box.PaddingRight, box.PaddingBottom, box.PaddingLeft));
        Assert.Null(cells[1].Borders);
    }

    [Fact]
    public void Merged_cells_keep_their_spans_through_rtf()
    {
        var document = Html("<table><tr><td colspan=\"2\" rowspan=\"2\">a</td><td>b</td></tr><tr><td>c</td></tr><tr><td>d</td><td>e</td><td>f</td></tr></table>");

        var table = RtfTable(Rtf(document));

        Assert.Equal(2, table.Rows[0].Cells[0].ColumnSpan);
        Assert.Equal(2, table.Rows[0].Cells[0].RowSpan);
        Assert.Equal("f", table.Rows[2].Cells[2].Content.Blocks.OfType<ParagraphBlock>().Single().Content.Text);
    }
}
