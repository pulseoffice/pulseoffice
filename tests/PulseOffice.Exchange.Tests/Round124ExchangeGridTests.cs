using PulseOffice.Document.Embedding;
using PulseOffice.Exchange;
using Xunit;

namespace PulseOffice.Exchange.Tests;

public sealed class Round124ExchangeGridTests
{
    private readonly CellGridConversions _conversions = new();

    private readonly ExchangeContentGuard _guard = new();

    [Fact]
    public void Html_writes_merged_cells_as_spans_and_skips_the_covered_cells()
    {
        var html = _conversions.Html(Merged());

        Assert.Contains("colspan=\"2\"", html, StringComparison.Ordinal);
        Assert.Contains("rowspan=\"2\"", html, StringComparison.Ordinal);
        Assert.Equal(4, Count(html, "<td"));
    }

    [Fact]
    public void Html_writes_borders_padding_alignment_and_font()
    {
        var cell = new ExchangeCell
        {
            Kind = ExchangeCellKind.Text,
            Text = "x",
            Borders = new ExchangeBorders { Top = new ExchangeBorderLine(1.5, "#FF0000") { Style = ExchangeLineStyle.Dashed }, Left = new ExchangeBorderLine(0.75, "#000000") },
            Horizontal = ExchangeHorizontalAlignment.Center,
            Vertical = ExchangeVerticalAlignment.Middle,
            Strikethrough = true,
            FontFamily = "Georgia",
            FontSizePoints = 14,
        };

        var html = _conversions.Html(Single(cell));

        Assert.Contains("border-top:1.5pt dashed #FF0000", html, StringComparison.Ordinal);
        Assert.Contains("border-left:0.75pt solid #000000", html, StringComparison.Ordinal);
        Assert.Contains("padding:", html, StringComparison.Ordinal);
        Assert.Contains("text-align:center", html, StringComparison.Ordinal);
        Assert.Contains("vertical-align:middle", html, StringComparison.Ordinal);
        Assert.Contains("line-through", html, StringComparison.Ordinal);
        Assert.Contains("font-family:'Georgia'", html, StringComparison.Ordinal);
        Assert.Contains("font-size:14pt", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Html_does_not_let_a_font_family_break_out_of_the_style_attribute()
    {
        var html = _conversions.Html(Single(new ExchangeCell { Kind = ExchangeCellKind.Text, Text = "x", FontFamily = "A\"><script>'" }));

        Assert.DoesNotContain("<script>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Table_carries_merges_with_the_header_row_convention()
    {
        var table = _conversions.Table(Merged(), "t");

        Assert.Contains(new TableMerge(TableCellFormat.HeaderRow, 0, 1, 2), table.Merges);
        Assert.Contains(new TableMerge(0, 0, 2, 1), table.Merges);
    }

    [Fact]
    public void Table_carries_cell_looks_when_the_range_is_formatted()
    {
        var cell = new ExchangeCell
        {
            Kind = ExchangeCellKind.Text,
            Text = "x",
            Bold = true,
            BackgroundHex = "#FFFF00",
            ColorHex = "#112233",
            FontSizePoints = 9,
            Horizontal = ExchangeHorizontalAlignment.Center,
            Vertical = ExchangeVerticalAlignment.Top,
            Borders = new ExchangeBorders { Bottom = new ExchangeBorderLine(1, "#00FF00") },
        };

        var table = _conversions.Table(Single(cell), "t");

        Assert.True(table.Style.OwnLook);
        var format = Assert.Single(table.CellFormats, entry => entry.Row == TableCellFormat.HeaderRow && entry.Column == 0);
        Assert.Equal(CellVerticalAlign.Top, format.VerticalAlign);
        Assert.Equal("#FFFF00", format.Look!.FillHex);
        Assert.Equal("#112233", format.Look.TextHex);
        Assert.True(format.Look.Bold);
        Assert.Equal(9, format.Look.FontSize);
        Assert.Equal(CellHorizontalAlign.Center, format.Look.Horizontal);
        Assert.Equal(new TableBorderLine("#00FF00", 1), format.Look.Bottom);
    }

    [Fact]
    public void An_unformatted_range_keeps_the_plain_table_style()
    {
        var table = _conversions.Table(Single(new ExchangeCell { Kind = ExchangeCellKind.Text, Text = "x" }), "t");

        Assert.False(table.Style.OwnLook);
        Assert.Empty(table.CellFormats);
    }

    [Fact]
    public void The_native_json_keeps_merges_borders_fonts_and_alignment()
    {
        var serializer = new JsonExchangeSerializer(new LoadedExchangeTypeCatalog(), _guard);
        var grid = Merged() with
        {
            Rows =
            [
                [new ExchangeCell { Kind = ExchangeCellKind.Text, Text = "a", Strikethrough = true, FontFamily = "Georgia", FontSizePoints = 13, Wrap = true, Horizontal = ExchangeHorizontalAlignment.Justify, Vertical = ExchangeVerticalAlignment.Top, Borders = new ExchangeBorders { Right = new ExchangeBorderLine(2, "#123456") { Style = ExchangeLineStyle.Double } } }, ExchangeCell.Empty],
                [Text("b"), Text("c")],
                [ExchangeCell.Empty, Text("d")],
            ],
        };

        var read = serializer.Read(serializer.Write(new ExchangePackage { Id = "p", Content = grid }));

        var cells = Assert.IsType<CellRangeContent>(read!.Content);
        Assert.Equal(grid.Merges, cells.Merges);
        Assert.Equal(grid.Rows[0][0], cells.Rows[0][0]);
    }

    [Theory]
    [InlineData(0, 0, 1, 3)]
    [InlineData(2, 0, 2, 1)]
    [InlineData(-1, 0, 1, 1)]
    [InlineData(0, 0, 0, 1)]
    [InlineData(0, 0, int.MaxValue, 1)]
    public void The_guard_refuses_merges_outside_the_range(int row, int column, int rows, int columns)
    {
        var grid = Merged() with { Merges = [new ExchangeMerge(row, column, rows, columns)] };

        Assert.Null(_guard.Admit(grid));
    }

    [Fact]
    public void The_guard_refuses_overlapping_merges()
    {
        var grid = Merged() with { Merges = [new ExchangeMerge(0, 0, 2, 2), new ExchangeMerge(1, 1, 2, 1)] };

        Assert.Null(_guard.Admit(grid));
    }

    [Fact]
    public void The_guard_cleans_hostile_borders_and_fonts()
    {
        var cell = new ExchangeCell
        {
            Kind = ExchangeCellKind.Text,
            Text = "x",
            FontSizePoints = double.NaN,
            FontFamily = new string('f', 5000),
            Borders = new ExchangeBorders { Top = new ExchangeBorderLine(double.PositiveInfinity, "red"), Left = new ExchangeBorderLine(1e9, "#000000") },
        };

        var admitted = Assert.IsType<CellRangeContent>(_guard.Admit(Single(cell)));

        var cleaned = admitted.Rows[0][0];
        Assert.Null(cleaned.FontSizePoints);
        Assert.Null(cleaned.FontFamily);
        Assert.Null(cleaned.Borders!.Top);
        Assert.Equal(ExchangeContentGuard.MaximumBorderPoints, cleaned.Borders.Left!.WidthPoints);
    }

    [Fact]
    public void A_table_object_turns_back_into_a_grid_with_its_merges_looks_and_display_text()
    {
        var source = Merged() with
        {
            Rows =
            [
                [new ExchangeCell { Kind = ExchangeCellKind.Text, Text = "head", Bold = true, BackgroundHex = "#FFFF00", Borders = new ExchangeBorders { Bottom = new ExchangeBorderLine(1, "#000000") } }, ExchangeCell.Empty],
                [Text("tall"), new ExchangeCell { Kind = ExchangeCellKind.Number, Number = 1.5, Text = "1.5" }],
                [ExchangeCell.Empty, new ExchangeCell { Kind = ExchangeCellKind.Number, Number = 2, Text = "2" }],
            ],
        };
        var table = _conversions.Table(source, "t");

        var grid = _conversions.Grid(table, new System.Globalization.CultureInfo("it-IT"));

        Assert.Equal(source.Merges, grid.Merges);
        Assert.Equal("head", grid.At(0, 0).Text);
        Assert.True(grid.At(0, 0).Bold);
        Assert.Equal("#FFFF00", grid.At(0, 0).BackgroundHex);
        Assert.Equal(new ExchangeBorderLine(1, "#000000"), grid.At(0, 0).Borders!.Bottom);
        Assert.Equal("1,5", grid.At(1, 1).Text);
        Assert.Equal(ExchangeCellKind.Number, grid.At(1, 1).Kind);
        Assert.Equal("head\t\ntall\t1,5\n\t2\n", _conversions.Tabular(grid));
    }

    private static CellRangeContent Merged() => new()
    {
        SheetName = "S",
        RowCount = 3,
        ColumnCount = 2,
        Rows =
        [
            [Text("head"), ExchangeCell.Empty],
            [Text("tall"), Text("b")],
            [ExchangeCell.Empty, Text("c")],
        ],
        Merges = [new ExchangeMerge(0, 0, 1, 2), new ExchangeMerge(1, 0, 2, 1)],
    };

    private static CellRangeContent Single(ExchangeCell cell) => new()
    {
        SheetName = "S",
        RowCount = 1,
        ColumnCount = 1,
        Rows = [[cell]],
    };

    private static ExchangeCell Text(string text) => new() { Kind = ExchangeCellKind.Text, Text = text };

    private static int Count(string text, string part)
    {
        var count = 0;
        for (var index = text.IndexOf(part, StringComparison.Ordinal); index >= 0; index = text.IndexOf(part, index + 1, StringComparison.Ordinal)) count++;
        return count;
    }
}
