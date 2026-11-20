using PulseOffice.Document.Embedding;
using PulseOffice.Document.Tables;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class Round302TableCellWriteTests
{
    private readonly IDeckTableKeys _keys = new DeckTableKeys(new TableStructure(new TableSpans(), new ColumnWidths()));

    private static DataTableObject Table() => new()
    {
        Id = "t",
        Query = new DataQuerySource { ConnectionId = string.Empty, Sql = string.Empty },
        Snapshot = new DataSnapshot { Columns = [new DataColumn("a", DataValueKind.Text), new DataColumn("b", DataValueKind.Text)], Rows = [["1", "2"], ["3", "4"]] },
        Merges = [new TableMerge(0, 0, 2, 1)],
        CellTexts = [new TableCellText(0, 1, [new TableCellParagraph([new TableTextRun("2") { Bold = true }])])],
    };

    private static TableCellRange Cell(int row, int column) => new(row, column, row, column);

    [Fact]
    public void An_edit_session_writes_the_whole_text_of_the_anchor_cell_once()
    {
        var table = Table();

        Assert.Equal("1", _keys.Text(table, Cell(1, 0)));
        var written = _keys.Write(table, Cell(1, 0), "one\r\ntwo");

        Assert.Equal("one\ntwo", written.Snapshot.Rows[0][0]);
        Assert.Equal("3", written.Snapshot.Rows[1][0]);
    }

    [Fact]
    public void Writing_the_same_text_changes_nothing_and_new_text_keeps_the_runs()
    {
        var table = Table();

        Assert.Same(table, _keys.Write(table, Cell(0, 1), "2"));
        Assert.Equal("22", Assert.Single(_keys.Write(table, Cell(0, 1), "22").CellTexts).PlainText);
        Assert.Equal("H", _keys.Write(table, Cell(TableCellFormat.HeaderRow, 1), "H").Snapshot.Columns[1].Name);
    }
}
