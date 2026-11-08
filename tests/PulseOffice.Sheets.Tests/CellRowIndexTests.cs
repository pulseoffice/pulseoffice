using PulseOffice.Sheets.Addressing;
using Xunit;

namespace PulseOffice.Sheets.Tests;

public sealed class CellRowIndexTests
{
    [Fact]
    public void LeRigheEsconoInOrdineConLeColonneOrdinateESenzaDoppioni()
    {
        var index = new CellRowIndex([new(5, 3), new(0, 2), new(5, 1), new(5, 3), new(0, 0)]);

        Assert.Equal([0, 5], index.Rows);
        Assert.Equal([0, 2], index.Columns(0));
        Assert.Equal([1, 3], index.Columns(5));
        Assert.Null(index.Columns(3));
        Assert.Equal(5, index.LastRow);
        Assert.Equal(4, index.ColumnCount);
    }

    [Fact]
    public void UnIndiceVuotoNonHaRighe()
    {
        var index = new CellRowIndex([]);

        Assert.Empty(index.Rows);
        Assert.Equal(-1, index.LastRow);
        Assert.Equal(0, index.ColumnCount);
    }

    [Fact]
    public void DopoLaPrimaLetturaNonSiAggiungePiu()
    {
        var index = new CellRowIndex([new(1, 1)]);
        _ = index.Rows.ToList();

        Assert.Throws<InvalidOperationException>(() => index.Add(new CellAddress(2, 2)));
    }
}
