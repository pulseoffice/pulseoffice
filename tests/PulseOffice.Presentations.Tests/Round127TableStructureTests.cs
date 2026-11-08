using PulseOffice.Document.Embedding;
using PulseOffice.Document.Tables;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class Round127TableStructureTests
{
    private readonly ITableStructure _structure = new TableStructure(new TableSpans(), new ColumnWidths());

    private readonly TableCellLook _header = new() { FillHex = "#112233", Bold = true };

    private readonly TableCellLook _body = new() { FillHex = "#EEEEEE", Italic = true, Top = new TableBorderLine("#000000", 1) };

    private DataTableObject Table()
    {
        var formats = new List<TableCellFormat>();
        for (var row = TableCellFormat.HeaderRow; row < 2; row++)
            for (var column = 0; column < 2; column++)
                formats.Add(new TableCellFormat(row, column, CellVerticalAlign.Center) { Look = row < 0 ? _header : _body });
        return new DataTableObject
        {
            Id = "t",
            Query = new DataQuerySource { ConnectionId = string.Empty, Sql = string.Empty },
            Snapshot = new DataSnapshot
            {
                Columns = [new DataColumn("A", DataValueKind.Text), new DataColumn("B", DataValueKind.Text)],
                Rows = [new object?[] { "1", "2" }, new object?[] { "3", "4" }],
            },
            Style = new DataTableStyle { HeaderRow = true, OwnLook = true },
            CellFormats = formats,
        };
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void An_inserted_row_takes_the_look_of_its_body_neighbour(int at)
    {
        var after = new TableCellIndex(_structure.InsertRow(Table(), at));

        Assert.Equal(_body, after.Format(at, 0)?.Look);
        Assert.Equal(_body, after.Format(at, 1)?.Look);
        Assert.Equal(CellVerticalAlign.Center, after.Format(at, 1)?.VerticalAlign);
        Assert.Equal(_header, after.Format(TableCellFormat.HeaderRow, 0)?.Look);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void An_inserted_column_takes_the_look_of_its_neighbour(int at)
    {
        var after = new TableCellIndex(_structure.InsertColumn(Table(), at, new DataColumn("N", DataValueKind.Text)));

        Assert.Equal(_header, after.Format(TableCellFormat.HeaderRow, at)?.Look);
        Assert.Equal(_body, after.Format(0, at)?.Look);
        Assert.Equal(_body, after.Format(1, at)?.Look);
    }

    [Fact]
    public void Hit_testing_without_saved_widths_uses_the_widths_the_files_are_written_with()
    {
        var table = Table() with
        {
            Snapshot = new DataSnapshot
            {
                Columns = [new DataColumn("A much longer heading", DataValueKind.Text), new DataColumn("B", DataValueKind.Text)],
                Rows = [new object?[] { "1", "2" }],
            },
        };
        var fractions = new OptimalColumnSizing(new ColumnWidths()).Apply(table).ColumnFractions;

        var geometry = new TableGeometry();

        Assert.Equal(0, geometry.CellAt(table, fractions[0] - 0.01, 1)?.Column);
        Assert.Equal(fractions[0], geometry.Area(table, new TableCellRange(0, 1, 0, 1)).X, 6);
    }
}
