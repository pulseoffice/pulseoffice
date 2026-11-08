using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class ArrayBroadcastTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private CellValue Evaluate(string formula)
    {
        var (_, engine) = fixture.CreateWorkbook();
        engine.SetFormula(new SheetCell(0, CellAddress.First), formula);
        engine.RecalculateAll();
        return engine.GetValue(new SheetCell(0, CellAddress.First));
    }

    [Fact]
    public void ConMatriciDiverseIlRisultatoHaLaDimensioneMinoreComeNelRiferimento()
    {
        Assert.Equal(2d, Evaluate("=ROWS({1|2|3}+{10|20})").AsNumber());
        Assert.Equal(33d, Evaluate("=SUMPRODUCT({1|2|3}+{10|20})").AsNumber());
    }

    [Fact]
    public void UnaRigaSolaSiEstendeATutteLeRighe() =>
        Assert.Equal(13d, Evaluate("=INDEX({1|2|3}+{10};3)").AsNumber());

    [Fact]
    public void UnaColonnaSolaSiEstendeATutteLeColonne() =>
        Assert.Equal(32d, Evaluate("=INDEX({1|2}+{10;20;30};2;3)").AsNumber());

    [Fact]
    public void LeColonneSiFermanoAllaMatricePiuStretta() =>
        Assert.Equal(2d, Evaluate("=COLUMNS({1;2|3;4}+{1;2;3|4;5;6})").AsNumber());
}
