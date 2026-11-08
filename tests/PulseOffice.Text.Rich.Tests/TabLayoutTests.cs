using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Text.Rich.Tests;

public sealed class TabLayoutTests
{
    private readonly TabLayout _tabs = new();

    private double Width(int start, int end) => (end - start) * 10.0;

    [Fact]
    public void IlTestoDopoLaTabulazioneVaAllaTabulazioneSinistra()
    {
        var pieces = _tabs.Place("ab\tcd", 0, 5, 0, 0, [new TabStop(100)], 50, Width);

        Assert.Equal([new TabPiece(0, 2, 0), new TabPiece(3, 5, 100)], pieces);
    }

    [Fact]
    public void LeTabulazioniDestraCentraleEDecimaleAllineanoIlPezzo()
    {
        var right = _tabs.Place("a\tbcd", 0, 5, 10, 10, [new TabStop(100) { Alignment = TabAlignment.Right }], 50, Width);
        var center = _tabs.Place("a\tbcd", 0, 5, 10, 10, [new TabStop(100) { Alignment = TabAlignment.Center }], 50, Width);
        var decimals = _tabs.Place("a\t12.5", 0, 6, 10, 10, [new TabStop(100) { Alignment = TabAlignment.Decimal }], 50, Width);

        Assert.Equal(110 - 30, right[1].X);
        Assert.Equal(110 - 15, center[1].X);
        Assert.Equal(110 - 20, decimals[1].X);
    }

    [Fact]
    public void SenzaTabulazioniSiUsaIlPassoPredefinito()
    {
        var pieces = _tabs.Place("abcdef\tx\ty", 0, 10, 0, 0, null, 50, Width);

        Assert.Equal(100, pieces[1].X);
        Assert.Equal(150, pieces[2].X);
    }

    [Fact]
    public void UnaTabulazioneInFondoLasciaIlPostoAlCursore()
    {
        var pieces = _tabs.Place("ab\t", 0, 3, 0, 0, [new TabStop(100)], 50, Width);

        Assert.Equal(new TabPiece(3, 3, 100), pieces[^1]);
    }
}
