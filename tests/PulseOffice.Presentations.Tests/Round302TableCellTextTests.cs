using PulseOffice.Document.Embedding;
using PulseOffice.Document.Tables;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class Round302TableCellTextTests
{
    private readonly ITableStructure _structure = new TableStructure(new TableSpans(), new ColumnWidths());

    private static DataTableObject Table() => new()
    {
        Id = "t",
        Query = new DataQuerySource { ConnectionId = string.Empty, Sql = string.Empty },
        Snapshot = new DataSnapshot { Columns = [new DataColumn("a", DataValueKind.Text), new DataColumn("b", DataValueKind.Text)], Rows = [["x", "y"], ["z", "w"]] },
        CellTexts = [new TableCellText(1, 1, [new TableCellParagraph([new TableTextRun("w") { Bold = true }])])],
    };

    [Fact]
    public void Runs_follow_their_cell_when_rows_and_columns_are_inserted_and_deleted()
    {
        var moved = _structure.InsertColumn(_structure.InsertRow(Table(), 0), 0, new DataColumn("n", DataValueKind.Text));
        var text = Assert.Single(moved.CellTexts);
        Assert.Equal((2, 2), (text.Row, text.Column));
        Assert.NotNull(new TableCellIndex(moved).Text(2, 2, "w"));

        Assert.Empty(_structure.DeleteRows(Table(), 1, 1).CellTexts);
        Assert.Empty(_structure.DeleteColumns(Table(), 1, 1).CellTexts);
    }

    [Fact]
    public void Runs_whose_text_no_longer_matches_the_value_are_ignored()
    {
        Assert.Null(new TableCellIndex(Table()).Text(1, 1, "changed"));
    }

    [Fact]
    public void A_run_overrides_only_what_it_sets()
    {
        var own = new TableTextRun("x") { Bold = true }.Over(new TableCellLook { Italic = true, FontSize = 12 });

        Assert.True(own.Bold);
        Assert.True(own.Italic);
        Assert.Equal(12, own.FontSize);
    }
}
