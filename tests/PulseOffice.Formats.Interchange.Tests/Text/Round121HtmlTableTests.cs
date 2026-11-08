using System.Globalization;
using System.Text;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Formats.Interchange.Text.Html;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.Interchange.Tests.Text;

public sealed class Round121HtmlTableTests
{
    private readonly TextInterchange _formats = new();
    private readonly HtmlTableReader _tables = new(new HtmlTokenizer(), new CssDeclarations(), new HtmlCellBorders(new CssDeclarations()));

    private TextDocument Document(string html) => TextInterchange.Read(Encoding.UTF8.GetBytes(html), _formats.HtmlReader);

    private TableBlock Table(string html) => Document(html).Body.Blocks.OfType<TableBlock>().Single();

    private HtmlTableCell Cell(string cell) => _tables.FirstTable("<table><tr>" + cell + "</tr></table>")!.Rows[0][0]!;

    [Fact]
    public void Border_longhands_are_read_for_every_edge()
    {
        var edges = Cell("<td style=\"border-width:2pt;border-style:solid;border-color:#FF0000\">a</td>").Borders!;

        Assert.All(new[] { edges.Top, edges.Right, edges.Bottom, edges.Left }, line => Assert.Equal(new HtmlBorderLine(2, 0xFFFF0000), line));
    }

    [Fact]
    public void Side_longhands_override_the_shorthand()
    {
        var edges = Cell("<td style=\"border:1pt solid black;border-top-width:3pt;border-left-style:none;border-bottom-color:blue\">a</td>").Borders!;

        Assert.Equal(3, edges.Top!.WidthPoints);
        Assert.Null(edges.Left);
        Assert.Equal(0xFF0000FFu, edges.Bottom!.Color);
        Assert.Equal(new HtmlBorderLine(1, 0xFF000000), edges.Right);
    }

    [Fact]
    public void A_border_colour_written_as_rgb_with_spaces_is_kept()
    {
        Assert.Equal(0xFFFF0000u, Cell("<td style=\"border:1px solid rgb(255, 0, 0)\">a</td>").Borders!.Top!.Color);
    }

    [Fact]
    public void The_border_style_is_read()
    {
        Assert.Equal(BorderLineStyle.Dashed, Cell("<td style=\"border:1pt dashed black\">a</td>").Borders!.Top!.Style);
        Assert.Equal(BorderLineStyle.Double, Cell("<td style=\"border-style:double;border-width:3pt\">a</td>").Borders!.Top!.Style);
    }

    [Fact]
    public void Folio_reads_border_longhands_and_style()
    {
        var cell = Table("<table><tr><td style=\"border-width:2pt;border-style:dotted;border-color:red\">a</td></tr></table>").Rows[0].Cells[0];

        Assert.Equal(new BorderLine(2, 0xFFFF0000) { Style = BorderLineStyle.Dotted }, cell.Borders!.Top);
    }

    [Fact]
    public void A_nested_table_in_a_sheet_cell_keeps_its_cells_apart()
    {
        var table = _tables.FirstTable("<table><tr><td>x<table><tr><td>a</td><td>b</td></tr><tr><td>c</td><td>d</td></tr></table></td><td>y</td></tr></table>")!;

        Assert.Equal(2, table.ColumnCount);
        Assert.Equal("x\na\tb\nc\td", table.Rows[0][0]!.Text);
        Assert.Equal("y", table.Rows[0][1]!.Text);
    }

    [Fact]
    public void A_stray_table_end_before_the_table_does_not_hide_it()
    {
        Assert.Equal("a", _tables.FirstTable("</table><table><tr><td>a</td></tr></table>")!.Rows[0][0]!.Text);
    }

    [Fact]
    public void Formatting_that_opens_a_cell_but_does_not_cover_it_is_not_the_cell_formatting()
    {
        Assert.False(Cell("<td><b>Total</b> 5</td>").Bold);
        Assert.False(Cell("<td><span style=\"color:red\">a</span>b</td>").ColorHex is not null);
    }

    [Fact]
    public void Formatting_that_covers_all_the_text_of_a_cell_is_the_cell_formatting()
    {
        Assert.True(Cell("<td>  <b>a</b> <b><i>b</i></b></td>").Bold);
        Assert.True(Cell("<td>a<i>b</i></td>") is { Italic: false });
        Assert.True(Cell("<td><u>a</u> <u>b</u></td>").Underline);
        Assert.Equal("#FF0000", Cell("<td><span style=\"color:red\">a</span><font color=\"red\">b</font></td>").ColorHex);
    }

