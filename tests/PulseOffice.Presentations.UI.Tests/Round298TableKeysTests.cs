using Avalonia.Input;
using PulseOffice.Document.Embedding;
using PulseOffice.Document.Tables;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class Round298TableKeysTests
{
    private readonly IDeckTableKeys _keys = new DeckTableKeys(new TableStructure(new TableSpans(), new ColumnWidths()));

    private static DataTableObject Table() => new()
    {
        Id = "t",
        Query = new DataQuerySource { ConnectionId = string.Empty, Sql = string.Empty },
        Snapshot = new DataSnapshot
        {
            Columns = [new DataColumn("a", DataValueKind.Text), new DataColumn("b", DataValueKind.Text), new DataColumn("c", DataValueKind.Text)],
            Rows = [["1", "2", "3"], ["4", "5", "6"]],
        },
        Style = new DataTableStyle { HeaderRow = false },
        Merges = [new TableMerge(0, 0, 2, 2)],
    };

    private static TableCellRange Cell(int row, int column) => new(row, column, row, column);

    [Fact]
    public void Arrows_and_tab_step_over_merged_cells()
    {
        var table = Table();

        Assert.Equal(new TableKeyMove(Cell(0, 2)), _keys.Read(table, Cell(0, 0), Key.Right, KeyModifiers.None));
        Assert.Equal(new TableKeyMove(Cell(0, 0)), _keys.Read(table, Cell(1, 2), Key.Left, KeyModifiers.None));
        Assert.Equal(new TableKeyMove(Cell(0, 2)), _keys.Read(table, Cell(0, 0), Key.Tab, KeyModifiers.None));
        Assert.Equal(new TableKeyMove(Cell(1, 2)), _keys.Read(table, Cell(0, 2), Key.Tab, KeyModifiers.None));
        Assert.Equal(new TableKeyMove(Cell(0, 2)), _keys.Read(table, Cell(1, 2), Key.Tab, KeyModifiers.Shift));
        Assert.Equal(new TableKeyMove(Cell(0, 0)), _keys.Read(table, Cell(0, 0), Key.Down, KeyModifiers.None));
    }

    [Fact]
    public void Typing_replaces_then_appends_and_backspace_erases()
    {
        var table = Table();

        var typed = _keys.Type(table, Cell(1, 2), "x", replace: true);
        typed = _keys.Type(typed, Cell(1, 2), "y", replace: false);
        Assert.Equal("xy", typed.Snapshot.Rows[1][2]);
        Assert.Equal("x", _keys.Erase(typed, Cell(1, 2)).Snapshot.Rows[1][2]);
        Assert.Equal("z", _keys.Type(table, Cell(1, 1), "z", replace: true).Snapshot.Rows[0][0]);
    }
}
