using System.Globalization;
using PulseOffice.Exchange;
using PulseOffice.Formats.Interchange.Text.Html;
using PulseOffice.Sheets.Formatting;
using Xunit;

namespace PulseOffice.Shell.Tests;

public sealed class Round124HtmlTableGridsTests
{
    private static HtmlTableGrids Grids(string culture) =>
        new(new HtmlTableReader(new HtmlTokenizer(), new CssDeclarations(), new HtmlCellBorders(new CssDeclarations())), new FixedSpreadsheetLocale(new CultureInfo(culture)));

    private static ExchangePackage Html(string html) =>
        new() { Id = "p", Representations = [ExchangeRepresentation.OfText(ExchangeFlavor.Html, html)] };

    [Fact]
    public void Numbers_follow_the_spreadsheet_locale_not_the_current_culture()
    {
        var saved = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("en-US");
        try
        {
            var grid = Grids("it-IT").Grid(Html("<table><tr><td>1,5</td><td>1.234,5</td></tr></table>"))!;

            Assert.Equal(ExchangeCellKind.Number, grid.At(0, 0).Kind);
            Assert.Equal(1.5, grid.At(0, 0).Number);
            Assert.Equal(1234.5, grid.At(0, 1).Number);
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }

    [Fact]
    public void Spans_borders_and_alignment_reach_the_grid()
    {
        var grid = Grids("en-US").Grid(Html(
            "<table><tr><td colspan=\"2\" style=\"text-align:center;vertical-align:top;border-bottom:2px dashed #ff0000\">head</td></tr>"
            + "<tr><td>a</td><td>b</td></tr></table>"))!;

        Assert.Equal([new ExchangeMerge(0, 0, 1, 2)], grid.Merges);
        var head = grid.At(0, 0);
        Assert.Equal(ExchangeHorizontalAlignment.Center, head.Horizontal);
        Assert.Equal(ExchangeVerticalAlignment.Top, head.Vertical);
        Assert.Equal(ExchangeLineStyle.Dashed, head.Borders!.Bottom!.Style);
        Assert.Equal("#FF0000", head.Borders.Bottom.ColorHex, ignoreCase: true);
    }

    [Fact]
    public void The_caption_becomes_a_first_row_spanning_the_table()
    {
        var grid = Grids("en-US").Grid(Html("<table><caption>Title</caption><tr><td>a</td><td>b</td></tr></table>"))!;

        Assert.Equal(2, grid.RowCount);
        Assert.Equal("Title", grid.At(0, 0).Text);
        Assert.Equal([new ExchangeMerge(0, 0, 1, 2)], grid.Merges);
        Assert.Equal("b", grid.At(1, 1).Text);
    }
}
