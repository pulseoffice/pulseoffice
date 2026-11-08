using PulseOffice.UI.Controls;
using Xunit;

namespace PulseOffice.Shell.Tests;

public class NumericBoundsTests
{
    [Fact]
    public void IlValoreInizialeFuoriIntervalloSiRiportaDentro()
    {
        var bounds = new NumericBounds(1, 10);

        Assert.Equal(1, bounds.Clamp(0));
        Assert.Equal(10, bounds.Clamp(50));
        Assert.Equal(1, bounds.Clamp(double.NaN));
    }

    [Fact]
    public void UnCampoSvuotatoRestituisceIlValoreInizialeNelLimite()
    {
        var bounds = new NumericBounds(1, 10);

        Assert.Equal(5, bounds.Read(null, 5));
        Assert.Equal(1, bounds.Read(null, -3));
        Assert.Equal(7, bounds.Read(7m, 5));
    }

    [Fact]
    public void LimitiInfinitiOInvertitiEPassoNonValidoSiNormalizzano()
    {
        var bounds = new NumericBounds(double.PositiveInfinity, double.NegativeInfinity, double.NaN);

        Assert.Equal(-NumericBounds.Limit, bounds.Minimum);
        Assert.Equal(NumericBounds.Limit, bounds.Maximum);
        Assert.Equal(1, bounds.Step);
        Assert.Equal(NumericBounds.Limit, (double)bounds.ClampDecimal(double.MaxValue));
    }
}
