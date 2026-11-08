using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using Xunit;

namespace PulseOffice.Sheets.Tests;

public class SheetLayoutRemapTests
{
    [Fact]
    public void LeMisureAutomaticheSeguonoLeRighe()
    {
        var layout = new SheetLayout();
        layout.SetAutomaticRowHeight(3, Twip.FromPoints(30));
        layout.SetAutomaticRowHeight(4, Twip.FromPoints(35));

        layout.DeleteRows(4, 1);
        layout.InsertRows(3, 2);

        Assert.Equal(30, layout.RowHeight(5).ToPoints(), 1);
        Assert.Equal(layout.DefaultRowHeight, layout.RowHeight(3));
        Assert.Single(layout.AutomaticRowHeights);
    }

    [Fact]
    public void UnaRigaNascostaSpostataRitrovaLaSuaMisura()
    {
        var layout = new SheetLayout();
        layout.SetAutomaticRowHeight(2, Twip.FromPoints(30));
        layout.SetRowHeight(4, Twip.FromPoints(40));
        layout.HideRow(4);

        layout.InsertRows(3, 2);
        layout.ShowRow(6);

        Assert.Equal(40, layout.RowHeight(6).ToPoints(), 1);
        Assert.Equal(30, layout.RowHeight(2).ToPoints(), 1);
    }
}
