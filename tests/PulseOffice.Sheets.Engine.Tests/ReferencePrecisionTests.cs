using PulseOffice.Sheets.Addressing;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class ReferencePrecisionTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    [Theory]
    [InlineData("=GAMMA.INV(0.05;0.5;2)", 0.00393214000001952)]
    [InlineData("=GAMMA.INV(0.999;3;1)", 11.2288722424126)]
    [InlineData("=GAMMA.INV(0.001;10;0.5)", 1.48026018637188)]
    [InlineData("=BETA.INV(0.001;0.5;0.5)", 2.46739907091695E-06)]
    [InlineData("=BETA.INV(0.999;5;2)", 0.991744507212253)]
    [InlineData("=T.INV(0.0001;1)", -3183.09875711899)]
    [InlineData("=T.INV(0.975;30)", 2.04227245630124)]
    [InlineData("=CHISQ.INV(0.01;2)", 0.0201006717070029)]
    [InlineData("=CHISQ.INV(0.999;1)", 10.8275661706628)]
    [InlineData("=F.INV(0.01;2;3)", 0.0100840809281763)]
    [InlineData("=F.INV(0.95;10;20)", 2.34787756699831)]
    [InlineData("=NORM.S.INV(1E-10)", -6.36134090240406)]
    [InlineData("=BESSELJ(10;3)", 0.0583793793051869)]
    [InlineData("=BESSELJ(0.1;5)", 2.60308179096444E-09)]
    [InlineData("=BESSELJ(30;1)", -0.118751062616623)]
    [InlineData("=BESSELJ(50;0)", 0.055812327669252)]
    [InlineData("=BESSELJ(100;20)", 0.0622174584983387)]
    [InlineData("=BESSELJ(-2.5;3)", -0.216600391039114)]
    [InlineData("=BESSELJ(5;30)", 2.6711772782508E-21)]
    [InlineData("=BESSELI(30;2)", 730436828561.381)]
    [InlineData("=BESSELI(-2;3)", -0.212739959239853)]
    [InlineData("=BESSELI(100;0)", 1.07375170713107E+42)]
    [InlineData("=BESSELI(50;3)", 2.67776413888394E+20)]
    [InlineData("=BESSELI(-30;1)", -768532038938.957)]
    [InlineData("=BESSELY(50;3)", 0.0644591220602221)]
    [InlineData("=BESSELK(20;4)", 8.47423394950491E-10)]
    public void Le_funzioni_numeriche_coincidono_con_il_riferimento(string formula, double reference)
    {
        var (_, engine) = fixture.CreateWorkbook();
        var cell = new SheetCell(0, new CellAddress(0, 0));
        engine.SetFormula(cell, formula);
        var value = engine.GetValue(cell).AsNumber();
        Assert.True(Math.Abs(value - reference) <= 1e-12 * Math.Abs(reference), $"{formula}: {value} invece di {reference}");
    }
}
