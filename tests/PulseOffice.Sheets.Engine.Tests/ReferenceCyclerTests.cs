using PulseOffice.Sheets.Engine.Operations;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class ReferenceCyclerTests
{
    private static readonly ReferenceCycler Cycler = new();

    [Fact]
    public void IlGiroCompletoTornaAlPuntoDiPartenza()
    {
        var formula = "=A1";

        formula = Cycler.Cycle(formula, 2);
        Assert.Equal("=$A$1", formula);

        formula = Cycler.Cycle(formula, 2);
        Assert.Equal("=A$1", formula);

        formula = Cycler.Cycle(formula, 2);
        Assert.Equal("=$A1", formula);

        formula = Cycler.Cycle(formula, 2);
        Assert.Equal("=A1", formula);
    }

    [Fact]
    public void SiCambiaSoloIlRiferimentoSottoIlCursore()
    {
        Assert.Equal("=A1+$B$2", Cycler.Cycle("=A1+B2", 6));
    }

    [Fact]
    public void SenzaCursoreDentroUnRiferimentoSiPrendeLUltimo()
    {
        Assert.Equal("=A1+$B$2", Cycler.Cycle("=A1+B2", 0));
    }

    [Fact]
    public void UnaFormulaSenzaRiferimentiNonCambia()
    {
        Assert.Equal("=2+3", Cycler.Cycle("=2+3", 1));
    }

    [Theory]
    [InlineData("=LOG10(A1)", 9, "=LOG10($A$1)")]
    [InlineData("=ATAN2(B1;C1)", 12, "=ATAN2(B1;$C$1)")]
    [InlineData("=\"AB1\"&C2", 4, "=\"AB1\"&$C$2")]
    [InlineData("=SUM(A1:B5)", 8, "=SUM($A$1:$B$5)")]
    [InlineData("=SUM($A$1:B5)", 6, "=SUM(A$1:B$5)")]
    [InlineData("='AB1'.C3", 9, "='AB1'.$C$3")]
    public void NonSiToccanoNomiDiFunzioneStringheEGliIntervalliCambianoInteri(string formula, int caret, string expected) =>
        Assert.Equal(expected, Cycler.Cycle(formula, caret));

    [Fact]
    public void IlCursoreFinisceDopoIlRiferimento()
    {
        var cycled = Cycler.CycleAt("=A1:B2+1", 3);
        Assert.Equal("=$A$1:$B$2+1", cycled.Text);
        Assert.Equal(10, cycled.Caret);
    }
}
