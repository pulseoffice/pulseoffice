using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class HandoutLayoutTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(4, 4)]
    [InlineData(5, 4)]
    [InlineData(7, 6)]
    [InlineData(20, 9)]
    public void LaSceltaRicadeSuUnaDisposizioneVera(int asked, int expected) =>
        Assert.Equal(expected, new HandoutLayout().Nearest(asked));

    [Fact]
    public void LeCelleStannoNellaPaginaESonoInOrdineDiLettura()
    {
        var cells = new HandoutLayout().Cells(6);

        Assert.Equal(6, cells.Count);
        Assert.All(cells, cell => Assert.True(cell.X >= 0 && cell.Y >= 0 && cell.X + cell.Width <= 1 && cell.Y + cell.Height <= 1));
        Assert.True(cells[1].X > cells[0].X);
        Assert.True(cells[2].Y > cells[0].Y);
    }
}
