using Avalonia.Input;
using PulseOffice.Document.Embedding;
using PulseOffice.Document.Tables;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class Round297TableKeysTests
{
    private readonly IDeckTableKeys _keys = new DeckTableKeys(new TableStructure(new TableSpans(), new ColumnWidths()));

    private static DataTableObject Table() => new()
    {
        Id = "t",
        Query = new DataQuerySource { ConnectionId = string.Empty, Sql = string.Empty },
        Snapshot = new DataSnapshot { Columns = [new DataColumn("a", DataValueKind.Text), new DataColumn("b", DataValueKind.Text)], Rows = [["1", "2"]] },
        Style = new DataTableStyle { HeaderRow = true },
    };

    private static TableCellRange Cell(int row, int column) => new(row, column, row, column);

    [Fact]
    public void Tab_walks_the_cells_and_adds_a_row_after_the_last_one()
    {
        var table = Table();

        Assert.Equal(new TableKeyMove(Cell(TableCellFormat.HeaderRow, 1)), _keys.Read(table, Cell(TableCellFormat.HeaderRow, 0), Key.Tab, KeyModifiers.None));
        Assert.Equal(new TableKeyMove(Cell(0, 0)), _keys.Read(table, Cell(TableCellFormat.HeaderRow, 1), Key.Tab, KeyModifiers.None));
        Assert.Equal(new TableKeyMove(Cell(TableCellFormat.HeaderRow, 1)), _keys.Read(table, Cell(0, 0), Key.Tab, KeyModifiers.Shift));
        var append = Assert.IsType<TableKeyAppendRow>(_keys.Read(table, Cell(0, 1), Key.Tab, KeyModifiers.None));
        Assert.Equal(Cell(1, 0), append.Range);
        Assert.Equal(2, _keys.Apply(table, append).Snapshot.Rows.Count);
    }

    [Fact]
    public void Arrows_stay_inside_and_shift_extends()
    {
        var table = Table();

        Assert.Equal(new TableKeyMove(Cell(0, 0)), _keys.Read(table, Cell(0, 0), Key.Down, KeyModifiers.None));
        Assert.Equal(new TableKeyMove(new TableCellRange(0, 0, 0, 1)), _keys.Read(table, Cell(0, 0), Key.Right, KeyModifiers.Shift));
    }

    [Fact]
    public void Delete_clears_the_selected_cells_instead_of_the_table()
    {
        var table = Table();

        var clear = _keys.Read(table, new TableCellRange(TableCellFormat.HeaderRow, 0, 0, 0), Key.Delete, KeyModifiers.None)!;
        var cleared = _keys.Apply(table, clear);

        Assert.Equal(string.Empty, cleared.Snapshot.Columns[0].Name);
        Assert.Equal(string.Empty, cleared.Snapshot.Rows[0][0]);
        Assert.Equal("2", cleared.Snapshot.Rows[0][1]);
    }
}
