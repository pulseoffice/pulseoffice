using PulseOffice.Document.Tables;
using PulseOffice.Document.Embedding;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public class TableStructureTests
{
    private readonly ITableStructure _structure = new TableStructure(new TableSpans(), new ColumnWidths());

    private static DataTableObject Table() => new()
    {
        Id = "t",
        Query = new DataQuerySource { ConnectionId = string.Empty, Sql = string.Empty },
        Snapshot = new DataSnapshot
        {
            Columns = [new DataColumn("A", DataValueKind.Text), new DataColumn("B", DataValueKind.Text), new DataColumn("C", DataValueKind.Text)],
            Rows = [new object?[] { "1", "2", "3" }, new object?[] { "4", "5", "6" }, new object?[] { "7", "8", "9" }],
        },
        Merges = [new TableMerge(1, 0, 2, 1)],
        Layout = new TableLayout { ColumnFractions = [0.2, 0.3, 0.5], RowHeights = [10, 20, 30, 40] },
    };

    [Fact]
    public void EliminareLaPrimaRigaSpostaUnioniEAltezze()
    {
        var after = _structure.DeleteRows(Table(), 0, 0);

        Assert.Equal(new TableMerge(0, 0, 2, 1), Assert.Single(after.Merges));
        Assert.Equal([10, 30, 40], after.Layout.RowHeights);
    }

    [Fact]
    public void AggiungereUnaColonnaConservaLeLarghezze()
    {
        var after = _structure.InsertColumn(Table(), 1, new DataColumn("N", DataValueKind.Text));

        Assert.Equal(4, after.Layout.ColumnFractions.Count);
        Assert.Equal(1, after.Layout.ColumnFractions.Sum(), 6);
    }

    [Fact]
    public void UnUnioneRidottaAUnaCellaSparisce()
    {
        Assert.Empty(_structure.DeleteRows(Table(), 2, 2).Merges);
    }
}
