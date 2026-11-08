using PulseOffice.Foundation.Units;
using PulseOffice.Sheets.Addressing;
using Xunit;

namespace PulseOffice.Sheets.Tests;

public class SheetLayoutRangeTests
{
    [Fact]
    public void MostrareTutteLeRigheToccaSoloLeNascoste()
    {
        var layout = new SheetLayout();
        layout.SetRowHeight(4, Twip.FromPoints(30));
        layout.HideRow(4);
        layout.HideRow(900_000);

        layout.ShowRows(0, CellAddress.MaxRows - 1);

        Assert.False(layout.IsRowHidden(4));
        Assert.Equal(Twip.FromPoints(30), layout.RowHeight(4));
        Assert.False(layout.IsRowHidden(900_000));
    }

    [Fact]
    public void AltezzaSuTutteLeRigheDiventaIlPredefinitoManuale()
    {
        var layout = new SheetLayout();
        layout.SetRowHeight(2, Twip.FromPoints(40));
        layout.HideRow(7);

        layout.SetRowHeights(0, CellAddress.MaxRows - 1, Twip.FromPoints(25));

        Assert.Equal(Twip.FromPoints(25), layout.RowHeight(2));
        Assert.Equal(Twip.FromPoints(25), layout.RowHeight(500_000));
        Assert.True(layout.DefaultRowHeightManual);
        Assert.True(layout.IsRowHidden(7));
        layout.ShowRow(7);
        Assert.Equal(Twip.FromPoints(25), layout.RowHeight(7));
        Assert.Empty(layout.RowHeights);
    }

    [Fact]
    public void LarghezzaSuTutteLeColonneDiventaIlPredefinito()
    {
        var layout = new SheetLayout();
        layout.SetColumnWidth(3, Twip.FromPoints(100));
        layout.HideColumn(5);

        layout.SetColumnWidths(0, CellAddress.MaxColumns - 1, Twip.FromPoints(80));

        Assert.Equal(Twip.FromPoints(80), layout.ColumnWidth(3));
        Assert.True(layout.IsColumnHidden(5));
        layout.ShowColumns(0, CellAddress.MaxColumns - 1);
        Assert.Equal(Twip.FromPoints(80), layout.ColumnWidth(5));
    }

    [Fact]
    public void UnIntervalloParzialeImpostaRigaPerRiga()
    {
        var layout = new SheetLayout();

        layout.SetRowHeights(1, 3, Twip.FromPoints(20));

        Assert.Equal(3, layout.RowHeights.Count);
        Assert.False(layout.DefaultRowHeightManual);
    }
}
