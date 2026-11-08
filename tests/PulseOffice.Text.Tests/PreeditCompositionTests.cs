using PulseOffice.Text.Input;
using Xunit;

namespace PulseOffice.Text.Tests;

public sealed class PreeditCompositionTests
{
    private readonly PreeditComposer _composer = new();

    [Fact]
    public void LaComposizioneSiInserisceAlCursore()
    {
        var composition = _composer.Compose("ab", 1, 1, "にほ", null);
        Assert.Equal("aにほb", composition.Text);
        Assert.Equal((1, 2, 3), (composition.Start, composition.Length, composition.Caret));
    }

    [Fact]
    public void LaComposizioneSostituisceLaSelezioneEUsaIlSuoCursore()
    {
        var composition = _composer.Compose("abcd", 3, 1, "xyz", 1);
        Assert.Equal("axyzd", composition.Text);
        Assert.Equal(2, composition.Caret);
    }

    [Fact]
    public void SenzaComposizioneIlTestoResta()
    {
        var composition = _composer.Compose("abc", 2, 2, null, null);
        Assert.False(composition.IsActive);
        Assert.Equal("abc", composition.Text);
        Assert.Equal(2, composition.Caret);
    }
}
