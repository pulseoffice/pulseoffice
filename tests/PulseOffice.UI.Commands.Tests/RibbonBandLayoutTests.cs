using PulseOffice.UI.Commands;
using Xunit;

namespace PulseOffice.UI.Commands.Tests;

public sealed class RibbonBandLayoutTests
{
    private readonly RibbonBandLayout _layout = new();

    [Fact]
    public void LeTessereRiempionoLAltezzaConLaProporzioneDellaDiapositiva()
    {
        var metrics = _layout.Measure(90, new RibbonBandSpec(1, 4, 16.0 / 9.0), 21, 2);

        Assert.Equal(90, metrics.TileHeight, 3);
        Assert.Equal(160, metrics.TileWidth, 3);
        Assert.Equal(6, metrics.TotalRows);
        Assert.Equal(4 * 160 + 3 * 2, metrics.Width, 3);
    }

    [Fact]
    public void PiuRigheDividonoLAltezzaTogliendoGliSpazi()
    {
        var metrics = _layout.Measure(82, new RibbonBandSpec(2, 3, 2), 10, 2);

        Assert.Equal(40, metrics.TileHeight, 3);
        Assert.Equal(80, metrics.TileWidth, 3);
        Assert.Equal(2, metrics.VisibleRows);
        Assert.Equal(2, metrics.LastFirstRow);
    }

    [Fact]
    public void LoScorrimentoRestaDentroLeRighe()
    {
        var metrics = _layout.Measure(0, new RibbonBandSpec(1, 4, 1), 9, 0);

        Assert.Equal(0, _layout.Clamp(-3, metrics));
        Assert.Equal(2, _layout.Clamp(10, metrics));
    }

    [Fact]
    public void LaSceltaCorrenteVieneResaVisibile()
    {
        var metrics = _layout.Measure(0, new RibbonBandSpec(1, 4, 1), 21, 0);

        Assert.Equal(3, _layout.Reveal(13, 0, metrics));
        Assert.Equal(1, _layout.Reveal(5, 4, metrics));
        Assert.Equal(2, _layout.Reveal(9, 2, metrics));
        Assert.Equal(2, _layout.Reveal(-1, 2, metrics));
    }

    [Fact]
    public void SiVedonoSoloLeTessereDelleRigheVisibili()
    {
        var metrics = _layout.Measure(0, new RibbonBandSpec(1, 4, 1), 21, 0);

        Assert.False(_layout.Shows(3, 1, metrics));
        Assert.True(_layout.Shows(4, 1, metrics));
        Assert.True(_layout.Shows(7, 1, metrics));
        Assert.False(_layout.Shows(8, 1, metrics));
    }

    [Fact]
    public void UnElencoVuotoHaUnaRigaSola()
    {
        var metrics = _layout.Measure(double.PositiveInfinity, RibbonBandSpec.Default, 0, 2);

        Assert.Equal(1, metrics.TotalRows);
        Assert.Equal(0, metrics.TileHeight);
        Assert.Equal(0, metrics.LastFirstRow);
    }

    [Fact]
    public void LaProporzioneDellaPresentazioneVinceSuQuellaDichiarata()
    {
        var metrics = _layout.Measure(90, new RibbonBandSpec(1, 4, 16.0 / 9.0) { Proportion = () => 4.0 / 3.0 }, 4, 2);

        Assert.Equal(120, metrics.TileWidth, 3);
    }
}
