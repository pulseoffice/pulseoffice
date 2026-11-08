using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using Xunit;

namespace PulseOffice.Sheets.Tests;

public class SheetLayoutHideTests
{
    [Fact]
    public void NascondereEMostrareRestituisceLaMisuraImpostataAMano()
    {
        var layout = new SheetLayout();
        layout.SetRowHeight(3, Twip.FromCentimeters(2));

        layout.HideRow(3);
        Assert.True(layout.IsRowHidden(3));

        layout.ShowRow(3);
        Assert.Equal(2, layout.RowHeight(3).ToCentimeters(), 3);
    }

    [Fact]
    public void UnaRigaSenzaMisuraTornaAQuellaPredefinita()
    {
        var layout = new SheetLayout();

        layout.HideRow(5);
        layout.ShowRow(5);

        Assert.Equal(layout.DefaultRowHeight, layout.RowHeight(5));
    }

    [Fact]
    public void NascondereDueVolteNonPerdeLaMisura()
    {
        var layout = new SheetLayout();
        layout.SetColumnWidth(1, Twip.FromCentimeters(4));

        layout.HideColumn(1);
        layout.HideColumn(1);
        layout.ShowColumn(1);

        Assert.Equal(4, layout.ColumnWidth(1).ToCentimeters(), 3);
    }
}
