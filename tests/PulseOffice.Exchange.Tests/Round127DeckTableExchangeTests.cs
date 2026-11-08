using PulseOffice.Document.Embedding;
using PulseOffice.Exchange;
using Xunit;

namespace PulseOffice.Exchange.Tests;

public sealed class Round127DeckTableExchangeTests
{
    private readonly CellGridConversions _conversions = new();

    [Fact]
    public void Italic_underline_strike_and_font_family_reach_the_deck_table()
    {
        var cell = new ExchangeCell { Kind = ExchangeCellKind.Text, Text = "x", Italic = true, Underline = true, Strikethrough = true, FontFamily = "Georgia" };
        var grid = new CellRangeContent { SheetName = "S", RowCount = 1, ColumnCount = 1, Rows = [[cell]] };

        var table = _conversions.Table(grid, "t");

        Assert.True(table.Style.OwnLook);
        var look = table.CellFormats.Single().Look!;
        Assert.True(look.Italic);
        Assert.True(look.Underline);
        Assert.True(look.Strikethrough);
        Assert.Equal("Georgia", look.FontFamily);
    }

    [Fact]
    public void Italic_underline_strike_and_font_family_leave_the_deck_table()
    {
        var table = new DataTableObject
        {
            Id = "t",
            Query = new DataQuerySource { ConnectionId = string.Empty, Sql = string.Empty },
            Snapshot = new DataSnapshot { Columns = [new DataColumn("A", DataValueKind.Text)], Rows = [["x"]] },
            Style = new DataTableStyle { HeaderRow = true, OwnLook = true },
            CellFormats = [new TableCellFormat(0, 0, CellVerticalAlign.Top) { Look = new TableCellLook { Italic = true, Underline = true, Strikethrough = true, FontFamily = "Georgia" } }],
        };

        var cell = _conversions.Grid(table, System.Globalization.CultureInfo.InvariantCulture).At(1, 0);

        Assert.True(cell.Italic);
        Assert.True(cell.Underline);
        Assert.True(cell.Strikethrough);
        Assert.Equal("Georgia", cell.FontFamily);
    }
}