    [Fact]
    public void Folio_keeps_a_nested_table_and_mixed_formatting_in_a_cell()
    {
        var outer = Table("<table><tr><td>x <b>bold</b> y<table><tr><td>a</td><td>b</td></tr></table></td><td>z</td></tr></table>");

        var first = outer.Rows[0].Cells[0];
        var inner = Assert.Single(first.Content.Blocks.OfType<TableBlock>());
        Assert.Equal(2, inner.Rows[0].Cells.Count);
        var runs = first.Content.Blocks.OfType<ParagraphBlock>().First().Content.Runs;
        Assert.Contains(runs, run => run.Text == "bold" && run.Bold);
        Assert.Contains(runs, run => run.Text.StartsWith("x", StringComparison.Ordinal) && !run.Bold);
        Assert.Equal("z", outer.Rows[0].Cells[1].Content.Blocks.OfType<ParagraphBlock>().Single().Content.Text);
    }

    [Fact]
    public void The_html_writer_writes_cell_borders_and_reads_them_back()
    {
        var document = Document("<table><tr><td style=\"border-top:2pt dashed #FF0000;border-bottom:1pt solid #0000FF\">a</td><td>b</td></tr></table>");

        var html = Encoding.UTF8.GetString(TextInterchange.Bytes(document, _formats.HtmlWriter));
        var cells = TextInterchange.Read(Encoding.UTF8.GetBytes(html), _formats.HtmlReader).Body.Blocks.OfType<TableBlock>().Single().Rows[0].Cells;

        Assert.Equal(new BorderLine(2, 0xFFFF0000) { Style = BorderLineStyle.Dashed }, cells[0].Borders!.Top);
        Assert.Equal(new BorderLine(1, 0xFF0000FF), cells[0].Borders!.Bottom);
        Assert.Null(cells[0].Borders!.Left);
        Assert.Null(cells[1].Borders);
    }

    [Fact]
    public void The_html_writer_does_not_draw_borders_on_a_borderless_table()
    {
        var html = Encoding.UTF8.GetString(TextInterchange.Bytes(Document("<table><tr><td>a</td></tr></table>"), _formats.HtmlWriter));

        Assert.DoesNotContain("border:1px", html, StringComparison.Ordinal);
    }

    [Fact]
    public void The_html_writer_keeps_nested_tables()
    {
        var document = Document("<table><tr><td>x<table><tr><td>a</td><td>b</td></tr></table></td></tr></table>");

        var html = Encoding.UTF8.GetString(TextInterchange.Bytes(document, _formats.HtmlWriter));
        var outer = TextInterchange.Read(Encoding.UTF8.GetBytes(html), _formats.HtmlReader).Body.Blocks.OfType<TableBlock>().Single();

        Assert.Equal(2, Assert.Single(outer.Rows[0].Cells[0].Content.Blocks.OfType<TableBlock>()).Rows[0].Cells.Count);
    }

    [Fact]
    public void The_ledger_html_fragment_carries_cell_borders()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Sheet1");
        var address = new CellAddress(0, 0);
        sheet.SetValue(address, CellValue.Text("a"));
        var edge = new BorderEdge(BorderStyle.Dashed, "#FF0000", PulseOffice.Foundation.Units.Twip.FromPoints(1));
        sheet.SetStyle(address, workbook.Styles.Intern(CellStyle.Default with { Borders = new CellBorders(BorderEdge.None, edge, BorderEdge.None, edge) }));
        var writer = new HtmlSpreadsheetWriter(new CellPresenter(new NumberFormatter(CultureInfo.InvariantCulture, serial => DateTime.FromOADate(serial))));

        var cell = _tables.FirstTable(writer.RenderFragment(sheet, new CellRange(address, address), workbook.Styles))!.Rows[0][0]!;

        Assert.Equal(new HtmlBorderLine(1, 0xFFFF0000) { Style = BorderLineStyle.Dashed }, cell.Borders!.Top);
        Assert.NotNull(cell.Borders.Bottom);
        Assert.Null(cell.Borders.Left);
    }
}
