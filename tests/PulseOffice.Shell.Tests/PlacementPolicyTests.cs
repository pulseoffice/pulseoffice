using Xunit;
using PulseOffice.Foundation.Windowing;

namespace PulseOffice.Shell.Tests;

public sealed class PlacementPolicyTests
{
    private static readonly ScreenArea Primary = new(0, 0, 1920, 1080);

    private static readonly WindowPlacement Fallback = new(0, 0, 1280, 800, Maximized: false);

    private readonly PlacementPolicy _policy = new();

    private WindowPlacement Resolve(WindowPlacement? saved, params ScreenArea[] screens) =>
        _policy.Resolve(saved, screens.Length == 0 ? [Primary] : screens, Fallback, 720, 480);

    [Fact]
    public void SenzaCollocazioneSalvataLaFinestraNasceCentrata()
    {
        var resolved = Resolve(saved: null);

        Assert.Equal(320, resolved.X);
        Assert.Equal(140, resolved.Y);
        Assert.Equal(1280, resolved.Width);
    }

    [Fact]
    public void UnaCollocazioneVisibileRestaDovEra()
    {
        var saved = new WindowPlacement(200, 150, 1000, 700, Maximized: false);

        Assert.Equal(saved, Resolve(saved));
    }

    [Fact]
    public void UnaFinestraFuoriDalloSchermoTornaAlCentro()
    {
        var saved = new WindowPlacement(5000, 4000, 1000, 700, Maximized: false);

        var resolved = Resolve(saved);

        Assert.Equal(460, resolved.X);
        Assert.Equal(190, resolved.Y);
    }

    [Fact]
    public void UnaFinestraSporgenteVieneRiportataDentro()
    {
        var saved = new WindowPlacement(1200, 500, 1000, 700, Maximized: false);

        var resolved = Resolve(saved);

        Assert.Equal(920, resolved.X);
        Assert.Equal(380, resolved.Y);
    }

    [Fact]
    public void LaFinestraNonSuperaMaiLoSchermoCheLaOspita()
    {
        var saved = new WindowPlacement(0, 0, 4000, 3000, Maximized: false);

        var resolved = Resolve(saved);

        Assert.Equal(1920, resolved.Width);
        Assert.Equal(1080, resolved.Height);
    }

    [Fact]
    public void UnaMisuraSottoIlMinimoRicadeSuiValoriDiPartenza()
    {
        var saved = new WindowPlacement(100, 100, 40, 30, Maximized: false);

        var resolved = Resolve(saved);

        Assert.Equal(Fallback.Width, resolved.Width);
        Assert.Equal(Fallback.Height, resolved.Height);
    }

    [Fact]
    public void ConDueSchermiLaFinestraRestaSuQuelloDiPrima()
    {
        var second = new ScreenArea(1920, 0, 1920, 1080);
        var saved = new WindowPlacement(2100, 200, 1000, 700, Maximized: false);

        Assert.Equal(saved, Resolve(saved, Primary, second));
    }

    [Fact]
    public void SpentoIlSecondoSchermoLaFinestraTornaSulPrimo()
    {
        var saved = new WindowPlacement(2100, 200, 1000, 700, Maximized: false);

        var resolved = Resolve(saved, Primary);

        Assert.True(resolved.X >= Primary.X);
        Assert.True(resolved.Right <= Primary.Right);
    }

    [Fact]
    public void CoordinateNonValideRicadonoSuiValoriDiPartenza()
    {
        var saved = new WindowPlacement(double.NaN, 0, 1000, 700, Maximized: false);

        var resolved = Resolve(saved);

        Assert.Equal(Fallback.Width, resolved.Width);
        Assert.Equal(320, resolved.X);
    }

    [Fact]
    public void LoStatoIngranditoSiConserva()
    {
        var saved = new WindowPlacement(100, 100, 1000, 700, Maximized: true);

        Assert.True(Resolve(saved).Maximized);
    }

    [Fact]
    public void UnMinimoPiuGrandeDelloSchermoNonFaCadere()
    {
        var resolved = _policy.Resolve(null, [new ScreenArea(0, 0, 640, 400)], Fallback, 720, 480);

        Assert.Equal(720, resolved.Width);
        Assert.Equal(480, resolved.Height);
    }

    [Fact]
    public void SenzaSchermiUnaCollocazioneInsensataNonSiUsa()
    {
        var broken = new WindowPlacement(double.NaN, 0, 10, 10, Maximized: false);

        Assert.Equal(Fallback, _policy.Resolve(broken, [], Fallback, 720, 480));
    }

    [Fact]
    public void UnaLarghezzaInfinitaSenzaSchermiTornaAllaCollocazioneDiRiserva()
    {
        var saved = new WindowPlacement(10, 10, double.PositiveInfinity, 700, Maximized: false);

        var resolved = _policy.Resolve(saved, [], Fallback, 720, 480);

        Assert.Equal(Fallback, resolved);
    }
}
