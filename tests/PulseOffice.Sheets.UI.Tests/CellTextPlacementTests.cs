using PulseOffice.Sheets.Styling;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public class CellTextPlacementTests
{
    private static readonly CellBox Cell = new(0, 0, 40, 100);

    [Fact]
    public void LAllineamentoVerticaleSiRispetta()
    {
        var placement = new CellTextPlacement();

        Assert.Equal(1, placement.Top(Cell, 10, CellVerticalAlignment.Top, 1));
        Assert.Equal(45, placement.Top(Cell, 10, CellVerticalAlignment.Middle, 1));
        Assert.Equal(89, placement.Top(Cell, 10, CellVerticalAlignment.Bottom, 1));
    }

    [Fact]
    public void IlTestoRuotatoDiNovantaGradiSiPosaInBassoASinistra()
    {
        var (x, y) = new CellTextPlacement().RotatedCentre(
            Cell, 60, 10, 90, alignRight: false, centred: false, CellVerticalAlignment.Bottom, 3);

        Assert.Equal(8, x, 6);
        Assert.Equal(69, y, 6);
    }
}
