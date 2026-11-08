using PulseOffice.Document.Tables;
using PulseOffice.Document.Embedding;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class TableSizingTests
{
    private static DataTableObject Table(TableLayout? layout = null) => new()
    {
        Id = "t",
        Query = new DataQuerySource { ConnectionId = "", Sql = "" },
        Snapshot = new DataSnapshot
        {
            Columns = [new("Nome", DataValueKind.Text), new("Descrizione lunga", DataValueKind.Text)],
            Rows = [["Dado", "un pezzo esagonale filettato"]],
        },
        Layout = layout ?? TableLayout.Automatic,
    };

    [Fact]
    public void LaLarghezzaOttimaleSegueIlTestoPiuLungo()
    {
        var layout = new OptimalColumnSizing(new ColumnWidths()).Apply(Table());
        Assert.Equal(1, layout.ColumnFractions.Sum(), 6);
        Assert.True(layout.ColumnFractions[1] > layout.ColumnFractions[0] * 4);
    }

    [Fact]
    public void LaLarghezzaMinimaSegueLaParolaPiuLunga()
    {
        var minimal = new MinimalColumnSizing(new ColumnWidths()).Apply(Table());
        var optimal = new OptimalColumnSizing(new ColumnWidths()).Apply(Table());
        Assert.True(minimal.ColumnFractions[1] < optimal.ColumnFractions[1]);
    }

    [Fact]
    public void DistribuireRendeLeColonneUguali() =>
        Assert.All(new DistributedColumnSizing().Apply(Table()).ColumnFractions, f => Assert.Equal(0.5, f, 6));

    [Fact]
    public void DistribuireLeRigheNePareggiaLaMedia()
    {
        var layout = new DistributedRowSizing().Apply(Table(new TableLayout { RowHeights = [10, 30] }));
        Assert.Equal([20.0, 20.0], layout.RowHeights);
    }

    [Fact]
    public void LAltezzaMinimaEPiuBassaDellOttimale()
    {
        var minimal = new MinimalRowSizing().Apply(Table());
        var optimal = new OptimalRowSizing().Apply(Table());
        Assert.Equal(2, minimal.RowHeights.Count);
        Assert.True(minimal.RowHeights[0] < optimal.RowHeights[0]);
    }

    [Fact]
    public void LAreaDiUnaTabellaSenzaColonneNonFaCadereNulla()
    {
        var empty = Table() with { Snapshot = DataSnapshot.Empty };

        Assert.Equal((0d, 0d, 0d, 0d), new TableGeometry().Area(empty, new TableCellRange(0, 0, 0, 0)));
    }
}
