using PulseOffice.Foundation.Windowing;
using PulseOffice.Shell;
using Xunit;

namespace PulseOffice.Shell.Tests;

public class PlacementPixelMapperTests
{
    private readonly PlacementPixelMapper _mapper = new();

    private readonly IReadOnlyList<PlacementScreen> _mixed =
    [
        new(new ScreenArea(0, 0, 1920, 1080), 1),
        new(new ScreenArea(1920, 0, 2560, 1440), 2),
    ];

    [Fact]
    public void UnaFinestraSulSecondoSchermoTornaSulSecondoSchermoConLaSuaScala()
    {
        var saved = _mapper.FromWindow(2000, 100, 800, 600, 2);

        var restored = _mapper.ToWindow(saved, _mixed, 1);

        Assert.Equal(new WindowFrame(2000, 100, 800, 600), restored);
    }

    [Fact]
    public void UnaFinestraSulPrimoSchermoNonCambia()
    {
        var saved = _mapper.FromWindow(100, 50, 800, 600, 1);

        Assert.Equal(new WindowFrame(100, 50, 800, 600), _mapper.ToWindow(saved, _mixed, 2));
    }

    [Fact]
    public void LaStessaPosizioneLogicaSuSchermiDiversiNonSiConfonde()
    {
        var first = _mapper.FromWindow(1000, 100, 400, 300, 1);
        var second = _mapper.FromWindow(2000, 100, 400, 300, 2);

        Assert.NotEqual(first.X, second.X);
    }

    [Fact]
    public void SenzaSchermiSiUsaLaScalaDiRiserva()
    {
        Assert.Equal(new WindowFrame(10, 20, 50, 25), _mapper.ToWindow(new WindowPlacement(10, 20, 100, 50, false), [], 2));
    }
}
